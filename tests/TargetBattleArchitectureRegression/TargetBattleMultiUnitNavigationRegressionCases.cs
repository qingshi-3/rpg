using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;

internal static class TargetBattleMultiUnitNavigationRegressionCases
{
    public static void Register(Action<string, Action> run)
    {
        run("runtime many allies converge on single holdline enemy without overlap", RuntimeManyAlliesConvergeOnSingleHoldlineEnemyWithoutOverlap);
        run("runtime many enemies converge on single holdline defender without overlap", RuntimeManyEnemiesConvergeOnSingleHoldlineDefenderWithoutOverlap);
        run("runtime many vs many open field closes without illegal positions", RuntimeManyVsManyOpenFieldClosesWithoutIllegalPositions);
        run("runtime four versus four battle does not timeout while both sides live", RuntimeFourVersusFourBattleDoesNotTimeoutWhileBothSidesLive);
        run("runtime same-lane crowd blocks same-tick chain follow", RuntimeSameLaneCrowdBlocksSameTickChainFollow);
        run("runtime support queue blocks same-tick chain behind engaged frontline", RuntimeSupportQueueBlocksSameTickChainBehindEngagedFrontline);
        run("runtime same-tick follow cannot enter released footprint", RuntimeSameTickFollowCannotEnterReleasedFootprint);
        run("runtime smaller units can surround large target attack slots", RuntimeSmallerUnitsCanSurroundLargeTargetAttackSlots);
        run("runtime support below vertical engagement flanks to open attack slot", RuntimeSupportBelowVerticalEngagementFlanksToOpenAttackSlot);
        run("runtime support behind engaged ally uses side approach when direct step occupied", RuntimeSupportBehindEngagedAllyUsesSideApproachWhenDirectStepOccupied);
        run("runtime support unit continues into orthogonal attack range against engaged target", RuntimeSupportUnitContinuesIntoOrthogonalAttackRangeAgainstEngagedTarget);
        run("runtime rear local combat units degrade blocked ingress to named support", RuntimeRearLocalCombatUnitsDegradeBlockedIngressToNamedSupport);
        run("runtime rear local combat units route around blocked ingress when side path exists", RuntimeRearLocalCombatUnitsRouteAroundBlockedIngressWhenSidePathExists);
        run("runtime combat ingress planner allows a temporary detour around blocked closer steps", RuntimeCombatIngressPlannerAllowsTemporaryDetourAroundBlockedCloserSteps);
        run("runtime local combat movement keeps stable slot intent instead of oscillating", RuntimeLocalCombatMovementKeepsStableSlotIntentInsteadOfOscillating);
        run("runtime blocked local combat keeps combat pressure instead of path failure", RuntimeBlockedLocalCombatKeepsCombatPressureInsteadOfPathFailure);
    }

    public static void RuntimeManyAlliesConvergeOnSingleHoldlineEnemyWithoutOverlap()
    {
        BattleStartSnapshot snapshot = BuildOpenFieldSnapshot("battle_many_allies_single_enemy");
        AddGroup(snapshot, "player_top", "player", "player_top", 0, -1, 35);
        AddGroup(snapshot, "player_mid", "player", "player_mid", 0, 0, 35);
        AddGroup(snapshot, "player_bottom", "player", "player_bottom", 0, 1, 35);
        AddGroup(snapshot, "enemy_anchor", "enemy", "enemy_anchor", 6, 0, 45, initialCommandId: "HoldLine");
        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        AssertCompletedWithoutRuntimeException(result, "many allies vs single holdline enemy should resolve instead of stalling");
        AssertActorsMoved(result, "player_top:1", "player_mid:1", "player_bottom:1");
        AssertAllMovementDestinationsAreAuthored(result, snapshot);
        AssertNoDuplicateMovementDestinationsPerTick(result);
        AssertNoDuplicateLivingCorpsCells(result);
        AssertAllMovesProgressX(result, actorId => actorId.StartsWith("player_", StringComparison.Ordinal), expectedDirection: 1);
    }

    public static void RuntimeManyEnemiesConvergeOnSingleHoldlineDefenderWithoutOverlap()
    {
        BattleStartSnapshot snapshot = BuildOpenFieldSnapshot("battle_many_enemies_single_defender");
        AddGroup(snapshot, "player_anchor", "player", "player_anchor", 0, 0, 45, initialCommandId: "HoldLine");
        AddGroup(snapshot, "enemy_top", "enemy", "enemy_top", 6, -1, 35);
        AddGroup(snapshot, "enemy_mid", "enemy", "enemy_mid", 6, 0, 35);
        AddGroup(snapshot, "enemy_bottom", "enemy", "enemy_bottom", 6, 1, 35);
        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        AssertCompletedWithoutRuntimeException(result, "many enemies vs single holdline defender should resolve instead of stalling");
        AssertActorsMoved(result, "enemy_top:1", "enemy_mid:1", "enemy_bottom:1");
        AssertAllMovementDestinationsAreAuthored(result, snapshot);
        AssertNoDuplicateMovementDestinationsPerTick(result);
        AssertNoDuplicateLivingCorpsCells(result);
        AssertAllMovesProgressX(result, actorId => actorId.StartsWith("enemy_", StringComparison.Ordinal), expectedDirection: -1);
    }

    public static void RuntimeManyVsManyOpenFieldClosesWithoutIllegalPositions()
    {
        BattleStartSnapshot snapshot = BuildOpenFieldSnapshot("battle_many_vs_many_open_field");
        AddGroup(snapshot, "player_top", "player", "player_top", 0, -1, 35);
        AddGroup(snapshot, "player_bottom", "player", "player_bottom", 0, 1, 35);
        AddGroup(snapshot, "enemy_top", "enemy", "enemy_top", 6, -1, 35);
        AddGroup(snapshot, "enemy_bottom", "enemy", "enemy_bottom", 6, 1, 35);

        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        AssertCompletedWithoutRuntimeException(result, "many vs many open-field battle should resolve instead of stalling");
        AssertActorsMoved(result, "player_top:1", "player_bottom:1", "enemy_top:1", "enemy_bottom:1");
        AssertAllMovementDestinationsAreAuthored(result, snapshot);
        AssertNoDuplicateMovementDestinationsPerTick(result);
        AssertNoDuplicateLivingCorpsCells(result);
        AssertAllMovesProgressX(result, actorId => actorId.StartsWith("player_", StringComparison.Ordinal), expectedDirection: 1);
        AssertAllMovesProgressX(result, actorId => actorId.StartsWith("enemy_", StringComparison.Ordinal), expectedDirection: -1);
    }

    public static void RuntimeFourVersusFourBattleDoesNotTimeoutWhileBothSidesLive()
    {
        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(BuildFourVersusFourOpenFieldSnapshot());

        AssertCompletedWithoutRuntimeException(result, "4v4 open-field battle should resolve through combat instead of the runtime tick cap");
        AssertTrue(
            result.EventStream.Events.Any(item => item.Kind == BattleEventKind.DamageApplied),
            "4v4 battle should produce combat damage before completion");
    }

    public static void RuntimeSameLaneCrowdBlocksSameTickChainFollow()
    {
        BattleStartSnapshot snapshot = BuildSingleLaneSnapshot("battle_same_lane_chain");
        AddGroup(snapshot, "player_rear", "player", "player_rear", 0, 0, 90);
        AddGroup(snapshot, "player_mid", "player", "player_mid", 1, 0, 90);
        AddGroup(snapshot, "player_front", "player", "player_front", 2, 0, 90);
        AddGroup(snapshot, "enemy_anchor", "enemy", "enemy_anchor", 6, 0, 120, initialCommandId: "HoldLine");

        BattleRuntimeAdvanceResult tick = new BattleRuntimeSession().Begin(snapshot).AdvanceNextTick();

        AssertMove(tick.Events, "player_front:1", 2, 0, 3, 0);
        AssertNoMove(tick.Events, "player_mid:1");
        AssertNoMove(tick.Events, "player_rear:1");
    }

    public static void RuntimeSupportQueueBlocksSameTickChainBehindEngagedFrontline()
    {
        BattleStartSnapshot snapshot = BuildSingleLaneSnapshot("battle_support_chain");
        AddGroup(snapshot, "player_rear", "player", "player_rear", 2, 0, 90);
        AddGroup(snapshot, "player_mid", "player", "player_mid", 3, 0, 90);
        AddGroup(snapshot, "player_front", "player", "player_front", 5, 0, 90);
        AddGroup(snapshot, "enemy_anchor", "enemy", "enemy_anchor", 6, 0, 120, initialCommandId: "HoldLine");

        BattleRuntimeAdvanceResult tick = new BattleRuntimeSession().Begin(snapshot).AdvanceNextTick();

        AssertTrue(
            tick.Events.Any(item =>
                item.Kind == BattleEventKind.DamageApplied &&
                item.ActorId == "player_front:1" &&
                item.TargetId == "enemy_anchor:1"),
            "engaged frontline should attack while support units advance behind it");
        AssertMove(tick.Events, "player_mid:1", 3, 0, 4, 0);
        AssertNoMove(tick.Events, "player_rear:1");
    }

    public static void RuntimeSameTickFollowCannotEnterReleasedFootprint()
    {
        BattleStartSnapshot snapshot = BuildSingleLaneSnapshot("battle_same_tick_released_footprint_block");
        AddGroup(snapshot, "player_rear", "player", "player_rear", 1, 0, 90);
        AddGroup(snapshot, "player_front", "player", "player_front", 2, 0, 90, footprintWidth: 2);
        AddGroup(snapshot, "enemy_anchor", "enemy", "enemy_anchor", 6, 0, 120, initialCommandId: "HoldLine");

        BattleRuntimeAdvanceResult tick = new BattleRuntimeSession().Begin(snapshot).AdvanceNextTick();

        AssertMove(tick.Events, "player_front:1", 2, 0, 3, 0);
        AssertTrue(
            tick.Events.All(item =>
                item.Kind != BattleEventKind.MovementStarted ||
                item.ActorId != "player_rear:1"),
            "rear support must not enter the frontline footprint released earlier in the same tick");

        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        _ = controller.AdvanceNextTick();
        BattleRuntimeAdvanceResult completionTick = controller.AdvanceNextTick();
        AssertTrue(
            completionTick.Events.Any(item =>
                item.Kind == BattleEventKind.MovementCompleted &&
                item.ActorId == "player_front:1"),
            "frontline movement should complete on the next action boundary");
        AssertTrue(
            completionTick.Events.All(item =>
                item.Kind != BattleEventKind.MovementStarted ||
                item.ActorId != "player_rear:1"),
            "rear support must not enter the frontline footprint released by a movement completion in the same resolver pass");
    }

    public static void RuntimeSmallerUnitsCanSurroundLargeTargetAttackSlots()
    {
        BattleStartSnapshot snapshot = BuildLargeTargetSurroundSnapshot();

        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        BattleEvent[] attacks = result.EventStream.Events
            .Where(item =>
                item.Kind == BattleEventKind.DamageApplied &&
                item.TargetId == "enemy_large:1")
            .GroupBy(item => item.ActorId)
            .Select(group => group.First())
            .ToArray();
        AssertEqual(2, attacks.Length, "two smaller attackers should damage the same larger footprint from distinct attack slots");
        AssertTrue(
            attacks.Any(item => item.ActorId == "player_left:1") &&
            attacks.Any(item => item.ActorId == "player_top:1"),
            $"large target should be attackable from multiple footprint-valid sides: actual=[{string.Join(",", attacks.Select(item => item.ActorId))}]");
    }

    public static void RuntimeSupportBelowVerticalEngagementFlanksToOpenAttackSlot()
    {
        BattleStartSnapshot snapshot = BuildVerticalEngagementSupportSnapshot();

        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        BattleEvent? firstSupportMove = result.EventStream.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementCompleted &&
            item.ActorId == "player_support:1");
        AssertTrue(firstSupportMove != null, "support below a vertical engagement should move toward an open attack slot");
        AssertTrue(
            firstSupportMove!.ToGridX != 0 || firstSupportMove.ToGridY != 2,
            "support should not stay directly behind the engaged ally when side attack slots are open");
        AssertTrue(
            result.EventStream.Events.Any(item =>
                item.Kind == BattleEventKind.DamageApplied &&
                item.ActorId == "player_support:1" &&
                item.TargetId == "enemy_anchor:1"),
            "support should eventually attack from a side or upper footprint-valid slot");
    }

    public static void RuntimeSupportBehindEngagedAllyUsesSideApproachWhenDirectStepOccupied()
    {
        BattleStartSnapshot snapshot = BuildOneStepBehindEngagedAllySnapshot();

        BattleRuntimeAdvanceResult tick = new BattleRuntimeSession().Begin(snapshot).AdvanceNextTick();

        BattleEvent? supportMove = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "player_support:1");
        AssertTrue(supportMove != null, "support one step behind an engaged ally should not idle on a blocked direct approach");
        AssertTrue(
            supportMove!.ToGridX != 3 || supportMove.ToGridY != 1,
            "support must not choose the engaged ally's occupied attack cell as its next step");
        AssertTrue(
            (supportMove.ToGridX == 2 || supportMove.ToGridX == 4) && supportMove.ToGridY == 1,
            $"support should side-step toward another orthogonal attack slot: actual=({supportMove.ToGridX},{supportMove.ToGridY})");
    }

    public static void RuntimeSupportUnitContinuesIntoOrthogonalAttackRangeAgainstEngagedTarget()
    {
        BattleStartSnapshot snapshot = BuildOpenFieldSnapshot("battle_support_joins_engaged_target");
        AddGroup(snapshot, "player_front", "player", "player_front", 2, 0, 120);
        AddGroup(snapshot, "player_support", "player", "player_support", 5, -1, 120);
        AddGroup(snapshot, "enemy_anchor", "enemy", "enemy_anchor", 3, 0, 220, initialCommandId: "HoldLine");

        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        BattleEvent? supportMove = result.EventStream.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementCompleted &&
            item.ActorId == "player_support:1");
        AssertTrue(supportMove != null, "support unit should not stop outside attack range just because the target is engaged");
        AssertEqual(4, supportMove!.ToGridX, "support unit should close to orthogonal attack x");
        AssertEqual(0, supportMove.ToGridY, "support unit should close to orthogonal attack y");
        AssertTrue(
            result.EventStream.Events.Any(item =>
                item.Kind == BattleEventKind.DamageApplied &&
                item.ActorId == "player_support:1" &&
                item.TargetId == "enemy_anchor:1"),
            "support unit should attack the already engaged target after reaching orthogonal range");
    }

    public static void RuntimeRearLocalCombatUnitsDegradeBlockedIngressToNamedSupport()
    {
        BattleStartSnapshot snapshot = BuildRearBlockedLocalCombatSnapshot();
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        BattleRuntimeActor rearTop = controller.State.Actors.Single(item => item.ActorId == "enemy_rear_top:1");
        rearTop.TargetActorId = "player_front_a:1";
        string previousLog = File.Exists(GameLog.CurrentLogPath)
            ? File.ReadAllText(GameLog.CurrentLogPath)
            : "";

        BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();
        string log = File.Exists(GameLog.CurrentLogPath)
            ? File.ReadAllText(GameLog.CurrentLogPath)
            : "";
        string newLog = log.Length >= previousLog.Length
            ? log[previousLog.Length..]
            : log;

        BattleEvent? rearMove = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "enemy_rear_top:1");
        AssertTrue(
            rearMove == null,
            "rear unit must not fake movement when every local-combat ingress step is blocked by live footprints");
        AssertTrue(
            rearTop.LastAdvanceFailureReason == "hold_support_attack_slots_full",
            $"blocked local-combat ingress should degrade to a named support/queue role before generic rejection: actual={rearTop.LastAdvanceFailureReason}");
        AssertTrue(
            newLog.Contains("BattleRuntimeAdvanceDiagnostic battle=battle_rear_blocked_local_combat_support", StringComparison.Ordinal) &&
            newLog.Contains("actor=enemy_rear_top:1", StringComparison.Ordinal) &&
            newLog.Contains("target=player_front_a:1", StringComparison.Ordinal) &&
            newLog.Contains("reason=hold_support_attack_slots_full", StringComparison.Ordinal) &&
            newLog.Contains("attemptedNext=none", StringComparison.Ordinal),
            "blocked local-combat ingress should log a named support/queue reason with no fake next step");
        AssertTrue(
            !newLog.Contains("actor=enemy_rear_top:1 target=player_front_a:1 reason=path_not_found", StringComparison.Ordinal),
            "blocked local-combat ingress must not be reported as generic path_not_found");
    }

    public static void RuntimeRearLocalCombatUnitsRouteAroundBlockedIngressWhenSidePathExists()
    {
        BattleStartSnapshot snapshot = BuildRearBlockedIngressWithSidePathSnapshot();
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        BattleRuntimeActor rearBottom = controller.State.Actors.Single(item => item.ActorId == "enemy_rear_bottom:1");
        rearBottom.TargetActorId = "player_front_c:1";

        BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();

        BattleEvent? rearMove = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "enemy_rear_bottom:1");
        AssertTrue(
            rearMove != null,
            $"rear unit should route through the current dynamic side path instead of reporting no reachable slot: failure={rearBottom.LastAdvanceFailureReason}");
        AssertTrue(
            rearMove!.ToGridY > rearMove.FromGridY,
            $"rear unit should take the open lower lane around the blocked ingress: from=({rearMove.FromGridX},{rearMove.FromGridY}) to=({rearMove.ToGridX},{rearMove.ToGridY})");
        AssertTrue(
            rearBottom.LastAdvanceFailureReason != "reject_no_reachable_slot",
            "side-path ingress must not be degraded as reject_no_reachable_slot");
    }

    public static void RuntimeCombatIngressPlannerAllowsTemporaryDetourAroundBlockedCloserSteps()
    {
        BattleStartSnapshot snapshot = BuildTemporaryDetourIngressSnapshot();
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        BattleRuntimeActor rear = controller.State.Actors.Single(item => item.ActorId == "enemy_rear:1");
        rear.TargetActorId = "player_front:1";

        BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();

        BattleEvent? rearMove = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "enemy_rear:1");
        AssertTrue(
            rearMove != null,
            $"combat ingress should allow a temporary detour when all currently closer first steps are occupied but a dynamic path remains open: failure={rear.LastAdvanceFailureReason}");
        AssertTrue(
            rearMove!.ToGridX > rearMove.FromGridX,
            $"first detour step should move away from the blocked ingress before wrapping around: from=({rearMove.FromGridX},{rearMove.FromGridY}) to=({rearMove.ToGridX},{rearMove.ToGridY})");
    }

    public static void RuntimeLocalCombatMovementKeepsStableSlotIntentInsteadOfOscillating()
    {
        BattleStartSnapshot snapshot = BuildBonefieldOscillationStateSnapshot();
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        BattleRuntimeActor rear = controller.State.Actors.Single(item => item.ActorId == "bonefield:f6_draugarlord:3");
        rear.TargetActorId = "expedition:player_camp:1:army:f1_azuritelion:2";
        rear.PlanState = BattleGroupPlanRuntimeState.MovingToAttackSlot;

        for (int i = 0; i < 24 && !controller.IsComplete; i++)
        {
            _ = controller.AdvanceNextTick();
        }

        BattleEvent[] moves = controller.EventStream.Events
            .Where(item =>
                item.Kind == BattleEventKind.MovementStarted &&
                item.ActorId == "bonefield:f6_draugarlord:3" &&
                item.TargetId == "expedition:player_camp:1:army:f1_azuritelion:2")
            .ToArray();
        AssertTrue(moves.Length >= 3, $"fixture should produce enough local-combat movement to expose slot jitter: moves={DescribeMoves(moves)}");
        AssertNoImmediateReverseMoves(moves, "local combat movement should execute a stable combat-slot intent instead of alternating between two anchors");
        AssertTrue(
            moves.Any(item => item.ToGridX == 35 && item.ToGridY == 22 && item.ToGridHeight == 0),
            $"rear unit should keep moving toward the assigned support slot instead of just suppressing the reverse step: moves={DescribeMoves(moves)}");
    }

    public static void RuntimeBlockedLocalCombatKeepsCombatPressureInsteadOfPathFailure()
    {
        BattleStartSnapshot snapshot = BuildBlockedTargetPressureSnapshot();
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        BattleRuntimeActor pressure = controller.State.Actors.Single(item => item.ActorId == "player_pressure:1");
        pressure.TargetActorId = "enemy_anchor:1";
        pressure.PlanState = BattleGroupPlanRuntimeState.TargetLocked;

        BattleRuntimeAdvanceResult tick = controller.AdvanceNextTick();

        BattleEvent? pressureMove = tick.Events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "player_pressure:1");
        AssertTrue(
            pressureMove != null,
            $"blocked local combat should keep pressure through a legal step instead of idling: failure={pressure.LastAdvanceFailureReason}");
        AssertEqual("combat_pressure_advance", pressureMove!.ReasonCode, "pressure fallback movement reason");
        AssertEqual(3, pressureMove.ToGridX, "pressure fallback first step x");
        AssertEqual(0, pressureMove.ToGridY, "pressure fallback first step y");
        AssertTrue(
            pressure.LastAdvanceFailureReason != "path_not_found",
            "pressure fallback must not record local combat pressure as a terminal path failure");
    }

    private static BattleStartSnapshot BuildOpenFieldSnapshot(string battleId)
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = $"snapshot_{battleId}",
            BattleId = battleId,
            TargetLocationId = "site_1"
        };

        for (int x = 0; x <= 6; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                AddSurface(snapshot, x, y);
            }
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildSingleLaneSnapshot(string battleId)
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = $"snapshot_{battleId}",
            BattleId = battleId,
            TargetLocationId = "site_1"
        };

        for (int x = 0; x <= 6; x++)
        {
            AddSurface(snapshot, x, 0);
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildFourVersusFourOpenFieldSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_four_vs_four_open_field",
            BattleId = "battle_four_vs_four_open_field",
            TargetLocationId = "site_1"
        };

        for (int x = 0; x <= 12; x++)
        {
            for (int y = 0; y <= 5; y++)
            {
                AddSurface(snapshot, x, y);
            }
        }

        for (int i = 0; i < 4; i++)
        {
            AddGroup(snapshot, $"group_player_{i}", "player", $"player_{i}", 0, i, 160);
            AddGroup(snapshot, $"group_enemy_{i}", "enemy", $"enemy_{i}", 10, i, 160);
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildVerticalEngagementSupportSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_vertical_engagement_support",
            BattleId = "battle_vertical_engagement_support",
            TargetLocationId = "site_1"
        };

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 3; y++)
            {
                AddSurface(snapshot, x, y);
            }
        }

        AddGroup(snapshot, "group_player_front", "player", "player_front", 0, 1, 180);
        AddGroup(snapshot, "group_player_support", "player", "player_support", 0, 3, 180);
        AddGroup(snapshot, "group_enemy_anchor", "enemy", "enemy_anchor", 0, 0, 260, initialCommandId: "HoldLine");

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildOneStepBehindEngagedAllySnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_support_one_step_behind_engaged_ally",
            BattleId = "battle_support_one_step_behind_engaged_ally",
            TargetLocationId = "site_1"
        };

        for (int x = 2; x <= 4; x++)
        {
            for (int y = 0; y <= 2; y++)
            {
                AddSurface(snapshot, x, y);
            }
        }

        AddGroup(snapshot, "group_player_front", "player", "player_front", 3, 1, 120);
        AddGroup(snapshot, "group_player_support", "player", "player_support", 3, 2, 120);
        AddGroup(snapshot, "group_enemy_anchor", "enemy", "enemy_anchor", 3, 0, 220, initialCommandId: "HoldLine");

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildLargeTargetSurroundSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_large_target_surround",
            BattleId = "battle_large_target_surround",
            TargetLocationId = "site_1"
        };

        for (int x = 0; x <= 5; x++)
        {
            for (int y = -2; y <= 3; y++)
            {
                AddSurface(snapshot, x, y);
            }
        }

        AddGroup(snapshot, "group_player_left", "player", "player_left", 1, 0, 90);
        AddGroup(snapshot, "group_player_top", "player", "player_top", 3, -2, 90);
        AddGroup(snapshot, "group_enemy_large", "enemy", "enemy_large", 3, 0, 240, initialCommandId: "HoldLine", footprintWidth: 2, footprintHeight: 2);

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildRearBlockedLocalCombatSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_rear_blocked_local_combat_support",
            BattleId = "battle_rear_blocked_local_combat_support",
            TargetLocationId = "site_1"
        };

        for (int x = 32; x <= 40; x++)
        {
            for (int y = 17; y <= 23; y++)
            {
                AddSurface(snapshot, x, y);
            }
        }

        AddGroup(snapshot, "player_top", "player", "player_top", 34, 18, 160, runtimeCommanderGroupId: "player_company", attackRange: 2);
        AddGroup(snapshot, "player_front_a", "player", "player_front_a", 34, 19, 160, runtimeCommanderGroupId: "player_company", attackRange: 2);
        AddGroup(snapshot, "player_front_b", "player", "player_front_b", 34, 20, 160, runtimeCommanderGroupId: "player_company", attackRange: 2);
        AddGroup(snapshot, "player_front_c", "player", "player_front_c", 34, 21, 160, runtimeCommanderGroupId: "player_company", attackRange: 2);
        AddGroup(snapshot, "enemy_front_top", "enemy", "enemy_front_top", 36, 19, 220, footprintWidth: 2, footprintHeight: 2, runtimeCommanderGroupId: "enemy_company", attackRange: 2);
        AddGroup(snapshot, "enemy_front_bottom", "enemy", "enemy_front_bottom", 36, 21, 220, footprintWidth: 2, footprintHeight: 2, runtimeCommanderGroupId: "enemy_company", attackRange: 2);
        AddGroup(snapshot, "enemy_rear_top", "enemy", "enemy_rear_top", 38, 19, 220, footprintWidth: 2, footprintHeight: 2, runtimeCommanderGroupId: "enemy_company", attackRange: 2);
        AddGroup(snapshot, "enemy_rear_bottom", "enemy", "enemy_rear_bottom", 38, 21, 220, footprintWidth: 2, footprintHeight: 2, runtimeCommanderGroupId: "enemy_company", attackRange: 2);
        AddGroup(snapshot, "enemy_screen_top_left", "enemy", "enemy_screen_top_left", 38, 18, 80, initialCommandId: "HoldLine", runtimeCommanderGroupId: "enemy_company");
        AddGroup(snapshot, "enemy_screen_top_right", "enemy", "enemy_screen_top_right", 39, 18, 80, initialCommandId: "HoldLine", runtimeCommanderGroupId: "enemy_company");
        AddGroup(snapshot, "enemy_screen_right_top", "enemy", "enemy_screen_right_top", 40, 19, 80, initialCommandId: "HoldLine", runtimeCommanderGroupId: "enemy_company");
        AddGroup(snapshot, "enemy_screen_bottom_left", "enemy", "enemy_screen_bottom_left", 38, 23, 80, initialCommandId: "HoldLine", runtimeCommanderGroupId: "enemy_company");
        AddGroup(snapshot, "enemy_screen_bottom_right", "enemy", "enemy_screen_bottom_right", 39, 23, 80, initialCommandId: "HoldLine", runtimeCommanderGroupId: "enemy_company");
        AddGroup(snapshot, "enemy_screen_right_bottom", "enemy", "enemy_screen_right_bottom", 40, 21, 80, initialCommandId: "HoldLine", runtimeCommanderGroupId: "enemy_company");

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildRearBlockedIngressWithSidePathSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_rear_blocked_ingress_with_side_path",
            BattleId = "battle_rear_blocked_ingress_with_side_path",
            TargetLocationId = "site_1"
        };

        for (int x = 32; x <= 42; x++)
        {
            for (int y = 17; y <= 25; y++)
            {
                AddSurface(snapshot, x, y);
            }
        }

        AddGroup(snapshot, "player_top", "player", "player_top", 34, 18, 160, runtimeCommanderGroupId: "player_company", attackRange: 2);
        AddGroup(snapshot, "player_front_a", "player", "player_front_a", 34, 19, 160, runtimeCommanderGroupId: "player_company", attackRange: 2);
        AddGroup(snapshot, "player_front_b", "player", "player_front_b", 34, 20, 160, runtimeCommanderGroupId: "player_company", attackRange: 2);
        AddGroup(snapshot, "player_front_c", "player", "player_front_c", 34, 21, 160, runtimeCommanderGroupId: "player_company", attackRange: 2);
        AddGroup(snapshot, "enemy_front_top", "enemy", "enemy_front_top", 36, 19, 220, footprintWidth: 2, footprintHeight: 2, runtimeCommanderGroupId: "enemy_company", attackRange: 2);
        AddGroup(snapshot, "enemy_front_bottom", "enemy", "enemy_front_bottom", 36, 21, 220, footprintWidth: 2, footprintHeight: 2, runtimeCommanderGroupId: "enemy_company", attackRange: 2);
        AddGroup(snapshot, "enemy_rear_top", "enemy", "enemy_rear_top", 38, 19, 220, footprintWidth: 2, footprintHeight: 2, runtimeCommanderGroupId: "enemy_company", attackRange: 2);
        AddGroup(snapshot, "enemy_rear_bottom", "enemy", "enemy_rear_bottom", 38, 21, 220, footprintWidth: 2, footprintHeight: 2, runtimeCommanderGroupId: "enemy_company", attackRange: 2);

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildTemporaryDetourIngressSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_temporary_detour_ingress",
            BattleId = "battle_temporary_detour_ingress",
            TargetLocationId = "site_1"
        };

        for (int x = -2; x <= 5; x++)
        {
            for (int y = -2; y <= 4; y++)
            {
                AddSurface(snapshot, x, y);
            }
        }

        AddGroup(snapshot, "player_group", "player", "player_front", 0, 1, 260, initialCommandId: "HoldLine");
        AddGroup(snapshot, "enemy_rear_group", "enemy", "enemy_rear", 2, 1, 220, runtimeCommanderGroupId: "enemy_company");
        AddGroup(snapshot, "blocker_direct_group", "enemy", "blocker_direct", 1, 1, 220, initialCommandId: "HoldLine", runtimeCommanderGroupId: "enemy_company");
        AddGroup(snapshot, "blocker_upper_left_group", "enemy", "blocker_upper_left", 1, 0, 220, initialCommandId: "HoldLine", runtimeCommanderGroupId: "enemy_company");
        AddGroup(snapshot, "blocker_lower_left_group", "enemy", "blocker_lower_left", 1, 2, 220, initialCommandId: "HoldLine", runtimeCommanderGroupId: "enemy_company");
        AddGroup(snapshot, "blocker_upper_group", "enemy", "blocker_upper", 2, 0, 220, initialCommandId: "HoldLine", runtimeCommanderGroupId: "enemy_company");
        AddGroup(snapshot, "blocker_lower_group", "enemy", "blocker_lower", 2, 2, 220, initialCommandId: "HoldLine", runtimeCommanderGroupId: "enemy_company");

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildBlockedTargetPressureSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_blocked_target_pressure",
            BattleId = "battle_blocked_target_pressure",
            TargetLocationId = "site_1"
        };

        for (int x = 0; x <= 4; x++)
        {
            AddSurface(snapshot, x, 0);
        }

        AddGroup(snapshot, "player_front_group", "player", "player_front", 1, 0, 140, initialCommandId: "HoldLine");
        AddGroup(snapshot, "player_queue_group", "player", "player_queue", 2, 0, 140, initialCommandId: "HoldLine");
        AddGroup(snapshot, "player_pressure_group", "player", "player_pressure", 4, 0, 140);
        AddGroup(snapshot, "enemy_anchor_group", "enemy", "enemy_anchor", 0, 0, 220, initialCommandId: "HoldLine");

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    internal static BattleStartSnapshot BuildBonefieldOscillationStateSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_stable_combat_slot_intent",
            BattleId = "battle_stable_combat_slot_intent",
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

        for (int x = 31; x <= 43; x++)
        {
            for (int y = 14; y <= 26; y++)
            {
                AddSurface(snapshot, x, y);
            }
        }

        AddPlannedGroup(
            snapshot,
            "player_unit_1",
            "player",
            "expedition:player_camp:1:army:f1_grandmasterzir",
            35,
            17,
            600,
            "undead_deployment_zone_east_1",
            footprintWidth: 2,
            footprintHeight: 1);
        AddPlannedGroup(
            snapshot,
            "player_unit_2",
            "player",
            "expedition:player_camp:1:army:f1_azuritelion",
            34,
            19,
            600,
            "undead_deployment_zone_east_1",
            footprintWidth: 2,
            footprintHeight: 1);
        AddPlannedGroup(
            snapshot,
            "player_unit_3",
            "player",
            "expedition:player_camp:1:army:f1_azuritelion",
            36,
            20,
            600,
            "undead_deployment_zone_east_1",
            footprintWidth: 2,
            footprintHeight: 1);
        AddPlannedGroup(
            snapshot,
            "player_unit_4",
            "player",
            "expedition:player_camp:1:army:f1_azuritelion",
            34,
            21,
            600,
            "undead_deployment_zone_east_1",
            footprintWidth: 2,
            footprintHeight: 1);
        AddPlannedGroup(
            snapshot,
            "enemy_unit_1",
            "undead",
            "bonefield:f6_draugarlord",
            38,
            19,
            900,
            "player_deployment_zone_west_1",
            tacticalMode: BattleGroupTacticalMode.EnemyOffense,
            footprintWidth: 2,
            footprintHeight: 2);
        AddPlannedGroup(
            snapshot,
            "enemy_unit_2",
            "undead",
            "bonefield:f6_draugarlord",
            37,
            21,
            900,
            "player_deployment_zone_west_1",
            tacticalMode: BattleGroupTacticalMode.EnemyOffense,
            footprintWidth: 2,
            footprintHeight: 2);
        AddPlannedGroup(
            snapshot,
            "enemy_unit_3",
            "undead",
            "bonefield:f6_draugarlord",
            39,
            21,
            900,
            "player_deployment_zone_west_1",
            tacticalMode: BattleGroupTacticalMode.EnemyOffense,
            footprintWidth: 2,
            footprintHeight: 2);
        AddPlannedGroup(
            snapshot,
            "enemy_unit_4",
            "undead",
            "bonefield:f6_draugarlord",
            36,
            18,
            900,
            "player_deployment_zone_west_1",
            tacticalMode: BattleGroupTacticalMode.EnemyOffense,
            footprintWidth: 2,
            footprintHeight: 2);

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

    private static void AddGroup(
        BattleStartSnapshot snapshot,
        string groupId,
        string factionId,
        string sourceForceId,
        int cellX,
        int cellY,
        int hitPoints,
        string initialCommandId = "",
        int footprintWidth = 1,
        int footprintHeight = 1,
        string runtimeCommanderGroupId = "",
        int attackRange = 1)
    {
        snapshot.BattleGroups.Add(new BattleGroupSnapshot
        {
            BattleGroupId = groupId,
            RuntimeCommanderGroupId = runtimeCommanderGroupId,
            FactionId = factionId,
            SourceForceId = sourceForceId,
            HeroId = $"{sourceForceId}_hero",
            HeroDefinitionId = $"{sourceForceId}_hero_definition",
            CorpsId = $"{sourceForceId}_corps",
            CorpsDefinitionId = $"{sourceForceId}_corps_definition",
            CorpsStrength = hitPoints,
            MaxHitPoints = hitPoints,
            AttackDamage = 5,
            AttackRange = attackRange,
            AttackSpeed = 1.0,
            AttackImpactDelaySeconds = 0,
            SourceLocationId = factionId == "player" ? "city_1" : "site_1",
            CellX = cellX,
            CellY = cellY,
            InitialCorpsCommandId = initialCommandId,
            FootprintWidth = footprintWidth,
            FootprintHeight = footprintHeight
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
            AttackImpactDelaySeconds = 0,
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

    private static void AssertCompletedWithoutRuntimeException(BattleRuntimeSessionResult result, string message)
    {
        AssertTrue(result.Outcome.IsComplete, $"{message}: outcome should be complete");
        AssertTrue(
            result.Outcome.TerminationReason != BattleTerminationReason.RuntimeException,
            $"{message}: termination={result.Outcome.TerminationReason}");
    }

    private static void AssertActorsMoved(BattleRuntimeSessionResult result, params string[] actorIds)
    {
        foreach (string actorId in actorIds)
        {
            AssertTrue(
                result.EventStream.Events.Any(item => item.Kind == BattleEventKind.MovementCompleted && item.ActorId == actorId),
                $"actor should produce at least one runtime movement event: {actorId}");
        }
    }

    private static void AssertAllMovementDestinationsAreAuthored(BattleRuntimeSessionResult result, BattleStartSnapshot snapshot)
    {
        var authored = snapshot.LocationContext.NavigationSurfaces
            .Select(item => (item.X, item.Y, item.Height))
            .ToHashSet();
        foreach (BattleEvent movement in result.EventStream.Events.Where(item => item.Kind == BattleEventKind.MovementCompleted))
        {
            AssertTrue(
                authored.Contains((movement.ToGridX, movement.ToGridY, movement.ToGridHeight)),
                $"movement destination must stay on authored walkable surfaces: actor={movement.ActorId} to=({movement.ToGridX},{movement.ToGridY},{movement.ToGridHeight})");
        }
    }

    private static void AssertNoDuplicateMovementDestinationsPerTick(BattleRuntimeSessionResult result)
    {
        string[] duplicates = result.EventStream.Events
            .Where(item => item.Kind == BattleEventKind.MovementCompleted)
            .GroupBy(item => $"{item.RuntimeTick}:{item.ToGridX},{item.ToGridY},{item.ToGridHeight}")
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        AssertEqual(0, duplicates.Length, $"same-tick movement reservations should prevent duplicate destination cells: {string.Join(",", duplicates)}");
    }

    private static void AssertNoDuplicateLivingCorpsCells(BattleRuntimeSessionResult result)
    {
        string[] duplicates = result.FinalState.Actors
            .Where(item => item.Kind == BattleRuntimeActorKind.Corps && item.HitPoints > 0)
            .GroupBy(item => $"{item.GridX},{item.GridY},{item.GridHeight}")
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        AssertEqual(0, duplicates.Length, $"living corps should not end stacked on the same cell: {string.Join(",", duplicates)}");
    }

    private static void AssertAllMovesProgressX(BattleRuntimeSessionResult result, Func<string, bool> actorFilter, int expectedDirection)
    {
        foreach (BattleEvent movement in result.EventStream.Events.Where(item => item.Kind == BattleEventKind.MovementCompleted && actorFilter(item.ActorId)))
        {
            int deltaX = movement.ToGridX - movement.FromGridX;
            AssertTrue(
                Math.Sign(deltaX) == Math.Sign(expectedDirection) || deltaX == 0,
                $"movement should not step away from the opposing side: actor={movement.ActorId} fromX={movement.FromGridX} toX={movement.ToGridX}");
        }
    }

    private static void AssertMove(
        IReadOnlyList<BattleEvent> events,
        string actorId,
        int fromX,
        int fromY,
        int toX,
        int toY)
    {
        BattleEvent? movement = events.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == actorId);
        AssertTrue(movement != null, $"actor should move in this tick: {actorId}");
        AssertEqual(fromX, movement!.FromGridX, $"{actorId} movement from x");
        AssertEqual(fromY, movement.FromGridY, $"{actorId} movement from y");
        AssertEqual(toX, movement.ToGridX, $"{actorId} movement to x");
        AssertEqual(toY, movement.ToGridY, $"{actorId} movement to y");
    }

    private static void AssertNoMove(IReadOnlyList<BattleEvent> events, string actorId)
    {
        AssertTrue(
            events.All(item =>
                item.Kind != BattleEventKind.MovementStarted ||
                item.ActorId != actorId),
            $"actor should not move in this tick: {actorId}");
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

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"{message}: expected={expected} actual={actual}");
        }
    }
}
