using Godot;
using Rpg.Application.Battle;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;

internal static partial class WorldSiteDeploymentCacheRegressionCases
{
internal static string ReadStrategicWorldRootSource()
{
    string worldRootDir = Path.Combine(ProjectRoot(), "src", "Presentation", "World");
    return string.Join("\n", Directory.GetFiles(worldRootDir, "StrategicWorldRoot*.cs").OrderBy(path => path).Select(File.ReadAllText));
}

internal static string ReadWorldSiteRootSource()
{
    string siteRootDir = Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites");
    return string.Join("\n", Directory.GetFiles(siteRootDir, "WorldSiteRoot*.cs").OrderBy(path => path).Select(File.ReadAllText));
}

internal static string ReadWorldSitePresentationSource()
{
    string siteRootDir = Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites");
    return string.Join("\n", Directory.GetFiles(siteRootDir, "*.cs").OrderBy(path => path).Select(File.ReadAllText));
}

internal static string ReadBattleGridHighlightOverlaySource()
{
    string battlePresentationDir = Path.Combine(ProjectRoot(), "src", "Presentation", "Battle");
    return string.Join("\n", Directory.GetFiles(battlePresentationDir, "BattleGridHighlightOverlay*.cs").OrderBy(path => path).Select(File.ReadAllText));
}

internal static string ExtractMethodBody(string source, string signature)
{
    int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
    AssertTrue(signatureIndex >= 0, $"source method missing signature={signature}");

    int openBraceIndex = source.IndexOf('{', signatureIndex);
    AssertTrue(openBraceIndex >= 0, $"source method missing opening brace signature={signature}");

    int depth = 0;
    for (int index = openBraceIndex; index < source.Length; index++)
    {
        char current = source[index];
        if (current == '{')
        {
            depth++;
        }
        else if (current == '}')
        {
            depth--;
            if (depth == 0)
            {
                return source.Substring(openBraceIndex + 1, index - openBraceIndex - 1);
            }
        }
    }

    throw new InvalidOperationException($"source method missing closing brace signature={signature}");
}

internal static string ProjectRoot()
{
    DirectoryInfo? directory = new(AppContext.BaseDirectory);
    while (directory != null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "rpg.csproj")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException("Could not locate project root from test output directory.");
}

internal static void GodotTextResourcesDoNotUseUtf8Bom()
{
    string root = ProjectRoot();
    string[] searchRoots =
    {
        Path.Combine(root, "assets"),
        Path.Combine(root, "resource"),
        Path.Combine(root, "scenes")
    };
    string[] extensions = { ".tscn", ".tres", ".gdshader" };
    List<string> bomFiles = new();
    foreach (string searchRoot in searchRoots)
    {
        if (!Directory.Exists(searchRoot))
        {
            continue;
        }

        foreach (string file in Directory.GetFiles(searchRoot, "*", SearchOption.AllDirectories))
        {
            if (!extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            byte[] bytes = File.ReadAllBytes(file);
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                bomFiles.Add(Path.GetRelativePath(root, file));
            }
        }
    }

    AssertTrue(bomFiles.Count == 0, $"Godot text resources must not start with UTF-8 BOM: {string.Join(", ", bomFiles)}");
}

internal static GridCellSurface AddNonWalkableFoundation(
    BattleGridMap grid,
    int x,
    int y,
    int height = 0,
    string terrainTag = "blocked")
{
    GridCellSurface surface = grid.GetOrCreateSurface(new GridPosition(x, y), height);
    surface.AddLayer(new GridCellLayerData(
        "foundation",
        LayerRole.Foundation,
        height,
        affectsWalkability: true,
        affectsLineOfSight: false,
        isHeightTransitionLayer: false,
        isVisualOnly: false,
        walkable: false,
        moveCost: 0,
        canStandOn: false,
        isObstacle: true,
        terrainTag,
        sourceId: 0,
        atlasX: 0,
        atlasY: 0,
        alternativeTile: 0));
    return surface;
}

internal static GridCellSurface AddFoundationWithoutWalkability(
    BattleGridMap grid,
    int x,
    int y,
    int height = 0,
    string terrainTag = "zero_cost")
{
    GridCellSurface surface = grid.GetOrCreateSurface(new GridPosition(x, y), height);
    surface.AddLayer(new GridCellLayerData(
        "foundation",
        LayerRole.Foundation,
        height,
        affectsWalkability: false,
        affectsLineOfSight: false,
        isHeightTransitionLayer: false,
        isVisualOnly: false,
        walkable: true,
        moveCost: 0,
        canStandOn: false,
        isObstacle: false,
        terrainTag,
        sourceId: 0,
        atlasX: 0,
        atlasY: 0,
        alternativeTile: 0));
    return surface;
}

internal static WorldSiteState BuildDeploymentSite()
{
    return new WorldSiteState
    {
        SiteId = "site_under_test",
        OwnerFactionId = "player"
    };
}

internal static StrategicWorldState BuildFirstSliceAssaultState(
    StrategicWorldDefinition definition,
    string armyId,
    string heroUnitId,
    string corpsUnitId)
{
    StrategicWorldState state = new StrategicWorldService().CreateInitialState(definition);
    state.PlayerFactionId = StrategicWorldIds.FactionPlayer;

    WorldSiteState playerSite = state.SiteStates[StrategicWorldIds.SitePlayerCamp];
    playerSite.OwnerFactionId = StrategicWorldIds.FactionPlayer;
    playerSite.ControlState = SiteControlState.PlayerHeld;
    playerSite.Garrison.Clear();
    foreach (string hero in new[] { "f1_grandmasterzir", "f1_windbladecommander", "f1_elyxstormblade" })
    {
        playerSite.Garrison.Add(new GarrisonState
        {
            UnitTypeId = hero,
            Count = 1,
            Morale = 80
        });
    }
    playerSite.UnitPlacements.Clear();

    WorldSiteState bonefield = state.SiteStates[StrategicWorldIds.SiteBonefield];
    bonefield.OwnerFactionId = StrategicWorldIds.FactionUndead;
    bonefield.ControlState = SiteControlState.Hostile;
    bonefield.Garrison.Clear();
    bonefield.Garrison.Add(new GarrisonState
    {
        UnitTypeId = "f6_draugarlord",
        Count = 2,
        Morale = 35
    });
    bonefield.Garrison.Add(new GarrisonState
    {
        UnitTypeId = "f6_spiritwolf",
        Count = 2,
        Morale = 35
    });
    bonefield.Garrison.Add(new GarrisonState
    {
        UnitTypeId = "f4_skullcaster",
        Count = 2,
        Morale = 35
    });
    bonefield.UnitPlacements.Clear();

    state.ArmyStates[armyId] = new WorldArmyState
    {
        ArmyId = armyId,
        OwnerFactionId = StrategicWorldIds.FactionPlayer,
        SourceSiteId = StrategicWorldIds.SitePlayerCamp,
        TargetSiteId = StrategicWorldIds.SiteBonefield,
        Intent = WorldArmyIntent.AssaultSite,
        Status = WorldArmyStatus.Attacking,
        GarrisonUnits =
        {
            new GarrisonState
            {
                UnitTypeId = heroUnitId,
                Count = 1,
                Morale = 80
            },
            new GarrisonState
            {
                UnitTypeId = corpsUnitId,
                Count = 3,
                Morale = 70
            }
        }
    };

    return state;
}

internal static WorldSiteUnitPlacement BuildVisitingArmyPlacement(
    string armyId,
    string unitTypeId,
    int index,
    int cellX,
    int cellY)
{
    return new WorldSiteUnitPlacement
    {
        PlacementId = $"visiting:{armyId}:{unitTypeId}:{index}",
        UnitTypeId = unitTypeId,
        UnitIndex = index,
        FactionId = StrategicWorldIds.FactionPlayer,
        PlacementKind = WorldSiteUnitPlacementKind.VisitingArmy,
        SourceKind = "PlayerArmy",
        SourceId = armyId,
        ArmyId = armyId,
        CellX = cellX,
        CellY = cellY,
        CellHeight = 1
    };
}

internal static BattleResult BuildAssaultVictoryResult(BattleStartRequest request)
{
    BattleResult result = new()
    {
        RequestId = request.RequestId,
        ContextId = request.ContextId,
        BattleKind = request.BattleKind,
        Outcome = BattleOutcome.Victory,
        ObjectiveResults =
        {
            new BattleObjectiveResult
            {
                ObjectiveId = "occupy_bonefield",
                State = BattleObjectiveState.Succeeded
            }
        }
    };

    foreach (BattleForceRequest force in request.PlayerForces)
    {
        result.ForceResults.Add(new BattleForceResult
        {
            ForceId = force.ForceId,
            SourceKind = force.SourceKind,
            SourceId = force.SourceId,
            UnitDefinitionId = force.UnitDefinitionId,
            InitialCount = force.Count,
            SurvivedCount = force.Count,
            DefeatedCount = 0
        });
    }

    foreach (BattleForceRequest force in request.EnemyForces)
    {
        result.ForceResults.Add(new BattleForceResult
        {
            ForceId = force.ForceId,
            SourceKind = force.SourceKind,
            SourceId = force.SourceId,
            UnitDefinitionId = force.UnitDefinitionId,
            InitialCount = force.Count,
            SurvivedCount = 0,
            DefeatedCount = force.Count
        });
    }

    return result;
}

internal static WorldSiteDefinition BuildDefaultZoneDefinition(params Vector2I[] cells)
{
    WorldSiteDefinition definition = new()
    {
        Id = "site_under_test",
        DefaultGarrisonZoneId = WorldSiteDeploymentService.DefaultGarrisonZoneId
    };
    SiteDeploymentZoneDefinition zone = new()
    {
        ZoneId = WorldSiteDeploymentService.DefaultGarrisonZoneId,
        ZoneKind = SiteDeploymentZoneKind.DefaultGarrison
    };
    zone.Cells.AddRange(cells);
    definition.DeploymentZones.Add(zone);
    return definition;
}

internal static bool CanPlacementEnterWater(WorldSiteUnitPlacement placement)
{
    return string.Equals(placement?.UnitTypeId, "boat", StringComparison.OrdinalIgnoreCase);
}

internal static bool CanForceEnterWater(BattleForceRequest force)
{
    return string.Equals(force?.UnitDefinitionId, "boat", StringComparison.OrdinalIgnoreCase);
}

internal static void Run(string name, Action test)
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

internal static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

internal static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{message}: expected={expected} actual={actual}");
    }
}
}
