using Rpg.Runtime.Battle;

namespace Rpg.Runtime.Battle.Results;

public sealed class BattleOutcomeResult
{
    public string SnapshotId { get; set; } = "";
    public string BattleId { get; set; } = "";
    public bool IsComplete { get; set; }
    public BattleTerminationReason TerminationReason { get; set; } = BattleTerminationReason.None;

    public static BattleOutcomeResult Completed(
        string snapshotId,
        string battleId,
        BattleTerminationReason terminationReason)
    {
        return new BattleOutcomeResult
        {
            SnapshotId = snapshotId ?? "",
            BattleId = battleId ?? "",
            IsComplete = true,
            TerminationReason = terminationReason
        };
    }
}
