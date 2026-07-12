using System;
using System.IO;
using System.Linq;
using Rpg.Application.Battle.Commands;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;

internal static class TargetBattleRegroupRetreatRegressionCases
{
    private const string BattleId = "battle_regroup_retreat";
    private const string PlayerGroupId = "player_group";
    private const string EnemyGroupId = "enemy_group";

    internal static void Register(Action<string, Action> run)
    {
        run("regroup and retreat use production tactical command path", RegroupAndRetreatUseProductionTacticalCommandPath);
        run("regroup converges scattered commander members and completes", RegroupConvergesScatteredCommanderMembersAndCompletes);
        run("retreat completes with player retreat termination", RetreatCompletesWithPlayerRetreatTermination);
        run("regroup and retreat reject invalid requests without mutation", RegroupAndRetreatRejectInvalidRequestsWithoutMutation);
        run("regroup and retreat hud is authored and submits through Application", RegroupAndRetreatHudIsAuthoredAndSubmitsThroughApplication);
    }

    private static void RegroupConvergesScatteredCommanderMembersAndCompletes()
    {
        BattleStartSnapshot snapshot = BuildSnapshot();
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        BattleRuntimeActor first = controller.State.Actors.Single(actor =>
            actor.Kind == BattleRuntimeActorKind.Corps && actor.BattleGroupId == PlayerGroupId);
        first.TargetActorId = "enemy_force:1";
        controller.State.Actors.Add(new BattleRuntimeActor
        {
            ActorId = "player_force:2",
            BattleGroupId = PlayerGroupId,
            FactionId = "player",
            SourceForceId = "player_force",
            Kind = BattleRuntimeActorKind.Corps,
            HitPoints = 100,
            GridX = 3,
            GridY = 0,
            GridHeight = 0,
            Position = 3,
            AttackDamage = 5,
            AttackRange = 1,
            MoveStepSeconds = 0.16
        });
        controller.State.Actors.Single(actor => actor.ActorId == "player_force:2").TargetActorId = "enemy_force:1";

        BattleCommandSubmissionResult regroup = Submit(snapshot, controller, CommandKind.Regroup, "regroup_scattered");
        AssertTrue(regroup.Accepted, $"scattered regroup should be accepted: {regroup.ReasonCode}");
        AssertTrue(controller.State.Actors
                .Where(actor => actor.BattleGroupId == PlayerGroupId)
                .All(actor => string.IsNullOrWhiteSpace(actor.TargetActorId)),
            "regroup acceptance should immediately clear incompatible local target assignments");
        for (int index = 0; index < 8 && !controller.EventStream.Events.Any(item =>
                 item.Kind == BattleEventKind.CommandCompleted && item.SourceCommandId == "regroup_scattered"); index++)
        {
            controller.AdvanceFixedTick(0.2);
        }

        BattleRuntimeActor second = controller.State.Actors.Single(actor => actor.ActorId == "player_force:2");
        AssertTrue(second.GridX < 3, "regroup should visibly move a scattered member toward the group rally target");
        AssertTrue(controller.EventStream.Events.Any(item =>
                item.Kind == BattleEventKind.CommandCompleted &&
                item.SourceCommandId == "regroup_scattered" &&
                item.BattleGroupId == PlayerGroupId),
            "regroup should emit an attributable completion event");
    }

    private static void RetreatCompletesWithPlayerRetreatTermination()
    {
        BattleStartSnapshot snapshot = BuildSnapshot();
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        BattleCommandSubmissionResult retreat = Submit(snapshot, controller, CommandKind.Retreat, "retreat_complete");
        AssertTrue(retreat.Accepted, $"retreat should be accepted: {retreat.ReasonCode}");

        BattleRuntimeAdvanceResult advance = controller.AdvanceFixedTick(0.2);

        AssertTrue(advance.IsComplete, "all deployed player groups completing retreat should terminate battle");
        AssertEqual(BattleTerminationReason.PlayerRetreat, advance.Outcome.TerminationReason,
            "retreat must not fabricate victory or defeat");
        AssertTrue(controller.State.Actors.Any(actor =>
                actor.BattleGroupId == PlayerGroupId && actor.Kind == BattleRuntimeActorKind.Corps && actor.HasRetreated && actor.HitPoints > 0),
            "retreated corps should remain alive while leaving active combat");
        AssertTrue(controller.EventStream.Events.Any(item =>
                item.Kind == BattleEventKind.CommandCompleted && item.SourceCommandId == "retreat_complete"),
            "retreat completion should remain report-attributable");
    }

    private static void RegroupAndRetreatRejectInvalidRequestsWithoutMutation()
    {
        BattleStartSnapshot snapshot = BuildSnapshot();
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        int eventCount = controller.EventStream.Events.Count;
        string commandBefore = controller.State.TacticalStates[PlayerGroupId].ActiveCommandId;

        CommandRequest missingSelection = new()
        {
            CommandId = "missing_selection",
            BattleId = BattleId,
            Channel = CommandChannel.Combined,
            Kind = CommandKind.Regroup
        };
        BattleCommandSubmissionResult missing = new BattleCommandSubmissionService().Submit(snapshot, "player", missingSelection, controller);
        AssertFalse(missing.Accepted, "missing selection should reject");
        AssertEqual("battle_group_unavailable", missing.ReasonCode, "missing selection reason");

        CommandRequest wrongChannel = new()
        {
            CommandId = "wrong_channel",
            BattleId = BattleId,
            BattleGroupId = PlayerGroupId,
            Channel = CommandChannel.Corps,
            Kind = CommandKind.Retreat
        };
        BattleCommandSubmissionResult channel = new BattleCommandSubmissionService().Submit(snapshot, "player", wrongChannel, controller);
        AssertFalse(channel.Accepted, "non-combined retreat should reject");
        AssertEqual("command_channel_unavailable", channel.ReasonCode, "wrong channel reason");

        CommandRequest enemyGroup = new()
        {
            CommandId = "enemy_group",
            BattleId = BattleId,
            BattleGroupId = EnemyGroupId,
            Channel = CommandChannel.Combined,
            Kind = CommandKind.Regroup
        };
        BattleCommandSubmissionResult enemy = new BattleCommandSubmissionService().Submit(snapshot, "player", enemyGroup, controller);
        AssertFalse(enemy.Accepted, "enemy tactical command should reject at Application");
        AssertEqual("battle_group_not_owned", enemy.ReasonCode, "enemy ownership reason");

        CommandRequest staleBattle = new()
        {
            CommandId = "stale_battle",
            BattleId = "other_battle",
            BattleGroupId = PlayerGroupId,
            Channel = CommandChannel.Combined,
            Kind = CommandKind.Retreat
        };
        BattleCommandSubmissionResult stale = new BattleCommandSubmissionService().Submit(snapshot, "player", staleBattle, controller);
        AssertFalse(stale.Accepted, "wrong battle tactical command should reject at Application");
        AssertEqual("battle_id_mismatch", stale.ReasonCode, "wrong battle reason");

        AssertEqual(eventCount, controller.EventStream.Events.Count,
            "Application rejection must not emit Runtime accepted or rejected events");
        AssertEqual(commandBefore, controller.State.TacticalStates[PlayerGroupId].ActiveCommandId,
            "Application rejection must not mutate commander intent");

        BattleStartSnapshot unreachableSnapshot = BuildSnapshot();
        unreachableSnapshot.LocationContext.NavigationTopology.Edges.Clear();
        BattleRuntimeSessionController unreachableController = new BattleRuntimeSession().Begin(unreachableSnapshot);
        BattleRuntimeActor player = unreachableController.State.Actors.Single(actor =>
            actor.Kind == BattleRuntimeActorKind.Corps && actor.BattleGroupId == PlayerGroupId);
        player.GridX = 2;
        player.Position = 2;
        BattleCommandSubmissionResult unreachable = Submit(
            unreachableSnapshot,
            unreachableController,
            CommandKind.Retreat,
            "retreat_unreachable");
        AssertFalse(unreachable.Accepted, "unreachable retreat target should reject atomically");
        AssertEqual("retreat_target_unreachable", unreachable.ReasonCode, "unreachable retreat reason");
        AssertFalse(unreachableController.State.TacticalStates[PlayerGroupId].HasActiveTacticalCommand,
            "unreachable rejection must not install commander intent");
        AssertTrue(unreachable.Events.Count == 1 && unreachable.Events[0].Kind == BattleEventKind.CommandRejected,
            "Runtime rejection should emit one attributable rejection fact");

        BattleStartSnapshot invalidRegroupSnapshot = BuildSnapshot();
        BattleRuntimeSessionController invalidRegroupController = new BattleRuntimeSession().Begin(invalidRegroupSnapshot);
        BattleRuntimeActor invalidRegroupActor = invalidRegroupController.State.Actors.Single(actor =>
            actor.Kind == BattleRuntimeActorKind.Corps && actor.BattleGroupId == PlayerGroupId);
        invalidRegroupActor.GridX = 999;
        BattleCommandSubmissionResult invalidRegroup = Submit(
            invalidRegroupSnapshot,
            invalidRegroupController,
            CommandKind.Regroup,
            "regroup_unreachable");
        AssertFalse(invalidRegroup.Accepted, "regroup without a legal rally target should reject");
        AssertEqual("regroup_target_unreachable", invalidRegroup.ReasonCode, "unreachable regroup reason");

        BattleStartSnapshot lockedSnapshot = BuildSnapshot();
        BattleRuntimeSessionController lockedController = new BattleRuntimeSession().Begin(lockedSnapshot);
        BattleRuntimeActor lockedActor = lockedController.State.Actors.Single(actor =>
            actor.Kind == BattleRuntimeActorKind.Corps && actor.BattleGroupId == PlayerGroupId);
        lockedActor.Phase = BattleRuntimeActorPhase.SkillRecovery;
        BattleCommandSubmissionResult locked = Submit(lockedSnapshot, lockedController, CommandKind.Retreat, "retreat_locked");
        AssertFalse(locked.Accepted, "retreat should reject while a selected member has an incompatible action lock");
        AssertEqual("tactical_command_action_locked", locked.ReasonCode, "action lock reason");
    }

    private static void RegroupAndRetreatUseProductionTacticalCommandPath()
    {
        BattleStartSnapshot regroupSnapshot = BuildSnapshot();
        BattleRuntimeSessionController regroupController = new BattleRuntimeSession().Begin(regroupSnapshot);
        BattleCommandSubmissionResult regroup = Submit(regroupSnapshot, regroupController, CommandKind.Regroup, "regroup_live");

        AssertTrue(regroup.Accepted, $"live regroup should be accepted: {regroup.ReasonCode}");
        AssertEqual(BattleGroupPlanRuntimeState.RegroupingOrReturningToObjective,
            regroupController.State.TacticalStates[PlayerGroupId].PlanState,
            "regroup should be owned by commander state");
        AssertTrue(regroup.Events.Any(item => item.Kind == BattleEventKind.CommandAccepted && item.BattleGroupId.Contains(PlayerGroupId, StringComparison.Ordinal)),
            "regroup acceptance should carry group identity");
        AssertTrue(regroupController.State.Actors.All(actor => string.IsNullOrWhiteSpace(actor.CurrentSkillSourceCommandId)),
            "ordinary tactical commands must not enter the hero skill resolver");

        BattleStartSnapshot retreatSnapshot = BuildSnapshot();
        BattleRuntimeSessionController retreatController = new BattleRuntimeSession().Begin(retreatSnapshot);
        retreatController.SetPaused(true, "regression");
        double pausedTime = retreatController.CurrentTimeSeconds;
        BattleCommandSubmissionResult retreat = Submit(retreatSnapshot, retreatController, CommandKind.Retreat, "retreat_paused");

        AssertTrue(retreat.Accepted, $"paused retreat should be accepted: {retreat.ReasonCode}");
        AssertEqual(pausedTime, retreatController.CurrentTimeSeconds, "pause-time command submission must not advance time");
        AssertEqual(BattleGroupPlanRuntimeState.Retreating,
            retreatController.State.TacticalStates[PlayerGroupId].PlanState,
            "retreat should be owned by commander state");
    }

    private static void RegroupAndRetreatHudIsAuthoredAndSubmitsThroughApplication()
    {
        string root = ProjectRoot();
        string scene = File.ReadAllText(Path.Combine(root, "scenes", "world", "ui", "WorldSitePeacetimeHud.tscn"));
        string hud = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRuntimeCommandHud.cs"));

        AssertTrue(scene.Contains("BattleRuntimeRegroupButton", StringComparison.Ordinal) &&
                   scene.Contains("BattleRuntimeRetreatButton", StringComparison.Ordinal) &&
                   scene.Contains("focus_mode = 2", StringComparison.Ordinal),
            "production HUD should author focusable regroup and retreat controls");
        AssertTrue(hud.Contains("SubmitBattleRuntimeTacticalCommand", StringComparison.Ordinal) &&
                   hud.Contains("new BattleCommandSubmissionService().Submit", StringComparison.Ordinal) &&
                   hud.Contains("Kind = commandKind", StringComparison.Ordinal) &&
                   hud.Contains("Channel = CommandChannel.Combined", StringComparison.Ordinal),
            "HUD clicks should submit combined tactical commands through Application");
    }

    private static BattleCommandSubmissionResult Submit(
        BattleStartSnapshot snapshot,
        BattleRuntimeSessionController controller,
        CommandKind kind,
        string commandId)
    {
        CommandRequest request = new()
        {
            CommandId = commandId,
            BattleId = BattleId,
            BattleGroupId = PlayerGroupId,
            Channel = CommandChannel.Combined,
            Kind = kind
        };
        request.BattleGroupIds.Add(PlayerGroupId);
        return new BattleCommandSubmissionService().Submit(snapshot, "player", request, controller);
    }

    private static BattleStartSnapshot BuildSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_regroup_retreat",
            BattleId = BattleId,
            BattleGroups =
            {
                new BattleGroupSnapshot
                {
                    BattleGroupId = PlayerGroupId,
                    RuntimeCommanderGroupId = PlayerGroupId,
                    FactionId = "player",
                    SourceForceId = "player_force",
                    HeroId = "player_hero",
                    HeroDefinitionId = "player_hero_definition",
                    CorpsId = "player_corps",
                    CorpsDefinitionId = "player_corps_definition",
                    SourceLocationId = "player_city",
                    CorpsStrength = 100,
                    CellX = 0,
                    CellY = 0,
                    MaxHitPoints = 100,
                    AttackDamage = 5
                },
                new BattleGroupSnapshot
                {
                    BattleGroupId = "enemy_group",
                    RuntimeCommanderGroupId = "enemy_group",
                    FactionId = "enemy",
                    SourceForceId = "enemy_force",
                    HeroId = "enemy_hero",
                    HeroDefinitionId = "enemy_hero_definition",
                    CorpsId = "enemy_corps",
                    CorpsDefinitionId = "enemy_corps_definition",
                    SourceLocationId = "enemy_site",
                    CorpsStrength = 100,
                    CellX = 5,
                    CellY = 0,
                    MaxHitPoints = 100,
                    AttackDamage = 5
                }
            }
        };
        TargetBattleTestTopology.CompileAroundGroups(snapshot, margin: 2);
        return snapshot;
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

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertFalse(bool condition, string message) => AssertTrue(!condition, message);

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message}: expected={expected} actual={actual}");
        }
    }
}
