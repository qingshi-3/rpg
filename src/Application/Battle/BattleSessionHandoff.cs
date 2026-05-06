namespace Rpg.Application.Battle;

public static class BattleSessionHandoff
{
    private static BattleStartRequest _activeRequest;
    private static BattleSessionResult _lastResult;
    private static BattleStartRequest _lastRequest;
    private static BattleResult _lastBattleResult;

    public static bool HasActiveLaunch => _activeRequest != null;

    public static void BeginBattle(string contextId, string encounterId, string returnScenePath)
    {
        BeginBattle(new BattleStartRequest
        {
            ContextId = contextId ?? "",
            EncounterId = encounterId ?? "",
            ReturnScenePath = returnScenePath ?? "",
            BattleKind = BattleKind.Unknown
        });
    }

    public static void BeginBattle(BattleStartRequest request)
    {
        _activeRequest = request ?? new BattleStartRequest();
        _lastResult = null;
        _lastRequest = null;
        _lastBattleResult = null;
    }

    public static void CancelBattle()
    {
        _activeRequest = null;
    }

    public static bool TryPeekActiveRequest(out BattleStartRequest request)
    {
        request = _activeRequest;
        return request != null;
    }

    public static BattleSessionResult CompleteBattle(BattleOutcome outcome)
    {
        if (_activeRequest == null)
        {
            return null;
        }

        BattleStartRequest request = _activeRequest;
        _lastRequest = request;
        _lastResult = new BattleSessionResult(
            request.ContextId,
            request.EncounterId,
            request.ReturnScenePath,
            outcome);
        _lastBattleResult = BuildBattleResult(request, outcome);
        _activeRequest = null;
        return _lastResult;
    }

    public static bool TryConsumeLastResult(out BattleSessionResult result)
    {
        result = _lastResult;
        _lastResult = null;
        return result != null;
    }

    public static bool TryConsumeLastBattleResult(out BattleStartRequest request, out BattleResult result)
    {
        request = _lastRequest;
        result = _lastBattleResult;
        _lastRequest = null;
        _lastBattleResult = null;
        if (result != null)
        {
            _lastResult = null;
        }

        return request != null && result != null;
    }

    private static BattleResult BuildBattleResult(BattleStartRequest request, BattleOutcome outcome)
    {
        var result = new BattleResult
        {
            RequestId = request.RequestId,
            ContextId = request.ContextId,
            BattleKind = request.BattleKind,
            Outcome = outcome
        };

        foreach (string objectiveId in request.ObjectiveIds)
        {
            result.ObjectiveResults.Add(new BattleObjectiveResult
            {
                ObjectiveId = objectiveId,
                State = outcome == BattleOutcome.Victory
                    ? BattleObjectiveState.Succeeded
                    : BattleObjectiveState.Failed
            });
        }

        return result;
    }
}
