using Godot;

namespace Rpg.Presentation.Battle.Entities;

public partial class SelectableComponent : BattleEntityComponent
{
    [Export]
    public bool IsSelectable { get; set; } = true;

    [Export]
    public int Priority { get; set; } = 0;
}
