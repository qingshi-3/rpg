using static RegressionTestRunner;
using Godot;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.World;

System.Environment.SetEnvironmentVariable("RPG_GAMELOG_DIR", Path.Combine(Path.GetTempPath(), "rpg-world-army-tests"));

Run("strategic grid navigation builds path without Godot synchronization", StrategicGridNavigationBuildsPathWithoutGodotSynchronization);
Run("strategic grid navigation avoids diagonal corner cutting", StrategicGridNavigationAvoidsDiagonalCornerCutting);
Run("strategic grid navigation rejects disconnected cells", StrategicGridNavigationRejectsDisconnectedCells);
Run("provider path failure blocks movement immediately", ProviderPathFailureBlocksMovementImmediately);
Run("permanent path failure blocks navigation", PermanentPathFailureBlocksNavigation);
Run("cached path remains valid for equivalent navigation data version", CachedPathRemainsValidForEquivalentNavigationDataVersion);
Run("command navigation rejects provider path failure without deferral", CommandNavigationRejectsProviderPathFailureWithoutDeferral);
Run("command navigation rejects permanent destination failure", CommandNavigationRejectsPermanentDestinationFailure);

static void StrategicGridNavigationBuildsPathWithoutGodotSynchronization()
{
    StrategicNavigationGrid grid = new(new[]
    {
        new Vector2I(0, 0),
        new Vector2I(1, 0),
        new Vector2I(0, 1),
        new Vector2I(1, 1)
    });

    bool found = grid.TryBuildCellPath(
        new Vector2I(0, 0),
        new Vector2I(1, 1),
        out IReadOnlyList<Vector2I> cells,
        out string failureReason);

    AssertTrue(found, $"strategic grid should find an authored map path without Godot NavigationServer, got {failureReason}");
    AssertEqual(new Vector2I(0, 0), cells[0], "path should start at requested cell");
    AssertEqual(new Vector2I(1, 1), cells[^1], "path should end at requested cell");
}

static void StrategicGridNavigationAvoidsDiagonalCornerCutting()
{
    StrategicNavigationGrid grid = new(new[]
    {
        new Vector2I(0, 0),
        new Vector2I(1, 0),
        new Vector2I(1, 1)
    });

    bool found = grid.TryBuildCellPath(
        new Vector2I(0, 0),
        new Vector2I(1, 1),
        out IReadOnlyList<Vector2I> cells,
        out string failureReason);

    AssertTrue(found, $"strategic grid should route around an authored corner instead of diagonal cutting, got {failureReason}");
    AssertSequence(new[] { new Vector2I(0, 0), new Vector2I(1, 0), new Vector2I(1, 1) }, cells, "diagonal movement must not cut through a missing orthogonal neighbor");
}

static void StrategicGridNavigationRejectsDisconnectedCells()
{
    StrategicNavigationGrid grid = new(new[]
    {
        new Vector2I(0, 0),
        new Vector2I(5, 5)
    });

    bool found = grid.TryBuildCellPath(
        new Vector2I(0, 0),
        new Vector2I(5, 5),
        out _,
        out string failureReason);

    AssertTrue(!found, "strategic grid should reject disconnected painted cells");
    AssertEqual("strategic_grid_path_missing", failureReason, "disconnected cells should report a deterministic missing-path reason");
}

static void ProviderPathFailureBlocksMovementImmediately()
{
    WorldArmyMovementService service = new();
    StrategicWorldState state = BuildState();
    WorldArmyState army = BuildMovingArmy();
    state.ArmyStates[army.ArmyId] = army;
    FakeNavigationContext navigation = new(version: 101)
    {
        FailureReason = "strategic_grid_path_missing",
        FailuresBeforeSuccess = int.MaxValue
    };

    WorldArmyMovementResult result = service.AdvanceArmies(state, new StrategicWorldDefinition(), 0.25, navigation);

    AssertEqual(WorldArmyStatus.NavigationBlocked, army.Status, "provider path failure should block immediately under the custom strategic grid contract");
    AssertEqual(1, result.NavigationBlockedArmyIds.Count, "provider path failure should report NavigationBlocked immediately");
    AssertEqual(1, navigation.TryBuildPathCalls, "movement should ask the provider once before reporting a deterministic failure");
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

static void CachedPathRemainsValidForEquivalentNavigationDataVersion()
{
    WorldArmyMovementService service = new();
    StrategicWorldState state = BuildState();
    WorldArmyState army = BuildMovingArmy();
    army.SetNavigationPath(new[] { new Vector2(0, 0), new Vector2(100, 0) }, new Vector2(100, 0), 303);
    state.ArmyStates[army.ArmyId] = army;
    FakeNavigationContext navigation = new(version: 303)
    {
        FailureReason = "strategic_grid_path_missing",
        FailuresBeforeSuccess = int.MaxValue
    };

    WorldArmyMovementResult result = service.AdvanceArmies(state, new StrategicWorldDefinition(), 0.25, navigation);

    AssertEqual(0, navigation.TryBuildPathCalls, "valid cached path should not ask the navigation provider for a new path");
    AssertEqual(0, result.NavigationBlockedArmyIds.Count, "valid cached path should not block");
    AssertTrue(army.WorldPosition.X > 0.0f, "army should advance on the cached path");
}

static void CommandNavigationRejectsProviderPathFailureWithoutDeferral()
{
    WorldArmyState army = BuildMovingArmy();
    FakeNavigationContext navigation = new(version: 505)
    {
        FailureReason = "strategic_grid_path_missing",
        FailuresBeforeSuccess = int.MaxValue
    };

    bool accepted = StrategicCommandNavigationService.TryBuildOrDeferPaths(
        new[] { army },
        new Vector2(100, 0),
        navigation,
        out StrategicCommandNavigationResult result,
        out string failureReason);

    AssertTrue(!accepted, "custom strategic navigation should reject missing command paths immediately");
    AssertTrue(!result.HasDeferredPaths, "custom strategic navigation should not keep Godot-style deferred path state");
    AssertTrue(failureReason.Contains("strategic_grid_path_missing", StringComparison.Ordinal), "failure reason should preserve provider path cause");
}

static void CommandNavigationRejectsPermanentDestinationFailure()
{
    WorldArmyState army = BuildMovingArmy();
    FakeNavigationContext navigation = new(version: 707)
    {
        FailureReason = "destination_point_outside_strategic_navigation_tile_layer",
        FailuresBeforeSuccess = int.MaxValue
    };

    bool accepted = StrategicCommandNavigationService.TryBuildOrDeferPaths(
        new[] { army },
        new Vector2(100, 0),
        navigation,
        out _,
        out string failureReason);

    AssertTrue(!accepted, "permanent command path failure should still reject the command");
    AssertTrue(failureReason.Contains("destination_point_outside_strategic_navigation_tile_layer", StringComparison.Ordinal), "failure reason should preserve the permanent navigation cause");
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
        Intent = WorldArmyIntent.AssaultSite
    };
    army.WorldPosition = new Vector2(0, 0);
    army.Destination = new Vector2(100, 0);
    return army;
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

static void AssertSequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string message)
{
    if (expected.Count != actual.Count)
    {
        throw new InvalidOperationException($"{message}. ExpectedCount={expected.Count} ActualCount={actual.Count}");
    }

    for (int index = 0; index < expected.Count; index++)
    {
        if (!EqualityComparer<T>.Default.Equals(expected[index], actual[index]))
        {
            throw new InvalidOperationException($"{message}. Index={index} Expected={expected[index]} Actual={actual[index]}");
        }
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

    public bool TryGetNearestReachableNavigablePoint(
        Vector2 start,
        Vector2 preferredPoint,
        int maxCellRadius,
        out Vector2 navigablePoint,
        out StrategicNavigationPath path,
        out string failureReason)
    {
        navigablePoint = preferredPoint;
        path = new StrategicNavigationPath { ProviderId = PrimaryProviderId };
        path.Points.Add(start);
        path.Points.Add(preferredPoint);
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
