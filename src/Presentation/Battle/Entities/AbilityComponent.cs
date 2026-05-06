using Godot;
using Rpg.Definitions.Battle.Abilities;

namespace Rpg.Presentation.Battle.Entities;

public partial class AbilityComponent : BattleEntityComponent
{
    [Export]
    public Godot.Collections.Array<AbilityDefinition> Abilities { get; set; } = new();
}
