using Godot;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Application.StrategicBattleBridge;
using Rpg.Infrastructure.Scenes;

System.Environment.SetEnvironmentVariable(
    "RPG_GAMELOG_DIR",
    Path.Combine(Path.GetTempPath(), "rpg-scene-transition-cas-tests"));

Run("strategic scene transition publishes and retains initial token", StrategicTransitionPublishesAndRetainsInitialToken);
Run("strategic scene failure clears only exact initial token", StrategicSceneFailureClearsOnlyExactInitialToken);
Run("same-reference advanced revision survives stale scene failure", SameReferenceAdvancedRevisionSurvivesStaleSceneFailure);

static void StrategicTransitionPublishesAndRetainsInitialToken()
{
    ResetStore();
    FakeGateway gateway = new(Error.Ok);
    SceneTransitionRouter router = new(gateway);
    StrategicBattleActiveContext context = BuildContext("scene_success");
    int enteredCalls = 0;

    SceneTransitionResult result = router.EnterBattlePreparation(new SceneTransitionBattleRequest
    {
        ActiveContext = context,
        OnSuccess = () => enteredCalls++
    });

    AssertTrue(result.Success, "accepted scene change should start");
    AssertTrue(
        StrategicBattleActiveContextStore.TryPeek(
            out StrategicBattleActiveContext stored,
            out StrategicBattleActiveContextToken token) &&
        ReferenceEquals(context, stored),
        "destination should observe the exact published context");
    AssertTrue(token.Revision > 0, "begin should publish a positive immutable revision");
    AssertEqual(0, enteredCalls, "entered callback must wait for scene entry");

    gateway.CompleteSceneChange();

    AssertEqual(1, enteredCalls, "entered callback should run once");
    AssertTrue(!router.IsTransitioning, "router should release busy state after scene entry");
    AssertTrue(StrategicBattleActiveContextStore.TryPeek(token, out _), "scene entry must not consume active context");
    ResetStore();
}

static void StrategicSceneFailureClearsOnlyExactInitialToken()
{
    ResetStore();
    FakeGateway gateway = new(Error.Failed);
    SceneTransitionRouter router = new(gateway);
    StrategicBattleActiveContext context = BuildContext("scene_failure");
    int rollbackCalls = 0;

    SceneTransitionResult result = router.EnterBattlePreparation(new SceneTransitionBattleRequest
    {
        ActiveContext = context,
        RollbackOnFailure = _ => rollbackCalls++
    });

    AssertTrue(!result.Success, "failed scene change should fail transition");
    AssertEqual(1, rollbackCalls, "exact initial token failure should roll back once");
    AssertTrue(!StrategicBattleActiveContextStore.HasActiveContext, "exact initial token failure should clear context");
}

static void SameReferenceAdvancedRevisionSurvivesStaleSceneFailure()
{
    ResetStore();
    StrategicBattleActiveContext context = BuildContext("scene_same_reference");
    StrategicBattleActiveContextToken? advancedToken = null;
    FakeGateway gateway = new(Error.Failed)
    {
        OnChange = () =>
        {
            AssertTrue(
                StrategicBattleActiveContextStore.TryPeek(
                    out StrategicBattleActiveContext stored,
                    out StrategicBattleActiveContextToken transitionToken) &&
                ReferenceEquals(context, stored),
                "interleaving should retain the same mutable context reference");
            AssertTrue(
                StrategicBattleActiveContextStore.TryAdvanceSnapshot(
                    transitionToken,
                    context,
                    new BattleStartSnapshot
                    {
                        SnapshotId = transitionToken.SnapshotId,
                        BattleId = transitionToken.SessionId,
                        TargetLocationId = context.Session.TargetLocationId
                    },
                    context.CompatibilityRequest,
                    "scene_same_reference_draft",
                    1,
                    Array.Empty<string>(),
                    out advancedToken,
                    out string advanceFailure),
                $"interleaving should advance revision, got {advanceFailure}");
        }
    };
    SceneTransitionRouter router = new(gateway);
    int rollbackCalls = 0;

    SceneTransitionResult result = router.EnterBattlePreparation(new SceneTransitionBattleRequest
    {
        ActiveContext = context,
        RollbackOnFailure = _ => rollbackCalls++
    });

    AssertTrue(!result.Success, "original scene request should still report failure");
    AssertEqual(0, rollbackCalls, "stale failure must not roll back the advanced revision");
    AssertTrue(advancedToken != null, "interleaving should capture the advanced token");
    AssertTrue(
        StrategicBattleActiveContextStore.TryPeek(advancedToken, out StrategicBattleActiveContext active) &&
        ReferenceEquals(context, active),
        "stale scene failure must leave the same-reference advanced revision active");
    ResetStore();
}

static StrategicBattleActiveContext BuildContext(string id)
{
    BattleStartRequest request = new()
    {
        RequestId = $"request:{id}",
        SiteScenePath = "res://scenes/world/sites/WorldSiteRoot.tscn",
        ReturnScenePath = "res://scenes/world/StrategicWorldRoot.tscn",
        BattleKind = BattleKind.AssaultSite
    };
    return new StrategicBattleActiveContext
    {
        ContextId = id,
        ScenePath = request.SiteScenePath,
        ReturnScenePath = request.ReturnScenePath,
        CompatibilityRequest = request,
        Session = new StrategicBattleSession
        {
            SessionId = id,
            ExpeditionId = $"expedition:{id}",
            TargetLocationId = "target_location",
            SiteScenePath = request.SiteScenePath,
            ReturnScenePath = request.ReturnScenePath
        },
        Snapshot = new BattleStartSnapshot
        {
            SnapshotId = $"snapshot:{id}",
            BattleId = id,
            TargetLocationId = "target_location"
        }
    };
}

static void ResetStore()
{
    if (StrategicBattleActiveContextStore.TryPeek(out _, out StrategicBattleActiveContextToken token))
    {
        StrategicBattleActiveContextStore.TryClear(token, "test_reset", out _);
    }
}

static void Run(string name, Action test)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"FAIL {name}: {exception.Message}");
        System.Environment.ExitCode = 1;
    }
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{message}. Expected={expected}; Actual={actual}");
    }
}

sealed class FakeGateway : ISceneTransitionGateway
{
    private readonly Error _result;
    private Action? _onSceneEntered;

    public FakeGateway(Error result)
    {
        _result = result;
    }

    public Action? OnChange { get; init; }

    public Error ChangeSceneToFile(string scenePath, Action onSceneEntered)
    {
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
