using Rpg.Application.Battle.Snapshots;

internal static partial class WorldSiteDeploymentCacheRegressionCases
{
internal static void BattlePreparationRosterClickStartsPlacementFollowMode()
{
    string root = ProjectRoot();
    string rootSource = ReadWorldSiteRootSource();
    string rowSource = File.ReadAllText(Path.Combine(
        root,
        "src",
        "Presentation",
        "World",
        "Sites",
        "BattlePreparationRosterRow.cs"));
    string selectedBody = ExtractMethodBody(rootSource, "private void OnBattlePreparationCompanySelected(");

    AssertTrue(
        rowSource.Contains("EmitSignal(SignalName.Selected", StringComparison.Ordinal) &&
        rowSource.Contains("!mouseButton.Pressed", StringComparison.Ordinal) &&
        rowSource.Contains("AcceptEvent()", StringComparison.Ordinal),
        "clicking a roster row should still emit the selected group without requiring drag motion");
    AssertTrue(
        rootSource.Contains("BeginBattlePreparationCompanyPlacementFollow", StringComparison.Ordinal) &&
        selectedBody.Contains("BeginBattlePreparationCompanyPlacementFollow(groupKey)", StringComparison.Ordinal),
        "selecting a preparation roster row should enter formation-follow placement mode immediately");
    AssertTrue(
        !selectedBody.Contains("BindBattlePreparationObjectiveThumbnail", StringComparison.Ordinal),
        "roster click should not refresh the old objective thumbnail selection flow");
}

internal static void BattlePreparationPlacementFollowCommitsOnLeftClickPress()
{
    string rootSource = ReadWorldSiteRootSource();
    string inputBody = ExtractMethodBody(rootSource, "private void HandleBattlePreparationCompanyDragInput(");

    AssertTrue(
        inputBody.Contains("InputEventMouseMotion", StringComparison.Ordinal) &&
        inputBody.Contains("UpdateBattlePreparationCompanyDragPreview", StringComparison.Ordinal),
        "placement-follow mode should keep updating the full formation preview while the mouse moves");
    AssertTrue(
        inputBody.Contains("MouseButton.Left", StringComparison.Ordinal) &&
        inputBody.Contains("Pressed: true", StringComparison.Ordinal) &&
        inputBody.Contains("TryCommitBattlePreparationCompanyPlacement", StringComparison.Ordinal) &&
        !inputBody.Contains("Pressed: false", StringComparison.Ordinal),
        "placement-follow mode should commit on the next legal left click, not on drag-button release");
    AssertTrue(
        inputBody.Contains("BattlePreparationCompanyPlacementRejected", StringComparison.Ordinal) &&
        inputBody.Contains("return;", StringComparison.Ordinal),
        "invalid placement clicks should keep the preview active instead of restoring and ending placement-follow mode");
}

internal static void BattlePreparationDestinationTargetingUsesCurvedGuideOverlay()
{
    string root = ProjectRoot();
    string scene = File.ReadAllText(Path.Combine(root, "scenes", "world", "ui", "WorldSitePeacetimeHud.tscn"));
    string guideScenePath = Path.Combine(root, "scenes", "world", "ui", "BattlePreparationDestinationGuideOverlay.tscn");
    string guideScene = File.Exists(guideScenePath) ? File.ReadAllText(guideScenePath) : "";
    string guideOverlayPath = Path.Combine(
        root,
        "src",
        "Presentation",
        "World",
        "Sites",
        "BattlePreparationDestinationGuideOverlay.cs");
    string guideOverlaySource = File.Exists(guideOverlayPath) ? File.ReadAllText(guideOverlayPath) : "";
    string rootSource = ReadWorldSiteRootSource();
    string presentationSource = ReadWorldSitePresentationSource();

    AssertTrue(
        File.Exists(guideOverlayPath) &&
        File.Exists(guideScenePath) &&
        guideScene.Contains("[node name=\"BattlePreparationDestinationGuideOverlay\" type=\"Node2D\"", StringComparison.Ordinal) &&
        guideScene.Contains("[node name=\"ArcBody\" type=\"Line2D\" parent=\".\"]", StringComparison.Ordinal) &&
        guideScene.Contains("width = 4.0", StringComparison.Ordinal) &&
        guideScene.Contains("width_curve = SubResource", StringComparison.Ordinal) &&
        guideScene.Contains("[node name=\"ArrowHeadLeft\" type=\"Line2D\" parent=\".\"]", StringComparison.Ordinal) &&
        guideScene.Contains("[node name=\"ArrowHeadRight\" type=\"Line2D\" parent=\".\"]", StringComparison.Ordinal) &&
        !guideScene.Contains("ArcGlow", StringComparison.Ordinal) &&
        !guideScene.Contains("ArrowGlow", StringComparison.Ordinal) &&
        !guideScene.Contains("Polygon2D", StringComparison.Ordinal) &&
        !guideScene.Contains("antialiased = true", StringComparison.Ordinal) &&
        guideOverlaySource.Contains("partial class BattlePreparationDestinationGuideOverlay", StringComparison.Ordinal) &&
        guideOverlaySource.Contains("SetGuide(", StringComparison.Ordinal) &&
        guideOverlaySource.Contains("ClearGuide()", StringComparison.Ordinal) &&
        guideOverlaySource.Contains("Line2D", StringComparison.Ordinal) &&
        guideOverlaySource.Contains("EaseOutCubic", StringComparison.Ordinal) &&
        guideOverlaySource.Contains("ApplyArrowLines", StringComparison.Ordinal) &&
        !guideOverlaySource.Contains("Polygon2D", StringComparison.Ordinal) &&
        !guideOverlaySource.Contains("DrawPolyline", StringComparison.Ordinal) &&
        !guideOverlaySource.Contains("DrawPolygon", StringComparison.Ordinal),
        "battle preparation destination targeting should use the card target selector's pixel-style Line2D width-curve arc with line-stroke arrowheads");
    AssertTrue(
        rootSource.Contains("_battlePreparationDestinationGuideOverlay", StringComparison.Ordinal) &&
        presentationSource.Contains("BattlePreparationDestinationGuideOverlayScenePath", StringComparison.Ordinal) &&
        presentationSource.Contains("Instantiate<BattlePreparationDestinationGuideOverlay>", StringComparison.Ordinal) &&
        !presentationSource.Contains("new BattlePreparationDestinationGuideOverlay()", StringComparison.Ordinal) &&
        presentationSource.Contains("BeginBattlePreparationDestinationTargeting", StringComparison.Ordinal) &&
        presentationSource.Contains("UpdateBattlePreparationDestinationTargetingGuide", StringComparison.Ordinal) &&
        presentationSource.Contains("ClearBattlePreparationDestinationTargeting", StringComparison.Ordinal),
        "WorldSiteRoot should own an explicit preparation destination-targeting lifecycle around the guide overlay");
    AssertTrue(
        presentationSource.Contains("Input.MouseMode = Input.MouseModeEnum.Hidden", StringComparison.Ordinal) &&
        presentationSource.Contains("Input.MouseMode = _battlePreparationDestinationPreviousMouseMode", StringComparison.Ordinal) &&
        presentationSource.Contains("BattleGridHighlightKind.Hover", StringComparison.Ordinal),
        "destination targeting should hide the system cursor while keeping grid hover available for cell positioning");
    AssertTrue(
        scene.Contains("node name=\"BattlePreparationLaunchDock\"", StringComparison.Ordinal) &&
        scene.Contains("node name=\"BattlePreparationStartButton\"", StringComparison.Ordinal) &&
        !scene.Contains("node name=\"BattlePreparationPlanBar\"", StringComparison.Ordinal),
        "battle preparation should remove the old bottom plan bar and keep only the lower-right launch control");
}

internal static void BattlePreparationLeftClickStoresInitialDestinationBeacon()
{
    string root = ProjectRoot();
    string rootSource = ReadWorldSiteRootSource();
    string inputBody = ExtractMethodBody(rootSource, "public override void _Input(InputEvent @event)");
    string destinationInputBody = ExtractMethodBody(rootSource, "private bool TryHandleBattlePreparationDestinationBeaconInput(InputEvent inputEvent)");
    string planSource = File.ReadAllText(Path.Combine(root, "src", "Application", "Battle", "Snapshots", "BattleGroupPlanSnapshot.cs"));
    string probeSource = File.ReadAllText(Path.Combine(root, "src", "Application", "Battle", "BattleGroupSessionProbeService.cs"));

    AssertTrue(
        inputBody.Contains("TryHandleBattlePreparationDestinationBeaconInput(@event)", StringComparison.Ordinal) &&
        inputBody.IndexOf("TryHandleBattlePreparationDestinationBeaconInput(@event)", StringComparison.Ordinal) <
        inputBody.IndexOf("HandleSiteDeploymentDragInput(@event)", StringComparison.Ordinal),
        "preparation destination input should run before generic deployment drag handling");
    AssertTrue(
        destinationInputBody.Contains("_isBattlePreparationActive", StringComparison.Ordinal) &&
        destinationInputBody.Contains("_battlePreparationDestinationTargetingActive", StringComparison.Ordinal) &&
        destinationInputBody.Contains("MouseButton.Left", StringComparison.Ordinal) &&
        !destinationInputBody.Contains("MouseButton.Right", StringComparison.Ordinal) &&
        destinationInputBody.Contains("BuildSelectedBattlePreparationDestinationBeaconGroupKeys", StringComparison.Ordinal) &&
        destinationInputBody.Contains("ApplyBattlePreparationDestinationBeaconToPlan", StringComparison.Ordinal) &&
        destinationInputBody.Contains("ClearBattlePreparationDestinationTargeting", StringComparison.Ordinal) &&
        destinationInputBody.Contains("RefreshBattlePreparationDestinationBeaconOverlays", StringComparison.Ordinal) &&
        destinationInputBody.Contains("GetViewport()?.SetInputAsHandled()", StringComparison.Ordinal),
        "preparation destination targeting should store an initial beacon on left-click for the selected deployed group");
    AssertTrue(
        planSource.Contains("HasInitialDestinationBeacon", StringComparison.Ordinal) &&
        planSource.Contains("InitialDestinationCellX", StringComparison.Ordinal) &&
        planSource.Contains("InitialDestinationCellY", StringComparison.Ordinal) &&
        planSource.Contains("InitialDestinationCellHeight", StringComparison.Ordinal),
        "battle group plans should carry preparation-seeded initial destination beacon facts");
    AssertTrue(
        probeSource.Contains("HasInitialDestinationBeacon = source.HasInitialDestinationBeacon", StringComparison.Ordinal) &&
        probeSource.Contains("InitialDestinationCellX = source.InitialDestinationCellX", StringComparison.Ordinal),
        "battle group session probe should copy initial beacon facts from request plans into Runtime snapshots");
}

internal static void BattlePreparationLaunchRequiresInitialBeaconAndHidesObjectiveThumbnail()
{
    string root = ProjectRoot();
    string rootSource = ReadWorldSiteRootSource();
    string scene = File.ReadAllText(Path.Combine(root, "scenes", "world", "ui", "WorldSitePeacetimeHud.tscn"));
    string bindBody = ExtractMethodBody(rootSource, "private void BindBattlePreparationPanel(");
    string canLaunchBody = ExtractMethodBody(rootSource, "private bool CanLaunchPreparedBattle(");
    string hudVisibleBody = ExtractMethodBody(rootSource, "private void SetBattlePreparationHudVisible(");

    AssertTrue(
        !bindBody.Contains("BindBattlePreparationObjectiveThumbnail", StringComparison.Ordinal),
        "battle preparation refresh should not bind the old objective thumbnail as a mandatory target selection step");
    AssertTrue(
        hudVisibleBody.Contains("_battlePreparationObjectiveThumbnailDock.Visible = false", StringComparison.Ordinal),
        "current battle preparation HUD should keep the objective thumbnail dock hidden");
    AssertTrue(
        canLaunchBody.Contains("HasBattlePreparationInitialDestinationBeacon", StringComparison.Ordinal) &&
        !canLaunchBody.Contains("鍙抽敭", StringComparison.Ordinal) &&
        !canLaunchBody.Contains("ObjectiveZoneId", StringComparison.Ordinal) &&
        !canLaunchBody.Contains("_explicitBattlePreparationRuleGroups.Contains", StringComparison.Ordinal),
        "launch readiness should require deployed groups to have initial beacons but not objective-zone or posture choices");
    AssertTrue(
        scene.Contains("node name=\"BattlePreparationLaunchDock\"", StringComparison.Ordinal) &&
        scene.Contains("node name=\"BattlePreparationStartButton\"", StringComparison.Ordinal) &&
        !scene.Contains("node name=\"BattlePreparationPlanBar\"", StringComparison.Ordinal),
        "launch readiness should be surfaced through the lower-right start button, not the removed bottom plan bar");
}
}
