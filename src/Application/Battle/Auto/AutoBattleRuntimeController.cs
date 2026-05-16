using System;
using System.Collections.Generic;
using System.Linq;

namespace Rpg.Application.Battle.Auto;

public sealed class AutoBattleRuntimeController
{
    private readonly AutoBattleSessionRunner _sessionRunner;
    private readonly AutoBattleReportBuilder _reportBuilder;
    private readonly double _secondsPerReportEvent;
    private double _playbackAccumulatorSeconds;
    private double _playbackSpeed = 1.0;

    public AutoBattleRuntimeController(
        AutoBattleSessionRunner sessionRunner = null,
        AutoBattleReportBuilder reportBuilder = null,
        AutoBattleRuntimeControllerConfig config = null)
    {
        _sessionRunner = sessionRunner ?? new AutoBattleSessionRunner();
        _reportBuilder = reportBuilder ?? new AutoBattleReportBuilder();
        config ??= new AutoBattleRuntimeControllerConfig();
        _secondsPerReportEvent = Math.Max(0.01, config.SecondsPerReportEvent);
    }

    public AutoBattleRuntimePhase Phase { get; private set; } = AutoBattleRuntimePhase.Idle;
    public double PlaybackSpeed => _playbackSpeed;
    public string FailureReason { get; private set; } = "";
    public AutoBattleSimulationResult SimulationResult { get; private set; }
    public AutoBattleReport Report { get; private set; }
    public int VisibleEventCount { get; private set; }

    public IReadOnlyList<AutoBattleReportEvent> VisibleEventFeed
    {
        get
        {
            if (Report?.EventFeed == null || VisibleEventCount <= 0)
            {
                return Array.Empty<AutoBattleReportEvent>();
            }

            return Report.EventFeed.Take(VisibleEventCount).ToList();
        }
    }

    public bool StartActiveBattle(out string failureReason)
    {
        Reset();

        if (!_sessionRunner.TryRunActiveBattle(out AutoBattleSimulationResult simulationResult, out failureReason))
        {
            FailureReason = failureReason ?? "";
            Phase = AutoBattleRuntimePhase.Failed;
            return false;
        }

        SimulationResult = simulationResult;
        Report = _reportBuilder.Build(simulationResult);
        Phase = AutoBattleRuntimePhase.Playing;
        failureReason = "";
        return true;
    }

    public void Pause()
    {
        if (Phase == AutoBattleRuntimePhase.Playing)
        {
            Phase = AutoBattleRuntimePhase.Paused;
        }
    }

    public void Resume()
    {
        if (Phase == AutoBattleRuntimePhase.Paused)
        {
            Phase = AutoBattleRuntimePhase.Playing;
        }
    }

    public void SetPlaybackSpeed(double speed)
    {
        _playbackSpeed = Math.Max(0.1, speed);
    }

    public void AdvancePlayback(double deltaSeconds)
    {
        if (Phase != AutoBattleRuntimePhase.Playing || Report?.EventFeed == null || deltaSeconds <= 0)
        {
            return;
        }

        _playbackAccumulatorSeconds += deltaSeconds * _playbackSpeed;
        int revealCount = (int)Math.Floor(_playbackAccumulatorSeconds / _secondsPerReportEvent);
        if (revealCount <= 0)
        {
            return;
        }

        _playbackAccumulatorSeconds -= revealCount * _secondsPerReportEvent;
        VisibleEventCount = Math.Min(Report.EventFeed.Count, VisibleEventCount + revealCount);
        if (VisibleEventCount >= Report.EventFeed.Count)
        {
            Phase = AutoBattleRuntimePhase.Completed;
        }
    }

    public void SkipToEnd()
    {
        if (Report?.EventFeed == null)
        {
            return;
        }

        VisibleEventCount = Report.EventFeed.Count;
        Phase = AutoBattleRuntimePhase.Completed;
    }

    private void Reset()
    {
        Phase = AutoBattleRuntimePhase.Idle;
        FailureReason = "";
        SimulationResult = null;
        Report = null;
        VisibleEventCount = 0;
        _playbackAccumulatorSeconds = 0;
        _playbackSpeed = 1.0;
    }
}
