using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Diagnostics;
using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle;

internal static class TargetBattlePerformanceRegressionCases
{
    public static void RuntimePerformanceCountersSeparateNavigationAndLoggingCosts()
    {
        BattlePerformanceCounters counters = new();
        GameLog.SetPerformanceCounters(counters);

        try
        {
            BattleRuntimeSessionResult result = new BattleRuntimeSession(performanceCounters: counters)
                .RunMinimal(BuildManyVsManyOpenFieldSnapshot());

            AssertTrue(result.Outcome.IsComplete, "performance scenario should complete through normal runtime");
            AssertTrue(counters.RuntimeTickCount > 0, "runtime ticks should be counted");
            AssertTrue(counters.DecisionReadyActorCount > 0, "decision-ready actors should be counted");
            AssertEqual(0L, counters.FlowFieldBuildCount, "first-slice runtime hot paths should not build flow fields");
            AssertEqual(0L, counters.FlowFieldCacheMissCount, "first-slice runtime hot paths should not miss flow-field cache entries");
            AssertEqual(0L, counters.FlowFieldCacheHitCount, "first-slice runtime hot paths should not hit flow-field cache entries");
            AssertTrue(counters.CombatSlotScanCount >= 0, "combat slot scans should remain observable");
            AssertTrue(counters.CombatSlotAnchorScanCount >= 0, "combat slot scanned anchors should remain observable");
            AssertTrue(counters.LogWriteCount > 0, "runtime diagnostics should still write required logs");
            AssertTrue(counters.RuntimeAdvanceElapsedTicks > 0, "runtime advance elapsed ticks should be measured");
            AssertEqual(0L, GetCounterValue(counters, "FlowFieldBuildElapsedTicks"), "flow-field build elapsed ticks should stay zero on hot paths");
            AssertTrue(GetCounterValue(counters, "CombatSlotScanElapsedTicks") >= 0, "combat slot scan elapsed ticks should be measured separately when scans occur");
            AssertTrue(GetCounterValue(counters, "TargetScoringElapsedTicks") > 0, "target scoring elapsed ticks should be measured separately");
            AssertTrue(GetCounterValue(counters, "MovementResolveElapsedTicks") > 0, "movement resolve elapsed ticks should be measured separately");
            AssertTrue(GetCounterValue(counters, "RuntimeAdvanceTickAtMax") >= 0, "runtime advance max should record the tick that spiked");
            AssertEqual(0L, GetCounterValue(counters, "OpenAttackFlowFieldBuildCount"), "open attack-slot checks should use target-local slot facts without flow fields");
            AssertEqual(0L, GetCounterValue(counters, "OpenAttackFlowFieldRequestCount"), "open attack-slot checks should not request flow fields");
            AssertEqual(0L, GetCounterValue(counters, "OpenAttackFlowFieldCacheHitCount"), "open attack-slot checks should not use flow-field cache hits");
            if (counters.CombatSlotScanCount > 0)
            {
                AssertTrue(GetCounterValue(counters, "CombatSlotScanElapsedTicks") > 0, "combat slot scan elapsed ticks should be measured separately when scans occur");
                long averageScannedAnchors = counters.CombatSlotAnchorScanCount / counters.CombatSlotScanCount;
                AssertTrue(
                    averageScannedAnchors <= 128,
                    $"combat slot scans should stay target-local when they occur: average={averageScannedAnchors} scans={counters.CombatSlotScanCount}");
            }
            AssertTrue(GetCounterValue(counters, "MovementEventsLastAdvance") >= 0, "last advance movement event count should be exposed");
            AssertTrue(GetCounterValue(counters, "ReservationRejectedCount") >= 0, "reservation rejection count should be exposed");
            AssertTrue(GetCounterValue(counters, "ReservationRejectedLastAdvance") >= 0, "last advance reservation rejection count should be exposed");
            AssertTrue(GetCounterValue(counters, "HoldDueReservationCount") >= 0, "reservation-hold count should be exposed");
            AssertTrue(GetCounterValue(counters, "ActorsReadyNoMoveLastAdvance") >= 0, "last advance ready-without-move count should be exposed");
            AssertTrue(GetCounterValue(counters, "MaxMovementEventGapMicroseconds") > 0, "movement-event runtime gap should be measured for cadence diagnosis");
            Console.WriteLine(
                $"PERF battle_movement runtimeTicks={counters.RuntimeTickCount} decisionActors={counters.DecisionReadyActorCount} flowBuilds={counters.FlowFieldBuildCount} flowHits={counters.FlowFieldCacheHitCount} flowMisses={counters.FlowFieldCacheMissCount} slotScans={counters.CombatSlotScanCount} slotAnchors={counters.CombatSlotAnchorScanCount} movementEvents={counters.MovementEventCount} logWrites={counters.LogWriteCount} logTicks={counters.LogWriteElapsedTicks} runtimeTicksElapsed={counters.RuntimeAdvanceElapsedTicks}");
        }
        finally
        {
            GameLog.SetPerformanceCounters(null);
        }
    }

    public static void HighFrequencyBattlePresentationLogsUseTraceChannel()
    {
        BattlePerformanceCounters counters = new();
        GameLog.SetPerformanceCounters(counters);

        try
        {
            GameLog.Trace("battle_move_trace_test", "hidden trace");
            AssertEqual(0L, counters.LogWriteCount, "trace diagnostics should be disabled by default");
            AssertEqual(1L, counters.LogSuppressedCount, "disabled trace diagnostics should be counted as suppressed");

            GameLog.SetTraceCategoryEnabled("battle_move_trace_test", true);
            GameLog.Trace("battle_move_trace_test", "visible trace");
            AssertEqual(1L, counters.LogWriteCount, "explicitly enabled trace diagnostics should write");

            string root = ProjectRoot();
            string unitRoot = string.Join("\n", Directory
                .GetFiles(Path.Combine(root, "src", "Presentation", "Battle", "Entities"), "BattleUnitRoot*.cs")
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
            string animation = File.ReadAllText(Path.Combine(root, "src", "Presentation", "Battle", "Entities", "UnitAnimationComponent.cs"));
            string audio = File.ReadAllText(Path.Combine(root, "src", "Presentation", "Battle", "Entities", "BattleUnitAudioComponent.cs"));
            string movementCommit = File
                .ReadAllText(Path.Combine(root, "src", "Runtime", "Battle", "BattleMovementCommitResolver.cs"))
                .Replace("\r\n", "\n", StringComparison.Ordinal);

            AssertTrue(
                unitRoot.Contains("GameLog.Trace(nameof(BattleUnitRoot)", StringComparison.Ordinal) &&
                animation.Contains("GameLog.Trace(nameof(UnitAnimationComponent)", StringComparison.Ordinal) &&
                audio.Contains("GameLog.Trace(nameof(BattleUnitAudioComponent)", StringComparison.Ordinal) &&
                movementCommit.Contains("GameLog.Trace(\n            nameof(BattleMovementCommitResolver)", StringComparison.Ordinal) &&
                !movementCommit.Contains("GameLog.Info(\n            nameof(BattleMovementCommitResolver)", StringComparison.Ordinal),
                "high-frequency movement, animation, audio, and combat slot diagnostics should use the trace channel");
        }
        finally
        {
            GameLog.SetTraceCategoryEnabled("battle_move_trace_test", false);
            GameLog.SetPerformanceCounters(null);
        }
    }

    public static void RuntimeCombatSlotScansStayBoundedNearTargetOnLargeTopology()
    {
        BattlePerformanceCounters counters = new();
        GameLog.SetPerformanceCounters(counters);

        try
        {
            BattleRuntimeSessionResult result = new BattleRuntimeSession(performanceCounters: counters)
                .RunMinimal(BuildLargeOpenFieldSnapshot());

            AssertTrue(result.Outcome.IsComplete, "large topology performance scenario should complete through normal runtime");
            if (counters.CombatSlotScanCount > 0)
            {
                long averageScannedAnchors = counters.CombatSlotAnchorScanCount / counters.CombatSlotScanCount;
                AssertTrue(
                    averageScannedAnchors <= 128,
                    $"combat slot scans should enumerate target-local candidates instead of the whole topology: average={averageScannedAnchors} scans={counters.CombatSlotScanCount} anchors={counters.CombatSlotAnchorScanCount}");
            }
        }
        finally
        {
            GameLog.SetPerformanceCounters(null);
        }
    }

    public static void RuntimeLocalCombatPositionSelectionUsesLocalNeighborResolver()
    {
        string root = ProjectRoot();
        string intentResolver = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Runtime",
            "Battle",
            "Navigation",
            "BattleCombatSlotIntentResolver.cs"));
        string crowdPlanner = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Runtime",
            "Battle",
            "Navigation",
            "BattleCrowdMovementPlanner.cs"));

        AssertTrue(
            !intentResolver.Contains("TryScoreSlot", StringComparison.Ordinal) &&
            !intentResolver.Contains("BuildScoredCandidates", StringComparison.Ordinal),
            "local-combat position selection must not score every candidate by building a separate flow field");
        AssertTrue(
            !intentResolver.Contains("GetOrBuildGoalField", StringComparison.Ordinal) &&
            !crowdPlanner.Contains("GetOrBuildGoalField", StringComparison.Ordinal) &&
            crowdPlanner.Contains("FindNextStepCandidatesTowardCombatSlot", StringComparison.Ordinal),
            "local-combat position selection should use target-local slots and local neighbor movement instead of goal fields");
    }

    public static void RuntimeLocalCombatMovementDoesNotBuildFlowFieldsOnLargeTopology()
    {
        BattlePerformanceCounters counters = new();
        BattleRuntimeSessionResult result = new BattleRuntimeSession(performanceCounters: counters)
            .RunMinimal(BuildLargeCombatJoinScopedFieldSnapshot());

        AssertTrue(result.Outcome.IsComplete, "large topology local-neighbor scenario should complete through normal runtime");
        AssertEqual(0L, GetCounterValue(counters, "FlowFieldBuildCount"), "local-combat movement should not build flow fields");
        AssertEqual(0L, GetCounterValue(counters, "ScopedFlowFieldBuildCount"), "local-combat movement should not build scoped flow fields");
        AssertEqual(0L, GetCounterValue(counters, "OpenAttackFlowFieldBuildCount"), "local-combat movement should not build open-attack flow fields");
        AssertEqual(0L, GetCounterValue(counters, "MaxScopedFlowFieldSearchNodes"), "local-combat movement should not search scoped flow-field nodes");
    }

    public static void RuntimeLocalCombatGoalFieldsAreNotHotPath()
    {
        string root = ProjectRoot();
        string intentResolver = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Runtime",
            "Battle",
            "Navigation",
            "BattleCombatSlotIntentResolver.cs"));
        string crowdPlanner = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Runtime",
            "Battle",
            "Navigation",
            "BattleCrowdMovementPlanner.cs"));
        AssertTrue(
            !intentResolver.Contains("GetOrBuildGoalField", StringComparison.Ordinal) &&
            !crowdPlanner.Contains("GetOrBuildGoalField", StringComparison.Ordinal) &&
            !intentResolver.Contains("BattleFlowFieldBuilder", StringComparison.Ordinal) &&
            !crowdPlanner.Contains("BattleFlowFieldBuilder", StringComparison.Ordinal),
            "local-combat slot movement should not use flow-field builders or goal-field cache entry points");
    }

    public static void RuntimeNavigationHotPathsAvoidStringKeysAndLinqSorts()
    {
        string root = ProjectRoot();
        string navigationRoot = Path.Combine(root, "src", "Runtime", "Battle", "Navigation");
        string crowdPlanner = File.ReadAllText(Path.Combine(
            navigationRoot,
            "BattleCrowdMovementPlanner.cs"));
        string slotAllocator = File.ReadAllText(Path.Combine(
            navigationRoot,
            "BattleCombatSlotAllocator.cs"));
        string slotIntentResolver = File.ReadAllText(Path.Combine(
            navigationRoot,
            "BattleCombatSlotIntentResolver.cs"));
        string movementCommitResolver = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Runtime",
            "Battle",
            "BattleMovementCommitResolver.cs"));

        AssertTrue(!File.Exists(Path.Combine(navigationRoot, "BattleFlowFieldCache.cs")), "flow-field cache should not remain in first-slice runtime navigation");
        AssertTrue(!File.Exists(Path.Combine(navigationRoot, "BattleFlowFieldBuilder.cs")), "flow-field builder should not remain in first-slice runtime navigation");
        AssertTrue(!File.Exists(Path.Combine(navigationRoot, "BattlePathfinder.cs")), "per-actor pathfinder should not remain in first-slice runtime navigation");
        AssertNoLinqSortChain(crowdPlanner, "crowd movement planner");
        AssertNoLinqSortChain(slotAllocator, "combat slot allocator");
        AssertNoLinqSortChain(slotIntentResolver, "combat slot intent resolver");
        AssertNoLinqSortChain(movementCommitResolver, "movement commit resolver");
    }

    public static void RuntimeSpikeDiagnosticsWriteAutomaticSummary()
    {
        const string ThresholdEnvironmentVariable = "RPG_BATTLE_RUNTIME_SPIKE_DIAGNOSTIC_MS";
        string? previousThreshold = Environment.GetEnvironmentVariable(ThresholdEnvironmentVariable);
        Environment.SetEnvironmentVariable(ThresholdEnvironmentVariable, "0");
        BattlePerformanceCounters counters = new();
        GameLog.SetPerformanceCounters(counters);

        try
        {
            BattleRuntimeSessionResult result = new BattleRuntimeSession(performanceCounters: counters)
                .RunMinimal(BuildSpikeDiagnosticSnapshot());

            AssertTrue(result.Outcome.IsComplete, "spike diagnostic scenario should complete through normal runtime");
            string log = File.Exists(GameLog.CurrentLogPath)
                ? File.ReadAllText(GameLog.CurrentLogPath)
                : "";
            AssertTrue(
                log.Contains("BattleRuntimeSpike battle=battle_runtime_spike_diagnostics", StringComparison.Ordinal) &&
                log.Contains("targetScoringMs=", StringComparison.Ordinal) &&
                log.Contains("flowFieldBuildMs=", StringComparison.Ordinal) &&
                log.Contains("flowFieldBuilds=", StringComparison.Ordinal) &&
                log.Contains("flowFieldSearchNodes=", StringComparison.Ordinal) &&
                log.Contains("scopedFlowFieldBuilds=", StringComparison.Ordinal) &&
                log.Contains("fullFlowFieldFallbacks=", StringComparison.Ordinal) &&
                log.Contains("flowFieldHits=", StringComparison.Ordinal) &&
                log.Contains("flowFieldMisses=", StringComparison.Ordinal) &&
                log.Contains("openAttackFlowFieldRequests=", StringComparison.Ordinal) &&
                log.Contains("openAttackFlowFieldCacheHits=", StringComparison.Ordinal) &&
                log.Contains("openAttackFlowFieldBuilds=", StringComparison.Ordinal) &&
                log.Contains("combatSlotScanMs=", StringComparison.Ordinal) &&
                log.Contains("movementResolveMs=", StringComparison.Ordinal) &&
                log.Contains("movementEvents=", StringComparison.Ordinal) &&
                log.Contains("presentationObserveMsMax=", StringComparison.Ordinal) &&
                log.Contains("logWriteMsMax=", StringComparison.Ordinal) &&
                log.Contains("reservationRejected=", StringComparison.Ordinal) &&
                log.Contains("actorsReadyNoMove=", StringComparison.Ordinal),
                "runtime spike diagnostics should write one self-contained summary instead of requiring manual monitor reading");
        }
        finally
        {
            Environment.SetEnvironmentVariable(ThresholdEnvironmentVariable, previousThreshold);
            GameLog.SetPerformanceCounters(null);
        }
    }

    private static BattleStartSnapshot BuildManyVsManyOpenFieldSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_performance_many_vs_many",
            BattleId = "battle_performance_many_vs_many",
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
            AddGroup(snapshot, $"group_player_{i}", "player", $"player_perf_{i}", 0, i, 160);
            AddGroup(snapshot, $"group_enemy_{i}", "enemy", $"enemy_perf_{i}", 10, i, 160);
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildLargeOpenFieldSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_performance_large_open_field",
            BattleId = "battle_performance_large_open_field",
            TargetLocationId = "site_1"
        };

        for (int x = 0; x < 64; x++)
        {
            for (int y = 0; y < 64; y++)
            {
                AddSurface(snapshot, x, y);
            }
        }

        AddGroup(snapshot, "group_player_large", "player", "player_perf_large", 0, 0, 80);
        AddGroup(snapshot, "group_enemy_large", "enemy", "enemy_perf_large", 8, 0, 80);

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildLargeCombatJoinScopedFieldSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_performance_large_scoped_combat_join",
            BattleId = "battle_performance_large_scoped_combat_join",
            TargetLocationId = "site_1"
        };

        for (int x = 0; x < 64; x++)
        {
            for (int y = 0; y < 64; y++)
            {
                AddSurface(snapshot, x, y);
            }
        }

        AddGroup(
            snapshot,
            "group_player_scoped_front",
            "player",
            "player_scoped_front",
            30,
            30,
            160,
            runtimeCommanderGroupId: "player_scoped_group");
        AddGroup(
            snapshot,
            "group_player_scoped_rear",
            "player",
            "player_scoped_rear",
            28,
            32,
            160,
            runtimeCommanderGroupId: "player_scoped_group");
        AddGroup(
            snapshot,
            "group_enemy_scoped_front",
            "enemy",
            "enemy_scoped_front",
            34,
            30,
            160,
            tacticalMode: BattleGroupTacticalMode.EnemyOffense,
            runtimeCommanderGroupId: "enemy_scoped_group");
        AddGroup(
            snapshot,
            "group_enemy_scoped_rear",
            "enemy",
            "enemy_scoped_rear",
            38,
            32,
            160,
            tacticalMode: BattleGroupTacticalMode.EnemyOffense,
            runtimeCommanderGroupId: "enemy_scoped_group");

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildSpikeDiagnosticSnapshot()
    {
        BattleStartSnapshot snapshot = BuildManyVsManyOpenFieldSnapshot();
        snapshot.SnapshotId = "snapshot_runtime_spike_diagnostics";
        snapshot.BattleId = "battle_runtime_spike_diagnostics";
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
        BattleGroupTacticalMode tacticalMode = BattleGroupTacticalMode.PlayerCommanded,
        string runtimeCommanderGroupId = "")
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
            AttackRange = 1,
            AttackSpeed = 1.0,
            SourceLocationId = factionId == "player" ? "city_1" : "site_1",
            CellX = cellX,
            CellY = cellY,
            TacticalMode = tacticalMode
        });
    }

    private static string ProjectRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current != null && !File.Exists(Path.Combine(current.FullName, "rpg.csproj")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("project root not found");
    }

    private static long GetCounterValue(BattlePerformanceCounters counters, string propertyName)
    {
        object? value = typeof(BattlePerformanceCounters)
            .GetProperty(propertyName)
            ?.GetValue(counters);
        return value switch
        {
            long longValue => longValue,
            int intValue => intValue,
            _ => throw new Exception($"missing long counter property: {propertyName}")
        };
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

    private static void AssertNoLinqSortChain(string source, string name)
    {
        AssertTrue(
            !source.Contains(".OrderBy(", StringComparison.Ordinal) &&
            !source.Contains(".ThenBy(", StringComparison.Ordinal),
            $"{name} should use deterministic comparer sorting instead of LINQ sort chains on Runtime hot paths");
    }
}
