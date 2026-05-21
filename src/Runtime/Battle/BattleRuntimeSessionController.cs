using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Rpg.Infrastructure.Diagnostics;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Navigation;
using Rpg.Runtime.Battle.Results;

namespace Rpg.Runtime.Battle;

public sealed class BattleRuntimeAdvanceResult
{
    public IReadOnlyList<BattleEvent> Events { get; init; } = System.Array.Empty<BattleEvent>();
    public bool IsComplete { get; init; }
    public BattleOutcomeResult Outcome { get; init; } = new();
    public double RuntimeTimeSeconds { get; init; }
    public double NextAdvanceDelaySeconds { get; init; }
}

public sealed class BattleRuntimeSessionController
{
    private readonly BattleRuntimeTickResolver _tickResolver;
    private readonly BattleNavigationGraph _navigationGraph;
    private readonly HashSet<string> _navigationFailureDiagnostics = new(System.StringComparer.Ordinal);
    private readonly BattlePerformanceCounters _performanceCounters;
    private readonly int _maxTicks;
    private int _nextTick;

    internal BattleRuntimeSessionController(
        BattleRuntimeTickResolver tickResolver,
        BattleRuntimeState state,
        BattleEventStream eventStream,
        string battleId,
        string snapshotId,
        BattleNavigationGraph navigationGraph,
        int maxTicks,
        BattlePerformanceCounters performanceCounters = null)
    {
        _tickResolver = tickResolver;
        State = state ?? new BattleRuntimeState();
        EventStream = eventStream ?? new BattleEventStream();
        BattleId = battleId ?? "";
        SnapshotId = snapshotId ?? "";
        _navigationGraph = navigationGraph;
        _performanceCounters = performanceCounters;
        _maxTicks = System.Math.Max(1, maxTicks);
        Outcome = new BattleOutcomeResult
        {
            SnapshotId = SnapshotId,
            BattleId = BattleId,
            IsComplete = false,
            TerminationReason = BattleTerminationReason.None
        };
    }

    public string BattleId { get; }
    public string SnapshotId { get; }
    public BattleRuntimeState State { get; }
    public BattleEventStream EventStream { get; }
    public bool IsComplete { get; private set; }
    public BattleOutcomeResult Outcome { get; private set; }
    public double CurrentTimeSeconds { get; private set; }

    public BattleRuntimeAdvanceResult AdvanceNextTick()
    {
        int startIndex = EventStream.Events.Count;
        if (IsComplete)
        {
            return BuildAdvanceResult(startIndex);
        }

        BattleTerminationReason preTickTermination = BattleRuntimeSession.ResolveTermination(State);
        if (preTickTermination != BattleTerminationReason.None)
        {
            Complete(preTickTermination);
            return BuildAdvanceResult(startIndex);
        }

        if (_nextTick >= _maxTicks)
        {
            Complete(BattleTerminationReason.RuntimeException);
            return BuildAdvanceResult(startIndex);
        }

        AdvanceToNextReadyActorTime();

        // Presentation-backed battles call this one action-time slice at a time.
        // Runtime remains the only owner of damage, movement, target choice, and action readiness.
        long resolveStartedAt = Stopwatch.GetTimestamp();
        _tickResolver.ResolveTick(
            State,
            EventStream,
            BattleId,
            _nextTick,
            CurrentTimeSeconds,
            _navigationGraph,
            _navigationFailureDiagnostics,
            _performanceCounters);
        long resolveElapsedTicks = Stopwatch.GetTimestamp() - resolveStartedAt;
        bool isNewMaximum = _performanceCounters?.RecordRuntimeAdvanceElapsedTicks(resolveElapsedTicks, _nextTick) == true;
        BattleRuntimeSpikeDiagnostics.LogIfNeeded(
            BattleId,
            _nextTick,
            CurrentTimeSeconds,
            resolveElapsedTicks,
            isNewMaximum,
            _performanceCounters);
        _nextTick++;

        BattleTerminationReason postTickTermination = BattleRuntimeSession.ResolveTermination(State);
        if (postTickTermination != BattleTerminationReason.None)
        {
            Complete(postTickTermination);
            return BuildAdvanceResult(startIndex);
        }

        if (_nextTick >= _maxTicks)
        {
            Complete(BattleTerminationReason.RuntimeException);
        }

        return BuildAdvanceResult(startIndex);
    }

    public BattleRuntimeSessionResult AdvanceToCompletion()
    {
        while (!IsComplete)
        {
            AdvanceNextTick();
        }

        return BuildResult();
    }

    public BattleRuntimeSessionResult BuildResult()
    {
        return new BattleRuntimeSessionResult
        {
            EventStream = EventStream,
            FinalState = State,
            Outcome = Outcome
        };
    }

    internal static BattleRuntimeSessionController CompletedInvalid(
        string snapshotId,
        string battleId,
        BattleEventStream eventStream,
        BattleRuntimeState state,
        BattleTerminationReason terminationReason)
    {
        BattleRuntimeSessionController controller = new(
            new BattleRuntimeTickResolver(null),
            state ?? new BattleRuntimeState(),
            eventStream ?? new BattleEventStream(),
            battleId,
            snapshotId,
            null,
            BattleRuntimeSession.MaxAutonomousCombatTicks);
        controller.IsComplete = true;
        controller.Outcome = new BattleOutcomeResult
        {
            SnapshotId = snapshotId ?? "",
            BattleId = battleId ?? "",
            IsComplete = false,
            TerminationReason = terminationReason
        };
        return controller;
    }

    private void Complete(BattleTerminationReason terminationReason)
    {
        if (IsComplete)
        {
            return;
        }

        EventStream.Add(new BattleEvent
        {
            EventId = $"{BattleId}:ended",
            BattleId = BattleId,
            Kind = BattleEventKind.BattleEnded,
            ReasonCode = terminationReason.ToString(),
            RuntimeTick = _nextTick,
            RuntimeTimeSeconds = CurrentTimeSeconds
        });
        Outcome = BattleRuntimeSession.BuildCompletedOutcome(SnapshotId, BattleId, State, terminationReason);
        IsComplete = true;
    }

    private BattleRuntimeAdvanceResult BuildAdvanceResult(int startIndex)
    {
        return new BattleRuntimeAdvanceResult
        {
            Events = EventStream.Events.Skip(System.Math.Max(0, startIndex)).ToArray(),
            IsComplete = IsComplete,
            Outcome = Outcome,
            RuntimeTimeSeconds = CurrentTimeSeconds,
            NextAdvanceDelaySeconds = ResolveNextAdvanceDelaySeconds()
        };
    }

    private void AdvanceToNextReadyActorTime()
    {
        double? nextReady = ResolveNextReadyActorTime();
        if (nextReady.HasValue && nextReady.Value > CurrentTimeSeconds)
        {
            CurrentTimeSeconds = nextReady.Value;
        }
    }

    private double ResolveNextAdvanceDelaySeconds()
    {
        if (IsComplete)
        {
            return 0;
        }

        double? nextReady = ResolveNextReadyActorTime();
        if (!nextReady.HasValue)
        {
            return 0;
        }

        return System.Math.Max(0, nextReady.Value - CurrentTimeSeconds);
    }

    private double? ResolveNextReadyActorTime()
    {
        BattleRuntimeActor[] livingCorps = State?.Actors?
            .Where(item => item.Kind == BattleRuntimeActorKind.Corps && item.HitPoints > 0)
            .ToArray() ?? System.Array.Empty<BattleRuntimeActor>();
        if (livingCorps.Length == 0)
        {
            return null;
        }

        return livingCorps
            .Select(actor => System.Math.Max(CurrentTimeSeconds, actor.ActionReadyAtSeconds))
            .DefaultIfEmpty(CurrentTimeSeconds)
            .Min();
    }
}
