internal static partial class WorldSiteDeploymentCacheRegressionCases
{
internal static void BattlePreparationMapDragUsesRequestBackedPlacements()
{
    string presentationSource = ReadWorldSitePresentationSource();
    string interactionSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites", "WorldSiteRoot.SiteInteraction.cs"));

    AssertTrue(
        presentationSource.Contains("BattlePreparationPlacementDragContext", StringComparison.Ordinal) &&
        presentationSource.Contains("BattlePreparationDeploymentDragController", StringComparison.Ordinal) &&
        interactionSource.Contains("_battlePreparationDeploymentDragController.TryResolveDragContext", StringComparison.Ordinal),
        "battle preparation map drag should resolve placement metadata from the battle request as well as site placements");
    AssertTrue(
        interactionSource.Contains("_battlePreparationDeploymentDragController.TryMovePlacement", StringComparison.Ordinal),
        "dropping a battle-preparation map entity should update request-backed placements without requiring a WorldSiteState placement row");
    AssertTrue(
        presentationSource.Contains("FootprintSize = _resolveForceFootprintSize(force)", StringComparison.Ordinal) &&
        interactionSource.Contains("dragContext.ForceId", StringComparison.Ordinal) &&
        interactionSource.Contains("dragContext.ForceIndex", StringComparison.Ordinal),
        "map drag preview, validation, and occupancy should use the dragged request force footprint and self identity");
}

internal static void BattlePreparationMapDragUsesSameDeploymentZoneRestrictionForBothSides()
{
    string presentationSource = ReadWorldSitePresentationSource();
    string interactionSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites", "WorldSiteRoot.SiteInteraction.cs"));

    AssertTrue(
        presentationSource.Contains("ShouldRestrictDeploymentZone", StringComparison.Ordinal),
        "battle preparation should make deployment-zone restriction an explicit side-aware rule");
    AssertTrue(
        interactionSource.Contains("BattlePreparationDeploymentDragController.ShouldRestrictDeploymentZone(dragContext)", StringComparison.Ordinal) &&
        interactionSource.Contains("IsBattlePreparationFootprintDeployable", StringComparison.Ordinal),
        "map drag should route both sides through the same authored deployment-zone validation");
    AssertTrue(
        !presentationSource.Contains("dragContext.FallbackFaction != BattleFaction.Enemy", StringComparison.Ordinal),
        "enemy deployment map drags should not bypass authored DeploymentZone markers");
}
}
