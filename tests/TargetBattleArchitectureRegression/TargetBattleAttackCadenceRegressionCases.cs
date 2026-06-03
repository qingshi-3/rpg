using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;

internal static class TargetBattleAttackCadenceRegressionCases
{
    internal static void RuntimeAdjacentOpponentsResolveSameTickAttacksWithoutActorIdInitiative()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_same_tick_fairness", enemyCellX: 1, enemyCellY: 0);
        foreach (BattleGroupSnapshot group in snapshot.BattleGroups)
        {
            group.MaxHitPoints = 10;
            group.AttackDamage = 10;
            group.AttackRange = 1;
            group.AttackSpeed = 1.0;
        }

        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        BattleEvent[] tickZeroDamage = result.EventStream.Events
            .Where(item => item.Kind == BattleEventKind.DamageApplied && item.RuntimeTick == 0)
            .ToArray();
        AssertEqual(2, tickZeroDamage.Length, "adjacent opponents should both apply damage on tick zero");
        AssertTrue(
            tickZeroDamage.Any(item => item.ActorId == "force_player:1" && item.TargetId == "force_enemy:1"),
            "player should damage enemy on tick zero");
        AssertTrue(
            tickZeroDamage.Any(item => item.ActorId == "force_enemy:1" && item.TargetId == "force_player:1"),
            "enemy should damage player on tick zero");

        int playerTickZeroDamageReceived = tickZeroDamage
            .Where(item => item.TargetId == "force_player:1")
            .Sum(item => -item.CorpsStrengthDelta);
        int enemyTickZeroDamageReceived = tickZeroDamage
            .Where(item => item.TargetId == "force_enemy:1")
            .Sum(item => -item.CorpsStrengthDelta);
        AssertEqual(10, playerTickZeroDamageReceived, "player should receive tick-zero damage");
        AssertEqual(10, enemyTickZeroDamageReceived, "enemy should receive tick-zero damage");

        BattleRuntimeActor playerActor = result.FinalState.Actors.Single(item => item.ActorId == "force_player:1");
        BattleRuntimeActor enemyActor = result.FinalState.Actors.Single(item => item.ActorId == "force_enemy:1");
        AssertEqual(0, playerActor.HitPoints, "same-tick damage should allow mutual defeat");
        AssertEqual(0, enemyActor.HitPoints, "same-tick damage should allow mutual defeat");
    }

    internal static void RuntimeMoverCannotAttackUntilAnchoredOnLaterTick()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_move_then_attack", enemyCellX: 2, enemyCellY: 0);
        foreach (BattleGroupSnapshot group in snapshot.BattleGroups)
        {
            group.MaxHitPoints = 80;
            group.AttackDamage = 5;
            group.AttackRange = 1;
            group.AttackSpeed = 1.0;
        }

        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);
        BattleEvent[] tickZeroCombatEvents = result.EventStream.Events
            .Where(item =>
                item.RuntimeTick == 0 &&
                item.Kind is BattleEventKind.MovementStarted or BattleEventKind.DamageApplied)
            .ToArray();
        AssertTrue(
            tickZeroCombatEvents.Any(item => item.Kind == BattleEventKind.MovementStarted),
            "tick zero should include an approach movement");
        AssertTrue(
            tickZeroCombatEvents.All(item => item.Kind != BattleEventKind.DamageApplied),
            "movement into range should not allow same-tick damage");

        string[] actorIds = { "force_player:1", "force_enemy:1" };
        foreach (string actorId in actorIds)
        {
            var combatTicks = result.EventStream.Events
                .Where(item =>
                    item.ActorId == actorId &&
                    item.Kind is BattleEventKind.MovementCompleted or BattleEventKind.DamageApplied)
                .GroupBy(item => item.RuntimeTick)
                .Select(group => new
                {
                    RuntimeTick = group.Key,
                    HasMove = group.Any(item => item.Kind == BattleEventKind.MovementCompleted),
                    HasDamage = group.Any(item => item.Kind == BattleEventKind.DamageApplied)
                })
                .ToArray();
            AssertTrue(
                combatTicks.All(item => !(item.HasMove && item.HasDamage)),
                $"one actor cannot both move and attack in the same tick: actor={actorId}");

            int? firstDamageTick = result.EventStream.Events
                .Where(item => item.ActorId == actorId && item.Kind == BattleEventKind.DamageApplied)
                .Select(item => (int?)item.RuntimeTick)
                .Min();
            int? lastApproachMoveTick = result.EventStream.Events
                .Where(item =>
                    item.ActorId == actorId &&
                    item.Kind == BattleEventKind.MovementCompleted &&
                    (!firstDamageTick.HasValue || item.RuntimeTick < firstDamageTick.Value))
                .Select(item => (int?)item.RuntimeTick)
                .Max();
            if (firstDamageTick.HasValue && lastApproachMoveTick.HasValue)
            {
                AssertTrue(
                    firstDamageTick.Value > lastApproachMoveTick.Value,
                    $"first attack should happen after a completed approach tick: actor={actorId}");
            }
        }

        int? firstDamageAcrossBattle = result.EventStream.Events
            .Where(item => item.Kind == BattleEventKind.DamageApplied)
            .Select(item => (int?)item.RuntimeTick)
            .Min();
        AssertTrue(
            !firstDamageAcrossBattle.HasValue || firstDamageAcrossBattle.Value > 0,
            "first damage tick must be after the approach tick");
    }

    internal static void RuntimeDefeatedActorMoveProposalIsDiscardedAfterSameTickDamage()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_defeated_move_discard", enemyCellX: 3, enemyCellY: 0);
        BattleGroupSnapshot player = snapshot.BattleGroups.Single(item => item.SourceForceId == "force_player");
        BattleGroupSnapshot enemy = snapshot.BattleGroups.Single(item => item.SourceForceId == "force_enemy");
        player.MaxHitPoints = 30;
        player.AttackRange = 3;
        player.AttackDamage = 30;
        player.AttackSpeed = 1.0;
        enemy.MaxHitPoints = 20;
        enemy.AttackRange = 1;
        enemy.AttackDamage = 5;
        enemy.AttackSpeed = 1.0;

        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        BattleEvent[] tickZeroDamage = result.EventStream.Events
            .Where(item =>
                item.Kind == BattleEventKind.DamageApplied &&
                item.RuntimeTick == 0 &&
                item.ActorId == "force_player:1" &&
                item.TargetId == "force_enemy:1")
            .ToArray();
        AssertTrue(tickZeroDamage.Length > 0, "player should damage enemy on tick zero");

        BattleEvent[] tickZeroEnemyMoves = result.EventStream.Events
            .Where(item =>
                item.Kind == BattleEventKind.MovementCompleted &&
                item.RuntimeTick == 0 &&
                item.ActorId == "force_enemy:1")
            .ToArray();
        AssertEqual(0, tickZeroEnemyMoves.Length, "defeated actor must not emit movement in the same tick");

        BattleRuntimeActor enemyActor = result.FinalState.Actors.Single(item => item.ActorId == "force_enemy:1");
        AssertEqual(0, enemyActor.HitPoints, "enemy should be defeated by tick-zero damage");
    }

    internal static void RuntimeDoesNotBurstStoredAttackChargeAfterApproach()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_no_burst_after_approach", enemyCellX: 6, enemyCellY: 0);
        foreach (BattleGroupSnapshot group in snapshot.BattleGroups)
        {
            group.MaxHitPoints = 40;
            group.AttackDamage = 4;
            group.AttackSpeed = 1.0;
        }

        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        string[] burstGroups = result.EventStream.Events
            .Where(item => item.Kind == BattleEventKind.DamageApplied)
            .Select(item => new
            {
                item.ActorId,
                TickKey = ExtractTickKey(item.EventId)
            })
            .GroupBy(item => $"{item.ActorId}:{item.TickKey}")
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        AssertEqual(
            0,
            burstGroups.Length,
            $"one actor should not emit multiple attacks in one runtime tick after moving into contact: {string.Join(",", burstGroups)}");
    }

    internal static void RuntimeAttackRecoveryPreventsConsecutiveTickDamage()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_attack_recovery_lock", enemyCellX: 1, enemyCellY: 0);
        foreach (BattleGroupSnapshot group in snapshot.BattleGroups)
        {
            group.MaxHitPoints = 12;
            group.AttackDamage = 1;
            group.AttackRange = 1;
            group.AttackSpeed = 1.0;
        }

        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        foreach (string actorId in new[] { "force_player:1", "force_enemy:1" })
        {
            double[] damageTimes = result.EventStream.Events
                .Where(item => item.Kind == BattleEventKind.DamageApplied && item.ActorId == actorId)
                .Select(item => item.RuntimeTimeSeconds)
                .OrderBy(item => item)
                .ToArray();

            AssertTrue(damageTimes.Length >= 2, $"actor should attack more than once for cadence validation: actor={actorId}");
            AssertFloatEqual(0, damageTimes[0], 0.0001, $"first adjacent attack should still resolve at time zero: actor={actorId}");
            AssertTrue(
                damageTimes[1] >= damageTimes[0] + 1.2 - 0.0001,
                $"attack recovery should block until action seconds complete: actor={actorId} times=[{string.Join(",", damageTimes.Select(item => item.ToString("0.00")))}]");
            AssertTrue(
                damageTimes.Zip(damageTimes.Skip(1), (first, second) => second - first).All(delta => delta >= 1.2 - 0.0001),
                $"attack recovery should separate every repeated damage event by action seconds: actor={actorId} times=[{string.Join(",", damageTimes.Select(item => item.ToString("0.00")))}]");
        }
    }

    internal static void RuntimeAttackCadenceUsesActorActionSeconds()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_attack_action_seconds", enemyCellX: 1, enemyCellY: 0);
        foreach (BattleGroupSnapshot group in snapshot.BattleGroups)
        {
            group.MaxHitPoints = 20;
            group.AttackDamage = 1;
            group.AttackRange = 1;
            group.AttackSpeed = 1.0;
            group.AttackActionSeconds = 1.2;
            group.MoveStepSeconds = 0.16;
        }

        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        foreach (string actorId in new[] { "force_player:1", "force_enemy:1" })
        {
            double[] damageTimes = result.EventStream.Events
                .Where(item => item.Kind == BattleEventKind.DamageApplied && item.ActorId == actorId)
                .Select(item => item.RuntimeTimeSeconds)
                .OrderBy(item => item)
                .Take(2)
                .ToArray();

            AssertTrue(damageTimes.Length >= 2, $"actor should attack more than once for action timing validation: actor={actorId}");
            AssertFloatEqual(0, damageTimes[0], 0.0001, $"first adjacent attack should still resolve at time zero: actor={actorId}");
            AssertTrue(
                damageTimes[1] >= damageTimes[0] + 1.2 - 0.0001,
                $"repeated attack should wait for attack action seconds, not one integer tick: actor={actorId} times=[{string.Join(",", damageTimes.Select(item => item.ToString("0.00")))}]");
        }
    }

    internal static void RuntimeMovementCadenceUsesMoveStepSeconds()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_move_action_seconds", enemyCellX: 6, enemyCellY: 0);
        foreach (BattleGroupSnapshot group in snapshot.BattleGroups)
        {
            group.MaxHitPoints = 40;
            group.AttackDamage = 1;
            group.AttackRange = 1;
            group.AttackSpeed = 1.0;
            group.MoveStepSeconds = 0.16;
            group.AttackActionSeconds = 1.2;
        }

        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        foreach (string actorId in new[] { "force_player:1", "force_enemy:1" })
        {
            double[] moveTimes = result.EventStream.Events
                .Where(item => item.Kind == BattleEventKind.MovementCompleted && item.ActorId == actorId)
                .Select(item => item.RuntimeTimeSeconds)
                .OrderBy(item => item)
                .Take(2)
                .ToArray();

            AssertTrue(moveTimes.Length >= 2, $"actor should need multiple movement steps for move timing validation: actor={actorId}");
            AssertFloatEqual(
                0.16,
                moveTimes[1] - moveTimes[0],
                0.0001,
                $"consecutive movement steps should use move step seconds, not attack action seconds: actor={actorId}");
        }
    }

    internal static void RuntimeMovementPhaseAllowsNextTickMovementContinuation()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_movement_action_lock", enemyCellX: 6, enemyCellY: 0);
        foreach (BattleGroupSnapshot group in snapshot.BattleGroups)
        {
            group.MaxHitPoints = 40;
            group.AttackDamage = 1;
            group.AttackRange = 1;
            group.AttackSpeed = 1.0;
        }

        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        foreach (string actorId in new[] { "force_player:1", "force_enemy:1" })
        {
            int[] moveTicks = result.EventStream.Events
                .Where(item => item.Kind == BattleEventKind.MovementStarted && item.ActorId == actorId)
                .Select(item => item.RuntimeTick)
                .OrderBy(item => item)
                .ToArray();

            AssertTrue(moveTicks.Length >= 2, $"actor should need multiple approach steps for movement lock validation: actor={actorId}");
            AssertTrue(
                moveTicks.Zip(moveTicks.Skip(1), (first, second) => second - first).Any(delta => delta <= 2),
                $"continuous movement should allow a new step soon after the previous movement boundary: actor={actorId} ticks=[{string.Join(",", moveTicks)}]");
        }
    }

    internal static void RuntimeFixedClockHandsOffNextMovementSameTickAfterBoundary()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_fixed_boundary_movement_continue", enemyCellX: 6, enemyCellY: 0);
        foreach (BattleGroupSnapshot group in snapshot.BattleGroups)
        {
            group.MaxHitPoints = 80;
            group.AttackDamage = 1;
            group.AttackRange = 1;
            group.AttackSpeed = 1.0;
            group.MoveStepSeconds = 0.16;
            group.AttackActionSeconds = 1.2;
        }

        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        BattleRuntimeAdvanceResult firstAdvance = controller.AdvanceFixedTick(0.04);
        AssertTrue(
            firstAdvance.Events.Any(item => item.Kind == BattleEventKind.MovementStarted && item.ActorId == "force_player:1"),
            "fixed-clock runtime should start the first movement segment at tick zero");

        BattleRuntimeAdvanceResult? boundaryAdvance = null;
        for (int i = 0; i < 8; i++)
        {
            BattleRuntimeAdvanceResult advance = controller.AdvanceFixedTick(0.04);
            if (advance.Events.Any(item => item.Kind == BattleEventKind.MovementCompleted && item.ActorId == "force_player:1"))
            {
                boundaryAdvance = advance;
                break;
            }
        }

        AssertTrue(boundaryAdvance != null, "fixed-clock runtime should reach the first movement boundary");
        BattleEvent[] playerBoundaryEvents = boundaryAdvance!.Events
            .Where(item => item.ActorId == "force_player:1")
            .ToArray();
        BattleEvent? completed = playerBoundaryEvents.FirstOrDefault(item => item.Kind == BattleEventKind.MovementCompleted);
        AssertTrue(completed != null, "movement boundary tick should emit the completed cell boundary");
        BattleEvent? continued = playerBoundaryEvents.FirstOrDefault(item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "force_player:1");
        AssertTrue(continued != null, "movement continuation should be authored in the same runtime tick when movement intent is still valid");
        AssertTrue(
            Array.IndexOf(playerBoundaryEvents, completed) < Array.IndexOf(playerBoundaryEvents, continued),
            "same-tick continuation should appear after the committed movement boundary");
        AssertEqual(1, completed!.ToGridX, "first movement completion should commit the first approach cell");
        AssertEqual(completed.ToGridX, continued!.FromGridX, "same-tick continuation should start from the committed boundary cell");
        AssertEqual(2, continued.ToGridX, "same-tick continuation should move toward the next approach cell");
    }

    internal static void RuntimeFixedClockDefersAttackAfterMovementBoundary()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_fixed_boundary_attack_deferred", enemyCellX: 2, enemyCellY: 0);
        foreach (BattleGroupSnapshot group in snapshot.BattleGroups)
        {
            group.MaxHitPoints = 80;
            group.AttackDamage = 5;
            group.AttackRange = 1;
            group.AttackSpeed = 1.0;
            group.MoveStepSeconds = 0.16;
            group.AttackActionSeconds = 1.2;
        }

        snapshot.BattleGroups.Single(item => item.SourceForceId == "force_enemy").InitialCorpsCommandId = "HoldLine";

        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        _ = controller.AdvanceFixedTick(0.04);

        BattleRuntimeAdvanceResult? boundaryAdvance = null;
        for (int i = 0; i < 8; i++)
        {
            BattleRuntimeAdvanceResult advance = controller.AdvanceFixedTick(0.04);
            if (advance.Events.Any(item => item.Kind == BattleEventKind.MovementCompleted && item.ActorId == "force_player:1"))
            {
                boundaryAdvance = advance;
                break;
            }
        }

        AssertTrue(boundaryAdvance != null, "fixed-clock runtime should reach the approach movement boundary");
        AssertTrue(
            boundaryAdvance!.Events.All(item => item.ActorId != "force_player:1" || item.Kind != BattleEventKind.DamageApplied),
            "an actor that just completed movement must not also attack in the same runtime tick");

        BattleRuntimeAdvanceResult nextAdvance = controller.AdvanceFixedTick(0.04);
        AssertTrue(
            nextAdvance.Events.Any(item => item.Kind == BattleEventKind.DamageApplied && item.ActorId == "force_player:1"),
            "deferred attack should be available on the next runtime tick after the movement boundary");
    }

    internal static void RuntimeSessionBeginDefersCombatResolutionUntilAdvance()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_incremental_runtime", enemyCellX: 1, enemyCellY: 0);
        foreach (BattleGroupSnapshot group in snapshot.BattleGroups)
        {
            group.MaxHitPoints = 12;
            group.AttackDamage = 1;
            group.AttackRange = 1;
            group.AttackSpeed = 1.0;
        }

        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);

        AssertTrue(!controller.IsComplete, "begin should create a live runtime session instead of resolving the whole battle");
        AssertTrue(
            controller.EventStream.Events.All(item => item.Kind is not BattleEventKind.DamageApplied and not BattleEventKind.MovementCompleted and not BattleEventKind.BattleEnded),
            "begin should emit only initial semantic events before the first runtime advance");

        BattleRuntimeAdvanceResult firstAdvance = controller.AdvanceNextTick();

        AssertTrue(
            firstAdvance.Events.Any(item => item.Kind == BattleEventKind.DamageApplied && item.RuntimeTick == 0),
            "first runtime advance should produce the tick-zero combat events");
        AssertTrue(!controller.IsComplete, "high-hit-point battle should remain live after the first action slice");

        BattleRuntimeSessionController movementController = new BattleRuntimeSession().Begin(BuildOpposedSnapshot("battle_deferred_move_commit", enemyCellX: 6, enemyCellY: 0));
        BattleRuntimeAdvanceResult movementStart = movementController.AdvanceNextTick();
        BattleRuntimeActor movingPlayer = movementController.State.Actors.Single(item => item.ActorId == "force_player:1");
        AssertTrue(
            movementStart.Events.Any(item => item.Kind == BattleEventKind.MovementStarted && item.ActorId == "force_player:1") &&
            movementStart.Events.All(item => item.Kind != BattleEventKind.MovementCompleted || item.ActorId != "force_player:1"),
            "movement start should launch movement without emitting a completed boundary event");
        AssertEqual(0, movingPlayer.GridX, "movement start should not immediately commit the next runtime cell");
        AssertTrue(
            movingPlayer.Phase == BattleRuntimeActorPhase.Moving &&
            movingPlayer.HasMovementTarget &&
            movingPlayer.MovementProgress <= 0.0001,
            "runtime should store in-progress movement until the action boundary commits the cell");
        BattleRuntimeAdvanceResult movementComplete = movementController.AdvanceNextTick();
        AssertTrue(
            movementComplete.Events.Any(item => item.Kind == BattleEventKind.MovementCompleted && item.ActorId == "force_player:1"),
            "movement completion should emit only after runtime reaches the committed cell boundary");
        AssertEqual(1, movingPlayer.GridX, "movement completion should commit the runtime cell");

        BattleRuntimeSessionController fixedClockController = new BattleRuntimeSession().Begin(BuildOpposedSnapshot("battle_fixed_runtime_clock", enemyCellX: 6, enemyCellY: 0));
        BattleRuntimeAdvanceResult fixedClockFirst = fixedClockController.AdvanceFixedTick(0.04);
        BattleRuntimeAdvanceResult fixedClockSecond = fixedClockController.AdvanceFixedTick(0.04);

        AssertFloatEqual(0.04, fixedClockFirst.RuntimeTimeSeconds, 0.0001, "fixed runtime tick should advance by the configured simulation interval after resolving tick-zero actions");
        AssertFloatEqual(0.08, fixedClockSecond.RuntimeTimeSeconds, 0.0001, "fixed runtime tick should not jump directly to the next actor-ready boundary");
        AssertTrue(
            fixedClockSecond.Events.All(item => item.Kind != BattleEventKind.MovementCompleted),
            "actor-local movement lock should still prevent another cell commit before move step seconds expires");
    }

    internal static void RuntimeActionDiagnosticsAreLogged()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_action_log", enemyCellX: 3, enemyCellY: 0);

        GameLog.SetTraceCategoryEnabled("BattleRuntimeTickResolver", true);
        try
        {
            _ = new BattleRuntimeSession().RunMinimal(snapshot);
        }
        finally
        {
            GameLog.SetTraceCategoryEnabled("BattleRuntimeTickResolver", false);
        }

        AssertTrue(File.Exists(GameLog.CurrentLogPath), "runtime action diagnostics should write to the current game log");
        string log = File.ReadAllText(GameLog.CurrentLogPath);
        AssertTrue(
            log.Contains("BattleRuntimeAction battle=battle_action_log", StringComparison.Ordinal) &&
            log.Contains("action=AdvanceTowardTarget", StringComparison.Ordinal) &&
            log.Contains("action=AttackTarget", StringComparison.Ordinal) &&
            log.Contains("actorCell=", StringComparison.Ordinal) &&
            log.Contains("targetCell=", StringComparison.Ordinal) &&
            log.Contains("distance=", StringComparison.Ordinal),
            "runtime action diagnostics should record movement and attack decisions with battle id and spatial context");
    }

    internal static void RuntimeUsesSnapshotCombatHitPointsAndAttackDamage()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_combat_stats", enemyCellX: 1, enemyCellY: 0);
        BattleGroupSnapshot player = snapshot.BattleGroups.Single(item => item.SourceForceId == "force_player");
        BattleGroupSnapshot enemy = snapshot.BattleGroups.Single(item => item.SourceForceId == "force_enemy");
        player.MaxHitPoints = 12;
        player.AttackDamage = 5;
        enemy.MaxHitPoints = 9;
        enemy.AttackDamage = 1;

        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        BattleRuntimeActor playerActor = result.FinalState.Actors.Single(item => item.ActorId == "force_player:1");
        BattleRuntimeActor enemyActor = result.FinalState.Actors.Single(item => item.ActorId == "force_enemy:1");
        AssertEqual(5, playerActor.AttackDamage, "runtime actor should copy snapshot attack damage");
        AssertEqual(1, enemyActor.AttackDamage, "enemy runtime actor should copy snapshot attack damage");

        int[] playerDamageDeltas = result.EventStream.Events
            .Where(item => item.Kind == BattleEventKind.DamageApplied && item.ActorId == "force_player:1")
            .Select(item => item.CorpsStrengthDelta)
            .ToArray();
        AssertSequence(
            new[] { -5, -4 },
            playerDamageDeltas,
            "damage events should use snapshot attack damage and clamp the final hit to target hp");
        AssertEqual(0, enemyActor.HitPoints, "enemy hp should resolve from snapshot max hit points rather than corps strength");
    }

    private static BattleStartSnapshot BuildOpposedSnapshot(
        string battleId,
        int enemyCellX,
        int enemyCellY)
    {
        return new BattleStartSnapshot
        {
            SnapshotId = $"snapshot_{battleId}",
            BattleId = battleId,
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("group_player", "player", "force_player", "hero_player", "corps_player", 0, 0),
                BuildGroup("group_enemy", "enemy", "force_enemy", "hero_enemy", "corps_enemy", enemyCellX, enemyCellY)
            }
        };
    }

    private static BattleGroupSnapshot BuildGroup(
        string groupId,
        string factionId,
        string sourceForceId,
        string heroId,
        string corpsId,
        int cellX,
        int cellY)
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
            CellY = cellY
        };
    }

    private static string ExtractTickKey(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return "";
        }

        int start = eventId.IndexOf(":tick_", StringComparison.Ordinal);
        if (start < 0)
        {
            return "";
        }

        start++;
        int end = eventId.IndexOf(':', start);
        return end < 0 ? eventId[start..] : eventId[start..end];
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"{message}: expected={expected} actual={actual}");
        }
    }

    private static void AssertFloatEqual(double expected, double actual, double tolerance, string message)
    {
        if (Math.Abs(expected - actual) > tolerance)
        {
            throw new Exception($"{message}: expected={expected} actual={actual} tolerance={tolerance}");
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }

    private static void AssertSequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string message)
    {
        if (!expected.SequenceEqual(actual))
        {
            throw new Exception($"{message}: expected=[{string.Join(",", expected)}] actual=[{string.Join(",", actual)}]");
        }
    }
}
