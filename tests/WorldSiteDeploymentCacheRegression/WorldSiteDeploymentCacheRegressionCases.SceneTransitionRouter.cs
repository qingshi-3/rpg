using Godot;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Application.StrategicBattleBridge;
using Rpg.Application.World;
using Rpg.Infrastructure.Scenes;

internal static partial class WorldSiteDeploymentCacheRegressionCases
{
internal static void SceneTransitionRouterBeginsAndClearsSiteVisitOnSceneFailure()
{
    StrategicWorldRuntime.ClearPendingSiteVisit();
    FakeSceneTransitionGateway gateway = new(Error.Failed);
    SceneTransitionRouter router = new(gateway);

    SceneTransitionResult result = router.EnterSiteDetail(new SceneTransitionSiteVisitRequest
    {
        SiteId = "site_under_test",
        TargetScenePath = "res://missing_site_scene.tscn",
        ReturnScenePath = "res://scenes/world/StrategicWorldRoot.tscn",
        ArmyId = "army_under_test"
    });

    AssertTrue(!result.Success, "failed site transition should report failure");
    AssertEqual(Error.Failed, result.Error, "site transition error");
    AssertEqual(1, gateway.ChangeCalls, "site transition should attempt one scene change");
    AssertEqual("res://missing_site_scene.tscn", gateway.LastScenePath, "site transition target scene");
    AssertTrue(
        !StrategicWorldRuntime.TryConsumePendingSiteVisit(out _, out _, out _),
        "failed site transition should clear pending site visit");
    AssertTrue(!router.IsTransitioning, "failed site transition should release busy state");
}

internal static void SceneTransitionRouterBeginsBattleAndCancelsHandoffOnSceneFailure()
{
    BattleSessionHandoff.CancelBattle();
    FakeSceneTransitionGateway gateway = new(Error.Failed);
    SceneTransitionRouter router = new(gateway);
    int rollbackCalls = 0;
    BattleStartRequest request = new()
    {
        RequestId = "request_under_test",
        SiteScenePath = "res://missing_battle_scene.tscn",
        ReturnScenePath = "res://scenes/world/StrategicWorldRoot.tscn",
        BattleKind = BattleKind.AssaultSite
    };

    SceneTransitionResult result = router.EnterBattlePreparation(new SceneTransitionBattleRequest
    {
        Request = request,
        RollbackOnFailure = reason =>
        {
            rollbackCalls++;
            AssertEqual("scene_change_failed:Failed", reason, "battle rollback reason");
        }
    });

    AssertTrue(!result.Success, "failed battle transition should report failure");
    AssertEqual(Error.Failed, result.Error, "battle transition error");
    AssertEqual(1, gateway.ChangeCalls, "battle transition should attempt one scene change");
    AssertEqual("res://missing_battle_scene.tscn", gateway.LastScenePath, "battle transition target scene");
    AssertTrue(!BattleSessionHandoff.HasActiveLaunch, "failed battle transition should cancel active handoff");
    AssertEqual(1, rollbackCalls, "failed battle transition should run supplied rollback once");
    AssertTrue(!router.IsTransitioning, "failed battle transition should release busy state");

    BattleSessionHandoff.CancelBattle();
}

internal static void SceneTransitionRouterEntersStrategicBattleThroughActiveContext()
{
    BattleSessionHandoff.CancelBattle();
    ResetActiveContextStore();
    FakeSceneTransitionGateway gateway = new(Error.Ok);
    SceneTransitionRouter router = new(gateway);
    int successCalls = 0;
    StrategicBattleActiveContext context = BuildSceneTransitionActiveContext("strategic_context_success");

    SceneTransitionResult result = router.EnterBattlePreparation(new SceneTransitionBattleRequest
    {
        ActiveContext = context,
        OnSuccess = () => successCalls++
    });

    AssertTrue(result.Success, "strategic battle transition should start when scene change is accepted");
    AssertEqual(1, gateway.ChangeCalls, "strategic battle transition should attempt one scene change");
    AssertEqual(context.ScenePath, gateway.LastScenePath, "strategic battle transition target scene");
    AssertTrue(StrategicBattleActiveContextStore.HasActiveContext, "strategic transition should publish active context for the destination scene");
    AssertTrue(
        StrategicBattleActiveContextStore.TryPeek(out StrategicBattleActiveContext active) &&
        ReferenceEquals(context, active),
        "destination scene should be able to peek the exact strategic active context");
    AssertTrue(!BattleSessionHandoff.HasActiveLaunch, "strategic active context path must not begin legacy battle handoff");
    AssertEqual(0, successCalls, "strategic battle success callback should wait for scene entered");

    gateway.CompleteSceneChange();

    AssertEqual(1, successCalls, "strategic battle success callback should run once after scene entered");
    AssertTrue(!router.IsTransitioning, "strategic battle transition should release busy state after scene entered");
    AssertTrue(StrategicBattleActiveContextStore.HasActiveContext, "scene entry should not consume strategic active context");
    AssertTrue(!BattleSessionHandoff.HasActiveLaunch, "scene entry should still not create legacy handoff");

    ResetActiveContextStore();
}

internal static void StrategicBattleTransitionClearsStaleLegacyHandoff()
{
    BattleSessionHandoff.CancelBattle();
    ResetActiveContextStore();
    FakeSceneTransitionGateway gateway = new(Error.Ok);
    SceneTransitionRouter router = new(gateway);
    StrategicBattleActiveContext context = BuildSceneTransitionActiveContext("strategic_context_clears_legacy");
    BattleSessionHandoff.BeginBattle(new BattleStartRequest
    {
        RequestId = "stale_legacy_strategic_request",
        BattleKind = BattleKind.AssaultSite,
        SiteScenePath = "res://scenes/world/sites/WorldSiteRoot.tscn",
        StrategicExpeditionId = "stale_expedition"
    });

    SceneTransitionResult result = router.EnterBattlePreparation(new SceneTransitionBattleRequest
    {
        ActiveContext = context
    });

    AssertTrue(result.Success, "strategic battle transition should still start with a stale legacy handoff present");
    AssertTrue(
        !BattleSessionHandoff.HasActiveLaunch,
        "strategic battle transition must clear stale legacy handoff before destination scene entry can read it");

    ResetActiveContextStore();
    BattleSessionHandoff.CancelBattle();
}

internal static void SceneTransitionRouterCancelsStrategicActiveContextOnSceneFailure()
{
    BattleSessionHandoff.CancelBattle();
    ResetActiveContextStore();
    FakeSceneTransitionGateway gateway = new(Error.Failed);
    SceneTransitionRouter router = new(gateway);
    int rollbackCalls = 0;
    StrategicBattleActiveContext context = BuildSceneTransitionActiveContext("strategic_context_failure");

    SceneTransitionResult result = router.EnterBattlePreparation(new SceneTransitionBattleRequest
    {
        ActiveContext = context,
        RollbackOnFailure = reason =>
        {
            rollbackCalls++;
            AssertEqual("scene_change_failed:Failed", reason, "strategic battle rollback reason");
        }
    });

    AssertTrue(!result.Success, "failed strategic battle transition should report failure");
    AssertEqual(Error.Failed, result.Error, "strategic battle transition error");
    AssertEqual(1, gateway.ChangeCalls, "strategic battle transition should attempt one scene change");
    AssertEqual(context.ScenePath, gateway.LastScenePath, "strategic battle transition target scene");
    AssertTrue(!StrategicBattleActiveContextStore.HasActiveContext, "failed strategic transition should clear active context");
    AssertTrue(!BattleSessionHandoff.HasActiveLaunch, "failed strategic transition must not create legacy handoff");
    AssertEqual(1, rollbackCalls, "failed strategic transition should run supplied rollback once");
    AssertTrue(!router.IsTransitioning, "failed strategic transition should release busy state");
}

internal static void SceneTransitionRouterStaleFailureCannotClearNewerContext()
{
    ResetActiveContextStore();
    StrategicBattleActiveContext stale = BuildSceneTransitionActiveContext("stale_context");
    StrategicBattleActiveContext newer = BuildSceneTransitionActiveContext("newer_context");
    FakeSceneTransitionGateway gateway = new(Error.Failed)
    {
        OnChange = () =>
        {
    ResetActiveContextStore();
            AssertTrue(StrategicBattleActiveContextStore.TryBegin(newer, out _), "test should publish newer context before stale failure returns");
        }
    };
    SceneTransitionRouter router = new(gateway);
    int rollbackCalls = 0;

    SceneTransitionResult result = router.EnterBattlePreparation(new SceneTransitionBattleRequest
    {
        ActiveContext = stale,
        RollbackOnFailure = _ => rollbackCalls++
    });

    AssertTrue(!result.Success, "stale transition should still report its scene failure");
    AssertEqual(0, rollbackCalls, "stale failure must not run rollback after identity changed");
    AssertTrue(
        StrategicBattleActiveContextStore.TryPeek(newer.ContextId, newer.Session.SessionId, newer.Snapshot.SnapshotId, out StrategicBattleActiveContext active) && ReferenceEquals(newer, active),
        "stale failure must not clear newer active context");
    ResetActiveContextStore();
}

internal static void SceneTransitionRouterRejectsOverlappingTransitions()
{
    StrategicWorldRuntime.ClearPendingSiteVisit();
    FakeSceneTransitionGateway gateway = new(Error.Ok);
    SceneTransitionRouter router = new(gateway);

    SceneTransitionResult first = router.EnterSiteDetail(new SceneTransitionSiteVisitRequest
    {
        SiteId = "site_under_test",
        TargetScenePath = "res://scenes/world/sites/WorldSiteRoot.tscn",
        ReturnScenePath = "res://scenes/world/StrategicWorldRoot.tscn"
    });
    SceneTransitionResult second = router.ReturnFromSite(new SceneTransitionReturnRequest
    {
        TargetScenePath = "res://scenes/world/StrategicWorldRoot.tscn"
    });

    AssertTrue(first.Success, "first transition should start");
    AssertTrue(router.IsTransitioning, "successful transition should stay busy until the source scene exits");
    AssertTrue(!second.Success, "overlapping transition should be rejected");
    AssertEqual("transition_in_progress", second.FailureReason, "overlapping transition failure reason");
    AssertEqual(1, gateway.ChangeCalls, "rejected transition should not call scene change");
}

internal static void SceneTransitionRouterRunsSiteEnteredCallbackAfterSceneEntered()
{
    StrategicWorldRuntime.ClearPendingSiteVisit();
    FakeSceneTransitionGateway gateway = new(Error.Ok);
    SceneTransitionRouter router = new(gateway);
    int callbackCalls = 0;

    SceneTransitionResult result = router.EnterSiteDetail(new SceneTransitionSiteVisitRequest
    {
        SiteId = "site_under_test",
        TargetScenePath = "res://scenes/world/sites/WorldSiteRoot.tscn",
        ReturnScenePath = "res://scenes/world/StrategicWorldRoot.tscn",
        OnEntered = () => callbackCalls++
    });

    AssertTrue(result.Success, "site transition should start when scene change is accepted");
    AssertEqual(0, callbackCalls, "site entered callback should wait for scene entered");

    gateway.CompleteSceneChange();

    AssertEqual(1, callbackCalls, "site entered callback should run once after scene entered");
    AssertTrue(!router.IsTransitioning, "site transition should release busy state after scene entered");
}

internal static void SceneTransitionRouterRunsReturnEnteredCallbackAfterSceneEntered()
{
    FakeSceneTransitionGateway gateway = new(Error.Ok);
    SceneTransitionRouter router = new(gateway);
    int callbackCalls = 0;

    SceneTransitionResult result = router.ReturnFromSite(new SceneTransitionReturnRequest
    {
        TargetScenePath = "res://scenes/world/StrategicWorldRoot.tscn",
        OnEntered = () => callbackCalls++
    });

    AssertTrue(result.Success, "return transition should start when scene change is accepted");
    AssertEqual(0, callbackCalls, "return entered callback should wait for scene entered");

    gateway.CompleteSceneChange();

    AssertEqual(1, callbackCalls, "return entered callback should run once after scene entered");
    AssertTrue(!router.IsTransitioning, "return transition should release busy state after scene entered");
}

internal static void SceneTransitionRouterSkipsEnteredCallbacksOnSceneFailure()
{
    StrategicWorldRuntime.ClearPendingSiteVisit();
    FakeSceneTransitionGateway gateway = new(Error.Failed);
    SceneTransitionRouter router = new(gateway);
    int siteCallbackCalls = 0;
    int returnCallbackCalls = 0;

    SceneTransitionResult siteResult = router.EnterSiteDetail(new SceneTransitionSiteVisitRequest
    {
        SiteId = "site_under_test",
        TargetScenePath = "res://missing_site_scene.tscn",
        ReturnScenePath = "res://scenes/world/StrategicWorldRoot.tscn",
        OnEntered = () => siteCallbackCalls++
    });
    SceneTransitionResult returnResult = router.ReturnFromSite(new SceneTransitionReturnRequest
    {
        TargetScenePath = "res://missing_world_scene.tscn",
        OnEntered = () => returnCallbackCalls++
    });

    AssertTrue(!siteResult.Success, "failed site transition should report failure");
    AssertTrue(!returnResult.Success, "failed return transition should report failure");
    AssertEqual(0, siteCallbackCalls, "failed site transition must not run entered callback");
    AssertEqual(0, returnCallbackCalls, "failed return transition must not run entered callback");
    AssertTrue(!router.IsTransitioning, "failed transitions should release busy state");
}

internal static void SceneTransitionRouterCompletesBattleSuccessAfterSceneEntered()
{
    BattleSessionHandoff.CancelBattle();
    FakeSceneTransitionGateway gateway = new(Error.Ok);
    SceneTransitionRouter router = new(gateway);
    int successCalls = 0;
    BattleStartRequest request = new()
    {
        RequestId = "request_scene_entered_success",
        SiteScenePath = "res://scenes/world/sites/WorldSiteRoot.tscn",
        ReturnScenePath = "res://scenes/world/StrategicWorldRoot.tscn",
        BattleKind = BattleKind.AssaultSite
    };

    SceneTransitionResult result = router.EnterBattlePreparation(new SceneTransitionBattleRequest
    {
        Request = request,
        OnSuccess = () => successCalls++
    });

    AssertTrue(result.Success, "battle transition should start when scene change is accepted");
    AssertEqual(1, gateway.ChangeCalls, "battle transition should request one scene change");
    AssertEqual(0, successCalls, "battle success callback should wait for scene entered");
    AssertTrue(router.IsTransitioning, "battle transition should remain busy until scene entered");
    AssertTrue(BattleSessionHandoff.HasActiveLaunch, "started battle transition should keep handoff active for destination scene");

    gateway.CompleteSceneChange();

    AssertEqual(1, successCalls, "battle success callback should run once after scene entered");
    AssertTrue(!router.IsTransitioning, "battle transition should release busy state after scene entered");
    AssertTrue(BattleSessionHandoff.HasActiveLaunch, "scene entry should not consume the active battle handoff");

    BattleSessionHandoff.CancelBattle();
}

internal static void SceneTransitionRouterClearsBusyAfterSceneEntered()
{
    StrategicWorldRuntime.ClearPendingSiteVisit();
    FakeSceneTransitionGateway gateway = new(Error.Ok);
    SceneTransitionRouter router = new(gateway);

    SceneTransitionResult first = router.EnterSiteDetail(new SceneTransitionSiteVisitRequest
    {
        SiteId = "site_under_test",
        TargetScenePath = "res://scenes/world/sites/WorldSiteRoot.tscn",
        ReturnScenePath = "res://scenes/world/StrategicWorldRoot.tscn"
    });
    SceneTransitionResult overlapping = router.ReturnFromSite(new SceneTransitionReturnRequest
    {
        TargetScenePath = "res://scenes/world/StrategicWorldRoot.tscn"
    });

    AssertTrue(first.Success, "site transition should start");
    AssertTrue(!overlapping.Success, "overlapping transition should be rejected before scene entered");
    AssertTrue(router.IsTransitioning, "site transition should stay busy before scene entered");

    gateway.CompleteSceneChange();

    AssertTrue(!router.IsTransitioning, "site transition should release busy state after scene entered");

    SceneTransitionResult afterEntered = router.ReturnFromSite(new SceneTransitionReturnRequest
    {
        TargetScenePath = "res://scenes/world/StrategicWorldRoot.tscn"
    });

    AssertTrue(afterEntered.Success, "router should accept another transition after scene entered");
}

internal static void RootSceneChangesAreRoutedThroughSceneTransitionRouter()
{
    string strategicSource = ReadStrategicWorldRootSource();
    string siteSource = ReadWorldSiteRootSource();
    string routerSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Infrastructure", "Scenes", "SceneTransitionRouter.cs"));
    string gatewaySource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Infrastructure", "Scenes", "GodotSceneTransitionGateway.cs"));

    AssertTrue(
        strategicSource.Contains("_sceneTransitionRouter.EnterSiteDetail", StringComparison.Ordinal) &&
        strategicSource.Contains("_sceneTransitionRouter.EnterBattlePreparation", StringComparison.Ordinal),
        "StrategicWorldRoot should route site and battle scene transitions through SceneTransitionRouter");
    AssertTrue(
        siteSource.Contains("_sceneTransitionRouter.ReturnFromSite", StringComparison.Ordinal),
        "WorldSiteRoot should route return scene transitions through SceneTransitionRouter");
    AssertTrue(
        strategicSource.Contains("StrategicManagementRuntime.PauseWorldTimeForCityManagement", StringComparison.Ordinal),
        "StrategicWorldRoot should pause Strategic Management world time after entering site management");
    AssertTrue(
        siteSource.Contains("StrategicManagementRuntime.ResumeWorldMapTime", StringComparison.Ordinal),
        "WorldSiteRoot should resume Strategic Management world time after returning to the world map");
    AssertTrue(
        !strategicSource.Contains("ChangeSceneToFile", StringComparison.Ordinal) &&
        !siteSource.Contains("ChangeSceneToFile", StringComparison.Ordinal),
        "presentation roots should not directly call root scene changes after router migration");
    AssertTrue(
        routerSource.Contains("EnterBattlePreparation", StringComparison.Ordinal) &&
        gatewaySource.Contains("ChangeSceneToFile", StringComparison.Ordinal),
        "router boundary should own player-facing root scene changes through the Godot gateway");
}

private sealed class FakeSceneTransitionGateway : ISceneTransitionGateway
{
    private readonly Error _result;
    private Action? _onSceneEntered;

    public FakeSceneTransitionGateway(Error result)
    {
        _result = result;
    }

    public int ChangeCalls { get; private set; }
    public string LastScenePath { get; private set; } = "";
    public Action? OnChange { get; init; }

    public Error ChangeSceneToFile(string scenePath, Action onSceneEntered)
    {
        ChangeCalls++;
        LastScenePath = scenePath ?? "";
        OnChange?.Invoke();
        _onSceneEntered = _result == Error.Ok ? onSceneEntered : null;
        return _result;
    }

    public void CompleteSceneChange()
    {
        Action? callback = _onSceneEntered;
        _onSceneEntered = null;
        callback?.Invoke();
    }
}

private static StrategicBattleActiveContext BuildSceneTransitionActiveContext(string contextId)
{
    BattleStartRequest request = new()
    {
        RequestId = $"request:{contextId}",
        SiteScenePath = "res://scenes/world/sites/WorldSiteRoot.tscn",
        ReturnScenePath = "res://scenes/world/StrategicWorldRoot.tscn",
        BattleKind = BattleKind.AssaultSite
    };
    return new StrategicBattleActiveContext
    {
        ContextId = contextId,
        ScenePath = request.SiteScenePath,
        ReturnScenePath = request.ReturnScenePath,
        CompatibilityRequest = request,
        Session = new StrategicBattleSession
        {
            SessionId = contextId,
            ExpeditionId = $"expedition:{contextId}",
            TargetLocationId = "target_location",
            SiteScenePath = request.SiteScenePath,
            ReturnScenePath = request.ReturnScenePath
        },
        Snapshot = new BattleStartSnapshot
        {
            SnapshotId = $"snapshot:{contextId}",
            BattleId = contextId,
            TargetLocationId = "target_location"
        }
    };
}

private static void ResetActiveContextStore()
{
    if (StrategicBattleActiveContextStore.TryPeek(out StrategicBattleActiveContext context))
    {
        StrategicBattleActiveContextStore.TryClear(
            context.ContextId,
            context.Session?.SessionId,
            context.Snapshot?.SnapshotId,
            "test_reset");
    }
}
}
