using Godot;

namespace Rpg.Presentation.Battle.Entities;

public partial class TargetableComponent : BattleEntityComponent
{
    [Export]
    public bool IsTargetable { get; set; } = true;

    [Export]
    public BattleTargetTags Tags { get; set; } = BattleTargetTags.Unit;
}
