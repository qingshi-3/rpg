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

internal static void BattlePreparationTopPromptUsesAuthoredNonBlockingHudNode()
{
    string root = ProjectRoot();
    string scene = File.ReadAllText(Path.Combine(root, "scenes", "world", "ui", "WorldSitePeacetimeHud.tscn"));
    string nodeRefsSource = File.ReadAllText(Path.Combine(
        root,
        "src",
        "Presentation",
        "World",
        "Sites",
        "WorldSitePeacetimeHudNodeRefs.cs"));
    string rootSource = ReadWorldSiteRootSource();

    AssertTrue(
        scene.Contains("BattlePreparationTopPromptDock", StringComparison.Ordinal) &&
        scene.Contains("BattlePreparationTopPromptLabel", StringComparison.Ordinal) &&
        scene.Contains("mouse_filter = 2", StringComparison.Ordinal),
        "battle preparation destination prompt should be authored in the HUD scene and ignore mouse input");
    AssertTrue(
        nodeRefsSource.Contains("internal Label BattlePreparationTopPromptLabel", StringComparison.Ordinal) &&
        nodeRefsSource.Contains("BattlePreparationTopPromptLabel = Get<Label>", StringComparison.Ordinal),
        "HUD node refs should expose the authored top prompt label");
    AssertTrue(
        rootSource.Contains("private Label _battlePreparationTopPromptLabel", StringComparison.Ordinal) &&
        rootSource.Contains("SetBattlePreparationTopPrompt", StringComparison.Ordinal) &&
        rootSource.Contains("右键选择部队目的地", StringComparison.Ordinal),
        "WorldSiteRoot should bind and refresh a concise Chinese top-center destination prompt");
}

internal static void BattlePreparationRightClickStoresInitialDestinationBeacon()
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
        "preparation right-click destination input should run before generic deployment drag handling");
    AssertTrue(
        destinationInputBody.Contains("_isBattlePreparationActive", StringComparison.Ordinal) &&
        destinationInputBody.Contains("MouseButton.Right", StringComparison.Ordinal) &&
        destinationInputBody.Contains("BuildSelectedBattlePreparationDestinationBeaconGroupKeys", StringComparison.Ordinal) &&
        destinationInputBody.Contains("ApplyBattlePreparationDestinationBeaconToPlan", StringComparison.Ordinal) &&
        destinationInputBody.Contains("RefreshBattlePreparationDestinationBeaconOverlays", StringComparison.Ordinal) &&
        destinationInputBody.Contains("GetViewport()?.SetInputAsHandled()", StringComparison.Ordinal),
        "preparation right-click should store an initial beacon for the selected deployed group or multi-selection");
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
    string rootSource = ReadWorldSiteRootSource();
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
        canLaunchBody.Contains("右键", StringComparison.Ordinal) &&
        !canLaunchBody.Contains("ObjectiveZoneId", StringComparison.Ordinal) &&
        !canLaunchBody.Contains("_explicitBattlePreparationRuleGroups.Contains", StringComparison.Ordinal),
        "launch readiness should require deployed groups to have initial beacons but not objective-zone or posture choices");
}
}
