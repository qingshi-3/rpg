using System.Collections.Generic;

namespace Rpg.Application.Battle.Auto;

public sealed class AutoBattleSimulationResult
{
    internal AutoBattleSimulationResult(
        AutoBattleRuntimeState finalState,
        IReadOnlyList<AutoBattleEvent> events,
        BattleResult battleResult)
    {
        FinalState = finalState;
        Events = events;
        BattleResult = battleResult;
    }

    public AutoBattleRuntimeState FinalState { get; }
    public IReadOnlyList<AutoBattleEvent> Events { get; }
    public BattleResult BattleResult { get; }
}
