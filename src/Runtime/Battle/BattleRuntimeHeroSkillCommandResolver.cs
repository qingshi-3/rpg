using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Commands;
using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle.Effects;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle;

internal static partial class BattleRuntimeHeroSkillCommandResolver
{
    private const string SkillDefinitionIdRequiredReason = "skill_definition_id_required";

    internal static string NormalizeSkillDefinitionId(string skillDefinitionId)
    {
        return string.IsNullOrWhiteSpace(skillDefinitionId) ? "" : skillDefinitionId.Trim();
    }

    internal static BattleRuntimeCommandSubmitResult Submit(
        BattleRuntimeState state,
        BattleEventStream stream,
        string battleId,
        int runtimeTick,
        double runtimeTimeSeconds,
        CommandRequest request,
        BattleNavigationGraph navigationGraph = null)
    {
        int startIndex = stream?.Events.Count ?? 0;
        string reason = ValidateSubmission(
            state,
            stream,
            battleId,
            runtimeTick,
            runtimeTimeSeconds,
            request,
            navigationGraph,
            out string commandId,
            out BattleSkillSnapshot skill,
            out BattleRuntimeActor caster,
            out BattleRuntimeActor target);
        if (!string.IsNullOrWhiteSpace(reason))
        {
            ResolveSkillRangeDiagnostic(caster, target, skill, out int rejectedRange, out int rejectedGap);
            AddCommandRejected(
                stream,
                battleId,
                runtimeTick,
                runtimeTimeSeconds,
                request,
                commandId,
                NormalizeSkillDefinitionId(request?.SkillDefinitionId),
                caster,
                request?.TargetActorId ?? "",
                target,
                reason,
                rejectedRange,
                rejectedGap);
            return BuildResult(stream, startIndex, accepted: false, reason);
        }

        BattleRuntimePendingHeroSkillCommand pendingOrder = new()
        {
            CommandId = commandId,
            BattleGroupId = request.BattleGroupId ?? "",
            SourceActorId = caster.ActorId ?? "",
            SkillDefinitionId = ResolveSkillDefinitionId(skill),
            GrantedSkillId = skill.GrantedSkillId ?? "",
            LoadoutSlotId = skill.LoadoutSlotId ?? "",
            OwnerHeroId = skill.OwnerHeroId ?? "",
            TargetActorId = target?.ActorId ?? "",
            HasTargetGrid = request.HasTargetGrid,
            TargetGridX = request.TargetGridX,
            TargetGridY = request.TargetGridY,
            TargetGridHeight = request.TargetGridHeight,
            SelectedSpatialMarkId = request.SelectedSpatialMarkId ?? "",
            LockedTargetGridX = target?.GridX ?? request.TargetGridX,
            LockedTargetGridY = target?.GridY ?? request.TargetGridY,
            LockedTargetGridHeight = target?.GridHeight ?? request.TargetGridHeight,
            AcceptedAtSeconds = runtimeTimeSeconds,
            AcceptedOrderSequence = state.NextAbilityOrderSequence++
        };

        new BattleActorRuntime(caster).AbilityController.EnqueuePendingSkillOrder(
            stream,
            battleId,
            runtimeTick,
            runtimeTimeSeconds,
            pendingOrder);

        BattleEvent acceptedEvent = new()
        {
            EventId = $"{battleId}:tick_{runtimeTick}:{commandId}:hero_skill_command_accepted",
            BattleId = battleId ?? "",
            BattleGroupId = request.BattleGroupId ?? "",
            ActorId = caster.ActorId ?? "",
            TargetId = target?.ActorId ?? "",
            SourceCommandId = commandId,
            SourceDefinitionId = ResolveSkillDefinitionId(skill),
            Kind = BattleEventKind.CommandAccepted,
            ReasonCode = ResolveSkillDefinitionId(skill),
            RuntimeTick = runtimeTick,
            RuntimeTimeSeconds = runtimeTimeSeconds,
            HasTargetCells = target != null || request.HasTargetGrid,
            TargetGridX = target?.GridX ?? request.TargetGridX,
            TargetGridY = target?.GridY ?? request.TargetGridY,
            TargetGridHeight = target?.GridHeight ?? request.TargetGridHeight
        };
        BattleEventPresentationFields.CopyFromSkill(acceptedEvent, skill);
        stream.Add(acceptedEvent);
        return BuildResult(stream, startIndex, accepted: true, ResolveSkillDefinitionId(skill));
    }

    private static string ValidateSubmission(
        BattleRuntimeState state,
        BattleEventStream stream,
        string battleId,
        int runtimeTick,
        double runtimeTimeSeconds,
        CommandRequest request,
        BattleNavigationGraph navigationGraph,
        out string commandId,
        out BattleSkillSnapshot skill,
        out BattleRuntimeActor caster,
        out BattleRuntimeActor target)
    {
        commandId = string.IsNullOrWhiteSpace(request?.CommandId)
            ? $"hero_skill:{request?.BattleGroupId ?? ""}:{runtimeTick}"
            : request.CommandId.Trim();
        skill = null;
        caster = null;
        target = null;

        if (state?.Actors == null || stream == null)
        {
            return "runtime_state_missing";
        }

        if (request == null)
        {
            return "command_missing";
        }

        if (!string.IsNullOrWhiteSpace(request.BattleId) &&
            !string.Equals(request.BattleId, battleId, System.StringComparison.Ordinal))
        {
            return "battle_id_mismatch";
        }

        if (request.Channel != CommandChannel.Hero || request.Kind != CommandKind.CastSkill)
        {
            return "runtime_command_unsupported";
        }

        if (string.IsNullOrWhiteSpace(request.BattleGroupId))
        {
            return "battle_group_missing";
        }

        if (string.IsNullOrWhiteSpace(request.SkillDefinitionId))
        {
            // Skill identity is authored command payload, not a runtime default.
            // Missing ids must surface the caller bug instead of casting a fallback skill.
            return SkillDefinitionIdRequiredReason;
        }

        caster = ResolveCaster(state, request.BattleGroupId, request.SourceActorId);
        if (caster == null)
        {
            return string.IsNullOrWhiteSpace(request.SourceActorId)
                ? "hero_actor_unavailable"
                : "skill_caster_invalid";
        }

        skill = ResolveSkillForCaster(state, caster, request.SkillDefinitionId);
        if (skill == null)
        {
            return "skill_definition_missing";
        }

        if (!IsSkillAllowedForCasterGroup(state, caster, skill))
        {
            return "skill_caster_not_allowed";
        }

        if (skill.TargetingMode == BattleSkillTargetingMode.TargetedActor &&
            string.IsNullOrWhiteSpace(request.TargetActorId))
        {
            return "skill_target_required";
        }

        if (skill.TargetingMode == BattleSkillTargetingMode.TargetedCell &&
            !request.HasTargetGrid)
        {
            return "skill_target_cell_required";
        }

        if (skill.TargetingMode == BattleSkillTargetingMode.TargetedActorOrCell &&
            string.IsNullOrWhiteSpace(request.TargetActorId) &&
            !request.HasTargetGrid)
        {
            return "skill_target_required";
        }

        if (!string.IsNullOrWhiteSpace(request.TargetActorId))
        {
            target = ResolveSubmittedTarget(state, caster, request.TargetActorId);
        }

        if (skill.TargetingMode == BattleSkillTargetingMode.TargetedActor && target == null)
        {
            return "skill_target_invalid";
        }

        if (skill.TargetingMode == BattleSkillTargetingMode.TargetedActorOrCell &&
            !string.IsNullOrWhiteSpace(request.TargetActorId) &&
            target == null)
        {
            return "skill_target_invalid";
        }

        if ((skill.TargetingMode == BattleSkillTargetingMode.TargetedActor ||
             skill.TargetingMode == BattleSkillTargetingMode.TargetedActorOrCell) &&
            target != null &&
            !IsTargetInRange(caster, target, skill.Range))
        {
            return "skill_target_out_of_range";
        }

        if (skill.TargetingMode == BattleSkillTargetingMode.TargetedActorOrCell &&
            target == null &&
            request.HasTargetGrid &&
            !IsTargetCellInRange(caster, request.TargetGridX, request.TargetGridY, request.TargetGridHeight, skill.Range))
        {
            return "skill_target_out_of_range";
        }

        if (UsesMarkTeleport(skill) &&
            string.IsNullOrWhiteSpace(request.SelectedSpatialMarkId))
        {
            return "thunder_mark_selection_required";
        }

        if (UsesMarkTeleport(skill) &&
            !ValidateMarkTeleportDestination(
                state,
                caster,
                skill,
                request.SelectedSpatialMarkId,
                request.TargetGridX,
                request.TargetGridY,
                request.TargetGridHeight,
                runtimeTimeSeconds,
                navigationGraph,
                out string teleportReason))
        {
            return teleportReason;
        }

        if (!state.SkillAvailability.CanSubmit(request.BattleGroupId, skill, out string availabilityReason))
        {
            return availabilityReason;
        }

        string skillDefinitionId = ResolveSkillDefinitionId(skill);
        if (IsSkillPendingOrActiveForBattleGroup(state, request.BattleGroupId, caster, skillDefinitionId))
        {
            return "hero_skill_already_queued";
        }

        return "";
    }

    private static bool IsSkillAllowedForCasterGroup(
        BattleRuntimeState state,
        BattleRuntimeActor caster,
        BattleSkillSnapshot skill)
    {
        string ownerHeroId = skill?.OwnerHeroId?.Trim() ?? "";
        if (!string.IsNullOrWhiteSpace(ownerHeroId))
        {
            return !string.IsNullOrWhiteSpace(caster?.SourceHeroId) &&
                string.Equals(caster.SourceHeroId, ownerHeroId, System.StringComparison.Ordinal);
        }

        string owner = !string.IsNullOrWhiteSpace(skill?.OwnerBattleGroupId)
            ? skill.OwnerBattleGroupId.Trim()
            : skill?.RuntimeCommanderGroupId?.Trim() ?? "";
        bool hasGrantOrLoadoutFacts =
            !string.IsNullOrWhiteSpace(skill?.GrantedSkillId) ||
            !string.IsNullOrWhiteSpace(skill?.LoadoutSlotId);
        if (!string.IsNullOrWhiteSpace(owner) || hasGrantOrLoadoutFacts)
        {
            string casterGroupId = caster?.BattleGroupId ?? "";
            return !string.IsNullOrWhiteSpace(casterGroupId) &&
                (string.IsNullOrWhiteSpace(owner) ||
                 string.Equals(owner, casterGroupId, System.StringComparison.Ordinal));

        }

        return BattleSkillLegacyOwnershipPolicy.IsAllowedForCasterGroup(state, caster, skill);
    }

    private static BattleSkillSnapshot ResolveSkillForCaster(
        BattleRuntimeState state,
        BattleRuntimeActor caster,
        string skillDefinitionId)
    {
        string normalized = NormalizeSkillDefinitionId(skillDefinitionId);
        BattleSkillSnapshot[] matches = (state?.SkillDefinitions ?? Enumerable.Empty<BattleSkillSnapshot>())
            .Where(item => string.Equals(ResolveSkillDefinitionId(item), normalized, System.StringComparison.Ordinal))
            .ToArray();
        if (matches.Length == 0)
        {
            return null;
        }

        string sourceHeroId = caster?.SourceHeroId?.Trim() ?? "";
        if (!string.IsNullOrWhiteSpace(sourceHeroId))
        {
            BattleSkillSnapshot heroMatch = matches.FirstOrDefault(item =>
                string.Equals(item?.OwnerHeroId?.Trim() ?? "", sourceHeroId, System.StringComparison.Ordinal));
            if (heroMatch != null)
            {
                return heroMatch;
            }
        }

        string battleGroupId = caster?.BattleGroupId?.Trim() ?? "";
        BattleSkillSnapshot groupMatch = matches.FirstOrDefault(item => SkillMatchesBattleGroup(item, battleGroupId));
        if (groupMatch != null)
        {
            return groupMatch;
        }

        BattleSkillSnapshot legacyMatch = matches.FirstOrDefault(item =>
            BattleSkillLegacyOwnershipPolicy.IsAllowedForCasterGroup(state, caster, item));
        return legacyMatch ?? matches.FirstOrDefault();
    }

    private static bool SkillMatchesBattleGroup(BattleSkillSnapshot skill, string battleGroupId)
    {
        if (string.IsNullOrWhiteSpace(battleGroupId))
        {
            return false;
        }

        return string.Equals(skill?.OwnerBattleGroupId?.Trim() ?? "", battleGroupId, System.StringComparison.Ordinal) ||
            string.Equals(skill?.RuntimeCommanderGroupId?.Trim() ?? "", battleGroupId, System.StringComparison.Ordinal);
    }

    private static string ResolveSkillDefinitionId(BattleSkillSnapshot skill)
    {
        return skill?.SkillDefinitionId?.Trim() ?? "";
    }

    private static BattleRuntimeActor ResolveHero(BattleRuntimeState state, string battleGroupId)
    {
        return state?.Actors?
            .Where(actor =>
                actor.Kind == BattleRuntimeActorKind.Hero &&
                actor.HitPoints > 0 &&
                string.Equals(actor.BattleGroupId, battleGroupId, System.StringComparison.Ordinal))
            .OrderBy(actor => actor.ActorId, System.StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static BattleRuntimeActor ResolveCaster(
        BattleRuntimeState state,
        string battleGroupId,
        string sourceActorId)
    {
        if (state?.Actors == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(sourceActorId))
        {
            string normalizedSourceActorId = sourceActorId.Trim();
            return state.Actors.FirstOrDefault(actor =>
                actor.HitPoints > 0 &&
                string.Equals(actor.ActorId, normalizedSourceActorId, System.StringComparison.Ordinal) &&
                string.Equals(actor.BattleGroupId, battleGroupId ?? "", System.StringComparison.Ordinal) &&
                (actor.Kind == BattleRuntimeActorKind.Hero || actor.Kind == BattleRuntimeActorKind.Corps));
        }

        // Older callers only identify the command group. Keep that compatibility
        // path, but pause-targeted commands should provide a visible caster id.
        return ResolveHero(state, battleGroupId);
    }

    private static BattleRuntimeActor ResolveSubmittedTarget(
        BattleRuntimeState state,
        BattleRuntimeActor hero,
        string requestedTargetActorId)
    {
        if (state?.Actors == null || hero == null || string.IsNullOrWhiteSpace(requestedTargetActorId))
        {
            return null;
        }

        return state.Actors.FirstOrDefault(actor =>
            string.Equals(actor.ActorId, requestedTargetActorId, System.StringComparison.Ordinal) &&
            IsValidLiveTarget(hero, actor));
    }

    private static bool IsValidLiveTarget(BattleRuntimeActor hero, BattleRuntimeActor target)
    {
        return hero != null &&
               target != null &&
               target.HitPoints > 0 &&
               target.Kind == BattleRuntimeActorKind.Corps &&
               !BattleRuntimeIdentityRules.SameFaction(hero, target);
    }

    private static bool IsTargetInRange(BattleRuntimeActor hero, BattleRuntimeActor target, int range)
    {
        int normalizedRange = System.Math.Max(0, range);
        // Targeted skills use a diamond preview and acceptance range; basic attacks keep their stricter orthogonal slot rules.
        return BattleActorFootprint.GetManhattanGap(
            hero,
            new BattleGridCoord(hero.GridX, hero.GridY, hero.GridHeight),
            target,
            new BattleGridCoord(target.GridX, target.GridY, target.GridHeight)) <= normalizedRange;
    }

    private static bool IsTargetCellInRange(BattleRuntimeActor hero, int x, int y, int height, int range)
    {
        if (hero == null || height != hero.GridHeight)
        {
            return false;
        }

        int normalizedRange = System.Math.Max(0, range);
        int width = BattleActorFootprint.NormalizeSize(hero.FootprintWidth);
        int heightSize = BattleActorFootprint.NormalizeSize(hero.FootprintHeight);
        int dx = x < hero.GridX ? hero.GridX - x : x >= hero.GridX + width ? x - (hero.GridX + width - 1) : 0;
        int dy = y < hero.GridY ? hero.GridY - y : y >= hero.GridY + heightSize ? y - (hero.GridY + heightSize - 1) : 0;
        return dx + dy <= normalizedRange;
    }

    private static void ResolveSkillRangeDiagnostic(
        BattleRuntimeActor caster,
        BattleRuntimeActor target,
        BattleSkillSnapshot skill,
        out int range,
        out int gap)
    {
        range = skill == null ? -1 : System.Math.Max(0, skill.Range);
        gap = caster == null || target == null
            ? -1
            : BattleActorFootprint.GetManhattanGap(
                caster,
                new BattleGridCoord(caster.GridX, caster.GridY, caster.GridHeight),
                target,
                new BattleGridCoord(target.GridX, target.GridY, target.GridHeight));
    }

    private static void AddCommandRejected(
        BattleEventStream stream,
        string battleId,
        int runtimeTick,
        double runtimeTimeSeconds,
        CommandRequest request,
        string commandId,
        string skillDefinitionId,
        BattleRuntimeActor caster,
        string targetActorId,
        BattleRuntimeActor target,
        string reasonCode,
        int range = -1,
        int gap = -1)
    {
        GameLog.Info(
            nameof(BattleRuntimeHeroSkillCommandResolver),
            $"BattleRuntimeHeroSkillCommandRejected battle={battleId ?? ""} tick={runtimeTick} source={request?.SourceActorId ?? ""} caster={caster?.ActorId ?? ""} casterCell={FormatCell(caster)} target={targetActorId ?? ""} targetCell={FormatCell(target)} skill={skillDefinitionId ?? ""} range={range} gap={gap} reason={reasonCode ?? ""}");
        stream?.Add(new BattleEvent
        {
            EventId = $"{battleId}:tick_{runtimeTick}:{commandId}:hero_skill_command_rejected",
            BattleId = battleId ?? "",
            BattleGroupId = request?.BattleGroupId ?? "",
            ActorId = caster?.ActorId ?? request?.SourceActorId ?? "",
            TargetId = targetActorId ?? "",
            SourceCommandId = commandId ?? "",
            SourceDefinitionId = skillDefinitionId ?? "",
            Kind = BattleEventKind.CommandRejected,
            ReasonCode = reasonCode ?? "",
            RuntimeTick = runtimeTick,
            RuntimeTimeSeconds = runtimeTimeSeconds,
            HasActorCells = caster != null,
            ActorGridX = caster?.GridX ?? 0,
            ActorGridY = caster?.GridY ?? 0,
            ActorGridHeight = caster?.GridHeight ?? 0,
            HasTargetCells = target != null,
            TargetGridX = target?.GridX ?? 0,
            TargetGridY = target?.GridY ?? 0,
            TargetGridHeight = target?.GridHeight ?? 0
        });
    }

    private static string FormatCell(BattleRuntimeActor actor)
    {
        return actor == null ? "-" : $"{actor.GridX},{actor.GridY},{actor.GridHeight}";
    }

    private static BattleRuntimeCommandSubmitResult BuildResult(
        BattleEventStream stream,
        int startIndex,
        bool accepted,
        string reasonCode)
    {
        return new BattleRuntimeCommandSubmitResult
        {
            Accepted = accepted,
            ReasonCode = reasonCode ?? "",
            Events = stream?.Events
                .Skip(System.Math.Max(0, startIndex))
                .ToArray() ?? System.Array.Empty<BattleEvent>()
        };
    }

    private static bool IsSkillPendingOrActiveForBattleGroup(
        BattleRuntimeState state,
        string battleGroupId,
        BattleRuntimeActor caster,
        string skillDefinitionId)
    {
        if (state?.Actors == null || string.IsNullOrWhiteSpace(battleGroupId))
        {
            return false;
        }

        string normalizedSkillDefinitionId = NormalizeSkillDefinitionId(skillDefinitionId);
        string casterId = caster?.ActorId ?? "";
        foreach (BattleRuntimeActor actor in state.Actors)
        {
            if (!string.Equals(actor?.BattleGroupId ?? "", battleGroupId ?? "", System.StringComparison.Ordinal))
            {
                continue;
            }

            if (BattleAbilityController.HasActiveSkillAction(actor) &&
                string.Equals(NormalizeSkillDefinitionId(actor.CurrentSkillDefinitionId), normalizedSkillDefinitionId, System.StringComparison.Ordinal))
            {
                return true;
            }

            // Idle pending orders are command intent and may be superseded by a
            // newer submission for the same caster; once the caster is busy,
            // same-skill orders already queued behind it must not duplicate.
            if (BattleAbilityController.HasActiveSkillAction(actor) &&
                string.Equals(actor.ActorId ?? "", casterId, System.StringComparison.Ordinal) &&
                actor.PendingAbilityOrders.Any(command =>
                    string.Equals(NormalizeSkillDefinitionId(command?.SkillDefinitionId), normalizedSkillDefinitionId, System.StringComparison.Ordinal)))
            {
                return true;
            }

            if (actor.ActiveChannels.Any(channel =>
                    string.Equals(NormalizeSkillDefinitionId(channel?.SourceDefinitionId), normalizedSkillDefinitionId, System.StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

}
