using Godot;

namespace Rpg.Presentation.Battle.Entities;

public partial class BattleUnitHealthBarComponent : BattleEntityComponent
{
    [Export]
    public NodePath RootPath { get; set; } = new("../HealthBarRoot");

    [Export]
    public NodePath BackPath { get; set; } = new("../HealthBarRoot/HealthBack");

    [Export]
    public NodePath TrackPath { get; set; } = new("../HealthBarRoot/HealthTrack");

    [Export]
    public NodePath FillPath { get; set; } = new("../HealthBarRoot/HealthFill");

    [Export]
    public Vector2 BarSize { get; set; } = new(36f, 5f);

    [Export]
    public bool HideWhenFullHp { get; set; } = true;

    [Export(PropertyHint.Range, "0.1,1,0.05")]
    public float AttentionAlpha { get; set; } = 0.96f;

    // Head bars are short-lived attention hints. Persistent battle readability lives
    // in the runtime HUD so damaged units do not permanently add world-space noise.
    [Export]
    public Color BorderColor { get; set; } = new(0.018f, 0.02f, 0.018f, 0.78f);

    [Export]
    public Color TrackColor { get; set; } = new(0.06f, 0.07f, 0.06f, 0.58f);

    [Export]
    public Color HighHpColor { get; set; } = new(0.34f, 0.78f, 0.38f, 0.92f);

    [Export]
    public Color MidHpColor { get; set; } = new(0.82f, 0.64f, 0.24f, 0.92f);

    [Export]
    public Color LowHpColor { get; set; } = new(0.82f, 0.25f, 0.20f, 0.92f);

    private HealthComponent _health;
    private BattleUnitOverlayAnchorComponent _overlayAnchor;
    private Control _root;
    private ColorRect _back;
    private ColorRect _track;
    private ColorRect _fill;
    private int _lastHp = -1;
    private bool _attentionVisible;
    private bool _hoverVisible;

    protected override void OnAttached()
    {
        ResolveNodes();
        BindHealth();
        SetProcess(false);
        Refresh();
    }

    public override void _ExitTree()
    {
        if (_health != null)
        {
            _health.HealthChanged -= OnHealthChanged;
        }

        _health = null;
        _overlayAnchor = null;
        _root = null;
        _back = null;
        _track = null;
        _fill = null;
    }

    public void SetAttentionVisible(bool visible)
    {
        if (_attentionVisible == visible)
        {
            return;
        }

        _attentionVisible = visible;
        Refresh();
    }

    public void SetHoverVisible(bool visible)
    {
        if (_hoverVisible == visible)
        {
            return;
        }

        _hoverVisible = visible;
        Refresh();
    }

    public void HideImmediately()
    {
        // Defeated units keep their sprite alive for the death cue, but HP UI
        // belongs to live/targetable state and must disappear before that cue.
        _attentionVisible = false;
        _hoverVisible = false;
        SetProcess(false);
        if (_root != null)
        {
            _root.Visible = false;
            _root.QueueRedraw();
        }
    }

    private void ResolveNodes()
    {
        _overlayAnchor =
            Entity?.GetComponent<BattleUnitOverlayAnchorComponent>() ??
            Entity?.GetNodeOrNull<BattleUnitOverlayAnchorComponent>("BattleUnitOverlayAnchorComponent");
        _root = ResolveSibling<Control>(RootPath);
        _back = ResolveSibling<ColorRect>(BackPath);
        _track = ResolveSibling<ColorRect>(TrackPath);
        _fill = ResolveSibling<ColorRect>(FillPath);
        if (_root == null)
        {
            return;
        }

        // The bar is authored in the unit scene; this component only enforces
        // stable runtime dimensions so HP changes cannot resize surrounding UI.
        _root.MouseFilter = Control.MouseFilterEnum.Ignore;
        ApplyBarGeometry();
    }

    private void ApplyBarGeometry()
    {
        if (_root == null)
        {
            return;
        }

        _root.Position = ResolveBarPosition();
        _root.Size = BarSize;
        Vector2 innerOffset = new(1f, 1f);
        Vector2 innerSize = ResolveInnerBarSize();
        if (_back != null)
        {
            _back.MouseFilter = Control.MouseFilterEnum.Ignore;
            _back.Position = Vector2.Zero;
            _back.Size = BarSize;
            _back.Color = BorderColor;
        }

        if (_track != null)
        {
            _track.MouseFilter = Control.MouseFilterEnum.Ignore;
            _track.Position = innerOffset;
            _track.Size = innerSize;
            _track.Color = TrackColor;
        }

        if (_fill != null)
        {
            _fill.MouseFilter = Control.MouseFilterEnum.Ignore;
            _fill.Position = innerOffset;
            _fill.Size = innerSize;
        }
    }

    private void BindHealth()
    {
        _health = Entity?.GetComponent<HealthComponent>();
        if (_health != null)
        {
            _lastHp = _health.Hp;
            _health.HealthChanged += OnHealthChanged;
        }
    }

    private void OnHealthChanged()
    {
        if (_health != null)
        {
            _lastHp = _health.Hp;
        }

        Refresh();
    }

    private void Refresh()
    {
        if (_root == null || _fill == null || _health == null)
        {
            return;
        }

        ApplyBarGeometry();

        int maxHp = System.Math.Max(1, _health.MaxHp);
        float ratio = Mathf.Clamp(_health.Hp / (float)maxHp, 0f, 1f);
        Vector2 innerSize = ResolveInnerBarSize();

        // Defeat is emitted after HealthChanged, so hiding here removes the HP bar
        // on the zero-HP frame before BattleUnitRoot starts the death animation.
        _root.Visible = ShouldShowHealthBar(maxHp, ratio);
        _root.Modulate = new Color(1f, 1f, 1f, ResolveVisibilityAlpha());
        _fill.Position = new Vector2(1f, 1f);
        _fill.Size = new Vector2(Mathf.Max(0f, innerSize.X * ratio), innerSize.Y);
        _fill.Color = ResolveFillColor(ratio);
        _root.QueueRedraw();
    }

    private Vector2 ResolveBarPosition()
    {
        if (_overlayAnchor != null)
        {
            return _overlayAnchor.ResolveHeadOverlayPosition(BarSize);
        }

        return new Vector2(-BarSize.X * 0.5f, -44f);
    }

    private bool ShouldShowHealthBar(int maxHp, float ratio)
    {
        if (maxHp <= 1 || _health.IsDead)
        {
            return false;
        }

        if (!_attentionVisible && !_hoverVisible)
        {
            return false;
        }

        if (HideWhenFullHp && ratio >= 0.999f)
        {
            return false;
        }

        return true;
    }

    private float ResolveVisibilityAlpha()
    {
        return Mathf.Clamp(AttentionAlpha, 0f, 1f);
    }

    private Vector2 ResolveInnerBarSize()
    {
        return new Vector2(
            Mathf.Max(0f, BarSize.X - 2f),
            Mathf.Max(1f, BarSize.Y - 2f));
    }

    private Color ResolveFillColor(float ratio)
    {
        if (ratio <= 0.25f)
        {
            return LowHpColor;
        }

        if (ratio <= 0.55f)
        {
            return MidHpColor;
        }

        return HighHpColor;
    }

    private T ResolveSibling<T>(NodePath path) where T : Node
    {
        string value = path?.ToString() ?? "";
        return string.IsNullOrWhiteSpace(value)
            ? null
            : GetNodeOrNull<T>(path);
    }
}
