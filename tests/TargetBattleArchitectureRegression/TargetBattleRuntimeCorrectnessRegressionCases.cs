using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Tactics;

internal static class TargetBattleRuntimeCorrectnessRegressionCases
{
    private const double FixedTickSeconds = 0.04;
    private const string MovingActorId = "player_mover:1";

    public static void Register(Action<string, Action> run)
    {
        run("runtime fixed target region containment uses centered bounds", FixedTargetRegionContainmentUsesCenteredBounds);
        run("runtime defeated moving actor emits cancellation boundary", DefeatedMovingActorEmitsCancellationBoundary);
        run("runtime local combat normalizes faction comparisons", LocalCombatNormalizesFactionComparisons);
        run("runtime stale retarget preserves tactical scope", StaleRetargetPreservesTacticalScope);
        run("runtime diagonal height step validates sides at source height", DiagonalHeightStepValidatesSidesAtSourceHeight);
        run("runtime invalid handoff remains incomplete", InvalidHandoffRemainsIncomplete);
        run("runtime missing navigation topology remains incomplete", MissingNavigationTopologyRemainsIncomplete);
        run("runtime blank faction remains invalid", BlankFactionRemainsInvalid);
        run("runtime malformed combat stats remain invalid", MalformedCombatStatsRemainInvalid);
        run("runtime malformed footprint dimensions remain invalid", MalformedFootprintDimensionsRemainInvalid);
        run("runtime illegal start footprint remains incomplete", IllegalStartFootprintRemainsIncomplete);
        run("runtime invalid skill targeting remains incomplete", InvalidSkillTargetingRemainsIncomplete);
        run("runtime targeted skill range remains required", TargetedSkillRangeRemainsRequired);
        run("runtime skill caster bindings remain required", SkillCasterBindingsRemainRequired);
        run("runtime skill interrupt policy remains required", SkillInterruptPolicyRemainsRequired);
        run("runtime explicit false skill interrupt policy remains valid", ExplicitFalseSkillInterruptPolicyRemainsValid);
        run("runtime ordinary damage effect amount remains positive", OrdinaryDamageEffectAmountRemainsPositive);
        run("runtime empty skill effects remain invalid", EmptySkillEffectsRemainInvalid);
        run("runtime invalid skill effect kind remains incomplete", InvalidSkillEffectKindRemainsIncomplete);
        run("runtime invalid channeled effect payload remains incomplete", InvalidChanneledEffectPayloadRemainsIncomplete);
        run("runtime duplicate actor ids fail with named invariant", DuplicateActorIdsFailWithNamedInvariant);
    }

    private static void FixedTargetRegionContainmentUsesCenteredBounds()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_centered_fixed_target_region",
            BattleId = "battle_centered_fixed_target_region",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup(
                    "enemy_group",
                    "enemy_force",
                    "enemy",
                    BattleGroupTacticalMode.EnemyOffense,
                    0,
                    0,
                    BuildFixedTargetRegion("enemy_fixed_player_region", "enemy_group", centerX: 10, centerY: 10, width: 7, height: 7)),
                BuildGroup("player_group", "player_force", "player", BattleGroupTacticalMode.PlayerCommanded, 7, 7)
            }
        };
        AddRectSurfaces(snapshot, 0, 0, 12, 12);
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);

        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        controller.AdvanceNextTick();

        BattleGroupTacticalState enemyState = controller.State.TacticalStates["enemy_group"];
        AssertEqual(BattleTacticalRegionKind.FixedTarget, enemyState.SelectedRegion.Kind, "fixed target region should remain selected");
        AssertEqual("enemy_fixed_player_region", enemyState.SelectedRegion.RegionId, "fixed target region should not be replaced");
    }

    private static void DefeatedMovingActorEmitsCancellationBoundary()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildMovementCancellationSnapshot());

        BattleRuntimeAdvanceResult firstAdvance = controller.AdvanceFixedTick(FixedTickSeconds);
        BattleEvent? started = firstAdvance.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == MovingActorId);
        AssertTrue(started != null, "test setup should start a movement segment before cancellation");

        BattleRuntimeActor mover = controller.State.Actors.Single(item => item.ActorId == MovingActorId);
        int currentX = mover.GridX;
        int currentY = mover.GridY;
        int currentHeight = mover.GridHeight;
        mover.HitPoints = 0;

        BattleRuntimeAdvanceResult cancellationAdvance = controller.AdvanceFixedTick(FixedTickSeconds);
        BattleEvent? cancelled = cancellationAdvance.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementCompleted &&
            item.ActorId == MovingActorId);

        AssertTrue(cancelled != null, "defeated moving actor should emit a boundary cleanup event");
        AssertEqual("movement_cancelled_defeated", cancelled!.ReasonCode, "cancellation reason");
        AssertEqual(currentX, cancelled.ToGridX, "cancelled movement should end at the current authoritative anchor x");
        AssertEqual(currentY, cancelled.ToGridY, "cancelled movement should end at the current authoritative anchor y");
        AssertEqual(currentHeight, cancelled.ToGridHeight, "cancelled movement should end at the current authoritative anchor height");
        AssertEqual(BattleRuntimeActorPhase.Defeated, mover.Phase, "mover phase");
        AssertTrue(!mover.HasMovementTarget, "defeated actor should not keep a live movement target");
    }

    private static void LocalCombatNormalizesFactionComparisons()
    {
        RecordingBattleRuntimeAiExecutor executor = new(new DefaultBattleRuntimeAiExecutor());
        new BattleRuntimeSession(executor)
            .Begin(BuildLocalCombatFactionNormalizationSnapshot())
            .AdvanceNextTick();

        BattleRuntimeAiDecisionFacts? supportFacts = executor.SeenFacts.LastOrDefault(item =>
            item.ActorId == "support:1" &&
            item.HasTarget);

        AssertTrue(supportFacts != null, "support actor should receive target-scoped decision facts");
        AssertTrue(
            supportFacts!.HasLocalCombatSituation,
            "blank faction ids should normalize to player when detecting same-faction local fights");
        AssertEqual("target:1", supportFacts.LocalCombatTargetActorId, "local combat target");
    }

    private static void StaleRetargetPreservesTacticalScope()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildScopedStaleRetargetSnapshot());
        BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();
        BattleRuntimeActor mover = controller.State.Actors.Single(item => item.ActorId == "player_mover:1");

        BattleEvent? moverMove = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "player_mover:1");

        AssertTrue(
            moverMove != null,
            $"mover should retarget after the weak local target dies: target={mover.TargetActorId} phase={mover.Phase} failure={mover.LastAdvanceFailureReason} events={DescribeEvents(tick.Events)}");
        AssertEqual("enemy_live:1", moverMove!.TargetId, "stale retarget should stay inside the active tactical scope");
    }

    private static void DiagonalHeightStepValidatesSidesAtSourceHeight()
    {
        BattleRuntimeAdvanceResult tick = new BattleRuntimeSession()
            .Begin(BuildDiagonalHeightStepSnapshot())
            .AdvanceNextTick();

        BattleEvent? move = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "height_player:1");

        AssertTrue(move != null, "height-changing diagonal connection should be usable when source-height side anchors are legal");
        AssertEqual(1, move!.ToGridX, "diagonal height step x");
        AssertEqual(1, move.ToGridY, "diagonal height step y");
        AssertEqual(1, move.ToGridHeight, "diagonal height step height");
    }

    private static void InvalidHandoffRemainsIncomplete()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(new BattleStartSnapshot
            {
                SnapshotId = "snapshot_invalid_handoff_controller",
                BattleId = "battle_invalid_handoff_controller"
            });

        AssertTrue(!controller.IsComplete, "invalid handoff controller should stay incomplete");
        AssertTrue(!controller.Outcome.IsComplete, "invalid handoff outcome should stay incomplete");

        BattleRuntimeAdvanceResult advance = controller.AdvanceNextTick();
        AssertTrue(!advance.IsComplete, "invalid handoff advance result should stay incomplete");
        AssertTrue(!advance.Outcome.IsComplete, "invalid handoff advance outcome should stay incomplete");

        BattleRuntimeSessionResult result = controller.AdvanceToCompletion();
        AssertTrue(!result.Outcome.IsComplete, "invalid handoff completion attempt should not create a completed outcome");
        AssertTrue(
            result.EventStream.Events.Any(item => item.ReasonCode == "battle_snapshot_invalid"),
            "invalid handoff should keep the diagnostic rejection event");
    }

    private static void MissingNavigationTopologyRemainsIncomplete()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_missing_navigation_topology",
            BattleId = "battle_missing_navigation_topology",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("group_player", "force_player", "player", BattleGroupTacticalMode.PlayerCommanded, 0, 0),
                BuildGroup("group_enemy", "force_enemy", "enemy", BattleGroupTacticalMode.EnemyOffense, 3, 0)
            }
        };

        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);

        AssertTrue(!controller.IsComplete, "missing topology handoff should stay incomplete");
        AssertTrue(!controller.Outcome.IsComplete, "missing topology outcome should stay incomplete");
        AssertTrue(
            controller.EventStream.Events.Any(item => item.ReasonCode == "navigation_topology_missing"),
            "missing topology should emit an explicit launch rejection reason");
    }

    private static void BlankFactionRemainsInvalid()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_blank_faction_invalid");
        snapshot.BattleGroups[0].FactionId = "";

        AssertInvalidGroupSnapshot(snapshot, "battle_group_faction_missing", "blank faction");
    }

    private static void MalformedCombatStatsRemainInvalid()
    {
        BattleStartSnapshot missingHitPoints = BuildOpposedSnapshot("battle_missing_hit_points_invalid");
        missingHitPoints.BattleGroups[0].MaxHitPoints = 0;
        AssertInvalidGroupSnapshot(missingHitPoints, "battle_group_hit_points_invalid", "missing hit points");

        BattleStartSnapshot missingAttackDamage = BuildOpposedSnapshot("battle_missing_attack_damage_invalid");
        missingAttackDamage.BattleGroups[0].AttackDamage = 0;
        AssertInvalidGroupSnapshot(missingAttackDamage, "battle_group_attack_damage_invalid", "missing attack damage");

        BattleStartSnapshot missingAttackRange = BuildOpposedSnapshot("battle_missing_attack_range_invalid");
        missingAttackRange.BattleGroups[0].AttackRange = 0;
        AssertInvalidGroupSnapshot(missingAttackRange, "battle_group_attack_range_invalid", "missing attack range");

        BattleStartSnapshot invalidAttackSpeed = BuildOpposedSnapshot("battle_invalid_attack_speed_invalid");
        invalidAttackSpeed.BattleGroups[0].AttackSpeed = double.NaN;
        AssertInvalidGroupSnapshot(invalidAttackSpeed, "battle_group_attack_speed_invalid", "invalid attack speed");
    }

    private static void MalformedFootprintDimensionsRemainInvalid()
    {
        BattleStartSnapshot zeroWidth = BuildOpposedSnapshot("battle_zero_footprint_width_invalid");
        zeroWidth.BattleGroups[0].FootprintWidth = 0;
        AssertInvalidGroupSnapshot(zeroWidth, "battle_group_footprint_invalid", "zero footprint width");

        BattleStartSnapshot negativeHeight = BuildOpposedSnapshot("battle_negative_footprint_height_invalid");
        negativeHeight.BattleGroups[0].FootprintHeight = -1;
        AssertInvalidGroupSnapshot(negativeHeight, "battle_group_footprint_invalid", "negative footprint height");

        BattleStartSnapshot tooWide = BuildOpposedSnapshot("battle_oversized_footprint_invalid");
        tooWide.BattleGroups[0].FootprintWidth = 4;
        AssertInvalidGroupSnapshot(tooWide, "battle_group_footprint_invalid", "oversized footprint width");
    }

    private static void IllegalStartFootprintRemainsIncomplete()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_illegal_start_footprint",
            BattleId = "battle_illegal_start_footprint",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("group_player", "force_player", "player", BattleGroupTacticalMode.PlayerCommanded, 0, 0),
                BuildGroup("group_enemy", "force_enemy", "enemy", BattleGroupTacticalMode.EnemyOffense, 3, 0)
            }
        };
        snapshot.BattleGroups[0].FootprintWidth = 2;
        snapshot.BattleGroups[0].FootprintHeight = 2;
        AddRectSurfaces(snapshot, 0, 0, 3, 0);
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);

        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);

        AssertTrue(!controller.IsComplete, "illegal footprint handoff should stay incomplete");
        AssertTrue(!controller.Outcome.IsComplete, "illegal footprint outcome should stay incomplete");
        AssertTrue(
            controller.EventStream.Events.Any(item => item.ReasonCode == "battle_start_footprint_invalid"),
            "illegal start footprint should emit an explicit launch rejection reason");
    }

    private static void InvalidSkillTargetingRemainsIncomplete()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshotWithSkill("battle_invalid_skill_targeting", new BattleSkillSnapshot
        {
            SkillId = "invalid_targeting_skill",
            DisplayName = "Invalid Targeting Skill",
            TargetingMode = BattleSkillTargetingMode.None,
            Range = 4,
            CasterUnitIds = { "force_player_hero_definition" },
            Effects =
            {
                new BattleSkillEffectSnapshot
                {
                    Kind = BattleSkillEffectKind.Damage,
                    Amount = 12
                }
            }
        });

        AssertInvalidSkillSnapshot(snapshot, "battle_skill_targeting_invalid", "invalid targeting mode");
    }

    private static void TargetedSkillRangeRemainsRequired()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshotWithSkill("battle_targeted_skill_range_required", new BattleSkillSnapshot
        {
            SkillId = "missing_range_skill",
            DisplayName = "Missing Range Skill",
            TargetingMode = BattleSkillTargetingMode.TargetedActor,
            Range = 0,
            CasterUnitIds = { "force_player_hero_definition" },
            Effects =
            {
                new BattleSkillEffectSnapshot
                {
                    Kind = BattleSkillEffectKind.Damage,
                    Amount = 12
                }
            }
        });

        AssertInvalidSkillSnapshot(snapshot, "battle_skill_range_invalid", "targeted skill range");
    }

    private static void SkillCasterBindingsRemainRequired()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshotWithSkill("battle_missing_skill_caster_bindings", new BattleSkillSnapshot
        {
            SkillId = "missing_caster_bindings_skill",
            DisplayName = "Missing Caster Bindings Skill",
            TargetingMode = BattleSkillTargetingMode.TargetedActor,
            Range = 4,
            Effects =
            {
                new BattleSkillEffectSnapshot
                {
                    Kind = BattleSkillEffectKind.Damage,
                    Amount = 12
                }
            }
        });

        AssertInvalidSkillSnapshot(snapshot, "battle_skill_caster_bindings_missing", "missing caster bindings");
    }

    private static void SkillInterruptPolicyRemainsRequired()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshotWithSkill("battle_missing_skill_interrupt_policy", new BattleSkillSnapshot
        {
            SkillId = "missing_interrupt_policy_skill",
            DisplayName = "Missing Interrupt Policy Skill",
            TargetingMode = BattleSkillTargetingMode.TargetedActor,
            Range = 4,
            CasterUnitIds = { "force_player_hero_definition" },
            Effects =
            {
                new BattleSkillEffectSnapshot
                {
                    Kind = BattleSkillEffectKind.Damage,
                    Amount = 12
                }
            }
        }, authoredInterruptPolicy: false);

        AssertInvalidSkillSnapshot(snapshot, "battle_skill_interrupt_policy_missing", "missing interrupt policy");
    }

    private static void ExplicitFalseSkillInterruptPolicyRemainsValid()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshotWithSkill("battle_explicit_false_skill_interrupt_policy", new BattleSkillSnapshot
        {
            SkillId = "explicit_false_interrupt_policy_skill",
            DisplayName = "Explicit False Interrupt Policy Skill",
            TargetingMode = BattleSkillTargetingMode.TargetedActor,
            Range = 4,
            CasterUnitIds = { "force_player_hero_definition" },
            HasInterruptPolicy = true,
            CanInterruptBasicAttackWindup = false,
            CanCancelBasicAttackRecovery = false,
            ReleasesWithoutOccupyingCaster = false,
            Effects =
            {
                new BattleSkillEffectSnapshot
                {
                    Kind = BattleSkillEffectKind.Damage,
                    Amount = 12
                }
            }
        });

        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);

        AssertTrue(
            !controller.EventStream.Events.Any(item =>
                item.Kind == BattleEventKind.CommandRejected &&
                item.SourceDefinitionId == "explicit_false_interrupt_policy_skill"),
            "explicitly authored all-false interrupt policy should not be rejected as missing");
    }

    private static void OrdinaryDamageEffectAmountRemainsPositive()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshotWithSkill("battle_non_positive_damage_amount", new BattleSkillSnapshot
        {
            SkillId = "non_positive_damage_skill",
            DisplayName = "Non Positive Damage Skill",
            TargetingMode = BattleSkillTargetingMode.TargetedActor,
            Range = 4,
            CasterUnitIds = { "force_player_hero_definition" },
            Effects =
            {
                new BattleSkillEffectSnapshot
                {
                    Kind = BattleSkillEffectKind.Damage,
                    Amount = 0
                }
            }
        });

        AssertInvalidSkillSnapshot(snapshot, "battle_skill_effect_damage_amount_invalid", "non-positive damage amount");
    }

    private static void EmptySkillEffectsRemainInvalid()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshotWithSkill("battle_empty_skill_effects_invalid", new BattleSkillSnapshot
        {
            SkillId = "empty_effects_skill",
            DisplayName = "Empty Effects Skill",
            TargetingMode = BattleSkillTargetingMode.TargetedActor,
            Range = 4,
            CasterUnitIds = { "force_player_hero_definition" }
        });

        AssertInvalidSkillSnapshot(snapshot, "battle_skill_effects_missing", "empty effect list");
    }

    private static void InvalidSkillEffectKindRemainsIncomplete()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshotWithSkill("battle_invalid_skill_effect_kind", new BattleSkillSnapshot
        {
            SkillId = "invalid_effect_kind_skill",
            DisplayName = "Invalid Effect Kind Skill",
            TargetingMode = BattleSkillTargetingMode.TargetedActor,
            Range = 4,
            CasterUnitIds = { "force_player_hero_definition" },
            Effects =
            {
                new BattleSkillEffectSnapshot
                {
                    Kind = (BattleSkillEffectKind)999,
                    Amount = 12
                }
            }
        });

        AssertInvalidSkillSnapshot(snapshot, "battle_skill_effect_kind_invalid", "invalid effect kind");
    }

    private static void InvalidChanneledEffectPayloadRemainsIncomplete()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshotWithSkill("battle_invalid_channel_payload", new BattleSkillSnapshot
        {
            SkillId = "invalid_channel_skill",
            DisplayName = "Invalid Channel Skill",
            TargetingMode = BattleSkillTargetingMode.TargetedCell,
            Range = 4,
            CasterUnitIds = { "force_player_hero_definition" },
            Effects =
            {
                new BattleSkillEffectSnapshot
                {
                    Kind = BattleSkillEffectKind.StartChanneledAreaDamage,
                    Amount = 10,
                    DurationSeconds = 0,
                    TickIntervalSeconds = 0.2,
                    Radius = 0
                }
            }
        });

        AssertInvalidSkillSnapshot(snapshot, "battle_skill_effect_channel_timing_invalid", "channeled effect timing");
    }

    private static void DuplicateActorIdsFailWithNamedInvariant()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildDuplicateActorIdSnapshot());
        BattleRuntimeActor[] corps = controller.State.Actors
            .Where(item => item.Kind == BattleRuntimeActorKind.Corps)
            .Take(2)
            .ToArray();
        corps[1].ActorId = corps[0].ActorId;

        InvalidOperationException exception = AssertThrows<InvalidOperationException>(
            () => controller.AdvanceNextTick(),
            "duplicate runtime actor id should fail at the Runtime invariant boundary");
        AssertTrue(
            exception.Message.Contains("duplicate runtime actor id", StringComparison.Ordinal),
            $"duplicate actor invariant should name the corrupt state: actual={exception.Message}");
    }

    private static BattleStartSnapshot BuildMovementCancellationSnapshot()
    {
        BattleGroupSnapshot mover = BuildGroup(
            "group_player_mover",
            "player_mover",
            "player",
            BattleGroupTacticalMode.PlayerCommanded,
            0,
            0);
        mover.InitialCorpsCommandId = "Assault";

        BattleGroupSnapshot playerAnchor = BuildGroup(
            "group_player_anchor",
            "player_anchor",
            "player",
            BattleGroupTacticalMode.PlayerCommanded,
            0,
            1);
        playerAnchor.InitialCorpsCommandId = "HoldLine";

        BattleGroupSnapshot enemy = BuildGroup(
            "group_enemy",
            "enemy_target",
            "enemy",
            BattleGroupTacticalMode.EnemyOffense,
            6,
            0);
        enemy.InitialCorpsCommandId = "HoldLine";

        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_movement_cancel_defeated",
            BattleId = "battle_movement_cancel_defeated",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                mover,
                playerAnchor,
                enemy
            }
        };
        AddRectSurfaces(snapshot, 0, 0, 6, 1);
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
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
                BuildGroup("group_player", "force_player", "player", BattleGroupTacticalMode.PlayerCommanded, 0, 0),
                BuildGroup("group_enemy", "force_enemy", "enemy", BattleGroupTacticalMode.EnemyOffense, 4, 0)
            }
        };
        AddRectSurfaces(snapshot, 0, 0, 4, 0);
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildOpposedSnapshotWithSkill(
        string battleId,
        BattleSkillSnapshot skill,
        bool authoredInterruptPolicy = true)
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = $"snapshot_{battleId}",
            BattleId = battleId,
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("group_player", "force_player", "player", BattleGroupTacticalMode.PlayerCommanded, 0, 0),
                BuildGroup("group_enemy", "force_enemy", "enemy", BattleGroupTacticalMode.EnemyOffense, 4, 0)
            }
        };
        skill.HasInterruptPolicy = authoredInterruptPolicy;
        snapshot.SkillDefinitions.Add(skill);
        AddRectSurfaces(snapshot, 0, 0, 4, 0);
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildDuplicateActorIdSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_duplicate_actor_id",
            BattleId = "battle_duplicate_actor_id",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("duplicate_group", "duplicate_force_a", "player", BattleGroupTacticalMode.PlayerCommanded, 0, 0),
                BuildGroup("duplicate_group", "duplicate_force_b", "player", BattleGroupTacticalMode.PlayerCommanded, 0, 1),
                BuildGroup("enemy_group", "enemy_force", "enemy", BattleGroupTacticalMode.EnemyOffense, 1, 0)
            }
        };
        AddRectSurfaces(snapshot, 0, 0, 1, 1);
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildDiagonalHeightStepSnapshot()
    {
        BattleGroupSnapshot player = BuildGroup(
            "group_height_player",
            "height_player",
            "player",
            BattleGroupTacticalMode.PlayerCommanded,
            0,
            0);
        player.CellHeight = 0;

        BattleGroupSnapshot enemy = BuildGroup(
            "group_height_enemy",
            "height_enemy",
            "enemy",
            BattleGroupTacticalMode.EnemyOffense,
            2,
            1);
        enemy.CellHeight = 1;
        enemy.InitialCorpsCommandId = "HoldLine";

        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_diagonal_height_source_sides",
            BattleId = "battle_diagonal_height_source_sides",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                player,
                enemy
            }
        };
        snapshot.LocationContext.NavigationSurfaces.AddRange(new[]
        {
            new BattleNavigationSurfaceSnapshot { X = 0, Y = 0, Height = 0, MoveCost = 1 },
            new BattleNavigationSurfaceSnapshot { X = 1, Y = 0, Height = 0, MoveCost = 1 },
            new BattleNavigationSurfaceSnapshot { X = 0, Y = 1, Height = 0, MoveCost = 1 },
            new BattleNavigationSurfaceSnapshot { X = 1, Y = 1, Height = 1, MoveCost = 1 },
            new BattleNavigationSurfaceSnapshot { X = 2, Y = 1, Height = 1, MoveCost = 1 }
        });
        snapshot.LocationContext.NavigationConnections.Add(new BattleNavigationConnectionSnapshot
        {
            FromX = 0,
            FromY = 0,
            FromHeight = 0,
            ToX = 1,
            ToY = 1,
            ToHeight = 1,
            MoveCost = 1
        });
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildScopedStaleRetargetSnapshot()
    {
        BattleGroupSnapshot killer = BuildGroup(
            "group_player_killer",
            "player_killer",
            "player",
            BattleGroupTacticalMode.PlayerCommanded,
            3,
            0);
        killer.InitialCorpsCommandId = "FocusFire";
        killer.AttackDamage = 1;

        BattleGroupSnapshot mover = BuildGroup(
            "group_player_mover",
            "player_mover",
            "player",
            BattleGroupTacticalMode.PlayerCommanded,
            0,
            0);
        mover.RuntimeCommanderGroupId = "group_player_mover";
        mover.InitialCorpsCommandId = "FocusFire";

        BattleGroupSnapshot weak = BuildGroup(
            "group_enemy_weak",
            "enemy_weak",
            "enemy",
            BattleGroupTacticalMode.EnemyOffense,
            4,
            0);
        weak.InitialCorpsCommandId = "HoldLine";
        weak.CorpsStrength = 1;
        weak.MaxHitPoints = 1;

        BattleGroupSnapshot live = BuildGroup(
            "group_enemy_live",
            "enemy_live",
            "enemy",
            BattleGroupTacticalMode.EnemyOffense,
            5,
            0);
        live.InitialCorpsCommandId = "HoldLine";
        live.CorpsStrength = 80;
        live.MaxHitPoints = 80;

        BattleGroupSnapshot lure = BuildGroup(
            "group_enemy_lure",
            "enemy_lure",
            "enemy",
            BattleGroupTacticalMode.EnemyOffense,
            0,
            10);
        lure.InitialCorpsCommandId = "HoldLine";
        lure.CorpsStrength = 2;
        lure.MaxHitPoints = 2;

        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_scoped_stale_retarget",
            BattleId = "battle_scoped_stale_retarget",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                killer,
                mover,
                weak,
                live,
                lure
            }
        };
        AddRectSurfaces(snapshot, 0, 0, 5, 10);
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildLocalCombatFactionNormalizationSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_local_combat_faction_normalization",
            BattleId = "battle_local_combat_faction_normalization",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("support_group", "support", "player", BattleGroupTacticalMode.PlayerCommanded, 0, 0),
                BuildGroup("front_group", "front", "player", BattleGroupTacticalMode.PlayerCommanded, 2, 0),
                BuildGroup("target_group", "target", "enemy", BattleGroupTacticalMode.EnemyOffense, 3, 0)
            }
        };
        snapshot.BattleGroups.Single(item => item.BattleGroupId == "target_group").InitialCorpsCommandId = "HoldLine";
        AddRectSurfaces(snapshot, 0, 0, 3, 0);
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleGroupSnapshot BuildGroup(
        string groupId,
        string sourceForceId,
        string factionId,
        BattleGroupTacticalMode tacticalMode,
        int x,
        int y,
        BattleTacticalRegionSnapshot? initialRegion = null)
    {
        BattleGroupSnapshot group = new()
        {
            BattleGroupId = groupId,
            FactionId = factionId,
            SourceForceId = sourceForceId,
            HeroId = $"{sourceForceId}_hero",
            HeroDefinitionId = $"{sourceForceId}_hero_definition",
            CorpsId = $"{sourceForceId}_corps",
            CorpsDefinitionId = $"{sourceForceId}_corps_definition",
            CorpsStrength = 80,
            MaxHitPoints = 80,
            AttackDamage = 1,
            AttackRange = 1,
            AttackSpeed = 1.0,
            AttackImpactDelaySeconds = 0,
            SourceLocationId = factionId == "player" ? "city_1" : "site_1",
            CellX = x,
            CellY = y,
            TacticalMode = tacticalMode
        };
        if (initialRegion != null)
        {
            group.InitialTacticalRegions.Add(initialRegion);
        }

        return group;
    }

    private static BattleTacticalRegionSnapshot BuildFixedTargetRegion(
        string regionId,
        string ownerBattleGroupId,
        int centerX,
        int centerY,
        int width,
        int height)
    {
        return new BattleTacticalRegionSnapshot
        {
            RegionId = regionId,
            OwnerBattleGroupId = ownerBattleGroupId,
            Kind = BattleTacticalRegionKind.FixedTarget,
            CenterCellX = centerX,
            CenterCellY = centerY,
            CenterCellHeight = 0,
            Width = width,
            Height = height
        };
    }

    private static void AddRectSurfaces(BattleStartSnapshot snapshot, int minX, int minY, int maxX, int maxY)
    {
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                snapshot.LocationContext.NavigationSurfaces.Add(new BattleNavigationSurfaceSnapshot
                {
                    X = x,
                    Y = y,
                    Height = 0,
                    MoveCost = 1
                });
            }
        }
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

    private static void AssertInvalidSkillSnapshot(BattleStartSnapshot snapshot, string expectedReason, string message)
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);

        AssertTrue(!controller.IsComplete, $"{message} handoff should stay incomplete");
        AssertTrue(!controller.Outcome.IsComplete, $"{message} outcome should stay incomplete");
        AssertTrue(
            controller.EventStream.Events.Any(item =>
                item.Kind == BattleEventKind.CommandRejected &&
                item.SourceDefinitionId == snapshot.SkillDefinitions[0].SkillId &&
                item.ReasonCode == expectedReason),
            $"{message} should emit explicit skill snapshot rejection reason");
    }

    private static void AssertInvalidGroupSnapshot(BattleStartSnapshot snapshot, string expectedReason, string message)
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);

        AssertTrue(!controller.IsComplete, $"{message} handoff should stay incomplete");
        AssertTrue(!controller.Outcome.IsComplete, $"{message} outcome should stay incomplete");
        AssertTrue(
            controller.EventStream.Events.Any(item =>
                item.Kind == BattleEventKind.CommandRejected &&
                item.BattleGroupId == snapshot.BattleGroups[0].BattleGroupId &&
                item.ReasonCode == expectedReason),
            $"{message} should emit explicit battle group snapshot rejection reason");
    }

    private static T AssertThrows<T>(Action action, string message) where T : Exception
    {
        try
        {
            action();
        }
        catch (T exception)
        {
            return exception;
        }
        catch (Exception exception)
        {
            throw new Exception($"{message}: expected={typeof(T).Name} actual={exception.GetType().Name} detail={exception.Message}");
        }

        throw new Exception($"{message}: expected={typeof(T).Name} but no exception was thrown");
    }

    private static string DescribeEvents(IEnumerable<BattleEvent> events)
    {
        return string.Join(
            "|",
            (events ?? Array.Empty<BattleEvent>())
            .Select(item => $"{item.Kind}:{item.ActorId}->{item.TargetId}:{item.ReasonCode}:to={item.ToGridX},{item.ToGridY}"));
    }

    private sealed class RecordingBattleRuntimeAiExecutor : IBattleRuntimeAiExecutor
    {
        private readonly IBattleRuntimeAiExecutor _inner;

        internal RecordingBattleRuntimeAiExecutor(IBattleRuntimeAiExecutor inner)
        {
            _inner = inner;
        }

        internal List<BattleRuntimeAiDecisionFacts> SeenFacts { get; } = new();

        public BattleRuntimeAiActionRequest ChooseAction(BattleRuntimeAiDecisionFacts facts)
        {
            SeenFacts.Add(facts);
            return _inner.ChooseAction(facts);
        }
    }
}
