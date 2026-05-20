using Godot;
using Rpg.Application.Battle;
using Rpg.Application.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Infrastructure.Scenes;

public sealed class SceneTransitionRouter
{
    private readonly ISceneTransitionGateway _gateway;

    public SceneTransitionRouter(ISceneTransitionGateway gateway)
    {
        _gateway = gateway;
    }

    public bool IsTransitioning { get; private set; }

    public SceneTransitionResult EnterSiteDetail(SceneTransitionSiteVisitRequest request)
    {
        if (!TryBeginTransition(out SceneTransitionResult busyResult))
        {
            return busyResult;
        }

        if (request == null || string.IsNullOrWhiteSpace(request.SiteId) || string.IsNullOrWhiteSpace(request.TargetScenePath))
        {
            IsTransitioning = false;
            return SceneTransitionResult.Fail("invalid_site_transition_request");
        }

        StrategicWorldRuntime.BeginSiteVisit(request.SiteId, request.ReturnScenePath, request.ArmyId);
        Error error = _gateway.ChangeSceneToFile(request.TargetScenePath);
        if (error == Error.Ok)
        {
            GameLog.Info(nameof(SceneTransitionRouter), $"SiteDetailTransitionStarted site={request.SiteId} scene={request.TargetScenePath}");
            return SceneTransitionResult.Ok();
        }

        StrategicWorldRuntime.ClearPendingSiteVisit();
        IsTransitioning = false;
        GameLog.Warn(nameof(SceneTransitionRouter), $"SiteDetailTransitionFailed site={request.SiteId} scene={request.TargetScenePath} error={error}");
        return SceneTransitionResult.Fail($"scene_change_failed:{error}", error);
    }

    public SceneTransitionResult EnterBattlePreparation(SceneTransitionBattleRequest request)
    {
        if (!TryBeginTransition(out SceneTransitionResult busyResult))
        {
            return busyResult;
        }

        BattleStartRequest battleRequest = request?.Request;
        if (battleRequest == null || string.IsNullOrWhiteSpace(battleRequest.SiteScenePath))
        {
            IsTransitioning = false;
            return SceneTransitionResult.Fail("invalid_battle_transition_request");
        }

        BattleSessionHandoff.BeginBattle(battleRequest);
        Error error = _gateway.ChangeSceneToFile(battleRequest.SiteScenePath);
        if (error == Error.Ok)
        {
            request.OnSuccess?.Invoke();
            GameLog.Info(nameof(SceneTransitionRouter), $"BattleTransitionStarted request={battleRequest.RequestId} scene={battleRequest.SiteScenePath}");
            return SceneTransitionResult.Ok();
        }

        BattleSessionHandoff.CancelBattle();
        IsTransitioning = false;
        string failureReason = $"scene_change_failed:{error}";
        request.RollbackOnFailure?.Invoke(failureReason);
        GameLog.Warn(nameof(SceneTransitionRouter), $"BattleTransitionFailed request={battleRequest.RequestId} scene={battleRequest.SiteScenePath} error={error}");
        return SceneTransitionResult.Fail(failureReason, error);
    }

    public SceneTransitionResult ReturnFromSite(SceneTransitionReturnRequest request)
    {
        if (!TryBeginTransition(out SceneTransitionResult busyResult))
        {
            return busyResult;
        }

        if (request == null || string.IsNullOrWhiteSpace(request.TargetScenePath))
        {
            IsTransitioning = false;
            return SceneTransitionResult.Fail("invalid_return_transition_request");
        }

        if (request.MarkWorldResume)
        {
            StrategicWorldRuntime.MarkWorldResumeAfterSiteReturn();
        }

        Error error = _gateway.ChangeSceneToFile(request.TargetScenePath);
        if (error == Error.Ok)
        {
            GameLog.Info(nameof(SceneTransitionRouter), $"ReturnTransitionStarted scene={request.TargetScenePath}");
            return SceneTransitionResult.Ok();
        }

        IsTransitioning = false;
        GameLog.Warn(nameof(SceneTransitionRouter), $"ReturnTransitionFailed scene={request.TargetScenePath} error={error}");
        return SceneTransitionResult.Fail($"scene_change_failed:{error}", error);
    }

    private bool TryBeginTransition(out SceneTransitionResult result)
    {
        if (IsTransitioning)
        {
            result = SceneTransitionResult.Fail("transition_in_progress");
            return false;
        }

        IsTransitioning = true;
        result = null;
        return true;
    }
}
