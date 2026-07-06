using Godot;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World.Sites;

public partial class WorldBuildingOptionCard : Button
{
    [Signal]
    public delegate void SelectedEventHandler(string buildingDefinitionId);

    private TextureRect _icon;
    private Label _nameLabel;
    private string _buildingDefinitionId = "";
    private string _displayName = "建筑";
    private string _iconPath = "";
    private int _footprintWidth = 1;
    private int _footprintHeight = 1;
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
        string buildingDefinitionId,
        string displayName,
        string iconPath,
        int footprintWidth,
        int footprintHeight,
        string costText,
        bool selectable,
        string disabledReason = "")
    {
        _buildingDefinitionId = buildingDefinitionId ?? "";
        _displayName = string.IsNullOrWhiteSpace(displayName) ? "建筑" : displayName.Trim();
        _iconPath = iconPath ?? "";
        _footprintWidth = System.Math.Max(1, footprintWidth);
        _footprintHeight = System.Math.Max(1, footprintHeight);
        _costText = string.IsNullOrWhiteSpace(costText) ? "无" : costText.Trim();
        _selectable = selectable;
        _disabledReason = string.IsNullOrWhiteSpace(disabledReason) ? "未知原因" : disabledReason.Trim();
        ApplyBinding();
    }

    // Keep the inventory card compact; construction details live in the hover panel.
    public override Control _MakeCustomTooltip(string forText)
    {
        if (string.IsNullOrWhiteSpace(forText))
        {
            return null;
        }

        WorldBuildingOptionTooltip tooltip = GameUiSceneFactory.CreateWorldBuildingOptionTooltip(nameof(WorldBuildingOptionCard));
        if (tooltip == null)
        {
            return null;
        }

        tooltip.Bind(
            _displayName,
            $"占地 {_footprintWidth}x{_footprintHeight}",
            $"成本 {_costText}",
            _selectable ? "" : _disabledReason);
        return tooltip;
    }

    private void ApplyBinding()
    {
        // Unavailable cards still need hover detail to explain costs and failure reasons.
        // Selection authority stays in OnPressed via _selectable instead of BaseButton.Disabled.
        Disabled = false;
        TooltipText = _displayName;
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
            EmitSignal(SignalName.Selected, _buildingDefinitionId);
        }
    }
}
