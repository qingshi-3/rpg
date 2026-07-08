using Godot;

namespace Rpg.Presentation.Battle.Entities;

public partial class BattleUnitHealthBarComponent : BattleEntityComponent
{
    [Export]
    public NodePath RootPath { get; set; } = new("../HealthBarRoot");

    [Export]
    public NodePath BackPath { get; set; } = new("../HealthBarRoot/HealthBack");

    [Export]
    public NodePath NameStripPath { get; set; } = new("../HealthBarRoot/NameStrip");

    [Export]
    public NodePath NameLabelPath { get; set; } = new("../HealthBarRoot/UnitNameLabel");

    [Export]
    public NodePath TrackPath { get; set; } = new("../HealthBarRoot/HealthTrack");

    [Export]
    public NodePath FillPath { get; set; } = new("../HealthBarRoot/HealthFill");

    [Export]
    public NodePath ValueLabelPath { get; set; } = new("../HealthBarRoot/HealthValueLabel");

    [Export]
    public Vector2 BarSize { get; set; } = new(72f, 24f);

    [Export]
    public bool HideWhenFullHp { get; set; } = true;

    [Export(PropertyHint.Range, "0.1,1,0.05")]
    public float AttentionAlpha { get; set; } = 0.96f;

    // The nameplate reuses one red HP fill for readability; faction identity
    // lives on the title strip and border so HP state is never color-coded twice.
    [Export]
    public Color BorderColor { get; set; } = new(0.36f, 0.13f, 0.1f, 0.96f);

    [Export]
    public Color PlayerBorderColor { get; set; } = new(0.16f, 0.36f, 0.68f, 0.96f);

    [Export]
    public Color EnemyBorderColor { get; set; } = new(0.58f, 0.12f, 0.1f, 0.96f);

    [Export]
    public Color NeutralBorderColor { get; set; } = new(0.34f, 0.29f, 0.23f, 0.96f);

    [Export]
    public Color TrackColor { get; set; } = new(0.18f, 0.035f, 0.035f, 0.92f);

    [Export]
    public Color HealthFillColor { get; set; } = new(0.86f, 0.12f, 0.08f, 0.98f);

    private HealthComponent _health;
    private FactionComponent _faction;
    private BattleUnitOverlayAnchorComponent _overlayAnchor;
    private Control _root;
    private ColorRect _back;
    private ColorRect _nameStrip;
    private Label _nameLabel;
    private ColorRect _track;
    private ColorRect _fill;
    private Label _valueLabel;
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
        _faction = null;
        _overlayAnchor = null;
        _root = null;
        _back = null;
        _nameStrip = null;
        _nameLabel = null;
        _track = null;
        _fill = null;
        _valueLabel = null;
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
        _faction =
            Entity?.GetComponent<FactionComponent>() ??
            Entity?.GetNodeOrNull<FactionComponent>("FactionComponent");
        _overlayAnchor =
            Entity?.GetComponent<BattleUnitOverlayAnchorComponent>() ??
            Entity?.GetNodeOrNull<BattleUnitOverlayAnchorComponent>("BattleUnitOverlayAnchorComponent");
        _root = ResolveSibling<Control>(RootPath);
        _back = ResolveSibling<ColorRect>(BackPath);
        _nameStrip = ResolveSibling<ColorRect>(NameStripPath);
        _nameLabel = ResolveSibling<Label>(NameLabelPath);
        _track = ResolveSibling<ColorRect>(TrackPath);
        _fill = ResolveSibling<ColorRect>(FillPath);
        _valueLabel = ResolveSibling<Label>(ValueLabelPath);
        if (_root == null)
        {
            return;
        }

        // The nameplate is authored in the unit scene; this component only
        // enforces stable runtime dimensions so HP/name changes cannot resize it.
        _root.MouseFilter = Control.MouseFilterEnum.Ignore;
        ApplyBarGeometry();
    }

    private void ApplyBarGeometry()
    {
        if (_root == null)
        {
            return;
        }

        BorderColor = ResolveFactionPalette();
        _root.Position = ResolveBarPosition();
        _root.Size = BarSize;
        if (_back != null)
        {
            _back.MouseFilter = Control.MouseFilterEnum.Ignore;
            _back.Position = Vector2.Zero;
            _back.Size = BarSize;
            _back.Color = BorderColor;
        }

        if (_nameStrip != null)
        {
            _nameStrip.MouseFilter = Control.MouseFilterEnum.Ignore;
            _nameStrip.Position = new Vector2(1f, 1f);
            _nameStrip.Size = new Vector2(Mathf.Max(0f, BarSize.X - 2f), 11f);
            _nameStrip.Color = ResolveNameStripColor(BorderColor);
        }

        if (_nameLabel != null)
        {
            _nameLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
            _nameLabel.Position = new Vector2(2f, 0f);
            _nameLabel.Size = new Vector2(Mathf.Max(0f, BarSize.X - 4f), 12f);
            _nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _nameLabel.VerticalAlignment = VerticalAlignment.Center;
            _nameLabel.ClipText = true;
        }

        if (_track != null)
        {
            _track.MouseFilter = Control.MouseFilterEnum.Ignore;
            _track.Position = new Vector2(1f, 13f);
            _track.Size = new Vector2(Mathf.Max(0f, BarSize.X - 2f), 10f);
            _track.Color = TrackColor;
        }

        if (_fill != null)
        {
            _fill.MouseFilter = Control.MouseFilterEnum.Ignore;
            _fill.Position = new Vector2(2f, 14f);
        }

        if (_valueLabel != null)
        {
            _valueLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
            _valueLabel.Position = new Vector2(2f, 13f);
            _valueLabel.Size = new Vector2(Mathf.Max(0f, BarSize.X - 4f), 10f);
            _valueLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _valueLabel.VerticalAlignment = VerticalAlignment.Center;
            _valueLabel.ClipText = true;
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
        if (_nameLabel != null)
        {
            _nameLabel.Text = ResolveDisplayName();
        }

        if (_valueLabel != null)
        {
            _valueLabel.Text = $"{_health.Hp}/{maxHp}";
        }

        _fill.Position = new Vector2(2f, 14f);
        _fill.Size = new Vector2(Mathf.Max(0f, innerSize.X * ratio), innerSize.Y);
        _fill.Color = HealthFillColor;
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
            Mathf.Max(0f, BarSize.X - 4f),
            8f);
    }

    private Color ResolveFactionPalette()
    {
        return (_faction?.Faction ?? BattleFaction.Neutral) switch
        {
            BattleFaction.Player => PlayerBorderColor,
            BattleFaction.Enemy => EnemyBorderColor,
            _ => NeutralBorderColor
        };
    }

    private static Color ResolveNameStripColor(Color borderColor)
    {
        return new Color(
            borderColor.R * 0.78f,
            borderColor.G * 0.78f,
            borderColor.B * 0.78f,
            borderColor.A);
    }

    private string ResolveDisplayName()
    {
        string displayName = Entity?.DisplayName ?? "";
        return string.IsNullOrWhiteSpace(displayName)
            ? "\u5355\u4f4d"
            : displayName.Trim();
    }

    private T ResolveSibling<T>(NodePath path) where T : Node
    {
        string value = path?.ToString() ?? "";
        return string.IsNullOrWhiteSpace(value)
            ? null
            : GetNodeOrNull<T>(path);
    }
}
