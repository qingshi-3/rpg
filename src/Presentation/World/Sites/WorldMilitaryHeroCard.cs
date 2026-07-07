using Godot;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World.Sites;

public partial class WorldMilitaryHeroCard : Button
{
    [Signal]
    public delegate void SelectedEventHandler(string heroId);

    private BattleUnitPlinthPreview _preview;
    private Label _nameLabel;
    private Label _corpsLabel;
    private string _heroId = "";
    private string _displayName = "";
    private BattleUnitAnimatedPreviewModel _previewModel;
    private string _corpsText = "";
    private bool _selected;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        _preview = GetNodeOrNull<BattleUnitPlinthPreview>("Content/PreviewSlot/PlinthPreview");
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
        BattleUnitAnimatedPreviewModel preview,
        string corpsDisplayName,
        bool selected)
    {
        _heroId = heroId ?? "";
        _displayName = string.IsNullOrWhiteSpace(displayName) ? "英雄" : displayName.Trim();
        _previewModel = preview;
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

        if (_preview != null)
        {
            _preview.Bind(_previewModel);
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
