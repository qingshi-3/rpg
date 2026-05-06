using Godot;
using Rpg.Presentation.Battle.Abilities;

namespace Rpg.Definitions.Battle.Abilities;

public abstract partial class AbilityTargetRule : Resource
{
    public abstract bool IsValidTarget(AbilityUseContext context, out string reason);
}
