using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Results;

namespace Rpg.Runtime.Battle;

public sealed class BattleRuntimeSession
{
    // Temporary contract skeleton for the architecture slice; final combat authority will live in the real runtime state machine.
    public BattleRuntimeSessionResult RunMinimal(BattleStartSnapshot snapshot)
    {
        BattleEventStream stream = new();
        string battleId = snapshot?.BattleId ?? "";
        string snapshotId = snapshot?.SnapshotId ?? "";
        if (string.IsNullOrWhiteSpace(snapshotId) || string.IsNullOrWhiteSpace(battleId))
        {
            stream.Add(new BattleEvent
            {
                EventId = $"{battleId}:snapshot_invalid",
                BattleId = battleId,
                Kind = BattleEventKind.CommandRejected,
                ReasonCode = "battle_snapshot_invalid"
            });

            return new BattleRuntimeSessionResult
            {
                EventStream = stream,
                Outcome = new BattleOutcomeResult
                {
                    SnapshotId = snapshotId,
                    BattleId = battleId,
                    IsComplete = false,
                    TerminationReason = BattleTerminationReason.RuntimeException
                }
            };
        }

        stream.Add(new BattleEvent
        {
            EventId = $"{battleId}:started",
            BattleId = battleId,
            Kind = BattleEventKind.BattleStarted
        });

        foreach (BattleGroupSnapshot group in snapshot?.BattleGroups ?? Enumerable.Empty<BattleGroupSnapshot>())
        {
            stream.Add(new BattleEvent
            {
                EventId = $"{battleId}:{group.BattleGroupId}:command",
                BattleId = battleId,
                BattleGroupId = group.BattleGroupId,
                Kind = BattleEventKind.CommandAccepted
            });
        }

        stream.Add(new BattleEvent
        {
            EventId = $"{battleId}:ended",
            BattleId = battleId,
            Kind = BattleEventKind.BattleEnded
        });

        return new BattleRuntimeSessionResult
        {
            EventStream = stream,
            Outcome = BattleOutcomeResult.Completed(snapshotId, battleId, BattleTerminationReason.NormalVictory)
        };
    }
}
