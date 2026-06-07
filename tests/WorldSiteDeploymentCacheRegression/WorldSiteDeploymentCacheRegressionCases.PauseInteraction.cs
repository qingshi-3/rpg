internal static partial class WorldSiteDeploymentCacheRegressionCases
{
    internal static void BattleRuntimePauseKeepsObservationInputAlive()
    {
        string root = ProjectRoot();
        string siteRootSource = ReadWorldSiteRootSource();
        string pauseBody = ExtractMethodBody(siteRootSource, "private void ApplyBattleRuntimeScenePause(bool paused, string reason)");
        string battleCameraSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "Battle", "BattleCameraController.cs"));

        AssertTrue(
            pauseBody.Contains("CaptureBattleRuntimePauseProcessMode(this, ProcessModeEnum.Always)", StringComparison.Ordinal) &&
            pauseBody.Contains("CaptureBattleRuntimePauseProcessMode(_siteHudRoot, ProcessModeEnum.Always)", StringComparison.Ordinal) &&
            pauseBody.Contains("CaptureBattleRuntimePauseProcessMode(_siteModalHost, ProcessModeEnum.Always)", StringComparison.Ordinal),
            "tactical pause should keep root and HUD command input processable");
        AssertTrue(
            pauseBody.Contains("CaptureBattleRuntimePauseProcessMode(_mainWorldViewportHost, ProcessModeEnum.Always)", StringComparison.Ordinal) &&
            pauseBody.Contains("CaptureBattleRuntimePauseProcessMode(_mainWorldViewport, ProcessModeEnum.Always)", StringComparison.Ordinal) &&
            pauseBody.Contains("CaptureBattleRuntimePauseProcessMode(_battleCamera, ProcessModeEnum.Always)", StringComparison.Ordinal) &&
            pauseBody.Contains("CaptureBattleRuntimePauseProcessMode(_highlightOverlay, ProcessModeEnum.Always)", StringComparison.Ordinal),
            "tactical pause should keep the viewport bridge, battle camera, and hover/highlight overlay processable for observation input");
        AssertTrue(
            pauseBody.Contains("CaptureBattleRuntimePauseProcessMode(_mapRoot, ProcessModeEnum.Pausable)", StringComparison.Ordinal) &&
            pauseBody.Contains("CaptureBattleRuntimePauseProcessMode(_activeSiteMap, ProcessModeEnum.Pausable)", StringComparison.Ordinal) &&
            pauseBody.Contains("CaptureBattleRuntimePauseProcessMode(_unitRoot, ProcessModeEnum.Pausable)", StringComparison.Ordinal) &&
            pauseBody.Contains("CaptureBattleRuntimePauseProcessMode(_sitePlacementEntityRoot, ProcessModeEnum.Pausable)", StringComparison.Ordinal),
            "tactical pause should keep battle fact, environment, and unit presentation nodes paused");
        AssertTrue(
            pauseBody.Contains("_battleCamera?.SetTacticalPauseActive(paused)", StringComparison.Ordinal) &&
            battleCameraSource.Contains("public void SetTacticalPauseActive(bool paused)", StringComparison.Ordinal) &&
            battleCameraSource.Contains("if (_tacticalPauseActive)", StringComparison.Ordinal) &&
            battleCameraSource.Contains("CancelFollowTween();", StringComparison.Ordinal),
            "tactical pause should block runtime-driven camera follow while manual camera navigation stays processable");
    }

    internal static void BattleRuntimePauseKeepsHighlightsStaticButInputRefreshable()
    {
        string root = ProjectRoot();
        string siteRootSource = ReadWorldSiteRootSource();
        string pauseBody = ExtractMethodBody(siteRootSource, "private void ApplyBattleRuntimeScenePause(bool paused, string reason)");
        string highlightSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "Battle", "BattleGridHighlightOverlay.cs"));
        string dynamicStyleBody = ExtractMethodBody(highlightSource, "private void ApplyDynamicRangeStyle(CanvasItem item, BattleGridHighlightKind kind)");
        string targetLockRingBody = ExtractMethodBody(highlightSource, "private void AddTargetLockRing()");
        string perceptionShaderBody = ExtractMethodBody(highlightSource, "private void ApplyPerceptionRangeShader(TileMapLayer layer)");

        AssertTrue(
            pauseBody.Contains("_highlightOverlay?.SetTacticalPauseVisualsStatic(paused)", StringComparison.Ordinal),
            "world site tactical pause should tell battle highlights to keep pause-time previews visually static");
        AssertTrue(
            highlightSource.Contains("public void SetTacticalPauseVisualsStatic(bool staticVisuals)", StringComparison.Ordinal) &&
            highlightSource.Contains("private bool ShouldAnimateOverlay", StringComparison.Ordinal),
            "highlight overlay should expose an explicit pause-static visual policy without disabling hover or preview refresh");
        AssertTrue(
            dynamicStyleBody.Contains("ShouldAnimateOverlay(kind)", StringComparison.Ordinal) &&
            targetLockRingBody.Contains("ApplyDynamicRangeStyle") &&
            targetLockRingBody.Contains("BattleGridHighlightKind.Target", StringComparison.Ordinal),
            "range and target lock-ring animation should be gated separately from input-driven overlay rebuilds");
        AssertTrue(
            perceptionShaderBody.Contains("_tacticalPauseVisualsStatic", StringComparison.Ordinal) &&
            perceptionShaderBody.Contains("layer.Material = null", StringComparison.Ordinal),
            "perception range shader motion should be disabled while tactical pause freezes battle facts");
        AssertTrue(
            highlightSource.Contains("SetHoverCells", StringComparison.Ordinal) &&
            highlightSource.Contains("SetCellsBatch", StringComparison.Ordinal) &&
            highlightSource.Contains("RebuildDynamicOverlay();", StringComparison.Ordinal),
            "hover, range, target, and path previews should still rebuild from player input while paused");
    }
}
