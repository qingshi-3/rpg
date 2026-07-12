using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Commands;
using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Diagnostics;
using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle.AI;
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
    private readonly BattleRuntimeClock _clock;
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
        _clock = new BattleRuntimeClock();
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
    public double CurrentTimeSeconds => _clock.CurrentTimeSeconds;
    public bool IsPaused => _clock.IsPaused;

    public void SetPaused(bool paused, string reason)
    {
        _clock.SetPaused(paused, reason);
        GameLog.Info(
            nameof(BattleRuntimeSessionController),
            $"BattleRuntimeClockPause battle={BattleId} paused={paused} time={CurrentTimeSeconds:0.###} reason={reason ?? ""}");
    }

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

        string authorizationFailure = ValidatePlayerCommandAuthorization(request);
        if (!string.IsNullOrWhiteSpace(authorizationFailure))
        {
            return RejectPlayerCommand(request, authorizationFailure);
        }

        if (request?.Kind == CommandKind.DestinationBeacon)
        {
            return BattleRuntimeDestinationBeaconCommandResolver.Submit(
                State,
                EventStream,
                BattleId,
                _nextTick,
                CurrentTimeSeconds,
                request,
                _navigationGraph);
        }

        if (request?.Kind is CommandKind.Regroup or CommandKind.Retreat)
        {
            return BattleRuntimeTacticalCommandResolver.Submit(
                State,
                EventStream,
                BattleId,
                _nextTick,
                CurrentTimeSeconds,
                request,
                _navigationGraph);
        }

        return BattleRuntimeHeroSkillCommandResolver.Submit(
            State,
            EventStream,
            BattleId,
            _nextTick,
            CurrentTimeSeconds,
            request,
            _navigationGraph);
    }

    private string ValidatePlayerCommandAuthorization(CommandRequest request)
    {
        if (request == null)
        {
            return "command_missing";
        }

        if (request.Kind is not (CommandKind.DestinationBeacon or CommandKind.CastSkill or CommandKind.Regroup or CommandKind.Retreat))
        {
            return "";
        }

        if (string.IsNullOrWhiteSpace(request.BattleId) ||
            !string.Equals(request.BattleId.Trim(), BattleId, System.StringComparison.Ordinal))
        {
            return "battle_id_mismatch";
        }

        if (request.Kind is CommandKind.DestinationBeacon or CommandKind.Regroup or CommandKind.Retreat)
        {
            if (request.Channel != CommandChannel.Combined)
            {
                return "command_channel_unavailable";
            }

            foreach (string groupId in ResolveRequestedBattleGroupIds(request))
            {
                BattleRuntimeActor[] corpsMembers = State?.Actors?
                    .Where(actor =>
                        actor.Kind == BattleRuntimeActorKind.Corps &&
                        actor.HitPoints > 0 &&
                        !actor.HasRetreated &&
                        string.Equals(actor.BattleGroupId ?? "", groupId, System.StringComparison.Ordinal))
                    .ToArray() ?? System.Array.Empty<BattleRuntimeActor>();
                if (corpsMembers.Length == 0)
                {
                    return "battle_group_unavailable";
                }

                if (corpsMembers.Any(actor => !BattleRuntimeIdentityRules.IsPlayerFaction(actor.FactionId)))
                {
                    return "battle_group_not_player_controlled";
                }
            }

            return ResolveRequestedBattleGroupIds(request).Length == 0
                ? "battle_group_unavailable"
                : "";
        }

        string battleGroupId = request.BattleGroupId?.Trim() ?? "";
        BattleRuntimeActor[] groupActors = State?.Actors?
            .Where(actor =>
                actor.HitPoints > 0 &&
                string.Equals(actor.BattleGroupId ?? "", battleGroupId, System.StringComparison.Ordinal))
            .ToArray() ?? System.Array.Empty<BattleRuntimeActor>();
        if (string.IsNullOrWhiteSpace(battleGroupId) || groupActors.Length == 0)
        {
            return "battle_group_unavailable";
        }

        if (groupActors.Any(actor => !BattleRuntimeIdentityRules.IsPlayerFaction(actor.FactionId)))
        {
            return "battle_group_not_player_controlled";
        }

        BattleRuntimeActor source = string.IsNullOrWhiteSpace(request.SourceActorId)
            ? groupActors
                .Where(actor => actor.Kind == BattleRuntimeActorKind.Hero)
                .OrderBy(actor => actor.ActorId, System.StringComparer.Ordinal)
                .FirstOrDefault()
            : groupActors.FirstOrDefault(actor =>
                string.Equals(actor.ActorId ?? "", request.SourceActorId.Trim(), System.StringComparison.Ordinal));
        if (source == null)
        {
            return "skill_caster_invalid";
        }

        string skillDefinitionId = request.SkillDefinitionId?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(skillDefinitionId))
        {
            return "skill_definition_id_required";
        }

        BattleSkillSnapshot[] definitions = (State?.SkillDefinitions ?? new List<BattleSkillSnapshot>())
            .Where(skill => string.Equals(
                skill?.SkillDefinitionId?.Trim() ?? "",
                skillDefinitionId,
                System.StringComparison.Ordinal))
            .ToArray();
        if (definitions.Length == 0)
        {
            return "skill_definition_missing";
        }

        BattleSkillSnapshot skill = definitions.FirstOrDefault(candidate => SkillMatchesRuntimeSource(candidate, source));
        if (skill == null)
        {
            return "skill_caster_not_allowed";
        }

        if (!ChannelMatches(request.Channel, skill.CommandChannel))
        {
            return "skill_command_channel_mismatch";
        }

        return SourceKindMatches(source.Kind, skill.CommandChannel)
            ? ""
            : "skill_caster_invalid";
    }

    private BattleRuntimeCommandSubmitResult RejectPlayerCommand(CommandRequest request, string reasonCode)
    {
        int startIndex = EventStream.Events.Count;
        EventStream.Add(new BattleEvent
        {
            EventId = $"{BattleId}:tick_{_nextTick}:{request?.CommandId ?? ""}:runtime_command_rejected",
            BattleId = BattleId,
            BattleGroupId = request?.BattleGroupId ?? "",
            ActorId = request?.SourceActorId ?? "",
            TargetId = request?.TargetActorId ?? "",
            SourceCommandId = request?.CommandId ?? "",
            SourceDefinitionId = request?.SkillDefinitionId ?? "",
            Kind = BattleEventKind.CommandRejected,
            ReasonCode = reasonCode ?? "",
            RuntimeTick = _nextTick,
            RuntimeTimeSeconds = CurrentTimeSeconds,
            HasTargetCells = request?.HasTargetGrid == true,
            TargetGridX = request?.TargetGridX ?? 0,
            TargetGridY = request?.TargetGridY ?? 0,
            TargetGridHeight = request?.TargetGridHeight ?? 0
        });
        GameLog.Info(
            nameof(BattleRuntimeSessionController),
            $"BattleRuntimeCommandAuthorizationRejected battle={BattleId} command={request?.CommandId ?? ""} group={request?.BattleGroupId ?? ""} source={request?.SourceActorId ?? ""} reason={reasonCode ?? ""}");
        return new BattleRuntimeCommandSubmitResult
        {
            Accepted = false,
            ReasonCode = reasonCode ?? "",
            Events = EventStream.Events.Skip(startIndex).ToArray()
        };
    }

    private bool SkillMatchesRuntimeSource(BattleSkillSnapshot skill, BattleRuntimeActor source)
    {
        if (skill == null || source == null)
        {
            return false;
        }

        bool hasHeroOwner = !string.IsNullOrWhiteSpace(skill.OwnerHeroId);
        if (hasHeroOwner &&
            !string.Equals(skill.OwnerHeroId.Trim(), source.SourceHeroId?.Trim() ?? "", System.StringComparison.Ordinal))
        {
            return false;
        }

        if (!hasHeroOwner &&
            !string.IsNullOrWhiteSpace(skill.OwnerBattleGroupId) &&
            !string.Equals(skill.OwnerBattleGroupId.Trim(), source.BattleGroupId?.Trim() ?? "", System.StringComparison.Ordinal))
        {
            return false;
        }

        if (!hasHeroOwner &&
            !string.IsNullOrWhiteSpace(skill.RuntimeCommanderGroupId) &&
            !string.Equals(skill.RuntimeCommanderGroupId.Trim(), source.BattleGroupId?.Trim() ?? "", System.StringComparison.Ordinal))
        {
            return false;
        }

        string[] casterUnitIds = (skill.CasterUnitIds ?? new List<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .ToArray();
        if (casterUnitIds.Length > 0 &&
            !(State?.Actors?.Any(actor =>
                actor.HitPoints > 0 &&
                string.Equals(actor.BattleGroupId ?? "", source.BattleGroupId ?? "", System.StringComparison.Ordinal) &&
                casterUnitIds.Contains(actor.UnitDefinitionId?.Trim() ?? "", System.StringComparer.Ordinal)) == true))
        {
            return false;
        }

        // Older in-memory fixtures can omit grant ownership. Production compiled
        // snapshots carry owner/group/caster facts and are constrained above.
        return true;
    }

    private static bool ChannelMatches(CommandChannel requestChannel, BattleSkillCommandChannel skillChannel)
    {
        return requestChannel switch
        {
            CommandChannel.Hero => skillChannel == BattleSkillCommandChannel.Hero,
            CommandChannel.Corps => skillChannel == BattleSkillCommandChannel.Corps,
            CommandChannel.Combined => skillChannel == BattleSkillCommandChannel.Combined,
            _ => false
        };
    }

    private static bool SourceKindMatches(BattleRuntimeActorKind sourceKind, BattleSkillCommandChannel skillChannel)
    {
        return skillChannel switch
        {
            BattleSkillCommandChannel.Hero => sourceKind is BattleRuntimeActorKind.Hero or BattleRuntimeActorKind.Corps,
            BattleSkillCommandChannel.Corps => sourceKind == BattleRuntimeActorKind.Corps,
            BattleSkillCommandChannel.Combined => sourceKind is BattleRuntimeActorKind.Hero or BattleRuntimeActorKind.Corps,
            _ => false
        };
    }

    private static string[] ResolveRequestedBattleGroupIds(CommandRequest request)
    {
        List<string> groupIds = new();
        string primary = request?.BattleGroupId?.Trim() ?? "";
        if (!string.IsNullOrWhiteSpace(primary))
        {
            groupIds.Add(primary);
        }

        foreach (string groupId in request?.BattleGroupIds ?? Enumerable.Empty<string>())
        {
            string normalized = groupId?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(normalized) &&
                !groupIds.Contains(normalized, System.StringComparer.Ordinal))
            {
                groupIds.Add(normalized);
            }
        }

        return groupIds.ToArray();
    }

    public BattleRuntimeAdvanceResult AdvanceFixedTick(double fixedDeltaSeconds = BattleActionTimingPolicy.DefaultSimulationTickSeconds)
    {
        double deltaSeconds = _clock.NormalizeFixedDelta(fixedDeltaSeconds);
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

        // Tactical pause freezes battle truth even when a terminal condition is already visible.
        // Completion and outcome construction settle only after runtime resumes.
        if (_clock.IsPaused)
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
        BattleRuntimeTacticalCommandLifecycle.Advance(
            State,
            EventStream,
            BattleId,
            _nextTick,
            CurrentTimeSeconds);
        BattleTerminationReason tacticalCommandTermination = BattleRuntimeSession.ResolveTermination(State);
        if (tacticalCommandTermination != BattleTerminationReason.None)
        {
            Complete(tacticalCommandTermination);
            return BuildAdvanceResult(startIndex);
        }
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
            _clock.AdvanceFixed(fixedDeltaSeconds);
        }

        return BuildAdvanceResult(startIndex);
    }

    public BattleRuntimeSessionResult AdvanceToCompletion()
    {
        if (_invalidHandoff)
        {
            return BuildResult();
        }

        while (!IsComplete && !IsPaused)
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
            new BattleRuntimeTickResolver(new DefaultBattleRuntimeAiExecutor()),
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
        if (nextReady.HasValue)
        {
            _clock.AdvanceTo(nextReady.Value);
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
