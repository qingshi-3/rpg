internal static partial class WorldSiteDeploymentCacheRegressionCases
{
internal static void BattlePreparationPlanChangesUseLightweightRefresh()
{
    string rootSource = ReadWorldSitePresentationSource();
    string selectObjectiveBody = ExtractMethodBody(rootSource, "private void SelectBattlePreparationObjectiveZone(");
    string selectRuleBody = ExtractMethodBody(rootSource, "private void SelectBattlePreparationEngagementRule(");
    string submitCommandBody = ExtractMethodBody(rootSource, "private void SubmitBattleRuntimeCommand(");
    string dialogCompanyBody = ExtractMethodBody(rootSource, "private void OnBattleObjectiveDialogCompanySelected(");
    string refreshPlanBody = ExtractMethodBody(rootSource, "private void RefreshBattlePreparationPlanUi(");

    AssertTrue(
        rootSource.Contains("RefreshBattlePreparationPlanUi", StringComparison.Ordinal),
        "battle preparation should expose a lightweight plan refresh path separate from map entity rebuilds");
    AssertTrue(
        !refreshPlanBody.Contains("RefreshBattlePreparationMapEntities", StringComparison.Ordinal) &&
        !refreshPlanBody.Contains("ClearBattleEntities", StringComparison.Ordinal),
        "lightweight plan refresh must not rebuild map entities");
    AssertTrue(
        selectObjectiveBody.Contains("RefreshBattlePreparationPlanUi", StringComparison.Ordinal) &&
        !selectObjectiveBody.Contains("RefreshBattlePreparationUi", StringComparison.Ordinal),
        "objective-zone selection should update plan controls without rebuilding all deployed unit entities");
    AssertTrue(
        selectRuleBody.Contains("RefreshBattlePreparationPlanUi", StringComparison.Ordinal) &&
        !selectRuleBody.Contains("RefreshBattlePreparationUi", StringComparison.Ordinal),
        "engagement-rule selection should update plan controls without rebuilding all deployed unit entities");
    AssertTrue(
        submitCommandBody.Contains("RefreshBattlePreparationPlanUi", StringComparison.Ordinal) &&
        !submitCommandBody.Contains("RefreshBattlePreparationUi", StringComparison.Ordinal),
        "pre-battle mode command selection should update plan controls without rebuilding all deployed unit entities");
    AssertTrue(
        dialogCompanyBody.Contains("RefreshBattlePreparationPlanUi", StringComparison.Ordinal) &&
        !dialogCompanyBody.Contains("RefreshBattlePreparationUi", StringComparison.Ordinal),
        "objective-map company selection should update plan controls without rebuilding all deployed unit entities");
}

internal static void BattlePreparationCompanyDragDropRebuildsOnlyMovedCompany()
{
    string rootSource = ReadWorldSitePresentationSource();
    string handleDragBody = ExtractMethodBody(rootSource, "private void HandleBattlePreparationCompanyDragInput(");
    string afterCompanyDragBody = ExtractMethodBody(rootSource, "private void RefreshBattlePreparationAfterCompanyDrag(");
    string rebuildCompanyBody = ExtractMethodBody(rootSource, "private void RebuildBattlePreparationCompanyMapEntities(");
    string createCompanyBody = ExtractMethodBody(rootSource, "private void CreateBattlePreparationCompanyMapEntities(");

    AssertTrue(
        handleDragBody.Contains("RefreshBattlePreparationAfterCompanyDrag", StringComparison.Ordinal) &&
        !handleDragBody.Contains("RefreshBattlePreparationUi", StringComparison.Ordinal),
        "company drag drop should not run the full battle-preparation refresh that restarts every deployed unit");
    AssertTrue(
        afterCompanyDragBody.Contains("RebuildBattlePreparationCompanyMapEntities", StringComparison.Ordinal) &&
        afterCompanyDragBody.Contains("RefreshBattlePreparationPlanUi", StringComparison.Ordinal) &&
        !afterCompanyDragBody.Contains("RefreshBattlePreparationMapEntities", StringComparison.Ordinal),
        "company drag completion should rebuild only the moved company and then refresh controls");
    AssertTrue(
        rebuildCompanyBody.Contains("RemoveBattlePreparationCompanyPreviewEntities", StringComparison.Ordinal) &&
        rebuildCompanyBody.Contains("CreateBattlePreparationCompanyMapEntities", StringComparison.Ordinal) &&
        !rebuildCompanyBody.Contains("ClearBattleEntities", StringComparison.Ordinal) &&
        !rebuildCompanyBody.Contains("RefreshBattlePreparationMapEntities", StringComparison.Ordinal),
        "company map rebuild should remove and recreate only the selected company's placement entities");
    AssertTrue(
        createCompanyBody.Contains("PlaceBattleEntityOnGrid", StringComparison.Ordinal) &&
        !createCompanyBody.Contains("PlaceBattleEntitiesOnGrid", StringComparison.Ordinal),
        "newly created company entities should be placed individually instead of forcing all unit positions through the full placement loop");
}
}
