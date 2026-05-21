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
    private const string RuntimeAdvanceTickAtMax = "Battle/RuntimeAdvanceTickAtMax";
    private const string RuntimeTicks = "Battle/RuntimeTicks";
    private const string DecisionActors = "Battle/DecisionActors";
    private const string FlowFieldBuilds = "Battle/FlowFieldBuilds";
    private const string FlowFieldBuildMsLast = "Battle/FlowFieldBuildMsLast";
    private const string FlowFieldBuildMsMax = "Battle/FlowFieldBuildMsMax";
    private const string FlowFieldCacheHits = "Battle/FlowFieldCacheHits";
    private const string FlowFieldCacheMisses = "Battle/FlowFieldCacheMisses";
    private const string CombatSlotScans = "Battle/CombatSlotScans";
    private const string CombatSlotAnchors = "Battle/CombatSlotAnchors";
    private const string CombatSlotScanMsLast = "Battle/CombatSlotScanMsLast";
    private const string CombatSlotScanMsMax = "Battle/CombatSlotScanMsMax";
    private const string TargetScoringMsLast = "Battle/TargetScoringMsLast";
    private const string TargetScoringMsMax = "Battle/TargetScoringMsMax";
    private const string MovementResolveMsLast = "Battle/MovementResolveMsLast";
    private const string MovementResolveMsMax = "Battle/MovementResolveMsMax";
    private const string MovementEvents = "Battle/MovementEvents";
    private const string MovementEventsLastAdvance = "Battle/MovementEventsLastAdvance";
    private const string ReservationRejectedCount = "Battle/ReservationRejectedCount";
    private const string ReservationRejectedLastAdvance = "Battle/ReservationRejectedLastAdvance";
    private const string HoldDueReservationCount = "Battle/HoldDueReservationCount";
    private const string ActorsReadyNoMoveLastAdvance = "Battle/ActorsReadyNoMoveLastAdvance";
    private const string MovementEventGapMsMax = "Battle/MovementEventGapMsMax";
    private const string MovementTweensCreated = "Battle/MovementTweensCreated";
    private const string MovementTweensInterrupted = "Battle/MovementTweensInterrupted";
    private const string ActiveMovementTweens = "Battle/ActiveMovementTweens";
    private const string PresentationObserveMsLast = "Battle/PresentationObserveMsLast";
    private const string PresentationObserveMsMax = "Battle/PresentationObserveMsMax";
    private const string PresentationObserveCount = "Battle/PresentationObserveCount";
    private const string LogWrites = "Battle/LogWrites";
    private const string LogWriteMsLast = "Battle/LogWriteMsLast";
    private const string LogWriteMsMax = "Battle/LogWriteMsMax";
    private const string GodotFps = "Godot/Fps";
    private const string GodotFrameMs = "Godot/FrameMs";
    private const string GodotPhysicsMs = "Godot/PhysicsMs";
    private const string GodotNodes = "Godot/Nodes";
    private const string GodotResources = "Godot/Resources";
    private const string GodotOrphanNodes = "Godot/OrphanNodes";
    private const string GodotDrawCalls = "Godot/DrawCalls";
    private const string GodotObjectsInFrame = "Godot/ObjectsInFrame";
    private const string GodotPrimitives = "Godot/Primitives";
    private const string GodotStaticMemoryMiB = "Godot/StaticMemoryMiB";
    private const string GodotStaticMemoryMaxMiB = "Godot/StaticMemoryMaxMiB";
    private const string GodotMessageBufferMaxMiB = "Godot/MessageBufferMaxMiB";

    private static readonly string[] MonitorIds =
    {
        RuntimeAdvanceMsLast,
        RuntimeAdvanceMsMax,
        RuntimeAdvanceTickAtMax,
        RuntimeTicks,
        DecisionActors,
        FlowFieldBuilds,
        FlowFieldBuildMsLast,
        FlowFieldBuildMsMax,
        FlowFieldCacheHits,
        FlowFieldCacheMisses,
        CombatSlotScans,
        CombatSlotAnchors,
        CombatSlotScanMsLast,
        CombatSlotScanMsMax,
        TargetScoringMsLast,
        TargetScoringMsMax,
        MovementResolveMsLast,
        MovementResolveMsMax,
        MovementEvents,
        MovementEventsLastAdvance,
        ReservationRejectedCount,
        ReservationRejectedLastAdvance,
        HoldDueReservationCount,
        ActorsReadyNoMoveLastAdvance,
        MovementEventGapMsMax,
        MovementTweensCreated,
        MovementTweensInterrupted,
        ActiveMovementTweens,
        PresentationObserveMsLast,
        PresentationObserveMsMax,
        PresentationObserveCount,
        LogWrites,
        LogWriteMsLast,
        LogWriteMsMax,
        GodotFps,
        GodotFrameMs,
        GodotPhysicsMs,
        GodotNodes,
        GodotResources,
        GodotOrphanNodes,
        GodotDrawCalls,
        GodotObjectsInFrame,
        GodotPrimitives,
        GodotStaticMemoryMiB,
        GodotStaticMemoryMaxMiB,
        GodotMessageBufferMaxMiB
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
        Add(RuntimeAdvanceTickAtMax, () => _counters?.RuntimeAdvanceTickAtMax ?? -1);
        Add(RuntimeTicks, () => _counters?.RuntimeTickCount ?? 0);
        Add(DecisionActors, () => _counters?.DecisionReadyActorCount ?? 0);
        Add(FlowFieldBuilds, () => _counters?.FlowFieldBuildCount ?? 0);
        Add(FlowFieldBuildMsLast, () => ToMilliseconds(_counters?.LastFlowFieldBuildElapsedTicks ?? 0));
        Add(FlowFieldBuildMsMax, () => ToMilliseconds(_counters?.MaxFlowFieldBuildElapsedTicks ?? 0));
        Add(FlowFieldCacheHits, () => _counters?.FlowFieldCacheHitCount ?? 0);
        Add(FlowFieldCacheMisses, () => _counters?.FlowFieldCacheMissCount ?? 0);
        Add(CombatSlotScans, () => _counters?.CombatSlotScanCount ?? 0);
        Add(CombatSlotAnchors, () => _counters?.CombatSlotAnchorScanCount ?? 0);
        Add(CombatSlotScanMsLast, () => ToMilliseconds(_counters?.LastCombatSlotScanElapsedTicks ?? 0));
        Add(CombatSlotScanMsMax, () => ToMilliseconds(_counters?.MaxCombatSlotScanElapsedTicks ?? 0));
        Add(TargetScoringMsLast, () => ToMilliseconds(_counters?.LastTargetScoringElapsedTicks ?? 0));
        Add(TargetScoringMsMax, () => ToMilliseconds(_counters?.MaxTargetScoringElapsedTicks ?? 0));
        Add(MovementResolveMsLast, () => ToMilliseconds(_counters?.LastMovementResolveElapsedTicks ?? 0));
        Add(MovementResolveMsMax, () => ToMilliseconds(_counters?.MaxMovementResolveElapsedTicks ?? 0));
        Add(MovementEvents, () => _counters?.MovementEventCount ?? 0);
        Add(MovementEventsLastAdvance, () => _counters?.MovementEventsLastAdvance ?? 0);
        Add(ReservationRejectedCount, () => _counters?.ReservationRejectedCount ?? 0);
        Add(ReservationRejectedLastAdvance, () => _counters?.ReservationRejectedLastAdvance ?? 0);
        Add(HoldDueReservationCount, () => _counters?.HoldDueReservationCount ?? 0);
        Add(ActorsReadyNoMoveLastAdvance, () => _counters?.ActorsReadyNoMoveLastAdvance ?? 0);
        Add(MovementEventGapMsMax, () => (_counters?.MaxMovementEventGapMicroseconds ?? 0) / 1000.0);
        Add(MovementTweensCreated, () => _counters?.MovementTweenCreatedCount ?? 0);
        Add(MovementTweensInterrupted, () => _counters?.MovementTweenInterruptedCount ?? 0);
        Add(ActiveMovementTweens, () => _activeMovementTweens?.Invoke() ?? 0);
        Add(PresentationObserveMsLast, () => ToMilliseconds(_counters?.LastPresentationObserveElapsedTicks ?? 0));
        Add(PresentationObserveMsMax, () => ToMilliseconds(_counters?.MaxPresentationObserveElapsedTicks ?? 0));
        Add(PresentationObserveCount, () => _counters?.PresentationObserveCount ?? 0);
        Add(LogWrites, () => _counters?.LogWriteCount ?? 0);
        Add(LogWriteMsLast, () => ToMilliseconds(_counters?.LastLogWriteElapsedTicks ?? 0));
        Add(LogWriteMsMax, () => ToMilliseconds(_counters?.MaxLogWriteElapsedTicks ?? 0));
        Add(GodotFps, () => GetGodotMonitor(Performance.Monitor.TimeFps));
        Add(GodotFrameMs, () => GetGodotMonitor(Performance.Monitor.TimeProcess) * 1000.0);
        Add(GodotPhysicsMs, () => GetGodotMonitor(Performance.Monitor.TimePhysicsProcess) * 1000.0);
        Add(GodotNodes, () => GetGodotMonitor(Performance.Monitor.ObjectNodeCount));
        Add(GodotResources, () => GetGodotMonitor(Performance.Monitor.ObjectResourceCount));
        Add(GodotOrphanNodes, () => GetGodotMonitor(Performance.Monitor.ObjectOrphanNodeCount));
        Add(GodotDrawCalls, () => GetGodotMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame));
        Add(GodotObjectsInFrame, () => GetGodotMonitor(Performance.Monitor.RenderTotalObjectsInFrame));
        Add(GodotPrimitives, () => GetGodotMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame));
        Add(GodotStaticMemoryMiB, () => ToMebibytes(GetGodotMonitor(Performance.Monitor.MemoryStatic)));
        Add(GodotStaticMemoryMaxMiB, () => ToMebibytes(GetGodotMonitor(Performance.Monitor.MemoryStaticMax)));
        Add(GodotMessageBufferMaxMiB, () => ToMebibytes(GetGodotMonitor(Performance.Monitor.MemoryMessageBufferMax)));
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

    private static double GetGodotMonitor(Performance.Monitor monitor)
    {
        return Performance.GetMonitor(monitor);
    }

    private static double ToMebibytes(double bytes)
    {
        return bytes <= 0 ? 0 : bytes / (1024.0 * 1024.0);
    }
}
