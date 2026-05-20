using System.Threading;

namespace Rpg.Infrastructure.Diagnostics;

public sealed class BattlePerformanceCounters
{
    private long _runtimeTickCount;
    private long _decisionReadyActorCount;
    private long _runtimeAdvanceElapsedTicks;
    private long _lastRuntimeAdvanceElapsedTicks;
    private long _maxRuntimeAdvanceElapsedTicks;
    private long _flowFieldCacheHitCount;
    private long _flowFieldCacheMissCount;
    private long _flowFieldBuildCount;
    private long _combatSlotScanCount;
    private long _combatSlotAnchorScanCount;
    private long _movementEventCount;
    private long _movementTweenCreatedCount;
    private long _movementTweenInterruptedCount;
    private long _logWriteCount;
    private long _logSuppressedCount;
    private long _logWriteElapsedTicks;
    private long _lastLogWriteElapsedTicks;
    private long _maxLogWriteElapsedTicks;

    public bool Enabled { get; set; } = true;

    public long RuntimeTickCount => Interlocked.Read(ref _runtimeTickCount);
    public long DecisionReadyActorCount => Interlocked.Read(ref _decisionReadyActorCount);
    public long RuntimeAdvanceElapsedTicks => Interlocked.Read(ref _runtimeAdvanceElapsedTicks);
    public long LastRuntimeAdvanceElapsedTicks => Interlocked.Read(ref _lastRuntimeAdvanceElapsedTicks);
    public long MaxRuntimeAdvanceElapsedTicks => Interlocked.Read(ref _maxRuntimeAdvanceElapsedTicks);
    public long FlowFieldCacheHitCount => Interlocked.Read(ref _flowFieldCacheHitCount);
    public long FlowFieldCacheMissCount => Interlocked.Read(ref _flowFieldCacheMissCount);
    public long FlowFieldBuildCount => Interlocked.Read(ref _flowFieldBuildCount);
    public long CombatSlotScanCount => Interlocked.Read(ref _combatSlotScanCount);
    public long CombatSlotAnchorScanCount => Interlocked.Read(ref _combatSlotAnchorScanCount);
    public long MovementEventCount => Interlocked.Read(ref _movementEventCount);
    public long MovementTweenCreatedCount => Interlocked.Read(ref _movementTweenCreatedCount);
    public long MovementTweenInterruptedCount => Interlocked.Read(ref _movementTweenInterruptedCount);
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
    }

    public void RecordRuntimeAdvanceElapsedTicks(long elapsedTicks)
    {
        Add(ref _runtimeAdvanceElapsedTicks, elapsedTicks);
        SetLastAndMax(ref _lastRuntimeAdvanceElapsedTicks, ref _maxRuntimeAdvanceElapsedTicks, elapsedTicks);
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

    public void RecordCombatSlotScan(int anchorCount)
    {
        Increment(ref _combatSlotScanCount);
        Add(ref _combatSlotAnchorScanCount, anchorCount);
    }

    public void RecordMovementEvent()
    {
        Increment(ref _movementEventCount);
    }

    public void RecordMovementTweenCreated()
    {
        Increment(ref _movementTweenCreatedCount);
    }

    public void RecordMovementTweenInterrupted()
    {
        Increment(ref _movementTweenInterruptedCount);
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
        Interlocked.Exchange(ref _runtimeAdvanceElapsedTicks, 0);
        Interlocked.Exchange(ref _lastRuntimeAdvanceElapsedTicks, 0);
        Interlocked.Exchange(ref _maxRuntimeAdvanceElapsedTicks, 0);
        Interlocked.Exchange(ref _flowFieldCacheHitCount, 0);
        Interlocked.Exchange(ref _flowFieldCacheMissCount, 0);
        Interlocked.Exchange(ref _flowFieldBuildCount, 0);
        Interlocked.Exchange(ref _combatSlotScanCount, 0);
        Interlocked.Exchange(ref _combatSlotAnchorScanCount, 0);
        Interlocked.Exchange(ref _movementEventCount, 0);
        Interlocked.Exchange(ref _movementTweenCreatedCount, 0);
        Interlocked.Exchange(ref _movementTweenInterruptedCount, 0);
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
