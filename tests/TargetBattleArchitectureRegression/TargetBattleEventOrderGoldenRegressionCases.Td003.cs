using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Events;

internal static partial class TargetBattleEventOrderGoldenRegressionCases
{
    // Locks same-tick multi-defeat event order. The snapshot inserts battle groups in
    // near-reverse-alphabetical order (force_z before force_a) on purpose: if defeated
    // order followed snapshot insertion, the golden would read force_z -> force_a, but it
    // reads force_a -> force_z, proving the runtime re-sorts.
    //
    // NOTE ON THE TRUE ORDERING SOURCE (the name "dictionary order" is a misnomer):
    // defeated events iterate postAttackHitPoints, a Dictionary<string,int> built from
    // tickStartFacts.Values (BattleRuntimeTickResolver.cs:626-629, :684-702). A Dictionary's
    // StringComparer.Ordinal does NOT order enumeration; enumeration follows insertion order
    // for an add-only dictionary. The alphabetical result actually comes from upstream:
    // livingCorps is explicitly OrderBy(ActorId, Ordinal) in
    // BattleRuntimeTickResolver.Perception.cs (~:18-21), and that order flows into
    // tickStartFacts -> postAttackHitPoints. This golden therefore guards two things: the
    // upstream ordinal sort, and the dictionary staying add-only (no Remove). Removing either
    // would change this sequence and fail here, which is the intended regression signal.
    internal static void RuntimeMultiDefeatDictionaryOrderGoldenLocksCurrentOrder()
    {
        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(BuildMultiDefeatDictionaryOrderSnapshot());

        string[] expectedEventIds =
        {
            "battle_multi_defeat_dictionary_order_golden:started",
            "battle_multi_defeat_dictionary_order_golden:group_enemy_z:hero:spawned",
            "battle_multi_defeat_dictionary_order_golden:force_z:1:spawned",
            "battle_multi_defeat_dictionary_order_golden:group_enemy_a:hero:spawned",
            "battle_multi_defeat_dictionary_order_golden:force_a:1:spawned",
            "battle_multi_defeat_dictionary_order_golden:group_player_z:hero:spawned",
            "battle_multi_defeat_dictionary_order_golden:force_player_z:1:spawned",
            "battle_multi_defeat_dictionary_order_golden:group_player_a:hero:spawned",
            "battle_multi_defeat_dictionary_order_golden:force_player_a:1:spawned",
            "battle_multi_defeat_dictionary_order_golden:force_player_z:1:initial_command",
            "battle_multi_defeat_dictionary_order_golden:force_player_a:1:initial_command",
            "battle_multi_defeat_dictionary_order_golden:force_player_z:1:initial_plan",
            "battle_multi_defeat_dictionary_order_golden:force_player_a:1:initial_plan",
            "battle_multi_defeat_dictionary_order_golden:tick_0:force_a:1:plan:TargetLocked",
            "battle_multi_defeat_dictionary_order_golden:tick_0:force_player_a:1:plan:TargetLocked",
            "battle_multi_defeat_dictionary_order_golden:tick_0:force_player_z:1:plan:TargetLocked",
            "battle_multi_defeat_dictionary_order_golden:tick_0:force_z:1:plan:TargetLocked",
            "battle_multi_defeat_dictionary_order_golden:tick_0:force_a:1:attack:force_player_a:1",
            "battle_multi_defeat_dictionary_order_golden:tick_0:force_player_a:1:attack:force_a:1",
            "battle_multi_defeat_dictionary_order_golden:tick_0:force_player_z:1:attack:force_z:1",
            "battle_multi_defeat_dictionary_order_golden:tick_0:force_z:1:attack:force_player_z:1",
            "battle_multi_defeat_dictionary_order_golden:tick_0:force_a:1:plan:Attacking",
            "battle_multi_defeat_dictionary_order_golden:tick_0:force_player_a:1:plan:Attacking",
            "battle_multi_defeat_dictionary_order_golden:tick_0:force_player_z:1:plan:Attacking",
            "battle_multi_defeat_dictionary_order_golden:tick_0:force_z:1:plan:Attacking",
            "battle_multi_defeat_dictionary_order_golden:tick_0:force_a:1:plan:Defeated",
            "battle_multi_defeat_dictionary_order_golden:tick_0:force_z:1:plan:Defeated",
            "battle_multi_defeat_dictionary_order_golden:ended"
        };
        string[] expectedStableProjection =
        {
            "-1:BattleStarted:->:",
            "-1:RuntimeActorSpawned:group_enemy_z:hero->:runtime_actor_spawned",
            "-1:RuntimeActorSpawned:force_z:1->:runtime_actor_spawned",
            "-1:RuntimeActorSpawned:group_enemy_a:hero->:runtime_actor_spawned",
            "-1:RuntimeActorSpawned:force_a:1->:runtime_actor_spawned",
            "-1:RuntimeActorSpawned:group_player_z:hero->:runtime_actor_spawned",
            "-1:RuntimeActorSpawned:force_player_z:1->:runtime_actor_spawned",
            "-1:RuntimeActorSpawned:group_player_a:hero->:runtime_actor_spawned",
            "-1:RuntimeActorSpawned:force_player_a:1->:runtime_actor_spawned",
            "-1:CommandAccepted:force_player_z:1->:Assault",
            "-1:CommandAccepted:force_player_a:1->:Assault",
            "-1:BattleGroupPlanAccepted:force_player_z:1->:AttackFirst",
            "-1:BattleGroupPlanAccepted:force_player_a:1->:AttackFirst",
            "0:BattleGroupPlanStateChanged:force_a:1->:target_locked",
            "0:BattleGroupPlanStateChanged:force_player_a:1->:target_locked",
            "0:BattleGroupPlanStateChanged:force_player_z:1->:target_locked",
            "0:BattleGroupPlanStateChanged:force_z:1->:target_locked",
            "0:DamageApplied:force_a:1->force_player_a:1:auto_attack",
            "0:DamageApplied:force_player_a:1->force_a:1:auto_attack_target_defeated",
            "0:DamageApplied:force_player_z:1->force_z:1:auto_attack_target_defeated",
            "0:DamageApplied:force_z:1->force_player_z:1:auto_attack",
            "0:BattleGroupPlanStateChanged:force_a:1->:attacking",
            "0:BattleGroupPlanStateChanged:force_player_a:1->:attacking",
            "0:BattleGroupPlanStateChanged:force_player_z:1->:attacking",
            "0:BattleGroupPlanStateChanged:force_z:1->:attacking",
            "0:BattleGroupPlanStateChanged:force_a:1->:defeated",
            "0:BattleGroupPlanStateChanged:force_z:1->:defeated",
            "1:BattleEnded:->:NormalVictory"
        };

        AssertSequence(expectedEventIds, result.EventStream.EventIds, "multi-defeat event id order golden");
        AssertSequence(expectedStableProjection, result.EventStream.Events.Select(ToStableProjection).ToArray(), "multi-defeat stable projection order golden");
    }

    internal static void RuntimeAttackStreamSliceGoldenLocksEngagementBeforeMovement()
    {
        BattleRuntimeAdvanceResult tick = new BattleRuntimeSession()
            .Begin(BuildAttackStreamSliceSnapshot())
            .AdvanceNextTick();

        string[] expectedEventIds =
        {
            "battle_attack_stream_slice_golden:tick_0:player_mover_slice:1:plan:TargetLocked",
            "battle_attack_stream_slice_golden:tick_0:player_ranged:1:plan:TargetLocked",
            "battle_attack_stream_slice_golden:tick_0:player_ranged:1:attack:enemy_hold:1",
            "battle_attack_stream_slice_golden:tick_0:player_ranged:1:plan:Attacking",
            "battle_attack_stream_slice_golden:tick_0:enemy_hold:1:plan:Defeated",
            "battle_attack_stream_slice_golden:tick_0:group_enemy_hold:engagement:engagement_enter_member_damaged",
            "battle_attack_stream_slice_golden:tick_0:player_mover_slice:1:plan:MovingToAttackSlot",
            "battle_attack_stream_slice_golden:tick_0:player_mover_slice:1:move_start"
        };
        string[] expectedStableProjection =
        {
            "0:BattleGroupPlanStateChanged:player_mover_slice:1->:target_locked",
            "0:BattleGroupPlanStateChanged:player_ranged:1->:target_locked",
            "0:DamageApplied:player_ranged:1->enemy_hold:1:auto_attack_target_defeated",
            "0:BattleGroupPlanStateChanged:player_ranged:1->:attacking",
            "0:BattleGroupPlanStateChanged:enemy_hold:1->:defeated",
            "0:BattleGroupEngagementStateChanged:enemy_hold:1->player_ranged:1:engagement_enter_member_damaged",
            "0:BattleGroupPlanStateChanged:player_mover_slice:1->:moving_to_attack_slot",
            "0:MovementStarted:player_mover_slice:1->enemy_live_slice:1:auto_advance"
        };

        AssertSequence(expectedEventIds, tick.Events.Select(item => item.EventId).ToArray(), "attack stream slice event id order golden");
        AssertSequence(expectedStableProjection, tick.Events.Select(ToStableProjection).ToArray(), "attack stream slice stable projection order golden");
        AssertTrue(
            IndexOfKind(tick.Events, BattleEventKind.BattleGroupEngagementStateChanged) <
            IndexOfKind(tick.Events, BattleEventKind.MovementStarted),
            "engagement trigger should be appended before movement starts");
    }

    internal static void RuntimeRetargetOrderGoldenLocksDeadTargetRetargetWithoutStreamWrites()
    {
        BattleRuntimeAdvanceResult tick = new BattleRuntimeSession()
            .Begin(TargetBattleMovementIntentRegressionCases.BuildSameTickTargetDeathRetargetSnapshot())
            .AdvanceNextTick();

        string[] expectedEventIds =
        {
            "battle_same_tick_target_death_retarget:tick_0:enemy_weak:1:plan:TargetLocked",
            "battle_same_tick_target_death_retarget:tick_0:player_killer:1:plan:TargetLocked",
            "battle_same_tick_target_death_retarget:tick_0:player_mover:1:plan:TargetLocked",
            "battle_same_tick_target_death_retarget:tick_0:enemy_weak:1:attack:player_killer:1",
            "battle_same_tick_target_death_retarget:tick_0:player_killer:1:attack:enemy_weak:1",
            "battle_same_tick_target_death_retarget:tick_0:enemy_weak:1:plan:Attacking",
            "battle_same_tick_target_death_retarget:tick_0:player_killer:1:plan:Attacking",
            "battle_same_tick_target_death_retarget:tick_0:enemy_weak:1:plan:Defeated",
            "battle_same_tick_target_death_retarget:tick_0:player_mover:1:plan:MovingToAttackSlot",
            "battle_same_tick_target_death_retarget:tick_0:player_mover:1:move_start"
        };
        string[] expectedStableProjection =
        {
            "0:BattleGroupPlanStateChanged:enemy_weak:1->:target_locked",
            "0:BattleGroupPlanStateChanged:player_killer:1->:target_locked",
            "0:BattleGroupPlanStateChanged:player_mover:1->:target_locked",
            "0:DamageApplied:enemy_weak:1->player_killer:1:auto_attack",
            "0:DamageApplied:player_killer:1->enemy_weak:1:auto_attack_target_defeated",
            "0:BattleGroupPlanStateChanged:enemy_weak:1->:attacking",
            "0:BattleGroupPlanStateChanged:player_killer:1->:attacking",
            "0:BattleGroupPlanStateChanged:enemy_weak:1->:defeated",
            "0:BattleGroupPlanStateChanged:player_mover:1->:moving_to_attack_slot",
            "0:MovementStarted:player_mover:1->enemy_live:1:auto_advance"
        };

        AssertSequence(expectedEventIds, tick.Events.Select(item => item.EventId).ToArray(), "retarget event id order golden");
        AssertSequence(expectedStableProjection, tick.Events.Select(ToStableProjection).ToArray(), "retarget stable projection order golden");
        AssertSequence(
            new[] { "enemy_live:1" },
            tick.Events
                .Where(item => item.Kind == BattleEventKind.MovementStarted && item.ActorId == "player_mover:1")
                .Select(item => item.TargetId)
                .ToArray(),
            "retarget movement target should be the live target");
    }

    internal static void RuntimeReservationBattleGroupIdTiebreakGoldenLocksMoveOrder()
    {
        BattleRuntimeAdvanceResult tick = new BattleRuntimeSession()
            .Begin(TargetBattleCongestionRegressionCases.BuildSameTickAlternateReservationSnapshot())
            .AdvanceNextTick();

        string[] expectedEventIds =
        {
            "battle_same_tick_alternate_reservation:tick_0:player_bottom:1:plan:TargetLocked",
            "battle_same_tick_alternate_reservation:tick_0:player_top:1:plan:TargetLocked",
            "battle_same_tick_alternate_reservation:tick_0:player_top:1:plan:MovingToAttackSlot",
            "battle_same_tick_alternate_reservation:tick_0:player_top:1:move_start",
            "battle_same_tick_alternate_reservation:tick_0:player_bottom:1:plan:MovingToAttackSlot",
            "battle_same_tick_alternate_reservation:tick_0:player_bottom:1:move_start"
        };
        string[] expectedStableProjection =
        {
            "0:BattleGroupPlanStateChanged:player_bottom:1->:target_locked",
            "0:BattleGroupPlanStateChanged:player_top:1->:target_locked",
            "0:BattleGroupPlanStateChanged:player_top:1->:moving_to_attack_slot",
            "0:MovementStarted:player_top:1->enemy:1:auto_advance",
            "0:BattleGroupPlanStateChanged:player_bottom:1->:moving_to_attack_slot",
            "0:MovementStarted:player_bottom:1->enemy:1:auto_advance"
        };

        AssertSequence(expectedEventIds, tick.Events.Select(item => item.EventId).ToArray(), "reservation tiebreak event id order golden");
        AssertSequence(expectedStableProjection, tick.Events.Select(ToStableProjection).ToArray(), "reservation tiebreak stable projection order golden");
        AssertSequence(
            new[] { "player_top:1", "player_bottom:1" },
            tick.Events.Where(item => item.Kind == BattleEventKind.MovementStarted).Select(item => item.ActorId).ToArray(),
            "reservation movement order is deterministic: candidates sort by gap, then From.Height, From.Y, From.X, then BattleGroupId ordinal; here player_top (From.Y=0) precedes player_bottom (From.Y=1)");
    }

    internal static void RuntimeFailedAttackContextsGoldenLocksFailureResultsWithoutCombatEvents()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession(new ForcedAttackAiExecutor(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["force_out:1"] = "enemy_far:1",
                ["force_empty:1"] = "enemy_empty:1"
            }))
            .Begin(BuildFailedAttackContextsSnapshot());
        BattleRuntimeActor emptyChargeActor = controller.State.Actors.Single(item => item.ActorId == "force_empty:1");
        emptyChargeActor.AttackCharge = 0;
        emptyChargeActor.ActionReadyAtSeconds = 1.0;

        GameLog.SetTraceCategoryEnabled("BattleRuntimeTickResolver", true);
        BattleRuntimeAdvanceResult tick;
        try
        {
            tick = controller.AdvanceFixedTick(0.04);
        }
        finally
        {
            GameLog.SetTraceCategoryEnabled("BattleRuntimeTickResolver", false);
        }

        string[] expectedEventIds =
        {
            "battle_failed_attack_contexts:tick_0:force_empty:1:plan:TargetLocked",
            "battle_failed_attack_contexts:tick_0:force_out:1:plan:TargetLocked"
        };
        string[] expectedStableProjection =
        {
            "0:BattleGroupPlanStateChanged:force_empty:1->:target_locked",
            "0:BattleGroupPlanStateChanged:force_out:1->:target_locked"
        };
        string log = File.Exists(GameLog.CurrentLogPath) ? File.ReadAllText(GameLog.CurrentLogPath) : "";

        AssertSequence(expectedEventIds, tick.Events.Select(item => item.EventId).ToArray(), "failed attack event id order golden");
        AssertSequence(expectedStableProjection, tick.Events.Select(ToStableProjection).ToArray(), "failed attack stable projection order golden");
        AssertSequence(
            Array.Empty<string>(),
            tick.Events
                .Where(item => item.Kind is BattleEventKind.DamageApplied or BattleEventKind.MovementStarted or BattleEventKind.MovementCompleted)
                .Select(ToStableProjection)
                .ToArray(),
            "failed attack contexts should not produce damage or movement");
        AssertTrue(
            log.Contains("BattleRuntimeAction battle=battle_failed_attack_contexts tick=0 time=0.00 actor=force_out:1 action=AttackTarget outcome=target_out_of_range", StringComparison.Ordinal),
            "out-of-range attack should record target_out_of_range result");
        AssertTrue(
            log.Contains("BattleRuntimeAction battle=battle_failed_attack_contexts tick=0 time=0.00 actor=force_empty:1 action=AttackTarget outcome=attack_charge_empty", StringComparison.Ordinal),
            "empty-charge attack should record attack_charge_empty result");
    }

    private static BattleStartSnapshot BuildMultiDefeatDictionaryOrderSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_multi_defeat_dictionary_order_golden",
            BattleId = "battle_multi_defeat_dictionary_order_golden",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGoldenGroup("group_enemy_z", "enemy", "force_z", 0, 0, hitPoints: 1, damage: 1, initialCommandId: "HoldLine"),
                BuildGoldenGroup("group_enemy_a", "enemy", "force_a", 10, 0, hitPoints: 1, damage: 1, initialCommandId: "HoldLine"),
                BuildGoldenGroup("group_player_z", "player", "force_player_z", 1, 0, hitPoints: 20, damage: 1),
                BuildGoldenGroup("group_player_a", "player", "force_player_a", 9, 0, hitPoints: 20, damage: 1)
            }
        };

        AddRectSurfaces(snapshot, 0, 0, 10, 0);
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildAttackStreamSliceSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_attack_stream_slice_golden",
            BattleId = "battle_attack_stream_slice_golden",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGoldenGroup("group_player_ranged", "player", "player_ranged", 0, 0, hitPoints: 40, damage: 1, attackRange: 10),
                BuildGoldenGroup("group_enemy_hold", "enemy", "enemy_hold", 9, 0, hitPoints: 1, damage: 1, initialCommandId: "HoldLine", tacticalMode: BattleGroupTacticalMode.EnemyHoldDefense),
                BuildGoldenGroup("group_player_mover", "player", "player_mover_slice", 0, 2, hitPoints: 40, damage: 1),
                BuildGoldenGroup("group_enemy_live", "enemy", "enemy_live_slice", 4, 2, hitPoints: 40, damage: 1, initialCommandId: "HoldLine")
            }
        };

        AddRectSurfaces(snapshot, 0, 0, 9, 0);
        AddRectSurfaces(snapshot, 0, 2, 4, 2);
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildFailedAttackContextsSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_failed_attack_contexts",
            BattleId = "battle_failed_attack_contexts",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGoldenGroup("group_force_out", "player", "force_out", 0, 0, hitPoints: 40, damage: 1),
                BuildGoldenGroup("group_enemy_far", "enemy", "enemy_far", 3, 0, hitPoints: 40, damage: 1, initialCommandId: "HoldLine"),
                BuildGoldenGroup("group_force_empty", "player", "force_empty", 0, 2, hitPoints: 40, damage: 1),
                BuildGoldenGroup("group_enemy_empty", "enemy", "enemy_empty", 1, 2, hitPoints: 40, damage: 1, initialCommandId: "HoldLine")
            }
        };

        AddRectSurfaces(snapshot, 0, 0, 3, 0);
        AddRectSurfaces(snapshot, 0, 2, 1, 2);
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleGroupSnapshot BuildGoldenGroup(
        string groupId,
        string factionId,
        string sourceForceId,
        int cellX,
        int cellY,
        int hitPoints,
        int damage,
        int attackRange = 1,
        string initialCommandId = "",
        BattleGroupTacticalMode tacticalMode = BattleGroupTacticalMode.PlayerCommanded)
    {
        return new BattleGroupSnapshot
        {
            BattleGroupId = groupId,
            FactionId = factionId,
            SourceForceId = sourceForceId,
            HeroId = $"{sourceForceId}_hero",
            HeroDefinitionId = $"{sourceForceId}_hero_definition",
            CorpsId = $"{sourceForceId}_corps",
            CorpsDefinitionId = $"{sourceForceId}_corps_definition",
            CorpsStrength = hitPoints,
            MaxHitPoints = hitPoints,
            AttackDamage = damage,
            AttackRange = attackRange,
            AttackSpeed = 1.0,
            MoveStepSeconds = 0.16,
            AttackActionSeconds = 1.2,
            SourceLocationId = factionId == "player" ? "city_1" : "site_1",
            CellX = cellX,
            CellY = cellY,
            InitialCorpsCommandId = initialCommandId,
            TacticalMode = tacticalMode
        };
    }

    private static void AddRectSurfaces(BattleStartSnapshot snapshot, int minX, int minY, int maxX, int maxY)
    {
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
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

    private static int IndexOfKind(IReadOnlyList<BattleEvent> events, BattleEventKind kind)
    {
        if (events == null)
        {
            return -1;
        }

        for (int i = 0; i < events.Count; i++)
        {
            if (events[i].Kind == kind)
            {
                return i;
            }
        }

        return -1;
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }

    private sealed class ForcedAttackAiExecutor : IBattleRuntimeAiExecutor
    {
        private readonly IReadOnlyDictionary<string, string> _targets;

        public ForcedAttackAiExecutor(IReadOnlyDictionary<string, string> targets)
        {
            _targets = targets ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }

        public BattleRuntimeAiActionRequest ChooseAction(BattleRuntimeAiDecisionFacts facts)
        {
            if (facts != null && _targets.TryGetValue(facts.ActorId ?? "", out string? targetId))
            {
                return BattleRuntimeAiActionRequest.AttackTarget(facts.ActorId ?? "", targetId ?? "");
            }

            return BattleRuntimeAiActionRequest.Hold(facts?.ActorId ?? "", "golden_hold");
        }
    }
}
