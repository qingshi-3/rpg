using System.Linq;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Results;

namespace Rpg.Application.Battle.Settlement;

public sealed class BattleSettlementService
{
    public SettlementPlan BuildPlan(
        string expectedSnapshotId,
        BattleOutcomeResult result,
        BattleEventStream eventStream)
    {
        if (result == null)
        {
            return Reject(expectedSnapshotId, "", "battle_result_missing");
        }

        if (!result.IsComplete)
        {
            return Reject(expectedSnapshotId, result.BattleId, "battle_result_incomplete");
        }

        if (result.SnapshotId != expectedSnapshotId)
        {
            return Reject(expectedSnapshotId, result.BattleId, "battle_snapshot_mismatch");
        }

        return new SettlementPlan
        {
            Accepted = true,
            SnapshotId = result.SnapshotId,
            BattleId = result.BattleId,
            SourceEventIds = eventStream?.EventIds.ToList() ?? new()
        };
    }

    private static SettlementPlan Reject(string snapshotId, string battleId, string reason)
    {
        return new SettlementPlan
        {
            Accepted = false,
            SnapshotId = snapshotId ?? "",
            BattleId = battleId ?? "",
            RejectionReason = reason ?? ""
        };
    }
}
