using System.Collections.Generic;

namespace Rpg.Application.Battle.Settlement;

public sealed class SettlementPlan
{
    public bool Accepted { get; set; }
    public string RejectionReason { get; set; } = "";
    public string SnapshotId { get; set; } = "";
    public string BattleId { get; set; } = "";
    public List<string> SourceEventIds { get; set; } = new();
    public StateDeltaSet Deltas { get; set; } = new();
}
