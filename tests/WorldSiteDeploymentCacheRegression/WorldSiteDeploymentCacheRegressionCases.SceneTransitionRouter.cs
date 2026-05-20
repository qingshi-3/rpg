using Godot;
using Rpg.Application.Battle;
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

    public FakeSceneTransitionGateway(Error result)
    {
        _result = result;
    }

    public int ChangeCalls { get; private set; }
    public string LastScenePath { get; private set; } = "";

    public Error ChangeSceneToFile(string scenePath)
    {
        ChangeCalls++;
        LastScenePath = scenePath ?? "";
        return _result;
    }
}
}
