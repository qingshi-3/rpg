internal static partial class WorldSiteDeploymentCacheRegressionCases
{
internal static void BattleMapOperationsSuppressBlockingScreenHud()
{
    string root = ProjectRoot();
    string siteRootDir = Path.Combine(root, "src", "Presentation", "World", "Sites");
    string rootSource = ReadWorldSiteRootSource();
    string suppressionPath = Path.Combine(siteRootDir, "WorldSiteRoot.BattleMapOperationHud.cs");
    string suppressorPath = Path.Combine(siteRootDir, "BattleMapOperationHudSuppressor.cs");
    string cancelCoordinatorPath = Path.Combine(siteRootDir, "BattleMapOperationHudCancelCoordinator.cs");
    string suppressionSource = File.Exists(suppressionPath) ? File.ReadAllText(suppressionPath) : "";
    string suppressorSource = File.Exists(suppressorPath) ? File.ReadAllText(suppressorPath) : "";
    string cancelCoordinatorSource = File.Exists(cancelCoordinatorPath) ? File.ReadAllText(cancelCoordinatorPath) : "";
    string commandHudSource = File.ReadAllText(Path.Combine(siteRootDir, "WorldSiteRoot.BattleRuntimeCommandHud.cs"));
    string beaconSource = File.ReadAllText(Path.Combine(siteRootDir, "WorldSiteRoot.BattleRuntimeDestinationBeacon.cs"));
    string dragSource = File.ReadAllText(Path.Combine(siteRootDir, "WorldSiteRoot.BattlePreparationDrag.cs"));
    string siteHudSource = File.ReadAllText(Path.Combine(siteRootDir, "WorldSiteRoot.SiteManagementHud.cs"));
    string presentationSource = ReadWorldSitePresentationSource();

    string inputBody = ExtractMethodBody(rootSource, "public override void _Input(InputEvent @event)");
    string beginSkillTargetBody = ExtractMethodBody(commandHudSource, "private void BeginBattleRuntimeHeroSkillTargetPicking(");
    string cancelSkillTargetBody = ExtractMethodBody(commandHudSource, "private void CancelBattleRuntimeHeroSkillTargetPicking(");
    string targetInputBody = ExtractMethodBody(commandHudSource, "private bool TryHandleBattleRuntimeHeroSkillTargetInput(InputEvent inputEvent)");
    string runtimeDestinationBody = ExtractMethodBody(beaconSource, "private bool TryHandleBattleRuntimeDestinationBeaconInput(InputEvent inputEvent)");
    string preparationDestinationBody = ExtractMethodBody(beaconSource, "private bool TryHandleBattlePreparationDestinationBeaconInput(InputEvent inputEvent)");
    string dragStartBody = ExtractMethodBody(dragSource, "private void BeginBattlePreparationCompanyDrag(");
    string dragInputBody = ExtractMethodBody(dragSource, "private void HandleBattlePreparationCompanyDragInput(");
    string dragClearBody = ExtractMethodBody(dragSource, "private void ClearBattlePreparationCompanyDragState()");
    string runtimePausePresentationBody = ExtractMethodBody(commandHudSource, "private void RefreshBattleRuntimeCommandPausePresentation()");
    string preparationVisibleBody = ExtractMethodBody(siteHudSource, "private void SetBattlePreparationHudVisible(");

    AssertTrue(
        File.Exists(suppressionPath) &&
        File.Exists(suppressorPath) &&
        File.Exists(cancelCoordinatorPath) &&
        rootSource.Contains("BattleMapOperationHudSuppressionReason", StringComparison.Ordinal) &&
        rootSource.Contains("_battleMapOperationHudSuppressionReason", StringComparison.Ordinal) &&
        suppressorSource.Contains("internal sealed class BattleMapOperationHudSuppressor", StringComparison.Ordinal) &&
        suppressionSource.Contains("EnterBattleMapOperationHudSuppression", StringComparison.Ordinal) &&
        suppressionSource.Contains("ExitBattleMapOperationHudSuppression", StringComparison.Ordinal),
        "battle map operation HUD suppression should live behind a named state/helper instead of scattered pointer gates");
    AssertTrue(
        suppressionSource.Contains("_battleRuntimeSummaryBar", StringComparison.Ordinal) &&
        suppressionSource.Contains("_battleRuntimeCommandBar", StringComparison.Ordinal) &&
        suppressionSource.Contains("_siteBottomCommandHost", StringComparison.Ordinal) &&
        suppressionSource.Contains("_battlePreparationRosterDock", StringComparison.Ordinal) &&
        suppressionSource.Contains("_battlePreparationStartButton", StringComparison.Ordinal) &&
        suppressionSource.Contains("_siteMinimapHost", StringComparison.Ordinal) &&
        suppressorSource.Contains("Control.MouseFilterEnum.Ignore", StringComparison.Ordinal),
        "suppression should hide or mouse-ignore all blocking runtime and preparation screen-space HUD controls, including the lower-right launch button");
    AssertTrue(
        beginSkillTargetBody.Contains("EnterBattleMapOperationHudSuppression(BattleMapOperationHudSuppressionReason.RuntimeSkillTarget", StringComparison.Ordinal) &&
        cancelSkillTargetBody.Contains("ExitBattleMapOperationHudSuppression(BattleMapOperationHudSuppressionReason.RuntimeSkillTarget", StringComparison.Ordinal) &&
        !targetInputBody.Contains("BattleRuntimeCommandHudPointerGate.ContainsPointer", StringComparison.Ordinal),
        "runtime skill target picking should suppress command HUD instead of leaving visible HUD pointer gates over the map");
    AssertTrue(
        dragStartBody.Contains("EnterBattleMapOperationHudSuppression(BattleMapOperationHudSuppressionReason.PreparationPlacement", StringComparison.Ordinal) &&
        dragClearBody.Contains("ExitBattleMapOperationHudSuppression(BattleMapOperationHudSuppressionReason.PreparationPlacement", StringComparison.Ordinal) &&
        dragInputBody.Contains("BeginBattlePreparationDestinationTargeting(groupKey)", StringComparison.Ordinal) &&
        presentationSource.Contains("EnterBattleMapOperationHudSuppression(BattleMapOperationHudSuppressionReason.PreparationDestinationBeacon", StringComparison.Ordinal),
        "battle-preparation formation placement should suppress HUD while placing, then switch into destination-beacon suppression after placement");
    AssertTrue(
        preparationDestinationBody.Contains("EnterBattleMapOperationHudSuppression(BattleMapOperationHudSuppressionReason.PreparationDestinationBeacon", StringComparison.Ordinal) &&
        preparationDestinationBody.Contains("ClearBattlePreparationDestinationTargeting", StringComparison.Ordinal) &&
        presentationSource.Contains("ExitBattleMapOperationHudSuppression(BattleMapOperationHudSuppressionReason.PreparationDestinationBeacon", StringComparison.Ordinal) &&
        !preparationDestinationBody.Contains("IsPointerOverSiteHud(inputEvent)", StringComparison.Ordinal) &&
        preparationDestinationBody.Contains("MouseButton.Left", StringComparison.Ordinal) &&
        !preparationDestinationBody.Contains("MouseButton.Right", StringComparison.Ordinal),
        "battle-preparation destination targeting should operate on the map under HUD, accept left-click, and restore HUD only after an accepted destination");
    AssertTrue(
        runtimeDestinationBody.Contains("EnterBattleMapOperationHudSuppression(BattleMapOperationHudSuppressionReason.RuntimeDestinationBeacon", StringComparison.Ordinal) &&
        runtimeDestinationBody.Contains("ExitBattleMapOperationHudSuppression(BattleMapOperationHudSuppressionReason.RuntimeDestinationBeacon", StringComparison.Ordinal) &&
        runtimeDestinationBody.IndexOf("EnterBattleMapOperationHudSuppression", StringComparison.Ordinal) <
        runtimeDestinationBody.IndexOf("TryGetMouseGridPosition(out GridPosition position)", StringComparison.Ordinal) &&
        !runtimeDestinationBody.Contains("BattleRuntimeCommandHudPointerGate.ContainsPointer", StringComparison.Ordinal),
        "runtime destination-beacon clicks should enter map-operation suppression before resolving the target cell and should not be blocked by visible HUD pointer gates");
    AssertTrue(
        runtimePausePresentationBody.Contains("ApplyBattleMapOperationHudSuppressionVisibility", StringComparison.Ordinal) &&
        preparationVisibleBody.Contains("ApplyBattleMapOperationHudSuppressionVisibility", StringComparison.Ordinal),
        "HUD refresh paths should re-apply suppression so late UI refreshes cannot reopen panels during an active map operation");
    AssertTrue(
        inputBody.Contains("TryHandleBattleMapOperationHudSuppressionCancelInput(@event)", StringComparison.Ordinal) &&
        inputBody.IndexOf("TryHandleBattleRuntimeHeroSkillTargetInput(@event)", StringComparison.Ordinal) <
        inputBody.IndexOf("TryHandleBattleMapOperationHudSuppressionCancelInput(@event)", StringComparison.Ordinal) &&
        inputBody.IndexOf("TryHandleBattleMapOperationHudSuppressionCancelInput(@event)", StringComparison.Ordinal) <
        inputBody.IndexOf("TryHandleBattleRuntimeDestinationBeaconInput(@event)", StringComparison.Ordinal) &&
        inputBody.IndexOf("TryHandleBattleMapOperationHudSuppressionCancelInput(@event)", StringComparison.Ordinal) <
        inputBody.IndexOf("TryHandleBattlePreparationDestinationBeaconInput(@event)", StringComparison.Ordinal),
        "map-operation cancel should run after skill target mouse handling but before destination input so ui_cancel restores HUD without stealing destination submit");
    AssertTrue(
        suppressionSource.Contains("BattleMapOperationHudCancelCoordinator.TryHandle", StringComparison.Ordinal) &&
        suppressionSource.Contains("BattleMapOperationCancelAction", StringComparison.Ordinal) &&
        cancelCoordinatorSource.Contains("inputEvent.IsActionPressed(cancelAction)", StringComparison.Ordinal) &&
        cancelCoordinatorSource.Contains("BattleMapOperationHudSuppressionReason.RuntimeSkillTarget", StringComparison.Ordinal) &&
        cancelCoordinatorSource.Contains("BattleMapOperationHudSuppressionReason.PreparationPlacement", StringComparison.Ordinal) &&
        cancelCoordinatorSource.Contains("BattleMapOperationHudSuppressionReason.PreparationDestinationBeacon", StringComparison.Ordinal) &&
        cancelCoordinatorSource.Contains("BattleMapOperationHudSuppressionReason.RuntimeDestinationBeacon", StringComparison.Ordinal) &&
        suppressionSource.Contains("CancelBattleRuntimeHeroSkillTargetPicking(\"map_operation_cancel\")", StringComparison.Ordinal) &&
        suppressionSource.Contains("RestoreBattlePreparationCompanyPlacements()", StringComparison.Ordinal) &&
        suppressionSource.Contains("ClearBattlePreparationCompanyDragState()", StringComparison.Ordinal) &&
        suppressionSource.Contains("ClearBattlePreparationDestinationTargeting(\"map_operation_cancel\")", StringComparison.Ordinal) &&
        presentationSource.Contains("ExitBattleMapOperationHudSuppression(BattleMapOperationHudSuppressionReason.PreparationDestinationBeacon", StringComparison.Ordinal) &&
        suppressionSource.Contains("ExitBattleMapOperationHudSuppression(BattleMapOperationHudSuppressionReason.RuntimeDestinationBeacon", StringComparison.Ordinal),
        "ui_cancel should restore the correct previous HUD layer for skill targeting, preparation placement, and destination-beacon target retries");
}
}
