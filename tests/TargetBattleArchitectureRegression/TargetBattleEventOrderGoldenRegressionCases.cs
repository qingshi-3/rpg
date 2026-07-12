using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;

internal static partial class TargetBattleEventOrderGoldenRegressionCases
{
    internal static void RuntimeEventStreamOrderGoldenLocksAttackMovementPlanAndDefeat()
    {
        BattleStartSnapshot snapshot = BuildEventOrderGoldenSnapshot();

        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        string[] actualEventIds = result.EventStream.EventIds.ToArray();
        string[] actualStableProjection = result.EventStream.Events
            .Select(ToStableProjection)
            .ToArray();

        // This golden freezes actor execution events plus commander transitions
        // folded once per battle group after resolution.
        string[] expectedEventIds =
        {
            "battle_event_order_golden:started",
            "battle_event_order_golden:group_player_a:hero:spawned",
            "battle_event_order_golden:force_player_a:1:spawned",
            "battle_event_order_golden:group_player_b:hero:spawned",
            "battle_event_order_golden:force_player_b:1:spawned",
            "battle_event_order_golden:group_enemy_front:hero:spawned",
            "battle_event_order_golden:force_enemy_front:1:spawned",
            "battle_event_order_golden:group_enemy_rear:hero:spawned",
            "battle_event_order_golden:force_enemy_rear:1:spawned",
            "battle_event_order_golden:group_player_a:initial_command",
            "battle_event_order_golden:group_player_b:initial_command",
            "battle_event_order_golden:group_player_a:initial_plan",
            "battle_event_order_golden:group_player_b:initial_plan",
            "battle_event_order_golden:tick_0:force_enemy_front:1:attack:force_player_a:1",
            "battle_event_order_golden:tick_0:force_player_a:1:attack:force_enemy_front:1",
            "battle_event_order_golden:tick_0:force_player_b:1:attack:force_enemy_front:1",
            "battle_event_order_golden:tick_0:force_enemy_rear:1:move_start",
            "battle_event_order_golden:tick_0:group_enemy_front:plan:Defeated",
            "battle_event_order_golden:tick_0:group_enemy_rear:plan:MovingToAttackSlot",
            "battle_event_order_golden:tick_0:group_player_a:plan:Attacking",
            "battle_event_order_golden:tick_0:group_player_b:plan:Attacking",
            "battle_event_order_golden:tick_1:force_enemy_rear:1:move_complete",
            "battle_event_order_golden:tick_2:force_enemy_rear:1:attack:force_player_b:1",
            "battle_event_order_golden:tick_2:group_enemy_rear:plan:Attacking",
            "battle_event_order_golden:tick_3:force_player_b:1:attack:force_enemy_rear:1",
            "battle_event_order_golden:tick_3:group_enemy_rear:plan:Defeated",
            "battle_event_order_golden:tick_3:group_player_a:plan:TargetLocked",
            "battle_event_order_golden:ended"
        };
        string[] expectedStableProjection =
        {
            "-1:BattleStarted:->:",
            "-1:RuntimeActorSpawned:group_player_a:hero->:runtime_actor_spawned",
            "-1:RuntimeActorSpawned:force_player_a:1->:runtime_actor_spawned",
            "-1:RuntimeActorSpawned:group_player_b:hero->:runtime_actor_spawned",
            "-1:RuntimeActorSpawned:force_player_b:1->:runtime_actor_spawned",
            "-1:RuntimeActorSpawned:group_enemy_front:hero->:runtime_actor_spawned",
            "-1:RuntimeActorSpawned:force_enemy_front:1->:runtime_actor_spawned",
            "-1:RuntimeActorSpawned:group_enemy_rear:hero->:runtime_actor_spawned",
            "-1:RuntimeActorSpawned:force_enemy_rear:1->:runtime_actor_spawned",
            "-1:CommandAccepted:->:Assault",
            "-1:CommandAccepted:->:Assault",
            "-1:BattleGroupPlanAccepted:->:AttackFirst",
            "-1:BattleGroupPlanAccepted:->:AttackFirst",
            "0:DamageApplied:force_enemy_front:1->force_player_a:1:auto_attack",
            "0:DamageApplied:force_player_a:1->force_enemy_front:1:auto_attack",
            "0:DamageApplied:force_player_b:1->force_enemy_front:1:auto_attack_target_defeated",
            "0:MovementStarted:force_enemy_rear:1->force_player_b:1:join_recent_damage",
            "0:BattleGroupPlanStateChanged:->:defeated",
            "0:BattleGroupPlanStateChanged:->:moving_to_attack_slot",
            "0:BattleGroupPlanStateChanged:->:attacking",
            "0:BattleGroupPlanStateChanged:->:attacking",
            "1:MovementCompleted:force_enemy_rear:1->force_player_b:1:movement_committed",
            "2:DamageApplied:force_enemy_rear:1->force_player_b:1:auto_attack",
            "2:BattleGroupPlanStateChanged:->:attacking",
            "3:DamageApplied:force_player_b:1->force_enemy_rear:1:auto_attack_target_defeated",
            "3:BattleGroupPlanStateChanged:->:defeated",
            "3:BattleGroupPlanStateChanged:->:target_locked",
            "4:BattleEnded:->:NormalVictory"
        };

        AssertSequence(
            expectedEventIds,
            actualEventIds,
            "event id order golden");
        AssertSequence(
            expectedStableProjection,
            actualStableProjection,
            "stable event projection order golden");
    }

    private static BattleStartSnapshot BuildEventOrderGoldenSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_battle_event_order_golden",
            BattleId = "battle_event_order_golden",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("group_player_a", "player", "force_player_a", "hero_player_a", "corps_player_a", 0, 0, hitPoints: 30, damage: 10),
                BuildGroup("group_player_b", "player", "force_player_b", "hero_player_b", "corps_player_b", 1, 1, hitPoints: 30, damage: 10),
                BuildGroup("group_enemy_front", "enemy", "force_enemy_front", "hero_enemy_front", "corps_enemy_front", 1, 0, hitPoints: 15, damage: 4),
                BuildGroup("group_enemy_rear", "enemy", "force_enemy_rear", "hero_enemy_rear", "corps_enemy_rear", 2, 0, hitPoints: 10, damage: 4)
            }
        };
        TargetBattleTestTopology.CompileAroundGroups(snapshot);
        return snapshot;
    }

    private static BattleGroupSnapshot BuildGroup(
        string groupId,
        string factionId,
        string sourceForceId,
        string heroId,
        string corpsId,
        int cellX,
        int cellY,
        int hitPoints,
        int damage)
    {
        return new BattleGroupSnapshot
        {
            BattleGroupId = groupId,
            FactionId = factionId,
            SourceForceId = sourceForceId,
            HeroId = heroId,
            HeroDefinitionId = $"{heroId}_definition",
            CorpsId = corpsId,
            CorpsDefinitionId = $"{corpsId}_definition",
            CorpsStrength = 100,
            SourceLocationId = factionId == "player" ? "city_1" : "site_1",
            CellX = cellX,
            CellY = cellY,
            MaxHitPoints = hitPoints,
            AttackDamage = damage,
            AttackRange = 1,
            AttackSpeed = 1.0,
            MoveStepSeconds = 0.16,
            AttackActionSeconds = 1.2,
            AttackImpactDelaySeconds = 0
        };
    }

    private static string ToStableProjection(BattleEvent battleEvent)
    {
        return $"{battleEvent.RuntimeTick}:{battleEvent.Kind}:{battleEvent.ActorId}->{battleEvent.TargetId}:{battleEvent.ReasonCode}";
    }

    private static void AssertSequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string message)
    {
        if (!expected.SequenceEqual(actual))
        {
            throw new Exception($"{message}: expected=[{string.Join(",", expected)}] actual=[{string.Join(",", actual)}]");
        }
    }
}
