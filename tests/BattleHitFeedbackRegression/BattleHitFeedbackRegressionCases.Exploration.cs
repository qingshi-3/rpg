using Rpg.Presentation.Battle.Actions;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Debug;
using Rpg.Presentation.Battle.Feedback;
using Rpg.Presentation.Battle.Flow;
using Rpg.Presentation.Battle.Preview;
using Rpg.Presentation.Common;
using Rpg.Presentation.World;
using Rpg.Definitions.Battle.Audio;
using Rpg.Application.Battle;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using System.Text.Json;

internal static partial class BattleHitFeedbackRegressionCases
{
internal static void WorldSiteGridExplorationStatePersistsPositionAndMemory()
{
    WorldSiteState site = new()
    {
        SiteId = "test_site",
        Exploration = new WorldSiteExplorationState
        {
            CurrentCellX = 2,
            CurrentCellY = 3,
            CurrentCellHeight = 1,
            AlertLevel = 2
        }
    };
    site.Exploration.RevealedCellKeys.Add("2:3:1");
    site.Exploration.VisitedCellKeys.Add("1:3:1");
    site.Exploration.RevealedPointIds.Add("broken_cart");
    site.Exploration.ResolvedPointIds.Add("drain_entry");

    AssertEqual(2, site.Exploration.CurrentCellX, "exploration should persist current grid x");
    AssertEqual(3, site.Exploration.CurrentCellY, "exploration should persist current grid y");
    AssertEqual(1, site.Exploration.CurrentCellHeight, "exploration should persist current grid height");
    AssertTrue(site.Exploration.RevealedCellKeys.Contains("2:3:1"), "exploration should persist revealed cells");
    AssertTrue(site.Exploration.VisitedCellKeys.Contains("1:3:1"), "exploration should persist visited cells");
    AssertTrue(site.Exploration.RevealedPointIds.Contains("broken_cart"), "exploration should persist revealed point ids");
    AssertTrue(site.Exploration.ResolvedPointIds.Contains("drain_entry"), "exploration should persist resolved point ids");
}

internal static void WorldSiteGridExplorationUsesBattleGridPathingOutsideBattleTurns()
{
    BattleGridMap gridMap = new();
    for (int x = 0; x <= 2; x++)
    {
        GridCellSurface surface = gridMap.GetOrCreateSurface(new GridPosition(x, 0), 0);
        surface.AddLayer(new GridCellLayerData("test", LayerRole.Foundation, 0, true, false, false, false, true, 1, true, false, "", 0, 0, 0, 0));
    }

    gridMap.RebuildTopSurfaceIndex();
    WorldSiteExplorationState exploration = new() { CurrentCellX = 0, CurrentCellY = 0, CurrentCellHeight = 0 };

    bool moved = WorldSiteExplorationService.TryMoveParty(
        exploration,
        gridMap,
        new GridPosition(2, 0),
        out IReadOnlyList<GridSurfacePosition> path,
        out string failureReason);

    AssertTrue(moved, $"exploration should move through walkable BattleGridMap cells failure={failureReason}");
    AssertEqual(3, path.Count, "exploration path should include start, middle, and destination");
    AssertEqual(2, exploration.CurrentCellX, "exploration should update current x after movement");
    AssertEqual(0, exploration.CurrentCellY, "exploration should update current y after movement");
    AssertTrue(exploration.VisitedCellKeys.Contains("2:0:0"), "exploration should mark destination as visited");

    string worldSiteRoot = ReadWorldSiteRootSource();
    AssertTrue(worldSiteRoot.Contains("WorldSiteRuntimeMode.Exploration", StringComparison.Ordinal), "WorldSiteRoot should expose a site exploration runtime mode");
    AssertTrue(worldSiteRoot.Contains("TryHandleSiteExplorationInput", StringComparison.Ordinal), "WorldSiteRoot should route non-battle input through exploration before management drag behavior");
    AssertTrue(!ExtractMethodBlock(worldSiteRoot, "private bool TryHandleSiteExplorationInput").Contains("_turnController", StringComparison.Ordinal), "exploration input must not use battle turn controller or AP");
}

internal static void WorldSiteRootRoutesAuthoredExplorationPointActions()
{
    string worldSiteRoot = ReadWorldSiteRootSource();

    AssertTrue(
        worldSiteRoot.Contains("TryAppendSiteExplorationPointActions", StringComparison.Ordinal),
        "WorldSiteRoot should append authored exploration point actions during active site exploration");
    AssertTrue(
        worldSiteRoot.Contains("ExecuteSiteExplorationPointAction", StringComparison.Ordinal),
        "WorldSiteRoot should route point action button presses through a dedicated executor");

    string executeMethod = ExtractMethodBlock(worldSiteRoot, "private void ExecuteSiteExplorationPointAction");
    AssertTrue(
        executeMethod.Contains("WorldSiteExplorationService.ApplyActionResult", StringComparison.Ordinal),
        "point action execution should apply authored memory and reveal effects at runtime");
}

internal static void WorldSiteRootGatesHostileGarrisonTextBySiteIntel()
{
    string worldSiteRoot = ReadWorldSiteRootSource();
    string overviewMethod = ExtractMethodBlock(worldSiteRoot, "private string BuildSiteOverview");
    string garrisonMethod = ExtractMethodBlock(worldSiteRoot, "private void RefreshGarrisonList");

    AssertTrue(
        overviewMethod.Contains("WorldSiteIntelService.BuildCurrentView", StringComparison.Ordinal),
        "site overview should build a site intel view before displaying garrison text");
    AssertTrue(
        overviewMethod.Contains("BuildSiteGarrisonOverviewText(site, intelView)", StringComparison.Ordinal),
        "site overview should route displayed garrison count through the intel-gated helper");
    AssertTrue(
        !overviewMethod.Contains("site.Garrison.Sum", StringComparison.Ordinal),
        "site overview should not directly sum exact hostile garrison counts for display");
    AssertTrue(
        garrisonMethod.Contains("WorldSiteIntelService.BuildCurrentView", StringComparison.Ordinal),
        "site garrison panel should build a site intel view");
    AssertTrue(
        garrisonMethod.Contains("AddSiteGarrisonLines(_siteGarrisonList, site, intelView)", StringComparison.Ordinal),
        "site garrison panel should route rows through the intel-gated helper");
    AssertTrue(
        !garrisonMethod.Contains("foreach (GarrisonState garrison in site.Garrison)", StringComparison.Ordinal),
        "site garrison panel should not unconditionally iterate exact hostile garrison details");
}

internal static void WorldSiteDeploymentUsesKnownEntrancesBeforeDesiredApproachDirection()
{
    string preparerSource = File.ReadAllText(Path.Combine("src", "Application", "World", "WorldSiteBattleDeploymentPreparer.cs"));
    string resolveForceEntrance = ExtractMethodBlock(preparerSource, "private static BattleEntranceRequest ResolveForceEntrance");
    string preparer = NormalizeWhitespace(preparerSource);

    AssertTrue(
        !resolveForceEntrance.Contains("return desiredDirection == WorldSiteAttackDirection.Any", StringComparison.Ordinal),
        "ResolveForceEntrance should return a known force entrance candidate instead of nulling non-Any hidden desired directions");
    AssertTrue(
        resolveForceEntrance.Contains("return candidates.FirstOrDefault();", StringComparison.Ordinal),
        "ResolveForceEntrance should fall back to a known AvailableEntrances candidate when preferred/exact/Any resolution misses");
    AssertTrue(
        !preparer.Contains("WorldSiteAttackDirection deploymentDirection = entrance != null && entrance.Direction != WorldSiteAttackDirection.Any ? entrance.Direction : desiredDirection;", StringComparison.Ordinal),
        "WorldSiteBattleDeploymentPreparer should not blindly reuse desiredDirection while force entrance candidates exist");
}

internal static void WorldSiteExplorationBattleRequestCarriesExplorationContext()
{
    BattleStartRequest request = WorldSiteExplorationService.BuildExplorationBattleRequest(
        "bonefield",
        "warehouse",
        "",
        new GridSurfacePosition(4, 5, 1),
        alertLevel: 3,
        "res://return.tscn",
        "res://site.tscn");

    AssertEqual(BattleKind.AssaultSite, request.BattleKind, "exploration battle request should enter tactical battle through an existing battle kind for first slice");
    AssertEqual("bonefield", request.TargetSiteId, "exploration battle request should carry target site");
    AssertEqual("site_exploration:warehouse", request.EncounterId, "exploration battle request should carry point encounter id");
    AssertEqual("exploration_cell=4:5:1", request.ObjectiveIds.FirstOrDefault(), "exploration battle request should carry entry cell as stable context");
    AssertTrue(request.ObjectiveIds.Contains("exploration_alert=3"), "exploration battle request should carry alert level");
}

internal static void SiteExplorationTickMovesPartyByExplorationAp()
{
    BattleGridMap gridMap = BuildLineGridMap(0, 2);
    WorldSiteExplorationState exploration = new() { CurrentCellX = 0, CurrentCellY = 0, CurrentCellHeight = 0 };

    bool intentSet = WorldSiteExplorationService.TrySetPartyMoveIntent(
        exploration,
        gridMap,
        new GridPosition(2, 0),
        out IReadOnlyList<GridSurfacePosition> path,
        out string failureReason);

    AssertTrue(intentSet, $"exploration should accept a reachable movement intent failure={failureReason}");
    AssertEqual(3, path.Count, "intent path should include start, middle, and destination");

    SiteExplorationTickResult result = WorldSiteExplorationService.AdvanceTick(
        exploration,
        new WorldSiteDefinition(),
        gridMap,
        partyActionPointRegenPerTick: 1,
        partyMoveCostPerCell: 1);

    AssertTrue(result.PartyMoved, "exploration tick should move party when exploration AP covers one cell");
    AssertEqual(1, exploration.CurrentCellX, "exploration tick should move one cell, not teleport to destination");
    AssertEqual(0, exploration.CurrentCellY, "exploration tick should keep y on line path");
    AssertTrue(exploration.VisitedCellKeys.Contains("1:0:0"), "exploration tick should mark the stepped cell visited");
}

internal static void SiteExplorationTickMovesPatrolByRouteAp()
{
    BattleGridMap gridMap = BuildLineGridMap(3, 4);
    WorldSiteDefinition definition = new()
    {
        ExplorationPatrols =
        {
            new SiteExplorationPatrolDefinition
            {
                Id = "patrol_a",
                DisplayName = "Patrol A",
                AlertRadiusCells = 0,
                ActionPointRegenPerTick = 1,
                MoveCostPerCell = 1,
                RouteCells =
                {
                    new SiteExplorationRouteCellDefinition { CellX = 3, CellY = 0, CellHeight = 0 },
                    new SiteExplorationRouteCellDefinition { CellX = 4, CellY = 0, CellHeight = 0 }
                }
            }
        }
    };
    WorldSiteExplorationState exploration = new() { CurrentCellX = 0, CurrentCellY = 0, CurrentCellHeight = 0, IsSimulationPaused = false };

    WorldSiteExplorationService.EnsurePatrolStates(exploration, definition);
    SiteExplorationTickResult result = WorldSiteExplorationService.AdvanceTick(exploration, definition, gridMap);

    AssertTrue(result.PatrolMoved, "exploration tick should move patrol when route AP covers one cell");
    AssertEqual(4, exploration.PatrolUnits[0].CellX, "patrol should advance to next route cell");
    AssertEqual(1, exploration.PatrolUnits[0].RouteIndex, "patrol route index should advance");
}

internal static void SiteExplorationAlertRadiusPausesSimulation()
{
    BattleGridMap gridMap = BuildLineGridMap(0, 4);
    WorldSiteDefinition definition = new()
    {
        ExplorationPatrols =
        {
            new SiteExplorationPatrolDefinition
            {
                Id = "patrol_alert",
                DisplayName = "Alert Patrol",
                AlertRadiusCells = 2,
                ActionPointRegenPerTick = 0,
                MoveCostPerCell = 1,
                RouteCells =
                {
                    new SiteExplorationRouteCellDefinition { CellX = 4, CellY = 0, CellHeight = 0 }
                }
            }
        }
    };
    WorldSiteExplorationState exploration = new() { CurrentCellX = 2, CurrentCellY = 0, CurrentCellHeight = 0, IsSimulationPaused = false };
    WorldSiteExplorationService.EnsurePatrolStates(exploration, definition);

    SiteExplorationTickResult result = WorldSiteExplorationService.AdvanceTick(exploration, definition, gridMap);

    AssertTrue(result.Paused, "alert radius should pause exploration simulation");
    AssertEqual("patrol_alert", result.AlertPatrolId, "alert result should identify triggering patrol");
    AssertEqual(true, exploration.IsSimulationPaused, "exploration state should persist paused state");
    AssertEqual("exploration_alert_radius", exploration.PauseReason, "pause reason should be stable");
}

internal static void ExplorationBattleRequestCarriesPatrolTrigger()
{
    BattleStartRequest request = WorldSiteExplorationService.BuildExplorationBattleRequest(
        "bonefield",
        "warehouse",
        "bonefield_patrol_01",
        new GridSurfacePosition(4, 5, 1),
        alertLevel: 4,
        "res://return.tscn",
        "res://site.tscn");

    AssertEqual("warehouse", request.ExplorationPointId, "exploration request should carry point id explicitly");
    AssertEqual("bonefield_patrol_01", request.ExplorationTriggerPatrolId, "exploration request should carry patrol trigger explicitly");
    AssertEqual(4, request.ExplorationEntryCellX, "exploration request should carry entry x");
    AssertEqual(5, request.ExplorationEntryCellY, "exploration request should carry entry y");
    AssertEqual(1, request.ExplorationEntryCellHeight, "exploration request should carry entry height");
    AssertEqual(4, request.ExplorationAlertLevel, "exploration request should carry alert level explicitly");
    AssertTrue(request.ObjectiveIds.Contains("exploration_patrol=bonefield_patrol_01"), "exploration request should keep patrol objective compatibility");
}

internal static void ExplorationBattleVictoryRemovesTriggeringPatrol()
{
    const string triggerPatrolId = "bonefield_patrol_01";
    const string triggerPlacementId = "garrison:neutral_shadow1:2";
    const string otherPatrolId = "bonefield_patrol_02";
    const string otherPlacementId = "garrison:neutral_shadow1:1";

    SiteExplorationPatrolDefinition triggerPatrol = new()
    {
        Id = triggerPatrolId,
        DisplayName = "Trigger Patrol",
        UnitTypeId = StrategicWorldIds.UnitGraveShadow,
        SourcePlacementId = triggerPlacementId,
        RouteCells =
        {
            new SiteExplorationRouteCellDefinition { CellX = 1, CellY = 0, CellHeight = 0 }
        }
    };
    StrategicWorldDefinition definition = new()
    {
        Id = "test_world",
        SiteDefinitions =
        {
            new WorldSiteDefinition
            {
                Id = StrategicWorldIds.SiteBonefield,
                DefaultGarrisonZoneId = "bonefield_garrison",
                DeploymentZones =
                {
                    new SiteDeploymentZoneDefinition
                    {
                        ZoneId = "bonefield_garrison",
                        ZoneKind = SiteDeploymentZoneKind.DefaultGarrison,
                        Capacity = 2,
                        Cells =
                        {
                            new Godot.Vector2I(1, 0),
                            new Godot.Vector2I(2, 0)
                        }
                    }
                },
                ExplorationPatrols =
                {
                    triggerPatrol,
                    new SiteExplorationPatrolDefinition
                    {
                        Id = otherPatrolId,
                        DisplayName = "Other Patrol",
                        UnitTypeId = StrategicWorldIds.UnitGraveShadow,
                        SourcePlacementId = otherPlacementId,
                        RouteCells =
                        {
                            new SiteExplorationRouteCellDefinition { CellX = 2, CellY = 0, CellHeight = 0 }
                        }
                    }
                }
            }
        }
    };
    StrategicWorldState state = new()
    {
        PlayerFactionId = StrategicWorldIds.FactionPlayer
    };
    WorldSiteState site = new()
    {
        SiteId = StrategicWorldIds.SiteBonefield,
        Exploration = new WorldSiteExplorationState
        {
            IsSimulationPaused = true,
            PauseReason = "exploration_alert_radius",
            ActiveAlertPatrolId = triggerPatrolId
        }
    };
    site.Exploration.PatrolUnits.Add(new SiteExplorationPatrolState { PatrolId = triggerPatrolId, CellX = 1, CellY = 0, CellHeight = 0 });
    site.Exploration.PatrolUnits.Add(new SiteExplorationPatrolState { PatrolId = otherPatrolId, CellX = 2, CellY = 0, CellHeight = 0 });
    site.Garrison.Add(new GarrisonState { UnitTypeId = StrategicWorldIds.UnitGraveShadow, Count = 2 });
    site.UnitPlacements.Add(new WorldSiteUnitPlacement
    {
        PlacementId = triggerPlacementId,
        UnitTypeId = StrategicWorldIds.UnitGraveShadow,
        UnitIndex = 2,
        FactionId = StrategicWorldIds.FactionUndead,
        PlacementKind = WorldSiteUnitPlacementKind.Garrison,
        SourceKind = "Garrison",
        SourceId = StrategicWorldIds.SiteBonefield,
        CellX = 1,
        CellY = 0,
        CellHeight = 0
    });
    site.UnitPlacements.Add(new WorldSiteUnitPlacement
    {
        PlacementId = otherPlacementId,
        UnitTypeId = StrategicWorldIds.UnitGraveShadow,
        UnitIndex = 1,
        FactionId = StrategicWorldIds.FactionUndead,
        PlacementKind = WorldSiteUnitPlacementKind.Garrison,
        SourceKind = "Garrison",
        SourceId = StrategicWorldIds.SiteBonefield,
        CellX = 2,
        CellY = 0,
        CellHeight = 0
    });
    state.SiteStates[StrategicWorldIds.SiteBonefield] = site;

    BattleStartRequest request = WorldSiteExplorationService.BuildExplorationBattleRequest(
        StrategicWorldIds.SiteBonefield,
        "",
        triggerPatrolId,
        null,
        new[] { triggerPatrol },
        new GridSurfacePosition(1, 0, 0),
        alertLevel: 2,
        "res://return.tscn",
        "res://site.tscn");
    BattleResult result = BuildVictoryResult(request, "site_exploration");
    BattleForceRequest defeatedPatrolForce = request.EnemyForces.Single(force => force.SourceId == triggerPlacementId);
    result.ForceResults.Add(new BattleForceResult
    {
        ForceId = defeatedPatrolForce.ForceId,
        SourceKind = defeatedPatrolForce.SourceKind,
        SourceId = defeatedPatrolForce.SourceId,
        UnitDefinitionId = defeatedPatrolForce.UnitDefinitionId,
        InitialCount = 1,
        SurvivedCount = 0,
        DefeatedCount = 1
    });

    WorldActionResult actionResult = new WorldBattleResultApplier().Apply(
        state,
        definition,
        request,
        result);

    AssertTrue(actionResult.Success, $"exploration encounter result should apply success message={actionResult.Message}");
    AssertTrue(site.Exploration.PatrolUnits[0].IsRemoved, "victory should remove triggering patrol from exploration state");
    AssertTrue(site.Exploration.ResolvedPointIds.Contains("patrol:bonefield_patrol_01"), "victory should record resolved patrol encounter");
    AssertEqual("exploration_encounter_resolved", site.Exploration.PauseReason, "victory should leave stable exploration pause reason");
}

internal static BattleGridMap BuildLineGridMap(int minX, int maxX)
{
    BattleGridMap gridMap = new();
    for (int x = minX; x <= maxX; x++)
    {
        GridCellSurface surface = gridMap.GetOrCreateSurface(new GridPosition(x, 0), 0);
        surface.AddLayer(new GridCellLayerData("test", LayerRole.Foundation, 0, true, false, false, false, true, 1, true, false, "", 0, 0, 0, 0));
    }

    gridMap.RebuildTopSurfaceIndex();
    return gridMap;
}
}
