using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Adapters;
using Rpg.Application.Battle.Reports;
using Rpg.Application.Battle.Settlement;
using Rpg.Application.Battle.Snapshots;
using Rpg.Application.StrategicBattleBridge;
using Rpg.Infrastructure.Diagnostics;
using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle;

namespace Rpg.Application.World;

public sealed class WorldSiteBattleGroupRuntimeResolveResult
{
    public bool Success { get; init; }
    public string FailureReason { get; init; } = "";
    public BattleStartRequest Request { get; init; }
    public BattleResult BattleResult { get; init; }
    public BattleReportRecord Report { get; init; }
    public BattleGroupBattleFlowResult FlowResult { get; init; }
    public BattleStartSnapshot Snapshot { get; init; }
    public BattleRuntimeSessionController RuntimeController { get; init; }
    public StrategicBattleActiveContext ActiveContext { get; init; }
}

public sealed class WorldSiteBattleGroupRuntimeAdapter
{
    private readonly BattleGroupSessionProbeService _sessionService = new();
    private readonly BattleRuntimeSession _runtimeSession;
    private readonly BattleSettlementService _settlementService = new();
    private readonly BattleReportBuilder _reportBuilder = new();
    private readonly LegacyBattleResultAdapter _legacyResultAdapter = new();

    public WorldSiteBattleGroupRuntimeAdapter(BattlePerformanceCounters performanceCounters = null)
    {
        _runtimeSession = new(performanceCounters: performanceCounters);
    }

    public bool TryResolveActiveBattle(out WorldSiteBattleGroupRuntimeResolveResult result)
    {
        if (!TryStartActiveBattle(out WorldSiteBattleGroupRuntimeResolveResult started))
        {
            result = started;
            return false;
        }

        started.RuntimeController.AdvanceToCompletion();
        result = CompleteResolvedBattle(started);
        return result.Success;
    }

    public bool TryStartActiveBattle(out WorldSiteBattleGroupRuntimeResolveResult result)
    {
        if (!BattleSessionHandoff.TryPeekActiveRequest(out BattleStartRequest request) || request == null)
        {
            result = Reject("battle_handoff_missing", null, null);
            return false;
        }

        BattleGroupSessionProbeResult sessionResult = _sessionService.PrepareSnapshot(request);
        if (!sessionResult.Success)
        {
            result = Reject(sessionResult.FailureReason, request, sessionResult.FlowResult);
            return false;
        }

        BattleRuntimeSessionController runtimeController = _runtimeSession.Begin(sessionResult.Snapshot);
        if (IsInvalidRuntimeStart(runtimeController))
        {
            result = Reject("battle_group_runtime_start_failed", request, new BattleGroupBattleFlowResult
            {
                Snapshot = sessionResult.Snapshot,
                RuntimeResult = runtimeController.BuildResult()
            });
            return false;
        }

        result = new WorldSiteBattleGroupRuntimeResolveResult
        {
            Success = true,
            Request = request,
            Snapshot = sessionResult.Snapshot,
            RuntimeController = runtimeController,
            FlowResult = new BattleGroupBattleFlowResult
            {
                Snapshot = sessionResult.Snapshot,
                RuntimeResult = runtimeController.BuildResult()
            }
        };
        GameLog.Info(
            nameof(WorldSiteBattleGroupRuntimeAdapter),
            $"BattleGroupRuntimeStarted request={request.RequestId} snapshot={sessionResult.Snapshot.SnapshotId} initialEvents={runtimeController.EventStream.Events.Count}");
        return true;
    }

    public bool TryStartActiveBattle(
        StrategicBattleActiveContext activeContext,
        out WorldSiteBattleGroupRuntimeResolveResult result)
    {
        BattleStartRequest request = activeContext?.CompatibilityRequest;
        if (activeContext == null || request == null)
        {
            result = Reject("strategic_battle_active_context_missing", request, null);
            return false;
        }

        BattleGroupSessionProbeResult sessionResult = _sessionService.PrepareSnapshot(request);
        if (!sessionResult.Success)
        {
            result = Reject(sessionResult.FailureReason, request, sessionResult.FlowResult, activeContext);
            return false;
        }

        activeContext.Snapshot = sessionResult.Snapshot;
        BattleRuntimeSessionController runtimeController = _runtimeSession.Begin(sessionResult.Snapshot);
        if (IsInvalidRuntimeStart(runtimeController))
        {
            result = Reject("battle_group_runtime_start_failed", request, new BattleGroupBattleFlowResult
            {
                Snapshot = sessionResult.Snapshot,
                RuntimeResult = runtimeController.BuildResult()
            }, activeContext);
            return false;
        }

        result = new WorldSiteBattleGroupRuntimeResolveResult
        {
            Success = true,
            Request = request,
            Snapshot = sessionResult.Snapshot,
            RuntimeController = runtimeController,
            ActiveContext = activeContext,
            FlowResult = new BattleGroupBattleFlowResult
            {
                Snapshot = sessionResult.Snapshot,
                RuntimeResult = runtimeController.BuildResult()
            }
        };
        GameLog.Info(
            nameof(WorldSiteBattleGroupRuntimeAdapter),
            $"StrategicBattleGroupRuntimeStarted context={activeContext.ContextId} request={request.RequestId} snapshot={sessionResult.Snapshot.SnapshotId} initialEvents={runtimeController.EventStream.Events.Count}");
        return true;
    }

    public WorldSiteBattleGroupRuntimeResolveResult CompleteResolvedBattle(WorldSiteBattleGroupRuntimeResolveResult started)
    {
        BattleStartRequest request = started?.Request;
        BattleRuntimeSessionController runtimeController = started?.RuntimeController;
        if (request == null || runtimeController == null)
        {
            return Reject("battle_group_runtime_completion_missing", request, started?.FlowResult);
        }

        if (!runtimeController.IsComplete)
        {
            runtimeController.AdvanceToCompletion();
        }

        BattleRuntimeSessionResult runtimeResult = runtimeController.BuildResult();
        BattleStartSnapshot snapshot = started.Snapshot ?? started.FlowResult?.Snapshot ?? new BattleStartSnapshot();
        SettlementPlan settlementPlan = _settlementService.BuildPlan(
            snapshot.SnapshotId,
            runtimeResult.Outcome,
            runtimeResult.EventStream);
        BattleReportRecord report = _reportBuilder.Build(
            runtimeResult.Outcome,
            runtimeResult.EventStream,
            settlementPlan);
        BattleGroupBattleFlowResult flowResult = new()
        {
            Snapshot = snapshot,
            RuntimeResult = runtimeResult,
            SettlementPlan = settlementPlan,
            Report = report
        };
        if (!settlementPlan.Accepted)
        {
            string reason = string.IsNullOrWhiteSpace(settlementPlan.RejectionReason)
                ? "battle_group_runtime_settlement_rejected"
                : settlementPlan.RejectionReason;
            return Reject(reason, request, flowResult);
        }

        BattleResult legacyResult = _legacyResultAdapter.ToLegacyResult(
            request,
            runtimeResult.Outcome);
        CopyObjectiveResults(request, legacyResult);
        BattleSessionHandoff.CompleteBattle(legacyResult);
        BattleSessionHandoff.TryConsumeLastBattleResult(out _, out _);

        WorldSiteBattleGroupRuntimeResolveResult result = new()
        {
            Success = true,
            Request = request,
            BattleResult = legacyResult,
            Report = report,
            FlowResult = flowResult,
            Snapshot = snapshot,
            RuntimeController = runtimeController
        };
        GameLog.Info(
            nameof(WorldSiteBattleGroupRuntimeAdapter),
            $"BattleGroupRuntimeResolved request={request.RequestId} snapshot={snapshot.SnapshotId} outcome={legacyResult.Outcome} events={runtimeResult.EventStream.Events.Count}");
        return result;
    }

    public WorldSiteBattleGroupRuntimeResolveResult CompleteResolvedBattle(
        WorldSiteBattleGroupRuntimeResolveResult started,
        StrategicBattleActiveContext activeContext)
    {
        BattleStartRequest request = activeContext?.CompatibilityRequest ?? started?.Request;
        BattleRuntimeSessionController runtimeController = started?.RuntimeController;
        if (activeContext == null || request == null || runtimeController == null)
        {
            return Reject("strategic_battle_group_runtime_completion_missing", request, started?.FlowResult, activeContext);
        }

        if (!runtimeController.IsComplete)
        {
            runtimeController.AdvanceToCompletion();
        }

        BattleRuntimeSessionResult runtimeResult = runtimeController.BuildResult();
        BattleStartSnapshot snapshot = started.Snapshot ?? started.FlowResult?.Snapshot ?? activeContext.Snapshot ?? new BattleStartSnapshot();
        SettlementPlan settlementPlan = _settlementService.BuildPlan(
            snapshot.SnapshotId,
            runtimeResult.Outcome,
            runtimeResult.EventStream);
        BattleReportRecord report = _reportBuilder.Build(
            runtimeResult.Outcome,
            runtimeResult.EventStream,
            settlementPlan);
        BattleGroupBattleFlowResult flowResult = new()
        {
            Snapshot = snapshot,
            RuntimeResult = runtimeResult,
            SettlementPlan = settlementPlan,
            Report = report
        };
        activeContext.Snapshot = snapshot;
        activeContext.RuntimeResult = runtimeResult;
        activeContext.SettlementPlan = settlementPlan;
        activeContext.Report = report;
        activeContext.FlowResult = flowResult;
        if (!settlementPlan.Accepted)
        {
            string reason = string.IsNullOrWhiteSpace(settlementPlan.RejectionReason)
                ? "battle_group_runtime_settlement_rejected"
                : settlementPlan.RejectionReason;
            return Reject(reason, request, flowResult, activeContext);
        }

        BattleResult compatibilityResult = _legacyResultAdapter.ToLegacyResult(
            request,
            runtimeResult.Outcome);
        CopyObjectiveResults(request, compatibilityResult);
        activeContext.CompatibilityResult = compatibilityResult;

        WorldSiteBattleGroupRuntimeResolveResult result = new()
        {
            Success = true,
            Request = request,
            BattleResult = compatibilityResult,
            Report = report,
            FlowResult = flowResult,
            Snapshot = snapshot,
            RuntimeController = runtimeController,
            ActiveContext = activeContext
        };
        GameLog.Info(
            nameof(WorldSiteBattleGroupRuntimeAdapter),
            $"StrategicBattleGroupRuntimeResolved context={activeContext.ContextId} request={request.RequestId} snapshot={snapshot.SnapshotId} outcome={compatibilityResult.Outcome} events={runtimeResult.EventStream.Events.Count}");
        return result;
    }

    private static void CopyObjectiveResults(BattleStartRequest request, BattleResult result)
    {
        if (request == null || result == null || result.ObjectiveResults.Count > 0)
        {
            return;
        }

        foreach (string objectiveId in request.ObjectiveIds)
        {
            result.ObjectiveResults.Add(new BattleObjectiveResult
            {
                ObjectiveId = objectiveId,
                State = result.Outcome == BattleOutcome.Victory
                    ? BattleObjectiveState.Succeeded
                    : BattleObjectiveState.Failed
            });
        }
    }

    private static bool IsInvalidRuntimeStart(BattleRuntimeSessionController runtimeController)
    {
        return runtimeController?.Outcome?.IsComplete == false &&
               runtimeController.Outcome.TerminationReason == BattleTerminationReason.RuntimeException &&
               runtimeController.EventStream.Events.Any(item =>
                   item.Kind == Rpg.Runtime.Battle.Events.BattleEventKind.CommandRejected &&
                   item.ReasonCode == "battle_snapshot_invalid");
    }

    private static WorldSiteBattleGroupRuntimeResolveResult Reject(
        string reason,
        BattleStartRequest request,
        BattleGroupBattleFlowResult flowResult)
    {
        return Reject(reason, request, flowResult, null);
    }

    private static WorldSiteBattleGroupRuntimeResolveResult Reject(
        string reason,
        BattleStartRequest request,
        BattleGroupBattleFlowResult flowResult,
        StrategicBattleActiveContext activeContext)
    {
        string failureReason = string.IsNullOrWhiteSpace(reason)
            ? "battle_group_runtime_resolution_failed"
            : reason;
        GameLog.Warn(nameof(WorldSiteBattleGroupRuntimeAdapter), $"BattleGroupRuntimeResolveFailed request={request?.RequestId ?? ""} reason={failureReason}");
        return new WorldSiteBattleGroupRuntimeResolveResult
        {
            Success = false,
            FailureReason = failureReason,
            Request = request,
            FlowResult = flowResult,
            ActiveContext = activeContext
        };
    }
}
