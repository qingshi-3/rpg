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

    AssertTrue(idsSource.Contains("HeroUnit = \"f1_general\"", StringComparison.Ordinal), "v0 hero id should be f1_general");
    AssertTrue(idsSource.Contains("DefaultCorpsUnit = \"f1_melee\"", StringComparison.Ordinal), "v0 default corps id should be f1_melee");
    AssertTrue(idsSource.Contains("EnemyLeaderUnit = \"boss_city\"", StringComparison.Ordinal), "v0 enemy leader id should be boss_city");
    AssertTrue(initialState.Contains("res://assets/battle/units/f1_将军/unit.tres", StringComparison.Ordinal), "initial player hero should use authored f1 general resource");
    AssertTrue(initialState.Contains("res://assets/battle/units/首领_城域守卫/unit.tres", StringComparison.Ordinal), "initial enemy leader should use authored city boss resource");
    AssertTrue(!initialState.Contains("res://assets/battle/units/skeleton_warrior.tres", StringComparison.Ordinal), "v0 bonefield should not start from the old skeleton placeholder roster");
    AssertTrue(!initialState.Contains("res://assets/battle/units/skeleton_archer.tres", StringComparison.Ordinal), "v0 bonefield should not start from the old skeleton archer placeholder roster");
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
        rootSource.Contains("TryResolveRuntimeVisualPathStep", StringComparison.Ordinal) &&
        rootSource.Contains("MovementRangeFinder.FindReachableCells", StringComparison.Ordinal) &&
        !rootSource.Contains("targetGrid.GridX.CompareTo(actorGrid.GridX)", StringComparison.Ordinal),
        "runtime event playback should path through valid grid surfaces instead of stepping directly toward the target");
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

internal static void PresentationUiModeBindersStayInPresentation()
{
    string root = ProjectRoot();
    string uiModeSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "WorldUiMode.cs"));
    string strategicSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "StrategicWorldRoot.DetailHud.cs"));
    string siteManagementSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "Sites", "WorldSiteRoot.SiteManagementHud.cs"));
    string battlePreparationSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "Sites", "WorldSiteRoot.BattlePreparationHud.cs"));
    string battleRuntimeSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRuntime.cs"));

    AssertTrue(
        uiModeSource.Contains("namespace Rpg.Presentation.World", StringComparison.Ordinal) &&
        uiModeSource.Contains("public enum WorldUiMode", StringComparison.Ordinal) &&
        uiModeSource.Contains("StrategicSelection", StringComparison.Ordinal) &&
        uiModeSource.Contains("ExpeditionDraft", StringComparison.Ordinal) &&
        uiModeSource.Contains("SiteManagement", StringComparison.Ordinal) &&
        uiModeSource.Contains("SiteExploration", StringComparison.Ordinal) &&
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
        rootSource.Contains("SetDeploymentDragComponent(entity, placementId, fallbackFaction == BattleFaction.Player)", StringComparison.Ordinal),
        "battle preparation initialization should enable drag on player-side units through the component");
    AssertTrue(
        !rootSource.Contains("fallbackFaction != BattleFaction.Player", StringComparison.Ordinal),
        "enemy units should still be registered for battle preparation preview even though drag is disabled");
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
}
