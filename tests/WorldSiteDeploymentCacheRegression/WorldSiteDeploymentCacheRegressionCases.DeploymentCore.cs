using Godot;
using Rpg.Application.Battle;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;

internal static partial class WorldSiteDeploymentCacheRegressionCases
{
internal static void DeploymentCacheOrdersDirectionsAndPreservesWater()
{
    BattleGridMap grid = new();
    AddWalkableSurface(grid, 1, 1, terrainTag: "center");
    AddWalkableSurface(grid, 1, 0, terrainTag: "north");
    AddWalkableSurface(grid, 1, 2, terrainTag: "south");
    AddWalkableSurface(grid, 0, 1, terrainTag: "west");
    AddWalkableSurface(grid, 2, 1, terrainTag: "east");
    AddWalkableSurface(grid, 3, 1, terrainTag: "water");

    WorldSiteRuntimeDeploymentCache cache = new WorldSiteRuntimeDeploymentCacheBuilder().Build("site_under_test", grid);

    AssertEqual(new Vector2I(1, 0), cache.GetCandidates(WorldSiteAttackDirection.North)[0].Cell, "north should start from lowest y");
    AssertEqual(new Vector2I(1, 2), cache.GetCandidates(WorldSiteAttackDirection.South)[0].Cell, "south should start from highest y");
    AssertEqual(new Vector2I(0, 1), cache.GetCandidates(WorldSiteAttackDirection.West)[0].Cell, "west should start from lowest x");
    AssertEqual(new Vector2I(3, 1), cache.GetCandidates(WorldSiteAttackDirection.East)[0].Cell, "east should start from highest x");
    AssertEqual(new Vector2I(1, 1), cache.GetCandidates(WorldSiteAttackDirection.Any)[0].Cell, "any should start near center");

    WorldSiteDeploymentCell water = cache.GetCandidates(WorldSiteAttackDirection.Any)
        .Single(item => item.Cell == new Vector2I(3, 1));
    AssertTrue(water.IsWater, "water terrain candidate should preserve IsWater");
    AssertEqual("water", water.TerrainTag, "water terrain tag should be preserved");
}

internal static void DeploymentCacheExcludesInvalidSurfacesAndKeepsTopSurface()
{
    BattleGridMap grid = new();
    AddWalkableSurface(grid, 4, 4, height: 0, terrainTag: "lower");
    AddWalkableSurface(grid, 4, 4, height: 2, terrainTag: "upper");
    AddNonWalkableFoundation(grid, 5, 5, terrainTag: "blocked");
    AddFoundationWithoutWalkability(grid, 6, 6, terrainTag: "zero_cost");

    WorldSiteRuntimeDeploymentCache cache = new WorldSiteRuntimeDeploymentCacheBuilder().Build("site_under_test", grid);
    IReadOnlyList<WorldSiteDeploymentCell> any = cache.GetCandidates(WorldSiteAttackDirection.Any);

    AssertTrue(any.Any(item => item.Cell == new Vector2I(4, 4) && item.Height == 2), "top walkable surface should be included");
    AssertTrue(!any.Any(item => item.Cell == new Vector2I(4, 4) && item.Height == 0), "non-top surface should be excluded");
    AssertTrue(!any.Any(item => item.Cell == new Vector2I(5, 5)), "non-walkable surface should be excluded");
    AssertTrue(!any.Any(item => item.Cell == new Vector2I(6, 6)), "foundation without positive move cost should be excluded");
}

internal static void DeploymentCacheHandlesMissingGridAsEmptyCache()
{
    WorldSiteRuntimeDeploymentCache cache = new WorldSiteRuntimeDeploymentCacheBuilder().Build("missing_grid_site", null);

    AssertEqual("missing_grid_site", cache.SiteId, "empty cache should keep site id");
    AssertEqual(0, cache.CandidateSurfaceCount, "missing grid should have zero candidate surfaces");
    AssertEqual(0, cache.GetCandidates(WorldSiteAttackDirection.Any).Count, "any should be empty");
    AssertEqual(0, cache.GetCandidates(WorldSiteAttackDirection.North).Count, "north should be empty");
    AssertEqual(0, cache.GetCandidates(WorldSiteAttackDirection.South).Count, "south should be empty");
    AssertEqual(0, cache.GetCandidates(WorldSiteAttackDirection.West).Count, "west should be empty");
    AssertEqual(0, cache.GetCandidates(WorldSiteAttackDirection.East).Count, "east should be empty");
}

internal static void DeploymentCacheFallsBackToAnyDirection()
{
    WorldSiteDeploymentCell fallback = new(new Vector2I(7, 8), 1, "fallback", false);
    WorldSiteRuntimeDeploymentCache cache = new(
        "site_under_test",
        candidateSurfaceCount: 1,
        new Dictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>>
        {
            [WorldSiteAttackDirection.Any] = new[] { fallback }
        });

    AssertEqual(fallback, cache.GetCandidates(WorldSiteAttackDirection.North)[0], "missing direction should fall back to any candidates");
}

internal static void DeploymentTargetEvaluatorRejectsBlockedWaterAndOccupiedCells()
{
    BattleGridMap grid = new();
    AddWalkableSurface(grid, 0, 0, terrainTag: "plain");
    AddWalkableSurface(grid, 1, 0, terrainTag: "water");
    AddNonWalkableFoundation(grid, 2, 0, terrainTag: "blocked");
    AddWalkableSurface(grid, 3, 0, terrainTag: "plain");

    WorldSiteState site = BuildDeploymentSite();
    site.UnitPlacements.Add(new WorldSiteUnitPlacement { PlacementId = "unit:1", UnitTypeId = "militia", CellX = 0, CellY = 0 });
    site.UnitPlacements.Add(new WorldSiteUnitPlacement { PlacementId = "unit:2", UnitTypeId = "militia", CellX = 3, CellY = 0 });

    WorldSiteDeploymentTargetEvaluator evaluator = new();

    AssertTrue(
        !evaluator.CanMoveToGridCell(grid, site, new WorldSiteDefinition(), "unit:1", new Vector2I(2, 0), CanPlacementEnterWater, out string blockedReason),
        "blocked cell should reject placement move");
    AssertEqual("placement_cell_blocked", blockedReason, "blocked reason");

    AssertTrue(
        !evaluator.CanMoveToGridCell(grid, site, new WorldSiteDefinition(), "unit:1", new Vector2I(1, 0), CanPlacementEnterWater, out string waterReason),
        "water should reject non-water placement move");
    AssertEqual("placement_cell_water", waterReason, "water reason");

    AssertTrue(
        !evaluator.CanMoveToGridCell(grid, site, new WorldSiteDefinition(), "unit:1", new Vector2I(3, 0), CanPlacementEnterWater, out string occupiedReason),
        "occupied cell should reject placement move");
    AssertEqual("placement_cell_occupied", occupiedReason, "occupied reason");
}

internal static void DeploymentTargetEvaluatorMovesPlacementThroughDeploymentService()
{
    BattleGridMap grid = new();
    AddWalkableSurface(grid, 0, 0, terrainTag: "plain");
    AddWalkableSurface(grid, 1, 0, terrainTag: "plain");

    WorldSiteState site = BuildDeploymentSite();
    WorldSiteUnitPlacement placement = new() { PlacementId = "unit:1", UnitTypeId = "militia", CellX = 0, CellY = 0 };
    site.UnitPlacements.Add(placement);

    WorldSiteDeploymentTargetEvaluator evaluator = new();
    bool moved = evaluator.TryMoveToGridCell(
        grid,
        site,
        new WorldSiteDefinition(),
        "unit:1",
        new Vector2I(1, 0),
        CanPlacementEnterWater,
        out string failureReason);

    AssertTrue(moved, $"valid placement move should succeed failure={failureReason}");
    AssertEqual(1, placement.CellX, "placement x should update through deployment service");
    AssertEqual(0, placement.CellY, "placement y should update through deployment service");
}

internal static void DeploymentTerrainReconcilerSyncsPlacementHeight()
{
    BattleGridMap grid = new();
    AddWalkableSurface(grid, 2, 0, height: 2, terrainTag: "upper_plain");
    WorldSiteRuntimeDeploymentCache cache = new WorldSiteRuntimeDeploymentCacheBuilder().Build("site_under_test", grid);
    WorldSiteState site = BuildDeploymentSite();
    WorldSiteUnitPlacement placement = new() { PlacementId = "unit:1", UnitTypeId = "militia", CellX = 2, CellY = 0, CellHeight = 0 };
    site.UnitPlacements.Add(placement);

    WorldSiteDeploymentTerrainReconcileResult result = new WorldSiteDeploymentTerrainReconciler()
        .Reconcile(grid, cache, site, new WorldSiteDefinition(), CanPlacementEnterWater);

    AssertTrue(result.Success, $"height sync should succeed reason={result.LastFailureReason}");
    AssertEqual(1, result.HeightSynced, "height sync count");
    AssertEqual(2, placement.CellHeight, "placement height should sync to top surface");
}

internal static void DeploymentTerrainReconcilerRelocatesBlockedNonWaterPlacement()
{
    BattleGridMap grid = new();
    AddWalkableSurface(grid, 0, 0, terrainTag: "water");
    AddWalkableSurface(grid, 1, 0, terrainTag: "plain");
    WorldSiteRuntimeDeploymentCache cache = new WorldSiteRuntimeDeploymentCacheBuilder().Build("site_under_test", grid);
    WorldSiteState site = BuildDeploymentSite();
    WorldSiteDefinition definition = BuildDefaultZoneDefinition(new Vector2I(1, 0));
    WorldSiteUnitPlacement placement = new()
    {
        PlacementId = "unit:1",
        UnitTypeId = "militia",
        CellX = 0,
        CellY = 0,
        ZoneId = WorldSiteDeploymentService.DefaultGarrisonZoneId
    };
    site.UnitPlacements.Add(placement);

    WorldSiteDeploymentTerrainReconcileResult result = new WorldSiteDeploymentTerrainReconciler()
        .Reconcile(grid, cache, site, definition, CanPlacementEnterWater);

    AssertTrue(result.Success, $"relocation should succeed reason={result.LastFailureReason}");
    AssertEqual(1, result.Relocated, "relocation count");
    AssertEqual(1, placement.CellX, "relocated placement x");
    AssertEqual(0, placement.CellY, "relocated placement y");
}

internal static void BattleDeploymentPreparerCreatesSitePlacementsAndForcePreferredPlacements()
{
    BattleGridMap grid = new();
    AddWalkableSurface(grid, 0, 0, terrainTag: "west");
    AddWalkableSurface(grid, 2, 0, terrainTag: "east");
    WorldSiteRuntimeDeploymentCache cache = new WorldSiteRuntimeDeploymentCacheBuilder().Build("site_under_test", grid);
    WorldSiteState site = BuildDeploymentSite();
    WorldSiteDefinition definition = new() { Id = "site_under_test" };
    BattleStartRequest request = new()
    {
        TargetSiteId = site.SiteId,
        BattleKind = BattleKind.AssaultSite,
        AttackDirection = WorldSiteAttackDirection.East,
        AttackerFactionId = "player"
    };
    BattleForceRequest playerForce = new()
    {
        ForceId = "player_force",
        SourceKind = "PlayerArmy",
        SourceId = "army_1",
        UnitDefinitionId = "militia",
        Count = 1,
        FactionId = "player"
    };
    BattleForceRequest enemyForce = new()
    {
        ForceId = "enemy_force",
        SourceKind = "ThreatArmy",
        SourceId = "threat_1",
        UnitDefinitionId = "militia",
        Count = 1,
        FactionId = "enemy"
    };
    request.PlayerForces.Add(playerForce);
    request.EnemyForces.Add(enemyForce);

    bool prepared = new WorldSiteBattleDeploymentPreparer().Prepare(
        request,
        site,
        definition,
        cache,
        grid,
        CanForceEnterWater,
        CanPlacementEnterWater,
        out string failureReason);

    AssertTrue(prepared, $"deployment preparation should succeed failure={failureReason}");
    AssertEqual(2, site.UnitPlacements.Count, "created placement count");
    AssertEqual(1, playerForce.PreferredPlacements.Count, "player preferred placement count");
    AssertEqual(1, enemyForce.PreferredPlacements.Count, "enemy preferred placement count");
    AssertEqual(2, playerForce.PreferredPlacements[0].CellX, "attacker should use east candidate");
    AssertEqual(0, enemyForce.PreferredPlacements[0].CellX, "defender should use west candidate");
}

internal static void BattleDeploymentPreparerUsesKnownEntranceBeforeDesiredApproachDirection()
{
    BattleGridMap grid = new();
    AddWalkableSurface(grid, 0, 0, terrainTag: "west");
    AddWalkableSurface(grid, 2, 0, terrainTag: "east");
    WorldSiteRuntimeDeploymentCache cache = new WorldSiteRuntimeDeploymentCacheBuilder().Build("site_under_test", grid);
    WorldSiteState site = BuildDeploymentSite();
    BattleStartRequest request = new()
    {
        TargetSiteId = site.SiteId,
        BattleKind = BattleKind.AssaultSite,
        AttackDirection = WorldSiteAttackDirection.East,
        AttackerFactionId = "player"
    };
    request.AvailableEntrances.Add(new BattleEntranceRequest
    {
        EntranceId = "known_west_gate",
        Direction = WorldSiteAttackDirection.West,
        FactionId = "player"
    });
    BattleForceRequest playerForce = new()
    {
        ForceId = "player_force",
        SourceKind = "PlayerArmy",
        SourceId = "army_1",
        UnitDefinitionId = "militia",
        Count = 1,
        FactionId = "player"
    };
    request.PlayerForces.Add(playerForce);

    bool prepared = new WorldSiteBattleDeploymentPreparer().Prepare(
        request,
        site,
        new WorldSiteDefinition { Id = "site_under_test" },
        cache,
        grid,
        CanForceEnterWater,
        CanPlacementEnterWater,
        out string failureReason);

    AssertTrue(prepared, $"known entrance deployment should succeed failure={failureReason}");
    AssertEqual("known_west_gate", playerForce.PreferredEntranceId, "force preferred entrance should use known entrance");
    AssertEqual(0, playerForce.PreferredPlacements[0].CellX, "known west entrance should override east desired direction");
}
}
