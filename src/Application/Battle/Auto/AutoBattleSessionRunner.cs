using System;

namespace Rpg.Application.Battle.Auto;

public sealed class AutoBattleSessionRunner
{
    private readonly AutoBattleSimulation _simulation;

    public AutoBattleSessionRunner(AutoBattleSimulation simulation = null)
    {
        _simulation = simulation ?? new AutoBattleSimulation();
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

        try
        {
            simulationResult = _simulation.RunToEnd(request);
            BattleSessionResult sessionResult = BattleSessionHandoff.CompleteBattle(simulationResult.BattleResult);
            if (sessionResult == null)
            {
                failureReason = "battle_handoff_completion_failed";
                simulationResult = null;
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            simulationResult = null;
            failureReason = string.IsNullOrWhiteSpace(exception.Message)
                ? exception.GetType().Name
                : exception.Message;
            return false;
        }
    }
}
