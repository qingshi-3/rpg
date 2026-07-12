using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Commands;
using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Navigation;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle;

internal static class BattleRuntimeDestinationBeaconCommandResolver
{
    internal static BattleRuntimeCommandSubmitResult Submit(
        BattleRuntimeState state,
        BattleEventStream eventStream,
        string battleId,
        int tick,
        double currentTimeSeconds,
        CommandRequest request,
        BattleNavigationGraph navigationGraph)
    {
        int startIndex = eventStream?.Events.Count ?? 0;
        string commandId = request?.CommandId ?? "";
        string[] groupIds = ResolveBattleGroupIds(request);
        BattleGridCoord destination = new(request?.TargetGridX ?? 0, request?.TargetGridY ?? 0, request?.TargetGridHeight ?? 0);
        if (state?.Actors == null ||
            eventStream == null ||
            request == null ||
            !string.Equals(request.BattleId ?? "", battleId ?? "", System.StringComparison.Ordinal) ||
            groupIds.Length == 0 ||
            !request.HasTargetGrid ||
            navigationGraph == null)
        {
            return Reject(eventStream, startIndex, battleId, tick, currentTimeSeconds, commandId, request?.BattleGroupId ?? "", destination, "destination_command_invalid");
        }

        List<BattleRuntimeActor> selectedCorps = new();
        foreach (string groupId in groupIds)
        {
            BattleRuntimeActor corps = state.Actors
                .Where(actor => actor.Kind == BattleRuntimeActorKind.Corps &&
                                actor.HitPoints > 0 &&
                                string.Equals(actor.BattleGroupId ?? "", groupId, System.StringComparison.Ordinal))
                .OrderBy(actor => actor.ActorId, System.StringComparer.Ordinal)
                .FirstOrDefault();
            if (corps == null ||
                !BattleRuntimeIdentityRules.IsPlayerFaction(corps.FactionId))
            {
                return Reject(eventStream, startIndex, battleId, tick, currentTimeSeconds, commandId, groupId, destination, "battle_group_unavailable");
            }

            selectedCorps.Add(corps);
        }

        foreach (BattleRuntimeActor corps in selectedCorps)
        {
            if (!IsDestinationReachable(corps, destination, navigationGraph))
            {
                return Reject(eventStream, startIndex, battleId, tick, currentTimeSeconds, commandId, corps.BattleGroupId, destination, "destination_unreachable");
            }
        }

        BattleRuntimeDestinationBeacon beacon = CreateOrMoveBeacon(state, request, groupIds, destination);
        for (int index = 0; index < groupIds.Length; index++)
        {
            string groupId = groupIds[index];
            if (!state.TacticalStateStore.TryApplyDestinationCommand(
                    groupId,
                    commandId,
                    beacon.BeaconId,
                    beacon.Revision,
                    beacon.Anchor.X,
                    beacon.Anchor.Y,
                    beacon.Anchor.Height,
                    out BattleGroupPlanRuntimeState previousPlanState))
            {
                continue;
            }

            BattleGroupTacticalState commanderState = state.TacticalStateStore.GetRequiredSnapshot(groupId);
            BattlePlanStateEmitter.EmitAppliedTransition(
                eventStream,
                battleId,
                tick,
                currentTimeSeconds,
                commanderState,
                selectedCorps[index],
                previousPlanState,
                "destination_beacon_accepted");
        }

        foreach (BattleRuntimeActor actor in state.Actors.Where(actor => groupIds.Contains(actor.BattleGroupId ?? "", System.StringComparer.Ordinal)))
        {
            state.TacticalStateStore.SynchronizeActorExecutionCache(actor);
            // Replacing a beacon changes the movement intent. Runtime keeps any
            // in-progress action lock, then the next decision boundary rebuilds
            // movement from the new command-scoped flow field.
            BattleRuntimeActorStateMachine.ClearMovementIntentSnapshot(actor);
        }

        eventStream.Add(new BattleEvent
        {
            EventId = $"{battleId}:tick_{tick}:{commandId}:destination_beacon_accepted",
            BattleId = battleId ?? "",
            BattleGroupId = string.Join(",", groupIds),
            SourceCommandId = commandId,
            TargetId = beacon.BeaconId,
            Kind = BattleEventKind.CommandAccepted,
            ReasonCode = "destination_beacon_accepted",
            RuntimeTick = tick,
            RuntimeTimeSeconds = currentTimeSeconds,
            HasTargetCells = true,
            TargetGridX = destination.X,
            TargetGridY = destination.Y,
            TargetGridHeight = destination.Height
        });

        GameLog.Info(
            nameof(BattleRuntimeDestinationBeaconCommandResolver),
            $"BattleDestinationBeaconAccepted battle={battleId ?? ""} command={commandId} beacon={beacon.BeaconId} revision={beacon.Revision} groups={string.Join("|", groupIds)} anchor={destination}");

        return new BattleRuntimeCommandSubmitResult
        {
            Accepted = true,
            ReasonCode = "destination_beacon_accepted",
            Events = eventStream.Events.Skip(startIndex).ToArray()
        };
    }

    private static BattleRuntimeCommandSubmitResult Reject(
        BattleEventStream eventStream,
        int startIndex,
        string battleId,
        int tick,
        double currentTimeSeconds,
        string commandId,
        string groupId,
        BattleGridCoord destination,
        string reasonCode)
    {
        eventStream?.Add(new BattleEvent
        {
            EventId = $"{battleId}:tick_{tick}:{commandId}:destination_beacon_rejected",
            BattleId = battleId ?? "",
            BattleGroupId = groupId ?? "",
            SourceCommandId = commandId ?? "",
            Kind = BattleEventKind.CommandRejected,
            ReasonCode = reasonCode ?? "",
            RuntimeTick = tick,
            RuntimeTimeSeconds = currentTimeSeconds,
            HasTargetCells = true,
            TargetGridX = destination.X,
            TargetGridY = destination.Y,
            TargetGridHeight = destination.Height
        });
        GameLog.Info(
            nameof(BattleRuntimeDestinationBeaconCommandResolver),
            $"BattleDestinationBeaconRejected battle={battleId ?? ""} command={commandId ?? ""} group={groupId ?? ""} anchor={destination} reason={reasonCode ?? ""}");

        return new BattleRuntimeCommandSubmitResult
        {
            Accepted = false,
            ReasonCode = reasonCode ?? "",
            Events = eventStream?.Events.Skip(startIndex).ToArray() ?? System.Array.Empty<BattleEvent>()
        };
    }

    private static BattleRuntimeDestinationBeacon CreateOrMoveBeacon(
        BattleRuntimeState state,
        CommandRequest request,
        IReadOnlyList<string> groupIds,
        BattleGridCoord destination)
    {
        string requestedBeaconId = request?.BeaconId?.Trim() ?? "";
        BattleRuntimeDestinationBeacon beacon = string.IsNullOrWhiteSpace(requestedBeaconId)
            ? null
            : state.DestinationBeacons.FirstOrDefault(item => string.Equals(item.BeaconId ?? "", requestedBeaconId, System.StringComparison.Ordinal));
        if (beacon == null)
        {
            beacon = new BattleRuntimeDestinationBeacon
            {
                BeaconId = string.IsNullOrWhiteSpace(requestedBeaconId)
                    ? $"{request?.CommandId ?? "command"}:destination"
                    : requestedBeaconId
            };
            state.DestinationBeacons.Add(beacon);
        }
        else
        {
            beacon.Revision++;
        }

        foreach (BattleRuntimeDestinationBeacon existing in state.DestinationBeacons)
        {
            if (ReferenceEquals(existing, beacon))
            {
                continue;
            }

            existing.OwnerBattleGroupIds.RemoveAll(groupId => groupIds.Contains(groupId, System.StringComparer.Ordinal));
        }
        // A beacon without owner groups is no longer an active command fact. Keeping
        // it visible would leave stale destination markers and stale path targets.
        state.DestinationBeacons.RemoveAll(item => !ReferenceEquals(item, beacon) && item.OwnerBattleGroupIds.Count == 0);

        beacon.CommandId = request?.CommandId ?? "";
        beacon.Anchor = destination;
        beacon.IsValid = true;
        beacon.OwnerBattleGroupIds.Clear();
        foreach (string groupId in groupIds.OrderBy(item => item, System.StringComparer.Ordinal))
        {
            beacon.OwnerBattleGroupIds.Add(groupId);
        }

        return beacon;
    }

    private static bool IsDestinationReachable(
        BattleRuntimeActor actor,
        BattleGridCoord destination,
        BattleNavigationGraph graph)
    {
        BattleGridCoord start = new(actor.GridX, actor.GridY, actor.GridHeight);
        if (!graph.CanPlaceFootprint(actor, start) ||
            !graph.CanPlaceFootprint(actor, destination))
        {
            return false;
        }

        HashSet<BattleGridCoord> visited = new() { start };
        Queue<BattleGridCoord> frontier = new();
        frontier.Enqueue(start);
        while (frontier.Count > 0)
        {
            BattleGridCoord current = frontier.Dequeue();
            if (current == destination)
            {
                return true;
            }

            foreach (BattleGridCoord neighbor in graph.GetNeighbors(current))
            {
                if (!visited.Add(neighbor) ||
                    !BattlePathStepRules.CanUseStaticStep(actor, current, neighbor, graph))
                {
                    continue;
                }

                frontier.Enqueue(neighbor);
            }
        }

        return false;
    }

    private static string[] ResolveBattleGroupIds(CommandRequest request)
    {
        List<string> groupIds = new();
        string primary = request?.BattleGroupId?.Trim() ?? "";
        if (!string.IsNullOrWhiteSpace(primary))
        {
            groupIds.Add(primary);
        }

        foreach (string groupId in request?.BattleGroupIds ?? Enumerable.Empty<string>())
        {
            string normalized = groupId?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(normalized) &&
                !groupIds.Contains(normalized, System.StringComparer.Ordinal))
            {
                groupIds.Add(normalized);
            }
        }

        return groupIds.ToArray();
    }
}
