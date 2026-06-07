using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Commands;
using Rpg.Application.Battle.Reports;
using Rpg.Application.Battle.Settlement;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;

internal static class TargetBattleHeroSkillRegressionCases
{
    private const string FirstSliceSkillId = "first_slice_hero_breakthrough";
    private const string EnemyActorId = "force_enemy:1";

    internal static void Register(System.Action<string, System.Action> run)
    {
        run("targeted hero skill requires target at submission", TargetedHeroSkillRequiresTargetAtSubmission);
        run("targeted hero skill rejects out of range at submission", TargetedHeroSkillRejectsOutOfRangeAtSubmission);
        run("targeted hero skill accepts diamond range at submission", TargetedHeroSkillAcceptsDiamondRangeAtSubmission);
        run("targeted hero skill uses explicit source actor for range and release", TargetedHeroSkillUsesExplicitSourceActorForRangeAndRelease);
        run("runtime visible caster skill recovery completes", RuntimeVisibleCasterSkillRecoveryCompletes);
        run("runtime locks target at skill acceptance and ignores later range drift", RuntimeLocksTargetAtAcceptanceAndIgnoresLaterRangeDrift);
        run("runtime fails locked skill when target dies before release", RuntimeFailsLockedSkillWhenTargetDiesBeforeRelease);
        run("runtime skill waits for basic attack recovery by default", RuntimeSkillWaitsForBasicAttackRecoveryByDefault);
        run("runtime skill interrupts pre impact attack windup", RuntimeSkillInterruptsPreImpactAttackWindup);
        run("runtime skill command waits behind active skill by default", RuntimeSkillCommandWaitsBehindActiveSkillByDefault);
        run("runtime effect events carry skill source attribution", RuntimeEffectEventsCarrySkillSourceAttribution);
        run("runtime queues hero skill command and resolves it on next tick", RuntimeQueuesHeroSkillCommandAndResolvesItOnNextTick);
        run("battle report records hero skill use", BattleReportRecordsHeroSkillUse);
        run("battle report records hero skill effect attribution", BattleReportRecordsHeroSkillEffectAttribution);
        run("battle report records hero skill failure reason", BattleReportRecordsHeroSkillFailureReason);
    }

    internal static void TargetedHeroSkillRequiresTargetAtSubmission()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_hero_skill_requires_target", enemyStrength: 40);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);

        BattleRuntimeCommandSubmitResult submit = controller.SubmitCommand(new CommandRequest
        {
            CommandId = "cmd_hero_skill_requires_target",
            BattleId = "battle_hero_skill_requires_target",
            BattleGroupId = "group_player",
            Channel = CommandChannel.Hero,
            Kind = CommandKind.CastSkill,
            SkillId = FirstSliceSkillId
        });

        AssertTrue(!submit.Accepted, "targeted hero skill command without a target should be rejected");
        AssertTrue(
            submit.Events.Any(item =>
                item.Kind == BattleEventKind.CommandRejected &&
                item.SourceCommandId == "cmd_hero_skill_requires_target" &&
                item.ReasonCode == "skill_target_required"),
            "missing target rejection should enter event stream");
    }

    internal static void TargetedHeroSkillRejectsOutOfRangeAtSubmission()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot(
            "battle_hero_skill_out_of_range",
            enemyStrength: 40,
            enemyCellX: 20);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);

        BattleRuntimeCommandSubmitResult submit = SubmitTargetedSkill(
            controller,
            "battle_hero_skill_out_of_range",
            "cmd_hero_skill_out_of_range",
            EnemyActorId);

        AssertTrue(!submit.Accepted, "targeted hero skill should reject targets outside accepted range");
        AssertTrue(
            submit.Events.Any(item =>
                item.Kind == BattleEventKind.CommandRejected &&
                item.SourceCommandId == "cmd_hero_skill_out_of_range" &&
                item.TargetId == EnemyActorId &&
                item.SourceDefinitionId == FirstSliceSkillId &&
                item.ReasonCode == "skill_target_out_of_range"),
            "out-of-range rejection should keep target and definition attribution");
    }

    internal static void TargetedHeroSkillAcceptsDiamondRangeAtSubmission()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot(
            "battle_hero_skill_diamond_range",
            enemyStrength: 40,
            enemyCellX: 4,
            enemyCellY: 4);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);

        BattleRuntimeCommandSubmitResult submit = SubmitTargetedSkill(
            controller,
            "battle_hero_skill_diamond_range",
            "cmd_hero_skill_diamond_range",
            EnemyActorId);

        AssertTrue(submit.Accepted, "targeted hero skill should accept diagonal targets inside Manhattan diamond range");
        AssertTrue(
            submit.Events.Any(item =>
                item.Kind == BattleEventKind.CommandAccepted &&
                item.SourceCommandId == "cmd_hero_skill_diamond_range" &&
                item.TargetId == EnemyActorId),
            "accepted diamond-range command should enter the event stream");
    }

    internal static void TargetedHeroSkillUsesExplicitSourceActorForRangeAndRelease()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot(
            "battle_hero_skill_explicit_source",
            enemyStrength: 40,
            enemyCellX: 14);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        BattleRuntimeActor visibleCaster = PlayerCorps(controller);
        visibleCaster.GridX = 7;
        visibleCaster.Position = 7;

        BattleRuntimeCommandSubmitResult submit = controller.SubmitCommand(new CommandRequest
        {
            CommandId = "cmd_hero_skill_explicit_source",
            BattleId = "battle_hero_skill_explicit_source",
            BattleGroupId = "group_player",
            SourceActorId = "force_player:1",
            Channel = CommandChannel.Hero,
            Kind = CommandKind.CastSkill,
            SkillId = FirstSliceSkillId,
            TargetActorId = EnemyActorId
        });

        AssertTrue(submit.Accepted, "explicit selected caster in skill range should be accepted even when the hidden hero proxy is out of range");
        AssertTrue(
            submit.Events.Any(item =>
                item.Kind == BattleEventKind.CommandAccepted &&
                item.ActorId == "force_player:1" &&
                item.TargetId == EnemyActorId &&
                item.SourceCommandId == "cmd_hero_skill_explicit_source"),
            "accepted skill command should attribute the locked order to the selected visible caster");

        BattleRuntimeAdvanceResult advance = controller.AdvanceFixedTick();

        AssertTrue(
            advance.Events.Any(item =>
                item.Kind == BattleEventKind.SkillUsed &&
                item.ActorId == "force_player:1" &&
                item.TargetId == EnemyActorId &&
                item.SourceCommandId == "cmd_hero_skill_explicit_source"),
            "skill release should use the selected visible caster instead of re-resolving a hidden hero proxy");
        AssertEffectDamage(advance, "cmd_hero_skill_explicit_source", 18, "explicit caster skill damage", "force_player:1");
        AssertEqual(22, EnemyCorps(controller).HitPoints, "enemy HP after explicit caster skill damage");
    }

    internal static void RuntimeVisibleCasterSkillRecoveryCompletes()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot(
            "battle_visible_caster_skill_recovery",
            enemyStrength: 40);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        BattleRuntimeActor visibleCaster = PlayerCorps(controller);

        BattleRuntimeCommandSubmitResult submit = controller.SubmitCommand(new CommandRequest
        {
            CommandId = "cmd_visible_caster_skill_recovery",
            BattleId = "battle_visible_caster_skill_recovery",
            BattleGroupId = "group_player",
            SourceActorId = visibleCaster.ActorId,
            Channel = CommandChannel.Hero,
            Kind = CommandKind.CastSkill,
            SkillId = FirstSliceSkillId,
            TargetActorId = EnemyActorId
        });

        AssertTrue(submit.Accepted, "visible caster skill command should be accepted");
        _ = controller.AdvanceFixedTick();
        _ = controller.AdvanceFixedTick(0.2);
        _ = controller.AdvanceFixedTick(0.2);

        AssertTrue(
            visibleCaster.Phase != BattleRuntimeActorPhase.SkillCasting &&
            visibleCaster.Phase != BattleRuntimeActorPhase.SkillRecovery,
            $"visible caster must leave skill action locks after recovery: phase={visibleCaster.Phase}");
        AssertEqual("", visibleCaster.CurrentSkillId, "visible caster skill state should be cleared after recovery");
    }

    internal static void RuntimeLocksTargetAtAcceptanceAndIgnoresLaterRangeDrift()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_hero_skill_target_lock", enemyStrength: 40);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);

        BattleRuntimeCommandSubmitResult submit = SubmitTargetedSkill(
            controller,
            "battle_hero_skill_target_lock",
            "cmd_hero_skill_target_lock",
            EnemyActorId);

        AssertTrue(submit.Accepted, "in-range targeted hero skill should be accepted and locked");
        AssertTrue(
            submit.Events.All(item => item.Kind != BattleEventKind.DamageApplied),
            "submitting while paused should not immediately mutate battlefield HP");
        EnemyCorps(controller).GridX = 30;
        EnemyCorps(controller).Position = 30;

        BattleRuntimeAdvanceResult advance = controller.AdvanceFixedTick();

        AssertTrue(
            advance.Events.Any(item =>
                item.Kind == BattleEventKind.SkillUsed &&
                item.ActorId == "group_player:hero" &&
                item.TargetId == EnemyActorId &&
                item.SourceCommandId == "cmd_hero_skill_target_lock" &&
                item.SourceDefinitionId == FirstSliceSkillId),
            "locked target should still receive the skill after moving out of range");
        AssertEffectDamage(
            advance,
            "cmd_hero_skill_target_lock",
            expectedDamage: 18,
            "locked target effect damage");
        AssertEqual(22, EnemyCorps(controller).HitPoints, "enemy HP after locked target skill damage");
    }

    internal static void RuntimeFailsLockedSkillWhenTargetDiesBeforeRelease()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_hero_skill_dead_target", enemyStrength: 40);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        BattleRuntimeCommandSubmitResult submit = SubmitTargetedSkill(
            controller,
            "battle_hero_skill_dead_target",
            "cmd_hero_skill_dead_target",
            EnemyActorId);
        AssertTrue(submit.Accepted, "dead-target setup command should be accepted before target dies");
        AddBackupEnemy(controller);
        EnemyCorps(controller).HitPoints = 0;

        BattleRuntimeAdvanceResult advance = controller.AdvanceFixedTick();

        AssertTrue(
            advance.Events.Any(item =>
                item.Kind == BattleEventKind.CommandFailed &&
                item.SourceCommandId == "cmd_hero_skill_dead_target" &&
                item.TargetId == EnemyActorId &&
                item.SourceDefinitionId == FirstSliceSkillId &&
                item.ReasonCode == "skill_target_invalid_before_release"),
            "dead locked target should fail the skill before release");
        AssertTrue(
            advance.Events.All(item => item.Kind != BattleEventKind.SkillUsed && item.Kind != BattleEventKind.DamageApplied),
            "failed skill should not release or apply damage");
    }

    internal static void RuntimeSkillWaitsForBasicAttackRecoveryByDefault()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_hero_skill_waits_recovery", enemyStrength: 40);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        BattleRuntimeActor hero = Hero(controller);
        hero.Phase = BattleRuntimeActorPhase.AttackRecovery;
        hero.ActionReadyAtSeconds = 0.01;

        BattleRuntimeCommandSubmitResult submit = SubmitTargetedSkill(
            controller,
            "battle_hero_skill_waits_recovery",
            "cmd_hero_skill_waits_recovery",
            EnemyActorId);
        AssertTrue(submit.Accepted, "skill command during attack recovery should be accepted but wait");

        BattleRuntimeAdvanceResult firstAdvance = controller.AdvanceFixedTick();
        AssertEqual(40, EnemyCorps(controller).HitPoints, "skill should not cancel attack recovery by default");
        AssertTrue(
            firstAdvance.Events.All(item => item.Kind != BattleEventKind.SkillUsed && item.Kind != BattleEventKind.DamageApplied),
            "no skill release should occur while attack recovery is still locked");

        BattleRuntimeAdvanceResult secondAdvance = controller.AdvanceFixedTick();
        AssertEffectDamage(secondAdvance, "cmd_hero_skill_waits_recovery", 18, "skill damage after recovery");
        AssertEqual(22, EnemyCorps(controller).HitPoints, "skill should release after recovery boundary");
    }

    internal static void RuntimeSkillInterruptsPreImpactAttackWindup()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_hero_skill_interrupts_windup", enemyStrength: 40);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        BattleRuntimeActor hero = Hero(controller);
        hero.Phase = BattleRuntimeActorPhase.AttackWindup;
        hero.ActionReadyAtSeconds = 1.0;

        BattleRuntimeCommandSubmitResult submit = SubmitTargetedSkill(
            controller,
            "battle_hero_skill_interrupts_windup",
            "cmd_hero_skill_interrupts_windup",
            EnemyActorId);
        AssertTrue(submit.Accepted, "skill command during attack windup should be accepted");

        BattleRuntimeAdvanceResult advance = controller.AdvanceFixedTick();

        AssertEffectDamage(advance, "cmd_hero_skill_interrupts_windup", 18, "skill damage after windup interrupt");
        AssertTrue(
            advance.Events.Any(item =>
                item.Kind == BattleEventKind.CommandInterrupted &&
                item.ActorId == "group_player:hero" &&
                item.SourceCommandId == "cmd_hero_skill_interrupts_windup" &&
                item.ReasonCode == "basic_attack_windup_interrupted"),
            "skill should emit an interruption event for pre-impact basic attack windup");
    }

    internal static void RuntimeSkillCommandWaitsBehindActiveSkillByDefault()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_hero_skill_waits_active_skill", enemyStrength: 60);
        snapshot.SkillDefinitions[0].CastSeconds = 0.2;
        AddFollowUpSkill(snapshot, "followup_skill", damage: 5);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);

        BattleRuntimeCommandSubmitResult first = SubmitTargetedSkill(
            controller,
            "battle_hero_skill_waits_active_skill",
            "cmd_skill_casting_first",
            EnemyActorId);
        AssertTrue(first.Accepted, "first skill command should be accepted");
        BattleRuntimeAdvanceResult startFirst = controller.AdvanceFixedTick();
        AssertTrue(
            startFirst.Events.Any(item =>
                item.Kind == BattleEventKind.SkillUsed &&
                item.SourceCommandId == "cmd_skill_casting_first"),
            "first skill should start casting");

        BattleRuntimeCommandSubmitResult second = SubmitTargetedSkill(
            controller,
            "battle_hero_skill_waits_active_skill",
            "cmd_skill_casting_second",
            EnemyActorId,
            skillId: "followup_skill");
        AssertTrue(second.Accepted, "second skill command should be accepted as queued intent");

        BattleRuntimeAdvanceResult whileCasting = controller.AdvanceFixedTick();

        AssertEqual(60, EnemyCorps(controller).HitPoints, "active skill casting should prevent the queued second skill from releasing");
        AssertTrue(
            whileCasting.Events.All(item =>
                item.SourceCommandId != "cmd_skill_casting_second" ||
                item.Kind != BattleEventKind.SkillUsed),
            "second skill must not interrupt an active skill cast by default");

        _ = controller.AdvanceFixedTick(0.2);
        BattleRuntimeAdvanceResult afterFirstSkill = controller.AdvanceFixedTick(0.2);
        AssertTrue(
            afterFirstSkill.Events.Any(item =>
                item.Kind == BattleEventKind.DamageApplied &&
                item.SourceCommandId == "cmd_skill_casting_first"),
            "first active skill should resolve before the queued second skill");
        AssertTrue(
            afterFirstSkill.Events.All(item =>
                item.SourceCommandId != "cmd_skill_casting_second" ||
                item.Kind != BattleEventKind.DamageApplied),
            "second skill should still wait through first skill recovery");

        BattleRuntimeAdvanceResult afterRecovery = controller.AdvanceFixedTick(0.2);
        AssertTrue(
            afterRecovery.Events.Any(item =>
                item.Kind == BattleEventKind.SkillUsed &&
                item.SourceCommandId == "cmd_skill_casting_second"),
            "second skill should start only after the first skill recovery boundary");
    }

    internal static void RuntimeEffectEventsCarrySkillSourceAttribution()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_hero_skill_effect_source", enemyStrength: 40);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        BattleRuntimeCommandSubmitResult submit = SubmitTargetedSkill(
            controller,
            "battle_hero_skill_effect_source",
            "cmd_hero_skill_effect_source",
            EnemyActorId);
        AssertTrue(submit.Accepted, "source attribution setup command should be accepted");

        BattleRuntimeAdvanceResult advance = controller.AdvanceFixedTick();

        BattleEvent? effect = advance.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.EffectApplied &&
            item.ActorId == "group_player:hero" &&
            item.TargetId == EnemyActorId &&
            item.SourceCommandId == "cmd_hero_skill_effect_source");
        AssertTrue(effect != null, "skill should emit a source-agnostic effect result event");
        AssertEqual(FirstSliceSkillId, effect!.SourceDefinitionId, "effect source definition");
        AssertTrue(!string.IsNullOrWhiteSpace(effect.SourceActionId), "effect source action id");
        AssertEqual("Damage", effect.EffectKind, "effect kind");
        AssertEqual(-18, effect.CorpsStrengthDelta, "effect damage delta");
    }

    internal static void RuntimeQueuesHeroSkillCommandAndResolvesItOnNextTick()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_hero_skill", enemyStrength: 40);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);

        BattleRuntimeCommandSubmitResult submit = SubmitTargetedSkill(
            controller,
            "battle_hero_skill",
            "cmd_hero_skill_1",
            EnemyActorId);

        AssertTrue(submit.Accepted, "hero skill command should be accepted into runtime queue");
        AssertTrue(
            submit.Events.Any(item =>
                item.Kind == BattleEventKind.CommandAccepted &&
                item.SourceCommandId == "cmd_hero_skill_1" &&
                item.TargetId == EnemyActorId &&
                item.SourceDefinitionId == FirstSliceSkillId &&
                item.ReasonCode == FirstSliceSkillId),
            "accepted skill command should enter event stream with target and definition attribution");
        AssertTrue(
            submit.Events.All(item => item.Kind != BattleEventKind.DamageApplied),
            "submitting while paused should not immediately mutate battlefield HP");
        AssertEqual(40, EnemyCorps(controller).HitPoints, "enemy HP before next runtime tick");

        BattleRuntimeAdvanceResult advance = controller.AdvanceFixedTick();

        AssertTrue(
            advance.Events.Any(item =>
                item.Kind == BattleEventKind.SkillUsed &&
                item.ActorId == "group_player:hero" &&
                item.TargetId == EnemyActorId &&
                item.SourceCommandId == "cmd_hero_skill_1" &&
                item.SourceDefinitionId == FirstSliceSkillId &&
                item.ReasonCode == FirstSliceSkillId),
            "next runtime tick should emit a skill-used event from the hero actor");
        BattleEvent? damage = advance.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.DamageApplied &&
            item.ActorId == "group_player:hero" &&
            item.TargetId == EnemyActorId &&
            item.SourceCommandId == "cmd_hero_skill_1");
        AssertTrue(damage != null, "next runtime tick should apply the first-slice hero skill damage");
        AssertEqual(FirstSliceSkillId, damage!.SourceDefinitionId, "hero skill damage source definition");
        AssertEqual("effect_damage", damage!.ReasonCode, "hero skill damage reason");
        AssertEqual(-18, damage.CorpsStrengthDelta, "hero skill damage amount");
        AssertEqual(22, EnemyCorps(controller).HitPoints, "enemy HP after skill damage");
    }

    internal static void BattleReportRecordsHeroSkillUse()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_hero_skill_report", enemyStrength: 10);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        BattleRuntimeCommandSubmitResult submit = controller.SubmitCommand(new CommandRequest
        {
            CommandId = "cmd_hero_skill_report",
            BattleId = "battle_hero_skill_report",
            BattleGroupId = "group_player",
            Channel = CommandChannel.Hero,
            Kind = CommandKind.CastSkill,
            SkillId = FirstSliceSkillId,
            TargetActorId = EnemyActorId
        });
        AssertTrue(submit.Accepted, "report setup skill command should be accepted");

        BattleRuntimeAdvanceResult advance = controller.AdvanceFixedTick();
        AssertTrue(advance.IsComplete, "lethal first-slice skill should complete the tiny report battle");

        SettlementPlan settlement = new BattleSettlementService().BuildPlan(
            snapshot.SnapshotId,
            controller.Outcome,
            controller.EventStream);
        BattleReportRecord report = new BattleReportBuilder().Build(
            controller.Outcome,
            controller.EventStream,
            settlement);

        AssertTrue(settlement.Accepted, "skill report battle should be settlement-accepted");
        AssertTrue(
            report.HeroSkillUses.Any(item => item.Contains(FirstSliceSkillId, StringComparison.Ordinal)),
            "battle report should preserve hero skill use as report-visible evidence");
    }

    internal static void BattleReportRecordsHeroSkillEffectAttribution()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_hero_skill_report_effect", enemyStrength: 10);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        BattleRuntimeCommandSubmitResult submit = SubmitTargetedSkill(
            controller,
            "battle_hero_skill_report_effect",
            "cmd_hero_skill_report_effect",
            EnemyActorId);
        AssertTrue(submit.Accepted, "effect report setup skill command should be accepted");

        _ = controller.AdvanceFixedTick();
        BattleReportRecord report = BuildReport(snapshot, controller);

        AssertTrue(
            report.HeroSkillEffects.Any(item =>
                item.SourceCommandId == "cmd_hero_skill_report_effect" &&
                !string.IsNullOrWhiteSpace(item.SourceActionId) &&
                item.SourceDefinitionId == FirstSliceSkillId &&
                item.EffectKind == "Damage" &&
                item.TargetId == EnemyActorId &&
                item.CorpsStrengthDelta == -18),
            "battle report should preserve source/action/definition/effect attribution from runtime effect events");
    }

    internal static void BattleReportRecordsHeroSkillFailureReason()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_hero_skill_report_failure", enemyStrength: 40);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        BattleRuntimeCommandSubmitResult submit = SubmitTargetedSkill(
            controller,
            "battle_hero_skill_report_failure",
            "cmd_hero_skill_report_failure",
            EnemyActorId);
        AssertTrue(submit.Accepted, "failure report setup skill command should be accepted before target dies");
        AddBackupEnemy(controller);
        EnemyCorps(controller).HitPoints = 0;

        _ = controller.AdvanceFixedTick();
        BattleReportRecord report = BuildReport(snapshot, controller);

        AssertTrue(
            report.HeroSkillFailures.Any(item =>
                item.SourceCommandId == "cmd_hero_skill_report_failure" &&
                item.SourceDefinitionId == FirstSliceSkillId &&
                item.TargetId == EnemyActorId &&
                item.ReasonCode == "skill_target_invalid_before_release"),
            "battle report should preserve failed skill command reason from runtime events");
        AssertTrue(
            report.HeroSkillEffects.All(item => item.SourceCommandId != "cmd_hero_skill_report_failure"),
            "failed skill should not create report effect facts");
    }

    private static BattleStartSnapshot BuildOpposedSnapshot(
        string battleId,
        int enemyStrength,
        int enemyCellX = 6,
        int enemyCellY = 0)
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = $"snapshot_{battleId}",
            BattleId = battleId,
            TargetLocationId = "site_1",
            BattleGroups =
            {
                new BattleGroupSnapshot
                {
                    BattleGroupId = "group_player",
                    FactionId = "player",
                    SourceForceId = "force_player",
                    HeroId = "hero_player",
                    HeroDefinitionId = "hero_def_player",
                    CorpsId = "corps_player",
                    CorpsDefinitionId = "player_corps",
                    CorpsStrength = 100,
                    SourceLocationId = "city_player",
                    CellX = 0,
                    CellY = 0
                },
                new BattleGroupSnapshot
                {
                    BattleGroupId = "group_enemy",
                    FactionId = "enemy",
                    SourceForceId = "force_enemy",
                    HeroId = "hero_enemy",
                    HeroDefinitionId = "hero_def_enemy",
                    CorpsId = "corps_enemy",
                    CorpsDefinitionId = "enemy_corps",
                    CorpsStrength = enemyStrength,
                    SourceLocationId = "site_1",
                    CellX = enemyCellX,
                    CellY = enemyCellY
                }
            }
        };
        snapshot.SkillDefinitions.Add(new BattleSkillSnapshot
        {
            SkillId = FirstSliceSkillId,
            DisplayName = "破阵",
            TargetingMode = BattleSkillTargetingMode.TargetedActor,
            Range = 8,
            CastSeconds = 0,
            ImpactDelaySeconds = 0,
            RecoverySeconds = 0.2,
            CanInterruptBasicAttackWindup = true,
            CanCancelBasicAttackRecovery = false,
            Effects =
            {
                new BattleSkillEffectSnapshot
                {
                    Kind = BattleSkillEffectKind.Damage,
                    Amount = 18
                }
            }
        });
        return snapshot;
    }

    private static BattleRuntimeCommandSubmitResult SubmitTargetedSkill(
        BattleRuntimeSessionController controller,
        string battleId,
        string commandId,
        string targetActorId,
        string skillId = FirstSliceSkillId)
    {
        return controller.SubmitCommand(new CommandRequest
        {
            CommandId = commandId,
            BattleId = battleId,
            BattleGroupId = "group_player",
            Channel = CommandChannel.Hero,
            Kind = CommandKind.CastSkill,
            SkillId = skillId,
            TargetActorId = targetActorId
        });
    }

    private static void AddFollowUpSkill(BattleStartSnapshot snapshot, string skillId, int damage)
    {
        snapshot.SkillDefinitions.Add(new BattleSkillSnapshot
        {
            SkillId = skillId,
            DisplayName = "Follow Up",
            TargetingMode = BattleSkillTargetingMode.TargetedActor,
            Range = 8,
            CastSeconds = 0,
            ImpactDelaySeconds = 0,
            RecoverySeconds = 0.2,
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

    private static BattleRuntimeActor EnemyCorps(BattleRuntimeSessionController controller)
    {
        return controller.State.Actors.Single(item => item.ActorId == EnemyActorId);
    }

    private static BattleRuntimeActor Hero(BattleRuntimeSessionController controller)
    {
        return controller.State.Actors.Single(item => item.ActorId == "group_player:hero");
    }

    private static BattleRuntimeActor PlayerCorps(BattleRuntimeSessionController controller)
    {
        return controller.State.Actors.Single(item => item.ActorId == "force_player:1");
    }

    private static void AddBackupEnemy(BattleRuntimeSessionController controller)
    {
        controller.State.Actors.Add(new BattleRuntimeActor
        {
            ActorId = "force_enemy_backup:1",
            BattleGroupId = "group_enemy_backup",
            FactionId = "enemy",
            SourceForceId = "force_enemy_backup",
            SourceStateId = "corps_enemy_backup",
            Kind = BattleRuntimeActorKind.Corps,
            HitPoints = 1,
            GridX = 8,
            GridY = 0,
            GridHeight = 0,
            Position = 8
        });
    }

    private static BattleReportRecord BuildReport(
        BattleStartSnapshot snapshot,
        BattleRuntimeSessionController controller)
    {
        SettlementPlan settlement = new BattleSettlementService().BuildPlan(
            snapshot.SnapshotId,
            controller.Outcome,
            controller.EventStream);
        return new BattleReportBuilder().Build(
            controller.Outcome,
            controller.EventStream,
            settlement);
    }

    private static void AssertEffectDamage(
        BattleRuntimeAdvanceResult advance,
        string commandId,
        int expectedDamage,
        string message,
        string expectedActorId = "group_player:hero")
    {
        BattleEvent? damage = advance.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.DamageApplied &&
            item.ActorId == expectedActorId &&
            item.TargetId == EnemyActorId &&
            item.SourceCommandId == commandId);
        AssertTrue(damage != null, $"{message} should apply damage");
        AssertEqual(FirstSliceSkillId, damage!.SourceDefinitionId, $"{message} source definition");
        AssertEqual(-expectedDamage, damage.CorpsStrengthDelta, $"{message} amount");
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"{message}: expected={expected} actual={actual}");
        }
    }
}
