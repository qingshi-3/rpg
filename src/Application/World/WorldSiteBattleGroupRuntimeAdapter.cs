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
    public StrategicBattleActiveContextToken ActiveContextToken { get; init; }
}

public sealed class WorldSiteBattleGroupRuntimeAdapter
{
    private readonly BattleRuntimeSession _runtimeSession;
    private readonly BattleSettlementService _settlementService = new();
    private readonly BattleReportBuilder _reportBuilder = new();
    private readonly LegacyBattleResultAdapter _legacyResultAdapter = new();
    private readonly StrategicBattleDraftSnapshotCompiler _strategicDraftSnapshotCompiler = new();

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
        // Strategic battles must enter Runtime through StrategicBattleActiveContext.
        // The old handoff/probe path can no longer author production Runtime facts.
        BattleSessionHandoff.TryPeekActiveRequest(out BattleStartRequest request);
        result = Reject("strategic_battle_active_context_required", request, null);
        return false;
    }

    public bool TryStartActiveBattle(
        StrategicBattleActiveContext activeContext,
        StrategicBattleActiveContextToken expectedToken,
        out WorldSiteBattleGroupRuntimeResolveResult result)
    {
        StrategicBattlePreparationDraft draft = activeContext?.PreparationDraft;
        if (activeContext == null || draft == null)
        {
            result = Reject("strategic_battle_active_context_missing", null, null);
            return false;
        }

        if (!StrategicBattleActiveContextStore.TryPeek(expectedToken, out StrategicBattleActiveContext storedContext) ||
            !ReferenceEquals(activeContext, storedContext))
        {
            result = Reject(
                StrategicBattleActiveContextStore.CasMismatchReason,
                activeContext?.CompatibilityRequest,
                null,
                activeContext,
                expectedToken);
            return false;
        }

        BattleStartSnapshot activeSnapshot = activeContext.Snapshot;
        if (activeSnapshot == null || string.IsNullOrWhiteSpace(activeSnapshot.SnapshotId))
        {
            result = Reject("strategic_battle_active_context_snapshot_missing", null, null, activeContext, expectedToken);
            return false;
        }

        if (activeContext.Session != null &&
            (!string.Equals(activeContext.Session.SessionId ?? "", activeContext.ContextId ?? "", System.StringComparison.Ordinal) ||
             !string.Equals(activeSnapshot.BattleId ?? "", activeContext.Session.SessionId ?? "", System.StringComparison.Ordinal)))
        {
            result = Reject("strategic_battle_active_context_snapshot_mismatch", null, null, activeContext, expectedToken);
            return false;
        }

        if (!TryBuildStrategicLaunchSnapshot(
                activeContext,
                draft,
                expectedToken,
                out BattleStartSnapshot snapshot,
                out BattleStartRequest request,
                out StrategicBattleActiveContextToken snapshotToken,
                out string compileFailureReason))
        {
            result = Reject(
                compileFailureReason,
                activeContext.CompatibilityRequest,
                null,
                activeContext,
                expectedToken);
            return false;
        }

        BattleRuntimeSessionController runtimeController = _runtimeSession.Begin(snapshot);
        if (IsInvalidRuntimeStart(runtimeController))
        {
            result = Reject("battle_group_runtime_start_failed", request, new BattleGroupBattleFlowResult
            {
                Snapshot = snapshot,
                RuntimeResult = runtimeController.BuildResult()
            }, activeContext, snapshotToken);
            return false;
        }

        result = new WorldSiteBattleGroupRuntimeResolveResult
        {
            Success = true,
            Request = request,
            Snapshot = snapshot,
            RuntimeController = runtimeController,
            ActiveContext = activeContext,
            ActiveContextToken = snapshotToken,
            FlowResult = new BattleGroupBattleFlowResult
            {
                Snapshot = snapshot,
                RuntimeResult = runtimeController.BuildResult()
            }
        };
        GameLog.Info(
            nameof(WorldSiteBattleGroupRuntimeAdapter),
            $"StrategicBattleGroupRuntimeStarted context={activeContext.ContextId} session={activeContext.Session?.SessionId ?? ""} request={request.RequestId} snapshot={snapshot.SnapshotId} revision={snapshotToken.Revision} initialEvents={runtimeController.EventStream.Events.Count}");
        return true;
    }

    private bool TryBuildStrategicLaunchSnapshot(
        StrategicBattleActiveContext activeContext,
        StrategicBattlePreparationDraft draft,
        StrategicBattleActiveContextToken expectedToken,
        out BattleStartSnapshot snapshot,
        out BattleStartRequest compatibilityRequest,
        out StrategicBattleActiveContextToken snapshotToken,
        out string failureReason)
    {
        snapshot = null;
        compatibilityRequest = null;
        snapshotToken = null;
        failureReason = "";
        BattleStartSnapshot preparationSeed = activeContext?.PreparationSeedSnapshot;
        if (activeContext == null ||
            draft == null ||
            preparationSeed == null ||
            string.IsNullOrWhiteSpace(preparationSeed.SnapshotId))
        {
            failureReason = "strategic_battle_active_context_snapshot_missing";
            return false;
        }

        StrategicBattleDraftSnapshotResult compileResult = _strategicDraftSnapshotCompiler.CompileAndCommitFinalSnapshot(
            activeContext,
            expectedToken,
            out snapshotToken);
        if (!compileResult.Success)
        {
            failureReason = string.IsNullOrWhiteSpace(compileResult.FailureReason)
                ? "strategic_battle_draft_snapshot_compile_failed"
                : compileResult.FailureReason;
            return false;
        }

        snapshot = compileResult.Snapshot;
        compatibilityRequest = activeContext.CompatibilityRequest;
        GameLog.Info(
            nameof(WorldSiteBattleGroupRuntimeAdapter),
            $"StrategicBattleFinalSnapshotCommitted context={activeContext.ContextId ?? ""} draft={draft.DraftId} draftRevision={draft.Revision} contextRevision={snapshotToken.Revision} request={compatibilityRequest.RequestId ?? ""} snapshot={snapshot.SnapshotId} battle={snapshot.BattleId} groups={snapshot.BattleGroups.Count}");
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

        BattleStartSnapshot snapshot = started.Snapshot ?? started.FlowResult?.Snapshot ?? new BattleStartSnapshot();
        if (!runtimeController.IsComplete)
        {
            // Presentation-backed battles must already be complete when settlement begins.
            // Completing here would turn an interrupted live handoff into normal writeback.
            return Reject("battle_group_runtime_incomplete", request, new BattleGroupBattleFlowResult
            {
                Snapshot = snapshot,
                RuntimeResult = runtimeController.BuildResult()
            });
        }

        BattleRuntimeSessionResult runtimeResult = runtimeController.BuildResult();
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
        BattleStartRequest request = activeContext?.CompatibilityRequest;
        BattleRuntimeSessionController runtimeController = started?.RuntimeController;
        StrategicBattleActiveContextToken expectedToken = started?.ActiveContextToken;
        if (activeContext == null ||
            request == null ||
            runtimeController == null ||
            expectedToken == null ||
            !ReferenceEquals(started.ActiveContext, activeContext) ||
            !StrategicBattleActiveContextStore.TryPeek(expectedToken, out StrategicBattleActiveContext storedContext) ||
            !ReferenceEquals(storedContext, activeContext))
        {
            return Reject(
                expectedToken == null || activeContext == null || request == null || runtimeController == null
                    ? "strategic_battle_group_runtime_completion_missing"
                    : StrategicBattleActiveContextStore.CasMismatchReason,
                request,
                started?.FlowResult,
                activeContext,
                expectedToken);
        }

        BattleStartSnapshot snapshot = started?.Snapshot;
        if (snapshot == null ||
            activeContext.Snapshot == null ||
            !string.Equals(snapshot.SnapshotId ?? "", activeContext.Snapshot.SnapshotId ?? "", System.StringComparison.Ordinal) ||
            !string.Equals(snapshot.BattleId ?? "", activeContext.Session?.SessionId ?? "", System.StringComparison.Ordinal))
        {
            return Reject("strategic_battle_active_context_snapshot_mismatch", request, started?.FlowResult, activeContext, expectedToken);
        }

        if (!runtimeController.IsComplete)
        {
            // Strategic writeback only accepts runtime facts produced before this boundary.
            // Do not force an incomplete live-clock battle into a settlement/report chain.
            BattleRuntimeSessionResult incompleteResult = runtimeController.BuildResult();
            BattleGroupBattleFlowResult incompleteFlowResult = new()
            {
                Snapshot = snapshot,
                RuntimeResult = incompleteResult
            };
            return Reject("battle_group_runtime_incomplete", request, incompleteFlowResult, activeContext, expectedToken);
        }

        BattleRuntimeSessionResult runtimeResult = runtimeController.BuildResult();
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
            return Reject(reason, request, flowResult, activeContext, expectedToken);
        }

        BattleResult compatibilityResult = _legacyResultAdapter.ToLegacyResult(
            request,
            runtimeResult.Outcome);
        CopyObjectiveResults(request, compatibilityResult);
        if (!StrategicBattleActiveContextStore.TryPublishResultEnvelope(
                expectedToken,
                activeContext,
                runtimeResult,
                settlementPlan,
                report,
                compatibilityResult,
                out StrategicBattleResultEnvelope acceptedEnvelope,
                out StrategicBattleActiveContextToken resultToken,
                out string envelopeFailureReason))
        {
            return Reject(envelopeFailureReason, request, flowResult, activeContext, expectedToken);
        }
        BattleResult acceptedCompatibilityResult = activeContext.CompatibilityResult ?? compatibilityResult;
        BattleGroupBattleFlowResult acceptedFlowResult = new()
        {
            Snapshot = snapshot,
            RuntimeResult = acceptedEnvelope.RuntimeResult,
            SettlementPlan = acceptedEnvelope.SettlementPlan,
            Report = acceptedEnvelope.Report
        };

        WorldSiteBattleGroupRuntimeResolveResult result = new()
        {
            Success = true,
            Request = request,
            BattleResult = acceptedCompatibilityResult,
            Report = acceptedEnvelope.Report,
            FlowResult = acceptedFlowResult,
            Snapshot = snapshot,
            RuntimeController = runtimeController,
            ActiveContext = activeContext,
            ActiveContextToken = resultToken
        };
        GameLog.Info(
            nameof(WorldSiteBattleGroupRuntimeAdapter),
            $"StrategicBattleGroupRuntimeResolved context={activeContext.ContextId} request={request.RequestId} snapshot={snapshot.SnapshotId} revision={resultToken.Revision} result={resultToken.ResultId} outcome={acceptedCompatibilityResult.Outcome} events={acceptedEnvelope.RuntimeResult.EventStream.Events.Count}");
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
                   item.Kind == Rpg.Runtime.Battle.Events.BattleEventKind.CommandRejected);
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
        StrategicBattleActiveContext activeContext,
        StrategicBattleActiveContextToken activeContextToken = null)
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
            ActiveContext = activeContext,
            ActiveContextToken = activeContextToken
        };
    }
}
