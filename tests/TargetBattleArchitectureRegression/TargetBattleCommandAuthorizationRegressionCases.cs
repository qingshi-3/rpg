using System;
using System.Linq;
using Rpg.Application.Battle.Commands;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;

internal static class TargetBattleCommandAuthorizationRegressionCases
{
    private const string BattleId = "battle_command_authorization";
    private const string PlayerGroupId = "player_group";
    private const string EnemyGroupId = "enemy_group";
    private const string PlayerHeroActorId = "player_group:hero";
    private const string EnemyHeroActorId = "enemy_group:hero";
    private const string PlayerCorpsActorId = "player_force:1";
    private const string EnemyCorpsActorId = "enemy_force:1";
    private const string PlayerSkillId = "player_authorized_skill";

    internal static void Register(Action<string, Action> run)
    {
        run("application command boundary rejects enemy ownership without runtime mutation", ApplicationBoundaryRejectsEnemyOwnershipWithoutRuntimeMutation);
        run("application command boundary accepts valid player skill and beacon intents", ApplicationBoundaryAcceptsValidPlayerSkillAndBeaconIntents);
        run("application command boundary rejects forged stale unavailable and wrong channel intents", ApplicationBoundaryRejectsForgedStaleUnavailableAndWrongChannelIntents);
        run("runtime command boundary rejects enemy forged stale unavailable and wrong channel intents", RuntimeBoundaryRejectsEnemyForgedStaleUnavailableAndWrongChannelIntents);
    }

    private static void ApplicationBoundaryRejectsEnemyOwnershipWithoutRuntimeMutation()
    {
        BattleStartSnapshot snapshot = BuildSnapshot();
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        CommandRequest request = BuildSkillRequest(EnemyGroupId, EnemyHeroActorId, PlayerSkillId, CommandChannel.Hero);
        int eventCount = controller.EventStream.Events.Count;
        string[] commandIds = controller.State.Actors.Select(actor => actor.CommandId).ToArray();
        int beaconCount = controller.State.DestinationBeacons.Count;

        BattleCommandSubmissionResult result = new BattleCommandSubmissionService().Submit(
            snapshot,
            "player",
            request,
            controller);

        AssertFalse(result.Accepted, "enemy-controlled group should fail at Application");
        AssertEqual(CommandRejectionStage.Application, result.RejectionStage, "enemy rejection stage");
        AssertEqual("battle_group_not_owned", result.ReasonCode, "enemy rejection reason");
        AssertEqual(eventCount, controller.EventStream.Events.Count, "Application rejection must not call Runtime or append events");
        AssertEqual(beaconCount, controller.State.DestinationBeacons.Count, "Application rejection must not create a beacon");
        AssertSequence(commandIds, controller.State.Actors.Select(actor => actor.CommandId).ToArray(), "Application rejection must not mutate actor command state");
        AssertTrue(controller.State.Actors.All(actor => string.IsNullOrWhiteSpace(actor.CurrentSkillSourceCommandId)), "Application rejection must not start or queue visible skill state");
    }

    private static void ApplicationBoundaryAcceptsValidPlayerSkillAndBeaconIntents()
    {
        BattleStartSnapshot skillSnapshot = BuildSnapshot();
        BattleRuntimeSessionController liveController = new BattleRuntimeSession().Begin(skillSnapshot);
        BattleCommandSubmissionResult liveSkill = new BattleCommandSubmissionService().Submit(
            skillSnapshot,
            "player",
            BuildSkillRequest(PlayerGroupId, PlayerHeroActorId, PlayerSkillId, CommandChannel.Hero),
            liveController);

        AssertTrue(
            liveSkill.Accepted,
            $"valid live player skill should reach Runtime reason={liveSkill.ReasonCode} initial={DescribeEvents(liveController)}");
        AssertEqual(CommandRejectionStage.None, liveSkill.RejectionStage, "valid skill rejection stage");
        AssertTrue(liveSkill.Events.Any(item => item.Kind == BattleEventKind.CommandAccepted), "valid skill should emit Runtime acceptance");

        BattleStartSnapshot pauseSnapshot = BuildSnapshot();
        BattleRuntimeSessionController pauseController = new BattleRuntimeSession().Begin(pauseSnapshot);
        pauseController.SetPaused(true, "authorization_test");
        double pausedTime = pauseController.CurrentTimeSeconds;
        BattleCommandSubmissionResult pausedSkill = new BattleCommandSubmissionService().Submit(
            pauseSnapshot,
            "player",
            BuildSkillRequest(PlayerGroupId, PlayerHeroActorId, PlayerSkillId, CommandChannel.Hero),
            pauseController);

        AssertTrue(pausedSkill.Accepted, $"valid tactical-pause skill should reach Runtime reason={pausedSkill.ReasonCode}");
        AssertEqual(pausedTime, pauseController.CurrentTimeSeconds, "pause-time submission must not advance Runtime time");
        AssertTrue(pauseController.IsPaused, "pause-time submission must preserve tactical pause");

        BattleStartSnapshot beaconSnapshot = BuildSnapshot();
        BattleRuntimeSessionController beaconController = new BattleRuntimeSession().Begin(beaconSnapshot);
        CommandRequest beaconRequest = new()
        {
            CommandId = "authorized_beacon",
            BattleId = BattleId,
            BattleGroupId = PlayerGroupId,
            BattleGroupIds = { PlayerGroupId },
            Channel = CommandChannel.Combined,
            Kind = CommandKind.DestinationBeacon,
            HasTargetGrid = true,
            TargetGridX = 1,
            TargetGridY = 0,
            TargetGridHeight = 0
        };
        BattleCommandSubmissionResult beacon = new BattleCommandSubmissionService().Submit(
            beaconSnapshot,
            "player",
            beaconRequest,
            beaconController);

        AssertTrue(beacon.Accepted, $"valid destination beacon should reach Runtime reason={beacon.ReasonCode}");
        AssertEqual(1, beaconController.State.DestinationBeacons.Count, "valid beacon should preserve existing Runtime behavior");
    }

    private static void ApplicationBoundaryRejectsForgedStaleUnavailableAndWrongChannelIntents()
    {
        AssertApplicationReject(
            BuildSkillRequest(PlayerGroupId, EnemyHeroActorId, PlayerSkillId, CommandChannel.Hero),
            "source_actor_unavailable");

        CommandRequest wrongBattle = BuildSkillRequest(PlayerGroupId, PlayerHeroActorId, PlayerSkillId, CommandChannel.Hero);
        wrongBattle.BattleId = "stale_battle";
        AssertApplicationReject(wrongBattle, "battle_id_mismatch");

        AssertApplicationReject(
            BuildSkillRequest("reserve_group", "reserve_group:hero", PlayerSkillId, CommandChannel.Hero),
            "battle_group_unavailable");
        AssertApplicationReject(
            BuildSkillRequest(PlayerGroupId, PlayerHeroActorId, "unknown_skill", CommandChannel.Hero),
            "skill_definition_missing");
        AssertApplicationReject(
            BuildSkillRequest(PlayerGroupId, PlayerHeroActorId, PlayerSkillId, CommandChannel.Corps),
            "skill_command_channel_mismatch");
    }

    private static void RuntimeBoundaryRejectsEnemyForgedStaleUnavailableAndWrongChannelIntents()
    {
        AssertRuntimeReject(
            BuildSkillRequest(EnemyGroupId, EnemyHeroActorId, PlayerSkillId, CommandChannel.Hero),
            "battle_group_not_player_controlled");
        AssertRuntimeReject(
            BuildSkillRequest(PlayerGroupId, EnemyHeroActorId, PlayerSkillId, CommandChannel.Hero),
            "skill_caster_invalid");

        CommandRequest wrongBattle = BuildSkillRequest(PlayerGroupId, PlayerHeroActorId, PlayerSkillId, CommandChannel.Hero);
        wrongBattle.BattleId = "stale_battle";
        AssertRuntimeReject(wrongBattle, "battle_id_mismatch");

        AssertRuntimeReject(
            BuildSkillRequest("reserve_group", "reserve_group:hero", PlayerSkillId, CommandChannel.Hero),
            "battle_group_unavailable");
        AssertRuntimeReject(
            BuildSkillRequest(PlayerGroupId, PlayerHeroActorId, "unknown_skill", CommandChannel.Hero),
            "skill_definition_missing");
        AssertRuntimeReject(
            BuildSkillRequest(PlayerGroupId, PlayerHeroActorId, PlayerSkillId, CommandChannel.Corps),
            "skill_command_channel_mismatch");
    }

    private static void AssertApplicationReject(CommandRequest request, string expectedReason)
    {
        BattleStartSnapshot snapshot = BuildSnapshot();
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        int eventCount = controller.EventStream.Events.Count;
        int beaconCount = controller.State.DestinationBeacons.Count;
        string[] commandIds = controller.State.Actors.Select(actor => actor.CommandId).ToArray();

        BattleCommandSubmissionResult result = new BattleCommandSubmissionService().Submit(snapshot, "player", request, controller);

        AssertFalse(result.Accepted, $"Application should reject {expectedReason}");
        AssertEqual(CommandRejectionStage.Application, result.RejectionStage, $"Application stage for {expectedReason}");
        AssertEqual(expectedReason, result.ReasonCode, $"Application reason for {expectedReason}");
        AssertEqual(eventCount, controller.EventStream.Events.Count, $"Application rejection {expectedReason} must not call Runtime");
        AssertEqual(beaconCount, controller.State.DestinationBeacons.Count, $"Application rejection {expectedReason} must not mutate beacons");
        AssertSequence(commandIds, controller.State.Actors.Select(actor => actor.CommandId).ToArray(), $"Application rejection {expectedReason} must not mutate commands");
    }

    private static void AssertRuntimeReject(CommandRequest request, string expectedReason)
    {
        BattleStartSnapshot snapshot = BuildSnapshot();
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        int eventCount = controller.EventStream.Events.Count;
        int beaconCount = controller.State.DestinationBeacons.Count;
        string[] commandIds = controller.State.Actors.Select(actor => actor.CommandId).ToArray();

        BattleRuntimeCommandSubmitResult result = controller.SubmitCommand(request);

        AssertFalse(result.Accepted, $"Runtime should reject {expectedReason}");
        AssertEqual(expectedReason, result.ReasonCode, $"Runtime reason for {expectedReason}");
        AssertEqual(eventCount + 1, controller.EventStream.Events.Count, $"Runtime rejection {expectedReason} should append one diagnostic event");
        AssertTrue(result.Events.Count == 1 && result.Events[0].Kind == BattleEventKind.CommandRejected, $"Runtime rejection {expectedReason} should return one rejection event");
        AssertEqual(beaconCount, controller.State.DestinationBeacons.Count, $"Runtime rejection {expectedReason} must not mutate beacons");
        AssertSequence(commandIds, controller.State.Actors.Select(actor => actor.CommandId).ToArray(), $"Runtime rejection {expectedReason} must not mutate commands");
        AssertTrue(controller.State.Actors.All(actor => string.IsNullOrWhiteSpace(actor.CurrentSkillSourceCommandId)), $"Runtime rejection {expectedReason} must not start skill state");
    }

    private static CommandRequest BuildSkillRequest(
        string groupId,
        string sourceActorId,
        string skillDefinitionId,
        CommandChannel channel)
    {
        return new CommandRequest
        {
            CommandId = $"authorization:{groupId}:{skillDefinitionId}:{channel}",
            BattleId = BattleId,
            BattleGroupId = groupId,
            SourceActorId = sourceActorId,
            Channel = channel,
            Kind = CommandKind.CastSkill,
            SkillDefinitionId = skillDefinitionId,
            TargetActorId = string.Equals(groupId, EnemyGroupId, StringComparison.Ordinal)
                ? PlayerCorpsActorId
                : EnemyCorpsActorId
        };
    }

    private static BattleStartSnapshot BuildSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_command_authorization",
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
                    CorpsStrength = 100,
                    SourceLocationId = "player_city",
                    CellX = 0,
                    CellY = 0,
                    MaxHitPoints = 100,
                    AttackDamage = 5
                },
                new BattleGroupSnapshot
                {
                    BattleGroupId = EnemyGroupId,
                    RuntimeCommanderGroupId = EnemyGroupId,
                    FactionId = "enemy",
                    SourceForceId = "enemy_force",
                    HeroId = "enemy_hero",
                    HeroDefinitionId = "enemy_hero_definition",
                    CorpsId = "enemy_corps",
                    CorpsDefinitionId = "enemy_corps_definition",
                    CorpsStrength = 100,
                    SourceLocationId = "enemy_site",
                    CellX = 3,
                    CellY = 0,
                    MaxHitPoints = 100,
                    AttackDamage = 5
                }
            },
            SkillDefinitions =
            {
                new BattleSkillSnapshot
                {
                    SkillDefinitionId = PlayerSkillId,
                    GrantedSkillId = "grant_player_authorized_skill",
                    LoadoutSlotId = "slot_player_authorized_skill",
                    OwnerHeroId = "player_hero",
                    OwnerBattleGroupId = PlayerGroupId,
                    RuntimeCommanderGroupId = PlayerGroupId,
                    CommandChannel = BattleSkillCommandChannel.Hero,
                    SkillType = BattleSkillType.Active,
                    TargetingMode = BattleSkillTargetingMode.TargetedActor,
                    Range = 8,
                    CasterUnitIds = { "player_hero_definition" },
                    CastSeconds = 0,
                    ImpactDelaySeconds = 0,
                    RecoverySeconds = 0.2,
                    HasInterruptPolicy = true,
                    CanInterruptBasicAttackWindup = true,
                    CanCancelBasicAttackRecovery = false,
                    Costs = { new LimitedUseSkillCostSnapshot { MaxUses = 1 } },
                    Effects = { new DamageSkillEffectSnapshot { BaseDamage = 1 } }
                }
            }
        };
        TargetBattleTestTopology.CompileAroundGroups(snapshot, margin: 2);
        return snapshot;
    }

    private static void AssertTrue(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertFalse(bool value, string message) => AssertTrue(!value, message);

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message}: expected={expected} actual={actual}");
        }
    }

    private static void AssertSequence(string[] expected, string[] actual, string message)
    {
        if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"{message}: expected={string.Join("|", expected)} actual={string.Join("|", actual)}");
        }
    }

    private static string DescribeEvents(BattleRuntimeSessionController controller)
    {
        return string.Join(
            "|",
            controller?.EventStream?.Events?.Select(item => $"{item.Kind}:{item.ReasonCode}:{item.SourceDefinitionId}") ??
            Array.Empty<string>());
    }
}
