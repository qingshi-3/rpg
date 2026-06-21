using System.Collections.Generic;
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
            // Region movement stores center cells and converts to rectangular
            // anchors later. Width/height still come from the logged action
            // bounds so join movement targets the full combat area.
            CenterCellX = actionZone?.CenterCellX ?? region?.CenterCellX ?? 0,
            CenterCellY = actionZone?.CenterCellY ?? region?.CenterCellY ?? 0,
            CenterCellHeight = actionZone?.CenterCellHeight ?? region?.CenterCellHeight ?? 0,
            Width = System.Math.Max(1, actionZone == null ? region?.Width ?? 1 : actionZone.MaxCellX - actionZone.MinCellX + 1),
            Height = System.Math.Max(1, actionZone == null ? region?.Height ?? 1 : actionZone.MaxCellY - actionZone.MinCellY + 1),
            SourceRegionId = region?.SourceRegionId ?? actionZone?.TargetCombatZoneId ?? "",
            ReasonCode = BattleGroupTacticalReasonCode.CombatZoneJoinAdvance
        };
    }

    internal static bool TryBuildPressureAdvanceContext(
        BattleRuntimeTickStartActorFact actorFact,
        BattleRuntimeTickStartActorFact? targetFact,
        LocalCombatSituation localCombatSituation,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
        BattlePerformanceCounters performanceCounters,
        out BattleRuntimeTickContext context)
    {
        context = null;
        if (!ShouldKeepPressure(actorFact, targetFact, localCombatSituation))
        {
            return false;
        }

        BattleRegionMovementGoal goal = BuildPressureMovementGoal(actorFact, targetFact, localCombatSituation);
        if (goal == null ||
            actorFact.Actor == null ||
            navigationGraph == null ||
            occupancy == null)
        {
            return false;
        }

        BattleRuntimeActor projectedActor = BattleTickStartProjectionBuilder.Build(actorFact);
        projectedActor.HasObjectiveAnchor = true;
        projectedActor.ObjectiveZoneId = goal.RegionId;
        projectedActor.ObjectiveGridX = goal.CenterCellX;
        projectedActor.ObjectiveGridY = goal.CenterCellY;
        projectedActor.ObjectiveGridHeight = goal.CenterCellHeight;
        projectedActor.ObjectiveWidth = 1;
        projectedActor.ObjectiveHeight = 1;

        IReadOnlyList<BattleGridCoord> moveOptions = BattleCrowdMovementPlanner.FindNextStepCandidatesTowardObjective(
            projectedActor,
            navigationGraph,
            occupancy,
            new BattleMovementReservationMap(),
            performanceCounters);
        if (moveOptions.Count == 0)
        {
            return false;
        }

        // Pressure movement is a fallback for full local entry, not a new
        // target claim. It reuses region-directed steering so crowded units
        // keep closing on the fight without rebuilding global paths.
        BattleRuntimeActorStateMachine.CopyMovementSteering(actorFact.Actor, projectedActor);
        BattleRuntimeAiActionRequest request = BattleRuntimeAiActionRequest.AdvanceTowardRegion(actorFact.Actor.ActorId, goal);
        context = BattleRuntimeTickContextFactory.Create(
            request,
            actorFact,
            null,
            hasMoveTo: true,
            moveTo: moveOptions[0],
            failureReason: "",
            moveOptions: moveOptions,
            movementReasonCode: BattleGroupTacticalReasonCode.CombatPressureAdvance,
            regionMovementGoal: goal);
        return true;
    }

    private static bool ShouldKeepPressure(
        BattleRuntimeTickStartActorFact actorFact,
        BattleRuntimeTickStartActorFact? targetFact,
        LocalCombatSituation localCombatSituation)
    {
        if (actorFact.Actor == null ||
            targetFact == null ||
            localCombatSituation == null ||
            !localCombatSituation.InsideLeash ||
            localCombatSituation.HasReachableAttackSlot ||
            localCombatSituation.HasReachableSupportSlot)
        {
            return false;
        }

        BattleGridCoord pressurePoint = targetFact.Value.Anchor;
        int pressureDistance = System.Math.Max(
            System.Math.Abs(actorFact.Anchor.X - pressurePoint.X),
            System.Math.Abs(actorFact.Anchor.Y - pressurePoint.Y)) +
            System.Math.Abs(actorFact.Anchor.Height - pressurePoint.Height) * 4;
        return pressureDistance > 1;
    }

    private static BattleRegionMovementGoal BuildPressureMovementGoal(
        BattleRuntimeTickStartActorFact actorFact,
        BattleRuntimeTickStartActorFact? targetFact,
        LocalCombatSituation localCombatSituation)
    {
        BattleGridCoord center = targetFact?.Anchor ?? localCombatSituation.Center;
        string owner = actorFact.Actor?.BattleGroupId ?? "";
        string regionId = !string.IsNullOrWhiteSpace(localCombatSituation.RegionId)
            ? $"{localCombatSituation.RegionId}:pressure"
            : $"{localCombatSituation.SituationId}:pressure";

        return new BattleRegionMovementGoal
        {
            RegionId = regionId ?? "",
            OwnerBattleGroupId = owner ?? "",
            Kind = BattleTacticalRegionKind.LocalCombat,
            CenterCellX = center.X,
            CenterCellY = center.Y,
            CenterCellHeight = center.Height,
            Width = 1,
            Height = 1,
            SourceRegionId = localCombatSituation.RegionId ?? "",
            ReasonCode = BattleGroupTacticalReasonCode.CombatPressureAdvance
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
