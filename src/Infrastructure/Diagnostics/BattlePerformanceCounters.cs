using System.Threading;

namespace Rpg.Infrastructure.Diagnostics;

public sealed class BattlePerformanceCounters
{
    private long _runtimeTickCount;
    private long _decisionReadyActorCount;
    private long _decisionReadyActorsLastAdvance;
    private long _runtimeAdvanceElapsedTicks;
    private long _lastRuntimeAdvanceElapsedTicks;
    private long _maxRuntimeAdvanceElapsedTicks;
    private long _runtimeAdvanceTickAtMax = -1;
    private long _flowFieldCacheHitCount;
    private long _flowFieldCacheMissCount;
    private long _flowFieldBuildCount;
    private long _openAttackFlowFieldRequestCount;
    private long _openAttackFlowFieldCacheHitCount;
    private long _openAttackFlowFieldBuildCount;
    private long _flowFieldBuildElapsedTicks;
    private long _flowFieldBuildElapsedTicksLastAdvance;
    private long _lastFlowFieldBuildElapsedTicks;
    private long _maxFlowFieldBuildElapsedTicks;
    private long _combatSlotScanCount;
    private long _combatSlotAnchorScanCount;
    private long _combatSlotScanElapsedTicks;
    private long _combatSlotScanElapsedTicksLastAdvance;
    private long _lastCombatSlotScanElapsedTicks;
    private long _maxCombatSlotScanElapsedTicks;
    private long _targetScoringElapsedTicks;
    private long _targetScoringElapsedTicksLastAdvance;
    private long _lastTargetScoringElapsedTicks;
    private long _maxTargetScoringElapsedTicks;
    private long _movementResolveElapsedTicks;
    private long _movementResolveElapsedTicksLastAdvance;
    private long _lastMovementResolveElapsedTicks;
    private long _maxMovementResolveElapsedTicks;
    private long _movementEventCount;
    private long _movementEventsLastAdvance;
    private long _lastMovementEventRuntimeMicroseconds = -1;
    private long _maxMovementEventGapMicroseconds;
    private long _reservationRejectedCount;
    private long _reservationRejectedLastAdvance;
    private long _holdDueReservationCount;
    private long _actorsReadyNoMoveLastAdvance;
    private long _movementTweenCreatedCount;
    private long _movementTweenInterruptedCount;
    private long _presentationObserveCount;
    private long _presentationObserveElapsedTicks;
    private long _lastPresentationObserveElapsedTicks;
    private long _maxPresentationObserveElapsedTicks;
    private long _logWriteCount;
    private long _logSuppressedCount;
    private long _logWriteElapsedTicks;
    private long _lastLogWriteElapsedTicks;
    private long _maxLogWriteElapsedTicks;

    public bool Enabled { get; set; } = true;

    public long RuntimeTickCount => Interlocked.Read(ref _runtimeTickCount);
    public long DecisionReadyActorCount => Interlocked.Read(ref _decisionReadyActorCount);
    public long DecisionReadyActorsLastAdvance => Interlocked.Read(ref _decisionReadyActorsLastAdvance);
    public long RuntimeAdvanceElapsedTicks => Interlocked.Read(ref _runtimeAdvanceElapsedTicks);
    public long LastRuntimeAdvanceElapsedTicks => Interlocked.Read(ref _lastRuntimeAdvanceElapsedTicks);
    public long MaxRuntimeAdvanceElapsedTicks => Interlocked.Read(ref _maxRuntimeAdvanceElapsedTicks);
    public long RuntimeAdvanceTickAtMax => Interlocked.Read(ref _runtimeAdvanceTickAtMax);
    public long FlowFieldCacheHitCount => Interlocked.Read(ref _flowFieldCacheHitCount);
    public long FlowFieldCacheMissCount => Interlocked.Read(ref _flowFieldCacheMissCount);
    public long FlowFieldBuildCount => Interlocked.Read(ref _flowFieldBuildCount);
    public long OpenAttackFlowFieldRequestCount => Interlocked.Read(ref _openAttackFlowFieldRequestCount);
    public long OpenAttackFlowFieldCacheHitCount => Interlocked.Read(ref _openAttackFlowFieldCacheHitCount);
    public long OpenAttackFlowFieldBuildCount => Interlocked.Read(ref _openAttackFlowFieldBuildCount);
    public long FlowFieldBuildElapsedTicks => Interlocked.Read(ref _flowFieldBuildElapsedTicks);
    public long FlowFieldBuildElapsedTicksLastAdvance => Interlocked.Read(ref _flowFieldBuildElapsedTicksLastAdvance);
    public long LastFlowFieldBuildElapsedTicks => Interlocked.Read(ref _lastFlowFieldBuildElapsedTicks);
    public long MaxFlowFieldBuildElapsedTicks => Interlocked.Read(ref _maxFlowFieldBuildElapsedTicks);
    public long CombatSlotScanCount => Interlocked.Read(ref _combatSlotScanCount);
    public long CombatSlotAnchorScanCount => Interlocked.Read(ref _combatSlotAnchorScanCount);
    public long CombatSlotScanElapsedTicks => Interlocked.Read(ref _combatSlotScanElapsedTicks);
    public long CombatSlotScanElapsedTicksLastAdvance => Interlocked.Read(ref _combatSlotScanElapsedTicksLastAdvance);
    public long LastCombatSlotScanElapsedTicks => Interlocked.Read(ref _lastCombatSlotScanElapsedTicks);
    public long MaxCombatSlotScanElapsedTicks => Interlocked.Read(ref _maxCombatSlotScanElapsedTicks);
    public long TargetScoringElapsedTicks => Interlocked.Read(ref _targetScoringElapsedTicks);
    public long TargetScoringElapsedTicksLastAdvance => Interlocked.Read(ref _targetScoringElapsedTicksLastAdvance);
    public long LastTargetScoringElapsedTicks => Interlocked.Read(ref _lastTargetScoringElapsedTicks);
    public long MaxTargetScoringElapsedTicks => Interlocked.Read(ref _maxTargetScoringElapsedTicks);
    public long MovementResolveElapsedTicks => Interlocked.Read(ref _movementResolveElapsedTicks);
    public long MovementResolveElapsedTicksLastAdvance => Interlocked.Read(ref _movementResolveElapsedTicksLastAdvance);
    public long LastMovementResolveElapsedTicks => Interlocked.Read(ref _lastMovementResolveElapsedTicks);
    public long MaxMovementResolveElapsedTicks => Interlocked.Read(ref _maxMovementResolveElapsedTicks);
    public long MovementEventCount => Interlocked.Read(ref _movementEventCount);
    public long MovementEventsLastAdvance => Interlocked.Read(ref _movementEventsLastAdvance);
    public long MaxMovementEventGapMicroseconds => Interlocked.Read(ref _maxMovementEventGapMicroseconds);
    public long ReservationRejectedCount => Interlocked.Read(ref _reservationRejectedCount);
    public long ReservationRejectedLastAdvance => Interlocked.Read(ref _reservationRejectedLastAdvance);
    public long HoldDueReservationCount => Interlocked.Read(ref _holdDueReservationCount);
    public long ActorsReadyNoMoveLastAdvance => Interlocked.Read(ref _actorsReadyNoMoveLastAdvance);
    public long MovementTweenCreatedCount => Interlocked.Read(ref _movementTweenCreatedCount);
    public long MovementTweenInterruptedCount => Interlocked.Read(ref _movementTweenInterruptedCount);
    public long PresentationObserveCount => Interlocked.Read(ref _presentationObserveCount);
    public long PresentationObserveElapsedTicks => Interlocked.Read(ref _presentationObserveElapsedTicks);
    public long LastPresentationObserveElapsedTicks => Interlocked.Read(ref _lastPresentationObserveElapsedTicks);
    public long MaxPresentationObserveElapsedTicks => Interlocked.Read(ref _maxPresentationObserveElapsedTicks);
    public long LogWriteCount => Interlocked.Read(ref _logWriteCount);
    public long LogSuppressedCount => Interlocked.Read(ref _logSuppressedCount);
    public long LogWriteElapsedTicks => Interlocked.Read(ref _logWriteElapsedTicks);
    public long LastLogWriteElapsedTicks => Interlocked.Read(ref _lastLogWriteElapsedTicks);
    public long MaxLogWriteElapsedTicks => Interlocked.Read(ref _maxLogWriteElapsedTicks);

    public void RecordRuntimeTick()
    {
        Increment(ref _runtimeTickCount);
    }

    public void RecordDecisionReadyActors(int count)
    {
        Add(ref _decisionReadyActorCount, count);
        if (Enabled)
        {
            Interlocked.Exchange(ref _decisionReadyActorsLastAdvance, System.Math.Max(0, count));
        }
    }

    public void BeginRuntimeAdvance()
    {
        if (!Enabled)
        {
            return;
        }

        Interlocked.Exchange(ref _movementEventsLastAdvance, 0);
        Interlocked.Exchange(ref _reservationRejectedLastAdvance, 0);
        Interlocked.Exchange(ref _actorsReadyNoMoveLastAdvance, 0);
        Interlocked.Exchange(ref _flowFieldBuildElapsedTicksLastAdvance, 0);
        Interlocked.Exchange(ref _combatSlotScanElapsedTicksLastAdvance, 0);
        Interlocked.Exchange(ref _targetScoringElapsedTicksLastAdvance, 0);
        Interlocked.Exchange(ref _movementResolveElapsedTicksLastAdvance, 0);
    }

    public bool RecordRuntimeAdvanceElapsedTicks(long elapsedTicks, int runtimeTick = -1)
    {
        Add(ref _runtimeAdvanceElapsedTicks, elapsedTicks);
        return SetLastAndMax(ref _lastRuntimeAdvanceElapsedTicks, ref _maxRuntimeAdvanceElapsedTicks, elapsedTicks, runtimeTick);
    }

    public void RecordFlowFieldCacheHit()
    {
        Increment(ref _flowFieldCacheHitCount);
    }

    public void RecordFlowFieldCacheMiss()
    {
        Increment(ref _flowFieldCacheMissCount);
    }

    public void RecordFlowFieldBuild()
    {
        Increment(ref _flowFieldBuildCount);
    }

    public void RecordOpenAttackFlowFieldRequest()
    {
        Increment(ref _openAttackFlowFieldRequestCount);
    }

    public void RecordOpenAttackFlowFieldCacheHit()
    {
        Increment(ref _openAttackFlowFieldCacheHitCount);
    }

    public void RecordOpenAttackFlowFieldBuild()
    {
        Increment(ref _openAttackFlowFieldBuildCount);
    }

    public void RecordFlowFieldBuildElapsedTicks(long elapsedTicks)
    {
        Add(ref _flowFieldBuildElapsedTicks, elapsedTicks);
        Add(ref _flowFieldBuildElapsedTicksLastAdvance, elapsedTicks);
        SetLastAndMax(ref _lastFlowFieldBuildElapsedTicks, ref _maxFlowFieldBuildElapsedTicks, elapsedTicks);
    }

    public void RecordCombatSlotScan(int anchorCount, long elapsedTicks = 0)
    {
        Increment(ref _combatSlotScanCount);
        Add(ref _combatSlotAnchorScanCount, anchorCount);
        if (elapsedTicks > 0)
        {
            Add(ref _combatSlotScanElapsedTicks, elapsedTicks);
            Add(ref _combatSlotScanElapsedTicksLastAdvance, elapsedTicks);
            SetLastAndMax(ref _lastCombatSlotScanElapsedTicks, ref _maxCombatSlotScanElapsedTicks, elapsedTicks);
        }
    }

    public void RecordTargetScoringElapsedTicks(long elapsedTicks)
    {
        Add(ref _targetScoringElapsedTicks, elapsedTicks);
        Add(ref _targetScoringElapsedTicksLastAdvance, elapsedTicks);
        SetLastAndMax(ref _lastTargetScoringElapsedTicks, ref _maxTargetScoringElapsedTicks, elapsedTicks);
    }

    public void RecordMovementResolveElapsedTicks(long elapsedTicks)
    {
        Add(ref _movementResolveElapsedTicks, elapsedTicks);
        Add(ref _movementResolveElapsedTicksLastAdvance, elapsedTicks);
        SetLastAndMax(ref _lastMovementResolveElapsedTicks, ref _maxMovementResolveElapsedTicks, elapsedTicks);
    }

    public void RecordMovementEvent(double runtimeTimeSeconds)
    {
        Increment(ref _movementEventCount);
        Increment(ref _movementEventsLastAdvance);
        if (!Enabled || runtimeTimeSeconds < 0)
        {
            return;
        }

        long current = (long)System.Math.Round(runtimeTimeSeconds * 1_000_000.0);
        long previous = Interlocked.Exchange(ref _lastMovementEventRuntimeMicroseconds, current);
        if (previous >= 0 && current > previous)
        {
            SetMax(ref _maxMovementEventGapMicroseconds, current - previous);
        }
    }

    public void RecordReservationRejected()
    {
        Increment(ref _reservationRejectedCount);
        Increment(ref _reservationRejectedLastAdvance);
    }

    public void RecordHoldDueReservation()
    {
        Increment(ref _holdDueReservationCount);
    }

    public void RecordActorsReadyNoMoveLastAdvance(int count)
    {
        if (Enabled)
        {
            Interlocked.Exchange(ref _actorsReadyNoMoveLastAdvance, System.Math.Max(0, count));
        }
    }

    public void RecordMovementTweenCreated()
    {
        Increment(ref _movementTweenCreatedCount);
    }

    public void RecordMovementTweenInterrupted()
    {
        Increment(ref _movementTweenInterruptedCount);
    }

    public void RecordPresentationObserveElapsedTicks(long elapsedTicks)
    {
        Increment(ref _presentationObserveCount);
        Add(ref _presentationObserveElapsedTicks, elapsedTicks);
        SetLastAndMax(ref _lastPresentationObserveElapsedTicks, ref _maxPresentationObserveElapsedTicks, elapsedTicks);
    }

    public void RecordLogWrite(long elapsedTicks)
    {
        Increment(ref _logWriteCount);
        Add(ref _logWriteElapsedTicks, elapsedTicks);
        SetLastAndMax(ref _lastLogWriteElapsedTicks, ref _maxLogWriteElapsedTicks, elapsedTicks);
    }

    public void RecordLogSuppressed()
    {
        Increment(ref _logSuppressedCount);
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _runtimeTickCount, 0);
        Interlocked.Exchange(ref _decisionReadyActorCount, 0);
        Interlocked.Exchange(ref _decisionReadyActorsLastAdvance, 0);
        Interlocked.Exchange(ref _runtimeAdvanceElapsedTicks, 0);
        Interlocked.Exchange(ref _lastRuntimeAdvanceElapsedTicks, 0);
        Interlocked.Exchange(ref _maxRuntimeAdvanceElapsedTicks, 0);
        Interlocked.Exchange(ref _runtimeAdvanceTickAtMax, -1);
        Interlocked.Exchange(ref _flowFieldCacheHitCount, 0);
        Interlocked.Exchange(ref _flowFieldCacheMissCount, 0);
        Interlocked.Exchange(ref _flowFieldBuildCount, 0);
        Interlocked.Exchange(ref _openAttackFlowFieldRequestCount, 0);
        Interlocked.Exchange(ref _openAttackFlowFieldCacheHitCount, 0);
        Interlocked.Exchange(ref _openAttackFlowFieldBuildCount, 0);
        Interlocked.Exchange(ref _flowFieldBuildElapsedTicks, 0);
        Interlocked.Exchange(ref _flowFieldBuildElapsedTicksLastAdvance, 0);
        Interlocked.Exchange(ref _lastFlowFieldBuildElapsedTicks, 0);
        Interlocked.Exchange(ref _maxFlowFieldBuildElapsedTicks, 0);
        Interlocked.Exchange(ref _combatSlotScanCount, 0);
        Interlocked.Exchange(ref _combatSlotAnchorScanCount, 0);
        Interlocked.Exchange(ref _combatSlotScanElapsedTicks, 0);
        Interlocked.Exchange(ref _combatSlotScanElapsedTicksLastAdvance, 0);
        Interlocked.Exchange(ref _lastCombatSlotScanElapsedTicks, 0);
        Interlocked.Exchange(ref _maxCombatSlotScanElapsedTicks, 0);
        Interlocked.Exchange(ref _targetScoringElapsedTicks, 0);
        Interlocked.Exchange(ref _targetScoringElapsedTicksLastAdvance, 0);
        Interlocked.Exchange(ref _lastTargetScoringElapsedTicks, 0);
        Interlocked.Exchange(ref _maxTargetScoringElapsedTicks, 0);
        Interlocked.Exchange(ref _movementResolveElapsedTicks, 0);
        Interlocked.Exchange(ref _movementResolveElapsedTicksLastAdvance, 0);
        Interlocked.Exchange(ref _lastMovementResolveElapsedTicks, 0);
        Interlocked.Exchange(ref _maxMovementResolveElapsedTicks, 0);
        Interlocked.Exchange(ref _movementEventCount, 0);
        Interlocked.Exchange(ref _movementEventsLastAdvance, 0);
        Interlocked.Exchange(ref _lastMovementEventRuntimeMicroseconds, -1);
        Interlocked.Exchange(ref _maxMovementEventGapMicroseconds, 0);
        Interlocked.Exchange(ref _reservationRejectedCount, 0);
        Interlocked.Exchange(ref _reservationRejectedLastAdvance, 0);
        Interlocked.Exchange(ref _holdDueReservationCount, 0);
        Interlocked.Exchange(ref _actorsReadyNoMoveLastAdvance, 0);
        Interlocked.Exchange(ref _movementTweenCreatedCount, 0);
        Interlocked.Exchange(ref _movementTweenInterruptedCount, 0);
        Interlocked.Exchange(ref _presentationObserveCount, 0);
        Interlocked.Exchange(ref _presentationObserveElapsedTicks, 0);
        Interlocked.Exchange(ref _lastPresentationObserveElapsedTicks, 0);
        Interlocked.Exchange(ref _maxPresentationObserveElapsedTicks, 0);
        Interlocked.Exchange(ref _logWriteCount, 0);
        Interlocked.Exchange(ref _logSuppressedCount, 0);
        Interlocked.Exchange(ref _logWriteElapsedTicks, 0);
        Interlocked.Exchange(ref _lastLogWriteElapsedTicks, 0);
        Interlocked.Exchange(ref _maxLogWriteElapsedTicks, 0);
    }

    private void Increment(ref long field)
    {
        if (Enabled)
        {
            Interlocked.Increment(ref field);
        }
    }

    private void Add(ref long field, long value)
    {
        if (Enabled && value > 0)
        {
            Interlocked.Add(ref field, value);
        }
    }

    private void SetLastAndMax(ref long lastField, ref long maxField, long value)
    {
        if (!Enabled || value <= 0)
        {
            return;
        }

        Interlocked.Exchange(ref lastField, value);
        SetMax(ref maxField, value);
    }

    private bool SetLastAndMax(ref long lastField, ref long maxField, long value, int runtimeTick)
    {
        if (!Enabled || value <= 0)
        {
            return false;
        }

        Interlocked.Exchange(ref lastField, value);
        while (true)
        {
            long known = Interlocked.Read(ref maxField);
            if (value <= known ||
                Interlocked.CompareExchange(ref maxField, value, known) == known)
            {
                if (value > known && runtimeTick >= 0)
                {
                    Interlocked.Exchange(ref _runtimeAdvanceTickAtMax, runtimeTick);
                }

                return value > known;
            }
        }
    }

    private void SetMax(ref long maxField, long value)
    {
        if (!Enabled || value <= 0)
        {
            return;
        }

        while (true)
        {
            long known = Interlocked.Read(ref maxField);
            if (value <= known ||
                Interlocked.CompareExchange(ref maxField, value, known) == known)
            {
                return;
            }
        }
    }
}
