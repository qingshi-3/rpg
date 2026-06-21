using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Rpg.Application.Battle.Commands;
using Rpg.Application.Battle.Reports;
using Rpg.Application.Battle.Settlement;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;

internal static partial class TargetBattleEffectCommitBufferRegressionCases
{
    private const string FirstSliceSkillId = "first_slice_hero_breakthrough";
    private const string ZeroFloorSkillId = "effect_commit_zero_floor";
    private const string MultiDamageSkillId = "effect_commit_multi_damage";
    private const string MixedDamageChannelSkillId = "effect_commit_mixed_damage_channel";
    private const string DoubleChannelSkillId = "effect_commit_double_channel";
    private const string OpposingChannelSkillId = "effect_commit_opposing_channel";
    private const string ThunderSpiralBreakSkillId = "first_slice_skill_thunder_spiral_break";
    private const string PlayerActorId = "force_player:1";
    private const string EnemyActorId = "force_enemy:1";
    private const string NearEnemyActorId = "force_enemy_near:1";
    private const int StartChanneledAreaDamageEffectKindValue = 3;

    internal static void Register(Action<string, Action> run)
    {
        run("runtime effect receiver and health component are actor held", RuntimeEffectReceiverAndHealthComponentAreActorHeld);
        run("runtime effect resolver routes damage through commit buffer", RuntimeEffectResolverRoutesDamageThroughCommitBuffer);
        run("runtime channel damage uses effect delivery request boundary", RuntimeChannelDamageUsesEffectDeliveryRequestBoundary);
        run("runtime hero skill resolver does not tail patch defeated targets", RuntimeHeroSkillResolverDoesNotTailPatchDefeatedTargets);
        run("runtime hero skill damage compatible event order and attribution", RuntimeHeroSkillDamageCompatibleEventOrderAndAttribution);
        run("runtime lethal hero skill damage clamps and reports effect attribution", RuntimeLethalHeroSkillDamageClampsAndReportsEffectAttribution);
        run("runtime effect damage uses zero floor without basic attack minimum", RuntimeEffectDamageUsesZeroFloorWithoutBasicAttackMinimum);
        run("runtime multi damage effects report defeated transition once", RuntimeMultiDamageEffectsReportDefeatedTransitionOnce);
        run("runtime mixed damage and channel start effect ids stay unique", RuntimeMixedDamageAndChannelStartEffectIdsStayUnique);
        run("runtime active impact and pending release effect ids stay unique", RuntimeActiveImpactAndPendingReleaseEffectIdsStayUnique);
        run("runtime same tick channel damage effect ids stay unique", RuntimeSameTickChannelDamageEffectIdsStayUnique);
        run("runtime opposing same tick channels both resolve before defeat commit", RuntimeOpposingSameTickChannelsBothResolveBeforeDefeatCommit);
        run("runtime opposing same tick channel start ticks both resolve before defeat commit", RuntimeOpposingSameTickChannelStartsBothResolveBeforeDefeatCommit);
        run("runtime channeled area damage ticks immediately without catch up", RuntimeChanneledAreaDamageTicksImmediatelyWithoutCatchUp);
    }

    internal static void RuntimeEffectReceiverAndHealthComponentAreActorHeld()
    {
        string root = ProjectRoot();
        string battleRuntimePath = Path.Combine(root, "src", "Runtime", "Battle");
        string actorRuntimePath = Path.Combine(battleRuntimePath, "BattleActorRuntime.cs");
        AssertTrue(File.Exists(actorRuntimePath), "BattleActorRuntime source file should exist");

        AssertSourceTypeExists(battleRuntimePath, "BattleEffectReceiver", "Core Slice B should author an actor-local effect receiver");
        AssertSourceTypeExists(battleRuntimePath, "BattleHealthComponent", "Core Slice B should author an actor-local health component");

        string actorRuntimeSource = File.ReadAllText(actorRuntimePath);
        AssertTrue(
            actorRuntimeSource.Contains("BattleEffectReceiver", StringComparison.Ordinal),
            "BattleActorRuntime should hold or expose BattleEffectReceiver");
        AssertTrue(
            actorRuntimeSource.Contains("BattleHealthComponent", StringComparison.Ordinal),
            "BattleActorRuntime should hold or expose BattleHealthComponent");
    }

    internal static void RuntimeEffectResolverRoutesDamageThroughCommitBuffer()
    {
        string root = ProjectRoot();
        string effectResolverPath = Path.Combine(root, "src", "Runtime", "Battle", "Effects", "BattleEffectResolver.cs");
        AssertTrue(File.Exists(effectResolverPath), "BattleEffectResolver source file should exist");

        string source = File.ReadAllText(effectResolverPath);
        string relativePath = ToRepoPath(root, effectResolverPath);

        // Core Slice B should leave BattleEffectResolver as an effect request producer.
        // Target HP and defeat writes belong to the receiver/health/commit boundary.
        AssertDoesNotContain(source, "target.HitPoints =", relativePath, "BattleEffectResolver should not assign target HP directly");
        AssertDoesNotContain(source, "target.Phase = BattleRuntimeActorPhase.Defeated", relativePath, "BattleEffectResolver should not assign target defeated phase directly");
        AssertDoesNotContain(source, "MarkDefeated(target)", relativePath, "BattleEffectResolver should not directly mark effect targets defeated");
        AssertTrue(
            source.Contains("BattleCommitBuffer", StringComparison.Ordinal),
            "BattleEffectResolver should submit effect damage requests to BattleCommitBuffer");
    }

    internal static void RuntimeChannelDamageUsesEffectDeliveryRequestBoundary()
    {
        string root = ProjectRoot();
        string channelResolverPath = Path.Combine(root, "src", "Runtime", "Battle", "Effects", "BattleChannelDamageResolver.cs");
        string commitBufferPath = Path.Combine(root, "src", "Runtime", "Battle", "BattleCommitBuffer.cs");
        AssertTrue(File.Exists(channelResolverPath), "BattleChannelDamageResolver source file should exist");
        AssertTrue(File.Exists(commitBufferPath), "BattleCommitBuffer source file should exist");

        string channelSource = File.ReadAllText(channelResolverPath);
        string commitSource = File.ReadAllText(commitBufferPath);
        string channelRelativePath = ToRepoPath(root, channelResolverPath);
        string commitRelativePath = ToRepoPath(root, commitBufferPath);

        AssertDoesNotContain(channelSource, "BattleEffectResolver.Apply", channelRelativePath, "channel hit scan should enqueue effect delivery instead of executing effect primitives directly");
        AssertDoesNotContain(channelSource, "new BattleEffectPayload", channelRelativePath, "channel hit scan should not construct damage payloads for direct target execution");
        AssertTrue(
            commitSource.Contains("EffectDeliveryRequest", StringComparison.Ordinal),
            "BattleCommitBuffer should own effect delivery request records");
        AssertTrue(
            commitSource.Contains("RequestEffectDelivery", StringComparison.Ordinal),
            "BattleCommitBuffer should expose an effect delivery request boundary");
        AssertTrue(
            commitSource.Contains("CommitEffectDeliveries", StringComparison.Ordinal),
            "BattleCommitBuffer should expose a deterministic effect delivery phase");
        AssertTrue(
            commitSource.Contains("EffectReceiver", StringComparison.Ordinal),
            $"effect delivery commit phase should call the target receiver: file={commitRelativePath}");
        AssertTrue(
            commitSource.Contains("ActorAnchorOverride", StringComparison.Ordinal) &&
            commitSource.Contains("TargetAnchorOverride", StringComparison.Ordinal),
            "effect delivery should pass captured anchors into final damage event requests");
        AssertDoesNotContain(
            commitSource,
            "if (_effectDeliveryRequests.Count == 0)\r\n        {\r\n            return CommitEffectDamage();\r\n        }",
            commitRelativePath,
            "CommitEffectDeliveries should not flush unrelated direct damage when no deliveries are queued");
    }

    internal static void RuntimeHeroSkillResolverDoesNotTailPatchDefeatedTargets()
    {
        string root = ProjectRoot();
        string resolverPath = Path.Combine(root, "src", "Runtime", "Battle", "BattleRuntimeHeroSkillCommandResolver.cs");
        AssertTrue(File.Exists(resolverPath), "BattleRuntimeHeroSkillCommandResolver source file should exist");

        string source = File.ReadAllText(resolverPath);
        Regex directTargetDefeatPatch = new(
            @"if\s*\(\s*target\s*!=\s*null\s*&&\s*target\.HitPoints\s*<=\s*0\s*\)\s*\{?\s*BattleRuntimeActorStateMachine\.MarkDefeated\s*\(\s*target\s*\)",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        AssertTrue(
            !directTargetDefeatPatch.IsMatch(source),
            "BattleRuntimeHeroSkillCommandResolver should not tail-patch direct-target defeat after effect resolution");
    }

    internal static void RuntimeHeroSkillDamageCompatibleEventOrderAndAttribution()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_effect_commit_skill_damage", enemyStrength: 40);
        AddDamageSkill(snapshot, FirstSliceSkillId, damage: 18);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        FreezeAutonomousCorps(controller);

        BattleRuntimeCommandSubmitResult submit = SubmitTargetedSkill(
            controller,
            "battle_effect_commit_skill_damage",
            "cmd_effect_commit_skill_damage",
            EnemyActorId,
            FirstSliceSkillId);
        AssertTrue(submit.Accepted, $"normal damage skill should be accepted reason={submit.ReasonCode}");

        BattleRuntimeAdvanceResult advance = controller.AdvanceFixedTick();
        BattleEvent[] events = advance.Events.ToArray();
        int skillUsedIndex = IndexOf(events, item =>
            item.Kind == BattleEventKind.SkillUsed &&
            item.SourceCommandId == "cmd_effect_commit_skill_damage");
        int effectAppliedIndex = IndexOf(events, item =>
            item.Kind == BattleEventKind.EffectApplied &&
            item.SourceCommandId == "cmd_effect_commit_skill_damage");
        int damageAppliedIndex = IndexOf(events, item =>
            item.Kind == BattleEventKind.DamageApplied &&
            item.SourceCommandId == "cmd_effect_commit_skill_damage");

        AssertTrue(skillUsedIndex >= 0, "skill damage should emit SkillUsed");
        AssertTrue(effectAppliedIndex >= 0, "skill damage should emit EffectApplied");
        AssertTrue(damageAppliedIndex >= 0, "skill damage should emit DamageApplied");
        AssertTrue(
            skillUsedIndex < effectAppliedIndex && effectAppliedIndex < damageAppliedIndex,
            $"skill effect order should stay SkillUsed -> EffectApplied -> DamageApplied: {DescribeEvents(events)}");

        BattleEvent effectApplied = events[effectAppliedIndex];
        BattleEvent damageApplied = events[damageAppliedIndex];
        AssertEqual("cmd_effect_commit_skill_damage", damageApplied.SourceCommandId, "damage source command id");
        AssertTrue(!string.IsNullOrWhiteSpace(damageApplied.SourceActionId), "damage source action id should be preserved");
        AssertEqual(effectApplied.SourceActionId, damageApplied.SourceActionId, "effect and damage source action id should match");
        AssertEqual(FirstSliceSkillId, damageApplied.SourceDefinitionId, "damage source definition id");
        AssertEqual("Damage", damageApplied.EffectKind, "damage effect kind");
        AssertEqual(-18, damageApplied.CorpsStrengthDelta, "damage corps strength delta");
        AssertEqual("effect_damage", damageApplied.ReasonCode, "damage reason code");
        AssertEqual(22, EnemyCorps(controller).HitPoints, "enemy HP after compatible skill damage");
    }

    internal static void RuntimeLethalHeroSkillDamageClampsAndReportsEffectAttribution()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_effect_commit_lethal_skill_damage", enemyStrength: 10);
        AddDamageSkill(snapshot, FirstSliceSkillId, damage: 18);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        FreezeAutonomousCorps(controller);

        BattleRuntimeCommandSubmitResult submit = SubmitTargetedSkill(
            controller,
            "battle_effect_commit_lethal_skill_damage",
            "cmd_effect_commit_lethal_skill_damage",
            EnemyActorId,
            FirstSliceSkillId);
        AssertTrue(submit.Accepted, $"lethal damage skill should be accepted reason={submit.ReasonCode}");

        BattleRuntimeAdvanceResult advance = controller.AdvanceFixedTick();
        BattleEvent? effectApplied = advance.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.EffectApplied &&
            item.SourceCommandId == "cmd_effect_commit_lethal_skill_damage");
        BattleEvent? damageApplied = advance.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.DamageApplied &&
            item.SourceCommandId == "cmd_effect_commit_lethal_skill_damage");

        AssertTrue(effectApplied != null, "lethal skill damage should still emit EffectApplied");
        AssertTrue(damageApplied != null, "lethal skill damage should emit DamageApplied");
        AssertEqual(0, EnemyCorps(controller).HitPoints, "lethal skill damage should clamp target HP to zero");
        AssertTrue(
            damageApplied!.ReasonCode.Contains("defeated", StringComparison.Ordinal),
            $"lethal skill damage reason should include defeated actual={damageApplied.ReasonCode}");

        BattleReportRecord report = BuildReport(snapshot, controller);
        AssertTrue(
            report.HeroSkillEffects.Any(item =>
                item.SourceCommandId == "cmd_effect_commit_lethal_skill_damage" &&
                item.SourceActionId == effectApplied!.SourceActionId &&
                item.SourceDefinitionId == FirstSliceSkillId &&
                item.EffectKind == "Damage" &&
                item.TargetId == EnemyActorId &&
                item.CorpsStrengthDelta == -18),
            "battle report should keep reading skill effect attribution from EffectApplied");
    }

    internal static void RuntimeEffectDamageUsesZeroFloorWithoutBasicAttackMinimum()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_effect_commit_zero_floor", enemyStrength: 10);
        AddDamageSkill(snapshot, ZeroFloorSkillId, damage: -5);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        FreezeAutonomousCorps(controller);

        BattleRuntimeCommandSubmitResult submit = SubmitTargetedSkill(
            controller,
            "battle_effect_commit_zero_floor",
            "cmd_effect_commit_zero_floor",
            EnemyActorId,
            ZeroFloorSkillId);
        AssertTrue(submit.Accepted, $"zero-floor damage skill should be accepted reason={submit.ReasonCode}");

        BattleRuntimeAdvanceResult advance = controller.AdvanceFixedTick();
        BattleEvent? damageApplied = advance.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.DamageApplied &&
            item.SourceCommandId == "cmd_effect_commit_zero_floor");

        AssertTrue(damageApplied != null, "zero-floor skill damage should still emit compatible DamageApplied");
        AssertEqual(0, damageApplied!.CorpsStrengthDelta, "negative effect damage should floor to zero delta");
        AssertEqual("effect_damage", damageApplied.ReasonCode, "zero-floor effect damage should not report defeated");
        AssertEqual(10, EnemyCorps(controller).HitPoints, "negative effect damage should not use basic attack minimum damage");
    }

    internal static void RuntimeMultiDamageEffectsReportDefeatedTransitionOnce()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_effect_commit_multi_damage", enemyStrength: 10);
        AddMultiDamageSkill(snapshot, MultiDamageSkillId, firstDamage: 12, secondDamage: 5);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        FreezeAutonomousCorps(controller);

        BattleRuntimeCommandSubmitResult submit = SubmitTargetedSkill(
            controller,
            "battle_effect_commit_multi_damage",
            "cmd_effect_commit_multi_damage",
            EnemyActorId,
            MultiDamageSkillId);
        AssertTrue(submit.Accepted, $"multi-damage skill should be accepted reason={submit.ReasonCode}");

        BattleRuntimeAdvanceResult advance = controller.AdvanceFixedTick();
        BattleEvent[] damageEvents = advance.Events
            .Where(item =>
                item.Kind == BattleEventKind.DamageApplied &&
                item.SourceCommandId == "cmd_effect_commit_multi_damage")
            .ToArray();
        BattleEvent[] effectEvents = advance.Events
            .Where(item =>
                item.Kind == BattleEventKind.EffectApplied &&
                item.SourceCommandId == "cmd_effect_commit_multi_damage")
            .ToArray();

        AssertEqual(2, effectEvents.Length, $"multi-damage skill should emit each effect application event events={DescribeEvents(advance.Events)}");
        AssertEqual(2, damageEvents.Length, $"multi-damage skill should keep emitting each effect damage event events={DescribeEvents(advance.Events)}");
        AssertEqual(
            effectEvents.Length,
            effectEvents.Select(item => item.EventId).Distinct(StringComparer.Ordinal).Count(),
            $"multi-damage effect application event ids should remain unique events={string.Join("|", effectEvents.Select(item => item.EventId))}");
        AssertEqual(
            damageEvents.Length,
            damageEvents.Select(item => item.EventId).Distinct(StringComparer.Ordinal).Count(),
            $"multi-damage effect damage event ids should remain unique events={string.Join("|", damageEvents.Select(item => item.EventId))}");
        AssertEqual(
            1,
            damageEvents.Count(item => item.ReasonCode.Contains("defeated", StringComparison.Ordinal)),
            $"only the HP transition to zero should report defeated events={DescribeEvents(damageEvents)}");
        AssertEqual(0, EnemyCorps(controller).HitPoints, "multi-damage skill should clamp target HP to zero");
    }

    internal static void RuntimeMixedDamageAndChannelStartEffectIdsStayUnique()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_effect_commit_mixed_channel", enemyStrength: 10);
        AddMixedDamageChannelSkill(snapshot);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        FreezeAutonomousCorps(controller);

        CommandRequest request = new()
        {
            CommandId = "cmd_effect_commit_mixed_channel",
            BattleId = "battle_effect_commit_mixed_channel",
            BattleGroupId = "group_player",
            Channel = CommandChannel.Hero,
            Kind = CommandKind.CastSkill,
            SkillId = MixedDamageChannelSkillId,
            TargetActorId = EnemyActorId
        };
        SetCommandTargetGrid(request, x: 6, y: 0, height: 0);

        BattleRuntimeCommandSubmitResult submit = controller.SubmitCommand(request);
        AssertTrue(submit.Accepted, $"mixed damage/channel skill should be accepted reason={submit.ReasonCode}");

        BattleRuntimeAdvanceResult advance = controller.AdvanceFixedTick();
        BattleEvent[] effectEvents = advance.Events
            .Where(item =>
                item.Kind == BattleEventKind.EffectApplied &&
                item.SourceCommandId == request.CommandId &&
                item.TargetId == EnemyActorId)
            .ToArray();
        BattleEvent[] damageEvents = advance.Events
            .Where(item =>
                item.Kind == BattleEventKind.DamageApplied &&
                item.SourceCommandId == request.CommandId &&
                item.TargetId == EnemyActorId)
            .ToArray();

        AssertEqual(2, effectEvents.Length, $"mixed skill should emit direct and channel-start EffectApplied events={DescribeEvents(advance.Events)}");
        AssertEqual(2, damageEvents.Length, $"mixed skill should emit direct and channel-start DamageApplied events={DescribeEvents(advance.Events)}");
        AssertEqual(
            effectEvents.Length,
            effectEvents.Select(item => item.EventId).Distinct(StringComparer.Ordinal).Count(),
            $"mixed skill EffectApplied event ids should stay unique events={string.Join("|", effectEvents.Select(item => item.EventId))}");
        AssertEqual(
            damageEvents.Length,
            damageEvents.Select(item => item.EventId).Distinct(StringComparer.Ordinal).Count(),
            $"mixed skill DamageApplied event ids should stay unique events={string.Join("|", damageEvents.Select(item => item.EventId))}");
    }

    internal static void RuntimeChanneledAreaDamageTicksImmediatelyWithoutCatchUp()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot(
            "battle_effect_commit_channel_cadence",
            enemyStrength: 100,
            enemyCellX: 6,
            enemyCellY: 0);
        AddThunderSpiralSkill(snapshot);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        AddNearEnemy(controller);
        FreezeAutonomousCorps(controller);

        CommandRequest request = new()
        {
            CommandId = "cmd_effect_commit_channel_cadence",
            BattleId = "battle_effect_commit_channel_cadence",
            BattleGroupId = "group_player",
            SourceActorId = "group_player:hero",
            Channel = CommandChannel.Hero,
            Kind = CommandKind.CastSkill,
            SkillId = ThunderSpiralBreakSkillId
        };
        SetCommandTargetGrid(request, x: 2, y: 0, height: 0);

        BattleRuntimeCommandSubmitResult submit = controller.SubmitCommand(request);
        AssertTrue(submit.Accepted, $"channeled area skill should accept selected target cell reason={submit.ReasonCode}");

        BattleRuntimeAdvanceResult start = controller.AdvanceFixedTick();
        AssertEqual(1, CountDamageEvents(start, "cmd_effect_commit_channel_cadence", NearEnemyActorId), "channel start should immediately apply one damage tick");
        AssertEqual(71, NearEnemy(controller).HitPoints, "near enemy HP after initial channel tick");

        BattleRuntimeAdvanceResult longStep = controller.AdvanceFixedTick(0.6);
        AssertEqual(
            0,
            CountDamageEvents(longStep, "cmd_effect_commit_channel_cadence", NearEnemyActorId),
            $"channel cadence should resolve from the tick-start runtime time rather than the post-advance time events={DescribeEvents(longStep.Events)}");
        AssertEqual(71, NearEnemy(controller).HitPoints, "near enemy HP should not change during the overshoot advance itself");

        BattleRuntimeAdvanceResult catchUpBoundary = controller.AdvanceFixedTick();
        AssertEqual(
            1,
            CountDamageEvents(catchUpBoundary, "cmd_effect_commit_channel_cadence", NearEnemyActorId),
            $"channel cadence should not backfill multiple missed ticks after a long runtime advance events={DescribeEvents(catchUpBoundary.Events)}");
        AssertEqual(62, NearEnemy(controller).HitPoints, "near enemy HP after one cadence tick despite long fixed step");
    }

    internal static void RuntimeSameTickChannelDamageEffectIdsStayUnique()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot(
            "battle_effect_commit_double_channel_tick",
            enemyStrength: 100,
            enemyCellX: 6,
            enemyCellY: 0);
        AddDoubleChannelSkill(snapshot);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        FreezeAutonomousCorps(controller);

        CommandRequest request = new()
        {
            CommandId = "cmd_effect_commit_double_channel_tick",
            BattleId = "battle_effect_commit_double_channel_tick",
            BattleGroupId = "group_player",
            SourceActorId = "group_player:hero",
            Channel = CommandChannel.Hero,
            Kind = CommandKind.CastSkill,
            SkillId = DoubleChannelSkillId
        };
        SetCommandTargetGrid(request, x: 6, y: 0, height: 0);

        BattleRuntimeCommandSubmitResult submit = controller.SubmitCommand(request);
        AssertTrue(submit.Accepted, $"double channel skill should accept selected target cell reason={submit.ReasonCode}");

        controller.AdvanceFixedTick(0.2);
        BattleRuntimeAdvanceResult channelTick = controller.AdvanceFixedTick();
        BattleEvent[] effectEvents = channelTick.Events
            .Where(item =>
                item.Kind == BattleEventKind.EffectApplied &&
                item.SourceCommandId == request.CommandId &&
                item.TargetId == EnemyActorId)
            .ToArray();
        BattleEvent[] damageEvents = channelTick.Events
            .Where(item =>
                item.Kind == BattleEventKind.DamageApplied &&
                item.SourceCommandId == request.CommandId &&
                item.TargetId == EnemyActorId)
            .ToArray();

        AssertEqual(2, effectEvents.Length, $"same-tick channel tick should emit both EffectApplied events={DescribeEvents(channelTick.Events)}");
        AssertEqual(2, damageEvents.Length, $"same-tick channel tick should emit both DamageApplied events={DescribeEvents(channelTick.Events)}");
        AssertEqual(
            effectEvents.Length,
            effectEvents.Select(item => item.EventId).Distinct(StringComparer.Ordinal).Count(),
            $"same-tick channel EffectApplied event ids should stay unique events={string.Join("|", effectEvents.Select(item => item.EventId))}");
        AssertEqual(
            damageEvents.Length,
            damageEvents.Select(item => item.EventId).Distinct(StringComparer.Ordinal).Count(),
            $"same-tick channel DamageApplied event ids should stay unique events={string.Join("|", damageEvents.Select(item => item.EventId))}");
    }

    internal static void RuntimeOpposingSameTickChannelsBothResolveBeforeDefeatCommit()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot(
            "battle_effect_commit_opposing_channel_tick",
            enemyStrength: 20,
            enemyCellX: 6,
            enemyCellY: 0);
        AddOpposingChannelSkill(snapshot);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        PlayerCorps(controller).HitPoints = 20;

        CommandRequest playerRequest = new()
        {
            CommandId = "cmd_effect_commit_opposing_player_channel",
            BattleId = "battle_effect_commit_opposing_channel_tick",
            BattleGroupId = "group_player",
            SourceActorId = PlayerActorId,
            Channel = CommandChannel.Hero,
            Kind = CommandKind.CastSkill,
            SkillId = OpposingChannelSkillId
        };
        SetCommandTargetGrid(playerRequest, x: 6, y: 0, height: 0);
        CommandRequest enemyRequest = new()
        {
            CommandId = "cmd_effect_commit_opposing_enemy_channel",
            BattleId = "battle_effect_commit_opposing_channel_tick",
            BattleGroupId = "group_enemy",
            SourceActorId = EnemyActorId,
            Channel = CommandChannel.Hero,
            Kind = CommandKind.CastSkill,
            SkillId = OpposingChannelSkillId
        };
        SetCommandTargetGrid(enemyRequest, x: 0, y: 0, height: 0);

        AssertTrue(controller.SubmitCommand(playerRequest).Accepted, "player corps channel command should be accepted");
        AssertTrue(controller.SubmitCommand(enemyRequest).Accepted, "enemy corps channel command should be accepted");

        BattleRuntimeAdvanceResult start = controller.AdvanceFixedTick(0.2);
        AssertEqual(10, PlayerCorps(controller).HitPoints, $"player HP after channel-start tick events={DescribeEvents(start.Events)}");
        AssertEqual(10, EnemyCorps(controller).HitPoints, $"enemy HP after channel-start tick events={DescribeEvents(start.Events)}");

        BattleRuntimeAdvanceResult channelTick = controller.AdvanceFixedTick();
        AssertEqual(
            1,
            CountDamageEvents(channelTick, playerRequest.CommandId, EnemyActorId),
            $"player channel should damage enemy during the ordinary channel tick events={DescribeEvents(channelTick.Events)}");
        AssertEqual(
            1,
            CountDamageEvents(channelTick, enemyRequest.CommandId, PlayerActorId),
            $"enemy channel should still damage player during the same ordinary channel tick events={DescribeEvents(channelTick.Events)}");
        AssertEqual(0, PlayerCorps(controller).HitPoints, "player corps should receive same-tick opposing channel damage before defeat commit");
        AssertEqual(0, EnemyCorps(controller).HitPoints, "enemy corps should receive same-tick opposing channel damage before defeat commit");
    }

    internal static void RuntimeOpposingSameTickChannelStartsBothResolveBeforeDefeatCommit()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot(
            "battle_effect_commit_opposing_channel_start",
            enemyStrength: 10,
            enemyCellX: 6,
            enemyCellY: 0);
        AddOpposingChannelSkill(snapshot);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        PlayerCorps(controller).HitPoints = 10;

        CommandRequest playerRequest = new()
        {
            CommandId = "cmd_effect_commit_opposing_player_channel_start",
            BattleId = "battle_effect_commit_opposing_channel_start",
            BattleGroupId = "group_player",
            SourceActorId = PlayerActorId,
            Channel = CommandChannel.Hero,
            Kind = CommandKind.CastSkill,
            SkillId = OpposingChannelSkillId
        };
        SetCommandTargetGrid(playerRequest, x: 6, y: 0, height: 0);
        CommandRequest enemyRequest = new()
        {
            CommandId = "cmd_effect_commit_opposing_enemy_channel_start",
            BattleId = "battle_effect_commit_opposing_channel_start",
            BattleGroupId = "group_enemy",
            SourceActorId = EnemyActorId,
            Channel = CommandChannel.Hero,
            Kind = CommandKind.CastSkill,
            SkillId = OpposingChannelSkillId
        };
        SetCommandTargetGrid(enemyRequest, x: 0, y: 0, height: 0);

        AssertTrue(controller.SubmitCommand(playerRequest).Accepted, "player corps channel command should be accepted");
        AssertTrue(controller.SubmitCommand(enemyRequest).Accepted, "enemy corps channel command should be accepted");

        BattleRuntimeAdvanceResult start = controller.AdvanceFixedTick(0.2);

        AssertEqual(
            1,
            CountDamageEvents(start, playerRequest.CommandId, EnemyActorId),
            $"player channel start should damage enemy during the shared start phase events={DescribeEvents(start.Events)}");
        AssertEqual(
            1,
            CountDamageEvents(start, enemyRequest.CommandId, PlayerActorId),
            $"enemy channel start should still damage player during the shared start phase events={DescribeEvents(start.Events)}");
        AssertEqual(0, PlayerCorps(controller).HitPoints, "player corps should receive same-tick opposing channel-start damage before defeat commit");
        AssertEqual(0, EnemyCorps(controller).HitPoints, "enemy corps should receive same-tick opposing channel-start damage before defeat commit");
    }

    private static BattleStartSnapshot BuildOpposedSnapshot(
        string battleId,
        int enemyStrength,
        int enemyCellX = 6,
        int enemyCellY = 0)
    {
        return new BattleStartSnapshot
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
    }

    private static void AddDamageSkill(BattleStartSnapshot snapshot, string skillId, int damage)
    {
        snapshot.SkillDefinitions.Add(new BattleSkillSnapshot
        {
            SkillId = skillId,
            DisplayName = "Effect Commit Damage",
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

    private static void AddMultiDamageSkill(BattleStartSnapshot snapshot, string skillId, int firstDamage, int secondDamage)
    {
        snapshot.SkillDefinitions.Add(new BattleSkillSnapshot
        {
            SkillId = skillId,
            DisplayName = "Effect Commit Multi Damage",
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
                    Amount = firstDamage
                },
                new BattleSkillEffectSnapshot
                {
                    Kind = BattleSkillEffectKind.Damage,
                    Amount = secondDamage
                }
            }
        });
    }

    private static void AddMixedDamageChannelSkill(BattleStartSnapshot snapshot)
    {
        snapshot.SkillDefinitions.Add(new BattleSkillSnapshot
        {
            SkillId = MixedDamageChannelSkillId,
            DisplayName = "Effect Commit Mixed Damage Channel",
            TargetingMode = BattleSkillTargetingMode.TargetedActorOrCell,
            Range = 8,
            CastSeconds = 0,
            ImpactDelaySeconds = 0,
            RecoverySeconds = 0,
            CanInterruptBasicAttackWindup = true,
            Effects =
            {
                new BattleSkillEffectSnapshot
                {
                    Kind = BattleSkillEffectKind.Damage,
                    Amount = 1
                },
                new BattleSkillEffectSnapshot
                {
                    Kind = (BattleSkillEffectKind)StartChanneledAreaDamageEffectKindValue,
                    Amount = 1,
                    DurationSeconds = 0.4,
                    TickIntervalSeconds = 0.2,
                    Radius = 0
                }
            }
        });
    }

    private static void AddThunderSpiralSkill(BattleStartSnapshot snapshot)
    {
        snapshot.SkillDefinitions.Add(new BattleSkillSnapshot
        {
            SkillId = ThunderSpiralBreakSkillId,
            DisplayName = "Thunder Spiral Break",
            TargetingMode = BattleSkillTargetingMode.TargetedCell,
            Range = 3,
            CastSeconds = 0,
            ImpactDelaySeconds = 0,
            RecoverySeconds = 0,
            CanInterruptBasicAttackWindup = true,
            CanCancelBasicAttackRecovery = true,
            Effects =
            {
                new BattleSkillEffectSnapshot
                {
                    Kind = (BattleSkillEffectKind)StartChanneledAreaDamageEffectKindValue,
                    Amount = 9,
                    DurationSeconds = 1.6,
                    TickIntervalSeconds = 0.2,
                    Radius = 1
                }
            }
        });
    }

    private static void AddDoubleChannelSkill(BattleStartSnapshot snapshot)
    {
        snapshot.SkillDefinitions.Add(new BattleSkillSnapshot
        {
            SkillId = DoubleChannelSkillId,
            DisplayName = "Effect Commit Double Channel",
            TargetingMode = BattleSkillTargetingMode.TargetedCell,
            Range = 8,
            CastSeconds = 0,
            ImpactDelaySeconds = 0,
            RecoverySeconds = 0,
            CanInterruptBasicAttackWindup = true,
            Effects =
            {
                new BattleSkillEffectSnapshot
                {
                    Kind = (BattleSkillEffectKind)StartChanneledAreaDamageEffectKindValue,
                    Amount = 1,
                    DurationSeconds = 0.8,
                    TickIntervalSeconds = 0.2,
                    Radius = 0
                },
                new BattleSkillEffectSnapshot
                {
                    Kind = (BattleSkillEffectKind)StartChanneledAreaDamageEffectKindValue,
                    Amount = 1,
                    DurationSeconds = 0.8,
                    TickIntervalSeconds = 0.2,
                    Radius = 0
                }
            }
        });
    }

    private static void AddOpposingChannelSkill(BattleStartSnapshot snapshot)
    {
        snapshot.SkillDefinitions.Add(new BattleSkillSnapshot
        {
            SkillId = OpposingChannelSkillId,
            DisplayName = "Effect Commit Opposing Channel",
            TargetingMode = BattleSkillTargetingMode.TargetedCell,
            Range = 8,
            CastSeconds = 0,
            ImpactDelaySeconds = 0,
            RecoverySeconds = 0,
            CanInterruptBasicAttackWindup = true,
            Effects =
            {
                new BattleSkillEffectSnapshot
                {
                    Kind = (BattleSkillEffectKind)StartChanneledAreaDamageEffectKindValue,
                    Amount = 10,
                    DurationSeconds = 0.8,
                    TickIntervalSeconds = 0.2,
                    Radius = 0
                }
            }
        });
    }

    private static BattleRuntimeCommandSubmitResult SubmitTargetedSkill(
        BattleRuntimeSessionController controller,
        string battleId,
        string commandId,
        string targetActorId,
        string skillId)
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

    private static void SetCommandTargetGrid(CommandRequest request, int x, int y, int height)
    {
        SetProperty(request, "HasTargetGrid", true);
        SetProperty(request, "TargetGridX", x);
        SetProperty(request, "TargetGridY", y);
        SetProperty(request, "TargetGridHeight", height);
    }

    private static void AddNearEnemy(BattleRuntimeSessionController controller)
    {
        controller.State.Actors.Add(new BattleRuntimeActor
        {
            ActorId = NearEnemyActorId,
            BattleGroupId = "group_enemy_near",
            FactionId = "enemy",
            SourceForceId = "force_enemy_near",
            SourceStateId = "corps_enemy_near",
            Kind = BattleRuntimeActorKind.Corps,
            HitPoints = 80,
            GridX = 1,
            GridY = 0,
            GridHeight = 0,
            Position = 1
        });
    }

    private static void FreezeAutonomousCorps(BattleRuntimeSessionController controller)
    {
        foreach (BattleRuntimeActor actor in controller.State.Actors.Where(item => item.Kind == BattleRuntimeActorKind.Corps))
        {
            actor.Phase = BattleRuntimeActorPhase.Holding;
            actor.ActionReadyAtSeconds = 100;
        }
    }

    private static BattleRuntimeActor EnemyCorps(BattleRuntimeSessionController controller)
    {
        return controller.State.Actors.Single(item => item.ActorId == EnemyActorId);
    }

    private static BattleRuntimeActor PlayerCorps(BattleRuntimeSessionController controller)
    {
        return controller.State.Actors.Single(item => item.ActorId == PlayerActorId);
    }

    private static BattleRuntimeActor NearEnemy(BattleRuntimeSessionController controller)
    {
        return controller.State.Actors.Single(item => item.ActorId == NearEnemyActorId);
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

    private static int CountDamageEvents(BattleRuntimeAdvanceResult advance, string commandId, string targetActorId)
    {
        return advance.Events.Count(item =>
            item.Kind == BattleEventKind.DamageApplied &&
            item.SourceCommandId == commandId &&
            item.TargetId == targetActorId);
    }

    private static int IndexOf(IReadOnlyList<BattleEvent> events, Func<BattleEvent, bool> predicate)
    {
        for (int i = 0; i < events.Count; i++)
        {
            if (predicate(events[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static void AssertSourceTypeExists(string root, string typeName, string message)
    {
        bool exists = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .Any(source =>
                source.Contains($"class {typeName}", StringComparison.Ordinal) ||
                source.Contains($"record {typeName}", StringComparison.Ordinal) ||
                source.Contains($"struct {typeName}", StringComparison.Ordinal));
        AssertTrue(exists, message);
    }

    private static void SetProperty<T>(object source, string propertyName, T value)
    {
        System.Reflection.PropertyInfo? property = source.GetType().GetProperty(propertyName);
        AssertTrue(property != null && property.CanWrite, $"{source.GetType().Name} should expose writable {propertyName}");
        property!.SetValue(source, value);
    }

    private static string ProjectRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current != null && !File.Exists(Path.Combine(current.FullName, "rpg.csproj")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("project root not found");
    }

    private static string ToRepoPath(string root, string path)
    {
        return Path.GetRelativePath(root, path).Replace('\\', '/');
    }

    private static void AssertDoesNotContain(string source, string forbidden, string relativePath, string message)
    {
        AssertTrue(
            !source.Contains(forbidden, StringComparison.Ordinal),
            $"{message}: file={relativePath} forbidden={forbidden}");
    }

    private static string DescribeEvents(IEnumerable<BattleEvent> events)
    {
        return string.Join("|", events.Select(item =>
            $"{item.Kind}:{item.SourceCommandId}:{item.ActorId}->{item.TargetId}:{item.ReasonCode}@{item.RuntimeTick}/{item.RuntimeTimeSeconds:0.###}"));
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"{message}: expected={expected} actual={actual}");
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
