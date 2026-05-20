using System;
using System.Diagnostics;
using Godot;
using Rpg.Infrastructure.Diagnostics;
using Rpg.Infrastructure.Logging;

namespace Rpg.Presentation.Debug;

public static class BattlePerformanceMonitorRegistry
{
    private const string RuntimeAdvanceMsLast = "Battle/RuntimeAdvanceMsLast";
    private const string RuntimeAdvanceMsMax = "Battle/RuntimeAdvanceMsMax";
    private const string RuntimeTicks = "Battle/RuntimeTicks";
    private const string DecisionActors = "Battle/DecisionActors";
    private const string FlowFieldBuilds = "Battle/FlowFieldBuilds";
    private const string FlowFieldCacheHits = "Battle/FlowFieldCacheHits";
    private const string FlowFieldCacheMisses = "Battle/FlowFieldCacheMisses";
    private const string CombatSlotScans = "Battle/CombatSlotScans";
    private const string CombatSlotAnchors = "Battle/CombatSlotAnchors";
    private const string MovementEvents = "Battle/MovementEvents";
    private const string MovementTweensCreated = "Battle/MovementTweensCreated";
    private const string MovementTweensInterrupted = "Battle/MovementTweensInterrupted";
    private const string ActiveMovementTweens = "Battle/ActiveMovementTweens";
    private const string LogWrites = "Battle/LogWrites";
    private const string LogWriteMsLast = "Battle/LogWriteMsLast";
    private const string LogWriteMsMax = "Battle/LogWriteMsMax";

    private static readonly string[] MonitorIds =
    {
        RuntimeAdvanceMsLast,
        RuntimeAdvanceMsMax,
        RuntimeTicks,
        DecisionActors,
        FlowFieldBuilds,
        FlowFieldCacheHits,
        FlowFieldCacheMisses,
        CombatSlotScans,
        CombatSlotAnchors,
        MovementEvents,
        MovementTweensCreated,
        MovementTweensInterrupted,
        ActiveMovementTweens,
        LogWrites,
        LogWriteMsLast,
        LogWriteMsMax
    };

    private static BattlePerformanceCounters _counters;
    private static Func<double> _activeMovementTweens;

    public static void Register(BattlePerformanceCounters counters, Func<double> activeMovementTweens)
    {
        _counters = counters;
        _activeMovementTweens = activeMovementTweens;
        GameLog.SetPerformanceCounters(counters);

        Add(RuntimeAdvanceMsLast, () => ToMilliseconds(_counters?.LastRuntimeAdvanceElapsedTicks ?? 0));
        Add(RuntimeAdvanceMsMax, () => ToMilliseconds(_counters?.MaxRuntimeAdvanceElapsedTicks ?? 0));
        Add(RuntimeTicks, () => _counters?.RuntimeTickCount ?? 0);
        Add(DecisionActors, () => _counters?.DecisionReadyActorCount ?? 0);
        Add(FlowFieldBuilds, () => _counters?.FlowFieldBuildCount ?? 0);
        Add(FlowFieldCacheHits, () => _counters?.FlowFieldCacheHitCount ?? 0);
        Add(FlowFieldCacheMisses, () => _counters?.FlowFieldCacheMissCount ?? 0);
        Add(CombatSlotScans, () => _counters?.CombatSlotScanCount ?? 0);
        Add(CombatSlotAnchors, () => _counters?.CombatSlotAnchorScanCount ?? 0);
        Add(MovementEvents, () => _counters?.MovementEventCount ?? 0);
        Add(MovementTweensCreated, () => _counters?.MovementTweenCreatedCount ?? 0);
        Add(MovementTweensInterrupted, () => _counters?.MovementTweenInterruptedCount ?? 0);
        Add(ActiveMovementTweens, () => _activeMovementTweens?.Invoke() ?? 0);
        Add(LogWrites, () => _counters?.LogWriteCount ?? 0);
        Add(LogWriteMsLast, () => ToMilliseconds(_counters?.LastLogWriteElapsedTicks ?? 0));
        Add(LogWriteMsMax, () => ToMilliseconds(_counters?.MaxLogWriteElapsedTicks ?? 0));
    }

    public static void Unregister()
    {
        foreach (string id in MonitorIds)
        {
            RemoveIfPresent(id);
        }

        _activeMovementTweens = null;
        _counters = null;
        GameLog.SetPerformanceCounters(null);
    }

    private static void Add(string id, Func<double> valueProvider)
    {
        RemoveIfPresent(id);
        Performance.AddCustomMonitor(id, Callable.From(valueProvider));
    }

    private static void RemoveIfPresent(string id)
    {
        var name = new StringName(id);
        if (Performance.HasCustomMonitor(name))
        {
            Performance.RemoveCustomMonitor(name);
        }
    }

    private static double ToMilliseconds(long elapsedTicks)
    {
        return elapsedTicks <= 0
            ? 0
            : elapsedTicks * 1000.0 / Stopwatch.Frequency;
    }
}
