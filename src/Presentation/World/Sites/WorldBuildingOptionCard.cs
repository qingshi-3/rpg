using Godot;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World.Sites;

public partial class WorldBuildingOptionCard : Button
{
    private const int IconSlotWidth = 96;
    private const int IconSlotHeight = 70;

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
        _icon = GetNodeOrNull<TextureRect>("Content/IconSlot/Icon");
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
            Texture2D texture = string.IsNullOrWhiteSpace(_iconPath)
                ? null
                : GD.Load<Texture2D>(_iconPath);
            ApplyIconTexture(texture);
        }
    }

    private void ApplyIconTexture(Texture2D texture)
    {
        if (_icon == null)
        {
            return;
        }

        _icon.Texture = texture;
        _icon.TextureFilter = TextureFilterEnum.Nearest;
        _icon.StretchMode = TextureRect.StretchModeEnum.Scale;
        _icon.ExpandMode = TextureRect.ExpandModeEnum.KeepSize;
        if (texture == null)
        {
            _icon.CustomMinimumSize = Vector2.Zero;
            return;
        }

        Vector2I textureSize = new(
            System.Math.Max(1, texture.GetWidth()),
            System.Math.Max(1, texture.GetHeight()));
        int scale = CalculateIntegerIconScale(textureSize);
        _icon.CustomMinimumSize = new Vector2(
            textureSize.X * scale,
            textureSize.Y * scale);
    }

    private static int CalculateIntegerIconScale(Vector2I textureSize)
    {
        int fitWidthScale = System.Math.Max(1, IconSlotWidth / System.Math.Max(1, textureSize.X));
        int fitHeightScale = System.Math.Max(1, IconSlotHeight / System.Math.Max(1, textureSize.Y));
        return System.Math.Max(1, System.Math.Min(fitWidthScale, fitHeightScale));
    }

    private void OnPressed()
    {
        if (_selectable)
        {
            EmitSignal(SignalName.Selected, _buildingDefinitionId);
        }
    }
}
