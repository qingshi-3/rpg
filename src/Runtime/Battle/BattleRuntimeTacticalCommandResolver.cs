using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Commands;
using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Navigation;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle;

// Regroup and retreat are commander commands. This resolver chooses and validates
// one deterministic group target before mutating any selected commander state.
internal static class BattleRuntimeTacticalCommandResolver
{
    internal static BattleRuntimeCommandSubmitResult Submit(
        BattleRuntimeState state,
        BattleEventStream stream,
        string battleId,
        int tick,
        double currentTimeSeconds,
        CommandRequest request,
        BattleNavigationGraph navigationGraph)
    {
        int startIndex = stream?.Events.Count ?? 0;
        string[] groupIds = ResolveGroupIds(request);
        if (state?.Actors == null || stream == null || navigationGraph == null || request == null ||
            request.Kind is not (CommandKind.Regroup or CommandKind.Retreat) ||
            groupIds.Length == 0)
        {
            return Reject(stream, startIndex, battleId, tick, currentTimeSeconds, request, "tactical_command_invalid");
        }

        List<GroupCommandPlan> plans = new();
        foreach (string groupId in groupIds)
        {
            BattleRuntimeActor[] members = state.Actors
                .Where(actor => actor.Kind == BattleRuntimeActorKind.Corps &&
                                actor.HitPoints > 0 &&
                                !actor.HasRetreated &&
                                string.Equals(actor.BattleGroupId ?? "", groupId, System.StringComparison.Ordinal))
                .OrderBy(actor => actor.ActorId, System.StringComparer.Ordinal)
                .ToArray();
            if (members.Length == 0)
            {
                return Reject(stream, startIndex, battleId, tick, currentTimeSeconds, request, "battle_group_unavailable", groupId);
            }

            if (members.Any(IsActionLocked))
            {
                return Reject(stream, startIndex, battleId, tick, currentTimeSeconds, request, "tactical_command_action_locked", groupId);
            }

            BattleGroupTacticalState commander = state.TacticalStateStore.GetRequiredSnapshot(groupId);
            if (!TryResolveTarget(state, commander, members, request.Kind, navigationGraph, out BattleGridCoord target))
            {
                string reason = request.Kind == CommandKind.Regroup
                    ? "regroup_target_unreachable"
                    : "retreat_target_unreachable";
                return Reject(stream, startIndex, battleId, tick, currentTimeSeconds, request, reason, groupId);
            }

            plans.Add(new GroupCommandPlan(groupId, members, target));
        }

        foreach (GroupCommandPlan plan in plans)
        {
            state.TacticalStateStore.TryApplyTacticalCommand(
                plan.GroupId,
                request.CommandId,
                request.Kind,
                plan.Target.X,
                plan.Target.Y,
                plan.Target.Height,
                out string previousCommandId,
                out BattleGroupPlanRuntimeState previousPlanState);

            if (!string.IsNullOrWhiteSpace(previousCommandId) &&
                !string.Equals(previousCommandId, request.CommandId, System.StringComparison.Ordinal))
            {
                stream.Add(BuildEvent(
                    battleId, tick, currentTimeSeconds, plan.GroupId, previousCommandId,
                    BattleEventKind.CommandInterrupted, "command_superseded", plan.Target));
            }

            foreach (BattleRuntimeActor actor in state.Actors.Where(actor =>
                         string.Equals(actor.BattleGroupId ?? "", plan.GroupId, System.StringComparison.Ordinal)))
            {
                actor.TargetActorId = "";
                if (actor.Phase == BattleRuntimeActorPhase.AttackWindup && !actor.CurrentBasicAttackImpactApplied)
                {
                    BattleRuntimeActorStateMachine.MarkAnchoredDecision(actor);
                }
                else
                {
                    BattleRuntimeActorStateMachine.ClearBasicAttackAction(actor);
                    BattleRuntimeActorStateMachine.ClearMovementIntentSnapshot(actor);
                }
                state.TacticalStateStore.SynchronizeActorExecutionCache(actor);
            }

            BattleGroupTacticalState applied = state.TacticalStateStore.GetRequiredSnapshot(plan.GroupId);
            BattlePlanStateEmitter.EmitAppliedTransition(
                stream,
                battleId,
                tick,
                currentTimeSeconds,
                applied,
                plan.Members[0],
                previousPlanState,
                request.Kind == CommandKind.Regroup ? "regroup_accepted" : "retreat_accepted");
            stream.Add(BuildEvent(
                battleId, tick, currentTimeSeconds, plan.GroupId, request.CommandId,
                BattleEventKind.CommandAccepted,
                request.Kind == CommandKind.Regroup ? "regroup_accepted" : "retreat_accepted",
                plan.Target));
        }

        RemoveSupersededBeaconOwnership(state, groupIds);
        GameLog.Info(
            nameof(BattleRuntimeTacticalCommandResolver),
            $"BattleTacticalCommandAccepted battle={battleId ?? ""} command={request.CommandId ?? ""} kind={request.Kind} groups={string.Join("|", groupIds)}");
        return new BattleRuntimeCommandSubmitResult
        {
            Accepted = true,
            ReasonCode = request.Kind == CommandKind.Regroup ? "regroup_accepted" : "retreat_accepted",
            Events = stream.Events.Skip(startIndex).ToArray()
        };
    }

    private static bool IsActionLocked(BattleRuntimeActor actor) =>
        actor.Phase is BattleRuntimeActorPhase.SkillCasting or
            BattleRuntimeActorPhase.SkillRecovery or
            BattleRuntimeActorPhase.AttackRecovery ||
        actor.PendingAbilityOrders.Count > 0;

    private static bool TryResolveTarget(
        BattleRuntimeState state,
        BattleGroupTacticalState commander,
        IReadOnlyList<BattleRuntimeActor> members,
        CommandKind kind,
        BattleNavigationGraph graph,
        out BattleGridCoord target)
    {
        List<BattleGridCoord> candidates = new();
        if (kind == CommandKind.Retreat)
        {
            candidates.Add(new BattleGridCoord(
                commander.InitialDeploymentGridX,
                commander.InitialDeploymentGridY,
                commander.InitialDeploymentGridHeight));
        }
        else
        {
            if (state.GroupActionZones.TryGetValue(commander.BattleGroupId, out BattleGroupActionZoneSnapshot actionZone))
            {
                candidates.Add(new BattleGridCoord(actionZone.CenterCellX, actionZone.CenterCellY, actionZone.CenterCellHeight));
            }
            candidates.AddRange(members.Select(actor => new BattleGridCoord(actor.GridX, actor.GridY, actor.GridHeight)));
        }

        foreach (BattleGridCoord candidate in candidates.Distinct().OrderBy(item => item.X).ThenBy(item => item.Y).ThenBy(item => item.Height))
        {
            if (members.All(member => IsReachable(member, candidate, graph)))
            {
                target = candidate;
                return true;
            }
        }

        target = default;
        return false;
    }

    private static bool IsReachable(BattleRuntimeActor actor, BattleGridCoord target, BattleNavigationGraph graph)
    {
        BattleGridCoord start = new(actor.GridX, actor.GridY, actor.GridHeight);
        if (!graph.CanPlaceFootprint(actor, start) || !graph.CanPlaceFootprint(actor, target))
        {
            return false;
        }

        HashSet<BattleGridCoord> visited = new() { start };
        Queue<BattleGridCoord> frontier = new();
        frontier.Enqueue(start);
        while (frontier.Count > 0)
        {
            BattleGridCoord current = frontier.Dequeue();
            if (current == target)
            {
                return true;
            }
            foreach (BattleGridCoord neighbor in graph.GetNeighbors(current))
            {
                if (visited.Add(neighbor) && BattlePathStepRules.CanUseStaticStep(actor, current, neighbor, graph))
                {
                    frontier.Enqueue(neighbor);
                }
            }
        }
        return false;
    }

    private static void RemoveSupersededBeaconOwnership(BattleRuntimeState state, IReadOnlyCollection<string> groupIds)
    {
        foreach (BattleRuntimeDestinationBeacon beacon in state.DestinationBeacons)
        {
            beacon.OwnerBattleGroupIds.RemoveAll(groupId => groupIds.Contains(groupId));
        }
        state.DestinationBeacons.RemoveAll(beacon => beacon.OwnerBattleGroupIds.Count == 0);
    }

    private static BattleRuntimeCommandSubmitResult Reject(
        BattleEventStream stream,
        int startIndex,
        string battleId,
        int tick,
        double currentTimeSeconds,
        CommandRequest request,
        string reasonCode,
        string groupId = "")
    {
        stream?.Add(BuildEvent(
            battleId, tick, currentTimeSeconds,
            string.IsNullOrWhiteSpace(groupId) ? request?.BattleGroupId ?? "" : groupId,
            request?.CommandId ?? "", BattleEventKind.CommandRejected, reasonCode, default));
        return new BattleRuntimeCommandSubmitResult
        {
            Accepted = false,
            ReasonCode = reasonCode,
            Events = stream?.Events.Skip(startIndex).ToArray() ?? System.Array.Empty<BattleEvent>()
        };
    }

    private static BattleEvent BuildEvent(
        string battleId,
        int tick,
        double currentTimeSeconds,
        string groupId,
        string commandId,
        BattleEventKind kind,
        string reasonCode,
        BattleGridCoord target) => new()
    {
        EventId = $"{battleId}:tick_{tick}:{commandId}:{groupId}:{kind}",
        BattleId = battleId ?? "",
        BattleGroupId = groupId ?? "",
        SourceCommandId = commandId ?? "",
        Kind = kind,
        ReasonCode = reasonCode ?? "",
        RuntimeTick = tick,
        RuntimeTimeSeconds = currentTimeSeconds,
        HasTargetCells = true,
        TargetGridX = target.X,
        TargetGridY = target.Y,
        TargetGridHeight = target.Height
    };

    private static string[] ResolveGroupIds(CommandRequest request)
    {
        List<string> groupIds = new();
        foreach (string groupId in new[] { request?.BattleGroupId }.Concat(request?.BattleGroupIds ?? Enumerable.Empty<string>()))
        {
            string normalized = groupId?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(normalized) && !groupIds.Contains(normalized, System.StringComparer.Ordinal))
            {
                groupIds.Add(normalized);
            }
        }
        return groupIds.ToArray();
    }

    private sealed record GroupCommandPlan(string GroupId, BattleRuntimeActor[] Members, BattleGridCoord Target);
}
