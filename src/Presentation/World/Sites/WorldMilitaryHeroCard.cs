using Godot;

namespace Rpg.Presentation.World.Sites;

public partial class WorldMilitaryHeroCard : Button
{
    [Signal]
    public delegate void SelectedEventHandler(string heroId);

    private TextureRect _avatar;
    private Label _nameLabel;
    private Label _corpsLabel;
    private string _heroId = "";
    private string _displayName = "";
    private Texture2D _previewTexture;
    private string _corpsText = "";
    private bool _selected;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        _avatar = GetNodeOrNull<TextureRect>("Content/PreviewSlot/Avatar") ?? GetNodeOrNull<TextureRect>("Content/AvatarFrame/Avatar");
        _nameLabel = GetNodeOrNull<Label>("Content/TextStack/NameLabel");
        _corpsLabel = GetNodeOrNull<Label>("Content/TextStack/CorpsLabel");
        Pressed += OnPressed;
        ApplyBinding();
    }

    public override void _ExitTree()
    {
        Pressed -= OnPressed;
    }

    public void Bind(
        string heroId,
        string displayName,
        Texture2D previewTexture,
        string corpsDisplayName,
        bool selected)
    {
        _heroId = heroId ?? "";
        _displayName = string.IsNullOrWhiteSpace(displayName) ? "英雄" : displayName.Trim();
        _previewTexture = previewTexture;
        _corpsText = string.IsNullOrWhiteSpace(corpsDisplayName) ? "未配置编制" : $"当前：{corpsDisplayName.Trim()}";
        _selected = selected;
        ApplyBinding();
    }

    private void ApplyBinding()
    {
        ButtonPressed = _selected;
        TooltipText = _corpsText;
        SelfModulate = _selected
            ? new Color(1.0f, 0.92f, 0.72f, 1.0f)
            : Colors.White;

        if (_avatar != null)
        {
            _avatar.Texture = _previewTexture;
        }

        if (_nameLabel != null)
        {
            _nameLabel.Text = _displayName;
        }

        if (_corpsLabel != null)
        {
            _corpsLabel.Text = _corpsText;
        }
    }

    private void OnPressed()
    {
        EmitSignal(SignalName.Selected, _heroId);
    }
}
