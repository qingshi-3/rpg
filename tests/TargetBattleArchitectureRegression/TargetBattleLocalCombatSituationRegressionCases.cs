using System;
using System.Linq;
using Rpg.Application.Battle.Navigation;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Tactics;

internal static class TargetBattleLocalCombatSituationRegressionCases
{
    public static void LocalCombatRegionUsesPerceptionOverlapAndCap()
    {
        BattleLocalCombatRegionBuildResult singleMember = BattleLocalCombatRegionBuilder.BuildForGroup(
            "enemy_group",
            new[]
            {
                BuildRuntimeActor("enemy_a", "enemy_group", "enemy", 0, 0),
                BuildRuntimeActor("player_near", "player_group", "player", 3, 0)
            },
            runtimeTick: 1);
        BattleLocalCombatRegionBuildResult overlappingMembers = BattleLocalCombatRegionBuilder.BuildForGroup(
            "enemy_group",
            new[]
            {
                BuildRuntimeActor("enemy_a", "enemy_group", "enemy", 0, 0),
                BuildRuntimeActor("enemy_b", "enemy_group", "enemy", 1, 0),
                BuildRuntimeActor("player_near", "player_group", "player", 3, 0)
            },
            runtimeTick: 2);

        AssertTrue(overlappingMembers.PerceptionCoverageScore > singleMember.PerceptionCoverageScore, "overlap should increase local region coverage score");
        AssertTrue(
            overlappingMembers.Region.Width * overlappingMembers.Region.Height <= BattleGroupTacticalPolicySettings.DefaultLocalCombatMaxCells,
            "local region area must stay under the cap");
        AssertTrue(overlappingMembers.Region.OwnerBattleGroupId == "enemy_group", "local region records owner group id");
        AssertTrue(overlappingMembers.Region.Kind == BattleTacticalRegionKind.LocalCombat, "local region kind");
        AssertTrue(
            overlappingMembers.Region.ReasonCode == BattleGroupTacticalReasonCode.LocalRegionBuiltPerceptionOverlap,
            $"overlap build reason: actual={overlappingMembers.Region.ReasonCode}");
    }

    public static void RuntimeStoresLocalCombatRegionForEngagedGroup()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildRuntimeLocalCombatRegionSnapshot());

        BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();
        BattleGroupTacticalState enemyState = controller.State.TacticalStates["enemy_group"];
        BattleEvent? localRegionEvent = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.BattleGroupLocalCombatRegionChanged &&
            item.BattleGroupId == "enemy_group");

        AssertTrue(enemyState.LocalCombatRegion != null, "engaged group should store a local combat region");
        AssertTrue(enemyState.LocalCombatRegion!.OwnerBattleGroupId == "enemy_group", "stored local region owner");
        AssertTrue(enemyState.LocalCombatRegion.Kind == BattleTacticalRegionKind.LocalCombat, "stored local region kind");
        AssertTrue(enemyState.LocalCombatRegion.Width * enemyState.LocalCombatRegion.Height <= BattleGroupTacticalPolicySettings.DefaultLocalCombatMaxCells, "stored local region cap");
        AssertTrue(localRegionEvent != null, "local region build should emit a diagnostic event");
        AssertTrue(
            localRegionEvent!.ReasonCode == BattleGroupTacticalReasonCode.LocalRegionBuiltPerceptionOverlap,
            $"local region event reason: actual={localRegionEvent.ReasonCode}");
    }

    public static void LocalCombatDecisionFactsExposeStoredRegionFacts()
    {
        RecordingBattleRuntimeAiExecutor executor = new(new DefaultBattleRuntimeAiExecutor());
        BattleRuntimeSessionController controller = new BattleRuntimeSession(executor)
            .Begin(BuildRuntimeLocalCombatDecisionFactsSnapshot());

        controller.AdvanceNextTick();
        BattleTacticalRegionSnapshot region = controller.State.TacticalStates["enemy_group"].LocalCombatRegion;
        BattleRuntimeAiDecisionFacts facts = executor.SeenFacts.First(item =>
            item.HasLocalCombatSituation &&
            item.LocalCombatOwnerBattleGroupId == "enemy_group");

        if (region == null)
        {
            throw new Exception("runtime should store a local combat region before AI facts are consumed");
        }

        AssertTrue(facts.LocalCombatRegionId == region.RegionId, "decision facts region id");
        AssertTrue(
            facts.LocalCombatCenterCellX == region.CenterCellX &&
            facts.LocalCombatCenterCellY == region.CenterCellY,
            "decision facts region center");
        AssertTrue(facts.LocalCombatWidth == region.Width && facts.LocalCombatHeight == region.Height, "decision facts region bounds");
        AssertTrue(facts.LocalCombatVersion == region.Version, "decision facts region version");
        AssertTrue(facts.LocalCombatRegionReasonCode == region.ReasonCode, "decision facts region reason");
    }

    public static void EngagedTargetingIgnoresFarHostileOutsideLocalRegion()
    {
        RecordingBattleRuntimeAiExecutor executor = new(new DefaultBattleRuntimeAiExecutor());
        new BattleRuntimeSession(executor)
            .Begin(BuildFarHostileOutsideLocalRegionSnapshot())
            .AdvanceNextTick();

        BattleRuntimeAiDecisionFacts enemyFacts = executor.SeenFacts.Last(item =>
            item.ActorId == "enemy_a:1" &&
            item.HasLocalCombatSituation);

        AssertTrue(enemyFacts.HasLocalCombatSituation, "enemy should consume a stored local combat situation while engaged");
        AssertTrue(enemyFacts.TargetActorId == "player_near:1", $"engaged targeting should ignore far hostile outside local region: actual={enemyFacts.TargetActorId}");
        AssertTrue(enemyFacts.LocalCombatTargetActorId == "player_near:1", $"local combat target should stay inside local region: actual={enemyFacts.LocalCombatTargetActorId}");
    }

    public static void EngagedAttackSlotsExposeOutOfRegionFallbackFacts()
    {
        RecordingBattleRuntimeAiExecutor executor = new(new DefaultBattleRuntimeAiExecutor());
        BattleRuntimeSessionController controller = new BattleRuntimeSession(executor)
            .Begin(BuildAttackSlotOutsideLocalRegionSnapshot());

        controller.AdvanceNextTick();
        BattleTacticalRegionSnapshot region = controller.State.TacticalStates["enemy_group"].LocalCombatRegion;
        BattleRuntimeAiDecisionFacts enemyFacts = executor.SeenFacts.Last(item =>
            item.ActorId == "enemy_a:1" &&
            item.HasLocalCombatSituation);

        if (region == null)
        {
            throw new Exception("runtime should store a local combat region before slot facts are consumed");
        }

        AssertTrue(IsInsideRegion(region, 5, 0), "target should be inside local region");
        AssertTrue(!IsInsideRegion(region, 6, 0), "only open attack slot should be outside local region");
        AssertTrue(enemyFacts.HasLocalCombatSituation, "enemy should consume local combat facts");
        AssertTrue(
            enemyFacts.LocalCombatHasReachableAttackSlot,
            "attack slots outside the local combat region may be reachable fallback facts after in-region choices are blocked");
    }

    public static void EngagedOutOfRegionSlotIsFallbackWhenLocalSlotIsBlocked()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildOutOfRegionFallbackSlotSnapshot());

        BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();
        BattleRuntimeActor enemy = controller.State.Actors.Single(item => item.ActorId == "enemy_a:1");
        BattleTacticalRegionSnapshot region = controller.State.TacticalStates["enemy_group"].LocalCombatRegion;
        BattleEvent? enemyMove = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "enemy_a:1");

        if (region == null)
        {
            throw new Exception("runtime should store a local combat region before resolving fallback movement");
        }

        AssertTrue(IsInsideRegion(region, 5, 0), "target should be inside local region");
        AssertTrue(!IsInsideRegion(region, 6, 0), "the fallback attack slot should be outside local region");
        AssertTrue(
            enemyMove != null,
            $"blocked in-region slots should allow an executable out-of-region fallback instead of freezing: failure={enemy.LastAdvanceFailureReason}");
        AssertTrue(
            enemyMove!.ToGridX > enemyMove.FromGridX,
            $"fallback route should still move toward the target-side attackable cells: from=({enemyMove.FromGridX},{enemyMove.FromGridY}) to=({enemyMove.ToGridX},{enemyMove.ToGridY})");
    }

    public static void EngagedNoLocalSlotKeepsCombatPressure()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildNoLocalSlotSnapshot());

        BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();
        BattleRuntimeActor enemy = controller.State.Actors.Single(item => item.ActorId == "enemy_a:1");
        BattleEvent? pressureMove = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "enemy_a:1");

        AssertTrue(
            pressureMove != null,
            $"enemy should keep pressure toward the local fight when no slot is reachable but a legal pressure step exists: failure={enemy.LastAdvanceFailureReason}");
        AssertTrue(
            pressureMove!.ReasonCode == BattleGroupTacticalReasonCode.CombatPressureAdvance,
            $"no-slot pressure movement reason: actual={pressureMove.ReasonCode}");
        AssertTrue(
            pressureMove.ToGridX == 2 && pressureMove.ToGridY == 0,
            $"no-slot pressure first step should move toward the fight: to=({pressureMove.ToGridX},{pressureMove.ToGridY})");
    }

    public static void RuntimeMoveFirstJoinsRouteBlockingLocalFight()
    {
        BattleRuntimeAdvanceResult tick = new BattleRuntimeSession()
            .Begin(BuildRouteBlockingLocalFightSnapshot())
            .AdvanceNextTick();

        BattleEvent? supportMove = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "support:1");

        AssertTrue(supportMove != null, "move-first support should move when a local fight blocks the planned route");
        AssertTrue(
            supportMove!.TargetId == "enemy_blocker:1" &&
            supportMove.ReasonCode == "join_blocks_objective_route",
            $"route-blocking local fight should target the blocker with a local-combat reason: target={supportMove.TargetId} reason={supportMove.ReasonCode}");
    }

    public static void RuntimeFullAttackSlotsUsesNamedSupportSlot()
    {
        BattleRuntimeAdvanceResult tick = new BattleRuntimeSession()
            .Begin(BuildFullAttackSlotLocalFightSnapshot())
            .AdvanceNextTick();

        BattleEvent? supportMove = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "support:1");

        AssertTrue(supportMove != null, "support actor should move instead of idling when direct attack slots are full");
        AssertTrue(
            supportMove!.TargetId == "enemy_line:1" &&
            supportMove.ToGridX == -1 &&
            supportMove.ToGridY == 0 &&
            supportMove.ReasonCode == "hold_support_attack_slots_full",
            $"full attack slots should produce a deterministic support move: target={supportMove.TargetId} to=({supportMove.ToGridX},{supportMove.ToGridY}) reason={supportMove.ReasonCode}");
    }

    public static void RuntimeHoldRejectsLocalFightOutsideLeash()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildOutsideHoldLeashSnapshot());

        BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();
        BattleRuntimeActor defender = controller.State.Actors.Single(item => item.ActorId == "defender:1");

        AssertTrue(
            tick.Events.All(item => item.ActorId != "defender:1" || item.Kind != BattleEventKind.MovementStarted),
            "hold defender should not pursue a local fight outside the held-area leash");
        AssertTrue(
            defender.LastAdvanceFailureReason == "reject_outside_leash",
            $"outside-leash rejection should remain diagnosable on the actor: actual={defender.LastAdvanceFailureReason}");
    }

    private static BattleStartSnapshot BuildRouteBlockingLocalFightSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_local_route_block",
            BattleId = "battle_local_route_block",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("front_group", "player", "front", 2, 0, 160, BattleEngagementRule.AttackFirst),
                BuildGroup(
                    "support_group",
                    "player",
                    "support",
                    0,
                    0,
                    160,
                    BattleEngagementRule.MoveFirst,
                    objectiveX: 8,
                    objectiveY: 0,
                    objectiveWidth: 1,
                    objectiveHeight: 1),
                BuildGroup("enemy_group", "enemy", "enemy_blocker", 3, 0, 160, BattleEngagementRule.Hold, initialCommandId: "HoldLine")
            }
        };

        for (int x = 0; x <= 8; x++)
        {
            AddSurface(snapshot, x, 0);
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildRuntimeLocalCombatRegionSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_runtime_local_combat_region",
            BattleId = "battle_runtime_local_combat_region",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("enemy_group", "enemy", "enemy_a", 0, 0, 160, BattleEngagementRule.AttackFirst, tacticalMode: BattleGroupTacticalMode.EnemyOffense),
                BuildGroup("enemy_group", "enemy", "enemy_b", 1, 0, 160, BattleEngagementRule.AttackFirst, tacticalMode: BattleGroupTacticalMode.EnemyOffense),
                BuildGroup("player_group", "player", "player_near", 3, 0, 160, BattleEngagementRule.Hold, initialCommandId: "HoldLine")
            }
        };

        for (int x = 0; x <= 3; x++)
        {
            AddSurface(snapshot, x, 0);
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildRuntimeLocalCombatDecisionFactsSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_runtime_local_combat_decision_facts",
            BattleId = "battle_runtime_local_combat_decision_facts",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("enemy_group", "enemy", "enemy_a", 2, 0, 160, BattleEngagementRule.AttackFirst, tacticalMode: BattleGroupTacticalMode.EnemyOffense),
                BuildGroup("player_group", "player", "player_near", 3, 0, 160, BattleEngagementRule.Hold, initialCommandId: "HoldLine")
            }
        };

        for (int x = 0; x <= 3; x++)
        {
            AddSurface(snapshot, x, 0);
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildFarHostileOutsideLocalRegionSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_far_hostile_outside_local_region",
            BattleId = "battle_far_hostile_outside_local_region",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup(
                    "enemy_group",
                    "enemy",
                    "enemy_a",
                    0,
                    0,
                    160,
                    BattleEngagementRule.AttackFirst,
                    initialCommandId: "FocusFire",
                    tacticalMode: BattleGroupTacticalMode.EnemyOffense),
                BuildGroup("player_near_group", "player", "player_near", 3, 0, 160, BattleEngagementRule.Hold, initialCommandId: "HoldLine"),
                BuildGroup("player_far_group", "player", "player_far", 12, 0, 1, BattleEngagementRule.Hold, initialCommandId: "HoldLine")
            }
        };

        for (int x = 0; x <= 12; x++)
        {
            AddSurface(snapshot, x, 0);
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildAttackSlotOutsideLocalRegionSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_attack_slot_outside_local_region",
            BattleId = "battle_attack_slot_outside_local_region",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("enemy_group", "enemy", "enemy_a", 1, 0, 160, BattleEngagementRule.AttackFirst, tacticalMode: BattleGroupTacticalMode.EnemyOffense),
                BuildGroup("enemy_blocker_group", "enemy", "enemy_blocker", 4, 0, 160, BattleEngagementRule.Hold, initialCommandId: "HoldLine"),
                BuildGroup("player_group", "player", "player_near", 5, 0, 160, BattleEngagementRule.Hold, initialCommandId: "HoldLine")
            }
        };

        for (int x = 1; x <= 6; x++)
        {
            AddSurface(snapshot, x, 0);
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildNoLocalSlotSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_no_local_slot",
            BattleId = "battle_no_local_slot",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("enemy_group", "enemy", "enemy_a", 1, 0, 160, BattleEngagementRule.AttackFirst, tacticalMode: BattleGroupTacticalMode.EnemyOffense),
                BuildGroup("enemy_support_blocker_group", "enemy", "enemy_support_blocker", 3, 0, 160, BattleEngagementRule.Hold, initialCommandId: "HoldLine"),
                BuildGroup("enemy_attack_blocker_group", "enemy", "enemy_attack_blocker", 4, 0, 160, BattleEngagementRule.Hold, initialCommandId: "HoldLine"),
                BuildGroup("player_group", "player", "player_near", 5, 0, 160, BattleEngagementRule.Hold, initialCommandId: "HoldLine")
            }
        };

        for (int x = 1; x <= 5; x++)
        {
            AddSurface(snapshot, x, 0);
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildOutOfRegionFallbackSlotSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_out_of_region_slot_fallback",
            BattleId = "battle_out_of_region_slot_fallback",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("enemy_group", "enemy", "enemy_a", 1, 0, 160, BattleEngagementRule.AttackFirst, tacticalMode: BattleGroupTacticalMode.EnemyOffense),
                BuildGroup("enemy_blocker_group", "enemy", "enemy_blocker", 4, 0, 160, BattleEngagementRule.Hold, initialCommandId: "HoldLine"),
                BuildGroup("player_group", "player", "player_near", 5, 0, 160, BattleEngagementRule.Hold, initialCommandId: "HoldLine")
            }
        };

        for (int x = 1; x <= 6; x++)
        {
            AddSurface(snapshot, x, 0);
            AddSurface(snapshot, x, 1);
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildFullAttackSlotLocalFightSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_local_full_attack_slots",
            BattleId = "battle_local_full_attack_slots",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("front_group", "player", "front", 0, 0, 160, BattleEngagementRule.AttackFirst),
                BuildGroup("support_group", "player", "support", -2, 0, 160, BattleEngagementRule.AttackFirst),
                BuildGroup("enemy_group", "enemy", "enemy_line", 1, 0, 160, BattleEngagementRule.Hold, initialCommandId: "HoldLine")
            }
        };

        for (int x = -2; x <= 1; x++)
        {
            AddSurface(snapshot, x, 0);
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildOutsideHoldLeashSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_hold_outside_leash",
            BattleId = "battle_hold_outside_leash",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup(
                    "defender_group",
                    "player",
                    "defender",
                    0,
                    0,
                    160,
                    BattleEngagementRule.Hold,
                    initialCommandId: "",
                    objectiveX: 0,
                    objectiveY: 0,
                    objectiveWidth: 1,
                    objectiveHeight: 1),
                BuildGroup("enemy_group", "enemy", "enemy_outside", 4, 0, 160, BattleEngagementRule.AttackFirst)
            }
        };

        for (int x = 0; x <= 4; x++)
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
        BattleEngagementRule engagementRule,
        string initialCommandId = "",
        int objectiveX = 0,
        int objectiveY = 0,
        int objectiveWidth = 1,
        int objectiveHeight = 1,
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
            AttackDamage = 1,
            TacticalMode = tacticalMode,
            SourceLocationId = factionId == "player" ? "city_1" : "site_1",
            CellX = cellX,
            CellY = cellY,
            InitialCorpsCommandId = initialCommandId,
            Plan = new BattleGroupPlanSnapshot
            {
                BattleGroupId = groupId,
                ObjectiveZoneId = $"{groupId}_objective",
                EngagementRule = engagementRule,
                HasObjectiveAnchor = objectiveX != 0 || objectiveY != 0 || engagementRule is BattleEngagementRule.MoveFirst or BattleEngagementRule.Hold,
                ObjectiveCellX = objectiveX,
                ObjectiveCellY = objectiveY,
                ObjectiveCellHeight = 0,
                ObjectiveWidth = objectiveWidth,
                ObjectiveHeight = objectiveHeight
            }
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

    private static BattleRuntimeActor BuildRuntimeActor(
        string actorId,
        string battleGroupId,
        string factionId,
        int x,
        int y)
    {
        return new BattleRuntimeActor
        {
            ActorId = actorId,
            BattleGroupId = battleGroupId,
            FactionId = factionId,
            Kind = BattleRuntimeActorKind.Corps,
            HitPoints = 80,
            GridX = x,
            GridY = y,
            GridHeight = 0
        };
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }

    private static bool IsInsideRegion(BattleTacticalRegionSnapshot region, int x, int y, int height = 0)
    {
        int width = Math.Max(1, region?.Width ?? 1);
        int regionHeight = Math.Max(1, region?.Height ?? 1);
        int minX = (region?.CenterCellX ?? 0) - (width - 1) / 2;
        int minY = (region?.CenterCellY ?? 0) - (regionHeight - 1) / 2;
        return region != null &&
               height == region.CenterCellHeight &&
               x >= minX &&
               x < minX + width &&
               y >= minY &&
               y < minY + regionHeight;
    }

    private sealed class RecordingBattleRuntimeAiExecutor : IBattleRuntimeAiExecutor
    {
        private readonly IBattleRuntimeAiExecutor _inner;

        public RecordingBattleRuntimeAiExecutor(IBattleRuntimeAiExecutor inner)
        {
            _inner = inner;
        }

        public System.Collections.Generic.List<BattleRuntimeAiDecisionFacts> SeenFacts { get; } = new();

        public BattleRuntimeAiActionRequest ChooseAction(BattleRuntimeAiDecisionFacts facts)
        {
            SeenFacts.Add(facts);
            return _inner.ChooseAction(facts);
        }
    }
}
