using System.Linq;
using Rpg.Runtime.Battle;
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

        if (string.IsNullOrWhiteSpace(result.BattleId))
        {
            return Reject(expectedSnapshotId, result.BattleId, "battle_result_missing_battle_id");
        }

        if (result.TerminationReason == BattleTerminationReason.None ||
            result.TerminationReason == BattleTerminationReason.RuntimeException ||
            result.TerminationReason == BattleTerminationReason.Interrupted)
        {
            return Reject(expectedSnapshotId, result.BattleId, "battle_result_invalid_termination");
        }

        if (eventStream == null ||
            eventStream.Events.Count == 0 ||
            !eventStream.Events.Any(item => item.Kind == BattleEventKind.BattleEnded && item.BattleId == result.BattleId))
        {
            return Reject(expectedSnapshotId, result.BattleId, "battle_event_boundary_missing");
        }

        return new SettlementPlan
        {
            Accepted = true,
            SnapshotId = result.SnapshotId,
            BattleId = result.BattleId,
            SourceEventIds = eventStream.EventIds.ToList()
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
