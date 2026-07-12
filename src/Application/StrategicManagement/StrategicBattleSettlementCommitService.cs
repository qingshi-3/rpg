using System.Text.Json;
using Rpg.Application.StrategicBattleBridge;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.StrategicManagement;

public sealed class StrategicBattleSettlementCommitService
{
    private readonly StrategicManagementDefinitionSet _definitions;
    private readonly StrategicManagementSaveService _saveService;
    private readonly StrategicManagementStateInvariantService _invariants = new();

    public StrategicBattleSettlementCommitService(
        StrategicManagementDefinitionSet definitions,
        StrategicManagementSaveService saveService)
    {
        _definitions = definitions ?? new StrategicManagementDefinitionSet();
        _saveService = saveService ?? new StrategicManagementSaveService(_definitions);
    }

    public StrategicBattleSettlementCommitResult Commit(
        StrategicManagementState liveState,
        StrategicBattleActiveContext context,
        StrategicBattleActiveContextToken expectedResultToken,
        StrategicBattleResultSummary summary,
        string savePath,
        System.Action<StrategicManagementState> publishCandidate)
    {
        if (liveState == null || publishCandidate == null)
        {
            return StrategicBattleSettlementCommitResult.Failed(StrategicFailureReasons.StrategicPersistenceFailed);
        }

        StrategicBattleSettlementCommitResult replayResult = ResolveCommittedReplay(liveState, summary);
        if (replayResult != null)
        {
            if (replayResult.Success &&
                StrategicBattleActiveContextStore.TryPeek(
                    out StrategicBattleActiveContext currentContext,
                    out StrategicBattleActiveContextToken currentToken) &&
                ReferenceEquals(context, currentContext))
            {
                StrategicBattleResultSummary acceptedReplaySummary = new StrategicBattleBridgeService(_definitions)
                    .BuildResultSummary(context);
                if (!SummariesEqual(summary, acceptedReplaySummary))
                {
                    return StrategicBattleSettlementCommitResult.Failed(
                        StrategicFailureReasons.BattleResultMismatch,
                        replayResult.CommandResult);
                }

                string replayConsumeFailure = StrategicBattleActiveContextStore.CasMismatchReason;
                bool replayConsumed = expectedResultToken?.Equals(currentToken) == true &&
                    StrategicBattleActiveContextStore.TryConsumeCommittedReplay(
                        expectedResultToken,
                        context,
                        out _,
                        out replayConsumeFailure);
                if (!replayConsumed)
                {
                    bool exactContextStillActive = StrategicBattleActiveContextStore.TryPeek(
                        out StrategicBattleActiveContext remainingContext,
                        out _) &&
                        ReferenceEquals(context, remainingContext);
                    if (exactContextStillActive)
                    {
                        GameLog.Warn(
                            nameof(StrategicBattleSettlementCommitService),
                            $"StrategicBattleSettlementReplayConsumeFailed context={context.ContextId} expectedRevision={expectedResultToken?.Revision ?? 0} currentRevision={currentToken.Revision} result={expectedResultToken?.ResultId ?? ""} reason={replayConsumeFailure ?? StrategicBattleActiveContextStore.CasMismatchReason}");
                        return StrategicBattleSettlementCommitResult.Failed(
                            StrategicFailureReasons.ActiveBattleContextMismatch,
                            replayResult.CommandResult);
                    }
                }
            }
            else if (replayResult.Success && StrategicBattleActiveContextStore.HasActiveContext &&
                     !StrategicBattleActiveContextStore.TryPeek(out _, out _))
            {
                return StrategicBattleSettlementCommitResult.Failed(
                    StrategicFailureReasons.ActiveBattleContextMismatch,
                    replayResult.CommandResult);
            }

            return replayResult;
        }

        string completenessFailure = StrategicBattleBridgeService.GetActiveContextFailureReason(context);
        if (!string.IsNullOrWhiteSpace(completenessFailure))
        {
            return StrategicBattleSettlementCommitResult.Failed(completenessFailure);
        }

        string identityFailure = GetIdentityFailureReason(context, summary);
        if (!string.IsNullOrWhiteSpace(identityFailure) ||
            !StrategicBattleActiveContextStore.TryPeek(
                expectedResultToken,
                out StrategicBattleActiveContext storedContext) ||
            !ReferenceEquals(context, storedContext))
        {
            return StrategicBattleSettlementCommitResult.Failed(
                string.IsNullOrWhiteSpace(identityFailure)
                    ? StrategicFailureReasons.ActiveBattleContextMismatch
                    : identityFailure);
        }

        // P2-14 owns the only accepted result facts. A caller may transport the
        // summary, but cannot substitute a divergent outcome or consequence set.
        StrategicBattleResultSummary acceptedSummary = new StrategicBattleBridgeService(_definitions)
            .BuildResultSummary(context);
        if (!SummariesEqual(summary, acceptedSummary))
        {
            return StrategicBattleSettlementCommitResult.Failed(StrategicFailureReasons.BattleResultMismatch);
        }

        StrategicManagementState candidate = _saveService.CloneCandidate(liveState);
        StrategicManagementCommandService candidateCommands = new(
            _definitions,
            new StrategicManagementRules(_definitions));
        StrategicCommandResult commandResult = candidateCommands.ApplyBattleResultSummary(candidate, acceptedSummary);
        if (!commandResult.Success)
        {
            return StrategicBattleSettlementCommitResult.Failed(commandResult.FailureReason, commandResult);
        }

        _invariants.RepairAll(candidate);

        bool committed = StrategicBattleActiveContextStore.TryCommitAndConsume(
            expectedResultToken,
            context,
            () => _saveService.Save(candidate, savePath),
            () => publishCandidate?.Invoke(candidate),
            out _,
            out System.Exception callbackFailure,
            out string storeFailureReason);
        if (!committed)
        {
            string failureReason = callbackFailure == null
                ? StrategicFailureReasons.ActiveBattleContextMismatch
                : StrategicFailureReasons.StrategicPersistenceFailed;
            GameLog.Warn(
                nameof(StrategicBattleSettlementCommitService),
                $"StrategicBattleSettlementCommitFailed context={context.ContextId} session={context.Session.SessionId} snapshot={context.Snapshot.SnapshotId} revision={expectedResultToken?.Revision ?? 0} result={expectedResultToken?.ResultId ?? ""} reason={failureReason} storeReason={storeFailureReason} exception={callbackFailure?.GetType().Name ?? ""}");
            return StrategicBattleSettlementCommitResult.Failed(failureReason, commandResult);
        }

        return StrategicBattleSettlementCommitResult.Ok(commandResult, candidate);
    }

    private StrategicBattleSettlementCommitResult ResolveCommittedReplay(
        StrategicManagementState liveState,
        StrategicBattleResultSummary summary)
    {
        if (summary == null ||
            liveState.BattleSettlementRecordsByExpedition == null ||
            !liveState.BattleSettlementRecordsByExpedition.ContainsKey(summary.ExpeditionId ?? ""))
        {
            return null;
        }

        // The durable settlement record outlives Active Context, which the first successful commit consumes.
        StrategicManagementState replayCandidate = _saveService.CloneCandidate(liveState);
        StrategicManagementCommandService replayCommands = new(
            _definitions,
            new StrategicManagementRules(_definitions));
        StrategicCommandResult commandResult = replayCommands.ApplyBattleResultSummary(replayCandidate, summary);
        return commandResult.Success
            ? StrategicBattleSettlementCommitResult.Ok(commandResult, liveState)
            : StrategicBattleSettlementCommitResult.Failed(commandResult.FailureReason, commandResult);
    }

    private static string GetIdentityFailureReason(
        StrategicBattleActiveContext context,
        StrategicBattleResultSummary summary)
    {
        if (context?.Session == null || context.Snapshot == null || summary == null)
        {
            return StrategicFailureReasons.MissingBattleResultSummary;
        }

        return string.Equals(context.Session.SessionId ?? "", summary.SessionId ?? "", System.StringComparison.Ordinal) &&
               string.Equals(context.Snapshot.SnapshotId ?? "", summary.SnapshotId ?? "", System.StringComparison.Ordinal) &&
               string.Equals(context.Session.ExpeditionId ?? "", summary.ExpeditionId ?? "", System.StringComparison.Ordinal)
            ? ""
            : StrategicFailureReasons.BattleResultMismatch;
    }

    private static bool SummariesEqual(
        StrategicBattleResultSummary supplied,
        StrategicBattleResultSummary accepted)
    {
        return supplied != null &&
               accepted != null &&
               string.Equals(
                   JsonSerializer.Serialize(supplied),
                   JsonSerializer.Serialize(accepted),
                   System.StringComparison.Ordinal);
    }
}

public sealed class StrategicBattleSettlementCommitResult
{
    public bool Success { get; private set; }
    public string FailureReason { get; private set; } = "";
    public StrategicCommandResult CommandResult { get; private set; }
    public StrategicManagementState PublishedState { get; private set; }

    public static StrategicBattleSettlementCommitResult Ok(
        StrategicCommandResult commandResult,
        StrategicManagementState publishedState) => new()
    {
        Success = true,
        CommandResult = commandResult,
        PublishedState = publishedState
    };

    public static StrategicBattleSettlementCommitResult Failed(
        string failureReason,
        StrategicCommandResult commandResult = null) => new()
    {
        FailureReason = failureReason ?? "",
        CommandResult = commandResult
    };
}
