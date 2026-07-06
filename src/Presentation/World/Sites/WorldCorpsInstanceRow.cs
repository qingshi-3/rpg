using Godot;

namespace Rpg.Presentation.World.Sites;

public partial class WorldCorpsInstanceRow : Button
{
    [Signal]
    public delegate void ReplenishRequestedEventHandler(string corpsInstanceId);

    private TextureRect _icon;
    private Label _nameLabel;
    private Label _statusLabel;
    private Label _strengthLabel;
    private Label _metaLabel;
    private ProgressBar _strengthBar;
    private string _corpsInstanceId = "";
    private string _displayName = "编制";
    private Texture2D _previewTexture;
    private string _statusText = "";
    private string _costText = "";
    private string _disabledReason = "";
    private int _strength;
    private int _level;
    private int _equipmentLevel;
    private int _reserveCost;
    private bool _canReplenish;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        _icon = GetNodeOrNull<TextureRect>("Content/IconFrame/Icon");
        _nameLabel = GetNodeOrNull<Label>("Content/TextStack/NameLabel");
        _statusLabel = GetNodeOrNull<Label>("Content/TextStack/StatusLabel");
        _strengthLabel = GetNodeOrNull<Label>("Content/StrengthStack/StrengthLabel");
        _metaLabel = GetNodeOrNull<Label>("Content/StrengthStack/MetaLabel");
        _strengthBar = GetNodeOrNull<ProgressBar>("Content/StrengthStack/StrengthBar");
        Pressed += OnPressed;
        ApplyBinding();
    }

    public override void _ExitTree()
    {
        Pressed -= OnPressed;
    }

    public void Bind(
        string corpsInstanceId,
        string displayName,
        Texture2D previewTexture,
        int strength,
        int level,
        int equipmentLevel,
        string statusText,
        bool canReplenish,
        int reserveCost,
        string costText,
        string disabledReason)
    {
        _corpsInstanceId = corpsInstanceId ?? "";
        _displayName = string.IsNullOrWhiteSpace(displayName) ? "编制" : displayName.Trim();
        _previewTexture = previewTexture;
        _strength = Mathf.Clamp(strength, 0, 100);
        _level = Mathf.Max(0, level);
        _equipmentLevel = Mathf.Max(0, equipmentLevel);
        _statusText = string.IsNullOrWhiteSpace(statusText) ? "未知" : statusText.Trim();
        _canReplenish = canReplenish;
        _reserveCost = Mathf.Max(0, reserveCost);
        _costText = string.IsNullOrWhiteSpace(costText) ? "无" : costText.Trim();
        _disabledReason = disabledReason ?? "";
        ApplyBinding();
    }

    private void ApplyBinding()
    {
        Disabled = false;
        TooltipText = BuildTooltipText();
        SelfModulate = _canReplenish
            ? Colors.White
            : new Color(1.0f, 1.0f, 1.0f, 0.78f);

        if (_icon != null)
        {
            _icon.Texture = _previewTexture;
        }

        if (_nameLabel != null)
        {
            _nameLabel.Text = _displayName;
        }

        if (_statusLabel != null)
        {
            _statusLabel.Text = _statusText;
        }

        if (_strengthLabel != null)
        {
            _strengthLabel.Text = $"{_strength}/100";
        }

        if (_metaLabel != null)
        {
            _metaLabel.Text = $"Lv.{_level}  装备 {_equipmentLevel}";
        }

        if (_strengthBar != null)
        {
            _strengthBar.Value = _strength;
        }
    }

    private string BuildTooltipText()
    {
        if (_strength >= 100)
        {
            return "强度已满，无需补员";
        }

        return _canReplenish
            ? $"补员消耗：预备兵 {_reserveCost} / 成本 {_costText}"
            : $"暂不可补员：{(string.IsNullOrWhiteSpace(_disabledReason) ? "条件不足" : _disabledReason.Trim())}";
    }

    private void OnPressed()
    {
        if (_canReplenish && _strength < 100)
        {
            EmitSignal(SignalName.ReplenishRequested, _corpsInstanceId);
        }
    }
}
