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
    internal static string NormalizeSkillId(string skillId)
    {
        return string.IsNullOrWhiteSpace(skillId)
            ? HeroSkillCommandIds.FirstSliceHeroSkillId
            : skillId.Trim();
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
                NormalizeSkillId(request?.SkillId),
                caster,
                request?.TargetActorId ?? "",
                target,
                reason,
                rejectedRange,
                rejectedGap);
            return BuildResult(stream, startIndex, accepted: false, reason);
        }

        if (!IsActiveSkillLocked(caster))
        {
            SupersedeIdleCasterPendingCommands(
                state,
                stream,
                battleId,
                runtimeTick,
                runtimeTimeSeconds,
                caster,
                commandId);
        }

        state.PendingHeroSkillCommands.Add(new BattleRuntimePendingHeroSkillCommand
        {
            CommandId = commandId,
            BattleGroupId = request.BattleGroupId ?? "",
            SourceActorId = caster.ActorId ?? "",
            SkillId = skill.SkillId,
            TargetActorId = target?.ActorId ?? "",
            HasTargetGrid = request.HasTargetGrid,
            TargetGridX = request.TargetGridX,
            TargetGridY = request.TargetGridY,
            TargetGridHeight = request.TargetGridHeight,
            SelectedSpatialMarkId = request.SelectedSpatialMarkId ?? "",
            LockedTargetGridX = target?.GridX ?? request.TargetGridX,
            LockedTargetGridY = target?.GridY ?? request.TargetGridY,
            LockedTargetGridHeight = target?.GridHeight ?? request.TargetGridHeight,
            AcceptedAtSeconds = runtimeTimeSeconds
        });

        stream.Add(new BattleEvent
        {
            EventId = $"{battleId}:tick_{runtimeTick}:{commandId}:hero_skill_command_accepted",
            BattleId = battleId ?? "",
            BattleGroupId = request.BattleGroupId ?? "",
            ActorId = caster.ActorId ?? "",
            TargetId = target?.ActorId ?? "",
            SourceCommandId = commandId,
            SourceDefinitionId = skill.SkillId,
            Kind = BattleEventKind.CommandAccepted,
            ReasonCode = skill.SkillId,
            RuntimeTick = runtimeTick,
            RuntimeTimeSeconds = runtimeTimeSeconds,
            HasTargetCells = target != null || request.HasTargetGrid,
            TargetGridX = target?.GridX ?? request.TargetGridX,
            TargetGridY = target?.GridY ?? request.TargetGridY,
            TargetGridHeight = target?.GridHeight ?? request.TargetGridHeight
        });
        return BuildResult(stream, startIndex, accepted: true, skill.SkillId);
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

        caster = ResolveCaster(state, request.BattleGroupId, request.SourceActorId);
        if (caster == null)
        {
            return string.IsNullOrWhiteSpace(request.SourceActorId)
                ? "hero_actor_unavailable"
                : "skill_caster_invalid";
        }

        skill = ResolveSkill(state, request.SkillId);
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

        if (UsesThunderMarkTeleport(skill) &&
            string.IsNullOrWhiteSpace(request.SelectedSpatialMarkId))
        {
            return "thunder_mark_selection_required";
        }

        if (UsesThunderMarkTeleport(skill) &&
            !ValidateThunderMarkTeleportDestination(
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

        string requestedSkillKey = BuildSkillKey(request.BattleGroupId, skill.SkillId);
        if (state.UsedHeroSkillKeys.Contains(requestedSkillKey))
        {
            return "hero_skill_already_used";
        }

        return "";
    }

    private static bool IsActiveSkillLocked(BattleRuntimeActor caster)
    {
        return caster?.Phase is BattleRuntimeActorPhase.SkillCasting or BattleRuntimeActorPhase.SkillRecovery;
    }

    private static void SupersedeIdleCasterPendingCommands(
        BattleRuntimeState state,
        BattleEventStream stream,
        string battleId,
        int runtimeTick,
        double runtimeTimeSeconds,
        BattleRuntimeActor caster,
        string replacingCommandId)
    {
        if (state?.PendingHeroSkillCommands == null || caster == null)
        {
            return;
        }

        foreach (BattleRuntimePendingHeroSkillCommand pending in state.PendingHeroSkillCommands
                     .Where(command => string.Equals(command.SourceActorId ?? "", caster.ActorId ?? "", System.StringComparison.Ordinal))
                     .ToArray())
        {
            state.PendingHeroSkillCommands.Remove(pending);
            stream?.Add(new BattleEvent
            {
                EventId = $"{battleId}:tick_{runtimeTick}:{pending.CommandId}:superseded_by:{replacingCommandId}",
                BattleId = battleId ?? "",
                BattleGroupId = pending.BattleGroupId ?? "",
                ActorId = caster.ActorId ?? "",
                TargetId = pending.TargetActorId ?? "",
                SourceCommandId = pending.CommandId ?? "",
                SourceDefinitionId = pending.SkillId ?? "",
                SourceActionId = replacingCommandId ?? "",
                Kind = BattleEventKind.CommandInterrupted,
                ReasonCode = "skill_intent_superseded",
                RuntimeTick = runtimeTick,
                RuntimeTimeSeconds = runtimeTimeSeconds,
                HasTargetCells = pending.HasTargetGrid,
                TargetGridX = pending.TargetGridX,
                TargetGridY = pending.TargetGridY,
                TargetGridHeight = pending.TargetGridHeight
            });
        }
    }

    private static bool IsSkillAllowedForCasterGroup(
        BattleRuntimeState state,
        BattleRuntimeActor caster,
        BattleSkillSnapshot skill)
    {
        HashSet<string> casterUnitIds = (skill?.CasterUnitIds ?? new List<string>())
            .Where(unitId => !string.IsNullOrWhiteSpace(unitId))
            .Select(unitId => unitId.Trim())
            .ToHashSet(System.StringComparer.Ordinal);
        if (casterUnitIds.Count == 0)
        {
            return true;
        }

        if (state?.Actors == null || caster == null)
        {
            return false;
        }

        // Hero-company skills bind to authored unit ids, while existing runtime
        // commands may originate from either the hidden hero proxy or visible corps.
        return state.Actors.Any(actor =>
            actor.HitPoints > 0 &&
            string.Equals(actor.BattleGroupId, caster.BattleGroupId ?? "", System.StringComparison.Ordinal) &&
            casterUnitIds.Contains(actor.UnitDefinitionId ?? ""));
    }

    internal static HashSet<string> ResolvePending(
        BattleRuntimeState state,
        BattleEventStream stream,
        string battleId,
        int runtimeTick,
        double runtimeTimeSeconds,
        BattleNavigationGraph navigationGraph,
        IReadOnlySet<string> actionBlockedActorIds = null,
        ISet<string> waitingActionActorIds = null)
    {
        var startedActionActorIds = new HashSet<string>(System.StringComparer.Ordinal);
        if (state?.Actors == null || stream == null)
        {
            return startedActionActorIds;
        }

        AdvanceActiveSkillActions(state, stream, battleId, runtimeTick, runtimeTimeSeconds, navigationGraph);
        foreach (BattleEvent channelEvent in BattleEffectResolver.AdvanceActiveChannels(
                     state,
                     battleId,
                     runtimeTick,
                     runtimeTimeSeconds))
        {
            stream.Add(channelEvent);
        }

        if (state.PendingHeroSkillCommands.Count == 0)
        {
            return startedActionActorIds;
        }

        BattleRuntimePendingHeroSkillCommand[] pending = state.PendingHeroSkillCommands.ToArray();
        foreach (BattleRuntimePendingHeroSkillCommand command in pending)
        {
            PendingSkillResolution resolution = ResolveOne(
                state,
                stream,
                battleId,
                runtimeTick,
                runtimeTimeSeconds,
                command,
                navigationGraph,
                actionBlockedActorIds,
                waitingActionActorIds,
                out string startedActorId);
            if (!string.IsNullOrWhiteSpace(startedActorId))
            {
                startedActionActorIds.Add(startedActorId);
            }

            if (resolution != PendingSkillResolution.Waiting)
            {
                state.PendingHeroSkillCommands.Remove(command);
            }
        }

        return startedActionActorIds;
    }

    private static void AdvanceActiveSkillActions(
        BattleRuntimeState state,
        BattleEventStream stream,
        string battleId,
        int runtimeTick,
        double runtimeTimeSeconds,
        BattleNavigationGraph navigationGraph)
    {
        // Skill actions are actor execution state; visible corps casters must
        // recover through the same clock as hidden hero-proxy compatibility casts.
        foreach (BattleRuntimeActor actor in state.Actors
                     .Where(item =>
                         item.HitPoints > 0 &&
                         (item.Phase == BattleRuntimeActorPhase.SkillCasting ||
                          item.Phase == BattleRuntimeActorPhase.SkillRecovery))
                     .OrderBy(item => item.ActorId, System.StringComparer.Ordinal))
        {
            if (actor.Phase == BattleRuntimeActorPhase.SkillCasting &&
                !actor.CurrentSkillImpactApplied &&
                actor.CurrentSkillImpactAtSeconds <= runtimeTimeSeconds + 0.0001)
            {
                BattleSkillSnapshot skill = ResolveSkill(state, actor.CurrentSkillId);
                BattleRuntimeActor target = ResolveActorById(state, actor.CurrentSkillTargetActorId);
                if (skill == null ||
                    SkillRequiresLiveActorAtRelease(skill, actor.CurrentSkillTargetActorId) &&
                    (target == null || !IsValidLiveTarget(actor, target)))
                {
                    AddCommandFailed(
                        stream,
                        battleId,
                        runtimeTick,
                        runtimeTimeSeconds,
                        actor,
                        actor.CurrentSkillTargetActorId,
                        actor.CurrentSkillSourceCommandId,
                        actor.CurrentSkillId,
                        "skill_target_invalid_before_impact");
                    BattleRuntimeActorStateMachine.MarkAnchoredDecision(actor);
                    continue;
                }

                ApplySkillEffects(state, stream, battleId, runtimeTick, runtimeTimeSeconds, actor, target, skill, actor.CurrentSkillSourceCommandId, actor.CurrentSkillActionId, navigationGraph);
                actor.CurrentSkillImpactApplied = true;
                double postImpactLockSeconds = ResolvePostImpactSkillLockSeconds(state, actor, skill, runtimeTimeSeconds);
                if (postImpactLockSeconds > 0)
                {
                    BattleRuntimeActorStateMachine.MarkSkillRecovery(actor, runtimeTimeSeconds, postImpactLockSeconds);
                }
                else
                {
                    BattleRuntimeActorStateMachine.MarkAnchoredDecision(actor);
                }
            }
            else if (actor.Phase == BattleRuntimeActorPhase.SkillRecovery &&
                     actor.ActionReadyAtSeconds <= runtimeTimeSeconds + 0.0001)
            {
                BattleRuntimeActorStateMachine.MarkAnchoredDecision(actor);
            }
        }
    }

    private static PendingSkillResolution ResolveOne(
        BattleRuntimeState state,
        BattleEventStream stream,
        string battleId,
        int runtimeTick,
        double runtimeTimeSeconds,
        BattleRuntimePendingHeroSkillCommand command,
        BattleNavigationGraph navigationGraph,
        IReadOnlySet<string> actionBlockedActorIds,
        ISet<string> waitingActionActorIds,
        out string startedActorId)
    {
        startedActorId = "";
        BattleSkillSnapshot skill = ResolveSkill(state, command?.SkillId);
        BattleRuntimeActor hero = ResolveCaster(state, command?.BattleGroupId, command?.SourceActorId);
        BattleRuntimeActor target = ResolveActorById(state, command?.TargetActorId);
        if (skill == null || hero == null)
        {
            AddCommandFailed(stream, battleId, runtimeTick, runtimeTimeSeconds, hero, command?.TargetActorId ?? "", command?.CommandId ?? "", command?.SkillId ?? "", "skill_caster_invalid_before_release");
            return PendingSkillResolution.Completed;
        }

        if (SkillRequiresLiveActorAtRelease(skill, command?.TargetActorId) &&
            (target == null || !IsValidLiveTarget(hero, target)))
        {
            AddCommandFailed(stream, battleId, runtimeTick, runtimeTimeSeconds, hero, command?.TargetActorId ?? "", command?.CommandId ?? "", skill.SkillId, "skill_target_invalid_before_release");
            return PendingSkillResolution.Completed;
        }

        if (!CanStartSkillNow(
                state,
                hero,
                skill,
                runtimeTimeSeconds,
                stream,
                battleId,
                runtimeTick,
                command,
                actionBlockedActorIds,
                waitingActionActorIds))
        {
            return PendingSkillResolution.Waiting;
        }

        bool consumedActorAction = StartSkillAction(state, stream, battleId, runtimeTick, runtimeTimeSeconds, hero, target, skill, command, navigationGraph);
        if (consumedActorAction)
        {
            startedActorId = hero.ActorId ?? "";
        }

        return PendingSkillResolution.Completed;
    }

    private static bool CanStartSkillNow(
        BattleRuntimeState state,
        BattleRuntimeActor hero,
        BattleSkillSnapshot skill,
        double runtimeTimeSeconds,
        BattleEventStream stream,
        string battleId,
        int runtimeTick,
        BattleRuntimePendingHeroSkillCommand command,
        IReadOnlySet<string> actionBlockedActorIds,
        ISet<string> waitingActionActorIds)
    {
        if (ReleasesImmediatelyWithoutOccupyingCaster(state, hero, skill, runtimeTimeSeconds) &&
            hero.Phase is BattleRuntimeActorPhase.Moving or BattleRuntimeActorPhase.AnchoredDecision)
        {
            return true;
        }

        if (actionBlockedActorIds?.Contains(hero?.ActorId ?? "") == true)
        {
            // Movement completion is a runtime action boundary. A queued skill may
            // consume the next slice, but it must not release on the same tick that
            // visually finishes a move or the unit appears to cast while drifting.
            waitingActionActorIds?.Add(hero.ActorId ?? "");
            return false;
        }

        if (hero.Phase == BattleRuntimeActorPhase.AttackWindup)
        {
            if (!skill.CanInterruptBasicAttackWindup)
            {
                return false;
            }

            stream.Add(new BattleEvent
            {
                EventId = $"{battleId}:tick_{runtimeTick}:{hero.ActorId}:{command.CommandId}:attack_windup_interrupted",
                BattleId = battleId ?? "",
                BattleGroupId = hero.BattleGroupId ?? "",
                ActorId = hero.ActorId ?? "",
                TargetId = command.TargetActorId ?? "",
                SourceCommandId = command.CommandId ?? "",
                SourceDefinitionId = skill.SkillId ?? "",
                Kind = BattleEventKind.CommandInterrupted,
                ReasonCode = "basic_attack_windup_interrupted",
                RuntimeTick = runtimeTick,
                RuntimeTimeSeconds = runtimeTimeSeconds
            });
            return true;
        }

        if (hero.Phase == BattleRuntimeActorPhase.AttackRecovery)
        {
            if (skill.CanCancelBasicAttackRecovery)
            {
                return true;
            }

            if (hero.ActionReadyAtSeconds > runtimeTimeSeconds + 0.0001)
            {
                return false;
            }

            BattleRuntimeActorStateMachine.MarkAnchoredDecision(hero);
            return true;
        }

        if (hero.Phase == BattleRuntimeActorPhase.SkillCasting ||
            hero.Phase == BattleRuntimeActorPhase.SkillRecovery)
        {
            return CanInterruptActiveChannelWithSkill(state, hero, skill, runtimeTimeSeconds);
        }

        if (hero.Phase != BattleRuntimeActorPhase.AnchoredDecision &&
            hero.ActionReadyAtSeconds > runtimeTimeSeconds + 0.0001)
        {
            return false;
        }

        if (hero.Phase != BattleRuntimeActorPhase.AnchoredDecision)
        {
            BattleRuntimeActorStateMachine.MarkAnchoredDecision(hero);
        }

        return true;
    }

    private static bool StartSkillAction(
        BattleRuntimeState state,
        BattleEventStream stream,
        string battleId,
        int runtimeTick,
        double runtimeTimeSeconds,
        BattleRuntimeActor hero,
        BattleRuntimeActor target,
        BattleSkillSnapshot skill,
        BattleRuntimePendingHeroSkillCommand command,
        BattleNavigationGraph navigationGraph)
    {
        string actionId = $"{command.CommandId}:action:{skill.SkillId}";
        double impactDelaySeconds = System.Math.Max(0, skill.CastSeconds + skill.ImpactDelaySeconds);
        double postImpactLockSeconds = ResolvePostImpactSkillLockSeconds(state, hero, skill, runtimeTimeSeconds);
        if (ReleasesImmediatelyWithoutOccupyingCaster(state, hero, skill, runtimeTimeSeconds))
        {
            AddSkillUsed(stream, battleId, runtimeTick, runtimeTimeSeconds, hero, target, skill, command, actionId);
            ApplySkillEffects(state, stream, battleId, runtimeTick, runtimeTimeSeconds, hero, target, skill, command.CommandId, actionId, navigationGraph, command);
            return false;
        }

        BattleRuntimeActorStateMachine.MarkSkillCasting(
            hero,
            actionId,
            skill.SkillId,
            command.CommandId,
            target?.ActorId ?? "",
            runtimeTimeSeconds,
            impactDelaySeconds,
            postImpactLockSeconds);

        AddSkillUsed(stream, battleId, runtimeTick, runtimeTimeSeconds, hero, target, skill, command, actionId);

        if (impactDelaySeconds <= 0.0001)
        {
            ApplySkillEffects(state, stream, battleId, runtimeTick, runtimeTimeSeconds, hero, target, skill, command.CommandId, actionId, navigationGraph, command);
            hero.CurrentSkillImpactApplied = true;
            postImpactLockSeconds = ResolvePostImpactSkillLockSeconds(state, hero, skill, runtimeTimeSeconds);
            if (postImpactLockSeconds > 0)
            {
                BattleRuntimeActorStateMachine.MarkSkillRecovery(hero, runtimeTimeSeconds, postImpactLockSeconds);
            }
            else
            {
                BattleRuntimeActorStateMachine.MarkAnchoredDecision(hero);
            }
        }

        return true;
    }

    private static void ApplySkillEffects(
        BattleRuntimeState state,
        BattleEventStream stream,
        string battleId,
        int runtimeTick,
        double runtimeTimeSeconds,
        BattleRuntimeActor hero,
        BattleRuntimeActor target,
        BattleSkillSnapshot skill,
        string sourceCommandId,
        string sourceActionId,
        BattleNavigationGraph navigationGraph,
        BattleRuntimePendingHeroSkillCommand command = null)
    {
        foreach (BattleSkillEffectSnapshot effect in skill.Effects)
        {
            foreach (BattleEvent effectEvent in BattleEffectResolver.Apply(
                         new BattleEffectExecutionContext
                         {
                             BattleId = battleId ?? "",
                             RuntimeTick = runtimeTick,
                             RuntimeTimeSeconds = runtimeTimeSeconds,
                             SourceCommandId = sourceCommandId ?? "",
                             SourceActionId = sourceActionId ?? "",
                             SourceDefinitionId = skill.SkillId ?? "",
                             State = state,
                             NavigationGraph = navigationGraph,
                             Actor = hero,
                             Target = target ?? new BattleRuntimeActor(),
                             HasTargetGrid = command?.HasTargetGrid ?? false,
                             TargetGridX = command?.TargetGridX ?? 0,
                             TargetGridY = command?.TargetGridY ?? 0,
                             TargetGridHeight = command?.TargetGridHeight ?? 0,
                             SelectedSpatialMarkId = command?.SelectedSpatialMarkId ?? ""
                         },
                         new BattleEffectPayload
                         {
                             EffectKind = effect.Kind,
                             Amount = effect.Amount,
                             DurationSeconds = effect.DurationSeconds,
                             TickIntervalSeconds = effect.TickIntervalSeconds,
                             Radius = effect.Radius
                         }))
            {
                stream.Add(effectEvent);
            }
        }

        if (target != null && target.HitPoints <= 0)
        {
            BattleRuntimeActorStateMachine.MarkDefeated(target);
        }

        state.UsedHeroSkillKeys.Add(BuildSkillKey(hero.BattleGroupId, skill.SkillId));
    }

    private static BattleSkillSnapshot ResolveSkill(BattleRuntimeState state, string skillId)
    {
        string normalized = NormalizeSkillId(skillId);
        return state?.SkillDefinitions?
            .FirstOrDefault(item => string.Equals(item?.SkillId, normalized, System.StringComparison.Ordinal));
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

    private static BattleRuntimeActor ResolveActorById(BattleRuntimeState state, string actorId)
    {
        if (state?.Actors == null || string.IsNullOrWhiteSpace(actorId))
        {
            return null;
        }

        return state.Actors.FirstOrDefault(actor =>
            string.Equals(actor.ActorId, actorId, System.StringComparison.Ordinal));
    }

    private static bool IsValidLiveTarget(BattleRuntimeActor hero, BattleRuntimeActor target)
    {
        return hero != null &&
               target != null &&
               target.HitPoints > 0 &&
               target.Kind == BattleRuntimeActorKind.Corps &&
               !BattleRuntimeTickResolver.SameFaction(hero, target);
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

    private static bool ReleasesImmediatelyWithoutOccupyingCaster(
        BattleRuntimeState state,
        BattleRuntimeActor actor,
        BattleSkillSnapshot skill,
        double runtimeTimeSeconds)
    {
        if (skill?.ReleasesWithoutOccupyingCaster != true)
        {
            return false;
        }

        // Current offhand support is for immediate projectile-like releases.
        // Timed casts, recovery locks, and channels still need actor action state
        // until Runtime has a separate delayed projectile/action scheduler.
        double impactDelaySeconds = System.Math.Max(0, skill.CastSeconds + skill.ImpactDelaySeconds);
        double lockSeconds = ResolvePostImpactSkillLockSeconds(state, actor, skill, runtimeTimeSeconds);
        return impactDelaySeconds <= 0.0001 && lockSeconds <= 0.0001;
    }

    private static bool CanInterruptActiveChannelWithSkill(
        BattleRuntimeState state,
        BattleRuntimeActor actor,
        BattleSkillSnapshot skill,
        double runtimeTimeSeconds) =>
        UsesThunderMarkTeleport(skill) &&
        ResolveActiveChannelRemainingSeconds(state, actor, runtimeTimeSeconds) > 0.0001;

    private static double ResolvePostImpactSkillLockSeconds(
        BattleRuntimeState state,
        BattleRuntimeActor actor,
        BattleSkillSnapshot skill,
        double runtimeTimeSeconds)
    {
        // Channeled hero skills are runtime action locks, not presentation-only effects:
        // ordinary movement must stay blocked while the active damage window follows the caster.
        double lockSeconds = System.Math.Max(0, skill?.RecoverySeconds ?? 0);
        foreach (BattleSkillEffectSnapshot effect in skill?.Effects ?? Enumerable.Empty<BattleSkillEffectSnapshot>())
        {
            if (effect?.Kind == BattleSkillEffectKind.StartChanneledAreaDamage)
            {
                lockSeconds = System.Math.Max(lockSeconds, System.Math.Max(0, effect.DurationSeconds));
            }
        }

        return System.Math.Max(lockSeconds, ResolveActiveChannelRemainingSeconds(state, actor, runtimeTimeSeconds));
    }

    private static double ResolveActiveChannelRemainingSeconds(
        BattleRuntimeState state,
        BattleRuntimeActor actor,
        double runtimeTimeSeconds)
    {
        if (state?.ActiveChannels == null || actor == null || string.IsNullOrWhiteSpace(actor.ActorId))
        {
            return 0;
        }

        return state.ActiveChannels
            .Where(channel =>
                channel != null &&
                string.Equals(channel.ActorId ?? "", actor.ActorId ?? "", System.StringComparison.Ordinal))
            .Select(channel => channel.EndsAtSeconds - runtimeTimeSeconds)
            .DefaultIfEmpty(0)
            .Max();
    }

    private static bool SkillRequiresLiveActorAtRelease(BattleSkillSnapshot skill, string targetActorId) =>
        skill?.TargetingMode == BattleSkillTargetingMode.TargetedActor ||
        skill?.TargetingMode == BattleSkillTargetingMode.TargetedActorOrCell &&
        !string.IsNullOrWhiteSpace(targetActorId);

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
        string skillId,
        BattleRuntimeActor caster,
        string targetActorId,
        BattleRuntimeActor target,
        string reasonCode,
        int range = -1,
        int gap = -1)
    {
        GameLog.Info(
            nameof(BattleRuntimeHeroSkillCommandResolver),
            $"BattleRuntimeHeroSkillCommandRejected battle={battleId ?? ""} tick={runtimeTick} source={request?.SourceActorId ?? ""} caster={caster?.ActorId ?? ""} casterCell={FormatCell(caster)} target={targetActorId ?? ""} targetCell={FormatCell(target)} skill={skillId ?? ""} range={range} gap={gap} reason={reasonCode ?? ""}");
        stream?.Add(new BattleEvent
        {
            EventId = $"{battleId}:tick_{runtimeTick}:{commandId}:hero_skill_command_rejected",
            BattleId = battleId ?? "",
            BattleGroupId = request?.BattleGroupId ?? "",
            ActorId = caster?.ActorId ?? request?.SourceActorId ?? "",
            TargetId = targetActorId ?? "",
            SourceCommandId = commandId ?? "",
            SourceDefinitionId = skillId ?? "",
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

    private static void AddCommandFailed(
        BattleEventStream stream,
        string battleId,
        int runtimeTick,
        double runtimeTimeSeconds,
        BattleRuntimeActor hero,
        string targetActorId,
        string sourceCommandId,
        string skillId,
        string reasonCode)
    {
        stream?.Add(new BattleEvent
        {
            EventId = $"{battleId}:tick_{runtimeTick}:{sourceCommandId}:hero_skill_command_failed",
            BattleId = battleId ?? "",
            BattleGroupId = hero?.BattleGroupId ?? "",
            ActorId = hero?.ActorId ?? "",
            TargetId = targetActorId ?? "",
            SourceCommandId = sourceCommandId ?? "",
            SourceDefinitionId = skillId ?? "",
            Kind = BattleEventKind.CommandFailed,
            ReasonCode = reasonCode ?? "",
            RuntimeTick = runtimeTick,
            RuntimeTimeSeconds = runtimeTimeSeconds
        });
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

    private static string BuildSkillKey(string battleGroupId, string skillId)
    {
        return $"{battleGroupId ?? ""}:{NormalizeSkillId(skillId)}";
    }

    private enum PendingSkillResolution
    {
        Waiting,
        Completed
    }
}
