using Godot;

namespace Rpg.Presentation.Battle.Entities;

public partial class AttackComponent : BattleEntityComponent
{
    [Export]
    public int Damage { get; set; } = 4;

    [Export]
    public int Range { get; set; } = 1;

    [Export]
    public int ApCost { get; set; } = 1;
}
