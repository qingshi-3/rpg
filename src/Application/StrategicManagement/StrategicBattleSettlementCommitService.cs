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
                context?.ContextId,
                context?.Session?.SessionId,
                context?.Snapshot?.SnapshotId,
                out _))
        {
            return StrategicBattleSettlementCommitResult.Failed(
                string.IsNullOrWhiteSpace(identityFailure)
                    ? StrategicFailureReasons.ActiveBattleContextMismatch
                    : identityFailure);
        }

        StrategicManagementState candidate = _saveService.CloneCandidate(liveState);
        StrategicManagementCommandService candidateCommands = new(
            _definitions,
            new StrategicManagementRules(_definitions));
        StrategicCommandResult commandResult = candidateCommands.ApplyBattleResultSummary(candidate, summary);
        if (!commandResult.Success)
        {
            return StrategicBattleSettlementCommitResult.Failed(commandResult.FailureReason, commandResult);
        }

        _invariants.RepairAll(candidate);

        bool committed = StrategicBattleActiveContextStore.TryCommitAndConsume(
            context.ContextId,
            context.Session.SessionId,
            context.Snapshot.SnapshotId,
            () => _saveService.Save(candidate, savePath),
            () => publishCandidate?.Invoke(candidate),
            out _,
            out System.Exception persistenceFailure);
        if (!committed)
        {
            string failureReason = persistenceFailure == null
                ? StrategicFailureReasons.ActiveBattleContextMismatch
                : StrategicFailureReasons.StrategicPersistenceFailed;
            GameLog.Warn(
                nameof(StrategicBattleSettlementCommitService),
                $"StrategicBattleSettlementCommitFailed context={context.ContextId} session={context.Session.SessionId} snapshot={context.Snapshot.SnapshotId} reason={failureReason} exception={persistenceFailure?.GetType().Name ?? ""}");
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
