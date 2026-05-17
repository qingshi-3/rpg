using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Results;

namespace Rpg.Runtime.Battle;

public sealed class BattleRuntimeSessionResult
{
    public BattleOutcomeResult Outcome { get; init; } = new();
    public BattleEventStream EventStream { get; init; } = new();
    public BattleRuntimeState FinalState { get; init; } = new();
}
