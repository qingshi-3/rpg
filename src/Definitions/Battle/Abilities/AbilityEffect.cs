using Godot;
using Rpg.Presentation.Battle.Abilities;

namespace Rpg.Definitions.Battle.Abilities;

public abstract partial class AbilityEffect : Resource
{
    public abstract AbilityEffectResult Apply(AbilityUseContext context);
}
