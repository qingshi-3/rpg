using System.Collections.Generic;
using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle;

internal static class BattleRuntimeAdvanceDiagnostics
{
    internal static void LogAdvanceFailureDiagnostic(
        string battleId,
        int tick,
        BattleRuntimeTickStartActorFact actorFact,
        BattleRuntimeTickStartActorFact? targetFact,
        BattleNavigationGraph navigationGraph,
        string failureReason,
        BattleGridCoord attemptedNext,
        HashSet<string> loggedDiagnostics)
    {
        string actorCell = $"{actorFact.Anchor.X},{actorFact.Anchor.Y},{actorFact.Anchor.Height}";
        string targetCell = targetFact == null
            ? $"{actorFact.Actor.ObjectiveGridX},{actorFact.Actor.ObjectiveGridY},{actorFact.Actor.ObjectiveGridHeight}"
            : $"{targetFact.Value.Anchor.X},{targetFact.Value.Anchor.Y},{targetFact.Value.Anchor.Height}";
        string reason = string.IsNullOrWhiteSpace(failureReason) ? "advance_failed" : failureReason;
        string attempted = string.Equals(reason, "path_not_found", System.StringComparison.Ordinal)
            ? "none"
            : attemptedNext.ToString();
        string targetId = targetFact?.Actor.ActorId ?? actorFact.Actor.ObjectiveZoneId ?? "";
        string key = $"{actorFact.Actor.ActorId}|{targetId}|{actorCell}|{targetCell}|{reason}|{attempted}";
        if (loggedDiagnostics != null && !loggedDiagnostics.Add(key))
        {
            return;
        }

        string reachability = targetFact == null
            ? "objectiveReachability=unavailable"
            : navigationGraph?.DescribeStaticReachability(actorFact.Actor, targetFact.Value.Actor, actorFact.Actor.AttackRange) ?? "graph=missing";
        GameLog.Warn(
            nameof(BattleRuntimeTickResolver),
            $"BattleRuntimeAdvanceDiagnostic battle={battleId ?? ""} tick={tick} actor={actorFact.Actor.ActorId} target={targetId} reason={reason} actorCell={actorCell} targetCell={targetCell} attemptedNext={attempted} {reachability}");
    }

    internal static void LogObjectiveAdvanceFailureDiagnostic(
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
