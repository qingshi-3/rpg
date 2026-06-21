using System.Collections.Generic;
using Rpg.Infrastructure.Diagnostics;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Navigation;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle;

internal static class BattleObjectiveAdvancePlanner
{
    internal static BattleRuntimeTickContext BuildObjectiveAdvanceContext(
        BattleRuntimeAiActionRequest request,
        BattleRuntimeTickStartActorFact actorFact,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
        BattlePerformanceCounters performanceCounters,
        string battleId,
        int tick)
    {
        if (!actorFact.Actor.HasObjectiveAnchor)
        {
            return BattleRuntimeTickContextFactory.Create(request, actorFact, null, false, default, "objective_missing");
        }

        BattleRuntimeActor tickStartActor = BattleTickStartProjectionBuilder.Build(actorFact);
        IReadOnlyList<BattleGridCoord> moveOptions = BattleCrowdMovementPlanner.FindNextStepCandidatesTowardObjective(
            tickStartActor,
            navigationGraph,
            occupancy,
            new BattleMovementReservationMap(),
            performanceCounters,
            battleId,
            tick);
        if (moveOptions.Count == 0)
        {
            BattleRuntimeAdvanceDiagnostics.LogObjectiveAdvanceFailureDiagnostic(
                battleId,
                tick,
                actorFact,
                navigationGraph,
                "objective_path_not_found");
            return BattleRuntimeTickContextFactory.Create(request, actorFact, null, false, default, "objective_path_not_found");
        }

        BattleRuntimeActorStateMachine.CopyMovementSteering(actorFact.Actor, tickStartActor);
        return BattleRuntimeTickContextFactory.Create(
            request,
            actorFact,
            null,
            hasMoveTo: true,
            moveTo: moveOptions[0],
            failureReason: "",
            moveOptions: moveOptions);
    }

    internal static BattleRuntimeTickContext BuildRegionAdvanceContext(
        BattleRuntimeAiActionRequest request,
        BattleRuntimeTickStartActorFact actorFact,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
        BattlePerformanceCounters performanceCounters,
        string battleId,
        int tick)
    {
        BattleRegionMovementGoal goal = request?.RegionMovementGoal;
        if (goal == null || string.IsNullOrWhiteSpace(goal.RegionId))
        {
            return BattleRuntimeTickContextFactory.Create(request, actorFact, null, false, default, "region_missing");
        }

        BattleRuntimeActor projectedActor = BattleTickStartProjectionBuilder.Build(actorFact);
        int width = System.Math.Max(1, goal.Width);
        int height = System.Math.Max(1, goal.Height);
        projectedActor.HasObjectiveAnchor = true;
        projectedActor.ObjectiveZoneId = goal.RegionId;
        // Tactical regions store center cells; objective movement consumes
        // top-left anchors before expanding the region into navigation goals.
        projectedActor.ObjectiveGridX = goal.CenterCellX - (width - 1) / 2;
        projectedActor.ObjectiveGridY = goal.CenterCellY - (height - 1) / 2;
        projectedActor.ObjectiveGridHeight = goal.CenterCellHeight;
        projectedActor.ObjectiveWidth = width;
        projectedActor.ObjectiveHeight = height;

        IReadOnlyList<BattleGridCoord> moveOptions = BattleCrowdMovementPlanner.FindNextStepCandidatesTowardObjective(
            projectedActor,
            navigationGraph,
            occupancy,
            new BattleMovementReservationMap(),
            performanceCounters,
            battleId,
            tick);
        if (moveOptions.Count == 0)
        {
            BattleRuntimeAdvanceDiagnostics.LogRegionAdvanceFailureDiagnostic(
                battleId,
                tick,
                actorFact,
                goal,
                navigationGraph,
                "region_path_not_found");
            return BattleRuntimeTickContextFactory.Create(request, actorFact, null, false, default, "region_path_not_found", regionMovementGoal: goal);
        }

        BattleRuntimeActorStateMachine.CopyMovementSteering(actorFact.Actor, projectedActor);
        return BattleRuntimeTickContextFactory.Create(
            request,
            actorFact,
            null,
            hasMoveTo: true,
            moveTo: moveOptions[0],
            failureReason: "",
            moveOptions: moveOptions,
            movementReasonCode: request.ReasonCode,
            regionMovementGoal: goal);
    }

    internal static BattleGridCoord GetObjectiveAnchor(BattleRuntimeActor actor)
    {
        return new BattleGridCoord(
            actor?.ObjectiveGridX ?? 0,
            actor?.ObjectiveGridY ?? 0,
            actor?.ObjectiveGridHeight ?? 0);
    }

    internal static string ResolveMovementEventTargetId(BattleRuntimeTickContext context)
    {
        if (context?.Request?.Kind == BattleRuntimeAiActionKind.AdvanceTowardRegion)
        {
            return context.Request.RegionMovementGoal?.RegionId ?? "";
        }

        return context?.TargetFact?.Actor.ActorId ?? context?.ActorFact.Actor.ObjectiveZoneId ?? "";
    }

    internal static bool IsObjectiveReached(BattleRuntimeTickStartActorFact actorFact)
    {
        BattleRuntimeActor actor = actorFact.Actor;
        if (actor?.HasObjectiveAnchor != true)
        {
            return false;
        }

        var objective = new BattleRuntimeActor
        {
            GridX = actor.ObjectiveGridX,
            GridY = actor.ObjectiveGridY,
            GridHeight = actor.ObjectiveGridHeight,
            FootprintWidth = System.Math.Max(1, actor.ObjectiveWidth),
            FootprintHeight = System.Math.Max(1, actor.ObjectiveHeight)
        };

        return BattleActorFootprint.GetGap(
            actor,
            actorFact.Anchor,
            objective,
            GetObjectiveAnchor(actor)) <= 1;
    }
}
