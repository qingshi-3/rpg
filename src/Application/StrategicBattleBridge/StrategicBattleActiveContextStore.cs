using Rpg.Infrastructure.Logging;

namespace Rpg.Application.StrategicBattleBridge;

public static class StrategicBattleActiveContextStore
{
    private static readonly object Gate = new();
    private static StrategicBattleActiveContext _activeContext;

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

    public static bool TryBegin(StrategicBattleActiveContext context, out string failureReason)
    {
        failureReason = GetIdentityFailureReason(context);
        if (!string.IsNullOrWhiteSpace(failureReason))
        {
            return false;
        }

        lock (Gate)
        {
            if (_activeContext != null)
            {
                bool samePublishedObject = IdentityMatches(
                                               _activeContext,
                                               context.ContextId,
                                               context.Session.SessionId,
                                               context.Snapshot.SnapshotId) &&
                                           ReferenceEquals(_activeContext, context);
                if (!samePublishedObject)
                {
                    failureReason = "active_battle_context_conflict";
                    return false;
                }

                return true;
            }

            _activeContext = context;
            GameLog.Info(
                nameof(StrategicBattleActiveContextStore),
                $"StrategicBattleActiveContextBegin context={context.ContextId} session={context.Session.SessionId} snapshot={context.Snapshot.SnapshotId} expedition={context.Session.ExpeditionId} target={context.Session.TargetLocationId}");
            return true;
        }
    }

    public static bool TryPeek(out StrategicBattleActiveContext context)
    {
        lock (Gate)
        {
            context = _activeContext;
            return context != null;
        }
    }

    public static bool TryPeek(
        string contextId,
        string sessionId,
        string snapshotId,
        out StrategicBattleActiveContext context)
    {
        lock (Gate)
        {
            context = IdentityMatches(_activeContext, contextId, sessionId, snapshotId)
                ? _activeContext
                : null;
            return context != null;
        }
    }

    public static bool TryClear(string contextId, string sessionId, string snapshotId, string reason = "")
    {
        lock (Gate)
        {
            if (!IdentityMatches(_activeContext, contextId, sessionId, snapshotId))
            {
                return false;
            }

            LogClear(_activeContext, reason);
            _activeContext = null;
            return true;
        }
    }

    public static bool TryCommitAndConsume(
        string contextId,
        string sessionId,
        string snapshotId,
        System.Action persistCandidate,
        System.Action publishCandidate,
        out StrategicBattleActiveContext consumed,
        out System.Exception persistenceFailure)
    {
        lock (Gate)
        {
            consumed = null;
            persistenceFailure = null;
            if (!IdentityMatches(_activeContext, contextId, sessionId, snapshotId))
            {
                return false;
            }

            try
            {
                // Keep identity stable across durable save, live publication, and final consumption.
                persistCandidate?.Invoke();
            }
            catch (System.Exception exception)
            {
                persistenceFailure = exception;
                return false;
            }

            publishCandidate?.Invoke();
            consumed = _activeContext;
            consumed.ResultConsumed = true;
            LogClear(consumed, "result_committed_and_consumed");
            _activeContext = null;
            return true;
        }
    }

    private static bool IdentityMatches(
        StrategicBattleActiveContext context,
        string contextId,
        string sessionId,
        string snapshotId)
    {
        return context != null &&
               string.Equals(context.ContextId ?? "", contextId ?? "", System.StringComparison.Ordinal) &&
               string.Equals(context.Session?.SessionId ?? "", sessionId ?? "", System.StringComparison.Ordinal) &&
               string.Equals(context.Snapshot?.SnapshotId ?? "", snapshotId ?? "", System.StringComparison.Ordinal);
    }

    private static string GetIdentityFailureReason(StrategicBattleActiveContext context)
    {
        return context == null ||
               string.IsNullOrWhiteSpace(context.ContextId) ||
               string.IsNullOrWhiteSpace(context.Session?.SessionId) ||
               string.IsNullOrWhiteSpace(context.Snapshot?.SnapshotId)
            ? "invalid_active_battle_context_identity"
            : "";
    }

    private static void LogClear(StrategicBattleActiveContext context, string reason)
    {
        GameLog.Info(
            nameof(StrategicBattleActiveContextStore),
            $"StrategicBattleActiveContextClear context={context?.ContextId ?? ""} session={context?.Session?.SessionId ?? ""} snapshot={context?.Snapshot?.SnapshotId ?? ""} reason={reason ?? ""}");
    }
}
