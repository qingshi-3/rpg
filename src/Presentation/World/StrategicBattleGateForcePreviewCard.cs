using Godot;

namespace Rpg.Presentation.World;

public partial class StrategicBattleGateForcePreviewCard : PanelContainer
{
    private TextureRect _preview;
    private Label _nameLabel;
    private Label _countLabel;
    private string _displayName = "";
    private string _countText = "";
    private Texture2D _previewTexture;

    public override void _Ready()
    {
        _preview = GetNodeOrNull<TextureRect>("Content/Preview");
        _nameLabel = GetNodeOrNull<Label>("Content/TextStack/NameLabel");
        _countLabel = GetNodeOrNull<Label>("Content/TextStack/CountLabel");
        ApplyBinding();
    }

    public void Bind(
        string displayName,
        string countText,
        Texture2D previewTexture)
    {
        _displayName = string.IsNullOrWhiteSpace(displayName) ? "未命名部队" : displayName.Trim();
        _countText = string.IsNullOrWhiteSpace(countText) ? "" : countText.Trim();
        _previewTexture = previewTexture;
        ApplyBinding();
    }

    private void ApplyBinding()
    {
        if (_preview != null)
        {
            _preview.Texture = _previewTexture;
        }

        if (_nameLabel != null)
        {
            _nameLabel.Text = _displayName;
        }

        if (_countLabel != null)
        {
            _countLabel.Text = _countText;
        }
    }
}
