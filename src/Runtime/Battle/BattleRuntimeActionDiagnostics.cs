using System.Collections.Generic;
using System.Linq;
using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle;

// Keeps action-result diagnostics out of tick orchestration while preserving
// the existing trace category consumed by regression tests and debug workflows.
internal static class BattleRuntimeActionDiagnostics
{
    private const string TraceCategory = "BattleRuntimeTickResolver";

    internal static void LogTickActionResults(
        IReadOnlyList<BattleRuntimeTickContext> contexts,
        string battleId,
        int tick,
        double currentTimeSeconds)
    {
        System.ArgumentNullException.ThrowIfNull(contexts);

        foreach (BattleRuntimeTickContext context in contexts
                     .OrderBy(item => item.ActorFact.Actor.ActorId, System.StringComparer.Ordinal))
        {
            if (context.Result == null)
            {
                context.Result = BattleRuntimeAiActionResult.Failed(context.Request, "unresolved_action");
            }

            LogRuntimeActionResult(
                battleId,
                tick,
                currentTimeSeconds,
                context.ActorFact.Actor,
                context.TargetFact?.Actor,
                context.Request,
                context.Result);
        }
    }

    private static void LogRuntimeActionResult(
        string battleId,
        int tick,
        double currentTimeSeconds,
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleRuntimeAiActionRequest request,
        BattleRuntimeAiActionResult result)
    {
        if (actor == null || request == null)
        {
            return;
        }

        if (request.Kind == BattleRuntimeAiActionKind.WaitForAttackCharge && result?.Success == true)
        {
            return;
        }

        string outcome = result?.Success == true
            ? result.Status
            : string.IsNullOrWhiteSpace(result?.FailureReason) ? "failed" : result.FailureReason;
        string targetId = target?.ActorId ?? request.TargetActorId ?? "";
        string targetHp = target == null ? "-" : target.HitPoints.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string targetCell = target == null ? "-" : $"{target.GridX},{target.GridY},{target.GridHeight}";
        string distance = target == null
            ? "-"
            : BattleActorFootprint.GetGap(actor, target).ToString(System.Globalization.CultureInfo.InvariantCulture);

        GameLog.Trace(
            TraceCategory,
            $"BattleRuntimeAction battle={battleId ?? ""} tick={tick} time={currentTimeSeconds:0.00} actor={actor.ActorId} action={request.Kind} outcome={outcome} target={targetId} actorCell={actor.GridX},{actor.GridY},{actor.GridHeight} targetCell={targetCell} distance={distance} actorHp={actor.HitPoints} readyAt={actor.ActionReadyAtSeconds:0.00} targetHp={targetHp}");
    }
}
