using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Commands;
using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle.Effects;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle;

internal static class BattleRuntimeHeroSkillCommandResolver
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
        CommandRequest request)
    {
        int startIndex = stream?.Events.Count ?? 0;
        string reason = ValidateSubmission(
            state,
            stream,
            battleId,
            runtimeTick,
            request,
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

        string skillKey = BuildSkillKey(request.BattleGroupId, skill.SkillId);
        state.PendingHeroSkillCommands.Add(new BattleRuntimePendingHeroSkillCommand
        {
            CommandId = commandId,
            BattleGroupId = request.BattleGroupId ?? "",
            SourceActorId = caster.ActorId ?? "",
            SkillId = skill.SkillId,
            TargetActorId = target.ActorId ?? "",
            LockedTargetGridX = target.GridX,
            LockedTargetGridY = target.GridY,
            LockedTargetGridHeight = target.GridHeight,
            AcceptedAtSeconds = runtimeTimeSeconds
        });

        stream.Add(new BattleEvent
        {
            EventId = $"{battleId}:tick_{runtimeTick}:{commandId}:hero_skill_command_accepted",
            BattleId = battleId ?? "",
            BattleGroupId = request.BattleGroupId ?? "",
            ActorId = caster.ActorId ?? "",
            TargetId = target.ActorId ?? "",
            SourceCommandId = commandId,
            SourceDefinitionId = skill.SkillId,
            Kind = BattleEventKind.CommandAccepted,
            ReasonCode = skill.SkillId,
            RuntimeTick = runtimeTick,
            RuntimeTimeSeconds = runtimeTimeSeconds,
            HasTargetCells = true,
            TargetGridX = target.GridX,
            TargetGridY = target.GridY,
            TargetGridHeight = target.GridHeight
        });
        return BuildResult(stream, startIndex, accepted: true, skill.SkillId);
    }

    private static string ValidateSubmission(
        BattleRuntimeState state,
        BattleEventStream stream,
        string battleId,
        int runtimeTick,
        CommandRequest request,
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

        if (skill.TargetingMode == BattleSkillTargetingMode.TargetedActor &&
            string.IsNullOrWhiteSpace(request.TargetActorId))
        {
            return "skill_target_required";
        }

        target = ResolveSubmittedTarget(state, caster, request.TargetActorId);
        if (skill.TargetingMode == BattleSkillTargetingMode.TargetedActor && target == null)
        {
            return "skill_target_invalid";
        }

        if (target != null && !IsTargetInRange(caster, target, skill.Range))
        {
            return "skill_target_out_of_range";
        }

        string requestedSkillKey = BuildSkillKey(request.BattleGroupId, skill.SkillId);
        if (state.UsedHeroSkillKeys.Contains(requestedSkillKey))
        {
            return "hero_skill_already_used";
        }

        if (state.PendingHeroSkillCommands.Any(command =>
                string.Equals(BuildSkillKey(command.BattleGroupId, command.SkillId), requestedSkillKey, System.StringComparison.Ordinal)))
        {
            return "hero_skill_already_pending";
        }

        return "";
    }

    internal static void ResolvePending(
        BattleRuntimeState state,
        BattleEventStream stream,
        string battleId,
        int runtimeTick,
        double runtimeTimeSeconds)
    {
        if (state?.Actors == null || stream == null)
        {
            return;
        }

        AdvanceActiveSkillActions(state, stream, battleId, runtimeTick, runtimeTimeSeconds);
        if (state.PendingHeroSkillCommands.Count == 0)
        {
            return;
        }

        BattleRuntimePendingHeroSkillCommand[] pending = state.PendingHeroSkillCommands.ToArray();
        foreach (BattleRuntimePendingHeroSkillCommand command in pending)
        {
            PendingSkillResolution resolution = ResolveOne(state, stream, battleId, runtimeTick, runtimeTimeSeconds, command);
            if (resolution != PendingSkillResolution.Waiting)
            {
                state.PendingHeroSkillCommands.Remove(command);
            }
        }
    }

    private static void AdvanceActiveSkillActions(
        BattleRuntimeState state,
        BattleEventStream stream,
        string battleId,
        int runtimeTick,
        double runtimeTimeSeconds)
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
                if (skill == null || target == null || !IsValidLiveTarget(actor, target))
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

                ApplySkillEffects(state, stream, battleId, runtimeTick, runtimeTimeSeconds, actor, target, skill, actor.CurrentSkillSourceCommandId, actor.CurrentSkillActionId);
                actor.CurrentSkillImpactApplied = true;
                double recoverySeconds = System.Math.Max(0, skill.RecoverySeconds);
                if (recoverySeconds > 0)
                {
                    BattleRuntimeActorStateMachine.MarkSkillRecovery(actor, runtimeTimeSeconds, recoverySeconds);
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
        BattleRuntimePendingHeroSkillCommand command)
    {
        BattleSkillSnapshot skill = ResolveSkill(state, command?.SkillId);
        BattleRuntimeActor hero = ResolveCaster(state, command?.BattleGroupId, command?.SourceActorId);
        BattleRuntimeActor target = ResolveActorById(state, command?.TargetActorId);
        if (skill == null || hero == null)
        {
            AddCommandFailed(stream, battleId, runtimeTick, runtimeTimeSeconds, hero, command?.TargetActorId ?? "", command?.CommandId ?? "", command?.SkillId ?? "", "skill_caster_invalid_before_release");
            return PendingSkillResolution.Completed;
        }

        if (target == null || !IsValidLiveTarget(hero, target))
        {
            AddCommandFailed(stream, battleId, runtimeTick, runtimeTimeSeconds, hero, command?.TargetActorId ?? "", command?.CommandId ?? "", skill.SkillId, "skill_target_invalid_before_release");
            return PendingSkillResolution.Completed;
        }

        if (!CanStartSkillNow(hero, skill, runtimeTimeSeconds, stream, battleId, runtimeTick, command))
        {
            return PendingSkillResolution.Waiting;
        }

        StartSkillAction(state, stream, battleId, runtimeTick, runtimeTimeSeconds, hero, target, skill, command);
        return PendingSkillResolution.Completed;
    }

    private static bool CanStartSkillNow(
        BattleRuntimeActor hero,
        BattleSkillSnapshot skill,
        double runtimeTimeSeconds,
        BattleEventStream stream,
        string battleId,
        int runtimeTick,
        BattleRuntimePendingHeroSkillCommand command)
    {
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
            return false;
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

    private static void StartSkillAction(
        BattleRuntimeState state,
        BattleEventStream stream,
        string battleId,
        int runtimeTick,
        double runtimeTimeSeconds,
        BattleRuntimeActor hero,
        BattleRuntimeActor target,
        BattleSkillSnapshot skill,
        BattleRuntimePendingHeroSkillCommand command)
    {
        string actionId = $"{command.CommandId}:action:{skill.SkillId}";
        double impactDelaySeconds = System.Math.Max(0, skill.CastSeconds + skill.ImpactDelaySeconds);
        BattleRuntimeActorStateMachine.MarkSkillCasting(
            hero,
            actionId,
            skill.SkillId,
            command.CommandId,
            target.ActorId,
            runtimeTimeSeconds,
            impactDelaySeconds,
            skill.RecoverySeconds);

        stream.Add(new BattleEvent
        {
            EventId = $"{battleId}:tick_{runtimeTick}:{hero.ActorId}:skill:{target.ActorId}",
            BattleId = battleId ?? "",
            BattleGroupId = hero.BattleGroupId ?? "",
            ActorId = hero.ActorId ?? "",
            TargetId = target.ActorId ?? "",
            SourceCommandId = command.CommandId ?? "",
            SourceActionId = actionId,
            SourceDefinitionId = skill.SkillId ?? "",
            Kind = BattleEventKind.SkillUsed,
            ReasonCode = skill.SkillId ?? "",
            RuntimeTick = runtimeTick,
            RuntimeTimeSeconds = runtimeTimeSeconds,
            HasActorCells = true,
            ActorGridX = hero.GridX,
            ActorGridY = hero.GridY,
            ActorGridHeight = hero.GridHeight,
            HasTargetCells = true,
            TargetGridX = target.GridX,
            TargetGridY = target.GridY,
            TargetGridHeight = target.GridHeight
        });

        if (impactDelaySeconds <= 0.0001)
        {
            ApplySkillEffects(state, stream, battleId, runtimeTick, runtimeTimeSeconds, hero, target, skill, command.CommandId, actionId);
            hero.CurrentSkillImpactApplied = true;
            if (skill.RecoverySeconds > 0)
            {
                BattleRuntimeActorStateMachine.MarkSkillRecovery(hero, runtimeTimeSeconds, skill.RecoverySeconds);
            }
            else
            {
                BattleRuntimeActorStateMachine.MarkAnchoredDecision(hero);
            }
        }
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
        string sourceActionId)
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
                             Actor = hero,
                             Target = target
                         },
                         new BattleEffectPayload
                         {
                             EffectKind = effect.Kind,
                             Amount = effect.Amount
                         }))
            {
                stream.Add(effectEvent);
            }
        }

        if (target.HitPoints <= 0)
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
