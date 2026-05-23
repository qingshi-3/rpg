using System;
using System.Diagnostics;
using System.Globalization;
using Rpg.Infrastructure.Diagnostics;
using Rpg.Infrastructure.Logging;

namespace Rpg.Runtime.Battle;

internal static class BattleRuntimeSpikeDiagnostics
{
    private const string ThresholdEnvironmentVariable = "RPG_BATTLE_RUNTIME_SPIKE_DIAGNOSTIC_MS";
    private const double DefaultThresholdMilliseconds = 50.0;

    public static void LogIfNeeded(
        string battleId,
        int tick,
        double runtimeTimeSeconds,
        long elapsedTicks,
        bool isNewMaximum,
        BattlePerformanceCounters counters)
    {
        if (counters == null || elapsedTicks <= 0)
        {
            return;
        }

        double totalMilliseconds = ToMilliseconds(elapsedTicks);
        double thresholdMilliseconds = ResolveThresholdMilliseconds();
        if (totalMilliseconds < thresholdMilliseconds ||
            thresholdMilliseconds > 0 && !isNewMaximum)
        {
            return;
        }

        // The spike line is intentionally self-contained so manual Godot runs do
        // not require reading several monitor values at the same instant.
        GameLog.Warn(
            nameof(BattleRuntimeSpikeDiagnostics),
            FormattableString.Invariant(
                $"BattleRuntimeSpike battle={battleId ?? ""} tick={tick} time={runtimeTimeSeconds:0.000} totalMs={totalMilliseconds:0.###} targetScoringMs={ToMilliseconds(counters.TargetScoringElapsedTicksLastAdvance):0.###} flowFieldBuildMs={ToMilliseconds(counters.FlowFieldBuildElapsedTicksLastAdvance):0.###} flowFieldBuilds={counters.FlowFieldBuildCount} flowFieldHits={counters.FlowFieldCacheHitCount} flowFieldMisses={counters.FlowFieldCacheMissCount} openAttackFlowFieldRequests={counters.OpenAttackFlowFieldRequestCount} openAttackFlowFieldCacheHits={counters.OpenAttackFlowFieldCacheHitCount} openAttackFlowFieldBuilds={counters.OpenAttackFlowFieldBuildCount} combatSlotScanMs={ToMilliseconds(counters.CombatSlotScanElapsedTicksLastAdvance):0.###} movementResolveMs={ToMilliseconds(counters.MovementResolveElapsedTicksLastAdvance):0.###} movementEvents={counters.MovementEventsLastAdvance} reservationRejected={counters.ReservationRejectedLastAdvance} holdDueReservation={counters.HoldDueReservationCount} decisionActors={counters.DecisionReadyActorsLastAdvance} actorsReadyNoMove={counters.ActorsReadyNoMoveLastAdvance} movementEventGapMsMax={counters.MaxMovementEventGapMicroseconds / 1000.0:0.###} movementTweensCreated={counters.MovementTweenCreatedCount} movementTweensInterrupted={counters.MovementTweenInterruptedCount} presentationObserveMsMax={ToMilliseconds(counters.MaxPresentationObserveElapsedTicks):0.###} logWrites={counters.LogWriteCount} logWriteMsMax={ToMilliseconds(counters.MaxLogWriteElapsedTicks):0.###}"));
    }

    private static double ResolveThresholdMilliseconds()
    {
        string value = Environment.GetEnvironmentVariable(ThresholdEnvironmentVariable);
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            ? Math.Max(0, parsed)
            : DefaultThresholdMilliseconds;
    }

    private static double ToMilliseconds(long elapsedTicks)
    {
        return elapsedTicks <= 0
            ? 0
            : elapsedTicks * 1000.0 / Stopwatch.Frequency;
    }
}
