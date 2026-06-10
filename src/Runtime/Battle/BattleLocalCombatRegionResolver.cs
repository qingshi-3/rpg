using System.Collections.Generic;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle;

internal static class BattleLocalCombatRegionResolver
{
    internal static BattleTacticalRegionSnapshot ResolveEngagedLocalCombatRegion(
        BattleRuntimeTickStartActorFact actorFact,
        BattleGroupTacticalStateStore tacticalStateStore)
    {
        if (tacticalStateStore == null || string.IsNullOrWhiteSpace(actorFact.Actor.BattleGroupId))
        {
            return null;
        }

        try
        {
            BattleGroupTacticalState tacticalState = tacticalStateStore.GetRequiredSnapshot(actorFact.Actor.BattleGroupId);
            return tacticalState.EngagementState == BattleGroupEngagementState.Engaged
                ? tacticalState.LocalCombatRegion
                : null;
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    internal static IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> FilterFactsToLocalCombatRegion(
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> facts,
        BattleRuntimeTickStartActorFact actorFact,
        BattleTacticalRegionSnapshot localCombatRegion)
    {
        Dictionary<string, BattleRuntimeTickStartActorFact> filtered = new(System.StringComparer.Ordinal);
        foreach (BattleRuntimeTickStartActorFact fact in facts?.Values ?? System.Array.Empty<BattleRuntimeTickStartActorFact>())
        {
            if (string.Equals(fact.Actor.ActorId ?? "", actorFact.Actor.ActorId ?? "", System.StringComparison.Ordinal) ||
                BattleRuntimeTickResolver.SameFaction(fact.Actor, actorFact.Actor) ||
                IsInsideLocalCombatRegion(fact, localCombatRegion))
            {
                filtered[fact.Actor.ActorId ?? ""] = fact;
            }
        }

        return filtered;
    }

    internal static bool IsInsideLocalCombatRegion(
        BattleRuntimeTickStartActorFact fact,
        BattleTacticalRegionSnapshot localCombatRegion)
    {
        if (localCombatRegion == null ||
            fact.Actor.GridHeight != localCombatRegion.CenterCellHeight)
        {
            return false;
        }

        int width = System.Math.Max(1, localCombatRegion.Width);
        int height = System.Math.Max(1, localCombatRegion.Height);
        int minX = localCombatRegion.CenterCellX - (width - 1) / 2;
        int minY = localCombatRegion.CenterCellY - (height - 1) / 2;
        return fact.Actor.GridX >= minX &&
               fact.Actor.GridX < minX + width &&
               fact.Actor.GridY >= minY &&
               fact.Actor.GridY < minY + height;
    }

    internal static BattleRegionMovementGoal ResolveRegionMovementGoal(
        BattleRuntimeTickStartActorFact actorFact,
        BattleGroupTacticalStateStore tacticalStateStore)
    {
        if (tacticalStateStore == null || string.IsNullOrWhiteSpace(actorFact.Actor.BattleGroupId))
        {
            return null;
        }

        try
        {
            BattleGroupTacticalState tacticalState = tacticalStateStore.GetRequiredSnapshot(actorFact.Actor.BattleGroupId);
            return BattleGroupRegionMovementPolicy.ResolveMovementGoal(tacticalState);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }
}
