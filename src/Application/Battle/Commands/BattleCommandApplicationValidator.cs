using System.Collections.Generic;
using System.Linq;

namespace Rpg.Application.Battle.Commands;

public sealed class BattleCommandApplicationValidator
{
    public CommandValidationResult Validate(
        CommandRequest request,
        IEnumerable<string> availableBattleGroupIds,
        bool allowHero,
        bool allowCorps,
        bool allowCombined)
    {
        if (request == null)
        {
            return CommandValidationResult.Reject(CommandRejectionStage.Application, "command_missing");
        }

        if (string.IsNullOrWhiteSpace(request.BattleId))
        {
            return CommandValidationResult.Reject(CommandRejectionStage.Application, "battle_missing");
        }

        System.Collections.Generic.HashSet<string> available = new(
            availableBattleGroupIds ?? System.Array.Empty<string>(),
            System.StringComparer.Ordinal);
        string[] requestedGroupIds = ResolveRequestedBattleGroupIds(request);
        if (requestedGroupIds.Length == 0 ||
            requestedGroupIds.Any(groupId => !available.Contains(groupId)))
        {
            return CommandValidationResult.Reject(CommandRejectionStage.Application, "battle_group_unavailable");
        }

        if (request.Kind == CommandKind.DestinationBeacon && !request.HasTargetGrid)
        {
            return CommandValidationResult.Reject(CommandRejectionStage.Application, "destination_missing");
        }

        bool channelAllowed = request.Channel switch
        {
            CommandChannel.Hero => allowHero,
            CommandChannel.Corps => allowCorps,
            CommandChannel.Combined => allowCombined,
            _ => false
        };

        return channelAllowed
            ? CommandValidationResult.Accept()
            : CommandValidationResult.Reject(CommandRejectionStage.Application, "command_channel_unavailable");
    }

    private static string[] ResolveRequestedBattleGroupIds(CommandRequest request)
    {
        System.Collections.Generic.List<string> groupIds = new();
        foreach (string groupId in request?.BattleGroupIds ?? Enumerable.Empty<string>())
        {
            string normalized = groupId?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(normalized) &&
                !groupIds.Contains(normalized, System.StringComparer.Ordinal))
            {
                groupIds.Add(normalized);
            }
        }

        string primary = request?.BattleGroupId?.Trim() ?? "";
        if (!string.IsNullOrWhiteSpace(primary) &&
            !groupIds.Contains(primary, System.StringComparer.Ordinal))
        {
            groupIds.Insert(0, primary);
        }

        return groupIds.ToArray();
    }
}
