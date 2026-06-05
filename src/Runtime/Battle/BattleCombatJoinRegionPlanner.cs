using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Diagnostics;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Navigation;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle;

internal static class BattleCombatJoinRegionPlanner
{
    internal static bool TryBuildOutsiderAdvanceContext(
        BattleRuntimeTickStartActorFact actorFact,
        BattleGroupActionZoneSnapshot actionZone,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
        BattleFlowFieldCache flowFields,
        BattlePerformanceCounters performanceCounters,
        string battleId,
        int tick,
        out BattleRuntimeTickContext context)
    {
        context = null;
        if (actionZone == null ||
            BattleGroupActionZoneResolver.IsInsideActionZone(actorFact.Actor, actorFact.Anchor, actionZone))
        {
            return false;
        }

        // Group engagement is only a commander signal. A member still outside
        // the current combat-zone bounds must path to that zone before slot search.
        BattleRegionMovementGoal goal = BuildMovementGoal(actionZone);
        context = BattleObjectiveAdvancePlanner.BuildRegionAdvanceContext(
            BattleRuntimeAiActionRequest.AdvanceTowardRegion(actorFact.Actor.ActorId, goal),
            actorFact,
            navigationGraph,
            occupancy,
            flowFields,
            performanceCounters,
            battleId,
            tick);
        return true;
    }

    internal static BattleRegionMovementGoal BuildMovementGoal(BattleGroupActionZoneSnapshot actionZone)
    {
        BattleTacticalRegionSnapshot region = BattleGroupActionZoneBuilder.ToLocalCombatRegion(actionZone);
        return new BattleRegionMovementGoal
        {
            RegionId = region?.RegionId ?? "",
            OwnerBattleGroupId = region?.OwnerBattleGroupId ?? "",
            Kind = BattleTacticalRegionKind.LocalCombat,
            // Region movement is executed by the objective flow-field planner, whose
            // X/Y fields are the rectangular goal anchor. Combat action zones expose a
            // true center separately, so join movement must use the logged min bounds.
            CenterCellX = actionZone?.MinCellX ?? 0,
            CenterCellY = actionZone?.MinCellY ?? 0,
            CenterCellHeight = actionZone?.CenterCellHeight ?? region?.CenterCellHeight ?? 0,
            Width = System.Math.Max(1, actionZone == null ? region?.Width ?? 1 : actionZone.MaxCellX - actionZone.MinCellX + 1),
            Height = System.Math.Max(1, actionZone == null ? region?.Height ?? 1 : actionZone.MaxCellY - actionZone.MinCellY + 1),
            SourceRegionId = region?.SourceRegionId ?? actionZone?.TargetCombatZoneId ?? "",
            ReasonCode = BattleGroupTacticalReasonCode.CombatZoneJoinAdvance
        };
    }

    internal static BattleTacticalRegionSnapshot SelectLocalCombatScope(
        BattleRuntimeTickStartActorFact actorFact,
        BattleRuntimeTickStartActorFact? selectedTarget,
        BattleTacticalRegionSnapshot storedLocalRegion,
        BattleTacticalRegionSnapshot combatJoinRegion)
    {
        if (combatJoinRegion == null)
        {
            return storedLocalRegion;
        }

        if (storedLocalRegion == null ||
            !IsActorInsideLocalCombatRegion(actorFact.Actor, actorFact.Anchor, storedLocalRegion) ||
            selectedTarget != null &&
            !IsActorInsideLocalCombatRegion(selectedTarget.Value.Actor, selectedTarget.Value.Anchor, storedLocalRegion))
        {
            return combatJoinRegion;
        }

        return storedLocalRegion;
    }

    private static bool IsActorInsideLocalCombatRegion(
        BattleRuntimeActor actor,
        BattleGridCoord anchor,
        BattleTacticalRegionSnapshot localCombatRegion)
    {
        if (actor == null ||
            localCombatRegion == null ||
            anchor.Height != localCombatRegion.CenterCellHeight)
        {
            return false;
        }

        int width = System.Math.Max(1, localCombatRegion.Width);
        int height = System.Math.Max(1, localCombatRegion.Height);
        int minX = localCombatRegion.CenterCellX - (width - 1) / 2;
        int minY = localCombatRegion.CenterCellY - (height - 1) / 2;
        foreach (BattleGridCoord cell in BattleActorFootprint.Enumerate(actor, anchor))
        {
            if (cell.X >= minX &&
                cell.X < minX + width &&
                cell.Y >= minY &&
                cell.Y < minY + height)
            {
                return true;
            }
        }

        return false;
    }
}
