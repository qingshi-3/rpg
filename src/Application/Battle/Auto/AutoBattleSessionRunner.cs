using Rpg.Infrastructure.Logging;

namespace Rpg.Application.Battle.Auto;

public sealed class AutoBattleSessionRunner
{
    public const string FormalHandoffDisabledReason = "auto_battle_handoff_disabled_runtime_authority";

    public AutoBattleSessionRunner(AutoBattleSimulation simulation = null)
    {
    }

    public bool TryRunActiveBattle(
        out AutoBattleSimulationResult simulationResult,
        out string failureReason)
    {
        simulationResult = null;
        failureReason = "";

        if (!BattleSessionHandoff.TryPeekActiveRequest(out BattleStartRequest request))
        {
            failureReason = "no_active_battle_request";
            return false;
        }

        // Formal battle handoff belongs to the Runtime battle path. AutoBattleSimulation remains
        // available only as isolated legacy simulation/report coverage, not as a competing runtime.
        failureReason = FormalHandoffDisabledReason;
        GameLog.Warn(
            nameof(AutoBattleSessionRunner),
            $"AutoBattleFormalHandoffRejected request={request.RequestId} reason={failureReason}");
        return false;
    }
}
