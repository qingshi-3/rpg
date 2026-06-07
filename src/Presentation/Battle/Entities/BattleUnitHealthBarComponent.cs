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

    [Export(PropertyHint.Range, "0.2,4,0.05")]
    public double DamagedVisibleSeconds { get; set; } = 2.4;

    [Export(PropertyHint.Range, "0,1,0.05")]
    public float LowHpVisibleRatio { get; set; } = 0.35f;

    [Export(PropertyHint.Range, "0.05,1,0.05")]
    public double RevealFadeSeconds { get; set; } = 0.25;

    [Export(PropertyHint.Range, "0.1,1,0.05")]
    public float HighHpAlpha { get; set; } = 0.45f;

    [Export(PropertyHint.Range, "0.1,1,0.05")]
    public float LowHpAlpha { get; set; } = 1f;

    // Muted defaults keep the HP state readable without pulling attention away
    // from small unit sprites; exported values remain available for per-unit tuning.
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
    private double _damageRevealSecondsRemaining;
    private bool _attentionVisible;

    protected override void OnAttached()
    {
        ResolveNodes();
        BindHealth();
        SetProcess(false);
        Refresh();
    }

    public override void _Process(double delta)
    {
        if (_damageRevealSecondsRemaining <= 0)
        {
            SetProcess(false);
            Refresh();
            return;
        }

        _damageRevealSecondsRemaining = System.Math.Max(0, _damageRevealSecondsRemaining - delta);
        Refresh();
        if (_damageRevealSecondsRemaining <= 0)
        {
            SetProcess(false);
        }
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

    public void HideImmediately()
    {
        // Defeated units keep their sprite alive for the death cue, but HP UI
        // belongs to live/targetable state and must disappear before that cue.
        _damageRevealSecondsRemaining = 0;
        _attentionVisible = false;
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
            if (_lastHp >= 0 && _health.Hp < _lastHp && !_health.IsDead)
            {
                _damageRevealSecondsRemaining = System.Math.Max(0, DamagedVisibleSeconds);
                SetProcess(_damageRevealSecondsRemaining > 0);
            }

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
        _root.Modulate = new Color(1f, 1f, 1f, ResolveVisibilityAlpha(ratio));
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

        if (HideWhenFullHp && ratio >= 0.999f)
        {
            return false;
        }

        if (ratio <= Mathf.Clamp(LowHpVisibleRatio, 0f, 1f))
        {
            return true;
        }

        if (_attentionVisible)
        {
            return true;
        }

        return _damageRevealSecondsRemaining > 0;
    }

    private float ResolveVisibilityAlpha(float ratio)
    {
        float hpAlpha = ResolveHpAlpha(ratio);
        if (ratio <= Mathf.Clamp(LowHpVisibleRatio, 0f, 1f))
        {
            return hpAlpha;
        }

        if (_attentionVisible)
        {
            return hpAlpha;
        }

        double fadeSeconds = System.Math.Max(0.01, RevealFadeSeconds);
        if (_damageRevealSecondsRemaining >= fadeSeconds)
        {
            return hpAlpha;
        }

        return hpAlpha * Mathf.Clamp((float)(_damageRevealSecondsRemaining / fadeSeconds), 0f, 1f);
    }

    private float ResolveHpAlpha(float ratio)
    {
        float lowThreshold = Mathf.Clamp(LowHpVisibleRatio, 0f, 0.99f);
        float lowAlpha = Mathf.Clamp(LowHpAlpha, 0f, 1f);
        float highAlpha = Mathf.Clamp(HighHpAlpha, 0f, lowAlpha);
        if (ratio <= lowThreshold)
        {
            return lowAlpha;
        }

        float t = Mathf.Clamp((ratio - lowThreshold) / (1f - lowThreshold), 0f, 1f);
        return Mathf.Lerp(lowAlpha, highAlpha, t);
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
