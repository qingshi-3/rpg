namespace Rpg.Application.Battle;

public sealed class BattleSessionResult
{
    public BattleSessionResult(string contextId, string encounterId, string returnScenePath, BattleOutcome outcome)
    {
        ContextId = contextId ?? "";
        EncounterId = encounterId ?? "";
        ReturnScenePath = returnScenePath ?? "";
        Outcome = outcome;
    }

    public string ContextId { get; }
    public string EncounterId { get; }
    public string ReturnScenePath { get; }
    public BattleOutcome Outcome { get; }
    public bool IsVictory => Outcome == BattleOutcome.Victory;
}
