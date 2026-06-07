using Godot;

namespace Rpg.Presentation.World.Sites;

public partial class BattleRuntimeSkillSlot : PanelContainer
{
    [Signal]
    public delegate void PressedEventHandler(string skillId);

    private Label _nameLabel;
    private ColorRect _statusOverlay;
    private Label _statusLabel;
    private string _skillId = "";
    private string _displayName = "";
    private bool _available;

    public string StatusText { get; private set; } = "";
    public double CooldownRemainingSeconds { get; private set; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        _nameLabel = GetNodeOrNull<Label>("Margin/Stack/Name");
        _statusOverlay = GetNodeOrNull<ColorRect>("StatusOverlay");
        _statusLabel = GetNodeOrNull<Label>("StatusOverlay/Status");
        ApplyBinding();
    }

    public void Bind(
        string skillId,
        string displayName,
        bool available,
        string statusText,
        double cooldownRemainingSeconds)
    {
        _skillId = skillId ?? "";
        _displayName = string.IsNullOrWhiteSpace(displayName) ? "技能" : displayName.Trim();
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
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (!_available)
        {
            return;
        }

        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            EmitSignal(SignalName.Pressed, _skillId);
            AcceptEvent();
        }
    }
}
