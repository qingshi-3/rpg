using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;

internal static class TargetBattleLocalCombatPositionRegressionCases
{
    public static void Register(Action<string, Action> run)
    {
        run("runtime full local fight does not oscillate extra living unit", RuntimeFullLocalFightDoesNotOscillateExtraLivingUnit);
        run("runtime rear local combat unit routes before frontline defeat", RuntimeRearLocalCombatUnitRoutesBeforeFrontlineDefeat);
    }

    public static void RuntimeFullLocalFightDoesNotOscillateExtraLivingUnit()
    {
        BattleStartSnapshot snapshot = BuildFullLocalFightExtraUnitSnapshot();
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        BattleRuntimeActor extra = controller.State.Actors.Single(item => item.ActorId == "bonefield:f6_draugarlord:1");
        extra.TargetActorId = "expedition:player_camp:1:army:f1_azuritelion:2";
        extra.PlanState = BattleGroupPlanRuntimeState.MovingToAttackSlot;

        for (int i = 0; i < 80 && !controller.IsComplete; i++)
        {
            _ = controller.AdvanceNextTick();
        }

        AssertTrue(
            controller.State.Actors.All(item => item.HitPoints > 0),
            "fixture should reproduce the extra-unit movement problem before any actor is defeated");
        BattleEvent[] moves = controller.EventStream.Events
            .Where(item =>
                item.Kind == BattleEventKind.MovementStarted &&
                item.ActorId == "bonefield:f6_draugarlord:1")
            .ToArray();
        AssertTrue(
            moves.Length > 0 || extra.LastAdvanceFailureReason == "reject_no_reachable_slot",
            "extra living unit should either move toward a combat position or explicitly stop with a local-combat blockage reason");
        AssertNoImmediateReverseMoves(
            moves,
            "extra living unit must not oscillate between adjacent cells while every combatant is still alive");
    }

    public static void RuntimeRearLocalCombatUnitRoutesBeforeFrontlineDefeat()
    {
        BattleStartSnapshot snapshot = BuildBlockedCombatSlotRerouteSnapshot();
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        BattleRuntimeActor rear = controller.State.Actors.Single(item => item.ActorId == "bonefield:f6_draugarlord:3");
        rear.TargetActorId = "expedition:player_camp:1:army:f1_grandmasterzir:1";
        rear.PlanState = BattleGroupPlanRuntimeState.MovingToAttackSlot;
        rear.Phase = BattleRuntimeActorPhase.AnchoredDecision;
        rear.MotionState = BattleRuntimeActorMotionState.Anchored;
        rear.ActionReadyAtSeconds = 0;
        rear.AttackCharge = 1.0;
        rear.MovementFromGridX = 38;
        rear.MovementFromGridY = 18;
        rear.MovementFromGridHeight = 0;
        rear.MovementToGridX = 38;
        rear.MovementToGridY = 17;
        rear.MovementToGridHeight = 0;
        rear.MovementIntentKind = Rpg.Runtime.Battle.AI.BattleRuntimeAiActionKind.JoinLocalCombat;
        rear.MovementIntentTargetActorId = "expedition:player_camp:1:army:f1_grandmasterzir:1";
        rear.MovementIntentReasonCode = "join_recent_damage";
        rear.MovementIntentLocalCombatSituationId = "local:expedition:player_camp:1:army:f1_grandmasterzir:1";

        BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();

        AssertTrue(
            controller.State.Actors.All(item => item.HitPoints > 0),
            "reroute fixture must keep every actor alive while testing the blocked join path");
        BattleEvent? rearMove = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "bonefield:f6_draugarlord:3");
        AssertTrue(
            rearMove != null,
            $"rear unit should route around the live frontline before any unit dies instead of idling: failure={rear.LastAdvanceFailureReason}");
        AssertTrue(
            rear.LastAdvanceFailureReason != "reject_no_reachable_slot",
            "a live dynamic reroute must not be degraded as reject_no_reachable_slot");
    }

    private static BattleStartSnapshot BuildFullLocalFightExtraUnitSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_full_local_fight_extra_unit",
            BattleId = "battle_full_local_fight_extra_unit",
            TargetLocationId = "site_1",
            ObjectiveZones =
            {
                new BattleObjectiveZoneSnapshot
                {
                    ObjectiveZoneId = "player_deployment_zone_west_1",
                    ObjectiveRole = "player_deployment",
                    DeploymentSide = "Player",
                    FactionId = "player",
                    CellX = 10,
                    CellY = 16,
                    Width = 4,
                    Height = 8
                },
                new BattleObjectiveZoneSnapshot
                {
                    ObjectiveZoneId = "undead_deployment_zone_east_1",
                    ObjectiveRole = "enemy_deployment",
                    DeploymentSide = "Enemy",
                    FactionId = "undead",
                    CellX = 51,
                    CellY = 15,
                    Width = 20,
                    Height = 12
                }
            }
        };

        for (int x = 28; x <= 39; x++)
        {
            for (int y = 14; y <= 26; y++)
            {
                AddSurface(snapshot, x, y);
            }
        }

        AddPlannedGroup(snapshot, "player_unit_1", "player", "expedition:player_camp:1:army:f1_grandmasterzir", 32, 17, 600, "undead_deployment_zone_east_1", footprintWidth: 2, footprintHeight: 1);
        AddPlannedGroup(snapshot, "player_unit_2", "player", "expedition:player_camp:1:army:f1_azuritelion", 32, 20, 600, "undead_deployment_zone_east_1", footprintWidth: 2, footprintHeight: 1);
        AddPlannedGroup(snapshot, "player_unit_3", "player", "expedition:player_camp:1:army:f1_azuritelion", 33, 22, 600, "undead_deployment_zone_east_1", footprintWidth: 2, footprintHeight: 1);
        AddPlannedGroup(snapshot, "player_unit_4", "player", "expedition:player_camp:1:army:f1_azuritelion", 32, 21, 600, "undead_deployment_zone_east_1", footprintWidth: 2, footprintHeight: 1);
        AddPlannedGroup(snapshot, "enemy_unit_1", "undead", "bonefield:f6_draugarlord", 35, 18, 900, "player_deployment_zone_west_1", BattleGroupTacticalMode.EnemyOffense, footprintWidth: 2, footprintHeight: 2);
        AddPlannedGroup(snapshot, "enemy_unit_2", "undead", "bonefield:f6_draugarlord", 34, 20, 900, "player_deployment_zone_west_1", BattleGroupTacticalMode.EnemyOffense, footprintWidth: 2, footprintHeight: 2);
        AddPlannedGroup(snapshot, "enemy_unit_3", "undead", "bonefield:f6_draugarlord", 35, 22, 900, "player_deployment_zone_west_1", BattleGroupTacticalMode.EnemyOffense, footprintWidth: 2, footprintHeight: 2);
        AddPlannedGroup(snapshot, "enemy_unit_4", "undead", "bonefield:f6_draugarlord", 33, 18, 900, "player_deployment_zone_west_1", BattleGroupTacticalMode.EnemyOffense, footprintWidth: 2, footprintHeight: 2);

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildBlockedCombatSlotRerouteSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_blocked_combat_slot_reroute",
            BattleId = "battle_blocked_combat_slot_reroute",
            TargetLocationId = "site_1",
            ObjectiveZones =
            {
                new BattleObjectiveZoneSnapshot
                {
                    ObjectiveZoneId = "player_deployment_zone_west_1",
                    ObjectiveRole = "player_deployment",
                    DeploymentSide = "Player",
                    FactionId = "player",
                    CellX = 10,
                    CellY = 16,
                    Width = 4,
                    Height = 8
                },
                new BattleObjectiveZoneSnapshot
                {
                    ObjectiveZoneId = "undead_deployment_zone_east_1",
                    ObjectiveRole = "enemy_deployment",
                    DeploymentSide = "Enemy",
                    FactionId = "undead",
                    CellX = 51,
                    CellY = 15,
                    Width = 20,
                    Height = 12
                }
            }
        };

        for (int x = 31; x <= 42; x++)
        {
            for (int y = 12; y <= 23; y++)
            {
                AddSurface(snapshot, x, y);
            }
        }

        AddPlannedGroup(snapshot, "player_unit_1", "player", "expedition:player_camp:1:army:f1_grandmasterzir", 35, 16, 600, "undead_deployment_zone_east_1", footprintWidth: 2, footprintHeight: 1);
        AddPlannedGroup(snapshot, "player_unit_2", "player", "expedition:player_camp:1:army:f1_azuritelion", 34, 17, 600, "undead_deployment_zone_east_1", footprintWidth: 2, footprintHeight: 1);
        AddPlannedGroup(snapshot, "player_unit_3", "player", "expedition:player_camp:1:army:f1_azuritelion", 34, 18, 600, "undead_deployment_zone_east_1", footprintWidth: 2, footprintHeight: 1);
        AddPlannedGroup(snapshot, "player_unit_4", "player", "expedition:player_camp:1:army:f1_azuritelion", 34, 19, 600, "undead_deployment_zone_east_1", footprintWidth: 2, footprintHeight: 1);
        AddPlannedGroup(snapshot, "enemy_unit_1", "undead", "bonefield:f6_draugarlord", 37, 15, 900, "player_deployment_zone_west_1", BattleGroupTacticalMode.EnemyOffense, footprintWidth: 2, footprintHeight: 2);
        AddPlannedGroup(snapshot, "enemy_unit_2", "undead", "bonefield:f6_draugarlord", 36, 17, 900, "player_deployment_zone_west_1", BattleGroupTacticalMode.EnemyOffense, footprintWidth: 2, footprintHeight: 2);
        AddPlannedGroup(snapshot, "enemy_unit_3", "undead", "bonefield:f6_draugarlord", 38, 17, 900, "player_deployment_zone_west_1", BattleGroupTacticalMode.EnemyOffense, footprintWidth: 2, footprintHeight: 2);
        AddPlannedGroup(snapshot, "enemy_unit_4", "undead", "bonefield:f6_draugarlord", 36, 19, 900, "player_deployment_zone_west_1", BattleGroupTacticalMode.EnemyOffense, footprintWidth: 2, footprintHeight: 2);

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static void AddSurface(BattleStartSnapshot snapshot, int x, int y)
    {
        snapshot.LocationContext.NavigationSurfaces.Add(new BattleNavigationSurfaceSnapshot
        {
            X = x,
            Y = y,
            Height = 0,
            MoveCost = 1
        });
    }

    private static void AddPlannedGroup(
        BattleStartSnapshot snapshot,
        string groupId,
        string factionId,
        string sourceForceId,
        int cellX,
        int cellY,
        int hitPoints,
        string objectiveZoneId,
        BattleGroupTacticalMode tacticalMode = BattleGroupTacticalMode.PlayerCommanded,
        int footprintWidth = 1,
        int footprintHeight = 1)
    {
        string commanderGroupId = factionId == "player"
            ? "probe_group_PlayerArmy:expedition:player_camp:1:army"
            : "probe_group_DefenderSite:bonefield";
        snapshot.BattleGroups.Add(new BattleGroupSnapshot
        {
            BattleGroupId = groupId,
            RuntimeCommanderGroupId = commanderGroupId,
            FactionId = factionId,
            SourceForceId = sourceForceId,
            HeroId = $"{sourceForceId}:{groupId}:hero",
            HeroDefinitionId = $"{sourceForceId}:hero_definition",
            CorpsId = $"{sourceForceId}:{groupId}:corps",
            CorpsDefinitionId = $"{sourceForceId}:corps_definition",
            CorpsStrength = hitPoints,
            MaxHitPoints = hitPoints,
            AttackDamage = 5,
            AttackRange = 1,
            AttackSpeed = 1.0,
            SourceLocationId = factionId == "player" ? "city_1" : "site_1",
            CellX = cellX,
            CellY = cellY,
            FootprintWidth = footprintWidth,
            FootprintHeight = footprintHeight,
            TacticalMode = tacticalMode,
            Plan = new BattleGroupPlanSnapshot
            {
                ObjectiveZoneId = objectiveZoneId,
                EngagementRule = BattleEngagementRule.AttackFirst
            }
        });
    }

    private static void AssertNoImmediateReverseMoves(IReadOnlyList<BattleEvent> moves, string message)
    {
        for (int i = 1; i < moves.Count; i++)
        {
            BattleEvent previous = moves[i - 1];
            BattleEvent current = moves[i];
            bool reversed =
                previous.FromGridX == current.ToGridX &&
                previous.FromGridY == current.ToGridY &&
                previous.FromGridHeight == current.ToGridHeight &&
                previous.ToGridX == current.FromGridX &&
                previous.ToGridY == current.FromGridY &&
                previous.ToGridHeight == current.FromGridHeight;
            AssertTrue(!reversed, $"{message}: moves={DescribeMoves(moves)}");
        }
    }

    private static string DescribeMoves(IReadOnlyList<BattleEvent> moves)
    {
        return string.Join(
            ";",
            moves.Select(item =>
                $"t{item.RuntimeTick}:({item.FromGridX},{item.FromGridY},{item.FromGridHeight})->({item.ToGridX},{item.ToGridY},{item.ToGridHeight})"));
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
