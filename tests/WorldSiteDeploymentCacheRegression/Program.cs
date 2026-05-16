using Godot;
using Rpg.Application.Battle;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;

System.Environment.SetEnvironmentVariable(
    "RPG_GAMELOG_DIR",
    Path.Combine(Path.GetTempPath(), "rpg-world-site-deployment-cache-tests"));

Run("deployment cache orders directions and preserves water", DeploymentCacheOrdersDirectionsAndPreservesWater);
Run("deployment cache excludes invalid surfaces and keeps only top surface", DeploymentCacheExcludesInvalidSurfacesAndKeepsTopSurface);
Run("deployment cache handles missing grid as empty cache", DeploymentCacheHandlesMissingGridAsEmptyCache);
Run("deployment cache falls back to any direction", DeploymentCacheFallsBackToAnyDirection);
Run("deployment target evaluator rejects blocked water and occupied cells", DeploymentTargetEvaluatorRejectsBlockedWaterAndOccupiedCells);
Run("deployment target evaluator moves placement through deployment service", DeploymentTargetEvaluatorMovesPlacementThroughDeploymentService);
Run("deployment terrain reconciler syncs placement height", DeploymentTerrainReconcilerSyncsPlacementHeight);
Run("deployment terrain reconciler relocates blocked non-water placement", DeploymentTerrainReconcilerRelocatesBlockedNonWaterPlacement);
Run("battle deployment preparer creates site placements and force preferred placements", BattleDeploymentPreparerCreatesSitePlacementsAndForcePreferredPlacements);
Run("battle deployment preparer uses known entrance before desired approach direction", BattleDeploymentPreparerUsesKnownEntranceBeforeDesiredApproachDirection);
Run("battle launcher cancels handoff and restores site on activation failure", BattleLauncherCancelsHandoffAndRestoresSiteOnActivationFailure);
Run("world site root delegates deployment cache construction", WorldSiteRootDelegatesDeploymentCacheConstruction);
Run("world site root delegates deployment target validation", WorldSiteRootDelegatesDeploymentTargetValidation);
Run("world site root delegates deployment terrain reconciliation", WorldSiteRootDelegatesDeploymentTerrainReconciliation);
Run("world site root delegates battle deployment preparation", WorldSiteRootDelegatesBattleDeploymentPreparation);
Run("world site root delegates battle launch handoff", WorldSiteRootDelegatesBattleLaunchHandoff);
Run("world site root uses auto battle activation through adapter", WorldSiteRootUsesAutoBattleActivationThroughAdapter);
Run("world site root appends auto battle report summary to notice", WorldSiteRootAppendsAutoBattleReportSummaryToNotice);
Run("legacy manual battle authority docs stay deleted", LegacyManualBattleAuthorityDocsStayDeleted);
Run("world site root has no dead auto battle runtime switch", WorldSiteRootHasNoDeadAutoBattleRuntimeSwitch);
Run("world site root detaches legacy manual battle runtime", WorldSiteRootDetachesLegacyManualBattleRuntime);
Run("world site scene detaches legacy manual battle runtime", WorldSiteSceneDetachesLegacyManualBattleRuntime);
Run("legacy manual battle code files are deleted", LegacyManualBattleCodeFilesAreDeleted);
Run("legacy combat AP authoring fields are deleted", LegacyCombatApAuthoringFieldsAreDeleted);

static void DeploymentCacheOrdersDirectionsAndPreservesWater()
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

static void DeploymentCacheExcludesInvalidSurfacesAndKeepsTopSurface()
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

static void DeploymentCacheHandlesMissingGridAsEmptyCache()
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

static void DeploymentCacheFallsBackToAnyDirection()
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

static void DeploymentTargetEvaluatorRejectsBlockedWaterAndOccupiedCells()
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

static void DeploymentTargetEvaluatorMovesPlacementThroughDeploymentService()
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

static void DeploymentTerrainReconcilerSyncsPlacementHeight()
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

static void DeploymentTerrainReconcilerRelocatesBlockedNonWaterPlacement()
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

static void BattleDeploymentPreparerCreatesSitePlacementsAndForcePreferredPlacements()
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

static void BattleDeploymentPreparerUsesKnownEntranceBeforeDesiredApproachDirection()
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

static void BattleLauncherCancelsHandoffAndRestoresSiteOnActivationFailure()
{
    BattleSessionHandoff.CancelBattle();
    StrategicWorldState state = new() { WorldTick = 7 };
    WorldSiteState site = BuildDeploymentSite();
    site.SiteMode = WorldSiteMode.Alert;
    site.Exploration = new WorldSiteExplorationState
    {
        IsSimulationPaused = true,
        PauseReason = "exploration_ready",
        ActiveAlertPatrolId = "patrol_a"
    };
    site.Exploration.PendingPathCellKeys.Add("1:2:0");
    state.SiteStates[site.SiteId] = site;

    WorldSiteBattleLauncher launcher = new();
    WorldSiteBattleLaunchRollback rollback = launcher.CaptureRollback(site);
    site.SiteMode = WorldSiteMode.Wartime;
    site.Exploration.IsSimulationPaused = false;
    site.Exploration.PauseReason = "exploration_battle";
    site.Exploration.ActiveAlertPatrolId = "patrol_b";
    site.Exploration.PendingPathCellKeys.Clear();

    int applyCalls = 0;
    int cleanupCalls = 0;
    int runtimeDisableCalls = 0;
    WorldSiteBattleLaunchResult result = launcher.BeginAndActivate(
        state,
        new BattleStartRequest { RequestId = "request_under_test", TargetSiteId = site.SiteId },
        rollback,
        () => applyCalls++,
        () => false,
        () => "activation_blocked",
        () => cleanupCalls++,
        () => cleanupCalls++,
        enabled =>
        {
            if (!enabled)
            {
                runtimeDisableCalls++;
            }
        });

    AssertTrue(!result.Success, "failed activation should return failure");
    AssertEqual("activation_blocked", result.FailureReason, "failure reason");
    AssertTrue(!BattleSessionHandoff.HasActiveLaunch, "failed launch should cancel active handoff");
    AssertEqual(1, applyCalls, "apply start request call count");
    AssertEqual(2, cleanupCalls, "cleanup callbacks should run during rollback");
    AssertEqual(1, runtimeDisableCalls, "runtime should be disabled during rollback");
    AssertEqual(WorldSiteMode.Alert, site.SiteMode, "site mode should be restored");
    AssertTrue(site.Exploration.IsSimulationPaused, "exploration pause should be restored");
    AssertEqual("exploration_ready", site.Exploration.PauseReason, "exploration pause reason should be restored");
    AssertEqual("patrol_a", site.Exploration.ActiveAlertPatrolId, "active alert patrol should be restored");
    AssertEqual("1:2:0", site.Exploration.PendingPathCellKeys[0], "pending path should be restored");

    BattleSessionHandoff.CancelBattle();
}

static GridCellSurface AddWalkableSurface(
    BattleGridMap grid,
    int x,
    int y,
    int height = 0,
    string terrainTag = "plain")
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
        walkable: true,
        moveCost: 1,
        canStandOn: true,
        isObstacle: false,
        terrainTag,
        sourceId: 0,
        atlasX: 0,
        atlasY: 0,
        alternativeTile: 0));
    return surface;
}

static void WorldSiteRootDelegatesDeploymentCacheConstruction()
{
    string rootSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites", "WorldSiteRoot.cs"));
    string evaluatorSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Application", "World", "WorldSiteDeploymentTargetEvaluator.cs"));

    AssertTrue(
        !rootSource.Contains("private sealed class WorldSiteRuntimeDeploymentCache", StringComparison.Ordinal),
        "WorldSiteRoot should not own the deployment cache value object");
    AssertTrue(
        !rootSource.Contains("OrderDeploymentSurfaceCandidates", StringComparison.Ordinal),
        "WorldSiteRoot should not own deployment candidate ordering");
    AssertTrue(
        rootSource.Contains("_deploymentCacheBuilder.Build", StringComparison.Ordinal),
        "WorldSiteRoot should build deployment cache through WorldSiteRuntimeDeploymentCacheBuilder");
    AssertTrue(
        evaluatorSource.Contains("WorldSiteRuntimeDeploymentCacheBuilder.IsDeploymentCandidateSurface", StringComparison.Ordinal),
        "WorldSiteDeploymentTargetEvaluator placement validation should reuse builder candidate filtering");
}

static void WorldSiteRootDelegatesDeploymentTargetValidation()
{
    string rootSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites", "WorldSiteRoot.cs"));

    AssertTrue(
        rootSource.Contains("_deploymentTargetEvaluator.CanMoveToGridCell", StringComparison.Ordinal),
        "WorldSiteRoot should delegate placement target validation to WorldSiteDeploymentTargetEvaluator");
    AssertTrue(
        rootSource.Contains("_deploymentTargetEvaluator.TryMoveToGridCell", StringComparison.Ordinal),
        "WorldSiteRoot should delegate placement movement writes to WorldSiteDeploymentTargetEvaluator");
    AssertTrue(
        !rootSource.Contains("private bool CanPlaceSiteDeploymentOnGridCell", StringComparison.Ordinal),
        "WorldSiteRoot should not own placement grid-cell validation");
}

static void WorldSiteRootDelegatesDeploymentTerrainReconciliation()
{
    string rootSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites", "WorldSiteRoot.cs"));

    AssertTrue(
        rootSource.Contains("_deploymentTerrainReconciler.Reconcile", StringComparison.Ordinal),
        "WorldSiteRoot should delegate placement terrain reconciliation");
    AssertTrue(
        !rootSource.Contains("private bool TryRelocatePlacementForTerrain", StringComparison.Ordinal),
        "WorldSiteRoot should not own terrain relocation wrapper");
    AssertTrue(
        !rootSource.Contains("private bool CanUsePlacementSurface", StringComparison.Ordinal),
        "WorldSiteRoot should not own placement surface validation");
    AssertTrue(
        !rootSource.Contains("private IReadOnlyList<WorldSiteDeploymentCell> BuildRelocationCandidates", StringComparison.Ordinal),
        "WorldSiteRoot should not own relocation candidate ordering");
    AssertTrue(
        !rootSource.Contains("private bool TryGetPlacementSurface", StringComparison.Ordinal),
        "WorldSiteRoot should not own placement surface lookup");
}

static void WorldSiteRootDelegatesBattleDeploymentPreparation()
{
    string rootSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites", "WorldSiteRoot.cs"));

    AssertTrue(
        rootSource.Contains("_battleDeploymentPreparer.Prepare", StringComparison.Ordinal),
        "WorldSiteRoot should delegate battle deployment preparation");
    AssertTrue(
        !rootSource.Contains("private bool EnsureForceWorldSitePlacement", StringComparison.Ordinal),
        "WorldSiteRoot should not own force placement preparation");
    AssertTrue(
        !rootSource.Contains("private bool ApplyPreferredPlacementsFromWorldSite", StringComparison.Ordinal),
        "WorldSiteRoot should not own force preferred placement projection");
    AssertTrue(
        !rootSource.Contains("private static BattleEntranceRequest ResolveForceEntrance", StringComparison.Ordinal),
        "WorldSiteRoot should not own force entrance resolution");
}

static void WorldSiteRootDelegatesBattleLaunchHandoff()
{
    string rootSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites", "WorldSiteRoot.cs"));

    AssertTrue(
        rootSource.Contains("_battleLauncher.BeginAndActivate", StringComparison.Ordinal),
        "WorldSiteRoot should delegate battle handoff and failed activation rollback");
    AssertTrue(
        rootSource.Contains("_battleLauncher.CaptureRollback", StringComparison.Ordinal),
        "WorldSiteRoot should capture battle launch rollback through WorldSiteBattleLauncher");
    AssertTrue(
        !rootSource.Contains("private sealed class SiteBattleLaunchRollback", StringComparison.Ordinal),
        "WorldSiteRoot should not own battle launch rollback DTO");
    AssertTrue(
        !rootSource.Contains("private void RollbackSiteBattleLaunch", StringComparison.Ordinal),
        "WorldSiteRoot should not own battle launch rollback");
    AssertTrue(
        !rootSource.Contains("private static void ApplyModeTransitionRollbackEvent", StringComparison.Ordinal),
        "WorldSiteRoot should not own mode transition rollback extraction");
}

static void WorldSiteRootUsesAutoBattleActivationThroughAdapter()
{
    string rootSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites", "WorldSiteRoot.cs"));

    AssertTrue(
        !rootSource.Contains("UseAutoBattleRuntime", StringComparison.Ordinal),
        "WorldSiteRoot should not keep the dead auto battle runtime switch after manual fallback removal");
    AssertTrue(
        rootSource.Contains("_autoBattleAdapter.TryResolveActiveBattle", StringComparison.Ordinal),
        "WorldSiteRoot should delegate auto battle handoff resolution to WorldSiteAutoBattleAdapter");
    AssertTrue(
        rootSource.Contains("ActivateAutoBattleRuntime", StringComparison.Ordinal),
        "WorldSiteRoot should keep auto battle activation in a focused helper");
    AssertTrue(
        !rootSource.Contains("_turnController?.StartBattle()", StringComparison.Ordinal),
        "WorldSiteRoot should not keep the legacy manual battle activation path");
    AssertTrue(
        !rootSource.Contains("new AutoBattleRuntimeController", StringComparison.Ordinal),
        "WorldSiteRoot should not instantiate or own AutoBattleRuntimeController directly");
}

static void LegacyManualBattleAuthorityDocsStayDeleted()
{
    string root = ProjectRoot();
    string[] deletedDocs =
    {
        Path.Combine(root, "docs", "20-game-design", "tactical-battle", "mechanism-battle-slice.md"),
        Path.Combine(root, "docs", "20-game-design", "tactical-battle", "battle-ui-interaction-review.md"),
        Path.Combine(root, "docs", "20-game-design", "tactical-battle", "enemy-intent-design.md"),
        Path.Combine(root, "docs", "20-game-design", "tactical-battle", "battle-demo-undead-commander.md"),
        Path.Combine(root, "docs", "30-technical-design", "battle", "battle-action-architecture.md"),
        Path.Combine(root, "docs", "30-technical-design", "battle", "battle-input-command-architecture.md"),
        Path.Combine(root, "docs", "30-technical-design", "battle", "battle-runtime-responsibility-review.md"),
        Path.Combine(root, "docs", "30-technical-design", "battle", "intent-system.md"),
        Path.Combine(root, "docs", "30-technical-design", "battle", "card-system.md"),
        Path.Combine(root, "docs", "30-technical-design", "battle", "targeting-and-preview.md"),
        Path.Combine(root, "docs", "40-content", "tutorial", "tutorial-battle.md"),
        Path.Combine(root, "docs", "60-qa", "testcases", "phase1-core-prototype.md"),
        Path.Combine(root, "docs", "50-production", "technical-changes", "2026-04-29-battle-intent-system.md"),
        Path.Combine(root, "docs", "50-production", "technical-changes", "2026-05-08-battle-action-menu.md"),
        Path.Combine(root, "docs", "50-production", "technical-changes", "2026-05-08-battle-player-action-order.md"),
        Path.Combine(root, "docs", "50-production", "technical-changes", "2026-05-13-battle-action-cue.md")
    };

    foreach (string path in deletedDocs)
    {
        AssertTrue(!File.Exists(path), $"stale manual battle authority should be deleted path={path}");
    }

    string gameplayReadme = File.ReadAllText(Path.Combine(root, "docs", "20-game-design", "tactical-battle", "README.md"));
    string technicalReadme = File.ReadAllText(Path.Combine(root, "docs", "30-technical-design", "battle", "README.md"));
    string combined = gameplayReadme + "\n" + technicalReadme;
    AssertTrue(!combined.Contains("mechanism-battle-slice", StringComparison.Ordinal), "gameplay README should not route to manual mechanism slice");
    AssertTrue(!combined.Contains("battle-ui-interaction-review", StringComparison.Ordinal), "gameplay README should not route to manual action menu review");
    AssertTrue(!combined.Contains("enemy-intent-design", StringComparison.Ordinal), "gameplay README should not route to old turn intent design");
    AssertTrue(!combined.Contains("battle-action-architecture", StringComparison.Ordinal), "technical README should not route to AP action architecture");
    AssertTrue(!combined.Contains("battle-input-command-architecture", StringComparison.Ordinal), "technical README should not route to manual command architecture");
    AssertTrue(!combined.Contains("battle-runtime-responsibility-review", StringComparison.Ordinal), "technical README should not route to manual runtime review");
    AssertTrue(!combined.Contains("intent-system", StringComparison.Ordinal), "technical README should not route to legacy intent system");
    AssertTrue(!combined.Contains("card-system", StringComparison.Ordinal), "technical README should not route to legacy AP card system");
    AssertTrue(!combined.Contains("targeting-and-preview", StringComparison.Ordinal), "technical README should not route to manual targeting preview vocabulary");

    string sceneArchitecture = File.ReadAllText(Path.Combine(root, "docs", "30-technical-design", "battle", "battle-scene-architecture.md"));
    AssertTrue(!sceneArchitecture.Contains("Current manual-map flow", StringComparison.Ordinal), "battle scene architecture should not describe manual map flow as current authority");
    AssertTrue(!sceneArchitecture.Contains("BattleCommandController", StringComparison.Ordinal), "battle scene architecture should not route future work through manual command controller");
    AssertTrue(sceneArchitecture.Contains("hero-led light RTS", StringComparison.OrdinalIgnoreCase), "battle scene architecture should route future work to the accepted light RTS direction");
}

static void WorldSiteRootHasNoDeadAutoBattleRuntimeSwitch()
{
    string rootSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites", "WorldSiteRoot.cs"));

    AssertTrue(
        !rootSource.Contains("UseAutoBattleRuntime", StringComparison.Ordinal),
        "WorldSiteRoot should not expose a runtime toggle when the legacy manual fallback is gone");
    AssertTrue(
        rootSource.Contains("_autoBattleAdapter.TryResolveActiveBattle", StringComparison.Ordinal),
        "WorldSiteRoot should still delegate auto handoff resolution to WorldSiteAutoBattleAdapter");
    AssertTrue(
        !rootSource.Contains("_turnController?.StartBattle()", StringComparison.Ordinal),
        "manual battle start should be removed after scene dependencies are detached");
}

static void WorldSiteRootDetachesLegacyManualBattleRuntime()
{
    string rootSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites", "WorldSiteRoot.cs"));
    string[] forbiddenFragments =
    {
        "HudRootPath",
        "InputRouterPath",
        "CommandControllerPath",
        "TurnControllerPath",
        "IntentControllerPath",
        "PreviewControllerPath",
        "BattleHudRoot",
        "BattleInputRouter",
        "BattleCommandController",
        "BattleTurnController",
        "BattleIntentController",
        "BattlePreviewController",
        "BattleActionExecutor",
        "ExecuteActionRequest",
        "CreateActionExecutionContext",
        "CreateAiContext",
        "ShowBattleEntityInHud",
        "OnTurnQueueUpdated",
        "OnBattleEnded",
        "MarkBattleStateChanged"
    };

    foreach (string fragment in forbiddenFragments)
    {
        AssertTrue(!rootSource.Contains(fragment, StringComparison.Ordinal), $"WorldSiteRoot should not retain legacy manual battle runtime fragment={fragment}");
    }

    AssertTrue(
        rootSource.Contains("ActivateAutoBattleRuntime", StringComparison.Ordinal),
        "WorldSiteRoot should keep only the auto battle activation path");
}

static void WorldSiteSceneDetachesLegacyManualBattleRuntime()
{
    string sceneSource = File.ReadAllText(Path.Combine(ProjectRoot(), "scenes", "world", "sites", "WorldSiteRoot.tscn"));
    string[] forbiddenFragments =
    {
        "BattleHudRoot",
        "BattleInputRouter",
        "BattleCommandController",
        "BattleTurnController",
        "BattleIntentController",
        "BattlePreviewController"
    };

    foreach (string fragment in forbiddenFragments)
    {
        AssertTrue(!sceneSource.Contains(fragment, StringComparison.Ordinal), $"WorldSiteRoot.tscn should not wire legacy manual battle node={fragment}");
    }
}

static void LegacyManualBattleCodeFilesAreDeleted()
{
    string root = ProjectRoot();
    string[] deletedFiles =
    {
        Path.Combine(root, "src", "Presentation", "Battle", "Flow", "BattleCommandController.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Flow", "BattleCommandController.cs.uid"),
        Path.Combine(root, "src", "Presentation", "Battle", "Flow", "BattleTurnController.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Flow", "BattleTurnController.cs.uid"),
        Path.Combine(root, "src", "Presentation", "Battle", "Flow", "BattleIntentController.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Flow", "BattleIntentController.cs.uid"),
        Path.Combine(root, "src", "Presentation", "Battle", "Input", "BattleCommand.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Input", "BattleCommand.cs.uid"),
        Path.Combine(root, "src", "Presentation", "Battle", "Input", "BattleCommandKind.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Input", "BattleCommandKind.cs.uid"),
        Path.Combine(root, "src", "Presentation", "Battle", "Input", "BattleInputRouter.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Input", "BattleInputRouter.cs.uid"),
        Path.Combine(root, "src", "Presentation", "Battle", "UI", "BattleHudRoot.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "UI", "BattleHudRoot.cs.uid"),
        Path.Combine(root, "src", "Presentation", "Battle", "UI", "BattleActionMenu.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "UI", "BattleActionMenu.cs.uid"),
        Path.Combine(root, "src", "Presentation", "Battle", "UI", "BattleActionMenuButton.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "UI", "BattleActionMenuButton.cs.uid"),
        Path.Combine(root, "src", "Presentation", "Battle", "UI", "BattleActionMenuCommandViewModel.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "UI", "BattleActionMenuCommandViewModel.cs.uid"),
        Path.Combine(root, "src", "Presentation", "Battle", "UI", "BattleTurnQueueEntry.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "UI", "BattleTurnQueueEntry.cs.uid"),
        Path.Combine(root, "src", "Presentation", "Battle", "UI", "BattleTurnQueueItem.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "UI", "BattleTurnQueueItem.cs.uid"),
        Path.Combine(root, "src", "Presentation", "Battle", "UI", "CommandInfoPanel.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "UI", "CommandInfoPanel.cs.uid"),
        Path.Combine(root, "src", "Presentation", "Battle", "UI", "FloatingActionHint.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "UI", "FloatingActionHint.cs.uid"),
        Path.Combine(root, "src", "Presentation", "Battle", "UI", "TopTurnBar.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "UI", "TopTurnBar.cs.uid"),
        Path.Combine(root, "src", "Presentation", "Battle", "UI", "UnitStatusCard.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "UI", "UnitStatusCard.cs.uid"),
        Path.Combine(root, "src", "Presentation", "Battle", "Preview", "BattlePreviewController.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Preview", "BattlePreviewController.cs.uid"),
        Path.Combine(root, "src", "Presentation", "Battle", "Entities", "ActionPointComponent.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Entities", "ActionPointComponent.cs.uid"),
        Path.Combine(root, "scenes", "battle", "ui", "BattleHudContent.tscn"),
        Path.Combine(root, "scenes", "battle", "ui", "BattleActionMenu.tscn"),
        Path.Combine(root, "scenes", "battle", "ui", "BattleActionMenuButton.tscn"),
        Path.Combine(root, "scenes", "battle", "ui", "BattleTurnQueueItem.tscn"),
        Path.Combine(root, "scenes", "battle", "ui", "CommandInfoPanel.tscn"),
        Path.Combine(root, "scenes", "battle", "ui", "FloatingActionHint.tscn"),
        Path.Combine(root, "scenes", "battle", "ui", "TopTurnBar.tscn"),
        Path.Combine(root, "scenes", "battle", "ui", "UnitStatusCard.tscn"),
        Path.Combine(root, "scenes", "battle", "ui", "BattleActionDock.tscn"),
        Path.Combine(root, "scenes", "battle", "ui", "ActionWheelSlot.tscn")
    };

    foreach (string path in deletedFiles)
    {
        AssertTrue(!File.Exists(path), $"legacy manual battle code/resource should be deleted path={path}");
    }
}

static void LegacyCombatApAuthoringFieldsAreDeleted()
{
    string root = ProjectRoot();
    string[] sourceFiles =
    {
        Path.Combine(root, "src", "Definitions", "Battle", "BattleUnitDefinition.cs"),
        Path.Combine(root, "src", "Definitions", "Battle", "Abilities", "AbilityDefinition.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Entities", "MovementComponent.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Entities", "AttackComponent.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Entities", "BattleUnitFactory.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Entities", "BattleUnitRoot.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Actions", "BattleActionExecutor.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Intents", "BattleIntentResolver.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Threats", "BattleThreatProjectionBuilder.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Debug", "BattleCellInfoDebug.cs")
    };
    string[] forbiddenSourceFragments =
    {
        "MaxActionPoints",
        "MoveActionPointCost",
        "AttackActionPointCost",
        "ApCost",
        "MaxMoveUsesPerTurn",
        "MoveUsesRemaining",
        "CanUseMove",
        "TryUseMove",
        "RestoreMoveUses",
        "RestoreTurnResourcesForFaction"
    };

    foreach (string file in sourceFiles)
    {
        string text = File.ReadAllText(file);
        foreach (string fragment in forbiddenSourceFragments)
        {
            AssertTrue(!text.Contains(fragment, StringComparison.Ordinal), $"battle authoring source should not keep legacy combat AP field={fragment} file={file}");
        }
    }

    string unitRoot = Path.Combine(root, "assets", "battle", "units");
    foreach (string file in Directory.EnumerateFiles(unitRoot, "unit.tres", SearchOption.AllDirectories))
    {
        string text = File.ReadAllText(file);
        foreach (string fragment in forbiddenSourceFragments)
        {
            AssertTrue(!text.Contains(fragment, StringComparison.Ordinal), $"battle unit resource should not serialize legacy combat AP field={fragment} file={file}");
        }
    }
}

static void WorldSiteRootAppendsAutoBattleReportSummaryToNotice()
{
    string rootSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites", "WorldSiteRoot.cs"));

    AssertTrue(
        rootSource.Contains("AutoBattleReportSummaryFormatter", StringComparison.Ordinal),
        "WorldSiteRoot auto branch should use the application-layer auto battle report summary formatter");
    AssertTrue(
        rootSource.Contains("BuildAutoBattleReturnNotice", StringComparison.Ordinal),
        "WorldSiteRoot should keep notice composition in a focused helper");
    AssertTrue(
        rootSource.Contains("autoBattleNotice", StringComparison.Ordinal),
        "WorldSiteRoot should pass the auto battle summary notice into existing non-battle UI refresh");
    AssertTrue(
        !rootSource.Contains("AutoBattleReportPanel", StringComparison.Ordinal),
        "this slice should not add a full report panel to WorldSiteRoot");
}

static string ProjectRoot()
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

static GridCellSurface AddNonWalkableFoundation(
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

static GridCellSurface AddFoundationWithoutWalkability(
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

static WorldSiteState BuildDeploymentSite()
{
    return new WorldSiteState
    {
        SiteId = "site_under_test",
        OwnerFactionId = "player"
    };
}

static WorldSiteDefinition BuildDefaultZoneDefinition(params Vector2I[] cells)
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

static bool CanPlacementEnterWater(WorldSiteUnitPlacement placement)
{
    return string.Equals(placement?.UnitTypeId, "boat", StringComparison.OrdinalIgnoreCase);
}

static bool CanForceEnterWater(BattleForceRequest force)
{
    return string.Equals(force?.UnitDefinitionId, "boat", StringComparison.OrdinalIgnoreCase);
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
        throw new InvalidOperationException($"{message}: expected={expected} actual={actual}");
    }
}
