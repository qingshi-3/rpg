using Godot;

namespace Rpg.Presentation.World.Sites;

public partial class BattleRuntimeSkillSlot : PanelContainer
{
    [Signal]
    public delegate void PressedEventHandler(string skillDefinitionId);

    [Export]
    public StyleBox NormalPanelStyle { get; set; }

    [Export]
    public StyleBox HoverPanelStyle { get; set; }

    private Label _nameLabel;
    private Label _iconGlyphLabel;
    private TextureRect _iconTextureRect;
    private ColorRect _statusOverlay;
    private Label _statusLabel;
    private string _skillDefinitionId = "";
    private string _displayName = "";
    private string _iconText = "";
    private string _iconPath = "";
    private bool _available;
    private bool _hovered;

    public string StatusText { get; private set; } = "";
    public double CooldownRemainingSeconds { get; private set; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        MouseEntered += ApplyHoverPanelStyle;
        MouseExited += ApplyNormalPanelStyle;
        _nameLabel = GetNodeOrNull<Label>("Margin/Stack/Name");
        _iconGlyphLabel = GetNodeOrNull<Label>("Margin/Stack/GlyphPlate/IconGlyph");
        _iconTextureRect = GetNodeOrNull<TextureRect>("Margin/Stack/GlyphPlate/IconTexture");
        _statusOverlay = GetNodeOrNull<ColorRect>("StatusOverlay");
        _statusLabel = GetNodeOrNull<Label>("StatusOverlay/Status");
        ApplyBinding();
    }

    public void Bind(
        string skillDefinitionId,
        string displayName,
        string iconText,
        string iconPath,
        bool available,
        string statusText,
        double cooldownRemainingSeconds)
    {
        _skillDefinitionId = skillDefinitionId ?? "";
        _displayName = string.IsNullOrWhiteSpace(displayName) ? "技能" : displayName.Trim();
        _iconText = string.IsNullOrWhiteSpace(iconText) ? BuildFallbackIconText(_displayName) : iconText.Trim();
        _iconPath = iconPath?.Trim() ?? "";
        _available = available;
        StatusText = statusText ?? "";
        CooldownRemainingSeconds = System.Math.Max(0, cooldownRemainingSeconds);
        ApplyBinding();
    }

    private void ApplyBinding()
    {
        if (_nameLabel != null)
        {
            _nameLabel.Text = _displayName;
        }

        Texture2D iconTexture = LoadIconTexture(_iconPath);
        if (_iconTextureRect != null)
        {
            _iconTextureRect.Texture = iconTexture;
            _iconTextureRect.Visible = iconTexture != null;
        }

        if (_iconGlyphLabel != null)
        {
            _iconGlyphLabel.Text = _iconText;
            _iconGlyphLabel.Visible = iconTexture == null;
        }

        string visibleStatus = CooldownRemainingSeconds > 0.0
            ? System.Math.Ceiling(CooldownRemainingSeconds).ToString("0")
            : StatusText;
        if (_statusOverlay != null)
        {
            _statusOverlay.Visible = !string.IsNullOrWhiteSpace(visibleStatus);
        }

        if (_statusLabel != null)
        {
            _statusLabel.Text = visibleStatus;
        }

        TooltipText = _displayName;
        SelfModulate = _available
            ? Colors.White
            : new Color(1.0f, 1.0f, 1.0f, 0.72f);
        ApplyPanelStyle();
    }

    private void ApplyHoverPanelStyle()
    {
        _hovered = true;
        ApplyPanelStyle();
    }

    private void ApplyNormalPanelStyle()
    {
        _hovered = false;
        ApplyPanelStyle();
    }

    private void ApplyPanelStyle()
    {
        StyleBox style = _hovered && _available && HoverPanelStyle != null
            ? HoverPanelStyle
            : NormalPanelStyle;
        if (style != null)
        {
            AddThemeStyleboxOverride("panel", style);
        }
    }

    private static string BuildFallbackIconText(string displayName)
    {
        string trimmed = string.IsNullOrWhiteSpace(displayName) ? "" : displayName.Trim();
        return trimmed.Length == 0 ? "\u6280" : trimmed[..1];
    }

    private static Texture2D LoadIconTexture(string iconPath)
    {
        if (string.IsNullOrWhiteSpace(iconPath))
        {
            return null;
        }

        return GD.Load<Texture2D>(iconPath);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (!_available)
        {
            return;
        }

        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            EmitSignal(SignalName.Pressed, _skillDefinitionId);
            AcceptEvent();
        }
    }
}
