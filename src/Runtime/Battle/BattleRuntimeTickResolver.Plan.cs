using System.Collections.Generic;
using Rpg.Infrastructure.Diagnostics;
using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle;

internal sealed partial class BattleRuntimeTickResolver
{
    private static TickContext BuildObjectiveAdvanceContext(
        BattleRuntimeAiActionRequest request,
        TickStartActorFact actorFact,
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

    private static BattleGridCoord GetObjectiveAnchor(BattleRuntimeActor actor)
    {
        return new BattleGridCoord(
            actor?.ObjectiveGridX ?? 0,
            actor?.ObjectiveGridY ?? 0,
            actor?.ObjectiveGridHeight ?? 0);
    }

    private static bool IsObjectiveReached(TickStartActorFact actorFact)
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
        TickStartActorFact actorFact,
        BattleNavigationGraph navigationGraph,
        string failureReason)
    {
        GameLog.Warn(
            nameof(BattleRuntimeTickResolver),
            $"BattleRuntimeObjectiveAdvanceDiagnostic battle={battleId ?? ""} tick={tick} actor={actorFact.Actor.ActorId} objective={actorFact.Actor.ObjectiveZoneId} reason={failureReason ?? "objective_advance_failed"} actorCell={actorFact.Anchor.X},{actorFact.Anchor.Y},{actorFact.Anchor.Height} objectiveCell={actorFact.Actor.ObjectiveGridX},{actorFact.Actor.ObjectiveGridY},{actorFact.Actor.ObjectiveGridHeight} graph={navigationGraph?.DescribeTopology() ?? "missing"}");
    }

    private static void SetPlanState(
        BattleEventStream stream,
        string battleId,
        int tick,
        double currentTimeSeconds,
        BattleRuntimeActor actor,
        BattleGroupPlanRuntimeState state,
        string reasonCode)
    {
        if (stream == null || actor == null || actor.PlanState == state)
        {
            return;
        }

        actor.PlanState = state;
        stream.Add(new BattleEvent
        {
            EventId = $"{battleId}:tick_{tick}:{actor.ActorId}:plan:{state}",
            BattleId = battleId,
            BattleGroupId = actor.BattleGroupId,
            ActorId = actor.ActorId,
            TargetId = actor.ObjectiveZoneId ?? "",
            Kind = BattleEventKind.BattleGroupPlanStateChanged,
            ReasonCode = string.IsNullOrWhiteSpace(reasonCode) ? state.ToString() : reasonCode,
            RuntimeTick = tick,
            RuntimeTimeSeconds = currentTimeSeconds
        });
    }
}
