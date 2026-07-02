using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle.Effects;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle;

internal sealed class BattleAbilityController
{
    private readonly BattleRuntimeActor _actor;

    internal BattleAbilityController(BattleRuntimeActor actor)
    {
        _actor = actor ?? throw new ArgumentNullException(nameof(actor));
    }

    internal static bool HasActiveSkillAction(BattleRuntimeActor actor) =>
        actor?.Phase is BattleRuntimeActorPhase.SkillCasting or BattleRuntimeActorPhase.SkillRecovery;

    internal void EnqueuePendingSkillOrder(
        BattleEventStream stream,
        string battleId,
        int runtimeTick,
        double runtimeTimeSeconds,
        BattleRuntimePendingHeroSkillCommand command)
    {
        if (command == null)
        {
            return;
        }

        if (!HasActiveSkillAction(_actor))
        {
            SupersedeLocalPendingOrders(stream, battleId, runtimeTick, runtimeTimeSeconds, command.CommandId);
        }

        _actor.PendingAbilityOrders.Add(command);
    }

    internal static HashSet<string> ResolvePendingSkillOrders(
        BattleRuntimeState state,
        BattleEventStream stream,
        string battleId,
        int runtimeTick,
        double runtimeTimeSeconds,
        BattleNavigationGraph navigationGraph,
        IReadOnlySet<string> actionBlockedActorIds,
        ISet<string> waitingActionActorIds,
        BattleCommitBuffer channelStartCommitBuffer = null)
    {
        var startedActionActorIds = new HashSet<string>(StringComparer.Ordinal);
        if (state?.Actors == null || stream == null)
        {
            return startedActionActorIds;
        }

        var pending = state.Actors
            .Where(actor => actor?.PendingAbilityOrders.Count > 0)
            .SelectMany(actor => actor.PendingAbilityOrders.Select(command => new PendingAbilityOrderEntry(actor, command)))
            .OrderBy(entry => entry.Command.AcceptedOrderSequence)
            .ToArray();
        var consumedActionActorIds = new HashSet<string>(StringComparer.Ordinal);
        BattleCommitBuffer abilityCommitBuffer = channelStartCommitBuffer ?? new BattleCommitBuffer();
        foreach (PendingAbilityOrderEntry entry in pending)
        {
            if (!entry.Actor.PendingAbilityOrders.Contains(entry.Command))
            {
                continue;
            }

            string actorId = entry.Actor.ActorId ?? "";
            if (consumedActionActorIds.Contains(actorId))
            {
                continue;
            }

            PendingSkillResolution resolution = new BattleActorRuntime(entry.Actor).AbilityController.ResolvePendingSkillOrder(
                state,
                stream,
                battleId,
                runtimeTick,
                runtimeTimeSeconds,
                entry.Command,
                navigationGraph,
                actionBlockedActorIds,
                waitingActionActorIds,
                abilityCommitBuffer,
                out string startedActorId);
            if (!string.IsNullOrWhiteSpace(startedActorId))
            {
                startedActionActorIds.Add(startedActorId);
            }

            if (resolution == PendingSkillResolution.StartedAction)
            {
                if (!string.IsNullOrWhiteSpace(actorId))
                {
                    consumedActionActorIds.Add(actorId);
                }

                entry.Actor.PendingAbilityOrders.Remove(entry.Command);
            }
            else if (resolution == PendingSkillResolution.Completed)
            {
                entry.Actor.PendingAbilityOrders.Remove(entry.Command);
            }
        }

        foreach (BattleEvent channelStartEvent in abilityCommitBuffer.CommitEffectDeliveries())
        {
            stream.Add(channelStartEvent);
        }

        return startedActionActorIds;
    }

    internal static bool HasActiveChannels(BattleRuntimeActor actor) =>
        actor?.ActiveChannels.Count > 0;

    internal IReadOnlyList<BattleEvent> AdvanceActiveChannels(
        BattleRuntimeState state,
        string battleId,
        int runtimeTick,
        double runtimeTimeSeconds,
        BattleCommitBuffer channelTickCommitBuffer)
    {
        if (_actor.ActiveChannels.Count == 0)
        {
            return Array.Empty<BattleEvent>();
        }

        List<BattleEvent> events = new();
        List<BattleRuntimeActiveChannel> remove = new();
        foreach (BattleRuntimeActiveChannel channel in _actor.ActiveChannels)
        {
            if (_actor.HitPoints <= 0 || runtimeTimeSeconds >= channel.EndsAtSeconds + 0.0001)
            {
                remove.Add(channel);
                continue;
            }

            if (channel.NextTickAtSeconds <= runtimeTimeSeconds + 0.0001)
            {
                events.AddRange(BattleChannelDamageResolver.ApplyDamageTick(
                    state,
                    battleId ?? "",
                    runtimeTick,
                    runtimeTimeSeconds,
                    _actor,
                    channel,
                    channelTickCommitBuffer,
                    deferEffectDamageCommit: true));
                channel.NextTickAtSeconds += Math.Max(0.001, channel.TickIntervalSeconds);
            }
        }

        foreach (BattleRuntimeActiveChannel channel in remove)
        {
            _actor.ActiveChannels.Remove(channel);
        }

        return events;
    }

    internal void AdvanceActiveSkillAction(
        BattleRuntimeState state,
        BattleEventStream stream,
        string battleId,
        int runtimeTick,
        double runtimeTimeSeconds,
        BattleNavigationGraph navigationGraph,
        BattleCommitBuffer channelStartCommitBuffer = null)
    {
        if (_actor.HitPoints <= 0)
        {
            return;
        }

        if (_actor.Phase == BattleRuntimeActorPhase.SkillCasting &&
            !_actor.CurrentSkillImpactApplied &&
            _actor.CurrentSkillImpactAtSeconds <= runtimeTimeSeconds + 0.0001)
        {
            BattleRuntimePendingHeroSkillCommand lockedCommand = BuildCurrentSkillCommand();
            BattleSkillSnapshot skill = ResolveSkill(state, lockedCommand);
            BattleRuntimeActor target = ResolveActorById(state, _actor.CurrentSkillTargetActorId);
            if (skill == null ||
                SkillRequiresLiveActorAtRelease(skill, _actor.CurrentSkillTargetActorId) &&
                (target == null || !IsValidLiveTarget(_actor, target)))
            {
                AddCommandFailed(
                    stream,
                    battleId,
                    runtimeTick,
                    runtimeTimeSeconds,
                    _actor,
                    _actor.CurrentSkillTargetActorId,
                    _actor.CurrentSkillSourceCommandId,
                    _actor.CurrentSkillDefinitionId,
                    "skill_target_invalid_before_impact");
                BattleRuntimeActorStateMachine.MarkAnchoredDecision(_actor);
                return;
            }

            BattleAbilityEffectReleaseBoundary.ReleaseSkillEffects(state, stream, battleId, runtimeTick, runtimeTimeSeconds, _actor, target, skill, _actor.CurrentSkillSourceCommandId, _actor.CurrentSkillActionId, navigationGraph, lockedCommand, channelStartCommitBuffer);
            _actor.CurrentSkillImpactApplied = true;
            double postImpactLockSeconds = ResolvePostImpactSkillLockSeconds(state, _actor, skill, runtimeTimeSeconds);
            if (postImpactLockSeconds > 0)
            {
                BattleRuntimeActorStateMachine.MarkSkillRecovery(_actor, runtimeTimeSeconds, postImpactLockSeconds);
            }
            else
            {
                BattleRuntimeActorStateMachine.MarkAnchoredDecision(_actor);
            }
        }
        else if (_actor.Phase == BattleRuntimeActorPhase.SkillRecovery &&
                 _actor.ActionReadyAtSeconds <= runtimeTimeSeconds + 0.0001)
        {
            BattleRuntimeActorStateMachine.MarkAnchoredDecision(_actor);
        }
    }

    internal bool CanStartSkillNow(
        BattleRuntimeState state,
        BattleSkillSnapshot skill,
        double runtimeTimeSeconds,
        BattleEventStream stream,
        string battleId,
        int runtimeTick,
        BattleRuntimePendingHeroSkillCommand command,
        IReadOnlySet<string> actionBlockedActorIds,
        ISet<string> waitingActionActorIds)
    {
        if (ReleasesImmediatelyWithoutOccupyingCaster(state, skill, runtimeTimeSeconds) &&
            _actor.Phase is BattleRuntimeActorPhase.Moving or BattleRuntimeActorPhase.AnchoredDecision)
        {
            return true;
        }

        if (actionBlockedActorIds?.Contains(_actor.ActorId ?? "") == true)
        {
            // Movement completion is an action boundary; queued skills wait for
            // the next slice instead of releasing while the unit is still drifting.
            waitingActionActorIds?.Add(_actor.ActorId ?? "");
            return false;
        }

        if (_actor.Phase == BattleRuntimeActorPhase.AttackWindup)
        {
            if (_actor.CurrentBasicAttackImpactAtSeconds <= runtimeTimeSeconds + 0.0001)
            {
                return false;
            }

            if (!skill.CanInterruptBasicAttackWindup)
            {
                return false;
            }

            BattleEvent interruptedEvent = new()
            {
                EventId = $"{battleId}:tick_{runtimeTick}:{_actor.ActorId}:{command.CommandId}:attack_windup_interrupted",
                BattleId = battleId ?? "",
                BattleGroupId = _actor.BattleGroupId ?? "",
                ActorId = _actor.ActorId ?? "",
                TargetId = command.TargetActorId ?? "",
                SourceCommandId = command.CommandId ?? "",
                SourceDefinitionId = skill.SkillDefinitionId ?? "",
                Kind = BattleEventKind.CommandInterrupted,
                ReasonCode = "basic_attack_windup_interrupted",
                RuntimeTick = runtimeTick,
                RuntimeTimeSeconds = runtimeTimeSeconds
            };
            BattleEventPresentationFields.CopyFromSkill(interruptedEvent, skill);
            stream.Add(interruptedEvent);
            return true;
        }

        if (_actor.Phase == BattleRuntimeActorPhase.AttackRecovery)
        {
            if (skill.CanCancelBasicAttackRecovery)
            {
                return true;
            }

            if (_actor.ActionReadyAtSeconds > runtimeTimeSeconds + 0.0001)
            {
                return false;
            }

            BattleRuntimeActorStateMachine.MarkAnchoredDecision(_actor);
            return true;
        }

        if (_actor.Phase == BattleRuntimeActorPhase.SkillCasting ||
            _actor.Phase == BattleRuntimeActorPhase.SkillRecovery)
        {
            return CanInterruptActiveChannelWithSkill(state, skill, runtimeTimeSeconds);
        }

        if (_actor.Phase != BattleRuntimeActorPhase.AnchoredDecision &&
            _actor.ActionReadyAtSeconds > runtimeTimeSeconds + 0.0001)
        {
            return false;
        }

        if (_actor.Phase != BattleRuntimeActorPhase.AnchoredDecision)
        {
            BattleRuntimeActorStateMachine.MarkAnchoredDecision(_actor);
        }

        return true;
    }

    private void SupersedeLocalPendingOrders(
        BattleEventStream stream,
        string battleId,
        int runtimeTick,
        double runtimeTimeSeconds,
        string replacingCommandId)
    {
        foreach (BattleRuntimePendingHeroSkillCommand pending in _actor.PendingAbilityOrders.ToArray())
        {
            _actor.PendingAbilityOrders.Remove(pending);
            stream?.Add(new BattleEvent
            {
                EventId = $"{battleId}:tick_{runtimeTick}:{pending.CommandId}:superseded_by:{replacingCommandId}",
                BattleId = battleId ?? "",
                BattleGroupId = pending.BattleGroupId ?? "",
                ActorId = _actor.ActorId ?? "",
                TargetId = pending.TargetActorId ?? "",
                SourceCommandId = pending.CommandId ?? "",
                SourceDefinitionId = pending.SkillDefinitionId ?? "",
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

    private PendingSkillResolution ResolvePendingSkillOrder(
        BattleRuntimeState state,
        BattleEventStream stream,
        string battleId,
        int runtimeTick,
        double runtimeTimeSeconds,
        BattleRuntimePendingHeroSkillCommand command,
        BattleNavigationGraph navigationGraph,
        IReadOnlySet<string> actionBlockedActorIds,
        ISet<string> waitingActionActorIds,
        BattleCommitBuffer channelStartCommitBuffer,
        out string startedActorId)
    {
        startedActorId = "";
        BattleRuntimeActor caster = ResolveLiveCasterForOrder(command);
        BattleSkillSnapshot skill = ResolveSkill(state, command);
        BattleRuntimeActor target = ResolveActorById(state, command?.TargetActorId);
        if (skill == null)
        {
            AddCommandFailed(stream, battleId, runtimeTick, runtimeTimeSeconds, caster, command, "skill_definition_missing_before_release");
            return PendingSkillResolution.Completed;
        }

        if (caster == null)
        {
            AddCommandFailed(stream, battleId, runtimeTick, runtimeTimeSeconds, caster, command, "skill_caster_invalid_before_release");
            return PendingSkillResolution.Completed;
        }

        if (SkillRequiresLiveActorAtRelease(skill, command?.TargetActorId) &&
            (target == null || !IsValidLiveTarget(caster, target)))
        {
            AddCommandFailed(stream, battleId, runtimeTick, runtimeTimeSeconds, caster, command, "skill_target_invalid_before_release");
            return PendingSkillResolution.Completed;
        }

        if (!CanStartSkillNow(
                state,
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

        bool consumedActorAction = StartSkillAction(state, stream, battleId, runtimeTick, runtimeTimeSeconds, target, skill, command, navigationGraph, channelStartCommitBuffer);
        if (consumedActorAction)
        {
            startedActorId = caster.ActorId ?? "";
            return PendingSkillResolution.StartedAction;
        }

        return PendingSkillResolution.Completed;
    }

    internal bool StartSkillAction(
        BattleRuntimeState state,
        BattleEventStream stream,
        string battleId,
        int runtimeTick,
        double runtimeTimeSeconds,
        BattleRuntimeActor target,
        BattleSkillSnapshot skill,
        BattleRuntimePendingHeroSkillCommand command,
        BattleNavigationGraph navigationGraph,
        BattleCommitBuffer channelStartCommitBuffer = null)
    {
        string actionId = $"{command.CommandId}:action:{skill.SkillDefinitionId}";
        double impactDelaySeconds = Math.Max(0, skill.CastSeconds + skill.ImpactDelaySeconds);
        double postImpactLockSeconds = ResolvePostImpactSkillLockSeconds(state, _actor, skill, runtimeTimeSeconds);
        if (ReleasesImmediatelyWithoutOccupyingCaster(state, skill, runtimeTimeSeconds))
        {
            AddSkillUsed(stream, battleId, runtimeTick, runtimeTimeSeconds, target, skill, command, actionId);
            BattleAbilityEffectReleaseBoundary.ReleaseSkillEffects(state, stream, battleId, runtimeTick, runtimeTimeSeconds, _actor, target, skill, command.CommandId, actionId, navigationGraph, command, channelStartCommitBuffer);
            return false;
        }

        BattleRuntimeActorStateMachine.MarkSkillCasting(
            _actor,
            actionId,
            skill.SkillDefinitionId,
            skill.GrantedSkillId,
            skill.LoadoutSlotId,
            skill.OwnerHeroId,
            command.CommandId,
            target?.ActorId ?? "",
            command.HasTargetGrid,
            command.TargetGridX,
            command.TargetGridY,
            command.TargetGridHeight,
            command.SelectedSpatialMarkId,
            runtimeTimeSeconds,
            impactDelaySeconds,
            postImpactLockSeconds);

        AddSkillUsed(stream, battleId, runtimeTick, runtimeTimeSeconds, target, skill, command, actionId);

        if (impactDelaySeconds <= 0.0001)
        {
            BattleAbilityEffectReleaseBoundary.ReleaseSkillEffects(state, stream, battleId, runtimeTick, runtimeTimeSeconds, _actor, target, skill, command.CommandId, actionId, navigationGraph, command, channelStartCommitBuffer);
            _actor.CurrentSkillImpactApplied = true;
            postImpactLockSeconds = ResolvePostImpactSkillLockSeconds(state, _actor, skill, runtimeTimeSeconds);
            if (postImpactLockSeconds > 0)
            {
                BattleRuntimeActorStateMachine.MarkSkillRecovery(_actor, runtimeTimeSeconds, postImpactLockSeconds);
            }
            else
            {
                BattleRuntimeActorStateMachine.MarkAnchoredDecision(_actor);
            }
        }

        return true;
    }

    private void AddSkillUsed(
        BattleEventStream stream,
        string battleId,
        int runtimeTick,
        double runtimeTimeSeconds,
        BattleRuntimeActor target,
        BattleSkillSnapshot skill,
        BattleRuntimePendingHeroSkillCommand command,
        string actionId)
    {
        BattleEvent skillUsedEvent = new()
        {
            EventId = $"{battleId}:tick_{runtimeTick}:{_actor.ActorId}:skill:{target?.ActorId ?? "cell"}",
            BattleId = battleId ?? "",
            BattleGroupId = _actor.BattleGroupId ?? "",
            ActorId = _actor.ActorId ?? "",
            TargetId = target?.ActorId ?? "",
            SourceCommandId = command.CommandId ?? "",
            SourceActionId = actionId,
            SourceDefinitionId = skill.SkillDefinitionId ?? "",
            Kind = BattleEventKind.SkillUsed,
            ReasonCode = skill.SkillDefinitionId ?? "",
            ActionDurationSeconds = ResolveSkillUsedActionDurationSeconds(skill),
            RuntimeTick = runtimeTick,
            RuntimeTimeSeconds = runtimeTimeSeconds,
            HasActorCells = true,
            ActorGridX = _actor.GridX,
            ActorGridY = _actor.GridY,
            ActorGridHeight = _actor.GridHeight,
            HasTargetCells = target != null || command.HasTargetGrid,
            TargetGridX = target?.GridX ?? command.TargetGridX,
            TargetGridY = target?.GridY ?? command.TargetGridY,
            TargetGridHeight = target?.GridHeight ?? command.TargetGridHeight
        };
        BattleEventPresentationFields.CopyFromSkill(skillUsedEvent, skill);
        stream.Add(skillUsedEvent);
    }

    private BattleRuntimePendingHeroSkillCommand BuildCurrentSkillCommand()
    {
        return new BattleRuntimePendingHeroSkillCommand
        {
            CommandId = _actor.CurrentSkillSourceCommandId ?? "",
            BattleGroupId = _actor.BattleGroupId ?? "",
            SourceActorId = _actor.ActorId ?? "",
            SkillDefinitionId = _actor.CurrentSkillDefinitionId ?? "",
            GrantedSkillId = _actor.CurrentSkillGrantedSkillId ?? "",
            LoadoutSlotId = _actor.CurrentSkillLoadoutSlotId ?? "",
            OwnerHeroId = _actor.CurrentSkillOwnerHeroId ?? "",
            TargetActorId = _actor.CurrentSkillTargetActorId ?? "",
            HasTargetGrid = _actor.CurrentSkillHasTargetGrid,
            TargetGridX = _actor.CurrentSkillTargetGridX,
            TargetGridY = _actor.CurrentSkillTargetGridY,
            TargetGridHeight = _actor.CurrentSkillTargetGridHeight,
            SelectedSpatialMarkId = _actor.CurrentSkillSelectedSpatialMarkId ?? "",
            LockedTargetGridX = _actor.CurrentSkillTargetGridX,
            LockedTargetGridY = _actor.CurrentSkillTargetGridY,
            LockedTargetGridHeight = _actor.CurrentSkillTargetGridHeight
        };
    }

    private static double ResolveSkillUsedActionDurationSeconds(BattleSkillSnapshot skill)
    {
        if (skill?.Effects == null || skill.Effects.Count == 0)
        {
            return 0;
        }

        double durationSeconds = 0;
        foreach (BattleSkillEffectSnapshot effect in skill.Effects)
        {
            if (effect is ChanneledAreaDamageSkillEffectSnapshot channel)
            {
                durationSeconds = Math.Max(durationSeconds, Math.Max(0, channel.DurationSeconds));
            }
        }

        return durationSeconds;
    }

    private static BattleSkillSnapshot ResolveSkill(BattleRuntimeState state, BattleRuntimePendingHeroSkillCommand command)
    {
        string normalized = BattleRuntimeHeroSkillCommandResolver.NormalizeSkillDefinitionId(command?.SkillDefinitionId);
        BattleSkillSnapshot[] matches = (state?.SkillDefinitions ?? Enumerable.Empty<BattleSkillSnapshot>())
            .Where(item => string.Equals(item?.SkillDefinitionId, normalized, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length == 0)
        {
            return null;
        }

        string grantedSkillId = command?.GrantedSkillId?.Trim() ?? "";
        if (!string.IsNullOrWhiteSpace(grantedSkillId))
        {
            BattleSkillSnapshot grantMatch = matches.FirstOrDefault(item =>
                string.Equals(item?.GrantedSkillId?.Trim() ?? "", grantedSkillId, StringComparison.Ordinal));
            if (grantMatch != null)
            {
                return grantMatch;
            }
        }

        string ownerHeroId = command?.OwnerHeroId?.Trim() ?? "";
        if (!string.IsNullOrWhiteSpace(ownerHeroId))
        {
            BattleSkillSnapshot heroMatch = matches.FirstOrDefault(item =>
                string.Equals(item?.OwnerHeroId?.Trim() ?? "", ownerHeroId, StringComparison.Ordinal));
            if (heroMatch != null)
            {
                return heroMatch;
            }
        }

        string loadoutSlotId = command?.LoadoutSlotId?.Trim() ?? "";
        if (!string.IsNullOrWhiteSpace(loadoutSlotId))
        {
            BattleSkillSnapshot loadoutMatch = matches.FirstOrDefault(item =>
                string.Equals(item?.LoadoutSlotId?.Trim() ?? "", loadoutSlotId, StringComparison.Ordinal) &&
                (string.IsNullOrWhiteSpace(ownerHeroId) ||
                 string.Equals(item?.OwnerHeroId?.Trim() ?? "", ownerHeroId, StringComparison.Ordinal)));
            if (loadoutMatch != null)
            {
                return loadoutMatch;
            }
        }

        string battleGroupId = command?.BattleGroupId?.Trim() ?? "";
        return matches.FirstOrDefault(item => SkillMatchesBattleGroup(item, battleGroupId)) ??
            matches.FirstOrDefault();
    }

    private static bool SkillMatchesBattleGroup(BattleSkillSnapshot skill, string battleGroupId)
    {
        if (string.IsNullOrWhiteSpace(battleGroupId))
        {
            return false;
        }

        return string.Equals(skill?.OwnerBattleGroupId?.Trim() ?? "", battleGroupId, StringComparison.Ordinal) ||
            string.Equals(skill?.RuntimeCommanderGroupId?.Trim() ?? "", battleGroupId, StringComparison.Ordinal);
    }

    private static BattleRuntimeActor ResolveActorById(BattleRuntimeState state, string actorId)
    {
        if (state?.Actors == null || string.IsNullOrWhiteSpace(actorId))
        {
            return null;
        }

        return state.Actors.FirstOrDefault(actor =>
            string.Equals(actor.ActorId, actorId, StringComparison.Ordinal));
    }

    private BattleRuntimeActor ResolveLiveCasterForOrder(BattleRuntimePendingHeroSkillCommand command)
    {
        if (command == null ||
            _actor.HitPoints <= 0 ||
            string.IsNullOrWhiteSpace(_actor.ActorId) ||
            !string.Equals(_actor.ActorId, command.SourceActorId ?? "", StringComparison.Ordinal) ||
            !string.Equals(_actor.BattleGroupId ?? "", command.BattleGroupId ?? "", StringComparison.Ordinal) ||
            _actor.Kind is not (BattleRuntimeActorKind.Hero or BattleRuntimeActorKind.Corps))
        {
            return null;
        }

        return _actor;
    }

    private static bool IsValidLiveTarget(BattleRuntimeActor actor, BattleRuntimeActor target)
    {
        return actor != null &&
               target != null &&
               target.HitPoints > 0 &&
               target.Kind == BattleRuntimeActorKind.Corps &&
               !BattleRuntimeIdentityRules.SameFaction(actor, target);
    }

    private bool ReleasesImmediatelyWithoutOccupyingCaster(
        BattleRuntimeState state,
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
        double impactDelaySeconds = Math.Max(0, skill.CastSeconds + skill.ImpactDelaySeconds);
        double lockSeconds = ResolvePostImpactSkillLockSeconds(state, _actor, skill, runtimeTimeSeconds);
        return impactDelaySeconds <= 0.0001 && lockSeconds <= 0.0001;
    }

    private bool CanInterruptActiveChannelWithSkill(
        BattleRuntimeState state,
        BattleSkillSnapshot skill,
        double runtimeTimeSeconds) =>
        UsesMarkTeleport(skill) &&
            ResolveActiveChannelRemainingSeconds(_actor, runtimeTimeSeconds) > 0.0001;

    private static double ResolvePostImpactSkillLockSeconds(
        BattleRuntimeState state,
        BattleRuntimeActor actor,
        BattleSkillSnapshot skill,
        double runtimeTimeSeconds)
    {
        // Channeled hero skills are runtime action locks, not presentation-only effects:
        // ordinary movement must stay blocked while the active damage window follows the caster.
        double lockSeconds = Math.Max(0, skill?.RecoverySeconds ?? 0);
        foreach (BattleSkillEffectSnapshot effect in skill?.Effects ?? Enumerable.Empty<BattleSkillEffectSnapshot>())
        {
            if (effect is ChanneledAreaDamageSkillEffectSnapshot channel)
            {
                lockSeconds = Math.Max(lockSeconds, Math.Max(0, channel.DurationSeconds));
            }
        }

        return Math.Max(lockSeconds, ResolveActiveChannelRemainingSeconds(actor, runtimeTimeSeconds));
    }

    private static double ResolveActiveChannelRemainingSeconds(
        BattleRuntimeActor actor,
        double runtimeTimeSeconds)
    {
        if (actor == null || string.IsNullOrWhiteSpace(actor.ActorId))
        {
            return 0;
        }

        return actor.ActiveChannels
            .Where(channel =>
                channel != null &&
                string.Equals(channel.ActorId ?? "", actor.ActorId ?? "", StringComparison.Ordinal))
            .Select(channel => channel.EndsAtSeconds - runtimeTimeSeconds)
            .DefaultIfEmpty(0)
            .Max();
    }

    private static bool SkillRequiresLiveActorAtRelease(BattleSkillSnapshot skill, string targetActorId) =>
        skill?.TargetingMode == BattleSkillTargetingMode.TargetedActor ||
        skill?.TargetingMode == BattleSkillTargetingMode.TargetedActorOrCell &&
        !string.IsNullOrWhiteSpace(targetActorId);

    private static bool UsesMarkTeleport(BattleSkillSnapshot skill) =>
        skill?.Effects?.Any(effect => effect is TeleportToMarkSkillEffectSnapshot) == true;

    private static void AddCommandFailed(
        BattleEventStream stream,
        string battleId,
        int runtimeTick,
        double runtimeTimeSeconds,
        BattleRuntimeActor actor,
        string targetActorId,
        string sourceCommandId,
        string skillDefinitionId,
        string reasonCode)
    {
        stream?.Add(new BattleEvent
        {
            EventId = $"{battleId}:tick_{runtimeTick}:{sourceCommandId}:hero_skill_command_failed",
            BattleId = battleId ?? "",
            BattleGroupId = actor?.BattleGroupId ?? "",
            ActorId = actor?.ActorId ?? "",
            TargetId = targetActorId ?? "",
            SourceCommandId = sourceCommandId ?? "",
            SourceDefinitionId = skillDefinitionId ?? "",
            Kind = BattleEventKind.CommandFailed,
            ReasonCode = reasonCode ?? "",
            RuntimeTick = runtimeTick,
            RuntimeTimeSeconds = runtimeTimeSeconds
        });
    }

    private static void AddCommandFailed(
        BattleEventStream stream,
        string battleId,
        int runtimeTick,
        double runtimeTimeSeconds,
        BattleRuntimeActor actor,
        BattleRuntimePendingHeroSkillCommand command,
        string reasonCode)
    {
        stream?.Add(new BattleEvent
        {
            EventId = $"{battleId}:tick_{runtimeTick}:{command?.CommandId ?? ""}:hero_skill_command_failed",
            BattleId = battleId ?? "",
            BattleGroupId = actor?.BattleGroupId ?? command?.BattleGroupId ?? "",
            ActorId = actor?.ActorId ?? command?.SourceActorId ?? "",
            TargetId = command?.TargetActorId ?? "",
            SourceCommandId = command?.CommandId ?? "",
            SourceDefinitionId = command?.SkillDefinitionId ?? "",
            Kind = BattleEventKind.CommandFailed,
            ReasonCode = reasonCode ?? "",
            RuntimeTick = runtimeTick,
            RuntimeTimeSeconds = runtimeTimeSeconds
        });
    }

    private readonly record struct PendingAbilityOrderEntry(
        BattleRuntimeActor Actor,
        BattleRuntimePendingHeroSkillCommand Command);

    private enum PendingSkillResolution
    {
        Waiting,
        Completed,
        StartedAction
    }
}
