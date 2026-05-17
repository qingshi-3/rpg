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

        bool groupAvailable = availableBattleGroupIds?.Contains(request.BattleGroupId) == true;
        if (!groupAvailable)
        {
            return CommandValidationResult.Reject(CommandRejectionStage.Application, "battle_group_unavailable");
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
}
