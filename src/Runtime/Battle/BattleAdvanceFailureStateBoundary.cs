namespace Rpg.Runtime.Battle;

// Decision services report movement-degradation reasons through this narrow
// boundary without owning the actor's failure counters directly.
internal delegate void RecordAdvanceFailureCallback(BattleRuntimeActor actor, string reasonCode);

internal static class BattleAdvanceFailureStateBoundary
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
}
