using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Results;

namespace Rpg.Application.Battle.Adapters;

public sealed class LegacyBattleResultAdapter
{
    public BattleResult ToLegacyResult(string requestId, BattleKind battleKind, BattleOutcomeResult outcome)
    {
        BattleOutcome legacyOutcome = MapOutcome(outcome);
        string reason = outcome == null
            ? "null_outcome"
            : outcome.IsComplete
                ? outcome.TerminationReason.ToString()
                : $"incomplete_{outcome.TerminationReason}";
        GameLog.Info(nameof(LegacyBattleResultAdapter), $"Converted runtime outcome to legacy result request={requestId ?? ""} battle={outcome?.BattleId ?? ""} reason={reason} legacyOutcome={legacyOutcome}");

        return new BattleResult
        {
            RequestId = requestId ?? "",
            ContextId = outcome?.BattleId ?? "",
            BattleKind = battleKind,
            Outcome = legacyOutcome
        };
    }

    private static BattleOutcome MapOutcome(BattleOutcomeResult outcome)
    {
        if (outcome == null || !outcome.IsComplete)
        {
            return BattleOutcome.Disaster;
        }

        return outcome.TerminationReason switch
        {
            BattleTerminationReason.NormalVictory => BattleOutcome.Victory,
            BattleTerminationReason.NormalDefeat => BattleOutcome.Defeat,
            BattleTerminationReason.PlayerRetreat => BattleOutcome.Withdraw,
            BattleTerminationReason.RuntimeException => BattleOutcome.Disaster,
            BattleTerminationReason.Interrupted => BattleOutcome.Disaster,
            BattleTerminationReason.None => BattleOutcome.Disaster,
            _ => BattleOutcome.Disaster
        };
    }
}
