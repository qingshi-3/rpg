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
            AssertTrue(counters.FlowFieldBuildCount > 0, "flow-field builds should be counted");
            AssertTrue(counters.FlowFieldCacheMissCount >= counters.FlowFieldBuildCount, "cache misses should cover builds");
            AssertTrue(counters.CombatSlotScanCount > 0, "combat slot scans should be counted");
            AssertTrue(counters.CombatSlotAnchorScanCount > 0, "combat slot scanned anchors should be counted");
            AssertTrue(
                counters.CombatSlotScanCount <= counters.FlowFieldBuildCount,
                $"open attack-slot checks should reuse flow-field goal slots instead of scanning slots again: scans={counters.CombatSlotScanCount} builds={counters.FlowFieldBuildCount}");
            AssertTrue(counters.LogWriteCount > 0, "runtime diagnostics should still write required logs");
            AssertTrue(counters.RuntimeAdvanceElapsedTicks > 0, "runtime advance elapsed ticks should be measured");
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
            string unitRoot = File.ReadAllText(Path.Combine(root, "src", "Presentation", "Battle", "Entities", "BattleUnitRoot.cs"));
            string animation = File.ReadAllText(Path.Combine(root, "src", "Presentation", "Battle", "Entities", "UnitAnimationComponent.cs"));
            string audio = File.ReadAllText(Path.Combine(root, "src", "Presentation", "Battle", "Entities", "BattleUnitAudioComponent.cs"));

            AssertTrue(
                unitRoot.Contains("GameLog.Trace(nameof(BattleUnitRoot)", StringComparison.Ordinal) &&
                animation.Contains("GameLog.Trace(nameof(UnitAnimationComponent)", StringComparison.Ordinal) &&
                audio.Contains("GameLog.Trace(nameof(BattleUnitAudioComponent)", StringComparison.Ordinal),
                "high-frequency movement, animation, and audio diagnostics should use the trace channel");
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
            AssertTrue(counters.CombatSlotScanCount > 0, "large topology should scan combat slots");
            long averageScannedAnchors = counters.CombatSlotAnchorScanCount / counters.CombatSlotScanCount;
            AssertTrue(
                averageScannedAnchors <= 128,
                $"combat slot scans should enumerate target-local candidates instead of the whole topology: average={averageScannedAnchors} scans={counters.CombatSlotScanCount} anchors={counters.CombatSlotAnchorScanCount}");
        }
        finally
        {
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
        int hitPoints)
    {
        snapshot.BattleGroups.Add(new BattleGroupSnapshot
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
            AttackDamage = 5,
            AttackRange = 1,
            AttackSpeed = 1.0,
            SourceLocationId = factionId == "player" ? "city_1" : "site_1",
            CellX = cellX,
            CellY = cellY
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
