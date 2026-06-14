using Godot;

namespace Rpg.Presentation.World.Sites;

public partial class BattleRuntimeHeroSwitchButton : Button
{
    [Signal]
    public delegate void SelectedEventHandler(string groupKey);

    private string _groupKey = "";

    public override void _Ready()
    {
        ToggleMode = true;
        FocusMode = FocusModeEnum.None;
        MouseFilter = MouseFilterEnum.Stop;
    }

    public void Bind(string groupKey, string displayName, bool selected, bool hasReadySkill, bool enabled)
    {
        _groupKey = groupKey ?? "";
        string label = BuildShortLabel(displayName);
        Text = $"{label}\n{(hasReadySkill ? "可用" : "锁定")}";
        TooltipText = string.IsNullOrWhiteSpace(displayName) ? _groupKey : displayName.Trim();
        Disabled = !enabled;
        ButtonPressed = selected;
        SelfModulate = selected ? Colors.White : new Color(1.0f, 1.0f, 1.0f, 0.74f);
    }

    public override void _Pressed()
    {
        if (!string.IsNullOrWhiteSpace(_groupKey))
        {
            EmitSignal(SignalName.Selected, _groupKey);
        }
    }

    private static string BuildShortLabel(string displayName)
    {
        string label = string.IsNullOrWhiteSpace(displayName) ? "英雄" : displayName.Trim();
        return label.Length <= 4 ? label : label[..4];
    }
}
