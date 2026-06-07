using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Commands;
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
    private bool _invalidHandoff;

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

    public BattleRuntimeCommandSubmitResult SubmitCommand(CommandRequest request)
    {
        if (_invalidHandoff)
        {
            int startIndex = EventStream.Events.Count;
            EventStream.Add(new BattleEvent
            {
                EventId = $"{BattleId}:tick_{_nextTick}:{request?.CommandId ?? ""}:runtime_command_rejected",
                BattleId = BattleId,
                BattleGroupId = request?.BattleGroupId ?? "",
                SourceCommandId = request?.CommandId ?? "",
                Kind = BattleEventKind.CommandRejected,
                ReasonCode = "battle_snapshot_invalid",
                RuntimeTick = _nextTick,
                RuntimeTimeSeconds = CurrentTimeSeconds
            });
            return new BattleRuntimeCommandSubmitResult
            {
                Accepted = false,
                ReasonCode = "battle_snapshot_invalid",
                Events = EventStream.Events.Skip(startIndex).ToArray()
            };
        }

        if (IsComplete)
        {
            int startIndex = EventStream.Events.Count;
            EventStream.Add(new BattleEvent
            {
                EventId = $"{BattleId}:tick_{_nextTick}:{request?.CommandId ?? ""}:runtime_command_rejected",
                BattleId = BattleId,
                BattleGroupId = request?.BattleGroupId ?? "",
                SourceCommandId = request?.CommandId ?? "",
                Kind = BattleEventKind.CommandRejected,
                ReasonCode = "battle_already_complete",
                RuntimeTick = _nextTick,
                RuntimeTimeSeconds = CurrentTimeSeconds
            });
            return new BattleRuntimeCommandSubmitResult
            {
                Accepted = false,
                ReasonCode = "battle_already_complete",
                Events = EventStream.Events.Skip(startIndex).ToArray()
            };
        }

        return BattleRuntimeHeroSkillCommandResolver.Submit(
            State,
            EventStream,
            BattleId,
            _nextTick,
            CurrentTimeSeconds,
            request);
    }

    public BattleRuntimeAdvanceResult AdvanceFixedTick(double fixedDeltaSeconds = BattleActionTimingPolicy.DefaultSimulationTickSeconds)
    {
        double deltaSeconds = double.IsNaN(fixedDeltaSeconds) ||
                              double.IsInfinity(fixedDeltaSeconds) ||
                              fixedDeltaSeconds <= 0
            ? BattleActionTimingPolicy.DefaultSimulationTickSeconds
            : System.Math.Clamp(fixedDeltaSeconds, 0.001, BattleActionTimingPolicy.MaxActionSeconds);
        return AdvanceNextTickCore(deltaSeconds, advanceToNextReadyActorTime: false);
    }

    public BattleRuntimeAdvanceResult AdvanceNextTick()
    {
        return AdvanceNextTickCore(0, advanceToNextReadyActorTime: true);
    }

    private BattleRuntimeAdvanceResult AdvanceNextTickCore(double fixedDeltaSeconds, bool advanceToNextReadyActorTime)
    {
        int startIndex = EventStream.Events.Count;
        if (_invalidHandoff)
        {
            return BuildAdvanceResult(startIndex);
        }

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

        if (advanceToNextReadyActorTime)
        {
            AdvanceToNextReadyActorTime();
        }

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

        if (!advanceToNextReadyActorTime && !IsComplete)
        {
            // Live RTS presentation resolves the current fixed slice, then advances
            // runtime time for the next slice so tick-zero actions still occur at 0.00.
            CurrentTimeSeconds += fixedDeltaSeconds;
        }

        return BuildAdvanceResult(startIndex);
    }

    public BattleRuntimeSessionResult AdvanceToCompletion()
    {
        if (_invalidHandoff)
        {
            return BuildResult();
        }

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
        controller._invalidHandoff = true;
        controller.IsComplete = false;
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
