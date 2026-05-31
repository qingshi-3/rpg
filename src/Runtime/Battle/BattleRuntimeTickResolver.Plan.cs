using System.Collections.Generic;
using Rpg.Infrastructure.Diagnostics;
using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Navigation;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle;

internal sealed partial class BattleRuntimeTickResolver
{
    private static BattleRuntimeTickContext BuildObjectiveAdvanceContext(
        BattleRuntimeAiActionRequest request,
        BattleRuntimeTickStartActorFact actorFact,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
        BattleFlowFieldCache flowFields,
        BattlePerformanceCounters performanceCounters,
        string battleId,
        int tick)
    {
        if (!actorFact.Actor.HasObjectiveAnchor)
        {
            return CreateContext(request, actorFact, null, false, default, "objective_missing");
        }

        BattleRuntimeActor tickStartActor = BuildTickStartProjection(actorFact);
        IReadOnlyList<BattleGridCoord> moveOptions = BattleCrowdMovementPlanner.FindNextStepCandidatesTowardObjective(
            tickStartActor,
            navigationGraph,
            occupancy,
            new BattleMovementReservationMap(),
            flowFields,
            performanceCounters);
        if (moveOptions.Count == 0)
        {
            LogObjectiveAdvanceFailureDiagnostic(
                battleId,
                tick,
                actorFact,
                navigationGraph,
                "objective_path_not_found");
            return CreateContext(request, actorFact, null, false, default, "objective_path_not_found");
        }

        return CreateContext(
            request,
            actorFact,
            null,
            hasMoveTo: true,
            moveTo: moveOptions[0],
            failureReason: "",
            moveOptions: moveOptions);
    }

    private static BattleRuntimeTickContext BuildRegionAdvanceContext(
        BattleRuntimeAiActionRequest request,
        BattleRuntimeTickStartActorFact actorFact,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
        BattleFlowFieldCache flowFields,
        BattlePerformanceCounters performanceCounters,
        string battleId,
        int tick)
    {
        BattleRegionMovementGoal goal = request?.RegionMovementGoal;
        if (goal == null || string.IsNullOrWhiteSpace(goal.RegionId))
        {
            return CreateContext(request, actorFact, null, false, default, "region_missing");
        }

        BattleRuntimeActor projectedActor = BuildTickStartProjection(actorFact);
        projectedActor.HasObjectiveAnchor = true;
        projectedActor.ObjectiveZoneId = goal.RegionId;
        projectedActor.ObjectiveGridX = goal.CenterCellX;
        projectedActor.ObjectiveGridY = goal.CenterCellY;
        projectedActor.ObjectiveGridHeight = goal.CenterCellHeight;
        projectedActor.ObjectiveWidth = System.Math.Max(1, goal.Width);
        projectedActor.ObjectiveHeight = System.Math.Max(1, goal.Height);

        IReadOnlyList<BattleGridCoord> moveOptions = BattleCrowdMovementPlanner.FindNextStepCandidatesTowardObjective(
            projectedActor,
            navigationGraph,
            occupancy,
            new BattleMovementReservationMap(),
            flowFields,
            performanceCounters);
        if (moveOptions.Count == 0)
        {
            LogObjectiveAdvanceFailureDiagnostic(
                battleId,
                tick,
                actorFact,
                navigationGraph,
                "region_path_not_found");
            return CreateContext(request, actorFact, null, false, default, "region_path_not_found", regionMovementGoal: goal);
        }

        return CreateContext(
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

    private static BattleGridCoord GetObjectiveAnchor(BattleRuntimeActor actor)
    {
        return new BattleGridCoord(
            actor?.ObjectiveGridX ?? 0,
            actor?.ObjectiveGridY ?? 0,
            actor?.ObjectiveGridHeight ?? 0);
    }

    private static string ResolveMovementEventTargetId(BattleRuntimeTickContext context)
    {
        if (context?.Request?.Kind == BattleRuntimeAiActionKind.AdvanceTowardRegion)
        {
            return context.Request.RegionMovementGoal?.RegionId ?? "";
        }

        return context?.TargetFact?.Actor.ActorId ?? context?.ActorFact.Actor.ObjectiveZoneId ?? "";
    }

    private static bool IsObjectiveReached(BattleRuntimeTickStartActorFact actorFact)
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

    private static void LogObjectiveAdvanceFailureDiagnostic(
        string battleId,
        int tick,
        BattleRuntimeTickStartActorFact actorFact,
        BattleNavigationGraph navigationGraph,
        string failureReason)
    {
        GameLog.Warn(
            nameof(BattleRuntimeTickResolver),
            $"BattleRuntimeObjectiveAdvanceDiagnostic battle={battleId ?? ""} tick={tick} actor={actorFact.Actor.ActorId} objective={actorFact.Actor.ObjectiveZoneId} reason={failureReason ?? "objective_advance_failed"} actorCell={actorFact.Anchor.X},{actorFact.Anchor.Y},{actorFact.Anchor.Height} objectiveCell={actorFact.Actor.ObjectiveGridX},{actorFact.Actor.ObjectiveGridY},{actorFact.Actor.ObjectiveGridHeight} graph={navigationGraph?.DescribeTopology() ?? "missing"}");
    }

}
