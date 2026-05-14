using Godot;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.World;

System.Environment.SetEnvironmentVariable("RPG_GAMELOG_DIR", Path.Combine(Path.GetTempPath(), "rpg-world-army-tests"));

Run("transient empty Godot path keeps moving and retries", TransientEmptyPathKeepsMovingAndRetries);
Run("short navigation warmup empty paths stay pending", ShortNavigationWarmupEmptyPathsStayPending);
Run("repeated empty Godot path eventually blocks navigation", RepeatedEmptyPathEventuallyBlocksNavigation);
Run("permanent path failure blocks navigation", PermanentPathFailureBlocksNavigation);
Run("cached path remains valid for equivalent navigation data version", CachedPathRemainsValidForEquivalentNavigationDataVersion);

static void TransientEmptyPathKeepsMovingAndRetries()
{
    WorldArmyMovementService service = new();
    StrategicWorldState state = BuildState();
    WorldArmyState army = BuildMovingArmy();
    state.ArmyStates[army.ArmyId] = army;
    FakeNavigationContext navigation = new(version: 101)
    {
        FailureReason = "godot_navigation_path_empty",
        FailuresBeforeSuccess = 1
    };

    WorldArmyMovementResult first = service.AdvanceArmies(state, new StrategicWorldDefinition(), 0.25, navigation);
    AssertEqual(WorldArmyStatus.Moving, army.Status, "transient failure must not block the army");
    AssertEqual(0, first.NavigationBlockedArmyIds.Count, "transient failure must not report NavigationBlocked");
    AssertEqual(1, navigation.TryBuildPathCalls, "first advance should try to build a path");

    WorldArmyMovementResult second = service.AdvanceArmies(state, new StrategicWorldDefinition(), 0.25, navigation);
    AssertEqual(WorldArmyStatus.Moving, army.Status, "army should still be moving after retry succeeds");
    AssertEqual(0, second.NavigationBlockedArmyIds.Count, "successful retry must not report NavigationBlocked");
    AssertTrue(army.HasNavigationPath, "successful retry should cache the path");
    AssertTrue(army.WorldPosition.X > 0.0f, "army should advance after path is cached");
}

static void PermanentPathFailureBlocksNavigation()
{
    WorldArmyMovementService service = new();
    StrategicWorldState state = BuildState();
    WorldArmyState army = BuildMovingArmy();
    state.ArmyStates[army.ArmyId] = army;
    FakeNavigationContext navigation = new(version: 202)
    {
        FailureReason = "destination_point_outside_strategic_navigation_tile_layer",
        FailuresBeforeSuccess = int.MaxValue
    };

    WorldArmyMovementResult result = service.AdvanceArmies(state, new StrategicWorldDefinition(), 0.25, navigation);

    AssertEqual(WorldArmyStatus.NavigationBlocked, army.Status, "permanent navigation failure should block the army");
    AssertEqual(1, result.NavigationBlockedArmyIds.Count, "permanent navigation failure should be reported");
    AssertEqual(army.ArmyId, result.NavigationBlockedArmyIds[0], "blocked army id should be reported");
}

static void ShortNavigationWarmupEmptyPathsStayPending()
{
    WorldArmyMovementService service = new();
    StrategicWorldState state = BuildState();
    WorldArmyState army = BuildMovingArmy();
    state.ArmyStates[army.ArmyId] = army;
    FakeNavigationContext navigation = new(version: 303)
    {
        FailureReason = "godot_navigation_path_empty",
        FailuresBeforeSuccess = int.MaxValue
    };

    WorldArmyMovementResult result = new();
    for (int i = 0; i < 4; i++)
    {
        result = service.AdvanceArmies(state, new StrategicWorldDefinition(), 0.004, navigation);
    }

    AssertEqual(WorldArmyStatus.Moving, army.Status, "short navigation warmup failures should keep the army moving");
    AssertEqual(0, result.NavigationBlockedArmyIds.Count, "short navigation warmup failures should not report NavigationBlocked");
    AssertEqual(4, navigation.TryBuildPathCalls, "each warmup frame should retry path building");
}

static void RepeatedEmptyPathEventuallyBlocksNavigation()
{
    WorldArmyMovementService service = new();
    StrategicWorldState state = BuildState();
    WorldArmyState army = BuildMovingArmy();
    state.ArmyStates[army.ArmyId] = army;
    FakeNavigationContext navigation = new(version: 404)
    {
        FailureReason = "godot_navigation_path_empty",
        FailuresBeforeSuccess = int.MaxValue
    };

    WorldArmyMovementResult result = new();
    for (int i = 0; i < 4; i++)
    {
        result = service.AdvanceArmies(state, new StrategicWorldDefinition(), 0.25, navigation);
    }

    AssertEqual(WorldArmyStatus.NavigationBlocked, army.Status, "repeated transient failures should become a hard navigation block");
    AssertEqual(1, result.NavigationBlockedArmyIds.Count, "repeated transient failures should report NavigationBlocked");
}

static void CachedPathRemainsValidForEquivalentNavigationDataVersion()
{
    WorldArmyMovementService service = new();
    StrategicWorldState state = BuildState();
    WorldArmyState army = BuildMovingArmy();
    army.SetNavigationPath(new[] { new Vector2(0, 0), new Vector2(100, 0) }, new Vector2(100, 0), 303);
    state.ArmyStates[army.ArmyId] = army;
    FakeNavigationContext navigation = new(version: 303)
    {
        FailureReason = "godot_navigation_path_empty",
        FailuresBeforeSuccess = int.MaxValue
    };

    WorldArmyMovementResult result = service.AdvanceArmies(state, new StrategicWorldDefinition(), 0.25, navigation);

    AssertEqual(0, navigation.TryBuildPathCalls, "valid cached path should not ask Godot for a new path");
    AssertEqual(0, result.NavigationBlockedArmyIds.Count, "valid cached path should not block");
    AssertTrue(army.WorldPosition.X > 0.0f, "army should advance on the cached path");
}

static StrategicWorldState BuildState()
{
    return new StrategicWorldState
    {
        WorldTick = 7,
        PlayerFactionId = StrategicWorldIds.FactionPlayer
    };
}

static WorldArmyState BuildMovingArmy()
{
    WorldArmyState army = new()
    {
        ArmyId = "army_under_test",
        OwnerFactionId = StrategicWorldIds.FactionUndead,
        SourceSiteId = StrategicWorldIds.SiteGraveyard,
        TargetSiteId = StrategicWorldIds.SiteBonefield,
        MoveSpeed = 40.0f,
        Radius = 18.0f,
        Status = WorldArmyStatus.Moving,
        Intent = WorldArmyIntent.Raid
    };
    army.WorldPosition = new Vector2(0, 0);
    army.Destination = new Vector2(100, 0);
    return army;
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
        throw new InvalidOperationException($"{message}. Expected={expected} Actual={actual}");
    }
}

sealed class FakeNavigationContext : IStrategicNavigationContext
{
    public FakeNavigationContext(int version)
    {
        Version = version;
    }

    public int Version { get; }
    public string PrimaryProviderId => "fake_navigation";
    public int FailuresBeforeSuccess { get; set; }
    public string FailureReason { get; set; } = "";
    public int TryBuildPathCalls { get; private set; }

    public bool IsSynchronized(out string failureReason)
    {
        failureReason = "";
        return true;
    }

    public bool IsPointNavigable(Vector2 mapPoint, out string failureReason)
    {
        failureReason = "";
        return true;
    }

    public bool TryGetNearestNavigablePoint(Vector2 mapPoint, int maxCellRadius, out Vector2 navigablePoint, out string failureReason)
    {
        navigablePoint = mapPoint;
        failureReason = "";
        return true;
    }

    public bool TryBuildPath(Vector2 start, Vector2 destination, out StrategicNavigationPath path, out string failureReason)
    {
        TryBuildPathCalls++;
        path = new StrategicNavigationPath { ProviderId = PrimaryProviderId };
        if (TryBuildPathCalls <= FailuresBeforeSuccess)
        {
            failureReason = FailureReason;
            return false;
        }

        failureReason = "";
        path.Points.Add(start);
        path.Points.Add(destination);
        return true;
    }
}
