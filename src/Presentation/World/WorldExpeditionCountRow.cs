using Godot;

namespace Rpg.Presentation.World;

public partial class WorldExpeditionCountRow : HBoxContainer
{
    private TextureRect _heroPreview;
    private TextureRect _corpsPreview;
    private Label _countLabel;
    private Button _minusButton;
    private Button _plusButton;
    private string _label = "";
    private Texture2D _heroPreviewTexture;
    private Texture2D _corpsPreviewTexture;
    private int _selected;
    private int _available;

    public override void _Ready()
    {
        _heroPreview = GetNodeOrNull<TextureRect>("HeroPreview");
        _corpsPreview = GetNodeOrNull<TextureRect>("CorpsPreview");
        _countLabel = GetNodeOrNull<Label>("CountLabel");
        _minusButton = GetNodeOrNull<Button>("MinusButton");
        _plusButton = GetNodeOrNull<Button>("PlusButton");
        ApplyBinding();
    }

    public void Bind(
        string label,
        Texture2D heroPreviewTexture,
        Texture2D corpsPreviewTexture,
        int selected,
        int available)
    {
        _label = string.IsNullOrWhiteSpace(label) ? "战斗编组" : label.Trim();
        _heroPreviewTexture = heroPreviewTexture;
        _corpsPreviewTexture = corpsPreviewTexture;
        _selected = Mathf.Max(0, selected);
        _available = Mathf.Max(0, available);
        ApplyBinding();
    }

    private void ApplyBinding()
    {
        if (_heroPreview != null)
        {
            _heroPreview.Texture = _heroPreviewTexture;
        }

        if (_corpsPreview != null)
        {
            _corpsPreview.Texture = _corpsPreviewTexture;
        }

        if (_countLabel != null)
        {
            _countLabel.Text = $"{_label} {_selected}/{_available}";
        }

        if (_minusButton != null)
        {
            _minusButton.Disabled = _selected <= 0;
        }

        if (_plusButton != null)
        {
            _plusButton.Disabled = _selected >= _available;
        }
    }
}
