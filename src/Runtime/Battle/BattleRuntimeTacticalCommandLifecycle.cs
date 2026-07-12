using System.Linq;
using Rpg.Application.Battle.Commands;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Navigation;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle;

internal static class BattleRuntimeTacticalCommandLifecycle
{
    private const int FailureThreshold = 3;

    internal static void Advance(
        BattleRuntimeState state,
        BattleEventStream stream,
        string battleId,
        int tick,
        double currentTimeSeconds)
    {
        foreach (BattleGroupTacticalState command in state.TacticalStates.Values
                     .Where(item => item.HasActiveTacticalCommand)
                     .OrderBy(item => item.BattleGroupId, System.StringComparer.Ordinal))
        {
            BattleRuntimeActor[] members = state.Actors
                .Where(actor => actor.Kind == BattleRuntimeActorKind.Corps &&
                                actor.HitPoints > 0 &&
                                !actor.HasRetreated &&
                                string.Equals(actor.BattleGroupId ?? "", command.BattleGroupId ?? "", System.StringComparison.Ordinal))
                .OrderBy(actor => actor.ActorId, System.StringComparer.Ordinal)
                .ToArray();
            if (members.Length == 0 || members.Any(actor => actor.ConsecutiveAdvanceFailures >= FailureThreshold))
            {
                string failure = members.FirstOrDefault(actor => actor.ConsecutiveAdvanceFailures >= FailureThreshold)?.LastAdvanceFailureReason;
                stream.Add(BuildEvent(command, battleId, tick, currentTimeSeconds, BattleEventKind.CommandFailed,
                    string.IsNullOrWhiteSpace(failure) ? "tactical_command_members_unavailable" : failure));
                state.TacticalStateStore.TryFinishTacticalCommand(command.BattleGroupId, retreatCompleted: false);
                continue;
            }

            if (!command.HasTacticalCommandTarget || !members.All(actor => HasReached(actor, command)))
            {
                continue;
            }

            bool retreat = command.ActiveTacticalCommandKind == CommandKind.Retreat;
            state.TacticalStateStore.TryFinishTacticalCommand(command.BattleGroupId, retreat);
            if (retreat)
            {
                foreach (BattleRuntimeActor actor in state.Actors.Where(actor =>
                             string.Equals(actor.BattleGroupId ?? "", command.BattleGroupId ?? "", System.StringComparison.Ordinal)))
                {
                    actor.TargetActorId = "";
                    BattleRuntimeActorStateMachine.MarkHolding(actor, currentTimeSeconds);
                    actor.HasRetreated = true;
                }
            }

            state.TacticalStateStore.SynchronizeActorExecutionCaches(state.Actors);
            stream.Add(BuildEvent(
                command,
                battleId,
                tick,
                currentTimeSeconds,
                BattleEventKind.CommandCompleted,
                retreat ? "retreat_completed" : "regroup_completed"));
        }
    }

    private static bool HasReached(BattleRuntimeActor actor, BattleGroupTacticalState command)
    {
        BattleGridCoord actorAnchor = new(actor.GridX, actor.GridY, actor.GridHeight);
        BattleGridCoord targetAnchor = new(
            command.TacticalCommandTargetGridX,
            command.TacticalCommandTargetGridY,
            command.TacticalCommandTargetGridHeight);
        BattleRuntimeActor target = new()
        {
            GridX = targetAnchor.X,
            GridY = targetAnchor.Y,
            GridHeight = targetAnchor.Height,
            FootprintWidth = 1,
            FootprintHeight = 1
        };
        return BattleActorFootprint.GetGap(actor, actorAnchor, target, targetAnchor) <= 1;
    }

    private static BattleEvent BuildEvent(
        BattleGroupTacticalState command,
        string battleId,
        int tick,
        double currentTimeSeconds,
        BattleEventKind kind,
        string reasonCode) => new()
    {
        EventId = $"{battleId}:tick_{tick}:{command.ActiveCommandId}:{command.BattleGroupId}:{kind}",
        BattleId = battleId ?? "",
        BattleGroupId = command.BattleGroupId ?? "",
        SourceCommandId = command.ActiveCommandId ?? "",
        Kind = kind,
        ReasonCode = reasonCode ?? "",
        RuntimeTick = tick,
        RuntimeTimeSeconds = currentTimeSeconds,
        HasTargetCells = command.HasTacticalCommandTarget,
        TargetGridX = command.TacticalCommandTargetGridX,
        TargetGridY = command.TacticalCommandTargetGridY,
        TargetGridHeight = command.TacticalCommandTargetGridHeight
    };
}
