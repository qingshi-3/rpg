using System;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.StrategicBattleBridge;
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
        Error error = _gateway.ChangeSceneToFile(
            request.TargetScenePath,
            () => CompleteTransition(
                "SiteDetail",
                request.TargetScenePath,
                $"SiteDetailTransitionEntered site={request.SiteId} scene={request.TargetScenePath}",
                request.OnEntered));
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

        if (request?.ActiveContext != null)
        {
            return EnterStrategicBattlePreparation(request);
        }

        BattleStartRequest battleRequest = request?.Request;
        if (battleRequest == null || string.IsNullOrWhiteSpace(battleRequest.SiteScenePath))
        {
            IsTransitioning = false;
            return SceneTransitionResult.Fail("invalid_battle_transition_request");
        }

        BattleSessionHandoff.BeginBattle(battleRequest);
        Error error = _gateway.ChangeSceneToFile(
            battleRequest.SiteScenePath,
            () => CompleteTransition(
                "Battle",
                battleRequest.SiteScenePath,
                $"BattleTransitionEntered request={battleRequest.RequestId} scene={battleRequest.SiteScenePath}",
                request.OnSuccess));
        if (error == Error.Ok)
        {
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

    private SceneTransitionResult EnterStrategicBattlePreparation(SceneTransitionBattleRequest request)
    {
        StrategicBattleActiveContext activeContext = request.ActiveContext;
        string scenePath = string.IsNullOrWhiteSpace(activeContext.ScenePath)
            ? activeContext.PreparationDraft?.SiteScenePath ?? ""
            : activeContext.ScenePath;
        if (string.IsNullOrWhiteSpace(scenePath))
        {
            IsTransitioning = false;
            return SceneTransitionResult.Fail("invalid_strategic_battle_transition_request");
        }

        // Strategic battles publish Bridge Active Context for the destination scene.
        // The legacy BattleSessionHandoff branch below remains only for non-Strategic compatibility.
        if (BattleSessionHandoff.HasActiveLaunch)
        {
            BattleSessionHandoff.CancelBattle();
            GameLog.Warn(
                nameof(SceneTransitionRouter),
                $"StaleLegacyBattleHandoffClearedForStrategicTransition context={activeContext.ContextId}");
        }

        if (!StrategicBattleActiveContextStore.TryBegin(activeContext, out string publicationFailureReason))
        {
            IsTransitioning = false;
            GameLog.Warn(
                nameof(SceneTransitionRouter),
                $"StrategicBattleTransitionPublicationRejected context={activeContext.ContextId} session={activeContext.Session?.SessionId ?? ""} snapshot={activeContext.Snapshot?.SnapshotId ?? ""} reason={publicationFailureReason}");
            return SceneTransitionResult.Fail(publicationFailureReason);
        }

        Error error = _gateway.ChangeSceneToFile(
            scenePath,
            () => CompleteTransition(
                "StrategicBattle",
                scenePath,
                $"StrategicBattleTransitionEntered context={activeContext.ContextId} scene={scenePath}",
                request.OnSuccess));
        if (error == Error.Ok)
        {
            GameLog.Info(nameof(SceneTransitionRouter), $"StrategicBattleTransitionStarted context={activeContext.ContextId} scene={scenePath}");
            return SceneTransitionResult.Ok();
        }

        bool clearedMatchingContext = StrategicBattleActiveContextStore.TryClear(
            activeContext.ContextId,
            activeContext.Session?.SessionId,
            activeContext.Snapshot?.SnapshotId,
            $"scene_change_failed:{error}");
        IsTransitioning = false;
        string failureReason = $"scene_change_failed:{error}";
        if (clearedMatchingContext)
        {
            request.RollbackOnFailure?.Invoke(failureReason);
        }
        GameLog.Warn(nameof(SceneTransitionRouter), $"StrategicBattleTransitionFailed context={activeContext.ContextId} scene={scenePath} error={error}");
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

        Error error = _gateway.ChangeSceneToFile(
            request.TargetScenePath,
            () => CompleteTransition(
                "Return",
                request.TargetScenePath,
                $"ReturnTransitionEntered scene={request.TargetScenePath}",
                request.OnEntered));
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

    private void CompleteTransition(
        string transitionKind,
        string scenePath,
        string enteredLogMessage,
        Action onEntered = null)
    {
        // The authoritative "entered" boundary is SceneTree.SceneChanged. Until this callback runs,
        // current_scene may still be null and rollback/duplicate-transition guards must stay active.
        IsTransitioning = false;
        GameLog.Info(nameof(SceneTransitionRouter), enteredLogMessage);

        try
        {
            onEntered?.Invoke();
        }
        catch (Exception exception)
        {
            string reason = string.IsNullOrWhiteSpace(exception.Message)
                ? exception.GetType().Name
                : exception.Message;
            GameLog.Warn(
                nameof(SceneTransitionRouter),
                $"SceneTransitionEnteredCallbackFailed kind={transitionKind} scene={scenePath} reason={reason}");
        }
    }
}
