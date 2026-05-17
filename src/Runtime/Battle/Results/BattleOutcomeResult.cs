using Rpg.Runtime.Battle;
using System.Collections.Generic;

namespace Rpg.Runtime.Battle.Results;

public sealed class BattleOutcomeResult
{
    public string SnapshotId { get; set; } = "";
    public string BattleId { get; set; } = "";
    public bool IsComplete { get; set; }
    public BattleTerminationReason TerminationReason { get; set; } = BattleTerminationReason.None;
    public List<BattleActorOutcome> ActorOutcomes { get; set; } = new();

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

public sealed class BattleActorOutcome
{
    public string ActorId { get; set; } = "";
    public string BattleGroupId { get; set; } = "";
    public string FactionId { get; set; } = "";
    public string SourceForceId { get; set; } = "";
    public string SourceStateId { get; set; } = "";
    public BattleRuntimeActorKind Kind { get; set; }
    public bool Survived { get; set; }
    public int RemainingHitPoints { get; set; }
}
