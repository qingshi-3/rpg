using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Tactics;

internal static class TargetBattleMovementBattlefieldRegressionCases
{
    public static void Register(Action<string, Action> run)
    {
        run("runtime far target pressure moves on large authored topology", RuntimeFarTargetPressureMovesOnLargeAuthoredTopology);
        run("runtime attack-first objective entrant seeks enemy inside region", RuntimeAttackFirstObjectiveEntrantSeeksEnemyInsideRegion);
        run("runtime committed combat join survives ally defeat refresh", RuntimeCommittedCombatJoinSurvivesAllyDefeatRefresh);
    }

    public static void RuntimeFarTargetPressureMovesOnLargeAuthoredTopology()
    {
        BattleRuntimeAdvanceResult tick = new BattleRuntimeSession()
            .Begin(BuildLargeAuthoredTargetPressureSnapshot())
            .AdvanceNextTick();

        BattleEvent? move = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "enemy:1");

        AssertTrue(move != null, "enemy with a reachable target on a large authored topology should not stall with path_not_found");
        AssertTrue(
            move!.TargetId == "force_player:1" &&
            move.ToGridX < move.FromGridX,
            $"enemy should start local pressure toward the player instead of standing still: target={move.TargetId} from=({move.FromGridX},{move.FromGridY}) to=({move.ToGridX},{move.ToGridY})");
    }

    public static void RuntimeAttackFirstObjectiveEntrantSeeksEnemyInsideRegion()
    {
        BattleRuntimeAdvanceResult tick = new BattleRuntimeSession()
            .Begin(BuildAttackFirstObjectiveEntrantSnapshot())
            .AdvanceNextTick();

        BattleEvent? move = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "force_player:1");

        AssertTrue(move != null, "attack-first group already inside the selected objective region should keep acting");
        AssertTrue(
            move!.TargetId == "enemy:1" &&
            move.ReasonCode == "auto_advance" &&
            move.ToGridX > move.FromGridX,
            $"attack-first group inside an enemy-held objective region should move toward the enemy, not orbit the region anchor: target={move.TargetId} reason={move.ReasonCode} from=({move.FromGridX},{move.FromGridY}) to=({move.ToGridX},{move.ToGridY})");
    }

    public static void RuntimeCommittedCombatJoinSurvivesAllyDefeatRefresh()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildCommittedCombatJoinRefreshSnapshot());
        BattleRuntimeActor player = controller.State.Actors.Single(item => item.ActorId == "player_force:1");
        player.TargetActorId = "enemy:1";
        player.PlanState = BattleGroupPlanRuntimeState.MovingToAttackSlot;
        player.Phase = BattleRuntimeActorPhase.AnchoredDecision;
        ApplyEngagementState(controller.State, "player_group", BattleGroupEngagementState.Engaged);
        SetGroupActionZoneStore(
            controller.State,
            new Dictionary<string, BattleGroupActionZoneSnapshot>(StringComparer.Ordinal)
            {
                ["player_group"] = new BattleGroupActionZoneSnapshot
                {
                    BattleGroupId = "player_group",
                    Kind = BattleGroupActionZoneKind.CombatJoin,
                    TargetCombatZoneId = "combat_zone_1",
                    ReasonCode = "group_action_combat_join",
                    MinCellX = 0,
                    MinCellY = 0,
                    MaxCellX = 10,
                    MaxCellY = 0,
                    CenterCellX = 5,
                    CenterCellY = 0
                }
            });

        controller.AdvanceNextTick();

        BattleGroupActionZoneSnapshot actionZone = controller.State.GroupActionZones["player_group"];
        BattleGroupTacticalState tacticalState = controller.State.TacticalStates["player_group"];
        AssertTrue(
            actionZone.Kind == BattleGroupActionZoneKind.CombatJoin,
            $"committed combat join should remain selected while the combat zone still exists: kind={actionZone.Kind} targetRegion={actionZone.TargetRegionId} reason={actionZone.ReasonCode}");
        AssertEqual("combat_zone_1", actionZone.TargetCombatZoneId, "committed combat-zone id should survive refresh");
        AssertEqual(BattleGroupEngagementState.Engaged, tacticalState.EngagementState, "combat-join owner should not exit engagement before switching to a valid next combat scope");
        AssertEqual("enemy:1", player.TargetActorId, "target lock should not be cleared before the committed combat join is invalidated");
    }

    private static BattleStartSnapshot BuildLargeAuthoredTargetPressureSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_large_authored_target_pressure",
            BattleId = "battle_large_authored_target_pressure",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("group_player", "player", "force_player", 11, 20, 160, initialCommandId: "HoldLine"),
                BuildGroup("group_enemy", "enemy", "enemy", 69, 20, 220, footprintWidth: 2, footprintHeight: 2)
            }
        };

        for (int x = 0; x <= 79; x++)
        {
            for (int y = 0; y <= 39; y++)
            {
                AddSurface(snapshot, x, y);
            }
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildAttackFirstObjectiveEntrantSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_attack_first_objective_entrant",
            BattleId = "battle_attack_first_objective_entrant",
            TargetLocationId = "site_1",
            ObjectiveZones =
            {
                new BattleObjectiveZoneSnapshot
                {
                    ObjectiveZoneId = "enemy_deployment",
                    CellX = 51,
                    CellY = 15,
                    Width = 20,
                    Height = 12
                }
            },
            BattleGroups =
            {
                BuildGroup(
                    "group_player",
                    "player",
                    "force_player",
                    51,
                    16,
                    160,
                    plan: new BattleGroupPlanSnapshot
                    {
                        BattleGroupId = "group_player",
                        ObjectiveZoneId = "enemy_deployment",
                        EngagementRule = BattleEngagementRule.AttackFirst
                    }),
                BuildGroup("group_enemy", "enemy", "enemy", 69, 20, 160, initialCommandId: "HoldLine")
            }
        };

        for (int x = 45; x <= 72; x++)
        {
            for (int y = 12; y <= 28; y++)
            {
                AddSurface(snapshot, x, y);
            }
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildCommittedCombatJoinRefreshSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_committed_combat_join_refresh",
            BattleId = "battle_committed_combat_join_refresh",
            TargetLocationId = "site_1",
            ObjectiveZones =
            {
                new BattleObjectiveZoneSnapshot
                {
                    ObjectiveZoneId = "old_objective",
                    CellX = 20,
                    CellY = 0,
                    Width = 3,
                    Height = 1
                }
            },
            BattleGroups =
            {
                BuildGroup(
                    "player_group",
                    "player",
                    "player_force",
                    0,
                    0,
                    160,
                    plan: new BattleGroupPlanSnapshot
                    {
                        BattleGroupId = "player_group",
                        ObjectiveZoneId = "old_objective",
                        EngagementRule = BattleEngagementRule.MoveFirst
                    }),
                BuildGroup("enemy_group", "enemy", "enemy", 10, 0, 160, initialCommandId: "HoldLine")
            }
        };

        for (int x = 0; x <= 22; x++)
        {
            AddSurface(snapshot, x, 0);
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleGroupSnapshot BuildGroup(
        string groupId,
        string factionId,
        string sourceForceId,
        int cellX,
        int cellY,
        int hitPoints,
        string initialCommandId = "",
        int footprintWidth = 1,
        int footprintHeight = 1,
        BattleGroupPlanSnapshot plan = null)
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
            AttackDamage = 1,
            AttackImpactDelaySeconds = 0,
            SourceLocationId = factionId == "player" ? "city_1" : "site_1",
            CellX = cellX,
            CellY = cellY,
            InitialCorpsCommandId = initialCommandId,
            FootprintWidth = footprintWidth,
            FootprintHeight = footprintHeight,
            Plan = plan ?? new BattleGroupPlanSnapshot()
        };
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

    private static void ApplyEngagementState(
        BattleRuntimeState state,
        string battleGroupId,
        BattleGroupEngagementState engagementState)
    {
        object store = typeof(BattleRuntimeState)
            .GetProperty("TacticalStateStore", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(state)!;
        System.Reflection.MethodInfo method = store.GetType()
            .GetMethod("TryApplyEngagementState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        object? accepted = method.Invoke(store, new object[] { battleGroupId, engagementState, BattleGroupTacticalMode.PlayerCommanded });
        AssertTrue(accepted is true, "test setup should be able to seed committed player combat engagement");
    }

    private static void SetGroupActionZoneStore(
        BattleRuntimeState state,
        IReadOnlyDictionary<string, BattleGroupActionZoneSnapshot> actionZones)
    {
        typeof(BattleRuntimeState)
            .GetProperty("GroupActionZoneStore", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(state, actionZones);
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
