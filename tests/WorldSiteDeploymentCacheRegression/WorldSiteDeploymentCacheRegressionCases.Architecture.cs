using Godot;
using Rpg.Application.Battle;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;

internal static partial class WorldSiteDeploymentCacheRegressionCases
{
internal static void WorldSiteRootDelegatesDeploymentCacheConstruction()
{
    string rootSource = ReadWorldSiteRootSource();
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

internal static void WorldSiteRootDelegatesDeploymentTargetValidation()
{
    string rootSource = ReadWorldSiteRootSource();

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

internal static void WorldSiteRootDelegatesDeploymentTerrainReconciliation()
{
    string rootSource = ReadWorldSiteRootSource();

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

internal static void WorldSiteRootDelegatesBattleDeploymentPreparation()
{
    string rootSource = ReadWorldSiteRootSource();

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

internal static void WorldSiteRootDelegatesBattleLaunchHandoff()
{
    string rootSource = ReadWorldSiteRootSource();

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

internal static void WorldSiteRootUsesBattleGroupRuntimeActivationThroughAdapter()
{
    string rootSource = ReadWorldSiteRootSource();

    AssertTrue(
        !rootSource.Contains("UseAutoBattleRuntime", StringComparison.Ordinal),
        "WorldSiteRoot should not keep the dead auto battle runtime switch after manual fallback removal");
    AssertTrue(
        rootSource.Contains("_battleGroupRuntimeAdapter.TryStartActiveBattle", StringComparison.Ordinal),
        "WorldSiteRoot should delegate live battle startup to the battle-group runtime adapter");
    AssertTrue(
        rootSource.Contains("_battleGroupRuntimeAdapter.CompleteResolvedBattle", StringComparison.Ordinal),
        "WorldSiteRoot should delegate live battle completion to the battle-group runtime adapter");
    AssertTrue(
        rootSource.Contains("ActivateBattleGroupRuntime", StringComparison.Ordinal),
        "WorldSiteRoot should keep battle-group runtime activation in a focused helper");
    AssertTrue(
        !rootSource.Contains("_turnController?.StartBattle()", StringComparison.Ordinal),
        "WorldSiteRoot should not keep the legacy manual battle activation path");
    AssertTrue(
        !rootSource.Contains("WorldSiteAutoBattleAdapter", StringComparison.Ordinal) &&
        !rootSource.Contains("AutoBattleRuntimeController", StringComparison.Ordinal),
        "WorldSiteRoot should not instantiate or own the old auto battle runtime directly");
}

internal static void WorldSiteRootSyncsRequestPlacementHeightsBeforeRuntimeSnapshot()
{
    string rootSource = ReadWorldSiteRootSource();
    string methodBody = ExtractMethodBody(rootSource, "private void ApplyBattleNavigationSnapshot(BattleStartRequest request)");

    AssertTrue(
        methodBody.Contains("SyncPreferredPlacementHeightsToCurrentNavigationSurfaces", StringComparison.Ordinal),
        "battle runtime handoff should sync preferred placement heights against the current navigation surface");
    AssertTrue(
        methodBody.IndexOf("SyncPreferredPlacementHeightsToCurrentNavigationSurfaces", StringComparison.Ordinal) <
        methodBody.IndexOf("BattleNavigationSnapshotBuilder.ApplyToRequest", StringComparison.Ordinal),
        "placement height sync must happen before exporting the navigation snapshot");
    AssertTrue(
        methodBody.Contains("ResolveForceCanEnterWater", StringComparison.Ordinal),
        "placement height sync should preserve force water-entry rules");
}

internal static void PerformanceDebugOverlayIsHiddenByDefault()
{
    string overlayScene = File.ReadAllText(Path.Combine(ProjectRoot(), "scenes", "debug", "PerformanceDebugOverlay.tscn"));

    AssertTrue(
        overlayScene.Contains("VisibleOnStart = false", StringComparison.Ordinal),
        "performance and memory debug overlay should be hidden on startup");
}

internal static void LegacyManualBattleAuthorityDocsStayDeleted()
{
    string root = ProjectRoot();
    string docsRoot = Path.Combine(root, "docs");
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

    AssertTrue(!Directory.Exists(docsRoot), "legacy docs root should stay deleted after authority consolidation");

    string combinedAuthority = string.Join("\n", new[]
    {
        File.ReadAllText(Path.Combine(root, "gameplay-design", "details", "combat-command", "README.md")),
        File.ReadAllText(Path.Combine(root, "system-design", "hero-led-light-rts-system-architecture.md")),
        File.ReadAllText(Path.Combine(root, "system-design", "battle-runtime-architecture.md")),
        File.ReadAllText(Path.Combine(root, "system-design", "battle-command-architecture.md"))
    });
    AssertTrue(!combinedAuthority.Contains("mechanism-battle-slice", StringComparison.Ordinal), "current authority should not route to manual mechanism slice");
    AssertTrue(!combinedAuthority.Contains("battle-ui-interaction-review", StringComparison.Ordinal), "current authority should not route to manual action menu review");
    AssertTrue(!combinedAuthority.Contains("enemy-intent-design", StringComparison.Ordinal), "current authority should not route to old turn intent design");
    AssertTrue(!combinedAuthority.Contains("battle-action-architecture", StringComparison.Ordinal), "current authority should not route to AP action architecture");
    AssertTrue(!combinedAuthority.Contains("battle-input-command-architecture", StringComparison.Ordinal), "current authority should not route to manual command architecture");
    AssertTrue(!combinedAuthority.Contains("battle-runtime-responsibility-review", StringComparison.Ordinal), "current authority should not route to manual runtime review");
    AssertTrue(!combinedAuthority.Contains("intent-system", StringComparison.Ordinal), "current authority should not route to legacy intent system");
    AssertTrue(!combinedAuthority.Contains("card-system", StringComparison.Ordinal), "current authority should not route to legacy AP card system");
    AssertTrue(!combinedAuthority.Contains("targeting-and-preview", StringComparison.Ordinal), "current authority should not route to manual targeting preview vocabulary");
    AssertTrue(!combinedAuthority.Contains("Current manual-map flow", StringComparison.Ordinal), "current authority should not describe manual map flow as current authority");
    AssertTrue(!combinedAuthority.Contains("BattleCommandController", StringComparison.Ordinal), "current authority should not route future work through manual command controller");
    AssertTrue(combinedAuthority.Contains("hero-led light RTS", StringComparison.OrdinalIgnoreCase), "current authority should route future work to the accepted light RTS direction");
}

internal static void WorldSiteRootHasNoDeadAutoBattleRuntimeSwitch()
{
    string rootSource = ReadWorldSiteRootSource();

    AssertTrue(
        !rootSource.Contains("UseAutoBattleRuntime", StringComparison.Ordinal),
        "WorldSiteRoot should not expose a runtime toggle when the legacy manual fallback is gone");
    AssertTrue(
        !rootSource.Contains("_autoBattleAdapter", StringComparison.Ordinal) &&
        !rootSource.Contains("ActivateAutoBattleRuntime", StringComparison.Ordinal),
        "WorldSiteRoot should not keep auto battle activation after target runtime migration");
    AssertTrue(
        !rootSource.Contains("_turnController?.StartBattle()", StringComparison.Ordinal),
        "manual battle start should be removed after scene dependencies are detached");
}

internal static void WorldSiteRootDetachesLegacyManualBattleRuntime()
{
    string rootSource = ReadWorldSiteRootSource();
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
        rootSource.Contains("ActivateBattleGroupRuntime", StringComparison.Ordinal),
        "WorldSiteRoot should keep only the battle-group runtime activation path");
}

internal static void WorldSiteSceneDetachesLegacyManualBattleRuntime()
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

internal static void LegacyManualBattleCodeFilesAreDeleted()
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
        Path.Combine(root, "src", "Presentation", "Battle", "Actions", "BattleActionExecutionContext.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Actions", "BattleActionExecutionContext.cs.uid"),
        Path.Combine(root, "src", "Presentation", "Battle", "Actions", "BattleActionExecutor.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Actions", "BattleActionExecutor.cs.uid"),
        Path.Combine(root, "src", "Presentation", "Battle", "Actions", "BattleActionRequest.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Actions", "BattleActionRequest.cs.uid"),
        Path.Combine(root, "src", "Presentation", "Battle", "AI", "GreedyAlliedIntentPlanner.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "AI", "GreedyAlliedIntentPlanner.cs.uid"),
        Path.Combine(root, "src", "Presentation", "Battle", "AI", "GreedyEnemyIntentPlanner.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "AI", "GreedyEnemyIntentPlanner.cs.uid"),
        Path.Combine(root, "src", "Presentation", "Battle", "AI", "IEnemyIntentPlanner.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "AI", "IEnemyIntentPlanner.cs.uid"),
        Path.Combine(root, "src", "Presentation", "Battle", "Intents", "BattleIntentResolver.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Intents", "BattleIntentResolver.cs.uid"),
        Path.Combine(root, "src", "Presentation", "Battle", "Intents", "BattleIntentPreview.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Intents", "BattleIntentPreview.cs.uid"),
        Path.Combine(root, "src", "Presentation", "Battle", "Intents", "BattleIntentTargetPolicy.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Intents", "BattleIntentTargetPolicy.cs.uid"),
        Path.Combine(root, "src", "Presentation", "Battle", "Intents", "BattleIntentType.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Intents", "BattleIntentType.cs.uid"),
        Path.Combine(root, "src", "Presentation", "Battle", "Threats", "BattleAttackPatternContext.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Threats", "BattleAttackPatternContext.cs.uid"),
        Path.Combine(root, "src", "Presentation", "Battle", "Threats", "BattleAttackPatternFactory.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Threats", "BattleAttackPatternFactory.cs.uid"),
        Path.Combine(root, "src", "Presentation", "Battle", "Threats", "BattleThreatProjection.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Threats", "BattleThreatProjection.cs.uid"),
        Path.Combine(root, "src", "Presentation", "Battle", "Threats", "BattleThreatProjectionBuilder.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Threats", "BattleThreatProjectionBuilder.cs.uid"),
        Path.Combine(root, "src", "Presentation", "Battle", "Threats", "BattleThreatSource.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Threats", "BattleThreatSource.cs.uid"),
        Path.Combine(root, "src", "Presentation", "Battle", "Threats", "IBattleAttackPattern.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Threats", "IBattleAttackPattern.cs.uid"),
        Path.Combine(root, "src", "Presentation", "Battle", "Threats", "ManhattanRangeAttackPattern.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Threats", "ManhattanRangeAttackPattern.cs.uid"),
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

internal static void LegacyCombatApAuthoringFieldsAreDeleted()
{
    string root = ProjectRoot();
    string[] sourceFiles =
    {
        Path.Combine(root, "src", "Definitions", "Battle", "BattleUnitDefinition.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Entities", "MovementComponent.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Entities", "AttackComponent.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Entities", "BattleUnitFactory.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Entities", "BattleUnitRoot.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Actions", "BattleActionResult.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Intents", "BattleIntentTemplate.cs"),
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

    AssertTrue(
        !File.Exists(Path.Combine(root, "src", "Definitions", "Battle", "Abilities", "AbilityDefinition.cs")),
        "legacy ability definition authoring file should stay deleted after skill resource cutover");

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

internal static void WorldSiteRootAppendsBattleGroupRuntimeReportSummaryToNotice()
{
    string rootSource = ReadWorldSiteRootSource();

    AssertTrue(
        rootSource.Contains("BuildBattleGroupRuntimeReportSummary", StringComparison.Ordinal),
        "WorldSiteRoot should summarize the target runtime battle report");
    AssertTrue(
        rootSource.Contains("BuildBattleGroupRuntimeReturnNotice", StringComparison.Ordinal),
        "WorldSiteRoot should keep notice composition in a focused helper");
    AssertTrue(
        rootSource.Contains("battleNotice", StringComparison.Ordinal),
        "WorldSiteRoot should pass the battle runtime summary notice into existing non-battle UI refresh");
    AssertTrue(
        !rootSource.Contains("AutoBattleReportPanel", StringComparison.Ordinal) &&
        !rootSource.Contains("AutoBattleReportSummaryFormatter", StringComparison.Ordinal),
        "this slice should not add a full report panel to WorldSiteRoot");
}

internal static void StrategicWorldBattleTriggerDialogOmitsStrategicPreparationMetadata()
{
    string rootSource = ReadStrategicWorldRootSource();
    string preBattleBody = ExtractMethodBody(rootSource, "private string BuildPreBattleText(BattleStartRequest request)");
    string preBattleDataBody = ExtractMethodBody(rootSource, "private StrategicBattleGateDialogData BuildPreBattleDialogData(BattleStartRequest request)");
    string showDialogBody = ExtractMethodBody(rootSource, "private void ShowPreBattleDialog()");
    string announcementBody = ExtractMethodBody(rootSource, "private void BeginBattleAnnouncement(BattleStartRequest request)");
    string focusBody = ExtractMethodBody(rootSource, "private Vector2 ResolveBattleFocusMapPosition(BattleStartRequest request)");

    AssertTrue(
        preBattleDataBody.Contains("Title = \"触发战斗\"", StringComparison.Ordinal),
        "world-map battle gate should use the accepted direct trigger title");
    AssertTrue(
        announcementBody.Contains("FocusWorldMapOn(focusPosition)", StringComparison.Ordinal) &&
        announcementBody.IndexOf("FocusWorldMapOn(focusPosition)", StringComparison.Ordinal) <
        announcementBody.IndexOf("ShowPreBattleDialog()", StringComparison.Ordinal),
        "battle trigger should focus the map location before showing the confirmation dialog");
    AssertTrue(
        focusBody.Contains("request.BattleKind == BattleKind.AssaultSite", StringComparison.Ordinal) &&
        focusBody.IndexOf("WorldSiteDefinition targetSite = queries.GetSite(request.TargetSiteId)", StringComparison.Ordinal) <
        focusBody.IndexOf("if (!string.IsNullOrWhiteSpace(request.SourceArmyId)", StringComparison.Ordinal),
        "assault-site battle trigger focus should prioritize the hostile target site over the arrived source army");
    AssertTrue(
        !preBattleBody.Contains("BuildStrategicPreparationSummary", StringComparison.Ordinal) &&
        !rootSource.Contains("BuildStrategicPreparationSummary", StringComparison.Ordinal) &&
        !rootSource.Contains("StrategicPreparationDisplayName", StringComparison.Ordinal) &&
        !rootSource.Contains("StrategicPreparationBriefingText", StringComparison.Ordinal) &&
        !rootSource.Contains("战前准备", StringComparison.Ordinal),
        "battle trigger dialog should not expose strategic battle-preparation metadata");
}

internal static void StrategicWorldBattleGateUsesTwoStageFocusedModal()
{
    string root = ProjectRoot();
    string rootSource = ReadStrategicWorldRootSource();
    string scene = File.ReadAllText(Path.Combine(root, "scenes", "world", "ui", "PreBattleDialog.tscn"));
    string showDialogBody = ExtractMethodBody(rootSource, "private void ShowPreBattleDialog()");
    string deferBody = ExtractMethodBody(rootSource, "private void DeferPendingBattleDecision()");

    string briefDetailsButton = ExtractSceneNodeBlock(scene, "[node name=\"BriefDetailsButton\"");
    string briefDeferButton = ExtractSceneNodeBlock(scene, "[node name=\"BriefDeferButton\"");
    string detailPanel = ExtractSceneNodeBlock(scene, "[node name=\"DetailPanel\"");
    string detailDeferButton = ExtractSceneNodeBlock(scene, "[node name=\"DetailDeferButton\"");

    AssertTrue(
        !scene.Contains("type=\"AcceptDialog\"", StringComparison.Ordinal) &&
        scene.Contains("StrategicBattleGateDialog.cs", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"BriefPanel\"", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"DetailPanel\"", StringComparison.Ordinal),
        "strategic battle gate should be a resource-authored two-stage modal, not a one-button AcceptDialog");
    AssertTrue(
        scene.Contains("text = \"进入战斗\"", StringComparison.Ordinal) &&
        briefDetailsButton.Contains("text = \"查看详情\"", StringComparison.Ordinal) &&
        briefDeferButton.Contains("text = \"稍后处理\"", StringComparison.Ordinal) &&
        detailDeferButton.Contains("text = \"稍后处理\"", StringComparison.Ordinal) &&
        !detailPanel.Contains("text = \"查看详情\"", StringComparison.Ordinal),
        "brief battle gate should offer enter/details/defer, while detail state should offer only enter/defer");
    AssertTrue(
        showDialogBody.Contains("HideWorldDetailSections()", StringComparison.Ordinal) &&
        showDialogBody.Contains("OpenBrief", StringComparison.Ordinal) &&
        !showDialogBody.Contains("PopupCentered", StringComparison.Ordinal),
        "showing the battle gate should hide the selected-site sheet and open the brief modal state instead of popping a legacy dialog");
    AssertTrue(
        deferBody.Contains("_pendingBattleRequest = null", StringComparison.Ordinal) &&
        deferBody.Contains("_pendingStrategicBattleActiveContext = null", StringComparison.Ordinal) &&
        deferBody.Contains("_worldClockPaused = false", StringComparison.Ordinal) &&
        !deferBody.Contains("CancelExpedition", StringComparison.Ordinal) &&
        !deferBody.Contains("ResetUnsupportedAssault", StringComparison.Ordinal),
        "defer should close the decision UI without retreating or cancelling the arrived assault army");
}

internal static void WorldSiteRuntimeReturnNoticeOmitsStrategicPreparationReportMetadata()
{
    string rootSource = ReadWorldSiteRootSource();
    string runtimeBody = ExtractMethodBody(rootSource, "private async Task PlayBattleGroupRuntimeAndApplyResultAsync(");

    AssertTrue(
        runtimeBody.Contains("BuildBattleGroupRuntimeReturnNotice(applyResult, resultReport, resolution.Request)", StringComparison.Ordinal) &&
        runtimeBody.Contains("resolution.ActiveContext.ResultEnvelope?.Report", StringComparison.Ordinal),
        "strategic battle completion should pass the Active Context result envelope report into return notice composition");
    AssertTrue(
        !rootSource.Contains("BuildBattlePreparationReportSummary", StringComparison.Ordinal) &&
        !rootSource.Contains("StrategicPreparationReportText", StringComparison.Ordinal) &&
        !rootSource.Contains("StrategicPreparationDisplayName", StringComparison.Ordinal),
        "battle return notice should not compose deleted strategic preparation report metadata");
    AssertTrue(
        !rootSource.Contains("AutoBattleReportPanel", StringComparison.Ordinal) &&
        !rootSource.Contains("AutoBattleReportSummaryFormatter", StringComparison.Ordinal),
        "strategic reward feedback should not add a full report panel to this slice");
}

internal static void WorldSiteRuntimeReturnNoticeIncludesStrategicRewardAndHeroFeedback()
{
    string rootSource = ReadWorldSiteRootSource();
    string applyBody = ExtractMethodBody(rootSource, "private WorldActionResult ApplyStrategicBattleResultToWorld(");

    AssertTrue(
        applyBody.Contains("BuildStrategicBattleFeedbackReturnNotice", StringComparison.Ordinal),
        "strategic battle result writeback should compose reward, progression, hero, and equipment feedback into the existing return notice");
    AssertTrue(
        rootSource.Contains("StrategicBattleFeedbackRecord", StringComparison.Ordinal) &&
        rootSource.Contains("HeroFeedback", StringComparison.Ordinal) &&
        rootSource.Contains("EquipmentSamples", StringComparison.Ordinal),
        "WorldSiteRoot should format the Strategic Management battle feedback record instead of inventing a presentation-only result model");
    AssertTrue(
        rootSource.Contains("奖励", StringComparison.Ordinal) &&
        rootSource.Contains("英雄反馈", StringComparison.Ordinal) &&
        rootSource.Contains("装备", StringComparison.Ordinal),
        "strategic battle feedback notice should include player-facing Chinese labels for reward, hero feedback, and equipment");
    AssertTrue(
        !rootSource.Contains("AutoBattleReportPanel", StringComparison.Ordinal) &&
        !rootSource.Contains("AutoBattleReportSummaryFormatter", StringComparison.Ordinal),
        "strategic reward feedback should reuse the current return notice surface instead of adding a full report panel");
}
}
