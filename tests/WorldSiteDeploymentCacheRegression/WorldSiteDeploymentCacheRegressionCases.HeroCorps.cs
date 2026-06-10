using Godot;
using Rpg.Application.Battle;
using Rpg.Application.World;
using Rpg.Definitions.Maps;
using Rpg.Definitions.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;

internal static partial class WorldSiteDeploymentCacheRegressionCases
{
internal static void AssaultVictoryClearsResolvedVisitingArmyPlacements()
{
    const string armyId = "army_first_slice_shield";
    StrategicWorldDefinition definition = StrategicWorldV1DefinitionFactory.Create(loadInitialStateConfig: false);
    StrategicWorldState state = BuildFirstSliceAssaultState(
        definition,
        armyId,
        heroUnitId: "f1_grandmasterzir",
        corpsUnitId: "f1_azuritelion");
    WorldSiteState site = state.SiteStates[StrategicWorldIds.SiteBonefield];
    site.UnitPlacements.Add(BuildVisitingArmyPlacement(armyId, "f1_grandmasterzir", 1, 1, 1));
    site.UnitPlacements.Add(BuildVisitingArmyPlacement(armyId, "f1_azuritelion", 2, 2, 1));
    site.UnitPlacements.Add(BuildVisitingArmyPlacement(armyId, "f1_azuritelion", 3, 3, 1));

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
            placement.UnitTypeId == "f1_grandmasterzir") == 1,
        "assault victory should leave exactly one hero garrison placement");
    AssertTrue(
        site.UnitPlacements.Count(placement =>
            WorldSiteDeploymentService.IsGarrisonPlacement(placement) &&
            placement.UnitTypeId == "f1_azuritelion") == 3,
        "assault victory should leave exactly the default corps as garrison placements");
}

internal static void StrategicWorldInvariantRepairRemovesResolvedArmyPlacements()
{
    const string armyId = "army_first_slice_shield";
    StrategicWorldDefinition definition = StrategicWorldV1DefinitionFactory.Create(loadInitialStateConfig: false);
    StrategicWorldState state = BuildFirstSliceAssaultState(
        definition,
        armyId,
        heroUnitId: "f1_grandmasterzir",
        corpsUnitId: "f1_azuritelion");
    WorldSiteState site = state.SiteStates[StrategicWorldIds.SiteBonefield];
    site.UnitPlacements.Add(BuildVisitingArmyPlacement(armyId, "f1_grandmasterzir", 1, 1, 1));
    site.UnitPlacements.Add(BuildVisitingArmyPlacement(armyId, "f1_azuritelion", 2, 2, 1));
    state.ArmyStates[armyId].Status = WorldArmyStatus.Garrisoned;
    state.ArmyStates[armyId].GarrisonUnits.Clear();

    int removed = new StrategicWorldStateInvariantService().RepairResolvedArmyPlacements(state);

    AssertEqual(2, removed, "resolved army placements should be removed");
    AssertTrue(
        !site.UnitPlacements.Any(placement => placement.SourceKind == "PlayerArmy" && placement.SourceId == armyId),
        "state repair should leave no stale player army placements");
}

internal static void StrategicWorldFirstSliceExpeditionSelectsOneHeroCompany()
{
    string rootSource = ReadStrategicWorldRootSource();

    AssertTrue(
        rootSource.Contains("FirstSliceHeroCompanyIds.HeroUnitIds", StringComparison.Ordinal) ||
        rootSource.Contains("FirstSliceHeroCompanyIds.Companies", StringComparison.Ordinal),
        "StrategicWorldRoot should draft from the three first-slice hero ids");
    AssertTrue(
        rootSource.Contains("AttachDefaultCorpsToHeroExpedition", StringComparison.Ordinal) &&
        rootSource.Contains("TryGetCompanyByHeroUnit", StringComparison.Ordinal),
        "StrategicWorldRoot should attach the selected hero company's default corps after expedition creation");
    AssertTrue(
        rootSource.Contains("默认兵团", StringComparison.Ordinal),
        "expedition panel should present the selected default corps as read-only information");
    AssertTrue(
        !rootSource.Contains("WorldExpeditionIssued army={army.ArmyId} intent={intent} target={targetSiteId}\");\n        if (intent == WorldArmyIntent.AssaultSite)", StringComparison.Ordinal) &&
        !rootSource.Contains("进入战前部署", StringComparison.Ordinal),
        "assault expedition should not bypass world travel immediately after the world army is created");
    AssertTrue(
        rootSource.Contains("WorldArmyIntent.AssaultSite => $\"已从{sourceName}派出{expeditionText}进攻{targetText}。\"", StringComparison.Ordinal),
        "assault expedition should clearly issue an attack order instead of entering battle immediately");
    AssertTrue(
        !rootSource.Contains("选择出征英雄和小兵", StringComparison.Ordinal),
        "first slice expedition player text should not ask for troop composition editing");
}

internal static void StrategicWorldExpeditionTargetingAcceptsLeftClickTarget()
{
    string rootSource = ReadStrategicWorldRootSource().Replace("\r\n", "\n", StringComparison.Ordinal);
    string leftMouseBody = ExtractMethodBody(
        rootSource,
        "private void HandleWorldArmyLeftMouse(\n        InputEventMouseButton mouseButton,\n        Vector2 screenPosition,\n        bool eventIsViewportLocal)");
    int targetingIndex = leftMouseBody.IndexOf("_isExpeditionTargeting", StringComparison.Ordinal);
    int issueIndex = leftMouseBody.IndexOf("TryIssueExpeditionToTarget(_armySelectionCurrentScreen)", StringComparison.Ordinal);
    int selectSiteIndex = leftMouseBody.IndexOf("TrySelectSiteAt(_armySelectionCurrentScreen)", StringComparison.Ordinal);

    AssertTrue(
        targetingIndex >= 0 &&
        issueIndex > targetingIndex &&
        selectSiteIndex > issueIndex,
        "expedition target mode should let left-click choose the target before ordinary site selection consumes the click");
    AssertTrue(
        rootSource.Contains("左键或右键场域", StringComparison.Ordinal),
        "expedition target prompt should tell players that left-click also confirms the target");
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
        rootSource.Contains("ClearPlayerBattlePreparationPlacements(request", StringComparison.Ordinal) &&
        rootSource.Contains("BattlePreparationPlayerPlacementsCleared", StringComparison.Ordinal),
        "battle preparation should start player units from the request-backed roster instead of pre-spreading them on the map");
    AssertTrue(
        rootSource.Contains("BindBattlePreparationCompanyRoster", StringComparison.Ordinal) &&
        rootSource.Contains("BeginBattlePreparationCompanyDrag", StringComparison.Ordinal) &&
        !rootSource.Contains("BeginBattlePreparationRosterDrag", StringComparison.Ordinal),
        "battle preparation should expose hero companies as compact draggable roster rows, not individual force-slot buttons");
    AssertTrue(
        rootSource.Contains("_battlePreparationStartButton", StringComparison.Ordinal) &&
        rootSource.Contains("BattlePreparationStartButton", StringComparison.Ordinal) &&
        rootSource.Contains("LaunchPreparedBattle", StringComparison.Ordinal),
        "battle preparation should show an explicit compact start battle command");
    AssertTrue(
        rootSource.Contains("目标区域", StringComparison.Ordinal) &&
        rootSource.Contains("SelectBattlePreparationObjectiveZone", StringComparison.Ordinal) &&
        rootSource.Contains("BattlePreparationObjectiveSelected", StringComparison.Ordinal),
        "battle preparation should expose an explicit objective-zone selection step before runtime activation");
    AssertTrue(
        rootSource.Contains("BindBattlePreparationCompactPlanControls", StringComparison.Ordinal) &&
        rootSource.Contains("SelectBattlePreparationEngagementRule", StringComparison.Ordinal) &&
        rootSource.Contains("BattlePreparationEngagementRuleSelected", StringComparison.Ordinal),
        "battle preparation should expose player-selected engagement rules in compact current-company controls");
    AssertTrue(
        rootSource.Contains("RegisterBattlePreparationPlacement", StringComparison.Ordinal),
        "battle placements should be registered during preparation");
    AssertTrue(
        rootSource.Contains("preview and runtime start positions cannot drift apart", StringComparison.Ordinal) &&
        rootSource.Contains("_sitePlacementEntities[placementId] = entity", StringComparison.Ordinal),
        "battle preparation should keep enemy and player preview entities indexed from prepared placements");
    AssertTrue(
        rootSource.Contains("ShowBattlePreparationDeploymentZone", StringComparison.Ordinal) &&
        rootSource.Contains("_deploymentZoneOverlay?.SetZones", StringComparison.Ordinal),
        "battle preparation should show player deployment through the dedicated deployment-zone overlay");
    AssertTrue(
        rootSource.Contains("ActivateBattleRuntime();", StringComparison.Ordinal),
        "start battle should still commit into the existing runtime after preparation");
    AssertTrue(
        rootSource.Contains("SyncBattlePreparationPlanToRequest(request)", StringComparison.Ordinal) &&
        rootSource.Contains("BattlePreparationPlanSynced", StringComparison.Ordinal) &&
        rootSource.Contains("PlayerBattleGroupPlan", StringComparison.Ordinal),
        "start battle should sync the selected objective and engagement rule into the same BattleStartRequest before activating runtime");
    AssertTrue(
        rootSource.Contains("_explicitBattlePreparationRuleGroups.Contains(group.GroupKey)", StringComparison.Ordinal),
        "battle launch should require an explicit engagement-rule choice for every player company");
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
        siteScene.Contains("BattleRuntimeHeroFrame", StringComparison.Ordinal) &&
        siteScene.Contains("BattleRuntimeHeroSkillList", StringComparison.Ordinal) &&
        siteScene.Contains("BattleRuntimeRegroupButton", StringComparison.Ordinal),
        "world site HUD scene should author a persistent bottom hero frame instead of building the runtime UI tree ad hoc");
    AssertTrue(
        rootSource.Contains("_siteBottomCommandHost", StringComparison.Ordinal) &&
        rootSource.Contains("_battleRuntimeHeroFrame", StringComparison.Ordinal) &&
        rootSource.Contains("_battleRuntimeHeroSkillList", StringComparison.Ordinal) &&
        rootSource.Contains("_battleRuntimeRegroupButton", StringComparison.Ordinal),
        "WorldSiteRoot should bind authored runtime hero frame nodes");
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
        "battle runtime HUD should show the bottom hero frame after mode visibility refreshes, keep the initial command compatibility boundary, and leave a low-noise diagnostic");
    AssertTrue(
        requestSource.Contains("InitialCorpsCommandId", StringComparison.Ordinal) &&
        snapshotSource.Contains("InitialCorpsCommandId", StringComparison.Ordinal) &&
        probeSource.Contains("InitialCorpsCommandId", StringComparison.Ordinal),
        "initial corps command should be copied from legacy battle request into the target battle snapshot");
    AssertTrue(
        requestSource.Contains("ObjectiveZones", StringComparison.Ordinal) &&
        requestSource.Contains("PlayerBattleGroupPlan", StringComparison.Ordinal) &&
        requestSource.Contains("PlayerBattleGroupPlans", StringComparison.Ordinal) &&
        requestSource.Contains("EnemyBattleGroupPlan", StringComparison.Ordinal) &&
        requestSource.Contains("EnemyBattleGroupPlans", StringComparison.Ordinal) &&
        probeSource.Contains("ResolveBattleGroupPlan", StringComparison.Ordinal) &&
        probeSource.Contains("BattlePlanSide.Enemy", StringComparison.Ordinal) &&
        probeSource.Contains("CopyPlanForGroup", StringComparison.Ordinal),
        "battle preparation should route objective-zone and engagement-rule selections for both sides through the request-to-snapshot probe boundary");
}

internal static void WorldSiteBattleRuntimeHeroSkillSubmitsHeroCastCommand()
{
    string rootSource = ReadWorldSiteRootSource();
    string pressBody = ExtractMethodBody(rootSource, "private void BeginBattleRuntimeSkillPress(");

    AssertTrue(
        rootSource.Contains("HeroSkillCommandIds.FirstSliceHeroSkillId", StringComparison.Ordinal),
        "battle runtime HUD should define one explicit first-slice hero skill id for the current deployed hero");
    AssertTrue(
        rootSource.Contains("OnBattleRuntimeHeroSkillPressed", StringComparison.Ordinal) &&
        rootSource.Contains("OnBattleRuntimeSkillSlotPressed", StringComparison.Ordinal) &&
        pressBody.Contains("SetBattleRuntimeCommandPauseActive(true", StringComparison.Ordinal) &&
        pressBody.Contains("BeginBattleRuntimeHeroSkillTargetPicking(selected, normalizedSkillId)", StringComparison.Ordinal) &&
        !pressBody.Contains("SubmitBattleRuntimeHeroSkillCommand(selected)", StringComparison.Ordinal),
        "hero skill button should enter tactical pause and target picking instead of submitting a targeted skill without a target");
    AssertTrue(
        rootSource.Contains("BuildBattleRuntimeHeroSkillCommandRequest", StringComparison.Ordinal) &&
        rootSource.Contains("Channel = CommandChannel.Hero", StringComparison.Ordinal) &&
        rootSource.Contains("Kind = CommandKind.CastSkill", StringComparison.Ordinal) &&
        rootSource.Contains("SkillId = skillId", StringComparison.Ordinal) &&
        rootSource.Contains("TargetActorId = targetActorId", StringComparison.Ordinal),
        "hero skill target click should build a CommandRequest through the hero cast-skill channel with the selected target");
    AssertTrue(
        rootSource.Contains("_activeBattleGroupRuntimeResolution?.RuntimeController?.SubmitCommand", StringComparison.Ordinal) &&
        rootSource.Contains("BattleRuntimeHeroSkillSubmitted", StringComparison.Ordinal),
        "hero skill command should submit to the active runtime controller and leave a low-noise diagnostic");
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
        siteScene.Contains("BattleRuntimeHeroFrame", StringComparison.Ordinal) &&
        siteScene.Contains("BattleRuntimeHeroHealthBar", StringComparison.Ordinal) &&
        siteScene.Contains("BattleRuntimeHeroManaBar", StringComparison.Ordinal) &&
        !siteScene.Contains("BattleRuntimeCommandPanel", StringComparison.Ordinal) &&
        !siteScene.Contains("BattleRuntimeHeroCommandList", StringComparison.Ordinal),
        "battle runtime command UI should be authored as a persistent hero frame, not a left hero/corps/combined command panel.");
    AssertTrue(
        rootSource.Contains("TryHandleBattleRuntimePauseInput", StringComparison.Ordinal) &&
        rootSource.Contains("Key.Space", StringComparison.Ordinal) &&
        rootSource.Contains("_battleRuntimeCommandPauseActive", StringComparison.Ordinal) &&
        rootSource.Contains("ToggleBattleRuntimeCommandPause", StringComparison.Ordinal),
        "WorldSiteRoot should treat Space as a presentation-only battle command pause toggle.");
    AssertTrue(
        rootSource.Contains("RefreshBattleRuntimeHeroFrame", StringComparison.Ordinal) &&
        rootSource.Contains("SelectBattleRuntimeCommandGroup", StringComparison.Ordinal) &&
        rootSource.Contains("ResolveBattleRuntimeGroupKey", StringComparison.Ordinal) &&
        rootSource.Contains("SourceKind", StringComparison.Ordinal) &&
        rootSource.Contains("SourceId", StringComparison.Ordinal),
        "battle runtime hero frame should group request forces by their shared source so a hero and attached corps select together.");
    AssertTrue(
        rootSource.Contains("SetCommandSelectionByEntityIds", StringComparison.Ordinal) &&
        rootSource.Contains("ApplyBattleRuntimeCommandGroupHighlight", StringComparison.Ordinal) &&
        rootSource.Contains("OnBattleRuntimeHeroSkillPressed", StringComparison.Ordinal),
        "selecting a runtime hero company should highlight matching units and keep commands in the compact hero frame without mutating runtime truth.");
    AssertTrue(
        worldSiteScene.Contains("UnitMoveDuration = 0.27", StringComparison.Ordinal),
        "runtime unit movement should be slowed in the site scene for readable realtime command evaluation.");
}

internal static void BattlePreparationUsesCompactPlanControls()
{
    string root = ProjectRoot();
    string battlePreparationSource = File.ReadAllText(Path.Combine(
        root,
        "src",
        "Presentation",
        "World",
        "Sites",
        "WorldSiteRoot.BattlePreparationHud.cs"));
    string siteHudScene = File.ReadAllText(Path.Combine(root, "scenes", "world", "ui", "WorldSitePeacetimeHud.tscn"));

    AssertTrue(
        !battlePreparationSource.Contains("private void RefreshBattlePreparationActions()", StringComparison.Ordinal) &&
        !battlePreparationSource.Contains("AddBattlePreparationStartButton(Container", StringComparison.Ordinal) &&
        !battlePreparationSource.Contains("AddBattlePreparationObjectiveMapButton(Container", StringComparison.Ordinal) &&
        !battlePreparationSource.Contains("AddBattlePreparationEngagementRuleButton(Container", StringComparison.Ordinal),
        "battle preparation controls should not be rendered through the old vertical action-list methods.");
    AssertTrue(
        battlePreparationSource.Contains("BindBattlePreparationCompactPlanControls", StringComparison.Ordinal) &&
        battlePreparationSource.Contains("_battlePreparationStartButton", StringComparison.Ordinal) &&
        battlePreparationSource.Contains("LaunchPreparedBattle", StringComparison.Ordinal) &&
        siteHudScene.Contains("node name=\"BattlePreparationPlanBar\"", StringComparison.Ordinal) &&
        siteHudScene.Contains("node name=\"BattlePreparationStartButton\"", StringComparison.Ordinal),
        "battle preparation should bind compact authored controls for current-company plan and start battle.");
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
        rootSource.Contains("RaiseBattlePreparationCompanyPreviewEntities", StringComparison.Ordinal),
        "existing placements and company drag previews should both use the high-Z drag state");
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
        rootSource.Contains("BuildBattlePreparationCompanyFormationDraft", StringComparison.Ordinal) &&
        rootSource.Contains("SetBattlePreparationDragFootprintPreview", StringComparison.Ordinal),
        "battle preparation company drag preview should derive highlighted cells from the full formation footprint");
    AssertTrue(
        rootSource.Contains("IsBattlePreparationFootprintDeployable", StringComparison.Ordinal) &&
        rootSource.Contains("IsBattlePreparationFootprintOccupied", StringComparison.Ordinal),
        "battle preparation drop validation should use the same footprint cells as the preview");
    AssertTrue(
        rootSource.Contains("_highlightOverlay?.SetCells(BattleGridHighlightKind.Hover, cells)", StringComparison.Ordinal) &&
        rootSource.Contains("_highlightOverlay?.SetCells(BattleGridHighlightKind.Invalid, cells)", StringComparison.Ordinal) &&
        rootSource.Contains("_highlightOverlay?.ClearCells(BattleGridHighlightKind.Hover)", StringComparison.Ordinal) &&
        rootSource.Contains("_highlightOverlay?.ClearCells(BattleGridHighlightKind.Invalid)", StringComparison.Ordinal),
        "deployment drag should show legal formation hover cells, invalid formation cells, and clear both when dragging ends");
    AssertTrue(
        rootSource.Contains("SetBattlePreparationCompanyPreviewInvalid", StringComparison.Ordinal),
        "invalid company placement should tint every preview entity red instead of only changing one cell marker");
    AssertTrue(
        rootSource.Contains("TryResolveMouseFormationIntentAnchor", StringComparison.Ordinal) &&
        !ExtractMethodBody(rootSource, "private void UpdateBattlePreparationCompanyDragPreview(")
            .Contains("TryResolveMouseFootprintAnchor(Vector2I.One", StringComparison.Ordinal),
        "company drag preview should keep following the pointer intent instead of freezing when the pointer leaves a valid grid cell");
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
    string beginDragBody = ExtractMethodBody(rootSource, "private void BeginBattlePreparationCompanyDrag(");
    string handleDragBody = ExtractMethodBody(rootSource, "private void HandleBattlePreparationCompanyDragInput(");
    string previewBody = ExtractMethodBody(rootSource, "private void UpdateBattlePreparationCompanyDragPreview(");
    int mouseReleaseBranchIndex = handleDragBody.IndexOf("if (@event is not InputEventMouseButton", StringComparison.Ordinal);
    AssertTrue(mouseReleaseBranchIndex >= 0, "battle preparation company drag input should keep mouse release handling explicit");
    string mouseMotionBody = handleDragBody.Substring(0, mouseReleaseBranchIndex);

    AssertTrue(
        beginDragBody.Contains("RemoveBattlePreparationCompanyPreviewEntities", StringComparison.Ordinal),
        "re-dragging a company should remove only that company's old preview entities");
    AssertTrue(
        !beginDragBody.Contains("RefreshBattlePreparationMapEntities();", StringComparison.Ordinal),
        "starting a company drag should not rebuild all battle-preparation entities because that restarts existing idle animations");
    AssertTrue(
        !beginDragBody.Contains("PlayIdle()", StringComparison.Ordinal),
        "temporary company drag entities should rely on UnitAnimationComponent attachment for initial idle and should not replay it");
    AssertTrue(
        !mouseMotionBody.Contains("PlayIdle()", StringComparison.Ordinal) &&
        !mouseMotionBody.Contains("RefreshBattlePreparationMapEntities", StringComparison.Ordinal) &&
        !mouseMotionBody.Contains("RefreshBattlePreparationUi", StringComparison.Ordinal),
        "mouse motion during company deployment drag should only move preview state, not rebuild UI or restart animations");
    AssertTrue(
        !previewBody.Contains("PlayIdle()", StringComparison.Ordinal) &&
        !previewBody.Contains("RefreshBattlePreparationMapEntities", StringComparison.Ordinal) &&
        !previewBody.Contains("RefreshBattlePreparationUi", StringComparison.Ordinal),
        "company drag preview updates should keep animation playback untouched while the formation anchor changes");
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
        !peacetimeHudSource.Contains("node name=\"BattlePreparationContent\"", StringComparison.Ordinal) &&
        !peacetimeHudSource.Contains("node name=\"BattlePreparationActionList\"", StringComparison.Ordinal) &&
        !peacetimeHudSource.Contains("node name=\"BattlePreparationEnemySummary\"", StringComparison.Ordinal) &&
        !peacetimeHudSource.Contains("parent=\"LeftPrimaryPanelHost/SitePeacetimePanel/Margin/Scroll/Content/BattlePreparationContent", StringComparison.Ordinal),
        "site management panel should not keep the old text-heavy battle-preparation subtree.");

    AssertTrue(
        peacetimeHudSource.Contains("node name=\"BattlePreparationRosterDock\"", StringComparison.Ordinal) &&
        peacetimeHudSource.Contains("node name=\"BattlePreparationRosterList\"", StringComparison.Ordinal) &&
        peacetimeHudSource.Contains("node name=\"BattlePreparationPlanBar\"", StringComparison.Ordinal) &&
        peacetimeHudSource.Contains("node name=\"BattlePreparationObjectiveThumbnailDock\"", StringComparison.Ordinal) &&
        peacetimeHudSource.Contains("node name=\"BattlePreparationObjectiveThumbnail\"", StringComparison.Ordinal) &&
        peacetimeHudSource.Contains("node name=\"BattlePreparationStartButton\"", StringComparison.Ordinal),
        "Peacetime HUD should author compact battle-preparation roster, plan, objective-thumbnail, and start controls outside the management panel.");

    AssertTrue(
        !managementSource.Contains("_siteBattlePreparationContent", StringComparison.Ordinal) &&
        !managementSource.Contains("SetBattlePreparationContentVisible", StringComparison.Ordinal) &&
        managementSource.Contains("_battlePreparationRosterDock", StringComparison.Ordinal) &&
        managementSource.Contains("_battlePreparationPlanBar", StringComparison.Ordinal) &&
        managementSource.Contains("_battlePreparationObjectiveThumbnail", StringComparison.Ordinal),
        "WorldSiteRoot management binding should remove old battle-preparation panel fields and bind compact HUD controls.");

    AssertTrue(
        !battlePreparationSource.Contains("SetBattlePreparationContentVisible(true)", StringComparison.Ordinal) &&
        !battlePreparationSource.Contains("RefreshBattlePreparationForceList()", StringComparison.Ordinal) &&
        !battlePreparationSource.Contains("_siteBattlePreparationActionList", StringComparison.Ordinal) &&
        battlePreparationSource.Contains("BindBattlePreparationCompanyRoster", StringComparison.Ordinal) &&
        battlePreparationSource.Contains("BindBattlePreparationCompactPlanControls", StringComparison.Ordinal),
        "battle preparation refresh should bind compact company controls instead of the old roster/action lists.");

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
        rootSource.Contains("BuildDeployedBattlePreparationPlayerGroups", StringComparison.Ordinal),
        "start battle should allow undeployed carried companies to remain in reserve while validating deployed groups");
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
        interactionSource.Contains("ResolveBattlePreparationCompanyDeploymentSide", StringComparison.Ordinal) &&
        interactionSource.Contains("BattlePreparationCompanyFormationPlanner", StringComparison.Ordinal),
        "company drag validation should route the selected company through the same side-aware deployment-zone planner");
}

internal static void BattlePreparationCompanyRosterUsesPlayerGroupsOnly()
{
    string hudSource = File.ReadAllText(Path.Combine(
        ProjectRoot(),
        "src",
        "Presentation",
        "World",
        "Sites",
        "WorldSiteRoot.BattlePreparationHud.cs"));
    string interactionSource = ReadWorldSiteRootSource();
    string beginDragBody = ExtractMethodBody(interactionSource, "private void BeginBattlePreparationCompanyDrag(");
    string handleDragBody = ExtractMethodBody(interactionSource, "private void HandleBattlePreparationCompanyDragInput(");
    string previewBody = ExtractMethodBody(interactionSource, "private void UpdateBattlePreparationCompanyDragPreview(");

    AssertTrue(
        hudSource.Contains("BindBattlePreparationCompanyRoster", StringComparison.Ordinal) &&
        hudSource.Contains("BuildBattlePreparationPlayerGroups()", StringComparison.Ordinal) &&
        !hudSource.Contains("AddBattlePreparationRosterButtons(_battlePreparationRequest?.EnemyForces", StringComparison.Ordinal),
        "battle preparation roster should be a player-company switcher, not a combined player/enemy force-slot list");
    AssertTrue(
        beginDragBody.Contains("_draggedBattlePreparationGroupKey", StringComparison.Ordinal) &&
        beginDragBody.Contains("CreateBattlePreparationCompanyPreviewEntities", StringComparison.Ordinal) &&
        beginDragBody.Contains("BattleFaction.Player", StringComparison.Ordinal),
        "company drag preview should create player-company previews from the selected group");
    AssertTrue(
        handleDragBody.Contains("TryCommitBattlePreparationCompanyPlacement", StringComparison.Ordinal) &&
        previewBody.Contains("BuildBattlePreparationCompanyFormationDraft", StringComparison.Ordinal),
        "company drag drop and preview should resolve all member force slots through the formation draft");
    AssertTrue(
        !interactionSource.Contains("FindBattlePreparationForce", StringComparison.Ordinal) &&
        !interactionSource.Contains("BeginBattlePreparationRosterDrag", StringComparison.Ordinal),
        "battle preparation should remove old single-force roster drag lookup paths");
}

internal static void BattlePreparationRosterRowBindsBeforeReadyAndDragsBeforeSelectionRefresh()
{
    string rowSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites", "BattlePreparationRosterRow.cs"));
    string rowScene = File.ReadAllText(Path.Combine(ProjectRoot(), "scenes", "world", "ui", "BattlePreparationRosterRow.tscn"));
    string inputBody = ExtractMethodBody(rowSource, "public override void _GuiInput(");
    int mousePressIndex = inputBody.IndexOf("if (mouseButton.Pressed)", StringComparison.Ordinal);
    int mousePressReturnIndex = mousePressIndex >= 0
        ? inputBody.IndexOf("return;", mousePressIndex, StringComparison.Ordinal)
        : -1;
    int selectedSignalIndex = inputBody.IndexOf("EmitSignal(SignalName.Selected", StringComparison.Ordinal);

    AssertTrue(
        rowScene.Contains("node name=\"BattlePreparationRosterRow\" type=\"PanelContainer\"", StringComparison.Ordinal) &&
        rowScene.Contains("node name=\"Row\" type=\"HBoxContainer\" parent=\".\"", StringComparison.Ordinal) &&
        rowScene.Contains("node name=\"Name\" type=\"Label\" parent=\"Row\"", StringComparison.Ordinal) &&
        rowScene.Contains("node name=\"Status\" type=\"Label\" parent=\"Row\"", StringComparison.Ordinal),
        "battle preparation roster row should be an authored Godot row scene with avatar/name/status nodes.");
    AssertTrue(
        rowSource.Contains("_pendingGroupKey", StringComparison.Ordinal) &&
        rowSource.Contains("ApplyBinding()", StringComparison.Ordinal) &&
        rowSource.Contains("public override void _Ready()", StringComparison.Ordinal) &&
        rowSource.Contains("ApplyBinding();", StringComparison.Ordinal),
        "roster rows must preserve Bind values when WorldSiteRoot binds before the row enters the scene tree.");
    AssertTrue(
        mousePressIndex >= 0 &&
        mousePressReturnIndex > mousePressIndex &&
        selectedSignalIndex > mousePressReturnIndex,
        "mouse press must not emit Selected because selection refresh rebuilds the roster and destroys the pending drag source.");
    AssertTrue(
        rowSource.Contains("!mouseButton.Pressed") &&
        rowSource.Contains("!_dragStarted") &&
        rowSource.Contains("EmitSignal(SignalName.Selected", StringComparison.Ordinal) &&
        rowSource.Contains("EmitSignal(SignalName.DragStarted", StringComparison.Ordinal),
        "roster rows should select on click release but start company drag from mouse motion over the threshold.");
}

internal static void BattlePreparationPlanUsesStrategicDefaultFormation()
{
    System.Reflection.PropertyInfo armyDefaultFormation = typeof(WorldArmyState).GetProperty("DefaultFormationId");
    System.Reflection.PropertyInfo forceDefaultFormation = typeof(BattleForceRequest).GetProperty("DefaultFormationId");

    AssertTrue(armyDefaultFormation != null, "world army state should persist a strategic default formation id.");
    AssertTrue(forceDefaultFormation != null, "battle force requests should carry the source default formation into battle preparation.");

    const string armyId = "army_first_slice_default_formation";
    const string expectedFormation = "formation_column";
    StrategicWorldDefinition definition = StrategicWorldV1DefinitionFactory.Create(loadInitialStateConfig: false);
    StrategicWorldState state = BuildFirstSliceAssaultState(
        definition,
        armyId,
        heroUnitId: "f1_grandmasterzir",
        corpsUnitId: "f1_azuritelion");
    armyDefaultFormation.SetValue(state.ArmyStates[armyId], expectedFormation);

    BattleStartRequest request = new WorldBattleRequestBuilder().BuildAssaultBonefieldRequest(
        state,
        definition,
        "res://scenes/world/StrategicWorldRoot.tscn",
        "res://scenes/world/sites/WorldSiteRoot.tscn",
        armyId);

    AssertTrue(
        request.PlayerForces.Count > 0 &&
        request.PlayerForces.All(force => string.Equals(forceDefaultFormation.GetValue(force) as string, expectedFormation, StringComparison.Ordinal)),
        "battle entry should copy the source army default formation to every player force in the hero company.");

    string groupViewSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites", "BattleRuntimeCommandGroupView.cs"));
    string hudSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites", "WorldSiteRoot.BattlePreparationHud.cs"));
    string dragSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites", "WorldSiteRoot.BattlePreparationDrag.cs"));
    string plannerSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Application", "World", "BattlePreparationCompanyFormationPlanner.cs"));

    AssertTrue(
        groupViewSource.Contains("DefaultFormationId", StringComparison.Ordinal),
        "battle preparation group view should expose the company default formation.");
    AssertTrue(
        hudSource.Contains("BattlePreparationPlanUiModel.ResolveFormationId(plan.InitialFormationId, group.DefaultFormationId)", StringComparison.Ordinal),
        "battle preparation plan defaults should initialize current-battle formation from the company default.");
    AssertTrue(
        dragSource.Contains("plan?.InitialFormationId", StringComparison.Ordinal) &&
        plannerSource.Contains("string formationId", StringComparison.Ordinal),
        "company drag preview should pass the current battle formation into the formation planner.");
}

internal static void BattlePreparationCompanyFormationPlannerAdaptsNarrowZoneWithoutOverlap()
{
    BattleGridMap grid = new();
    for (int x = 0; x < 4; x++)
    {
        AddWalkableSurface(grid, x, 0, terrainTag: "narrow_line");
    }

    WorldSiteRuntimeDeploymentCache cache = new WorldSiteRuntimeDeploymentCacheBuilder().Build("site_under_test", grid);
    BattleForceRequest wideForce = new()
    {
        ForceId = "wide_corps",
        SourceKind = "PlayerArmy",
        SourceId = "army_1",
        UnitDefinitionId = "wide_corps",
        Count = 2,
        FactionId = "player",
        FootprintWidth = 2,
        FootprintHeight = 1
    };

    BattlePreparationCompanyFormationDraft draft = new BattlePreparationCompanyFormationPlanner().BuildDraft(
        new[] { wideForce },
        "default_line",
        new GridPosition(0, 0),
        SemanticDeploymentSide.Player,
        "player",
        WorldSiteAttackDirection.East,
        grid,
        cache,
        Array.Empty<BattleForceRequest>(),
        CanForceEnterWater);

    AssertTrue(draft.IsValid, $"narrow deployment should adapt to a non-overlapping depth formation failure={draft.FailureReason}");
    AssertEqual(2, draft.Placements.Count, "two 2x1 members should produce two placement drafts");
    AssertEqual(4, draft.CoveredCells.Count, "two 2x1 members should cover four cells");
    AssertEqual(4, draft.CoveredCells.Distinct().Count(), "formation covered cells should not overlap");
    AssertTrue(
        draft.Placements.Any(placement => placement.Anchor == new GridPosition(0, 0)) &&
        draft.Placements.Any(placement => placement.Anchor == new GridPosition(2, 0)),
        $"narrow fallback should place the second 2x1 member after the first footprint cells={FormatCells(draft.CoveredCells)}");
}

internal static void BattlePreparationCompanyFormationPlannerProjectsOutsideAnchorToNearestValidWholeDraft()
{
    BattleGridMap grid = new();
    for (int x = 0; x < 6; x++)
    {
        AddWalkableSurface(grid, x, 0, terrainTag: "wide_line");
    }

    WorldSiteRuntimeDeploymentCache cache = new WorldSiteRuntimeDeploymentCacheBuilder().Build("site_under_test", grid);
    BattleForceRequest wideForce = new()
    {
        ForceId = "wide_corps",
        SourceKind = "PlayerArmy",
        SourceId = "army_1",
        UnitDefinitionId = "wide_corps",
        Count = 2,
        FactionId = "player",
        FootprintWidth = 2,
        FootprintHeight = 1
    };

    BattlePreparationCompanyFormationDraft draft = new BattlePreparationCompanyFormationPlanner().BuildDraft(
        new[] { wideForce },
        "default_line",
        new GridPosition(9, 0),
        SemanticDeploymentSide.Player,
        "player",
        WorldSiteAttackDirection.East,
        grid,
        cache,
        Array.Empty<BattleForceRequest>(),
        CanForceEnterWater);

    AssertTrue(draft.IsValid, $"outside pointer intent should project to nearest legal whole formation failure={draft.FailureReason}");
    AssertEqual(2, draft.Placements.Count, "projected draft should still contain every company member");
    AssertTrue(
        draft.Placements.Any(placement => placement.Anchor == new GridPosition(2, 0)) &&
        draft.Placements.Any(placement => placement.Anchor == new GridPosition(4, 0)),
        $"outside-right intent should choose the rightmost whole no-overlap placement cells={FormatCells(draft.CoveredCells)}");
    AssertEqual(4, draft.CoveredCells.Count, "projected two 2x1 members should cover four distinct cells");
}

internal static void BattlePreparationCompanyFormationPlannerReturnsWholeInvalidPreview()
{
    BattleGridMap grid = new();
    for (int x = 0; x < 3; x++)
    {
        AddWalkableSurface(grid, x, 0, terrainTag: "too_short_line");
    }

    WorldSiteRuntimeDeploymentCache cache = new WorldSiteRuntimeDeploymentCacheBuilder().Build("site_under_test", grid);
    BattleForceRequest wideForce = new()
    {
        ForceId = "wide_corps",
        SourceKind = "PlayerArmy",
        SourceId = "army_1",
        UnitDefinitionId = "wide_corps",
        Count = 2,
        FactionId = "player",
        FootprintWidth = 2,
        FootprintHeight = 1
    };

    BattlePreparationCompanyFormationDraft draft = new BattlePreparationCompanyFormationPlanner().BuildDraft(
        new[] { wideForce },
        "formation_column",
        new GridPosition(0, 0),
        SemanticDeploymentSide.Player,
        "player",
        WorldSiteAttackDirection.East,
        grid,
        cache,
        Array.Empty<BattleForceRequest>(),
        CanForceEnterWater);

    int memberCoveredCells = draft.Placements.Sum(placement => placement.CoveredCells.Count);
    AssertTrue(!draft.IsValid, "impossible deployment should remain invalid");
    AssertEqual(2, draft.Placements.Count, "invalid preview should still move every company member as a whole formation");
    AssertEqual(4, memberCoveredCells, "invalid preview should include every member footprint cell");
    AssertEqual(4, draft.Placements.SelectMany(placement => placement.CoveredCells).Distinct().Count(), "invalid preview members should not overlap each other");
}

internal static void BattlePreparationCompanyFormationPlannerIsTransactional()
{
    string plannerPath = Path.Combine(ProjectRoot(), "src", "Application", "World", "BattlePreparationCompanyFormationPlanner.cs");
    AssertTrue(File.Exists(plannerPath), "battle preparation should use an Application-owned company formation planner.");
    if (!File.Exists(plannerPath))
    {
        return;
    }

    string plannerSource = File.ReadAllText(plannerPath);
    string buildDraftBody = ExtractMethodBody(plannerSource, "public BattlePreparationCompanyFormationDraft BuildDraft(");
    string applyDraftBody = ExtractMethodBody(plannerSource, "public void ApplyDraft(");

    AssertTrue(
        plannerSource.Contains("public sealed class BattlePreparationCompanyFormationPlanner", StringComparison.Ordinal) &&
        plannerSource.Contains("BattlePreparationCompanyFormationDraft", StringComparison.Ordinal) &&
        plannerSource.Contains("BattlePreparationCompanyPlacementDraft", StringComparison.Ordinal),
        "company formation planning should expose explicit draft objects instead of mutating UI state directly.");
    AssertTrue(
        plannerSource.Contains("BattleFootprintCells.Enumerate", StringComparison.Ordinal) &&
        plannerSource.Contains("ValidateMemberCells", StringComparison.Ordinal) &&
        plannerSource.Contains("placement_cell_occupied", StringComparison.Ordinal) &&
        plannerSource.Contains("placement_cell_not_deployable", StringComparison.Ordinal),
        "formation draft validation should include every member footprint, deployment-zone legality, and occupancy.");
    AssertTrue(
        !buildDraftBody.Contains("PreferredPlacements[", StringComparison.Ordinal) &&
        !buildDraftBody.Contains(".PreferredPlacements.Add", StringComparison.Ordinal) &&
        applyDraftBody.Contains("PreferredPlacements", StringComparison.Ordinal),
        "formation hover draft must not mutate request placements until a valid drop is committed.");
}

internal static void BattlePreparationHudUsesSceneAuthoredDockLayout()
{
    string rootSource = ReadWorldSiteRootSource();
    string siteHudScene = File.ReadAllText(Path.Combine(ProjectRoot(), "scenes", "world", "ui", "WorldSitePeacetimeHud.tscn"));
    string layoutPath = Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites", "BattlePreparationHudLayout.cs");

    AssertTrue(
        !File.Exists(layoutPath) &&
        !rootSource.Contains("BattlePreparationHudLayout", StringComparison.Ordinal),
        "battle preparation HUD docks should be laid out by authored Godot scene nodes, not a C# anchor/offset layout helper.");
    AssertTrue(
        siteHudScene.Contains("node name=\"OverlayHost\" type=\"Control\" parent=\".\"", StringComparison.Ordinal) &&
        siteHudScene.Contains("node name=\"BattlePreparationRosterDock\" type=\"PanelContainer\" parent=\"OverlayHost\"", StringComparison.Ordinal) &&
        siteHudScene.Contains("node name=\"BattlePreparationPlanBar\" type=\"PanelContainer\" parent=\"OverlayHost\"", StringComparison.Ordinal) &&
        siteHudScene.Contains("node name=\"BattlePreparationObjectiveThumbnailDock\" type=\"Control\" parent=\"MinimapHost\"", StringComparison.Ordinal),
        "battle preparation HUD docks should be authored as normal Godot Control/Container nodes in the HUD scene.");
    AssertTrue(
        siteHudScene.Contains("anchor_top = 1.0", StringComparison.Ordinal) &&
        siteHudScene.Contains("anchor_bottom = 1.0", StringComparison.Ordinal) &&
        siteHudScene.Contains("anchor_left = 0.5", StringComparison.Ordinal) &&
        siteHudScene.Contains("anchor_right = 0.5", StringComparison.Ordinal) &&
        siteHudScene.Contains("anchor_left = 1.0", StringComparison.Ordinal),
        "scene-authored battle preparation docks should use Godot anchors for lower-left, lower-center, and lower-right placement.");
    AssertTrue(
        rootSource.Contains("_battlePreparationHudRestPositions", StringComparison.Ordinal) &&
        !ExtractMethodBody(rootSource, "private void SetBattlePreparationHudRetreated(").Contains("Vector2.Zero", StringComparison.Ordinal),
        "HUD retreat must restore authored scene positions instead of tweening anchored controls back to (0,0).");
}

internal static void BattlePreparationHudRetreatsDuringCompanyDrag()
{
    string rootSource = ReadWorldSiteRootSource();
    string beginDragBody = ExtractMethodBody(rootSource, "private void BeginBattlePreparationCompanyDrag(");
    string clearDragBody = ExtractMethodBody(rootSource, "private void ClearBattlePreparationCompanyDragState(");

    AssertTrue(
        rootSource.Contains("_battlePreparationHudRetreatTween", StringComparison.Ordinal) &&
        rootSource.Contains("SetBattlePreparationHudRetreated", StringComparison.Ordinal) &&
        rootSource.Contains("CreateTween().BindNode(this)", StringComparison.Ordinal) &&
        rootSource.Contains("TweenProperty", StringComparison.Ordinal),
        "battle preparation HUD retreat should be a single tween-controlled path for persistent controls.");
    AssertTrue(
        beginDragBody.Contains("SetBattlePreparationHudRetreated(true", StringComparison.Ordinal) &&
        clearDragBody.Contains("SetBattlePreparationHudRetreated(false", StringComparison.Ordinal),
        "company drag should slide persistent HUD offscreen at start and return it after drop or cancel.");
}

internal static void BattlePreparationLaunchRequiresExplicitCompanyPlans()
{
    string rootSource = ReadWorldSiteRootSource();
    string canLaunchBody = ExtractMethodBody(rootSource, "private bool CanLaunchPreparedBattle(");

    AssertTrue(
        canLaunchBody.Contains("BuildDeployedBattlePreparationPlayerGroups", StringComparison.Ordinal) &&
        canLaunchBody.Contains("deployedGroups.Count == 0", StringComparison.Ordinal) &&
        !canLaunchBody.Contains("ArePlayerRequestSlotsPlaced", StringComparison.Ordinal) &&
        canLaunchBody.Contains("foreach (BattleRuntimeCommandGroupView group in deployedGroups)", StringComparison.Ordinal) &&
        canLaunchBody.Contains("IsBattlePreparationCompanyPlaced(group)", StringComparison.Ordinal) &&
        canLaunchBody.Contains("_explicitBattlePreparationRuleGroups.Contains(group.GroupKey)", StringComparison.Ordinal),
        "start battle should require at least one deployed player company and validate only deployed companies.");
}

internal static void BattlePreparationLaunchExcludesReserveGroupsBeforeRuntime()
{
    string rootSource = ReadWorldSiteRootSource();
    string launchBody = ExtractMethodBody(rootSource, "private void LaunchPreparedBattle()");
    string reserveBody = ExtractMethodBody(rootSource, "private void ExcludeUndeployedBattlePreparationReserveGroups(");

    AssertTrue(
        launchBody.Contains("ExcludeUndeployedBattlePreparationReserveGroups(request)", StringComparison.Ordinal) &&
        launchBody.IndexOf("ExcludeUndeployedBattlePreparationReserveGroups(request)", StringComparison.Ordinal) <
        launchBody.IndexOf("ActivateBattleRuntime();", StringComparison.Ordinal),
        "battle preparation should prune reserve groups from the active request before Runtime activation.");
    AssertTrue(
        reserveBody.Contains("request.PlayerForces = request.PlayerForces", StringComparison.Ordinal) &&
        reserveBody.Contains("request.PlayerBattleGroupPlans.Remove", StringComparison.Ordinal) &&
        reserveBody.Contains("BattlePreparationReserveGroupsExcluded", StringComparison.Ordinal),
        "reserve pruning should remove undeployed player forces and plans from the Runtime request with a diagnostic.");
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
