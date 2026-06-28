using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Rpg.Application.Battle.Commands;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;

internal static class TargetBattleAbilityControllerRegressionCases
{
    internal static void Register(Action<string, Action> run)
    {
        run("runtime ability controller and actor ability shell are authored", RuntimeAbilityControllerAndActorAbilityShellAreAuthored);
        run("runtime hero skill resolver delegates active skill lifecycle", RuntimeHeroSkillResolverDelegatesActiveSkillLifecycle);
        run("runtime ability controller owns pending ability orders", RuntimeAbilityControllerOwnsPendingAbilityOrders);
        run("runtime active channels are actor owned", RuntimeActiveChannelsAreActorOwned);
        run("runtime ability controller preserves effect and spatial authority boundaries", RuntimeAbilityControllerPreservesEffectAndSpatialAuthorityBoundaries);
        run("runtime ability effect release is boundary owned", RuntimeAbilityEffectReleaseIsBoundaryOwned);
        run("runtime ability ticking is coordinator owned", RuntimeAbilityTickingIsCoordinatorOwned);
        run("runtime delayed cell skill preserves locked target payload", RuntimeDelayedCellSkillPreservesLockedTargetPayload);
        run("runtime pending ability order consumes one action per actor tick", RuntimePendingAbilityOrderConsumesOneActionPerActorTick);
        run("runtime pending ability failure preserves command attribution", RuntimePendingAbilityFailurePreservesCommandAttribution);
        run("runtime active one use skill rejects duplicate queued order", RuntimeActiveOneUseSkillRejectsDuplicateQueuedOrder);
    }

    private static void RuntimeAbilityControllerAndActorAbilityShellAreAuthored()
    {
        string root = ProjectRoot();
        string battleRuntimePath = Path.Combine(root, "src", "Runtime", "Battle");
        string controllerPath = Path.Combine(battleRuntimePath, "BattleAbilityController.cs");
        string actorRuntimePath = Path.Combine(battleRuntimePath, "BattleActorRuntime.cs");

        AssertTrue(File.Exists(controllerPath), "Core Slice C should author BattleAbilityController");
        AssertTrue(File.ReadAllText(controllerPath).Contains("class BattleAbilityController", StringComparison.Ordinal), "BattleAbilityController should be a runtime class");

        string actorRuntimeSource = File.ReadAllText(actorRuntimePath);
        AssertTrue(actorRuntimeSource.Contains("BattleAbilityController", StringComparison.Ordinal), "BattleActorRuntime should hold or expose BattleAbilityController");
        AssertTrue(actorRuntimeSource.Contains("AbilityController", StringComparison.Ordinal), "BattleActorRuntime should expose the actor ability controller by intent");
    }

    private static void RuntimeHeroSkillResolverDelegatesActiveSkillLifecycle()
    {
        string root = ProjectRoot();
        string battleRuntimePath = Path.Combine(root, "src", "Runtime", "Battle");
        string resolverPath = Path.Combine(root, "src", "Runtime", "Battle", "BattleRuntimeHeroSkillCommandResolver.cs");
        string legacyEventsPath = Path.Combine(battleRuntimePath, "BattleRuntimeHeroSkillCommandResolver.Events.cs");
        string source = string.Join(
            Environment.NewLine,
            Directory.GetFiles(battleRuntimePath, "BattleRuntimeHeroSkillCommandResolver*.cs")
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
        string relativePath = ToRepoPath(root, resolverPath);

        AssertTrue(!File.Exists(legacyEventsPath), "hero skill resolver should not keep legacy skill event helper file after ability controller migration");
        AssertContains(source, "AbilityController", relativePath, "hero skill resolver should delegate actor-local skill execution to BattleAbilityController");
        AssertDoesNotContain(source, "private static void AdvanceActiveSkillActions", relativePath, "hero skill resolver should not own active skill cast/recovery advancement");
        AssertDoesNotContain(source, "private static bool StartSkillAction", relativePath, "hero skill resolver should not own skill action start");
        AssertDoesNotContain(source, "BattleRuntimeActorStateMachine.MarkSkillCasting", relativePath, "hero skill resolver should not directly mark skill casting");
        AssertDoesNotContain(source, "BattleRuntimeActorStateMachine.MarkSkillRecovery", relativePath, "hero skill resolver should not directly mark skill recovery");
        AssertDoesNotContain(source, "CurrentSkillImpactApplied", relativePath, "hero skill resolver should not directly own active skill impact state");
        AssertDoesNotContain(source, "CurrentSkillImpactAtSeconds", relativePath, "hero skill resolver should not directly own active skill impact timing");
    }

    private static void RuntimeAbilityControllerOwnsPendingAbilityOrders()
    {
        string root = ProjectRoot();
        string battleRuntimePath = Path.Combine(root, "src", "Runtime", "Battle");
        string controllerPath = Path.Combine(battleRuntimePath, "BattleAbilityController.cs");
        string actorPath = Path.Combine(battleRuntimePath, "BattleRuntimeActor.cs");
        string statePath = Path.Combine(battleRuntimePath, "BattleRuntimeState.cs");
        string resolverPath = Path.Combine(battleRuntimePath, "BattleRuntimeHeroSkillCommandResolver.cs");
        string controllerSource = File.ReadAllText(controllerPath);
        string actorSource = File.ReadAllText(actorPath);
        string stateSource = File.ReadAllText(statePath);
        string resolverSource = File.ReadAllText(resolverPath);

        AssertContains(actorSource, "PendingAbilityOrders", ToRepoPath(root, actorPath), "actor state should carry actor-local pending ability orders");
        AssertContains(controllerSource, "EnqueuePendingSkillOrder", ToRepoPath(root, controllerPath), "ability controller should own pending skill order submission");
        AssertContains(controllerSource, "ResolvePendingSkillOrders", ToRepoPath(root, controllerPath), "ability controller should own pending skill order resolution");
        AssertContains(controllerSource, "PendingAbilityOrders", ToRepoPath(root, controllerPath), "ability controller should manipulate actor-local pending ability orders");
        AssertDoesNotContain(stateSource, "PendingHeroSkillCommands", ToRepoPath(root, statePath), "runtime state should not own a global pending hero skill command queue");
        AssertDoesNotContain(resolverSource, "PendingHeroSkillCommands", ToRepoPath(root, resolverPath), "hero skill resolver should not iterate or remove a global pending skill queue");
    }

    private static void RuntimeActiveChannelsAreActorOwned()
    {
        string root = ProjectRoot();
        string battleRuntimePath = Path.Combine(root, "src", "Runtime", "Battle");
        string controllerPath = Path.Combine(battleRuntimePath, "BattleAbilityController.cs");
        string actorPath = Path.Combine(battleRuntimePath, "BattleRuntimeActor.cs");
        string statePath = Path.Combine(battleRuntimePath, "BattleRuntimeState.cs");
        string resolverPath = Path.Combine(battleRuntimePath, "BattleRuntimeHeroSkillCommandResolver.cs");
        string effectResolverPath = Path.Combine(battleRuntimePath, "Effects", "BattleEffectResolver.cs");
        string controllerSource = File.ReadAllText(controllerPath);
        string actorSource = File.ReadAllText(actorPath);
        string stateSource = File.ReadAllText(statePath);
        string resolverSource = File.ReadAllText(resolverPath);
        string effectResolverSource = File.ReadAllText(effectResolverPath);

        AssertContains(actorSource, "ActiveChannels", ToRepoPath(root, actorPath), "actor state should carry caster-owned active channels");
        AssertContains(controllerSource, "_actor.ActiveChannels", ToRepoPath(root, controllerPath), "ability controller should tick caster-owned active channels");
        AssertContains(controllerSource, "AdvanceActiveChannels", ToRepoPath(root, controllerPath), "ability controller should expose actor-local channel ticking");
        AssertDoesNotContain(stateSource, "ActiveChannels", ToRepoPath(root, statePath), "runtime state should not own a global active channel list");
        AssertDoesNotContain(stateSource, "BattleRuntimeActiveChannel", ToRepoPath(root, statePath), "runtime state should not own renamed active channel containers");
        AssertDoesNotContain(effectResolverSource, "state.ActiveChannels.Add", ToRepoPath(root, effectResolverPath), "effect resolver should add channels to the caster actor, not global runtime state");
        AssertDoesNotContain(controllerSource, "state.ActiveChannels", ToRepoPath(root, controllerPath), "ability controller should not tick global runtime channels");
        AssertDoesNotContain(resolverSource, "BattleAbilityController.AdvanceActiveChannels", ToRepoPath(root, resolverPath), "hero skill command resolver should not call a global active-channel tick entry");
        AssertNoActiveChannelContainerOutsideActor(root, actorPath);
    }

    private static void RuntimeAbilityControllerPreservesEffectAndSpatialAuthorityBoundaries()
    {
        string root = ProjectRoot();
        string controllerPath = Path.Combine(root, "src", "Runtime", "Battle", "BattleAbilityController.cs");
        string channelResolverPath = Path.Combine(root, "src", "Runtime", "Battle", "Effects", "BattleChannelDamageResolver.cs");
        string effectResolverPath = Path.Combine(root, "src", "Runtime", "Battle", "Effects", "BattleEffectResolver.cs");
        AssertTrue(File.Exists(controllerPath), "BattleAbilityController source file should exist");
        AssertTrue(File.Exists(channelResolverPath), "BattleChannelDamageResolver source file should exist");

        string source = File.ReadAllText(controllerPath);
        string channelResolverSource = File.ReadAllText(channelResolverPath);
        string effectResolverSource = File.ReadAllText(effectResolverPath);
        string relativePath = ToRepoPath(root, controllerPath);
        string channelResolverRelativePath = ToRepoPath(root, channelResolverPath);

        AssertDoesNotContain(source, "BattleEffectResolver.AdvanceActiveChannels", relativePath, "ability controller should own channel ticking instead of forwarding to the effect resolver");
        AssertDoesNotContain(effectResolverSource, "AdvanceActiveChannels", ToRepoPath(root, effectResolverPath), "effect resolver should execute effect primitives, not own active channel lifecycle ticking");
        AssertDoesNotContain(effectResolverSource, "BattleAbilityController", ToRepoPath(root, effectResolverPath), "effect resolver should not call back into the ability lifecycle owner");
        AssertDoesNotContain(source, "using Godot", relativePath, "runtime ability controller must remain pure C# runtime code");
        AssertDoesNotContain(source, "DateTime", relativePath, "runtime ability controller must consume runtime time, not wall-clock time");
        AssertDoesNotContain(source, "Stopwatch", relativePath, "runtime ability controller must consume runtime time, not wall-clock time");
        AssertDoesNotContain(source, "Task.Delay", relativePath, "runtime ability controller must not use real-time async waits");
        AssertDoesNotContain(source, ".HitPoints =", relativePath, "ability controller must not directly mutate health");
        AssertDoesNotContain(source, "MarkDefeated", relativePath, "ability controller must not own defeat response");
        AssertDoesNotContain(source, "BattleEventKind.DamageApplied", relativePath, "ability controller must not emit damage events directly");
        AssertDoesNotContain(source, "BattleEventKind.EffectApplied", relativePath, "ability controller must not emit effect events directly");
        AssertDoesNotContain(source, ".GridX =", relativePath, "ability controller must not directly mutate spatial anchors");
        AssertDoesNotContain(source, ".GridY =", relativePath, "ability controller must not directly mutate spatial anchors");
        AssertDoesNotContain(source, "CommitDisplacement", relativePath, "ability controller must not own teleport displacement authority");
        AssertDoesNotContain(channelResolverSource, "using Godot", channelResolverRelativePath, "channel damage resolver must remain pure C# runtime code");
        AssertDoesNotContain(channelResolverSource, "DateTime", channelResolverRelativePath, "channel damage resolver must consume runtime time, not wall-clock time");
        AssertDoesNotContain(channelResolverSource, "Stopwatch", channelResolverRelativePath, "channel damage resolver must consume runtime time, not wall-clock time");
        AssertDoesNotContain(channelResolverSource, "Task.Delay", channelResolverRelativePath, "channel damage resolver must not use real-time async waits");
        AssertDoesNotContain(channelResolverSource, ".HitPoints =", channelResolverRelativePath, "channel damage resolver must not directly mutate health");
        AssertDoesNotContain(channelResolverSource, "MarkDefeated", channelResolverRelativePath, "channel damage resolver must not own defeat response");
        AssertDoesNotContain(channelResolverSource, "NextTickAtSeconds +=", channelResolverRelativePath, "channel damage resolver must not own channel cadence lifecycle");
        AssertDoesNotContain(channelResolverSource, "ActiveChannels.Remove", channelResolverRelativePath, "channel damage resolver must not own channel expiry lifecycle");
    }

    private static void RuntimeAbilityEffectReleaseIsBoundaryOwned()
    {
        string root = ProjectRoot();
        string controllerPath = Path.Combine(root, "src", "Runtime", "Battle", "BattleAbilityController.cs");
        string boundaryPath = Path.Combine(root, "src", "Runtime", "Battle", "BattleAbilityEffectReleaseBoundary.cs");
        string controllerSource = File.ReadAllText(controllerPath);

        AssertTrue(File.Exists(boundaryPath), "Core Slice H18 should author BattleAbilityEffectReleaseBoundary");

        string boundarySource = File.ReadAllText(boundaryPath);
        string controllerRelativePath = ToRepoPath(root, controllerPath);
        string boundaryRelativePath = ToRepoPath(root, boundaryPath);

        AssertContains(boundarySource, "class BattleAbilityEffectReleaseBoundary", boundaryRelativePath, "ability effect release boundary should be an explicit runtime service");
        AssertContains(boundarySource, "ReleaseSkillEffects", boundaryRelativePath, "ability effect release boundary should expose skill effect release");
        AssertContains(boundarySource, "BattleEffectResolver.Apply", boundaryRelativePath, "ability effect release boundary should dispatch effect primitives");
        AssertContains(boundarySource, "DeferEffectDamageCommit", boundaryRelativePath, "ability effect release boundary should preserve channel-start batching semantics");
        AssertContains(controllerSource, "BattleAbilityEffectReleaseBoundary.ReleaseSkillEffects", controllerRelativePath, "ability controller should delegate effect payload dispatch");
        AssertDoesNotContain(controllerSource, "BattleEffectResolver.Apply", controllerRelativePath, "ability controller must not execute effect primitives directly");
        AssertDoesNotContain(controllerSource, "new BattleEffectExecutionContext", controllerRelativePath, "ability controller must not build effect execution contexts directly");
        AssertDoesNotContain(controllerSource, "new BattleEffectPayload", controllerRelativePath, "ability controller must not build effect payloads directly");
    }

    private static void RuntimeAbilityTickingIsCoordinatorOwned()
    {
        string root = ProjectRoot();
        string battleRuntimePath = Path.Combine(root, "src", "Runtime", "Battle");
        string coordinatorPath = Path.Combine(battleRuntimePath, "BattleAbilityTickCoordinator.cs");
        string actionPhaseCoordinatorPath = Path.Combine(battleRuntimePath, "BattleRuntimeActionPhaseCoordinator.cs");
        string[] tickResolverFiles = Directory.GetFiles(battleRuntimePath, "BattleRuntimeTickResolver*.cs")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        string heroResolverSource = string.Join(
            Environment.NewLine,
            Directory.GetFiles(battleRuntimePath, "BattleRuntimeHeroSkillCommandResolver*.cs")
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
        string combinedTickResolverSource = string.Join(Environment.NewLine, tickResolverFiles.Select(File.ReadAllText));

        AssertTrue(File.Exists(coordinatorPath), "Core Slice H19 should author BattleAbilityTickCoordinator");
        AssertTrue(File.Exists(actionPhaseCoordinatorPath), "Core Slice H20 should route ability ticking through the runtime action phase coordinator");
        AssertTrue(tickResolverFiles.Length > 0, "BattleRuntimeTickResolver partial files should exist");

        string coordinatorSource = File.ReadAllText(coordinatorPath);
        string actionPhaseCoordinatorSource = File.ReadAllText(actionPhaseCoordinatorPath);
        string coordinatorRelativePath = ToRepoPath(root, coordinatorPath);
        string actionPhaseCoordinatorRelativePath = ToRepoPath(root, actionPhaseCoordinatorPath);
        string heroResolverRelativePath = "src/Runtime/Battle/BattleRuntimeHeroSkillCommandResolver*.cs";

        AssertContains(coordinatorSource, "class BattleAbilityTickCoordinator", coordinatorRelativePath, "ability tick coordinator should be an explicit runtime service");
        AssertContains(coordinatorSource, "ResolvePending", coordinatorRelativePath, "ability tick coordinator should expose the tick-phase pending ability entry");
        AssertContains(coordinatorSource, "AdvanceActiveAbilityControllers", coordinatorRelativePath, "ability tick coordinator should own active skill advancement ordering");
        AssertContains(coordinatorSource, "AdvanceActiveChannelControllers", coordinatorRelativePath, "ability tick coordinator should own active channel cadence ordering");
        AssertContains(coordinatorSource, "BattleAbilityController.ResolvePendingSkillOrders", coordinatorRelativePath, "ability tick coordinator should delegate pending orders to actor-local controllers");
        AssertContains(actionPhaseCoordinatorSource, "BattleAbilityTickCoordinator.ResolvePending", actionPhaseCoordinatorRelativePath, "action phase coordinator should enter ability ticking through the ability coordinator");
        AssertContains(combinedTickResolverSource, "BattleRuntimeActionPhaseCoordinator.AdvanceActionPhase", "BattleRuntimeTickResolver*.cs", "tick resolver should enter ability ticking through the action phase coordinator");
        foreach (string tickResolverFile in tickResolverFiles)
        {
            string tickResolverSource = File.ReadAllText(tickResolverFile);
            AssertDoesNotContain(tickResolverSource, "BattleAbilityTickCoordinator.ResolvePending", ToRepoPath(root, tickResolverFile), "tick resolver partials must not bypass the action phase coordinator for ability ticking after H20");
        }

        AssertDoesNotContain(heroResolverSource, "internal static HashSet<string> ResolvePending", heroResolverRelativePath, "hero skill command resolver must not own runtime ability ticking");
        AssertDoesNotContain(heroResolverSource, "AdvanceActiveAbilityControllers", heroResolverRelativePath, "hero skill command resolver must not own active skill advancement ordering");
        AssertDoesNotContain(heroResolverSource, "AdvanceActiveChannelControllers", heroResolverRelativePath, "hero skill command resolver must not own active channel cadence ordering");
        AssertDoesNotContain(heroResolverSource, "BattleAbilityController.ResolvePendingSkillOrders", heroResolverRelativePath, "hero skill command resolver must not dispatch pending orders after H19");
        AssertDoesNotContain(heroResolverSource, "CommitEffectDeliveries", heroResolverRelativePath, "hero skill command resolver must not own ability-effect commit barriers");
    }

    private static void RuntimeDelayedCellSkillPreservesLockedTargetPayload()
    {
        const string battleId = "battle_delayed_cell_skill_payload";
        const string commandId = "cmd_delayed_cell_mark";
        const string skillId = "test_delayed_cell_mark";
        BattleStartSnapshot snapshot = BuildOpposedSnapshot(battleId);
        snapshot.SkillDefinitions.Add(new BattleSkillSnapshot
        {
            SkillId = skillId,
            DisplayName = "Delayed Cell Mark",
            TargetingMode = BattleSkillTargetingMode.TargetedCell,
            Range = 8,
            CasterUnitIds = { "hero_def_player" },
            CastSeconds = 0,
            ImpactDelaySeconds = 0.3,
            RecoverySeconds = 0,
            HasInterruptPolicy = true,
            CanInterruptBasicAttackWindup = true,
            Effects =
            {
                new BattleSkillEffectSnapshot
                {
                    Kind = BattleSkillEffectKind.CreateThunderMark
                }
            }
        });
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);

        BattleRuntimeCommandSubmitResult submit = controller.SubmitCommand(new CommandRequest
        {
            CommandId = commandId,
            BattleId = battleId,
            BattleGroupId = "group_player",
            Channel = CommandChannel.Hero,
            Kind = CommandKind.CastSkill,
            SkillId = skillId,
            HasTargetGrid = true,
            TargetGridX = 3,
            TargetGridY = 2,
            TargetGridHeight = 0
        });
        AssertTrue(submit.Accepted, "delayed targeted-cell skill should be accepted before release");

        BattleRuntimeAdvanceResult start = controller.AdvanceFixedTick(0.1);
        AssertTrue(
            start.Events.Any(item => item.Kind == BattleEventKind.SkillUsed && item.SourceCommandId == commandId),
            "first advance should start the delayed skill action");
        AssertTrue(
            start.Events.All(item => item.Kind != BattleEventKind.ThunderMarkCreated),
            "delayed targeted-cell skill should not create the mark before impact time");

        _ = controller.AdvanceFixedTick(0.2);
        BattleRuntimeAdvanceResult impact = controller.AdvanceFixedTick(0.1);
        BattleEvent? mark = impact.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.ThunderMarkCreated &&
            item.SourceCommandId == commandId);
        if (mark == null)
        {
            throw new Exception("delayed targeted-cell impact should create a thunder mark");
        }

        AssertEqual(3, mark.TargetGridX, "delayed cell mark X should use locked command payload");
        AssertEqual(2, mark.TargetGridY, "delayed cell mark Y should use locked command payload");
        AssertEqual(0, mark.TargetGridHeight, "delayed cell mark height should use locked command payload");
    }

    private static void RuntimePendingAbilityOrderConsumesOneActionPerActorTick()
    {
        const string battleId = "battle_pending_ability_one_action";
        BattleStartSnapshot snapshot = BuildOpposedSnapshot(battleId);
        AddDamageSkill(snapshot, "active_skill", damage: 1, castSeconds: 0.2, recoverySeconds: 0.2);
        AddDamageSkill(snapshot, "queued_one", damage: 5, castSeconds: 0, recoverySeconds: 0);
        AddDamageSkill(snapshot, "queued_two", damage: 7, castSeconds: 0, recoverySeconds: 0);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);

        BattleRuntimeCommandSubmitResult active = SubmitTargetedSkill(controller, battleId, "cmd_active_skill", "active_skill");
        AssertTrue(active.Accepted, "active setup skill should be accepted");
        _ = controller.AdvanceFixedTick(0.1);

        BattleRuntimeCommandSubmitResult firstQueued = SubmitTargetedSkill(controller, battleId, "cmd_queued_one", "queued_one");
        BattleRuntimeCommandSubmitResult secondQueued = SubmitTargetedSkill(controller, battleId, "cmd_queued_two", "queued_two");
        AssertTrue(firstQueued.Accepted && secondQueued.Accepted, "active caster should queue follow-up pending ability orders");

        _ = controller.AdvanceFixedTick(0.2);
        _ = controller.AdvanceFixedTick(0.2);
        BattleRuntimeAdvanceResult firstReleaseTick = controller.AdvanceFixedTick(0.2);

        AssertTrue(
            firstReleaseTick.Events.Any(item =>
                item.Kind == BattleEventKind.SkillUsed &&
                item.SourceCommandId == "cmd_queued_one"),
            "first queued skill should release after the active skill recovery boundary");
        AssertTrue(
            firstReleaseTick.Events.All(item =>
                item.SourceCommandId != "cmd_queued_two" ||
                item.Kind != BattleEventKind.SkillUsed),
            "one actor tick should not release a second action-consuming pending skill");

        BattleRuntimeAdvanceResult secondReleaseTick = controller.AdvanceFixedTick(0.2);
        AssertTrue(
            secondReleaseTick.Events.Any(item =>
                item.Kind == BattleEventKind.SkillUsed &&
                item.SourceCommandId == "cmd_queued_two"),
            "second queued skill should release on a later runtime tick");
    }

    private static void RuntimePendingAbilityFailurePreservesCommandAttribution()
    {
        const string missingSkillBattleId = "battle_pending_ability_missing_skill";
        BattleStartSnapshot missingSkillSnapshot = BuildOpposedSnapshot(missingSkillBattleId);
        AddDamageSkill(missingSkillSnapshot, "transient_skill", damage: 4, castSeconds: 0, recoverySeconds: 0);
        BattleRuntimeSessionController missingSkillController = new BattleRuntimeSession().Begin(missingSkillSnapshot);
        BattleRuntimeCommandSubmitResult acceptedThenMissing = SubmitTargetedSkill(
            missingSkillController,
            missingSkillBattleId,
            "cmd_transient_skill",
            "transient_skill");
        AssertTrue(acceptedThenMissing.Accepted, "transient skill should be accepted before its definition disappears");
        missingSkillController.State.SkillDefinitions.Clear();

        BattleRuntimeAdvanceResult missingSkillAdvance = missingSkillController.AdvanceFixedTick();
        BattleEvent? missingSkillFailure = missingSkillAdvance.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.CommandFailed &&
            item.SourceCommandId == "cmd_transient_skill");
        AssertTrue(missingSkillFailure != null, "missing skill definition should fail the queued order");
        AssertEqual("skill_definition_missing_before_release", missingSkillFailure!.ReasonCode, "missing skill failure reason");
        AssertEqual("group_player:hero", missingSkillFailure.ActorId, "missing skill failure actor attribution");
        AssertEqual("group_player", missingSkillFailure.BattleGroupId, "missing skill failure group attribution");

        const string deadCasterBattleId = "battle_pending_ability_dead_caster";
        BattleStartSnapshot deadCasterSnapshot = BuildOpposedSnapshot(deadCasterBattleId);
        AddDamageSkill(deadCasterSnapshot, "dead_caster_skill", damage: 4, castSeconds: 0, recoverySeconds: 0);
        BattleRuntimeSessionController deadCasterController = new BattleRuntimeSession().Begin(deadCasterSnapshot);
        BattleRuntimeCommandSubmitResult acceptedThenDead = SubmitTargetedSkill(
            deadCasterController,
            deadCasterBattleId,
            "cmd_dead_caster_skill",
            "dead_caster_skill");
        AssertTrue(acceptedThenDead.Accepted, "dead-caster setup skill should be accepted before caster defeat");
        deadCasterController.State.Actors.Single(item => item.ActorId == "group_player:hero").HitPoints = 0;

        BattleRuntimeAdvanceResult deadCasterAdvance = deadCasterController.AdvanceFixedTick();
        BattleEvent? deadCasterFailure = deadCasterAdvance.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.CommandFailed &&
            item.SourceCommandId == "cmd_dead_caster_skill");
        AssertTrue(deadCasterFailure != null, "dead caster should fail the queued order");
        AssertEqual("skill_caster_invalid_before_release", deadCasterFailure!.ReasonCode, "dead caster failure reason");
        AssertEqual("group_player:hero", deadCasterFailure.ActorId, "dead caster failure actor attribution");
        AssertEqual("group_player", deadCasterFailure.BattleGroupId, "dead caster failure group attribution");
    }

    private static void RuntimeActiveOneUseSkillRejectsDuplicateQueuedOrder()
    {
        const string battleId = "battle_active_one_use_skill_duplicate";
        const string skillId = "one_use_active_skill";
        BattleStartSnapshot snapshot = BuildOpposedSnapshot(battleId);
        AddDamageSkill(snapshot, skillId, damage: 4, castSeconds: 0.4, recoverySeconds: 0);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);

        BattleRuntimeCommandSubmitResult first = SubmitTargetedSkill(
            controller,
            battleId,
            "cmd_one_use_active_first",
            skillId);
        AssertTrue(first.Accepted, "first one-use skill should be accepted");
        BattleRuntimeAdvanceResult start = controller.AdvanceFixedTick(0.1);
        AssertTrue(
            start.Events.Any(item =>
                item.Kind == BattleEventKind.SkillUsed &&
                item.SourceCommandId == "cmd_one_use_active_first"),
            "first one-use skill should enter active casting before the duplicate submission");

        BattleRuntimeCommandSubmitResult duplicate = SubmitTargetedSkill(
            controller,
            battleId,
            "cmd_one_use_active_duplicate",
            skillId);

        AssertTrue(!duplicate.Accepted, "duplicate one-use skill should reject while the first release is active or queued");
        AssertTrue(
            duplicate.Events.Any(item =>
                item.Kind == BattleEventKind.CommandRejected &&
                item.SourceCommandId == "cmd_one_use_active_duplicate" &&
                item.SourceDefinitionId == skillId &&
                item.ReasonCode == "hero_skill_already_queued"),
            "duplicate one-use skill rejection should enter the event stream with command attribution");

        _ = controller.AdvanceFixedTick(0.4);
        _ = controller.AdvanceFixedTick(0.4);
        int duplicateUses = controller.EventStream.Events.Count(item =>
            item.Kind == BattleEventKind.SkillUsed &&
            item.SourceCommandId == "cmd_one_use_active_duplicate");
        AssertEqual(0, duplicateUses, "rejected duplicate one-use skill must never release later");
    }

    private static BattleStartSnapshot BuildOpposedSnapshot(string battleId)
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
                    CorpsStrength = 40,
                    SourceLocationId = "site_1",
                    CellX = 6,
                    CellY = 0
                }
            }
        };
        TargetBattleTestTopology.CompileAroundGroups(snapshot);
        return snapshot;
    }

    private static void AddDamageSkill(
        BattleStartSnapshot snapshot,
        string skillId,
        int damage,
        double castSeconds,
        double recoverySeconds)
    {
        snapshot.SkillDefinitions.Add(new BattleSkillSnapshot
        {
            SkillId = skillId,
            DisplayName = skillId,
            TargetingMode = BattleSkillTargetingMode.TargetedActor,
            Range = 8,
            CasterUnitIds = { "hero_def_player" },
            CastSeconds = castSeconds,
            ImpactDelaySeconds = 0,
            RecoverySeconds = recoverySeconds,
            HasInterruptPolicy = true,
            CanInterruptBasicAttackWindup = true,
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

    private static BattleRuntimeCommandSubmitResult SubmitTargetedSkill(
        BattleRuntimeSessionController controller,
        string battleId,
        string commandId,
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
            TargetActorId = "force_enemy:1"
        });
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

    private static void AssertContains(string source, string expected, string relativePath, string message)
    {
        AssertTrue(source.Contains(expected, StringComparison.Ordinal), $"{message}: file={relativePath} expected={expected}");
    }

    private static void AssertDoesNotContain(string source, string forbidden, string relativePath, string message)
    {
        AssertTrue(!source.Contains(forbidden, StringComparison.Ordinal), $"{message}: file={relativePath} forbidden={forbidden}");
    }

    private static void AssertNoActiveChannelContainerOutsideActor(string root, string actorPath)
    {
        Regex durableChannelContainer = new(
            @"\b(?:private|internal|public)\s+(?:readonly\s+)?(?:List|IList|IReadOnlyList|ICollection|HashSet)<BattleRuntimeActiveChannel>\s+\w+",
            RegexOptions.CultureInvariant);
        foreach (string path in Directory.GetFiles(Path.Combine(root, "src", "Runtime", "Battle"), "*.cs", SearchOption.AllDirectories)
                     .Where(path => !string.Equals(Path.GetFullPath(path), Path.GetFullPath(actorPath), StringComparison.OrdinalIgnoreCase)))
        {
            string source = File.ReadAllText(path);
            AssertTrue(
                !durableChannelContainer.IsMatch(source),
                $"active channel durable containers must stay actor-owned: file={ToRepoPath(root, path)}");
        }
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
        if (!Equals(expected, actual))
        {
            throw new Exception($"{message}: expected={expected} actual={actual}");
        }
    }
}
