using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Commands;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;

internal static partial class TargetBattleThunderMarkSkillRegressionCases
{
    private const string EnemyActorId = "force_enemy:1";
    private const string NearEnemyActorId = "force_enemy_near:1";
    private const string ThunderTagThrowSkillId = "first_slice_skill_thunder_tag_throw";
    private const string ThunderMarkFoldSkillId = "first_slice_skill_thunder_mark_fold";
    private const string ThunderSpiralBreakSkillId = "first_slice_skill_thunder_spiral_break";
    private const int CreateThunderMarkEffectKindValue = 1;
    private const int TeleportToThunderMarkEffectKindValue = 2;
    private const int StartChanneledAreaDamageEffectKindValue = 3;
    private const int TargetedCellTargetingModeValue = 2;
    private const int TargetedActorOrCellTargetingModeValue = 3;
    private const int ThunderMarkCreatedEventKindValue = 20;
    private const int ThunderMarkTeleportEventKindValue = 21;

    internal static void Register(Action<string, Action> run)
    {
        run("runtime thunder tag creates attached mark", RuntimeThunderTagCreatesAttachedMark);
        run("runtime thunder tag creates ground mark", RuntimeThunderTagCreatesGroundMark);
        run("runtime thunder tag preserves moving caster state", RuntimeThunderTagPreservesMovingCasterState);
        run("runtime thunder fold rejects without live mark", RuntimeThunderFoldRejectsWithoutLiveMark);
        run("runtime thunder fold requires selected mark payload", RuntimeThunderFoldRequiresSelectedMarkPayload);
        run("runtime thunder fold rejects occupied landing anchor", RuntimeThunderFoldRejectsOccupiedLandingAnchor);
        run("runtime thunder fold uses selected mark reference", RuntimeThunderFoldUsesSelectedMarkReference);
        run("runtime thunder fold teleports caster near live mark", RuntimeThunderFoldTeleportsCasterNearLiveMark);
        run("runtime thunder fold clears stale displacement context", RuntimeThunderFoldClearsStaleDisplacementContext);
        run("runtime thunder spiral channel blocks ordinary movement", RuntimeThunderSpiralChannelBlocksOrdinaryMovement);
        run("runtime thunder spiral skill used carries channel duration", RuntimeThunderSpiralSkillUsedCarriesChannelDuration);
        run("runtime thunder spiral damages selected directional 3x3 area", RuntimeThunderSpiralDamagesSelectedDirectionalArea);
        run("runtime thunder spiral channel continues after fold", RuntimeThunderSpiralChannelContinuesAfterFold);
    }

    internal static void RuntimeThunderTagCreatesAttachedMark()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_thunder_tag_mark", enemyStrength: 100);
        AddThunderTagSkill(snapshot);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);

        BattleRuntimeCommandSubmitResult submit = SubmitTargetedSkill(
            controller,
            "battle_thunder_tag_mark",
            "cmd_thunder_tag_mark",
            EnemyActorId,
            skillDefinitionId: ThunderTagThrowSkillId);
        AssertTrue(submit.Accepted, "thunder tag should accept an enemy target");

        BattleRuntimeAdvanceResult advance = controller.AdvanceFixedTick();

        AssertSkillDamage(advance, ThunderTagThrowSkillId, "cmd_thunder_tag_mark", 12, "thunder tag impact");
        AssertEqual(88, EnemyCorps(controller).HitPoints, "enemy HP after thunder tag impact");
        AssertTrue(
            advance.Events.Any(item =>
                item.Kind == (BattleEventKind)ThunderMarkCreatedEventKindValue &&
                item.ActorId == "group_player:hero" &&
                item.TargetId == EnemyActorId &&
                item.SourceCommandId == "cmd_thunder_tag_mark" &&
                item.SourceDefinitionId == ThunderTagThrowSkillId &&
                item.HasTargetCells &&
                item.TargetGridX == EnemyCorps(controller).GridX &&
                item.TargetGridY == EnemyCorps(controller).GridY),
            "thunder tag should emit a runtime mark-created event with target cells");
        AssertRuntimeMarkAttachedToTarget(
            controller,
            ownerBattleGroupId: "group_player",
            attachedActorId: EnemyActorId,
            sourceCommandId: "cmd_thunder_tag_mark",
            sourceDefinitionId: ThunderTagThrowSkillId);
    }

    internal static void RuntimeThunderTagCreatesGroundMark()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_thunder_ground_mark", enemyStrength: 100);
        AddThunderTagSkill(snapshot);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);

        CommandRequest request = new()
        {
            CommandId = "cmd_thunder_ground_mark",
            BattleId = "battle_thunder_ground_mark",
            BattleGroupId = "group_player",
            Channel = CommandChannel.Hero,
            Kind = CommandKind.CastSkill,
            SkillDefinitionId = ThunderTagThrowSkillId
        };
        SetCommandTargetGrid(request, x: 3, y: 2, height: 0);

        BattleRuntimeCommandSubmitResult submit = controller.SubmitCommand(request);
        AssertTrue(submit.Accepted, "thunder tag should accept an empty ground cell");

        BattleRuntimeAdvanceResult advance = controller.AdvanceFixedTick();

        AssertEqual(100, EnemyCorps(controller).HitPoints, "ground thunder tag should not damage a fake or distant actor");
        AssertTrue(
            advance.Events.All(item => item.Kind != BattleEventKind.DamageApplied || item.SourceCommandId != "cmd_thunder_ground_mark"),
            "ground thunder tag should not emit damage without an actor target");
        AssertTrue(
            advance.Events.Any(item =>
                item.Kind == (BattleEventKind)ThunderMarkCreatedEventKindValue &&
                item.ActorId == "group_player:hero" &&
                item.TargetId == "" &&
                item.SourceCommandId == "cmd_thunder_ground_mark" &&
                item.SourceDefinitionId == ThunderTagThrowSkillId &&
                item.HasTargetCells &&
                item.TargetGridX == 3 &&
                item.TargetGridY == 2),
            "ground thunder tag should emit a mark-created event at the selected cell");
        AssertRuntimeGroundMark(
            controller,
            ownerBattleGroupId: "group_player",
            x: 3,
            y: 2,
            sourceCommandId: "cmd_thunder_ground_mark",
            sourceDefinitionId: ThunderTagThrowSkillId);
    }

    internal static void RuntimeThunderTagPreservesMovingCasterState()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_thunder_tag_offhand_move", enemyStrength: 100);
        AddOffhandThunderTagSkill(snapshot);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        BattleRuntimeActor caster = PlayerCorps(controller);
        caster.Phase = BattleRuntimeActorPhase.Moving;
        caster.MotionState = BattleRuntimeActorMotionState.Moving;
        caster.ActionReadyAtSeconds = 0.2;
        caster.HasMovementTarget = true;
        caster.MovementFromGridX = 0;
        caster.MovementFromGridY = 0;
        caster.MovementFromGridHeight = 0;
        caster.MovementToGridX = 1;
        caster.MovementToGridY = 0;
        caster.MovementToGridHeight = 0;
        caster.MovementStartedAtSeconds = 0;
        caster.MovementDurationSeconds = 0.2;
        caster.MovementProgress = 0.25;
        caster.HasMovementIntentSnapshot = true;
        caster.MovementIntentKind = Rpg.Runtime.Battle.AI.BattleRuntimeAiActionKind.AdvanceTowardTarget;
        caster.MovementIntentTargetActorId = EnemyActorId;
        caster.MovementIntentReasonCode = "existing_advance";
        caster.MovementSteeringMode = Rpg.Runtime.Battle.Navigation.BattleLocalSteeringMode.FollowObstacle;
        caster.MovementSteeringIntentKey = "existing_steering";

        BattleRuntimeCommandSubmitResult submit = controller.SubmitCommand(new CommandRequest
        {
            CommandId = "cmd_thunder_tag_offhand_move",
            BattleId = "battle_thunder_tag_offhand_move",
            BattleGroupId = "group_player",
            SourceActorId = caster.ActorId,
            Channel = CommandChannel.Hero,
            Kind = CommandKind.CastSkill,
            SkillDefinitionId = ThunderTagThrowSkillId,
            TargetActorId = EnemyActorId
        });
        AssertTrue(submit.Accepted, "moving caster should accept offhand thunder tag");

        BattleRuntimeAdvanceResult advance = controller.AdvanceFixedTick(0.04);

        AssertTrue(
            advance.Events.Any(item =>
                item.Kind == BattleEventKind.SkillUsed &&
                item.ActorId == caster.ActorId &&
                item.SourceCommandId == "cmd_thunder_tag_offhand_move"),
            $"offhand thunder tag should release while movement is still active events={DescribeEvents(advance)}");
        AssertSkillDamage(
            advance,
            ThunderTagThrowSkillId,
            "cmd_thunder_tag_offhand_move",
            12,
            "moving caster thunder tag impact",
            expectedActorId: caster.ActorId);
        AssertEqual(BattleRuntimeActorPhase.Moving, caster.Phase, "offhand thunder tag must not reset the caster phase");
        AssertTrue(caster.HasMovementTarget, "offhand thunder tag must not clear the active movement segment");
        AssertEqual(1, caster.MovementToGridX, "offhand thunder tag must preserve the movement target x");
        AssertEqual(0, caster.MovementToGridY, "offhand thunder tag must preserve the movement target y");
        AssertTrue(caster.HasMovementIntentSnapshot, "offhand thunder tag must preserve the movement intent snapshot");
        AssertEqual("existing_advance", caster.MovementIntentReasonCode, "offhand thunder tag must preserve movement intent reason");
        AssertEqual("existing_steering", caster.MovementSteeringIntentKey, "offhand thunder tag must preserve local steering");
    }

    internal static void RuntimeThunderFoldRejectsWithoutLiveMark()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_thunder_fold_no_mark", enemyStrength: 100);
        AddThunderFoldSkill(snapshot);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        CommandRequest foldRequest = new()
        {
            CommandId = "cmd_thunder_fold_no_mark",
            BattleId = "battle_thunder_fold_no_mark",
            BattleGroupId = "group_player",
            Channel = CommandChannel.Hero,
            Kind = CommandKind.CastSkill,
            SkillDefinitionId = ThunderMarkFoldSkillId
        };
        SetCommandTargetGrid(foldRequest, x: 5, y: 1, height: 0);
        SetProperty(foldRequest, "SelectedSpatialMarkId", "missing_mark");

        BattleRuntimeCommandSubmitResult fold = controller.SubmitCommand(foldRequest);

        AssertTrue(!fold.Accepted, "thunder fold should reject before queuing when the caster has no live mark");
        AssertEqual("thunder_mark_missing", fold.ReasonCode, "thunder fold missing-mark rejection reason");
        AssertTrue(
            fold.Events.Any(item =>
                item.Kind == BattleEventKind.CommandRejected &&
                item.SourceDefinitionId == ThunderMarkFoldSkillId &&
                item.ReasonCode == "thunder_mark_missing"),
            "missing thunder mark should be a command rejection event, not a queued failed release");
    }

    internal static void RuntimeThunderFoldRequiresSelectedMarkPayload()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_thunder_fold_selected_mark_required", enemyStrength: 100);
        AddThunderTagSkill(snapshot);
        AddThunderFoldSkill(snapshot);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);

        BattleRuntimeCommandSubmitResult tag = SubmitTargetedSkill(
            controller,
            "battle_thunder_fold_selected_mark_required",
            "cmd_thunder_fold_selected_mark_tag",
            EnemyActorId,
            skillDefinitionId: ThunderTagThrowSkillId);
        AssertTrue(tag.Accepted, "thunder tag setup should be accepted");
        _ = controller.AdvanceFixedTick();
        AssertTrue(controller.State.SpatialMarks.Count > 0, "setup should create a runtime thunder mark");

        CommandRequest foldRequest = new()
        {
            CommandId = "cmd_thunder_fold_without_selected_mark",
            BattleId = "battle_thunder_fold_selected_mark_required",
            BattleGroupId = "group_player",
            Channel = CommandChannel.Hero,
            Kind = CommandKind.CastSkill,
            SkillDefinitionId = ThunderMarkFoldSkillId
        };
        SetCommandTargetGrid(foldRequest, x: 5, y: 1, height: 0);

        BattleRuntimeCommandSubmitResult fold = controller.SubmitCommand(foldRequest);

        AssertTrue(!fold.Accepted, "thunder fold should not accept a destination without an explicitly selected mark");
        AssertEqual("thunder_mark_selection_required", fold.ReasonCode, "missing selected-mark rejection reason");
    }

    internal static void RuntimeThunderFoldRejectsOccupiedLandingAnchor()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_thunder_fold_occupied_landing", enemyStrength: 100);
        AddThunderFoldSkill(snapshot);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        BattleRuntimeActor hero = Hero(controller);
        controller.State.SpatialMarks.Add(new BattleRuntimeSpatialMark
        {
            MarkId = "manual_fold_mark_near_hero",
            OwnerBattleGroupId = "group_player",
            SourceActorId = hero.ActorId,
            SourceCommandId = "manual_fold_mark_near_hero_command",
            SourceDefinitionId = ThunderTagThrowSkillId,
            HasGroundAnchor = true,
            GridX = hero.GridX,
            GridY = hero.GridY,
            GridHeight = hero.GridHeight,
            CreatedAtSeconds = controller.CurrentTimeSeconds,
            ExpiresAtSeconds = controller.CurrentTimeSeconds + 8
        });
        CommandRequest foldRequest = new()
        {
            CommandId = "cmd_thunder_fold_occupied_landing",
            BattleId = "battle_thunder_fold_occupied_landing",
            BattleGroupId = "group_player",
            Channel = CommandChannel.Hero,
            Kind = CommandKind.CastSkill,
            SkillDefinitionId = ThunderMarkFoldSkillId
        };
        SetCommandTargetGrid(foldRequest, x: hero.GridX, y: hero.GridY, height: hero.GridHeight);
        SetProperty(foldRequest, "SelectedSpatialMarkId", "manual_fold_mark_near_hero");

        BattleRuntimeCommandSubmitResult fold = controller.SubmitCommand(foldRequest);

        AssertTrue(!fold.Accepted, "thunder fold should reject a landing anchor occupied by the caster");
        AssertEqual("thunder_mark_destination_occupied", fold.ReasonCode, "occupied landing rejection reason");
    }

    internal static void RuntimeThunderFoldUsesSelectedMarkReference()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_thunder_fold_selected_mark", enemyStrength: 100);
        AddThunderTagSkill(snapshot);
        AddThunderFoldSkill(snapshot);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);

        BattleRuntimeCommandSubmitResult attachedTag = SubmitTargetedSkill(
            controller,
            "battle_thunder_fold_selected_mark",
            "cmd_thunder_fold_selected_attached_mark",
            EnemyActorId,
            skillDefinitionId: ThunderTagThrowSkillId);
        AssertTrue(attachedTag.Accepted, "attached thunder tag setup should be accepted");
        _ = controller.AdvanceFixedTick();
        string attachedMarkId = controller.State.SpatialMarks.Last().MarkId;

        controller.State.SpatialMarks.Add(new BattleRuntimeSpatialMark
        {
            MarkId = "manual_newer_ground_mark",
            OwnerBattleGroupId = "group_player",
            SourceActorId = "group_player:hero",
            SourceCommandId = "manual_newer_ground_mark_command",
            SourceDefinitionId = ThunderTagThrowSkillId,
            HasGroundAnchor = true,
            GridX = 0,
            GridY = 6,
            GridHeight = 0,
            CreatedAtSeconds = controller.CurrentTimeSeconds,
            ExpiresAtSeconds = controller.CurrentTimeSeconds + 8
        });
        AssertTrue(
            !string.Equals(attachedMarkId, controller.State.SpatialMarks.Last().MarkId, StringComparison.Ordinal),
            "setup should create a newer mark so fold cannot rely on latest-mark lookup");

        CommandRequest foldRequest = new()
        {
            CommandId = "cmd_thunder_fold_selected_attached",
            BattleId = "battle_thunder_fold_selected_mark",
            BattleGroupId = "group_player",
            Channel = CommandChannel.Hero,
            Kind = CommandKind.CastSkill,
            SkillDefinitionId = ThunderMarkFoldSkillId
        };
        SetCommandTargetGrid(foldRequest, x: 6, y: 2, height: 0);
        SetProperty(foldRequest, "SelectedSpatialMarkId", attachedMarkId);

        BattleRuntimeCommandSubmitResult fold = controller.SubmitCommand(foldRequest);
        AssertTrue(fold.Accepted, $"fold should accept a legal destination around the selected mark reason={fold.ReasonCode}");

        _ = controller.AdvanceFixedTick();
        BattleRuntimeActor hero = Hero(controller);

        AssertEqual(6, hero.GridX, "hero should fold relative to the selected attached mark, not the newer ground mark");
        AssertEqual(2, hero.GridY, "hero y after selected-mark fold");
    }

    internal static void RuntimeThunderFoldTeleportsCasterNearLiveMark()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_thunder_fold", enemyStrength: 100);
        AddThunderTagSkill(snapshot);
        AddThunderFoldSkill(snapshot);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);

        BattleRuntimeCommandSubmitResult tag = SubmitTargetedSkill(
            controller,
            "battle_thunder_fold",
            "cmd_thunder_fold_tag",
            EnemyActorId,
            skillDefinitionId: ThunderTagThrowSkillId);
        AssertTrue(tag.Accepted, "thunder tag setup should be accepted");
        _ = controller.AdvanceFixedTick();
        string selectedMarkId = controller.State.SpatialMarks.Last().MarkId;

        CommandRequest foldRequest = new()
        {
            CommandId = "cmd_thunder_fold",
            BattleId = "battle_thunder_fold",
            BattleGroupId = "group_player",
            Channel = CommandChannel.Hero,
            Kind = CommandKind.CastSkill,
            SkillDefinitionId = ThunderMarkFoldSkillId
        };
        SetCommandTargetGrid(foldRequest, x: 5, y: 1, height: 0);
        SetProperty(foldRequest, "SelectedSpatialMarkId", selectedMarkId);

        BattleRuntimeCommandSubmitResult fold = controller.SubmitCommand(foldRequest);
        AssertTrue(fold.Accepted, "thunder fold should accept a legal target cell near the live mark");

        BattleRuntimeAdvanceResult advance = controller.AdvanceFixedTick();
        BattleRuntimeActor hero = Hero(controller);

        AssertEqual(5, hero.GridX, "hero grid x after thunder fold");
        AssertEqual(1, hero.GridY, "hero grid y after thunder fold");
        AssertTrue(
            advance.Events.Any(item =>
                item.Kind == (BattleEventKind)ThunderMarkTeleportEventKindValue &&
                item.ActorId == hero.ActorId &&
                item.SourceCommandId == "cmd_thunder_fold" &&
                item.SourceDefinitionId == ThunderMarkFoldSkillId &&
                item.HasMovementCells &&
                item.FromGridX == 0 &&
                item.FromGridY == 0 &&
                item.ToGridX == 5 &&
                item.ToGridY == 1),
            "thunder fold should emit a runtime teleport event with movement cells");
    }

    internal static void RuntimeThunderFoldClearsStaleDisplacementContext()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_thunder_fold_displacement", enemyStrength: 100);
        AddThunderTagSkill(snapshot);
        AddThunderFoldSkill(snapshot);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);

        BattleRuntimeCommandSubmitResult tag = SubmitTargetedSkill(
            controller,
            "battle_thunder_fold_displacement",
            "cmd_thunder_fold_displacement_tag",
            EnemyActorId,
            skillDefinitionId: ThunderTagThrowSkillId);
        AssertTrue(tag.Accepted, "thunder tag setup should be accepted");
        _ = controller.AdvanceFixedTick();
        string selectedMarkId = controller.State.SpatialMarks.Last().MarkId;

        BattleRuntimeActor hero = Hero(controller);
        hero.TargetActorId = EnemyActorId;
        hero.HasReservedGridCell = true;
        hero.ReservedGridX = 1;
        hero.ReservedGridY = 0;
        hero.ReservedGridHeight = 0;
        hero.HasMovementTarget = true;
        hero.MovementFromGridX = 0;
        hero.MovementFromGridY = 0;
        hero.MovementToGridX = 1;
        hero.MovementToGridY = 0;
        hero.MovementProgress = 0.5;
        hero.HasMovementBacktrackGuardCell = true;
        hero.MovementBacktrackGuardGridX = -1;
        hero.MovementBacktrackGuardGridY = 0;
        hero.HasSecondaryMovementBacktrackGuardCell = true;
        hero.SecondaryMovementBacktrackGuardGridX = -2;
        hero.SecondaryMovementBacktrackGuardGridY = 0;
        hero.MovementSteeringMode = Rpg.Runtime.Battle.Navigation.BattleLocalSteeringMode.FollowObstacle;
        hero.MovementSteeringIntentKey = "old_advance_context";
        hero.HasMovementIntentSnapshot = true;
        hero.MovementIntentTargetActorId = EnemyActorId;
        hero.MovementIntentReasonCode = "old_advance";
        hero.MovementIntentLocalCombatSituationId = "old_local_fight";

        CommandRequest foldRequest = new()
        {
            CommandId = "cmd_thunder_fold_displacement",
            BattleId = "battle_thunder_fold_displacement",
            BattleGroupId = "group_player",
            Channel = CommandChannel.Hero,
            Kind = CommandKind.CastSkill,
            SkillDefinitionId = ThunderMarkFoldSkillId
        };
        SetCommandTargetGrid(foldRequest, x: 5, y: 1, height: 0);
        SetProperty(foldRequest, "SelectedSpatialMarkId", selectedMarkId);

        BattleRuntimeCommandSubmitResult fold = controller.SubmitCommand(foldRequest);
        AssertTrue(fold.Accepted, "thunder fold should accept a legal target cell near the live mark");
        _ = controller.AdvanceFixedTick();

        AssertEqual(5, hero.GridX, "hero grid x after displacement");
        AssertEqual(1, hero.GridY, "hero grid y after displacement");
        AssertEqual(BattleRuntimeActorPhase.AnchoredDecision, hero.Phase, "idle displaced hero should return to decision from the new anchor");
        AssertEqual("", hero.TargetActorId, "displacement should clear stale retained target");
        AssertTrue(!hero.HasReservedGridCell, "displacement should clear stale reservation");
        AssertTrue(!hero.HasMovementTarget, "displacement should clear stale movement segment");
        AssertTrue(!hero.HasMovementIntentSnapshot, "displacement should clear stale movement intent snapshot");
        AssertTrue(!hero.HasMovementBacktrackGuardCell, "displacement should clear primary backtrack guard");
        AssertTrue(!hero.HasSecondaryMovementBacktrackGuardCell, "displacement should clear secondary backtrack guard");
        AssertEqual(Rpg.Runtime.Battle.Navigation.BattleLocalSteeringMode.SeekGoal, hero.MovementSteeringMode, "displacement should clear local steering mode");
        AssertEqual("", hero.MovementSteeringIntentKey, "displacement should clear local steering intent");
    }

    internal static void RuntimeThunderSpiralChannelContinuesAfterFold()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_thunder_spiral_fold", enemyStrength: 100);
        AddThunderTagSkill(snapshot);
        AddThunderFoldSkill(snapshot);
        AddThunderSpiralSkill(snapshot);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        MovePlayerCorpsAway(controller);
        AddNearEnemy(controller);
        FreezeAutonomousCorps(controller);

        BattleRuntimeCommandSubmitResult tag = SubmitTargetedSkill(
            controller,
            "battle_thunder_spiral_fold",
            "cmd_thunder_spiral_tag",
            EnemyActorId,
            skillDefinitionId: ThunderTagThrowSkillId);
        AssertTrue(tag.Accepted, "thunder tag setup should be accepted");
        _ = controller.AdvanceFixedTick();
        string selectedMarkId = controller.State.SpatialMarks.Last().MarkId;

        CommandRequest spiralRequest = new()
        {
            CommandId = "cmd_thunder_spiral",
            BattleId = "battle_thunder_spiral_fold",
            BattleGroupId = "group_player",
            Channel = CommandChannel.Hero,
            Kind = CommandKind.CastSkill,
            SkillDefinitionId = ThunderSpiralBreakSkillId
        };
        SetCommandTargetGrid(spiralRequest, x: 2, y: 0, height: 0);
        BattleRuntimeCommandSubmitResult spiral = controller.SubmitCommand(spiralRequest);
        AssertTrue(spiral.Accepted, "thunder spiral should accept a selected directional center cell");

        BattleRuntimeAdvanceResult start = controller.AdvanceFixedTick();
        AssertDamageEvent(start, "cmd_thunder_spiral", NearEnemyActorId, expectedActorGridX: 0, expectedDamage: 9, "initial spiral tick");
        AssertEqual(71, NearEnemy(controller).HitPoints, "near enemy HP after initial spiral tick");
        AssertEqual(88, EnemyCorps(controller).HitPoints, "marked enemy should not be hit before fold");

        CommandRequest foldRequest = new()
        {
            CommandId = "cmd_thunder_spiral_fold",
            BattleId = "battle_thunder_spiral_fold",
            BattleGroupId = "group_player",
            Channel = CommandChannel.Hero,
            Kind = CommandKind.CastSkill,
            SkillDefinitionId = ThunderMarkFoldSkillId
        };
        SetCommandTargetGrid(foldRequest, x: 5, y: 0, height: 0);
        SetProperty(foldRequest, "SelectedSpatialMarkId", selectedMarkId);
        BattleRuntimeCommandSubmitResult fold = controller.SubmitCommand(foldRequest);
        AssertTrue(fold.Accepted, "fold should be accepted during an active spiral channel");
        _ = controller.AdvanceFixedTick();
        AssertEqual(5, Hero(controller).GridX, "hero grid x after fold during spiral");

        _ = controller.AdvanceFixedTick(0.2);
        BattleRuntimeAdvanceResult continued = controller.AdvanceFixedTick(0.2);
        AssertDamageEvent(
            continued,
            "cmd_thunder_spiral",
            EnemyActorId,
            expectedActorGridX: 5,
            expectedDamage: 9,
            $"post-fold spiral tick hero={Hero(controller).GridX},{Hero(controller).GridY} enemy={EnemyCorps(controller).GridX},{EnemyCorps(controller).GridY} events={DescribeEvents(continued)}");
        AssertEqual(79, EnemyCorps(controller).HitPoints, "marked enemy HP after post-fold spiral tick");

        for (int i = 0; i < 9; i++)
        {
            _ = controller.AdvanceFixedTick(0.2);
        }
        BattleRuntimeAdvanceResult afterDuration = controller.AdvanceFixedTick(0.2);
        AssertTrue(
            afterDuration.Events.All(item =>
                item.Kind != BattleEventKind.DamageApplied ||
                item.SourceCommandId != "cmd_thunder_spiral"),
            "folding during thunder spiral must not refresh the channel duration");
    }

    internal static void RuntimeThunderSpiralChannelBlocksOrdinaryMovement()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_thunder_spiral_lock", enemyStrength: 100);
        AddThunderSpiralSkill(snapshot);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);

        CommandRequest spiralRequest = new()
        {
            CommandId = "cmd_thunder_spiral_lock",
            BattleId = "battle_thunder_spiral_lock",
            BattleGroupId = "group_player",
            SourceActorId = "force_player:1",
            Channel = CommandChannel.Hero,
            Kind = CommandKind.CastSkill,
            SkillDefinitionId = ThunderSpiralBreakSkillId
        };
        SetCommandTargetGrid(spiralRequest, x: 2, y: 0, height: 0);
        BattleRuntimeCommandSubmitResult spiral = controller.SubmitCommand(spiralRequest);
        AssertTrue(spiral.Accepted, "visible caster should accept thunder spiral");

        _ = controller.AdvanceFixedTick();
        BattleRuntimeAdvanceResult duringChannel = controller.AdvanceFixedTick();

        AssertTrue(
            duringChannel.Events.All(item =>
                item.Kind != BattleEventKind.MovementStarted ||
                item.ActorId != "force_player:1"),
            $"ordinary movement should not start while thunder spiral channel is active events={DescribeEvents(duringChannel)}");
    }

    internal static void RuntimeThunderSpiralSkillUsedCarriesChannelDuration()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_thunder_spiral_duration", enemyStrength: 100);
        AddThunderSpiralSkill(snapshot);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);

        CommandRequest spiralRequest = new()
        {
            CommandId = "cmd_thunder_spiral_duration",
            BattleId = "battle_thunder_spiral_duration",
            BattleGroupId = "group_player",
            SourceActorId = "force_player:1",
            Channel = CommandChannel.Hero,
            Kind = CommandKind.CastSkill,
            SkillDefinitionId = ThunderSpiralBreakSkillId
        };
        SetCommandTargetGrid(spiralRequest, x: 2, y: 0, height: 0);
        BattleRuntimeCommandSubmitResult spiral = controller.SubmitCommand(spiralRequest);
        AssertTrue(spiral.Accepted, "thunder spiral should accept a selected directional center cell");

        BattleRuntimeAdvanceResult start = controller.AdvanceFixedTick();
        BattleEvent skillUsed = start.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.SkillUsed &&
            item.SourceDefinitionId == ThunderSpiralBreakSkillId);

        AssertTrue(skillUsed != null, $"thunder spiral should emit SkillUsed event events={DescribeEvents(start)}");
        AssertTrue(
            Math.Abs(1.6 - skillUsed.ActionDurationSeconds) < 0.0001,
            $"thunder spiral SkillUsed should carry the channel duration for presentation hold timing: actual={skillUsed.ActionDurationSeconds}");
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
        TargetBattleTestTopology.CompileAroundGroups(snapshot, margin: 8);
        return snapshot;
    }

    private static void AddThunderTagSkill(BattleStartSnapshot snapshot)
    {
        snapshot.SkillDefinitions.Add(new BattleSkillSnapshot
        {
            SkillDefinitionId = ThunderTagThrowSkillId,
            DisplayName = "Thunder Tag Throw",
            TargetingMode = (BattleSkillTargetingMode)TargetedActorOrCellTargetingModeValue,
            Range = 8,
            CasterUnitIds = { "hero_def_player" },
            CastSeconds = 0,
            ImpactDelaySeconds = 0,
            RecoverySeconds = 0,
            HasInterruptPolicy = true,
            CanInterruptBasicAttackWindup = true,
            CanCancelBasicAttackRecovery = false,
            Effects =
            {
                new DamageSkillEffectSnapshot
                {
                    BaseDamage = 12
                },
                new CreateMarkSkillEffectSnapshot
                {
                    LifetimeSeconds = 8.0
                }
            }
        });
    }

    private static void AddOffhandThunderTagSkill(BattleStartSnapshot snapshot)
    {
        AddThunderTagSkill(snapshot);
        BattleSkillSnapshot skill = snapshot.SkillDefinitions.Last(item => item.SkillDefinitionId == ThunderTagThrowSkillId);
        SetProperty(skill, "ReleasesWithoutOccupyingCaster", true);
    }

    private static void AddThunderFoldSkill(BattleStartSnapshot snapshot)
    {
        snapshot.SkillDefinitions.Add(new BattleSkillSnapshot
        {
            SkillDefinitionId = ThunderMarkFoldSkillId,
            DisplayName = "Thunder Mark Fold",
            TargetingMode = (BattleSkillTargetingMode)TargetedCellTargetingModeValue,
            Range = 8,
            CasterUnitIds = { "hero_def_player" },
            CastSeconds = 0,
            ImpactDelaySeconds = 0,
            RecoverySeconds = 0,
            HasInterruptPolicy = true,
            CanInterruptBasicAttackWindup = true,
            CanCancelBasicAttackRecovery = true,
            Effects =
            {
                new TeleportToMarkSkillEffectSnapshot
                {
                    LandingRadius = 3
                }
            }
        });
    }

    private static void AddThunderSpiralSkill(BattleStartSnapshot snapshot)
    {
        BattleSkillEffectSnapshot channel = new ChanneledAreaDamageSkillEffectSnapshot
        {
            BaseDamage = 9,
            UsesTargetOffset = true
        };
        SetProperty(channel, "DurationSeconds", 1.6);
        SetProperty(channel, "TickIntervalSeconds", 0.2);
        SetProperty(channel, "Radius", 1);
        snapshot.SkillDefinitions.Add(new BattleSkillSnapshot
        {
            SkillDefinitionId = ThunderSpiralBreakSkillId,
            DisplayName = "Thunder Spiral Break",
            TargetingMode = BattleSkillTargetingMode.TargetedCell,
            Range = 3,
            CasterUnitIds = { "hero_def_player" },
            CastSeconds = 0,
            ImpactDelaySeconds = 0,
            RecoverySeconds = 0,
            HasInterruptPolicy = true,
            CanInterruptBasicAttackWindup = true,
            CanCancelBasicAttackRecovery = true,
            Effects = { channel }
        });
    }

    private static BattleRuntimeCommandSubmitResult SubmitTargetedSkill(
        BattleRuntimeSessionController controller,
        string battleId,
        string commandId,
        string targetActorId,
        string skillDefinitionId)
    {
        return controller.SubmitCommand(new CommandRequest
        {
            CommandId = commandId,
            BattleId = battleId,
            BattleGroupId = "group_player",
            Channel = CommandChannel.Hero,
            Kind = CommandKind.CastSkill,
            SkillDefinitionId = skillDefinitionId,
            TargetActorId = targetActorId
        });
    }

    private static BattleRuntimeActor EnemyCorps(BattleRuntimeSessionController controller)
    {
        return controller.State.Actors.Single(item => item.ActorId == EnemyActorId);
    }

    private static BattleRuntimeActor NearEnemy(BattleRuntimeSessionController controller)
    {
        return controller.State.Actors.Single(item => item.ActorId == NearEnemyActorId);
    }

    private static BattleRuntimeActor Hero(BattleRuntimeSessionController controller)
    {
        return controller.State.Actors.Single(item => item.ActorId == "group_player:hero");
    }

    private static BattleRuntimeActor PlayerCorps(BattleRuntimeSessionController controller)
    {
        return controller.State.Actors.Single(item => item.ActorId == "force_player:1");
    }

    private static void SetCommandTargetGrid(CommandRequest request, int x, int y, int height)
    {
        SetProperty(request, "HasTargetGrid", true);
        SetProperty(request, "TargetGridX", x);
        SetProperty(request, "TargetGridY", y);
        SetProperty(request, "TargetGridHeight", height);
    }

    private static void SetProperty<T>(object source, string propertyName, T value)
    {
        System.Reflection.PropertyInfo? property = source.GetType().GetProperty(propertyName);
        AssertTrue(property != null && property.CanWrite, $"{source.GetType().Name} should expose writable {propertyName}");
        property!.SetValue(source, value);
    }

    private static void MovePlayerCorpsAway(BattleRuntimeSessionController controller)
    {
        BattleRuntimeActor playerCorps = controller.State.Actors.Single(item => item.ActorId == "force_player:1");
        playerCorps.GridX = -6;
        playerCorps.GridY = 0;
        playerCorps.GridHeight = 0;
        playerCorps.Position = -6;
    }

    private static void AddNearEnemy(BattleRuntimeSessionController controller)
    {
        controller.State.Actors.Add(new BattleRuntimeActor
        {
            ActorId = NearEnemyActorId,
            BattleGroupId = "group_enemy",
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

    private static void AssertSkillDamage(
        BattleRuntimeAdvanceResult advance,
        string expectedSkillId,
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
        AssertEqual(expectedSkillId, damage!.SourceDefinitionId, $"{message} source definition");
        AssertEqual(-expectedDamage, damage.CorpsStrengthDelta, $"{message} amount");
    }

    private static void AssertDamageEvent(
        BattleRuntimeAdvanceResult advance,
        string commandId,
        string targetActorId,
        int expectedActorGridX,
        int expectedDamage,
        string message)
    {
        BattleEvent? damage = advance.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.DamageApplied &&
            item.SourceCommandId == commandId &&
            item.TargetId == targetActorId);
        AssertTrue(damage != null, $"{message} should apply damage");
        AssertEqual(expectedActorGridX, damage!.ActorGridX, $"{message} actor grid x");
        AssertEqual(-expectedDamage, damage.CorpsStrengthDelta, $"{message} amount");
    }

    private static string DescribeEvents(BattleRuntimeAdvanceResult advance)
    {
        return string.Join("|", advance.Events.Select(item =>
            $"{item.Kind}:{item.SourceCommandId}:{item.ActorId}->{item.TargetId}:{item.ReasonCode}@{item.ActorGridX},{item.ActorGridY}"));
    }

    private static void AssertRuntimeMarkAttachedToTarget(
        BattleRuntimeSessionController controller,
        string ownerBattleGroupId,
        string attachedActorId,
        string sourceCommandId,
        string sourceDefinitionId)
    {
        System.Reflection.PropertyInfo? marksProperty = controller.State.GetType().GetProperty("SpatialMarks");
        AssertTrue(marksProperty != null, "BattleRuntimeState should expose runtime SpatialMarks");
        object? value = marksProperty!.GetValue(controller.State);
        System.Collections.IEnumerable? marks = value as System.Collections.IEnumerable;
        AssertTrue(marks != null, "BattleRuntimeState.SpatialMarks should be enumerable");
        foreach (object mark in marks!)
        {
            if (ReadStringProperty(mark, "OwnerBattleGroupId") == ownerBattleGroupId &&
                ReadStringProperty(mark, "AttachedActorId") == attachedActorId &&
                ReadStringProperty(mark, "SourceCommandId") == sourceCommandId &&
                ReadStringProperty(mark, "SourceDefinitionId") == sourceDefinitionId)
            {
                return;
            }
        }

        throw new Exception("expected an attached runtime thunder mark on the target actor");
    }

    private static void AssertRuntimeGroundMark(
        BattleRuntimeSessionController controller,
        string ownerBattleGroupId,
        int x,
        int y,
        string sourceCommandId,
        string sourceDefinitionId)
    {
        System.Reflection.PropertyInfo? marksProperty = controller.State.GetType().GetProperty("SpatialMarks");
        AssertTrue(marksProperty != null, "BattleRuntimeState should expose runtime SpatialMarks");
        object? value = marksProperty!.GetValue(controller.State);
        System.Collections.IEnumerable? marks = value as System.Collections.IEnumerable;
        AssertTrue(marks != null, "BattleRuntimeState.SpatialMarks should be enumerable");
        foreach (object mark in marks!)
        {
            if (ReadStringProperty(mark, "OwnerBattleGroupId") == ownerBattleGroupId &&
                string.IsNullOrWhiteSpace(ReadStringProperty(mark, "AttachedActorId")) &&
                ReadBoolProperty(mark, "HasGroundAnchor") &&
                ReadIntProperty(mark, "GridX") == x &&
                ReadIntProperty(mark, "GridY") == y &&
                ReadStringProperty(mark, "SourceCommandId") == sourceCommandId &&
                ReadStringProperty(mark, "SourceDefinitionId") == sourceDefinitionId)
            {
                return;
            }
        }

        throw new Exception("expected a ground runtime thunder mark at the selected cell");
    }

    private static string ReadStringProperty(object source, string propertyName)
    {
        return source.GetType().GetProperty(propertyName)?.GetValue(source) as string ?? "";
    }

    private static bool ReadBoolProperty(object source, string propertyName)
    {
        object? value = source.GetType().GetProperty(propertyName)?.GetValue(source);
        return value is bool flag && flag;
    }

    private static int ReadIntProperty(object source, string propertyName)
    {
        object? value = source.GetType().GetProperty(propertyName)?.GetValue(source);
        return value is int number ? number : 0;
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
