using Godot;
using Rpg.Application.Battle;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;

internal static partial class WorldSiteDeploymentCacheRegressionCases
{
internal static void HeroCorpsV0PlayableSliceUsesAuthoredUnitResources()
{
    string root = ProjectRoot();
    string idsSource = File.ReadAllText(Path.Combine(root, "src", "Application", "World", "HeroCorpsV0PlayableSliceIds.cs"));
    string initialState = File.ReadAllText(Path.Combine(root, "assets", "definitions", "world", "strategic_world_v1_initial_state.tres"));

    AssertTrue(idsSource.Contains("HeroUnit = \"f1_grandmasterzir\"", StringComparison.Ordinal), "v0 hero id should be grandmaster Zir");
    AssertTrue(idsSource.Contains("DefaultCorpsUnit = \"f1_azuritelion\"", StringComparison.Ordinal), "v0 default corps id should be azurite lion");
    AssertTrue(idsSource.Contains("EnemyLeaderUnit = \"f6_draugarlord\"", StringComparison.Ordinal), "v0 enemy leader id should be draugar lord");
    AssertTrue(idsSource.Contains("DefaultCorpsCount = 1", StringComparison.Ordinal), "v0 should deploy one player corps case unit");
    AssertTrue(initialState.Contains("res://assets/battle/units/莱昂纳王国/f1_宗师Zir/unit.tres", StringComparison.Ordinal), "initial player hero should use authored grandmaster Zir resource");
    AssertTrue(initialState.Contains("res://assets/battle/units/霜原部盟/f6_Draugar领主/unit.tres", StringComparison.Ordinal), "initial enemy leader should use authored draugar lord resource");
    AssertUnitFootprint("assets/battle/units/莱昂纳王国/f1_天蓝石狮/unit.tres", 2, 1, "azurite lion corps");
    AssertUnitFootprint("assets/battle/units/莱昂纳王国/f1_宗师Zir/unit.tres", 2, 1, "grandmaster Zir hero");
    AssertUnitFootprint("assets/battle/units/霜原部盟/f6_Draugar领主/unit.tres", 2, 2, "draugar lord enemy hero");
    AssertUnitMaxHp("assets/battle/units/莱昂纳王国/f1_天蓝石狮/unit.tres", 48, "azurite lion corps");
    AssertUnitMaxHp("assets/battle/units/莱昂纳王国/f1_宗师Zir/unit.tres", 48, "grandmaster Zir hero");
    AssertUnitMaxHp("assets/battle/units/霜原部盟/f6_Draugar领主/unit.tres", 48, "draugar lord enemy hero");
}

private static void AssertUnitFootprint(string relativePath, int expectedWidth, int expectedHeight, string label)
{
    string unitText = File.ReadAllText(Path.Combine(ProjectRoot(), relativePath));
    AssertTrue(unitText.Contains($"FootprintWidth = {expectedWidth}", StringComparison.Ordinal), $"{label} footprint width");
    AssertTrue(unitText.Contains($"FootprintHeight = {expectedHeight}", StringComparison.Ordinal), $"{label} footprint height");
}

private static void AssertUnitMaxHp(string relativePath, int expectedMaxHp, string label)
{
    string unitText = File.ReadAllText(Path.Combine(ProjectRoot(), relativePath));
    AssertTrue(unitText.Contains($"MaxHp = {expectedMaxHp}", StringComparison.Ordinal), $"{label} max hp");
}

internal static void HeroCorpsV0AssaultRequestUsesPlayerHeroCorpsAndEnemyLeader()
{
    StrategicWorldDefinition definition = StrategicWorldV1DefinitionFactory.Create(loadInitialStateResource: false);
    StrategicWorldState state = BuildHeroCorpsV0AssaultState(definition, "army_v0");

    BattleStartRequest request = new WorldBattleRequestBuilder().BuildAssaultBonefieldRequest(
        state,
        definition,
        "res://scenes/world/StrategicWorldRoot.tscn",
        "res://scenes/world/sites/WorldSiteRoot.tscn",
        "army_v0");

    AssertEqual(2, request.PlayerForces.Count, "v0 assault should contain one hero force and one default corps force");
    AssertTrue(
        request.PlayerForces.Any(force => force.UnitDefinitionId == HeroCorpsV0PlayableSliceIds.HeroUnit && force.Count == 1),
        "v0 assault should read the player hero from the source army");
    AssertTrue(
        request.PlayerForces.Any(force => force.UnitDefinitionId == HeroCorpsV0PlayableSliceIds.DefaultCorpsUnit && force.Count == HeroCorpsV0PlayableSliceIds.DefaultCorpsCount),
        "v0 assault should read the attached default corps from the source army");
    AssertTrue(
        request.EnemyForces.Any(force => force.UnitDefinitionId == HeroCorpsV0PlayableSliceIds.EnemyLeaderUnit && force.Count == 1),
        "v0 assault should read the enemy leader from the target site garrison");
    AssertTrue(
        request.EnemyForces.All(force => force.UnitDefinitionId != HeroCorpsV0PlayableSliceIds.HeroUnit),
        "v0 assault should not mirror the player hero into enemy forces");
}

internal static void HeroCorpsV0AssaultImportsArmyIntoTargetSiteUnitPoolOnce()
{
    const string armyId = "army_v0";
    StrategicWorldDefinition definition = StrategicWorldV1DefinitionFactory.Create(loadInitialStateResource: false);
    StrategicWorldState state = BuildHeroCorpsV0AssaultState(definition, armyId);
    WorldSiteState site = state.SiteStates[StrategicWorldIds.SiteBonefield];

    BattleStartRequest request = new WorldBattleRequestBuilder().BuildAssaultBonefieldRequest(
        state,
        definition,
        "res://scenes/world/StrategicWorldRoot.tscn",
        "res://scenes/world/sites/WorldSiteRoot.tscn",
        armyId);
    BattleResult battleResult = BuildAssaultVictoryResult(request);
    new WorldBattleResultApplier().Apply(state, definition, request, battleResult);

    AssertEqual(
        1,
        site.Garrison.Where(garrison =>
            garrison.UnitTypeId == HeroCorpsV0PlayableSliceIds.HeroUnit &&
            garrison.SourceKind == "PlayerArmy" &&
            garrison.SourceId == armyId).Sum(garrison => garrison.Count),
        "target site pool should contain exactly one imported hero after victory");
    AssertEqual(
        HeroCorpsV0PlayableSliceIds.DefaultCorpsCount,
        site.Garrison.Where(garrison =>
            garrison.UnitTypeId == HeroCorpsV0PlayableSliceIds.DefaultCorpsUnit &&
            garrison.SourceKind == "PlayerArmy" &&
            garrison.SourceId == armyId).Sum(garrison => garrison.Count),
        "target site pool should contain exactly the imported default corps after victory");
    AssertEqual(
        0,
        site.Garrison.Where(garrison =>
            garrison.UnitTypeId == HeroCorpsV0PlayableSliceIds.EnemyLeaderUnit &&
            garrison.FactionId == StrategicWorldIds.FactionUndead).Sum(garrison => garrison.Count),
        "target site pool should remove defeated defender units after victory");
}

internal static void AssaultVictoryClearsResolvedVisitingArmyPlacements()
{
    const string armyId = "army_v0";
    StrategicWorldDefinition definition = StrategicWorldV1DefinitionFactory.Create(loadInitialStateResource: false);
    StrategicWorldState state = BuildHeroCorpsV0AssaultState(definition, armyId);
    WorldSiteState site = state.SiteStates[StrategicWorldIds.SiteBonefield];
    site.UnitPlacements.Add(BuildVisitingArmyPlacement(armyId, HeroCorpsV0PlayableSliceIds.HeroUnit, 1, 1, 1));
    site.UnitPlacements.Add(BuildVisitingArmyPlacement(armyId, HeroCorpsV0PlayableSliceIds.DefaultCorpsUnit, 2, 2, 1));
    site.UnitPlacements.Add(BuildVisitingArmyPlacement(armyId, HeroCorpsV0PlayableSliceIds.DefaultCorpsUnit, 3, 3, 1));

    BattleStartRequest request = new WorldBattleRequestBuilder().BuildAssaultBonefieldRequest(
        state,
        definition,
        "res://scenes/world/StrategicWorldRoot.tscn",
        "res://scenes/world/sites/WorldSiteRoot.tscn",
        armyId);
    BattleResult battleResult = BuildAssaultVictoryResult(request);

    WorldActionResult result = new WorldBattleResultApplier().Apply(state, definition, request, battleResult);

    AssertTrue(result.Success, "assault victory should apply successfully");
    AssertTrue(
        !site.UnitPlacements.Any(placement =>
            placement.SourceKind == "PlayerArmy" &&
            placement.SourceId == armyId &&
            placement.PlacementKind is WorldSiteUnitPlacementKind.VisitingArmy or WorldSiteUnitPlacementKind.Attacker),
        "assault victory should clear resolved visiting/attacker placement rows");
    AssertTrue(
        site.UnitPlacements.Count(placement =>
            WorldSiteDeploymentService.IsGarrisonPlacement(placement) &&
            placement.UnitTypeId == HeroCorpsV0PlayableSliceIds.HeroUnit) == 1,
        "assault victory should leave exactly one hero garrison placement");
    AssertTrue(
        site.UnitPlacements.Count(placement =>
            WorldSiteDeploymentService.IsGarrisonPlacement(placement) &&
            placement.UnitTypeId == HeroCorpsV0PlayableSliceIds.DefaultCorpsUnit) == HeroCorpsV0PlayableSliceIds.DefaultCorpsCount,
        "assault victory should leave exactly the default corps as garrison placements");
}

internal static void StrategicWorldInvariantRepairRemovesResolvedArmyPlacements()
{
    const string armyId = "army_v0";
    StrategicWorldDefinition definition = StrategicWorldV1DefinitionFactory.Create(loadInitialStateResource: false);
    StrategicWorldState state = BuildHeroCorpsV0AssaultState(definition, armyId);
    WorldSiteState site = state.SiteStates[StrategicWorldIds.SiteBonefield];
    site.UnitPlacements.Add(BuildVisitingArmyPlacement(armyId, HeroCorpsV0PlayableSliceIds.HeroUnit, 1, 1, 1));
    site.UnitPlacements.Add(BuildVisitingArmyPlacement(armyId, HeroCorpsV0PlayableSliceIds.DefaultCorpsUnit, 2, 2, 1));
    state.ArmyStates[armyId].Status = WorldArmyStatus.Garrisoned;
    state.ArmyStates[armyId].GarrisonUnits.Clear();

    int removed = new StrategicWorldStateInvariantService().RepairResolvedArmyPlacements(state);

    AssertEqual(2, removed, "resolved army placements should be removed");
    AssertTrue(
        !site.UnitPlacements.Any(placement => placement.SourceKind == "PlayerArmy" && placement.SourceId == armyId),
        "state repair should leave no stale player army placements");
}

internal static void StrategicWorldV0ExpeditionSelectsHeroOnly()
{
    string rootSource = ReadStrategicWorldRootSource();

    AssertTrue(
        rootSource.Contains("HeroCorpsV0PlayableSliceIds.HeroUnit", StringComparison.Ordinal),
        "StrategicWorldRoot should filter expedition drafting to the v0 hero");
    AssertTrue(
        rootSource.Contains("AttachDefaultCorpsToHeroExpedition", StringComparison.Ordinal),
        "StrategicWorldRoot should attach default corps after hero expedition creation");
    AssertTrue(
        rootSource.Contains("默认兵团", StringComparison.Ordinal),
        "expedition panel should present the default corps as read-only v0 information");
    AssertTrue(
        !rootSource.Contains("WorldExpeditionIssued army={army.ArmyId} intent={intent} target={targetSiteId}\");\n        if (intent == WorldArmyIntent.AssaultSite)", StringComparison.Ordinal) &&
        !rootSource.Contains("进入战前部署", StringComparison.Ordinal),
        "assault expedition should not bypass world travel immediately after the world army is created");
    AssertTrue(
        rootSource.Contains("WorldArmyIntent.AssaultSite => $\"已从{sourceName}派出{expeditionText}进攻{targetText}。\"", StringComparison.Ordinal),
        "assault expedition should clearly issue an attack order instead of entering battle immediately");
    AssertTrue(
        !rootSource.Contains("选择出征英雄和小兵", StringComparison.Ordinal),
        "v0 expedition player text should not ask for troop composition editing");
}

internal static void WorldSiteRootGatesBattleStartBehindDeployment()
{
    string rootSource = ReadWorldSiteRootSource();

    AssertTrue(
        rootSource.Contains("EnterBattlePreparation", StringComparison.Ordinal),
        "WorldSiteRoot should enter battle preparation before runtime activation");
    AssertTrue(
        rootSource.Contains("RefreshBattlePreparationUi", StringComparison.Ordinal),
        "WorldSiteRoot should expose a battle preparation UI state");
    AssertTrue(
        rootSource.Contains("ClearPlayerBattlePreparationPlacements(request)", StringComparison.Ordinal) &&
        rootSource.Contains("BattlePreparationPlayerPlacementsCleared", StringComparison.Ordinal),
        "battle preparation should start player units from the request-backed roster instead of pre-spreading them on the map");
    AssertTrue(
        rootSource.Contains("AddBattlePreparationRosterButtons", StringComparison.Ordinal) &&
        rootSource.Contains("BeginBattlePreparationRosterDrag", StringComparison.Ordinal),
        "battle preparation should expose player force slots as draggable request-backed roster entries");
    AssertTrue(
        rootSource.Contains("开战\\n确认部署并进入实时战斗", StringComparison.Ordinal),
        "battle preparation should show an explicit start battle button");
    AssertTrue(
        rootSource.Contains("RegisterBattlePreparationPlacement", StringComparison.Ordinal),
        "battle placements should be registered during preparation");
    AssertTrue(
        rootSource.Contains("preview and runtime start positions cannot drift apart", StringComparison.Ordinal) &&
        rootSource.Contains("_sitePlacementEntities[placementId] = entity", StringComparison.Ordinal),
        "battle preparation should keep enemy and player preview entities indexed from prepared placements");
    AssertTrue(
        rootSource.Contains("ShowBattlePreparationDeploymentZone", StringComparison.Ordinal) &&
        rootSource.Contains("BattleGridHighlightKind.FriendlyMove", StringComparison.Ordinal),
        "battle preparation should highlight the player deployment zone");
    AssertTrue(
        rootSource.Contains("ActivateBattleRuntime();", StringComparison.Ordinal),
        "start battle should still commit into the existing runtime after preparation");
    AssertTrue(
        rootSource.Contains("SetBattleRuntimeEnabled(true);", StringComparison.Ordinal) &&
        rootSource.Contains("Preparation can start runtime directly after the player confirms deployment", StringComparison.Ordinal),
        "runtime activation should own the preparation-to-battle UI transition so the deployment roster closes after start battle");
    AssertTrue(
        rootSource.Contains("runtimeEvent.HasMovementCells", StringComparison.Ordinal) &&
        rootSource.Contains("runtimeEvent.ToGridX", StringComparison.Ordinal) &&
        rootSource.Contains("runtimeEvent.ToGridY", StringComparison.Ordinal) &&
        !rootSource.Contains("TryResolveRuntimeVisualPathStep", StringComparison.Ordinal) &&
        !rootSource.Contains("MovementRangeFinder.FindReachableCells", StringComparison.Ordinal),
        "runtime event playback should consume authoritative runtime cells instead of presentation pathfinding");
}

internal static void WorldSiteRootBattleRuntimeCommandUiRoutesInitialCommand()
{
    string root = ProjectRoot();
    string rootSource = ReadWorldSiteRootSource();
    string battlePreparationSource = File.ReadAllText(Path.Combine(
        root,
        "src",
        "Presentation",
        "World",
        "Sites",
        "WorldSiteRoot.BattlePreparationHud.cs"));
    string battleRuntimeSource = ReadWorldSiteRootSource();
    string siteScene = File.ReadAllText(Path.Combine(
        root,
        "scenes",
        "world",
        "ui",
        "WorldSitePeacetimeHud.tscn"));
    string requestSource = File.ReadAllText(Path.Combine(root, "src", "Application", "Battle", "BattleStartRequest.cs"));
    string snapshotSource = File.ReadAllText(Path.Combine(root, "src", "Application", "Battle", "Snapshots", "BattleGroupSnapshot.cs"));
    string probeSource = File.ReadAllText(Path.Combine(root, "src", "Application", "Battle", "BattleGroupSessionProbeService.cs"));

    AssertTrue(
        siteScene.Contains("BottomCommandHost", StringComparison.Ordinal) &&
        siteScene.Contains("BattleRuntimeCommandBar", StringComparison.Ordinal) &&
        siteScene.Contains("BattleRuntimeCommandButtonRow", StringComparison.Ordinal),
        "world site HUD scene should author a bottom battle command bar instead of building the runtime UI tree ad hoc");
    AssertTrue(
        rootSource.Contains("_siteBottomCommandHost", StringComparison.Ordinal) &&
        rootSource.Contains("_battleRuntimeCommandLabel", StringComparison.Ordinal) &&
        rootSource.Contains("_battleRuntimeCommandButtonRow", StringComparison.Ordinal),
        "WorldSiteRoot should bind authored runtime command UI nodes");
    AssertTrue(
        !battlePreparationSource.Contains("RefreshBattleRuntimeCommandControls", StringComparison.Ordinal),
        "battle preparation should keep runtime command buttons out of the pre-start action list");
    AssertTrue(
        battleRuntimeSource.Contains("BindBattleRuntimeHud", StringComparison.Ordinal) &&
        battleRuntimeSource.Contains("ShowBattleRuntimeCommandHud(runtimeLocked: true)", StringComparison.Ordinal) &&
        battleRuntimeSource.IndexOf("UpdateSitePeacetimePanelVisibility(\"battle_runtime\")", StringComparison.Ordinal) <
        battleRuntimeSource.IndexOf("ShowBattleRuntimeCommandHud(runtimeLocked: true)", StringComparison.Ordinal) &&
        battleRuntimeSource.Contains("RefreshBattleRuntimeCommandControls", StringComparison.Ordinal) &&
        battleRuntimeSource.Contains("CommandRequest", StringComparison.Ordinal) &&
        battleRuntimeSource.Contains("BuildBattleRuntimeCommandRequest", StringComparison.Ordinal) &&
        battleRuntimeSource.Contains("BattleRuntimeCommandSelected", StringComparison.Ordinal),
        "battle runtime HUD should show the bottom command bar after mode visibility refreshes, route player intent through a CommandRequest-shaped boundary, and leave a low-noise diagnostic");
    AssertTrue(
        requestSource.Contains("InitialCorpsCommandId", StringComparison.Ordinal) &&
        snapshotSource.Contains("InitialCorpsCommandId", StringComparison.Ordinal) &&
        probeSource.Contains("InitialCorpsCommandId", StringComparison.Ordinal),
        "initial corps command should be copied from legacy battle request into the target battle snapshot");
}

internal static void WorldSiteRuntimePauseCommandUiSelectsHeroCompany()
{
    string root = ProjectRoot();
    string rootSource = ReadWorldSiteRootSource();
    string siteScene = File.ReadAllText(Path.Combine(
        root,
        "scenes",
        "world",
        "ui",
        "WorldSitePeacetimeHud.tscn"));
    string worldSiteScene = File.ReadAllText(Path.Combine(
        root,
        "scenes",
        "world",
        "sites",
        "WorldSiteRoot.tscn"));

    AssertTrue(
        siteScene.Contains("BattleRuntimeHeroButtonRow", StringComparison.Ordinal) &&
        siteScene.Contains("BattleRuntimeCommandPanel", StringComparison.Ordinal) &&
        siteScene.Contains("BattleRuntimeHeroCommandList", StringComparison.Ordinal) &&
        siteScene.Contains("BattleRuntimeCorpsCommandList", StringComparison.Ordinal) &&
        siteScene.Contains("BattleRuntimeCombinedCommandList", StringComparison.Ordinal),
        "battle runtime command UI should be authored as bottom hero selection plus left hero/corps/combined command containers.");
    AssertTrue(
        rootSource.Contains("TryHandleBattleRuntimePauseInput", StringComparison.Ordinal) &&
        rootSource.Contains("Key.Space", StringComparison.Ordinal) &&
        rootSource.Contains("_battleRuntimeCommandPauseActive", StringComparison.Ordinal) &&
        rootSource.Contains("ToggleBattleRuntimeCommandPause", StringComparison.Ordinal),
        "WorldSiteRoot should treat Space as a presentation-only battle command pause toggle.");
    AssertTrue(
        rootSource.Contains("RefreshBattleRuntimeHeroBar", StringComparison.Ordinal) &&
        rootSource.Contains("SelectBattleRuntimeCommandGroup", StringComparison.Ordinal) &&
        rootSource.Contains("ResolveBattleRuntimeGroupKey", StringComparison.Ordinal) &&
        rootSource.Contains("SourceKind", StringComparison.Ordinal) &&
        rootSource.Contains("SourceId", StringComparison.Ordinal),
        "battle runtime hero buttons should group request forces by their shared source so a hero and attached corps select together.");
    AssertTrue(
        rootSource.Contains("SetCommandSelectionByEntityIds", StringComparison.Ordinal) &&
        rootSource.Contains("RefreshBattleRuntimeSelectedCommandPanel", StringComparison.Ordinal) &&
        rootSource.Contains("AddBattleRuntimeCommandDraftButton", StringComparison.Ordinal),
        "selecting a runtime hero company should highlight matching units and bind separate placeholder command sections without mutating runtime truth.");
    AssertTrue(
        worldSiteScene.Contains("UnitMoveDuration = 0.16", StringComparison.Ordinal),
        "runtime unit movement should be slowed in the site scene for readable realtime command evaluation.");
}

internal static void BattlePreparationKeepsStartBattlePrimary()
{
    string battlePreparationSource = File.ReadAllText(Path.Combine(
        ProjectRoot(),
        "src",
        "Presentation",
        "World",
        "Sites",
        "WorldSiteRoot.BattlePreparationHud.cs"));
    string refreshActionsBody = ExtractMethodBody(battlePreparationSource, "private void RefreshBattlePreparationActions()");

    AssertTrue(
        !refreshActionsBody.Contains("RefreshBattleRuntimeCommandControls", StringComparison.Ordinal) &&
        refreshActionsBody.Contains("LaunchPreparedBattle", StringComparison.Ordinal),
        "battle preparation action list should keep Start Battle as the primary action; runtime command buttons belong after battle starts.");
}

internal static void WorldSiteRootBattlePreparationDragUsesTemporaryHighZAndSurfaceSort()
{
    string rootSource = ReadWorldSiteRootSource();

    AssertTrue(
        rootSource.Contains("DeploymentDragZIndex", StringComparison.Ordinal) &&
        rootSource.Contains("RaiseDeploymentDragEntity", StringComparison.Ordinal),
        "deployment drag should have an explicit high-Z presentation state");
    AssertTrue(
        rootSource.Contains("entity.ZAsRelative = false", StringComparison.Ordinal) &&
        rootSource.Contains("entity.ZIndex = DeploymentDragZIndex", StringComparison.Ordinal),
        "dragged deployment entities should be raised above terrain layers while following the pointer");
    AssertTrue(
        rootSource.Contains("RaiseDeploymentDragEntity(entity)", StringComparison.Ordinal) &&
        rootSource.Contains("RaiseDeploymentDragEntity(_draggedBattleRosterEntity)", StringComparison.Ordinal),
        "existing placements and roster-spawned drag previews should both use the high-Z drag state");
    AssertTrue(
        rootSource.Contains("RestoreDeploymentEntityRenderSort", StringComparison.Ordinal) &&
        rootSource.Contains("ResolveEntitySurfaceHeight(gridOccupant)", StringComparison.Ordinal) &&
        rootSource.Contains("ApplyEntityRenderSort(battleEntity, gridOccupant.SurfacePosition)", StringComparison.Ordinal),
        "drag completion should restore entity render sort from the grid surface hit by placement");
    AssertTrue(
        rootSource.Contains("RestoreDeploymentEntityRenderSort(draggedEntity)", StringComparison.Ordinal) &&
        rootSource.Contains("SyncSitePlacementGridOccupant(battleEntity, movedPlacement)", StringComparison.Ordinal),
        "drag cancel and successful drop should both leave the visible entity sorted by its final surface");
}

internal static void WorldSiteRootBattlePreparationDragPreviewUsesFootprint()
{
    string rootSource = ReadWorldSiteRootSource();
    string highlightSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "Battle", "BattleGridHighlightOverlay.cs"));

    AssertTrue(
        rootSource.Contains("BuildBattlePreparationFootprintCells(force, gridPosition)", StringComparison.Ordinal) &&
        rootSource.Contains("SetBattlePreparationDragFootprintPreview", StringComparison.Ordinal),
        "battle preparation roster drag preview should derive highlighted cells from the dragged unit footprint");
    AssertTrue(
        rootSource.Contains("IsBattlePreparationFootprintDeployable", StringComparison.Ordinal) &&
        rootSource.Contains("IsBattlePreparationFootprintOccupied", StringComparison.Ordinal),
        "battle preparation drop validation should use the same footprint cells as the preview");
    AssertTrue(
        rootSource.Contains("_highlightOverlay?.SetCells(BattleGridHighlightKind.Hover, cells)", StringComparison.Ordinal) &&
        rootSource.Contains("_highlightOverlay?.ClearCells(BattleGridHighlightKind.Hover)", StringComparison.Ordinal),
        "deployment drag should resize the existing hover selection frame for the full footprint and clear it when dragging ends");
    AssertTrue(
        !rootSource.Contains("_highlightOverlay?.SetCells(BattleGridHighlightKind.Selected", StringComparison.Ordinal) &&
        !rootSource.Contains("_highlightOverlay?.SetCells(BattleGridHighlightKind.Invalid", StringComparison.Ordinal),
        "deployment drag should not add selected or invalid tile-fill preview layers");
    AssertTrue(
        highlightSource.Contains("_hoverCells", StringComparison.Ordinal) &&
        highlightSource.Contains("_hoverOverrideActive", StringComparison.Ordinal) &&
        highlightSource.Contains("BuildHoverFramePolygon", StringComparison.Ordinal),
        "hover selection overlay should support an explicit multi-cell footprint frame");
    AssertTrue(
        !highlightSource.Contains("yield return BattleGridHighlightKind.Selected;", StringComparison.Ordinal),
        "selected footprint cells should not be added to the tile-layer draw order for deployment drag preview");
}

internal static void WorldSiteRootBattlePreparationDragSnapsEntityToFootprintCenter()
{
    string rootSource = ReadWorldSiteRootSource();
    string gridOccupantSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "Battle", "Entities", "GridOccupantComponent.cs"));
    string factorySource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "Battle", "Entities", "BattleUnitFactory.cs"));

    AssertTrue(
        rootSource.Contains("TryResolveMouseFootprintAnchor", StringComparison.Ordinal) &&
        rootSource.Contains("BattleFootprintCells.ResolveAnchorFromCenter", StringComparison.Ordinal),
        "deployment drag should resolve the top-left anchor by the dragged footprint center, not by the raw hovered cell");
    AssertTrue(
        rootSource.Contains("SetDeploymentDragEntityToFootprintCenter", StringComparison.Ordinal) &&
        rootSource.Contains("TryGetFootprintCenterGlobalPosition", StringComparison.Ordinal),
        "deployment drag should snap the visible entity to the same footprint center used by the hover frame");
    AssertTrue(
        !rootSource.Contains("entity.GlobalPosition = GetWorldViewportMousePosition()", StringComparison.Ordinal) &&
        !rootSource.Contains("_draggedBattleRosterEntity.GlobalPosition = GetWorldViewportMousePosition()", StringComparison.Ordinal),
        "dragged unit visuals should not move freely inside the same footprint anchor");
    AssertTrue(
        gridOccupantSource.Contains("public int FootprintWidth { get; set; } = 1;", StringComparison.Ordinal) &&
        gridOccupantSource.Contains("public int FootprintHeight { get; set; } = 1;", StringComparison.Ordinal) &&
        factorySource.Contains("gridOccupant.FootprintWidth = definition.FootprintWidth;", StringComparison.Ordinal) &&
        factorySource.Contains("gridOccupant.FootprintHeight = definition.FootprintHeight;", StringComparison.Ordinal),
        "presentation grid occupants should carry footprint size so placed entities can be centered on m*n cells");
    AssertTrue(
        !factorySource.Contains("animatedSprite.Offset = visual.Offset;", StringComparison.Ordinal) &&
        factorySource.Contains("new Vector2(0f, visual.Offset.Y)", StringComparison.Ordinal),
        "unit visual layout should allow vertical visual tuning while preserving horizontal center alignment");
}

internal static void WorldSiteRootBattlePreparationDragPreviewDoesNotResetIdle()
{
    string rootSource = ReadWorldSiteRootSource();
    string beginDragBody = ExtractMethodBody(rootSource, "private void BeginBattlePreparationRosterDrag(");
    string handleDragBody = ExtractMethodBody(rootSource, "private void HandleBattlePreparationRosterDragInput(");
    string previewBody = ExtractMethodBody(rootSource, "private void UpdateBattlePreparationRosterDragPreview(");
    int mouseReleaseBranchIndex = handleDragBody.IndexOf("if (@event is not InputEventMouseButton", StringComparison.Ordinal);
    AssertTrue(mouseReleaseBranchIndex >= 0, "battle preparation roster drag input should keep mouse release handling explicit");
    string mouseMotionBody = handleDragBody.Substring(0, mouseReleaseBranchIndex);

    AssertTrue(
        beginDragBody.Contains("RemoveBattlePreparationPlacementEntity(", StringComparison.Ordinal),
        "re-dragging a roster unit that was already deployed should remove only that unit's old preview entity");
    AssertTrue(
        !beginDragBody.Contains("RefreshBattlePreparationMapEntities();", StringComparison.Ordinal),
        "starting a roster drag should not rebuild all battle-preparation entities because that restarts existing idle animations");
    AssertTrue(
        !beginDragBody.Contains("PlayIdle()", StringComparison.Ordinal),
        "the temporary roster drag entity should rely on UnitAnimationComponent attachment for its initial idle and should not replay it");
    AssertTrue(
        !mouseMotionBody.Contains("PlayIdle()", StringComparison.Ordinal) &&
        !mouseMotionBody.Contains("RefreshBattlePreparationMapEntities", StringComparison.Ordinal) &&
        !mouseMotionBody.Contains("RefreshBattlePreparationUi", StringComparison.Ordinal),
        "mouse motion during roster deployment drag should only move preview state, not rebuild UI or restart animations");
    AssertTrue(
        !previewBody.Contains("PlayIdle()", StringComparison.Ordinal) &&
        !previewBody.Contains("RefreshBattlePreparationMapEntities", StringComparison.Ordinal) &&
        !previewBody.Contains("RefreshBattlePreparationUi", StringComparison.Ordinal),
        "deployment drag preview updates should keep animation playback untouched while the footprint anchor changes");
}

internal static void WorldSiteRootBattlePreparationUsesDedicatedUiContainers()
{
    string rootSource = ReadWorldSiteRootSource();
    string managementSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites", "WorldSiteRoot.SiteManagementHud.cs"));
    string battlePreparationSource = File.ReadAllText(
        Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites", "WorldSiteRoot.BattlePreparationHud.cs"));
    string peacetimeHudSource = File.ReadAllText(Path.Combine(ProjectRoot(), "scenes", "world", "ui", "WorldSitePeacetimeHud.tscn"));
    string tacticalWorldHudSource = File.ReadAllText(Path.Combine(ProjectRoot(), "scenes", "world", "ui", "StrategicWorldHud.tscn"));

    AssertTrue(
        tacticalWorldHudSource.Contains("node name=\"TopBarHost\"", StringComparison.Ordinal) &&
        tacticalWorldHudSource.Contains("node name=\"LeftPrimaryPanelHost\"", StringComparison.Ordinal) &&
        tacticalWorldHudSource.Contains("node name=\"RightNotificationHost\"", StringComparison.Ordinal) &&
        tacticalWorldHudSource.Contains("node name=\"MinimapHost\"", StringComparison.Ordinal) &&
        tacticalWorldHudSource.Contains("node name=\"BottomCommandHost\"", StringComparison.Ordinal) &&
        tacticalWorldHudSource.Contains("node name=\"OverlayHost\"", StringComparison.Ordinal) &&
        tacticalWorldHudSource.Contains("node name=\"ModalHost\"", StringComparison.Ordinal) &&
        peacetimeHudSource.Contains("node name=\"TopBarHost\"", StringComparison.Ordinal) &&
        peacetimeHudSource.Contains("node name=\"LeftPrimaryPanelHost\"", StringComparison.Ordinal) &&
        peacetimeHudSource.Contains("node name=\"RightNotificationHost\"", StringComparison.Ordinal) &&
        peacetimeHudSource.Contains("node name=\"MinimapHost\"", StringComparison.Ordinal) &&
        peacetimeHudSource.Contains("node name=\"BottomCommandHost\"", StringComparison.Ordinal) &&
        peacetimeHudSource.Contains("node name=\"OverlayHost\"", StringComparison.Ordinal) &&
        peacetimeHudSource.Contains("node name=\"ModalHost\"", StringComparison.Ordinal),
        "strategic-world and site HUD scenes should expose stable layout host names.");

    AssertTrue(
        tacticalWorldHudSource.Contains("node name=\"TopResourceBar\" type=\"PanelContainer\" parent=\"TopBarHost\"", StringComparison.Ordinal) &&
        tacticalWorldHudSource.Contains("node name=\"SiteDetailPanel\" type=\"PanelContainer\" parent=\"LeftPrimaryPanelHost\"", StringComparison.Ordinal) &&
        peacetimeHudSource.Contains("node name=\"SiteTopBar\" type=\"PanelContainer\" parent=\"TopBarHost\"", StringComparison.Ordinal) &&
        peacetimeHudSource.Contains("node name=\"SitePeacetimePanel\" type=\"PanelContainer\" parent=\"LeftPrimaryPanelHost\"", StringComparison.Ordinal),
        "existing top bars and primary panels should be mapped into the target layout hosts.");

    AssertTrue(
        managementSource.Contains("Layout hosts are Presentation-only containers", StringComparison.Ordinal),
        "layout host bindings should explain that hosts are not data authorities.");

    AssertTrue(
        tacticalWorldHudSource.Contains("node name=\"SiteDetailPanel\"", StringComparison.Ordinal) &&
        tacticalWorldHudSource.Contains("offset_left = 24.0", StringComparison.Ordinal) &&
        tacticalWorldHudSource.Contains("offset_right = 544.0", StringComparison.Ordinal),
        "UI layout migration should move tactical site detail panel into the left primary workspace.");

    AssertTrue(
        peacetimeHudSource.Contains("node name=\"BattlePreparationContent\"", StringComparison.Ordinal) &&
        peacetimeHudSource.Contains("visible = false", StringComparison.Ordinal) &&
        peacetimeHudSource.Contains("node name=\"BattlePreparationRosterList\"", StringComparison.Ordinal) &&
        peacetimeHudSource.Contains("node name=\"BattlePreparationActionList\"", StringComparison.Ordinal),
        "Peacetime HUD should define the dedicated battle-preparation containers and keep them hidden by default.");

    AssertTrue(
        managementSource.Contains("_siteBattlePreparationContent", StringComparison.Ordinal) &&
        managementSource.Contains("SetBattlePreparationContentVisible", StringComparison.Ordinal),
        "WorldSiteRoot management binding should expose dedicated battle-preparation container controls.");

    AssertTrue(
        battlePreparationSource.Contains("SetBattlePreparationContentVisible(true)", StringComparison.Ordinal) &&
        battlePreparationSource.Contains("ClearChildren(_siteBattlePreparationRosterList)", StringComparison.Ordinal) &&
        battlePreparationSource.Contains("_siteBattlePreparationActionList", StringComparison.Ordinal),
        "battle preparation refresh should render into dedicated containers.");

    AssertTrue(
        !battlePreparationSource.Contains("ClearChildren(_siteGarrisonList)", StringComparison.Ordinal) &&
        !battlePreparationSource.Contains("_siteGarrisonList.AddChild(button)", StringComparison.Ordinal) &&
        !battlePreparationSource.Contains("ClearChildren(_siteActionList)", StringComparison.Ordinal) &&
        !battlePreparationSource.Contains("_siteActionList.AddChild(startButton)", StringComparison.Ordinal),
        "battle preparation should not write into management action/garrison containers.");
}

internal static void PresentationUiScenePathsPreserveCodeBindings()
{
    string root = ProjectRoot();
    string strategicHud = File.ReadAllText(Path.Combine(root, "scenes", "world", "ui", "StrategicWorldHud.tscn"));
    string siteHud = File.ReadAllText(Path.Combine(root, "scenes", "world", "ui", "WorldSitePeacetimeHud.tscn"));

    AssertTrue(
        strategicHud.Contains("node name=\"Margin\" type=\"MarginContainer\" parent=\"LeftPrimaryPanelHost/SiteDetailPanel\"", StringComparison.Ordinal) &&
        strategicHud.Contains("node name=\"TopResourceBarContent\" type=\"MarginContainer\" parent=\"TopBarHost/TopResourceBar\"", StringComparison.Ordinal),
        "strategic HUD moved panels must keep full parent paths so code-bound labels/lists remain reachable.");
    AssertTrue(
        siteHud.Contains("node name=\"Margin\" type=\"MarginContainer\" parent=\"LeftPrimaryPanelHost/SitePeacetimePanel\"", StringComparison.Ordinal) &&
        siteHud.Contains("node name=\"TopMargin\" type=\"MarginContainer\" parent=\"TopBarHost/SiteTopBar\"", StringComparison.Ordinal),
        "site HUD moved panels must keep full parent paths so management and battle-preparation data binders reach their controls.");
}

internal static void StrategicWorldMapViewportStartsAfterLeftPanel()
{
    string geometrySource = File.ReadAllText(Path.Combine(
        ProjectRoot(),
        "src",
        "Presentation",
        "World",
        "StrategicWorldRoot.GeometryFormatting.cs"));

    AssertTrue(
        geometrySource.Contains("ResolveMainWorldViewportRect", StringComparison.Ordinal) &&
        geometrySource.Contains("_leftPrimaryPanelHost.GetGlobalRect()", StringComparison.Ordinal) &&
        geometrySource.Contains("_topBarHost.GetGlobalRect()", StringComparison.Ordinal) &&
        !geometrySource.Contains("float mapLeft = DetailWidth + OuterMargin * 2.0f", StringComparison.Ordinal),
        "strategic map viewport should derive from the left primary and top-bar layout hosts instead of drawing from duplicated constants.");
}

internal static void StrategicWorldUsesDedicatedMainWorldViewport()
{
    string root = ProjectRoot();
    string scene = File.ReadAllText(Path.Combine(root, "scenes", "world", "StrategicWorldRoot.tscn"));
    string rootSource = ReadStrategicWorldRootSource();
    string expectedArchitecture = File.ReadAllText(Path.Combine(
        root,
        "system-design",
        "presentation-ui-layout-architecture.md"));

    AssertTrue(
        expectedArchitecture.Contains("`MainWorldViewport` is a real Godot `SubViewport`", StringComparison.Ordinal),
        "expected UI architecture should require a real SubViewport boundary, not only logical map bounds.");
    AssertTrue(
        scene.Contains("node name=\"MainWorldViewportHost\" type=\"SubViewportContainer\" parent=\".\"", StringComparison.Ordinal) &&
        scene.Contains("node name=\"MainWorldViewport\" type=\"SubViewport\" parent=\"MainWorldViewportHost\"", StringComparison.Ordinal),
        "strategic world scene should define a real main world SubViewport under a viewport container.");
    AssertTrue(
        scene.Contains("node name=\"WorldCamera\" type=\"Camera2D\" parent=\"MainWorldViewportHost/MainWorldViewport\"", StringComparison.Ordinal) &&
        scene.Contains("node name=\"WorldMapRoot\" type=\"Node2D\" parent=\"MainWorldViewportHost/MainWorldViewport\"", StringComparison.Ordinal),
        "strategic world map and camera should live inside MainWorldViewport, not beside HUD UI.");
    AssertTrue(
        scene.Contains("node name=\"WorldMapOverlay\" type=\"Control\" parent=\"MainWorldViewportHost/MainWorldViewport\"", StringComparison.Ordinal),
        "strategic site hit controls need a viewport-local overlay instead of root UI placement.");
    AssertTrue(
        rootSource.Contains("WorldMapOverlayPath", StringComparison.Ordinal) &&
        rootSource.Contains("_worldMapOverlay", StringComparison.Ordinal) &&
        rootSource.Contains("_worldMapOverlay.AddChild(button)", StringComparison.Ordinal) &&
        !rootSource.Contains("            AddChild(button);\n            _siteButtons", StringComparison.Ordinal) &&
        !rootSource.Contains("            AddChild(button);\r\n            _siteButtons", StringComparison.Ordinal),
        "strategic site hit buttons should attach to the viewport-local overlay rather than the root canvas.");
    AssertTrue(
        rootSource.Contains("ToViewportLocal", StringComparison.Ordinal) &&
        rootSource.Contains("ToRootScreen", StringComparison.Ordinal),
        "strategic coordinate conversion should explicitly cross the root-screen and viewport-local boundary.");
}

internal static void StrategicWorldViewportOverlayOwnsInputAndMarkers()
{
    string rootSource = ReadStrategicWorldRootSource();
    string drawingSource = File.ReadAllText(Path.Combine(
        ProjectRoot(),
        "src",
        "Presentation",
        "World",
        "StrategicWorldRoot.MapDrawing.cs"));
    string setupSource = File.ReadAllText(Path.Combine(
        ProjectRoot(),
        "src",
        "Presentation",
        "World",
        "StrategicWorldRoot.MapSetup.cs"));
    string selectionSource = File.ReadAllText(Path.Combine(
        ProjectRoot(),
        "src",
        "Presentation",
        "World",
        "StrategicWorldRoot.SelectionInput.cs"));

    AssertTrue(
        setupSource.Contains("_worldMapOverlay.GuiInput += OnWorldMapOverlayGuiInput", StringComparison.Ordinal) &&
        setupSource.Contains("_worldMapOverlay.Draw += DrawWorldMapOverlay", StringComparison.Ordinal),
        "strategic viewport overlay should own map pointer input and map-space drawing after SubViewport isolation.");
    AssertTrue(
        setupSource.Contains("_worldMapOverlaySignalsConnected", StringComparison.Ordinal) &&
        !setupSource.Contains("_worldMapOverlay.GuiInput -=", StringComparison.Ordinal) &&
        !setupSource.Contains("_worldMapOverlay.Draw -=", StringComparison.Ordinal),
        "strategic viewport overlay signal setup should be guarded instead of disconnecting nonexistent Godot C# event connections.");
    AssertTrue(
        rootSource.Contains("HandleWorldArmyInput(@event, eventIsViewportLocal: true)", StringComparison.Ordinal) &&
        rootSource.Contains("ToRootScreen(mouseButton.Position)", StringComparison.Ordinal),
        "viewport-local map input should be converted back to root-screen coordinates before reusing world selection and command logic.");
    AssertTrue(
        drawingSource.Contains("private void DrawWorldMapOverlay()", StringComparison.Ordinal) &&
        !drawingSource.Contains("public override void _Draw()", StringComparison.Ordinal),
        "strategic runtime markers should draw on WorldMapOverlay instead of the root Control behind the SubViewport.");
    AssertTrue(
        drawingSource.Contains("MapToViewportLocal(army.WorldPosition)", StringComparison.Ordinal) &&
        drawingSource.Contains("MapToViewportLocal(opportunity.WorldPosition)", StringComparison.Ordinal) &&
        drawingSource.Contains("TryGetSiteVisualViewportBounds", StringComparison.Ordinal),
        "army, opportunity, and selected-site marker positions should be projected into viewport-local overlay coordinates.");
    AssertTrue(
        setupSource.Contains("QueueStrategicOverlayRedraw()", StringComparison.Ordinal) &&
        selectionSource.Contains("QueueStrategicOverlayRedraw()", StringComparison.Ordinal),
        "camera updates and drag selection changes should redraw the viewport overlay that owns the visible strategic markers.");
}

internal static void WorldSiteUsesDedicatedMainWorldViewport()
{
    string root = ProjectRoot();
    string scene = File.ReadAllText(Path.Combine(root, "scenes", "world", "sites", "WorldSiteRoot.tscn"));
    string rootSource = ReadWorldSiteRootSource();

    AssertTrue(
        scene.Contains("[node name=\"WorldSiteRoot\" type=\"Control\"]", StringComparison.Ordinal) &&
        rootSource.Contains("public partial class WorldSiteRoot : Control", StringComparison.Ordinal),
        "world site root should use the same Control-root layout contract as strategic world.");
    AssertTrue(
        scene.Contains("[node name=\"MainWorldViewportHost\" type=\"SubViewportContainer\" parent=\".\"]", StringComparison.Ordinal) &&
        scene.Contains("offset_left =", StringComparison.Ordinal) &&
        scene.Contains("offset_top =", StringComparison.Ordinal),
        "world site viewport host should be authored as a right-side workspace, not a full-screen map behind HUD.");
    AssertTrue(
        scene.Contains("node name=\"MainWorldViewportHost\" type=\"SubViewportContainer\" parent=\".\"", StringComparison.Ordinal) &&
        scene.Contains("node name=\"MainWorldViewport\" type=\"SubViewport\" parent=\"MainWorldViewportHost\"", StringComparison.Ordinal),
        "world site scene should define a real main world SubViewport under a viewport container.");
    AssertTrue(
        scene.Contains("clip_contents = true", StringComparison.Ordinal) &&
        rootSource.Contains("_mainWorldViewportHost.ClipContents = true", StringComparison.Ordinal),
        "world site viewport host must clip the SubViewport texture so the battle map cannot draw behind screen-space HUD.");
    AssertTrue(
        scene.Contains("node name=\"MapRoot\" type=\"Node2D\" parent=\"MainWorldViewportHost/MainWorldViewport\"", StringComparison.Ordinal) &&
        scene.Contains("node name=\"UnitRoot\" type=\"Node2D\" parent=\"MainWorldViewportHost/MainWorldViewport\"", StringComparison.Ordinal) &&
        scene.Contains("node name=\"OverlayRoot\" type=\"Node2D\" parent=\"MainWorldViewportHost/MainWorldViewport\"", StringComparison.Ordinal) &&
        scene.Contains("node name=\"DebugRoot\" type=\"Node\" parent=\"MainWorldViewportHost/MainWorldViewport\"", StringComparison.Ordinal) &&
        scene.Contains("node name=\"Camera2D\" type=\"Camera2D\" parent=\"MainWorldViewportHost/MainWorldViewport\"", StringComparison.Ordinal),
        "world site map, units, overlays, debug world nodes, and camera should live inside MainWorldViewport.");
    AssertTrue(
        scene.Contains("node name=\"CanvasLayer\" type=\"CanvasLayer\" parent=\".\"", StringComparison.Ordinal) &&
        scene.Contains("node name=\"SelectionVignetteOverlay\" type=\"ColorRect\" parent=\"CanvasLayer\"", StringComparison.Ordinal),
        "world site HUD and screen-space overlays should remain outside the world viewport.");
    AssertTrue(
        rootSource.Contains("MainWorldViewportHostPath", StringComparison.Ordinal) &&
        rootSource.Contains("MainWorldViewportPath", StringComparison.Ordinal) &&
        rootSource.Contains("UpdateMainWorldViewportLayout", StringComparison.Ordinal),
        "WorldSiteRoot should resolve and size the real viewport host from code.");
    AssertTrue(
        rootSource.Contains("GetWorldViewportMousePosition", StringComparison.Ordinal) &&
        rootSource.Contains("ToWorldViewportLocal", StringComparison.Ordinal) &&
        rootSource.Contains("WorldViewportLocalToWorld", StringComparison.Ordinal) &&
        rootSource.Contains("GetCanvasTransform().AffineInverse()", StringComparison.Ordinal) &&
        !rootSource.Contains("_coordinateLayer.ToLocal(GetGlobalMousePosition())", StringComparison.Ordinal),
        "world site grid picking and dragging should convert root mouse coordinates through viewport-local screen coordinates into SubViewport world coordinates.");
    AssertTrue(
        rootSource.Contains("SetDeploymentDragEntityToFootprintCenter", StringComparison.Ordinal) &&
        rootSource.Contains("TryGetFootprintCenterGlobalPosition", StringComparison.Ordinal),
        "battle-preparation roster and existing placement dragging should snap entities to the SubViewport footprint center.");
}

internal static void WorldCamerasUseConfiguredBoundsToPreventEmptyZoomOut()
{
    string root = ProjectRoot();
    string cameraSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "Common", "MapCameraController.cs"));
    string battleCameraSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "Battle", "BattleCameraController.cs"));
    string strategicSetupSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "StrategicWorldRoot.MapSetup.cs"));
    string strategicScene = File.ReadAllText(Path.Combine(root, "scenes", "world", "StrategicWorldRoot.tscn"));
    string siteScene = File.ReadAllText(Path.Combine(root, "scenes", "world", "sites", "WorldSiteRoot.tscn"));

    AssertTrue(
        cameraSource.Contains("UseConfiguredMapBoundsFallback", StringComparison.Ordinal) &&
        cameraSource.Contains("ConfiguredMapBoundsPosition", StringComparison.Ordinal) &&
        cameraSource.Contains("ConfiguredMapBoundsSize", StringComparison.Ordinal),
        "map cameras should expose authorable map bounds so zoom-out limits do not depend only on runtime discovery.");
    AssertTrue(
        cameraSource.Contains("ApplyConfiguredMapBoundsFallback", StringComparison.Ordinal) &&
        cameraSource.Contains("GetEffectiveMinZoom", StringComparison.Ordinal) &&
        cameraSource.Contains("requiredZoomX", StringComparison.Ordinal) &&
        cameraSource.Contains("requiredZoomY", StringComparison.Ordinal),
        "map camera zoom-out lower bound should be derived from viewport size versus configured/runtime map bounds.");
    AssertTrue(
        battleCameraSource.Contains("ResolveMapBoundsSource", StringComparison.Ordinal) &&
        battleCameraSource.Contains("IBattleMapBoundsSource", StringComparison.Ordinal) &&
        !battleCameraSource.Contains("WorldSiteRoot", StringComparison.Ordinal),
        "battle camera should discover battle map bounds through a provider contract instead of the concrete world-site root.");
    AssertTrue(
        !strategicSetupSource.Contains("bounds = bounds.Grow(96.0f)", StringComparison.Ordinal),
        "strategic camera bounds should not add empty padding beyond authored map tiles when calculating zoom-out limits.");
    AssertTrue(
        !strategicSetupSource.Contains("ExpandBounds(_worldMapRoot.ToLocal(anchor.GlobalPosition)", StringComparison.Ordinal),
        "strategic camera bounds should be based on authored map tile material, not non-rendered site anchors that can sit outside painted terrain.");
    AssertTrue(
        strategicScene.Contains("UseConfiguredMapBoundsFallback = true", StringComparison.Ordinal) &&
        strategicScene.Contains("ConfiguredMapBoundsSize", StringComparison.Ordinal) &&
        siteScene.Contains("UseConfiguredMapBoundsFallback = true", StringComparison.Ordinal) &&
        siteScene.Contains("ConfiguredMapBoundsSize", StringComparison.Ordinal),
        "strategic and site world cameras should both carry fallback map bounds in the scene resource.");
}

internal static void PresentationUiModeBindersStayInPresentation()
{
    string root = ProjectRoot();
    string uiModeSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "WorldUiMode.cs"));
    string strategicSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "StrategicWorldRoot.DetailHud.cs"));
    string siteManagementSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "Sites", "WorldSiteRoot.SiteManagementHud.cs"));
    string battlePreparationSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "Sites", "WorldSiteRoot.BattlePreparationHud.cs"));
    string battleRuntimeSource = ReadWorldSiteRootSource();

    AssertTrue(
        uiModeSource.Contains("namespace Rpg.Presentation.World", StringComparison.Ordinal) &&
        uiModeSource.Contains("public enum WorldUiMode", StringComparison.Ordinal) &&
        uiModeSource.Contains("StrategicSelection", StringComparison.Ordinal) &&
        uiModeSource.Contains("ExpeditionDraft", StringComparison.Ordinal) &&
        uiModeSource.Contains("SiteManagement", StringComparison.Ordinal) &&
        uiModeSource.Contains("BattlePreparation", StringComparison.Ordinal) &&
        uiModeSource.Contains("BattleRuntime", StringComparison.Ordinal) &&
        uiModeSource.Contains("SettlementReport", StringComparison.Ordinal),
        "WorldUiMode should be a Presentation-owned mode vocabulary.");

    AssertTrue(
        strategicSource.Contains("ResolveStrategicWorldUiMode", StringComparison.Ordinal) &&
        strategicSource.Contains("BindStrategicSelectionPanel", StringComparison.Ordinal) &&
        strategicSource.Contains("BindExpeditionDraftPanel", StringComparison.Ordinal),
        "strategic world UI should expose mode-specific binding boundaries.");

    AssertTrue(
        siteManagementSource.Contains("BindSiteManagementPanel", StringComparison.Ordinal) &&
        siteManagementSource.Contains("BindSettlementReportPanel", StringComparison.Ordinal) &&
        battlePreparationSource.Contains("BindBattlePreparationPanel", StringComparison.Ordinal) &&
        battleRuntimeSource.Contains("BindBattleRuntimeHud", StringComparison.Ordinal) &&
        battleRuntimeSource.Contains("CommandRequest", StringComparison.Ordinal),
        "site UI should expose mode-specific binding boundaries for management, settlement, preparation, and runtime.");
}

internal static void BattlePreparationSupportsDraggingOnHostileSites()
{
    string rootSource = ReadWorldSiteRootSource();
    string dragComponentSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites", "WorldSiteDeploymentDragComponent.cs"));

    AssertTrue(
        dragComponentSource.Contains("public bool DragEnabled", StringComparison.Ordinal) &&
        dragComponentSource.Contains("public string PlacementId", StringComparison.Ordinal) &&
        dragComponentSource.Contains("CanDragPlacement", StringComparison.Ordinal),
        "deployment dragging should be configured through a placement drag component");
    AssertTrue(
        rootSource.Contains("SetDeploymentDragComponent(entity, placementId, IsBattlePreparationPlacementDragEnabled(fallbackFaction))", StringComparison.Ordinal) &&
        rootSource.Contains("fallbackFaction is BattleFaction.Player or BattleFaction.Enemy", StringComparison.Ordinal),
        "battle preparation initialization should enable drag components for both player and enemy deployment entities");
    AssertTrue(
        rootSource.Contains("IsDeploymentDragEnabled", StringComparison.Ordinal),
        "drag picking should read component state instead of world ownership semantics");
    AssertTrue(
        !rootSource.Contains("if (!_isBattlePreparationActive && !CanOpenSiteDetail(site))", StringComparison.Ordinal),
        "drag picking should not embed hostile/player-owned site branching");
    AssertTrue(
        rootSource.Contains("SyncBattlePreparationRequestPlacements(request)", StringComparison.Ordinal),
        "start battle should sync dragged deployment placements back into the battle request");
    AssertTrue(
        rootSource.Contains("SyncBattlePreparationRequestPlacement(placementId, movedPlacement)", StringComparison.Ordinal),
        "dropping an existing battle-preparation unit should sync the matching request placement before the UI rebuilds");
    AssertTrue(
        rootSource.Contains("CanLaunchPreparedBattle", StringComparison.Ordinal) &&
        rootSource.Contains("还有我方单位未部署，不能开战。", StringComparison.Ordinal),
        "start battle should require all player request force slots to be deployed");
    AssertTrue(
        rootSource.Contains("SetAllDeploymentDragEnabled(false)", StringComparison.Ordinal) &&
        rootSource.Contains("DeploymentDragComponentsToggled", StringComparison.Ordinal),
        "start battle should disable deployment drag components before runtime starts");
    AssertTrue(
        rootSource.Contains("BattlePreparationPlacementsSynced", StringComparison.Ordinal),
        "placement sync should leave a low-noise diagnostic log");
}

internal static void BattlePreparationDragValidationUsesFactionDeploymentDirection()
{
    string rootSource = ReadWorldSiteRootSource();
    string interactionSource = ReadWorldSiteRootSource();

    AssertTrue(
        rootSource.Contains("ResolveBattlePreparationDeploymentSide", StringComparison.Ordinal) &&
        rootSource.Contains("ResolveBattlePreparationDeploymentDirection", StringComparison.Ordinal),
        "battle preparation should centralize side-aware deployment direction resolution");
    AssertTrue(
        interactionSource.Contains("ResolveBattlePreparationDeploymentSide(dragContext.FactionId, dragContext.FallbackFaction)", StringComparison.Ordinal),
        "existing placement drag validation should use the dragged request side rather than stale placement direction");
    AssertTrue(
        interactionSource.Contains("ResolveBattlePreparationDeploymentSide(force.FactionId, _draggedBattleForceFallbackFaction)", StringComparison.Ordinal),
        "roster drag validation should use the force side so defender-side roster drops use the defender zone");
}

internal static void BattlePreparationRosterDragResolvesBothSides()
{
    string hudSource = File.ReadAllText(Path.Combine(
        ProjectRoot(),
        "src",
        "Presentation",
        "World",
        "Sites",
        "WorldSiteRoot.BattlePreparationHud.cs"));
    string interactionSource = ReadWorldSiteRootSource();
    string refreshBody = ExtractMethodBody(hudSource, "private void RefreshBattlePreparationForceList()");
    string beginDragBody = ExtractMethodBody(interactionSource, "private void BeginBattlePreparationRosterDrag(");
    string handleDragBody = ExtractMethodBody(interactionSource, "private void HandleBattlePreparationRosterDragInput(");
    string previewBody = ExtractMethodBody(interactionSource, "private void UpdateBattlePreparationRosterDragPreview()");

    AssertTrue(
        refreshBody.Contains("AddBattlePreparationRosterButtons(_battlePreparationRequest?.PlayerForces, BattleFaction.Player)", StringComparison.Ordinal) &&
        refreshBody.Contains("AddBattlePreparationRosterButtons(_battlePreparationRequest?.EnemyForces, BattleFaction.Enemy)", StringComparison.Ordinal),
        "battle preparation roster should expose both player and enemy request forces through the same draggable button path");
    AssertTrue(
        beginDragBody.Contains("_draggedBattleForceFallbackFaction = fallbackFaction", StringComparison.Ordinal) &&
        beginDragBody.Contains("_battleUnitFactory.Create(" , StringComparison.Ordinal) &&
        beginDragBody.Contains("fallbackFaction", StringComparison.Ordinal),
        "roster drag preview should preserve the side that produced the dragged request force");
    AssertTrue(
        handleDragBody.Contains("FindBattlePreparationForce(forceId, _draggedBattleForceFallbackFaction)", StringComparison.Ordinal) &&
        previewBody.Contains("FindBattlePreparationForce(_draggedBattleForceId, _draggedBattleForceFallbackFaction)", StringComparison.Ordinal),
        "roster drag drop and preview should resolve request forces from both sides, not only PlayerForces");
    AssertTrue(
        !interactionSource.Contains("FindBattlePreparationPlayerForce", StringComparison.Ordinal),
        "battle preparation roster drag should not keep a player-only force lookup");
}

internal static void BattlePreparationMapDragUsesRequestBackedPlacements()
{
    string rootSource = ReadWorldSiteRootSource();
    string interactionSource = ReadWorldSiteRootSource();

    AssertTrue(
        rootSource.Contains("BattlePreparationPlacementDragContext", StringComparison.Ordinal) &&
        rootSource.Contains("TryResolveBattlePreparationDragContext", StringComparison.Ordinal),
        "battle preparation map drag should resolve placement metadata from the battle request as well as site placements");
    AssertTrue(
        interactionSource.Contains("TryMoveBattlePreparationPlacement", StringComparison.Ordinal),
        "dropping a battle-preparation map entity should update request-backed placements without requiring a WorldSiteState placement row");
    AssertTrue(
        interactionSource.Contains("dragContext.FootprintSize", StringComparison.Ordinal) &&
        interactionSource.Contains("dragContext.ForceId", StringComparison.Ordinal) &&
        interactionSource.Contains("dragContext.ForceIndex", StringComparison.Ordinal),
        "map drag preview, validation, and occupancy should use the dragged request force footprint and self identity");
}

internal static void BattlePreparationMapDragUsesSameDeploymentZoneRestrictionForBothSides()
{
    string rootSource = ReadWorldSiteRootSource();
    string interactionSource = ReadWorldSiteRootSource();

    AssertTrue(
        rootSource.Contains("ShouldRestrictBattlePreparationDeploymentZone", StringComparison.Ordinal),
        "battle preparation should make deployment-zone restriction an explicit side-aware rule");
    AssertTrue(
        interactionSource.Contains("ShouldRestrictBattlePreparationDeploymentZone(dragContext)", StringComparison.Ordinal) &&
        interactionSource.Contains("IsBattlePreparationFootprintDeployable", StringComparison.Ordinal),
        "map drag should route both sides through the same authored deployment-zone validation");
    AssertTrue(
        !rootSource.Contains("dragContext.FallbackFaction != BattleFaction.Enemy", StringComparison.Ordinal),
        "enemy deployment map drags should not bypass authored DeploymentZone markers");
}
}
