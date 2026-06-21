using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle;

internal static class BattleCombatGeometry
{
    internal static int GetOrthogonalAttackGap(BattleRuntimeTickStartActorFact first, BattleRuntimeTickStartActorFact second)
    {
        return GetOrthogonalAttackGap(first.Actor, first.Anchor, second.Actor, second.Anchor);
    }

    internal static int GetOrthogonalAttackGap(
        BattleRuntimeActor first,
        BattleGridCoord firstAnchor,
        BattleRuntimeActor second,
        BattleGridCoord secondAnchor)
    {
        return BattleActorFootprint.GetOrthogonalGap(first, firstAnchor, second, secondAnchor);
    }
}
