using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;

namespace Rpg.Application.Battle.Commands;

public sealed class BattleCommandApplicationValidator
{
    public CommandValidationResult Validate(
        CommandRequest request,
        BattleStartSnapshot snapshot,
        string playerFactionId)
    {
        if (request == null)
        {
            return CommandValidationResult.Reject(CommandRejectionStage.Application, "command_missing");
        }

        if (snapshot == null ||
            string.IsNullOrWhiteSpace(snapshot.BattleId) ||
            string.IsNullOrWhiteSpace(request.BattleId))
        {
            return CommandValidationResult.Reject(CommandRejectionStage.Application, "battle_missing");
        }

        if (!string.Equals(request.BattleId.Trim(), snapshot.BattleId.Trim(), System.StringComparison.Ordinal))
        {
            return CommandValidationResult.Reject(CommandRejectionStage.Application, "battle_id_mismatch");
        }

        string normalizedPlayerFactionId = playerFactionId?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(normalizedPlayerFactionId))
        {
            return CommandValidationResult.Reject(CommandRejectionStage.Application, "player_faction_missing");
        }

        string[] requestedGroupIds = ResolveRequestedBattleGroupIds(request);
        if (requestedGroupIds.Length == 0)
        {
            return CommandValidationResult.Reject(CommandRejectionStage.Application, "battle_group_unavailable");
        }

        foreach (string groupId in requestedGroupIds)
        {
            BattleGroupSnapshot[] matches = (snapshot.BattleGroups ?? new List<BattleGroupSnapshot>())
                .Where(group => string.Equals(
                    BattleCommanderGroupIdentity.Resolve(group),
                    groupId,
                    System.StringComparison.Ordinal))
                .ToArray();
            if (matches.Length == 0)
            {
                return CommandValidationResult.Reject(CommandRejectionStage.Application, "battle_group_unavailable");
            }

            if (matches.Any(group => !string.Equals(
                    group?.FactionId?.Trim() ?? "",
                    normalizedPlayerFactionId,
                    System.StringComparison.Ordinal)))
            {
                return CommandValidationResult.Reject(CommandRejectionStage.Application, "battle_group_not_owned");
            }
        }

        if (request.Kind == CommandKind.DestinationBeacon)
        {
            if (!request.HasTargetGrid)
            {
                return CommandValidationResult.Reject(CommandRejectionStage.Application, "destination_missing");
            }

            return request.Channel == CommandChannel.Combined
                ? CommandValidationResult.Accept()
                : CommandValidationResult.Reject(CommandRejectionStage.Application, "command_channel_unavailable");
        }

        if (request.Kind == CommandKind.CastSkill)
        {
            return ValidateSkillSubmission(request, snapshot);
        }

        bool channelAllowed = request.Channel is CommandChannel.Hero or CommandChannel.Corps or CommandChannel.Combined;
        return channelAllowed
            ? CommandValidationResult.Accept()
            : CommandValidationResult.Reject(CommandRejectionStage.Application, "command_channel_unavailable");
    }

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

    private static CommandValidationResult ValidateSkillSubmission(
        CommandRequest request,
        BattleStartSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(request.SkillDefinitionId))
        {
            return CommandValidationResult.Reject(CommandRejectionStage.Application, "skill_definition_missing");
        }

        SnapshotActorIdentity source = ResolveSourceActor(snapshot, request.BattleGroupId, request.SourceActorId);
        if (source == null)
        {
            return CommandValidationResult.Reject(CommandRejectionStage.Application, "source_actor_unavailable");
        }

        string skillDefinitionId = request.SkillDefinitionId.Trim();
        BattleSkillSnapshot[] definitions = (snapshot.SkillDefinitions ?? new List<BattleSkillSnapshot>())
            .Where(skill => string.Equals(
                skill?.SkillDefinitionId?.Trim() ?? "",
                skillDefinitionId,
                System.StringComparison.Ordinal))
            .ToArray();
        if (definitions.Length == 0)
        {
            return CommandValidationResult.Reject(CommandRejectionStage.Application, "skill_definition_missing");
        }

        BattleSkillSnapshot skill = definitions.FirstOrDefault(candidate => SkillMatchesSource(candidate, source));
        if (skill == null)
        {
            return CommandValidationResult.Reject(CommandRejectionStage.Application, "skill_caster_not_allowed");
        }

        if (!ChannelMatches(request.Channel, skill.CommandChannel))
        {
            return CommandValidationResult.Reject(CommandRejectionStage.Application, "skill_command_channel_mismatch");
        }

        if (!SourceKindMatches(source.Kind, skill.CommandChannel))
        {
            return CommandValidationResult.Reject(CommandRejectionStage.Application, "source_actor_kind_mismatch");
        }

        return skill.SkillType == BattleSkillType.Active
            ? CommandValidationResult.Accept()
            : CommandValidationResult.Reject(CommandRejectionStage.Application, "skill_not_active");
    }

    private static SnapshotActorIdentity ResolveSourceActor(
        BattleStartSnapshot snapshot,
        string battleGroupId,
        string sourceActorId)
    {
        string normalizedGroupId = battleGroupId?.Trim() ?? "";
        string normalizedActorId = sourceActorId?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(normalizedGroupId) || string.IsNullOrWhiteSpace(normalizedActorId))
        {
            return null;
        }

        Dictionary<string, int> sourceForceIndexes = new(System.StringComparer.Ordinal);
        foreach (BattleGroupSnapshot group in snapshot?.BattleGroups ?? new List<BattleGroupSnapshot>())
        {
            string commanderGroupId = BattleCommanderGroupIdentity.Resolve(group);
            string sourceForceId = string.IsNullOrWhiteSpace(group?.SourceForceId)
                ? group?.BattleGroupId ?? ""
                : group.SourceForceId.Trim();
            sourceForceIndexes.TryGetValue(sourceForceId, out int sourceForceIndex);
            sourceForceIndexes[sourceForceId] = sourceForceIndex + 1;

            if (!string.Equals(commanderGroupId, normalizedGroupId, System.StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals($"{group?.BattleGroupId ?? ""}:hero", normalizedActorId, System.StringComparison.Ordinal))
            {
                return new SnapshotActorIdentity(
                    normalizedActorId,
                    SnapshotActorKind.Hero,
                    group,
                    ResolveHeroUnitId(group));
            }

            if (string.Equals($"{sourceForceId}:{sourceForceIndex + 1}", normalizedActorId, System.StringComparison.Ordinal))
            {
                return new SnapshotActorIdentity(
                    normalizedActorId,
                    SnapshotActorKind.Corps,
                    group,
                    ResolveCorpsUnitId(group));
            }
        }

        return null;
    }

    private static bool SkillMatchesSource(BattleSkillSnapshot skill, SnapshotActorIdentity source)
    {
        if (skill == null || source?.Group == null)
        {
            return false;
        }

        string commanderGroupId = BattleCommanderGroupIdentity.Resolve(source.Group);
        bool hasHeroOwner = !string.IsNullOrWhiteSpace(skill.OwnerHeroId);
        if (hasHeroOwner &&
            !string.Equals(skill.OwnerHeroId.Trim(), source.Group.HeroId?.Trim() ?? "", System.StringComparison.Ordinal))
        {
            return false;
        }

        if (!hasHeroOwner &&
            !string.IsNullOrWhiteSpace(skill.OwnerBattleGroupId) &&
            !MatchesGroupIdentity(skill.OwnerBattleGroupId, source.Group, commanderGroupId))
        {
            return false;
        }

        if (!hasHeroOwner &&
            !string.IsNullOrWhiteSpace(skill.RuntimeCommanderGroupId) &&
            !string.Equals(skill.RuntimeCommanderGroupId.Trim(), commanderGroupId, System.StringComparison.Ordinal))
        {
            return false;
        }

        string[] casterUnitIds = (skill.CasterUnitIds ?? new List<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .ToArray();
        if (casterUnitIds.Length == 0)
        {
            return true;
        }

        string[] groupUnitIds =
        {
            ResolveHeroUnitId(source.Group),
            ResolveCorpsUnitId(source.Group)
        };
        return groupUnitIds.Any(unitId => casterUnitIds.Contains(unitId, System.StringComparer.Ordinal));
    }

    private static bool MatchesGroupIdentity(
        string requestedGroupId,
        BattleGroupSnapshot group,
        string commanderGroupId)
    {
        string normalized = requestedGroupId?.Trim() ?? "";
        return string.Equals(normalized, commanderGroupId, System.StringComparison.Ordinal) ||
               string.Equals(normalized, group?.BattleGroupId?.Trim() ?? "", System.StringComparison.Ordinal);
    }

    private static bool ChannelMatches(CommandChannel requestChannel, BattleSkillCommandChannel skillChannel)
    {
        return requestChannel switch
        {
            CommandChannel.Hero => skillChannel == BattleSkillCommandChannel.Hero,
            CommandChannel.Corps => skillChannel == BattleSkillCommandChannel.Corps,
            CommandChannel.Combined => skillChannel == BattleSkillCommandChannel.Combined,
            _ => false
        };
    }

    private static bool SourceKindMatches(SnapshotActorKind sourceKind, BattleSkillCommandChannel skillChannel)
    {
        return skillChannel switch
        {
            // A2 keeps the selected visible group actor as the cast source while
            // hero ownership still comes from the compiled group/hero snapshot.
            BattleSkillCommandChannel.Hero => sourceKind is SnapshotActorKind.Hero or SnapshotActorKind.Corps,
            BattleSkillCommandChannel.Corps => sourceKind == SnapshotActorKind.Corps,
            BattleSkillCommandChannel.Combined => sourceKind is SnapshotActorKind.Hero or SnapshotActorKind.Corps,
            _ => false
        };
    }

    private static string ResolveHeroUnitId(BattleGroupSnapshot group)
    {
        return !string.IsNullOrWhiteSpace(group?.HeroBattleUnitId)
            ? group.HeroBattleUnitId.Trim()
            : group?.HeroDefinitionId?.Trim() ?? "";
    }

    private static string ResolveCorpsUnitId(BattleGroupSnapshot group)
    {
        return !string.IsNullOrWhiteSpace(group?.CorpsBattleUnitId)
            ? group.CorpsBattleUnitId.Trim()
            : group?.CorpsDefinitionId?.Trim() ?? "";
    }

    private enum SnapshotActorKind
    {
        Hero,
        Corps
    }

    private sealed record SnapshotActorIdentity(
        string ActorId,
        SnapshotActorKind Kind,
        BattleGroupSnapshot Group,
        string UnitDefinitionId);
}
