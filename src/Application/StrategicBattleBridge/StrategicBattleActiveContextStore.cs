using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Reports;
using Rpg.Application.Battle.Settlement;
using Rpg.Application.Battle.Snapshots;
using Rpg.Domain.StrategicManagement;
using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle;

namespace Rpg.Application.StrategicBattleBridge;

public static class StrategicBattleActiveContextStore
{
    public const string CasMismatchReason = "active_battle_context_cas_mismatch";
    public const string ResultEnvelopeConflictReason = "strategic_battle_result_envelope_conflict";
    public const string CommitReservationConflictReason = "active_battle_context_commit_in_progress";
    public const string AcceptedResultTokenRequiredReason = "active_battle_context_result_token_required";
    public const string AcceptedResultCommitRequiredReason = "active_battle_context_result_commit_required";

    private static readonly object Gate = new();
    private static StrategicBattleActiveContext _activeContext;
    private static StrategicBattleActiveContextToken _activeToken;
    private static BattleStartSnapshot _activeSnapshot;
    private static CommitReservation _commitReservation;
    private static long _nextRevision;
    private static long _nextReservationId;

    public static bool HasActiveContext
    {
        get
        {
            lock (Gate)
            {
                return _activeContext != null;
            }
        }
    }

    public static bool TryBegin(
        StrategicBattleActiveContext context,
        out StrategicBattleActiveContextToken acceptedToken,
        out string failureReason)
    {
        return TryBegin(context, null, out acceptedToken, out failureReason);
    }

    public static bool TryBegin(
        StrategicBattleActiveContext context,
        StrategicBattleActiveContextToken expectedToken,
        out StrategicBattleActiveContextToken acceptedToken,
        out string failureReason)
    {
        acceptedToken = null;
        failureReason = GetIdentityFailureReason(context);
        if (!string.IsNullOrWhiteSpace(failureReason))
        {
            return false;
        }

        lock (Gate)
        {
            if (_activeContext != null)
            {
                if (ReferenceEquals(_activeContext, context) &&
                    expectedToken != null &&
                    TokensEqual(_activeToken, expectedToken) &&
                    ActiveStateMatchesTokenLocked())
                {
                    acceptedToken = _activeToken;
                    return true;
                }

                failureReason = "active_battle_context_conflict";
                LogCasRejectedLocked("begin", expectedToken, failureReason);
                return false;
            }

            if (expectedToken != null || context.ResultEnvelope != null || context.ResultConsumed)
            {
                failureReason = "invalid_active_battle_context_initial_state";
                LogCasRejectedLocked("begin", expectedToken, failureReason);
                return false;
            }

            if (_nextRevision == long.MaxValue)
            {
                failureReason = "active_battle_context_revision_exhausted";
                LogCasRejectedLocked("begin", expectedToken, failureReason);
                return false;
            }

            _activeContext = context;
            _activeSnapshot = context.Snapshot;
            _activeToken = new StrategicBattleActiveContextToken(
                context.ContextId,
                context.Session.SessionId,
                context.Snapshot.SnapshotId,
                ++_nextRevision);
            _commitReservation = null;
            acceptedToken = _activeToken;
            GameLog.Info(
                nameof(StrategicBattleActiveContextStore),
                $"StrategicBattleActiveContextBegin {FormatToken(_activeToken)} expedition={context.Session.ExpeditionId} target={context.Session.TargetLocationId}");
            return true;
        }
    }

    public static bool TryPeek(
        out StrategicBattleActiveContext context,
        out StrategicBattleActiveContextToken token)
    {
        lock (Gate)
        {
            if (_activeContext == null || !ActiveStateMatchesTokenLocked())
            {
                context = null;
                token = null;
                return false;
            }

            context = _activeContext;
            token = _activeToken;
            return true;
        }
    }

    public static bool TryPeek(
        StrategicBattleActiveContextToken expectedToken,
        out StrategicBattleActiveContext context)
    {
        lock (Gate)
        {
            if (!TryMatchExpectedLocked("peek", expectedToken, null, out _))
            {
                context = null;
                return false;
            }

            context = _activeContext;
            return true;
        }
    }

    public static bool TryClear(
        StrategicBattleActiveContextToken expectedToken,
        string reason,
        out string failureReason)
    {
        lock (Gate)
        {
            if (!TryMatchExpectedLocked("clear", expectedToken, null, out failureReason) ||
                !TryRequireNoReservationLocked("clear", expectedToken, out failureReason))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(_activeToken.ResultId) || _activeContext.ResultEnvelope != null)
            {
                failureReason = AcceptedResultCommitRequiredReason;
                LogCasRejectedLocked("clear", expectedToken, failureReason);
                return false;
            }

            LogClearLocked(reason);
            ClearLocked();
            return true;
        }
    }

    public static bool TryAdvanceSnapshot(
        StrategicBattleActiveContextToken expectedToken,
        StrategicBattleActiveContext context,
        BattleStartSnapshot finalSnapshot,
        BattleStartRequest compatibilityRequest,
        string finalizedDraftId,
        long finalizedDraftRevision,
        IReadOnlyCollection<string> deployedParticipantIds,
        out StrategicBattleActiveContextToken advancedToken,
        out string failureReason)
    {
        advancedToken = null;
        if (finalSnapshot == null ||
            string.IsNullOrWhiteSpace(finalSnapshot.SnapshotId) ||
            compatibilityRequest == null ||
            string.IsNullOrWhiteSpace(finalizedDraftId) ||
            finalizedDraftRevision <= 0 ||
            deployedParticipantIds == null)
        {
            failureReason = "invalid_active_battle_final_snapshot";
            return false;
        }

        lock (Gate)
        {
            if (!TryMatchExpectedLocked("advance_snapshot", expectedToken, context, out failureReason) ||
                !TryRequireNoReservationLocked("advance_snapshot", expectedToken, out failureReason))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(_activeToken.ResultId) || _activeContext.ResultEnvelope != null)
            {
                failureReason = ResultEnvelopeConflictReason;
                LogCasRejectedLocked("advance_snapshot", expectedToken, failureReason);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(context.FinalizedDraftId) ||
                context.FinalizedDraftRevision > 0)
            {
                failureReason = StrategicBattleDraftSnapshotCompiler.FinalSnapshotAlreadyCompiledReason;
                LogCasRejectedLocked("advance_snapshot", expectedToken, failureReason);
                return false;
            }

            IReadOnlyList<StrategicBattleParticipantReference> participants = context.Session?.Participants;
            if (participants == null || participants.Any(participant => participant == null))
            {
                failureReason = "invalid_active_battle_final_snapshot_participants";
                LogCasRejectedLocked("advance_snapshot", expectedToken, failureReason);
                return false;
            }

            if (_nextRevision == long.MaxValue)
            {
                failureReason = "active_battle_context_revision_exhausted";
                LogCasRejectedLocked("advance_snapshot", expectedToken, failureReason);
                return false;
            }

            HashSet<string> deployed = new(deployedParticipantIds, StringComparer.Ordinal);
            foreach (StrategicBattleParticipantReference participant in participants)
            {
                participant.Role = deployed.Contains(participant.ParticipantId ?? "")
                    ? StrategicBattleParticipantRole.Deployed
                    : StrategicBattleParticipantRole.Reserve;
            }

            // Snapshot, compatibility projection, frozen roles, and lineage become
            // visible together under the predecessor revision's CAS boundary.
            context.Snapshot = finalSnapshot;
            context.CompatibilityRequest = compatibilityRequest;
            context.FinalizedDraftId = finalizedDraftId;
            context.FinalizedDraftRevision = finalizedDraftRevision;
            _activeSnapshot = finalSnapshot;
            _activeToken = new StrategicBattleActiveContextToken(
                expectedToken.ContextId,
                expectedToken.SessionId,
                finalSnapshot.SnapshotId,
                ++_nextRevision);
            advancedToken = _activeToken;
            GameLog.Info(
                nameof(StrategicBattleActiveContextStore),
                $"StrategicBattleActiveContextSnapshotAdvanced expectedRevision={expectedToken.Revision} {FormatToken(_activeToken)} draft={finalizedDraftId} draftRevision={finalizedDraftRevision}");
            return true;
        }
    }

    public static bool TryPublishResultEnvelope(
        StrategicBattleActiveContextToken expectedToken,
        StrategicBattleActiveContext context,
        BattleRuntimeSessionResult runtimeResult,
        SettlementPlan settlementPlan,
        BattleReportRecord report,
        out StrategicBattleResultEnvelope acceptedEnvelope,
        out StrategicBattleActiveContextToken acceptedToken,
        out string failureReason)
    {
        return TryPublishResultEnvelope(
            expectedToken,
            context,
            runtimeResult,
            settlementPlan,
            report,
            null,
            out acceptedEnvelope,
            out acceptedToken,
            out failureReason);
    }

    public static bool TryPublishResultEnvelope(
        StrategicBattleActiveContextToken expectedToken,
        StrategicBattleActiveContext context,
        BattleRuntimeSessionResult runtimeResult,
        SettlementPlan settlementPlan,
        BattleReportRecord report,
        BattleResult compatibilityResult,
        out StrategicBattleResultEnvelope acceptedEnvelope,
        out StrategicBattleActiveContextToken acceptedToken,
        out string failureReason)
    {
        acceptedEnvelope = null;
        acceptedToken = null;
        if (expectedToken == null)
        {
            failureReason = CasMismatchReason;
            lock (Gate)
            {
                LogCasRejectedLocked("publish_result", expectedToken, failureReason);
            }
            return false;
        }

        StrategicBattleResultEnvelope candidate = StrategicBattleResultEnvelope.Create(
            expectedToken.SessionId,
            expectedToken.SnapshotId,
            runtimeResult,
            settlementPlan,
            report);

        lock (Gate)
        {
            StrategicBattleResultEnvelope currentEnvelope = _activeContext?.ResultEnvelope;
            if (currentEnvelope != null)
            {
                bool exactContextAndState = ReferenceEquals(_activeContext, context) &&
                                            ActiveStateMatchesTokenLocked();
                bool exactCurrentOrPredecessor = exactContextAndState &&
                    (TokensEqual(expectedToken, _activeToken) ||
                     IsImmediateResultPredecessor(expectedToken, _activeToken));
                if (exactCurrentOrPredecessor &&
                    string.Equals(candidate.ResultId, currentEnvelope.ResultId, StringComparison.Ordinal))
                {
                    acceptedEnvelope = currentEnvelope;
                    acceptedToken = _activeToken;
                    failureReason = "";
                    GameLog.Info(
                        nameof(StrategicBattleActiveContextStore),
                        $"StrategicBattleResultEnvelopeDuplicateReturned {FormatToken(_activeToken)}");
                    return true;
                }

                failureReason = exactCurrentOrPredecessor
                    ? ResultEnvelopeConflictReason
                    : CasMismatchReason;
                LogCasRejectedLocked("publish_result", expectedToken, failureReason);
                return false;
            }

            if (!TryMatchExpectedLocked("publish_result", expectedToken, context, out failureReason) ||
                !TryRequireNoReservationLocked("publish_result", expectedToken, out failureReason))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(expectedToken.ResultId))
            {
                failureReason = ResultEnvelopeConflictReason;
                LogCasRejectedLocked("publish_result", expectedToken, failureReason);
                return false;
            }

            failureReason = StrategicBattleBridgeService.GetResultEnvelopeFailureReason(context, candidate);
            if (!string.IsNullOrWhiteSpace(failureReason) || !candidate.HasIntactIdentity())
            {
                failureReason = string.IsNullOrWhiteSpace(failureReason)
                    ? ResultEnvelopeConflictReason
                    : failureReason;
                LogCasRejectedLocked("publish_result", expectedToken, failureReason);
                return false;
            }

            if (_nextRevision == long.MaxValue)
            {
                failureReason = "active_battle_context_revision_exhausted";
                LogCasRejectedLocked("publish_result", expectedToken, failureReason);
                return false;
            }

            if (!context.TryAcceptResultEnvelope(candidate))
            {
                failureReason = ResultEnvelopeConflictReason;
                LogCasRejectedLocked("publish_result", expectedToken, failureReason);
                return false;
            }

            context.CompatibilityResult = compatibilityResult;
            _activeToken = new StrategicBattleActiveContextToken(
                expectedToken.ContextId,
                expectedToken.SessionId,
                expectedToken.SnapshotId,
                ++_nextRevision,
                candidate.ResultId);
            acceptedEnvelope = candidate;
            acceptedToken = _activeToken;
            GameLog.Info(
                nameof(StrategicBattleActiveContextStore),
                $"StrategicBattleResultEnvelopePublished expectedRevision={expectedToken.Revision} {FormatToken(_activeToken)} report={candidate.Report.ReportId}");
            return true;
        }
    }

    public static bool TryCommitAndConsume(
        StrategicBattleActiveContextToken expectedResultToken,
        StrategicBattleActiveContext context,
        Action persistCandidate,
        Action publishCandidate,
        out StrategicBattleActiveContext consumed,
        out Exception callbackFailure,
        out string failureReason)
    {
        consumed = null;
        callbackFailure = null;
        CommitReservation reservation;
        lock (Gate)
        {
            if (!TryMatchExpectedLocked(
                    "reserve_commit",
                    expectedResultToken,
                    context,
                    out failureReason) ||
                !TryRequireNoReservationLocked("reserve_commit", expectedResultToken, out failureReason))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(expectedResultToken.ResultId) ||
                !string.Equals(
                    context.ResultEnvelope?.ResultId ?? "",
                    expectedResultToken.ResultId,
                    StringComparison.Ordinal))
            {
                failureReason = AcceptedResultTokenRequiredReason;
                LogCasRejectedLocked("reserve_commit", expectedResultToken, failureReason);
                return false;
            }

            if (persistCandidate == null || publishCandidate == null)
            {
                failureReason = "active_battle_context_commit_callback_missing";
                return false;
            }

            reservation = new CommitReservation(
                ++_nextReservationId,
                context,
                expectedResultToken);
            _commitReservation = reservation;
        }

        try
        {
            // External persistence and publication are intentionally outside Gate.
            persistCandidate();
        }
        catch (Exception exception)
        {
            callbackFailure = exception;
            failureReason = "active_battle_context_persistence_callback_failed";
            ReleaseReservation(reservation, failureReason);
            return false;
        }

        try
        {
            publishCandidate();
        }
        catch (Exception exception)
        {
            callbackFailure = exception;
            failureReason = "active_battle_context_publication_callback_failed";
            ReleaseReservation(reservation, failureReason);
            return false;
        }

        lock (Gate)
        {
            if (!ReferenceEquals(_commitReservation, reservation) ||
                !ReferenceEquals(_activeContext, context) ||
                !TokensEqual(_activeToken, expectedResultToken) ||
                !ActiveStateMatchesTokenLocked())
            {
                if (ReferenceEquals(_commitReservation, reservation))
                {
                    _commitReservation = null;
                }
                failureReason = CasMismatchReason;
                LogCasRejectedLocked("commit_consume", expectedResultToken, failureReason);
                return false;
            }

            consumed = _activeContext;
            consumed.ResultConsumed = true;
            LogClearLocked("result_committed_and_consumed");
            ClearLocked();
            failureReason = "";
            return true;
        }
    }

    internal static bool TryConsumeCommittedReplay(
        StrategicBattleActiveContextToken expectedResultToken,
        StrategicBattleActiveContext context,
        out StrategicBattleActiveContext consumed,
        out string failureReason)
    {
        lock (Gate)
        {
            consumed = null;
            if (!TryMatchExpectedLocked(
                    "consume_committed_replay",
                    expectedResultToken,
                    context,
                    out failureReason) ||
                !TryRequireNoReservationLocked(
                    "consume_committed_replay",
                    expectedResultToken,
                    out failureReason))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(expectedResultToken.ResultId) ||
                !string.Equals(
                    context.ResultEnvelope?.ResultId ?? "",
                    expectedResultToken.ResultId,
                    StringComparison.Ordinal))
            {
                failureReason = AcceptedResultTokenRequiredReason;
                LogCasRejectedLocked("consume_committed_replay", expectedResultToken, failureReason);
                return false;
            }

            // The caller may use this boundary only after exact durable replay
            // proves that save and live publication already precede consumption.
            consumed = _activeContext;
            consumed.ResultConsumed = true;
            LogClearLocked("committed_replay_consumed");
            ClearLocked();
            failureReason = "";
            return true;
        }
    }

    private static bool TryMatchExpectedLocked(
        string operation,
        StrategicBattleActiveContextToken expectedToken,
        StrategicBattleActiveContext expectedContext,
        out string failureReason)
    {
        if (_activeContext == null ||
            expectedToken == null ||
            !TokensEqual(_activeToken, expectedToken) ||
            expectedContext != null && !ReferenceEquals(_activeContext, expectedContext) ||
            !ActiveStateMatchesTokenLocked())
        {
            failureReason = CasMismatchReason;
            LogCasRejectedLocked(operation, expectedToken, failureReason);
            return false;
        }

        failureReason = "";
        return true;
    }

    private static bool TryRequireNoReservationLocked(
        string operation,
        StrategicBattleActiveContextToken expectedToken,
        out string failureReason)
    {
        if (_commitReservation == null)
        {
            failureReason = "";
            return true;
        }

        failureReason = CommitReservationConflictReason;
        LogCasRejectedLocked(operation, expectedToken, failureReason);
        return false;
    }

    private static bool ActiveStateMatchesTokenLocked()
    {
        if (_activeContext == null || _activeToken == null || _activeSnapshot == null)
        {
            return false;
        }

        StrategicBattleResultEnvelope envelope = _activeContext.ResultEnvelope;
        return ReferenceEquals(_activeContext.Snapshot, _activeSnapshot) &&
               _activeToken.Revision == _nextRevision &&
               string.Equals(_activeContext.ContextId ?? "", _activeToken.ContextId, StringComparison.Ordinal) &&
               string.Equals(_activeContext.Session?.SessionId ?? "", _activeToken.SessionId, StringComparison.Ordinal) &&
               string.Equals(_activeContext.Snapshot?.SnapshotId ?? "", _activeToken.SnapshotId, StringComparison.Ordinal) &&
               string.Equals(envelope?.ResultId ?? "", _activeToken.ResultId, StringComparison.Ordinal) &&
               (envelope == null || envelope.HasIntactIdentity()) &&
               !_activeContext.ResultConsumed;
    }

    private static bool IsImmediateResultPredecessor(
        StrategicBattleActiveContextToken expectedToken,
        StrategicBattleActiveContextToken currentToken)
    {
        return expectedToken != null &&
               currentToken != null &&
               string.IsNullOrWhiteSpace(expectedToken.ResultId) &&
               !string.IsNullOrWhiteSpace(currentToken.ResultId) &&
               expectedToken.Revision < long.MaxValue &&
               expectedToken.Revision + 1 == currentToken.Revision &&
               string.Equals(expectedToken.ContextId, currentToken.ContextId, StringComparison.Ordinal) &&
               string.Equals(expectedToken.SessionId, currentToken.SessionId, StringComparison.Ordinal) &&
               string.Equals(expectedToken.SnapshotId, currentToken.SnapshotId, StringComparison.Ordinal);
    }

    private static bool TokensEqual(
        StrategicBattleActiveContextToken left,
        StrategicBattleActiveContextToken right) => left?.Equals(right) == true;

    private static string GetIdentityFailureReason(StrategicBattleActiveContext context)
    {
        return context == null ||
               string.IsNullOrWhiteSpace(context.ContextId) ||
               string.IsNullOrWhiteSpace(context.Session?.SessionId) ||
               string.IsNullOrWhiteSpace(context.Snapshot?.SnapshotId)
            ? "invalid_active_battle_context_identity"
            : "";
    }

    private static void ReleaseReservation(CommitReservation reservation, string reason)
    {
        lock (Gate)
        {
            if (ReferenceEquals(_commitReservation, reservation))
            {
                _commitReservation = null;
                GameLog.Warn(
                    nameof(StrategicBattleActiveContextStore),
                    $"StrategicBattleActiveContextCommitReleased reservation={reservation.ReservationId} {FormatToken(reservation.Token)} reason={reason ?? ""}");
            }
        }
    }

    private static void ClearLocked()
    {
        _activeContext = null;
        _activeToken = null;
        _activeSnapshot = null;
        _commitReservation = null;
    }

    private static void LogClearLocked(string reason)
    {
        GameLog.Info(
            nameof(StrategicBattleActiveContextStore),
            $"StrategicBattleActiveContextClear {FormatToken(_activeToken)} reason={reason ?? ""}");
    }

    private static void LogCasRejectedLocked(
        string operation,
        StrategicBattleActiveContextToken expectedToken,
        string reason)
    {
        GameLog.Warn(
            nameof(StrategicBattleActiveContextStore),
            $"StrategicBattleActiveContextCasRejected operation={operation ?? ""} expected=[{FormatToken(expectedToken)}] current=[{FormatToken(_activeToken)}] reason={reason ?? ""}");
    }

    private static string FormatToken(StrategicBattleActiveContextToken token) =>
        token?.ToString() ?? "context= session= snapshot= revision=0 result=";

    private sealed class CommitReservation
    {
        public CommitReservation(
            long reservationId,
            StrategicBattleActiveContext context,
            StrategicBattleActiveContextToken token)
        {
            ReservationId = reservationId;
            Context = context;
            Token = token;
        }

        public long ReservationId { get; }
        public StrategicBattleActiveContext Context { get; }
        public StrategicBattleActiveContextToken Token { get; }
    }
}
