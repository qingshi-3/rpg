using Godot;

namespace Rpg.Presentation.World.Sites;

public partial class WorldMusterOptionCard : Button
{
    [Signal]
    public delegate void SelectedEventHandler(string corpsDefinitionId);

    private TextureRect _icon;
    private Label _nameLabel;
    private string _corpsDefinitionId = "";
    private string _displayName = "";
    private string _iconPath = "";
    private int _reserveCost;
    private string _costText = "";
    private string _disabledReason = "";
    private bool _selectable;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        _icon = GetNodeOrNull<TextureRect>("Content/Icon");
        _nameLabel = GetNodeOrNull<Label>("Content/NameLabel");
        Pressed += OnPressed;
        ApplyBinding();
    }

    public override void _ExitTree()
    {
        Pressed -= OnPressed;
    }

    public void Bind(
        string corpsDefinitionId,
        string displayName,
        string iconPath,
        int reserveCost,
        string costText,
        bool selectable,
        string disabledReason)
    {
        _corpsDefinitionId = corpsDefinitionId ?? "";
        _displayName = string.IsNullOrWhiteSpace(displayName) ? "编制" : displayName.Trim();
        _iconPath = iconPath ?? "";
        _reserveCost = System.Math.Max(0, reserveCost);
        _costText = string.IsNullOrWhiteSpace(costText) ? "无" : costText.Trim();
        _selectable = selectable;
        _disabledReason = disabledReason ?? "";
        ApplyBinding();
    }

    private void ApplyBinding()
    {
        Disabled = !_selectable;
        TooltipText = _selectable
            ? $"预备兵 {_reserveCost}\n成本 {_costText}"
            : $"预备兵 {_reserveCost}\n成本 {_costText}\n不可招募：{_disabledReason}";
        SelfModulate = _selectable
            ? Colors.White
            : new Color(1.0f, 1.0f, 1.0f, 0.62f);

        if (_nameLabel != null)
        {
            _nameLabel.Text = _displayName;
        }

        if (_icon != null)
        {
            _icon.Texture = string.IsNullOrWhiteSpace(_iconPath)
                ? null
                : GD.Load<Texture2D>(_iconPath);
        }
    }

    private void OnPressed()
    {
        if (_selectable)
        {
            EmitSignal(SignalName.Selected, _corpsDefinitionId);
        }
    }
}
