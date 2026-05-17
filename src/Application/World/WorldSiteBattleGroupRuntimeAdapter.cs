using Rpg.Application.Battle;
using Rpg.Application.Battle.Adapters;
using Rpg.Application.Battle.Reports;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.World;

public sealed class WorldSiteBattleGroupRuntimeResolveResult
{
    public bool Success { get; init; }
    public string FailureReason { get; init; } = "";
    public BattleStartRequest Request { get; init; }
    public BattleResult BattleResult { get; init; }
    public BattleReportRecord Report { get; init; }
    public BattleGroupBattleFlowResult FlowResult { get; init; }
}

public sealed class WorldSiteBattleGroupRuntimeAdapter
{
    private readonly BattleGroupSessionProbeService _sessionService = new();
    private readonly LegacyBattleResultAdapter _legacyResultAdapter = new();

    public bool TryResolveActiveBattle(out WorldSiteBattleGroupRuntimeResolveResult result)
    {
        if (!BattleSessionHandoff.TryPeekActiveRequest(out BattleStartRequest request) || request == null)
        {
            result = Reject("battle_handoff_missing", null, null);
            return false;
        }

        BattleGroupSessionProbeResult sessionResult = _sessionService.Probe(request);
        if (!sessionResult.Success)
        {
            result = Reject(sessionResult.FailureReason, request, sessionResult.FlowResult);
            return false;
        }

        BattleResult legacyResult = _legacyResultAdapter.ToLegacyResult(
            request,
            sessionResult.FlowResult.RuntimeResult.Outcome);
        CopyObjectiveResults(request, legacyResult);
        BattleSessionHandoff.CompleteBattle(legacyResult);
        BattleSessionHandoff.TryConsumeLastBattleResult(out _, out _);

        result = new WorldSiteBattleGroupRuntimeResolveResult
        {
            Success = true,
            Request = request,
            BattleResult = legacyResult,
            Report = sessionResult.FlowResult.Report,
            FlowResult = sessionResult.FlowResult
        };
        GameLog.Info(
            nameof(WorldSiteBattleGroupRuntimeAdapter),
            $"BattleGroupRuntimeResolved request={request.RequestId} snapshot={sessionResult.Snapshot.SnapshotId} outcome={legacyResult.Outcome} events={sessionResult.FlowResult.RuntimeResult.EventStream.Events.Count}");
        return true;
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

    private static WorldSiteBattleGroupRuntimeResolveResult Reject(
        string reason,
        BattleStartRequest request,
        BattleGroupBattleFlowResult flowResult)
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
            FlowResult = flowResult
        };
    }
}
