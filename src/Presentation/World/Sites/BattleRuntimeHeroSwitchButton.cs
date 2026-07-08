using Godot;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World.Sites;

public partial class BattleRuntimeHeroSwitchButton : Button
{
    [Signal]
    public delegate void SelectedEventHandler(string groupKey);

    private BattleUnitPlinthPreview _heroPlinthPreview;
    private Label _heroNameLabel;
    private ColorRect _selectedBackplate;
    private ColorRect _selectedSideMark;
    private string _groupKey = "";

    public override void _Ready()
    {
        ToggleMode = true;
        FocusMode = FocusModeEnum.None;
        MouseFilter = MouseFilterEnum.Stop;
        Text = "";
        _selectedBackplate = GetNodeOrNull<ColorRect>("SelectedBackplate");
        _selectedSideMark = GetNodeOrNull<ColorRect>("SelectedSideMark");
        _heroPlinthPreview = GetNodeOrNull<BattleUnitPlinthPreview>("HeroPlinthPreview");
        _heroNameLabel = GetNodeOrNull<Label>("HeroNameLabel");
    }

    public void Bind(string groupKey, string displayName, string heroBattleUnitId, bool selected, bool hasReadySkill, bool enabled)
    {
        _groupKey = groupKey ?? "";
        string label = string.IsNullOrWhiteSpace(displayName) ? "英雄" : displayName.Trim();
        Text = "";

        if (_heroNameLabel != null)
        {
            _heroNameLabel.Text = label;
        }

        BattleUnitAnimatedPreviewModel heroPreview = string.IsNullOrWhiteSpace(heroBattleUnitId)
            ? null
            : BattleUnitPreviewResolver.ResolveAnimatedPreview(heroBattleUnitId);
        if (_heroPlinthPreview != null)
        {
            bool hasPreview = heroPreview != null;
            _heroPlinthPreview.Visible = hasPreview;
            if (hasPreview)
            {
                _heroPlinthPreview.Bind(heroPreview);
            }
        }

        TooltipText = $"{label} / {(hasReadySkill ? "技能可用" : "技能锁定")}";
        Disabled = !enabled;
        ButtonPressed = selected;
        // Use scene-authored dark/cool selection pieces so the active hero remains readable on parchment UI.
        if (_selectedBackplate != null)
        {
            _selectedBackplate.Visible = selected;
        }

        if (_selectedSideMark != null)
        {
            _selectedSideMark.Visible = selected;
        }

        SelfModulate = enabled ? Colors.White : new Color(1f, 1f, 1f, 0.58f);
    }

    public override void _Pressed()
    {
        if (!string.IsNullOrWhiteSpace(_groupKey))
        {
            EmitSignal(SignalName.Selected, _groupKey);
        }
    }
}
