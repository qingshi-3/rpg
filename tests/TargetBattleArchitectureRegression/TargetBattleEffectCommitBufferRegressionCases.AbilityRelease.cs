using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;

internal static partial class TargetBattleEffectCommitBufferRegressionCases
{
    internal static void RuntimeActiveImpactAndPendingReleaseEffectIdsStayUnique()
    {
        const string delayedSkillId = "effect_commit_delayed_damage";
        const string instantSkillId = "effect_commit_pending_instant_damage";
        BattleStartSnapshot snapshot = BuildOpposedSnapshot(
            "battle_effect_commit_active_pending_ids",
            enemyStrength: 100,
            enemyCellX: 6,
            enemyCellY: 0);
        AddTimedDamageSkill(snapshot, delayedSkillId, damage: 1, castSeconds: 0.1, recoverySeconds: 0);
        AddTimedDamageSkill(snapshot, instantSkillId, damage: 1, castSeconds: 0, recoverySeconds: 0);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        FreezeAutonomousCorps(controller);

        BattleRuntimeCommandSubmitResult delayed = SubmitTargetedSkill(
            controller,
            "battle_effect_commit_active_pending_ids",
            "cmd_effect_commit_delayed_damage",
            EnemyActorId,
            delayedSkillId);
        AssertTrue(delayed.Accepted, $"delayed damage skill should be accepted reason={delayed.ReasonCode}");

        BattleRuntimeAdvanceResult start = controller.AdvanceFixedTick(0.1);
        AssertTrue(
            start.Events.Any(item =>
                item.Kind == BattleEventKind.SkillUsed &&
                item.SourceCommandId == "cmd_effect_commit_delayed_damage"),
            "first tick should start the delayed active skill");

        BattleRuntimeCommandSubmitResult pending = SubmitTargetedSkill(
            controller,
            "battle_effect_commit_active_pending_ids",
            "cmd_effect_commit_pending_instant_damage",
            EnemyActorId,
            instantSkillId);
        AssertTrue(pending.Accepted, $"pending instant skill should queue behind active skill reason={pending.ReasonCode}");

        BattleRuntimeAdvanceResult impactAndPending = controller.AdvanceFixedTick(0.2);
        BattleEvent[] effectEvents = impactAndPending.Events
            .Where(item =>
                item.Kind == BattleEventKind.EffectApplied &&
                item.TargetId == EnemyActorId &&
                (item.SourceCommandId == "cmd_effect_commit_delayed_damage" ||
                 item.SourceCommandId == "cmd_effect_commit_pending_instant_damage"))
            .ToArray();
        BattleEvent[] damageEvents = impactAndPending.Events
            .Where(item =>
                item.Kind == BattleEventKind.DamageApplied &&
                item.TargetId == EnemyActorId &&
                (item.SourceCommandId == "cmd_effect_commit_delayed_damage" ||
                 item.SourceCommandId == "cmd_effect_commit_pending_instant_damage"))
            .ToArray();

        AssertEqual(2, effectEvents.Length, $"active impact and pending release should both emit EffectApplied events={DescribeEvents(impactAndPending.Events)}");
        AssertEqual(2, damageEvents.Length, $"active impact and pending release should both emit DamageApplied events={DescribeEvents(impactAndPending.Events)}");
        AssertEqual(
            effectEvents.Length,
            effectEvents.Select(item => item.EventId).Distinct(StringComparer.Ordinal).Count(),
            $"active impact and pending release EffectApplied event ids should stay unique events={string.Join("|", effectEvents.Select(item => item.EventId))}");
        AssertEqual(
            damageEvents.Length,
            damageEvents.Select(item => item.EventId).Distinct(StringComparer.Ordinal).Count(),
            $"active impact and pending release DamageApplied event ids should stay unique events={string.Join("|", damageEvents.Select(item => item.EventId))}");
    }

    private static void AddTimedDamageSkill(
        BattleStartSnapshot snapshot,
        string skillId,
        int damage,
        double castSeconds,
        double recoverySeconds)
    {
        snapshot.SkillDefinitions.Add(new BattleSkillSnapshot
        {
            SkillId = skillId,
            DisplayName = "Effect Commit Timed Damage",
            TargetingMode = BattleSkillTargetingMode.TargetedActor,
            Range = 8,
            CastSeconds = castSeconds,
            ImpactDelaySeconds = 0,
            RecoverySeconds = recoverySeconds,
            CanInterruptBasicAttackWindup = true,
            CanCancelBasicAttackRecovery = false,
            Effects =
            {
                new BattleSkillEffectSnapshot
                {
                    Kind = BattleSkillEffectKind.Damage,
                    Amount = damage
                }
            }
        });
    }
}
