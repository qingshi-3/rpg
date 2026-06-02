using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle;

internal sealed partial class BattleRuntimeTickResolver
{
    internal static void RecordAdvanceFailure(BattleRuntimeActor actor, string failureReason)
    {
        if (actor == null)
        {
            return;
        }

        actor.ConsecutiveAdvanceFailures++;
        actor.LastAdvanceFailureReason = string.IsNullOrWhiteSpace(failureReason)
            ? "advance_failed"
            : failureReason;
    }

    internal static void ResetAdvanceFailureState(BattleRuntimeActor actor)
    {
        if (actor == null)
        {
            return;
        }

        actor.ConsecutiveAdvanceFailures = 0;
        actor.LastAdvanceFailureReason = "";
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
            nameof(BattleRuntimeTickResolver),
            $"BattleRuntimeAction battle={battleId ?? ""} tick={tick} time={currentTimeSeconds:0.00} actor={actor.ActorId} action={request.Kind} outcome={outcome} target={targetId} actorCell={actor.GridX},{actor.GridY},{actor.GridHeight} targetCell={targetCell} distance={distance} actorHp={actor.HitPoints} readyAt={actor.ActionReadyAtSeconds:0.00} targetHp={targetHp}");
    }
}
