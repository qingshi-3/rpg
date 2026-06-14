internal static partial class WorldSiteDeploymentCacheRegressionCases
{
internal static void BattlePreparationMapDragRequiresRequestBackedPlacementBeforeSiteMove()
{
    string rootSource = ReadWorldSiteRootSource();
    string controllerSource = File.ReadAllText(Path.Combine(
        ProjectRoot(),
        "src",
        "Presentation",
        "World",
        "Sites",
        "BattlePreparationDeploymentDragController.cs"));
    string moveBody = ExtractMethodBody(controllerSource, "public bool TryMovePlacement(");
    int requestGuardIndex = moveBody.IndexOf("dragContext.RequestPlacement == null", StringComparison.Ordinal);
    int siteMoveIndex = moveBody.IndexOf("_deploymentTargetEvaluator.TryMoveToGridCell", StringComparison.Ordinal);

    AssertTrue(
        requestGuardIndex >= 0,
        "battle-preparation map drag should explicitly reject placements missing request authority");
    AssertTrue(
        siteMoveIndex < 0 || requestGuardIndex < siteMoveIndex,
        "battle-preparation map drag must not mutate site placement state before confirming request-backed placement authority");
}

internal static void BattlePreparationSinglePlacementDragUsesLightweightRefresh()
{
    string rootSource = ReadWorldSiteRootSource();
    string interactionBody = ExtractMethodBody(rootSource, "private void HandleSiteDeploymentDragInput(");

    AssertTrue(
        rootSource.Contains("RefreshBattlePreparationAfterSinglePlacementDrag", StringComparison.Ordinal),
        "successful battle-preparation single-placement drag should use a lightweight refresh method");
    AssertTrue(
        interactionBody.Contains("RefreshBattlePreparationAfterSinglePlacementDrag", StringComparison.Ordinal),
        "single-placement drag success should update the current entity and controls without full map entity rebuild");
}
}
