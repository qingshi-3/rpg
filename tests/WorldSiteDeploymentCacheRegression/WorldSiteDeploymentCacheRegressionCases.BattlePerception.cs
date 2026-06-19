internal static partial class WorldSiteDeploymentCacheRegressionCases
{
    internal static void BattleRuntimePerceptionOverlayDefaultsVisibleAndTogglesWithInputAction()
    {
        string root = ProjectRoot();
        string siteRootSource = ReadWorldSiteRootSource();
        string overlaySource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Presentation",
            "World",
            "Sites",
            "WorldSiteRoot.BattlePerceptionOverlay.cs"));
        string highlightKindSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Presentation",
            "Battle",
            "BattleGridHighlightKind.cs"));
        string highlightSource = ReadBattleGridHighlightOverlaySource();
        string tileFactorySource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Presentation",
            "Battle",
            "Highlights",
            "BattleGridHighlightTileSetFactory.cs"));
        string tileRendererSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Presentation",
            "Battle",
            "Highlights",
            "BattleGridHighlightTileLayerRenderer.cs"));
        string perceptionShaderPath = Path.Combine(
            root,
            "assets",
            "battle",
            "shaders",
            "perception_range_highlight.gdshader");
        string policySource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Application",
            "Battle",
            "BattlePerceptionPolicy.cs"));
        string runtimeSource = string.Join(
            "\n",
            Directory.GetFiles(Path.Combine(root, "src", "Runtime", "Battle"), "*.cs", SearchOption.AllDirectories)
                .OrderBy(path => path)
                .Select(File.ReadAllText));
        string projectConfig = File.ReadAllText(Path.Combine(root, "project.godot"));

        AssertTrue(
            policySource.Contains("DefaultLocalPerceptionRange = 4", StringComparison.Ordinal) &&
            runtimeSource.Contains("BattlePerceptionPolicy.DefaultLocalPerceptionRange", StringComparison.Ordinal),
            "runtime and presentation should share the same local perception range policy");
        AssertTrue(
            siteRootSource.Contains("TryHandleBattlePerceptionOverlayInput(@event)", StringComparison.Ordinal) &&
            overlaySource.Contains("battle_perception_overlay_toggle", StringComparison.Ordinal) &&
            overlaySource.Contains(".IsActionPressed(", StringComparison.Ordinal) &&
            !overlaySource.Contains("Key.E", StringComparison.Ordinal) &&
            projectConfig.Contains("battle_perception_overlay_toggle", StringComparison.Ordinal) &&
            projectConfig.Contains("\"physical_keycode\":69", StringComparison.Ordinal) &&
            overlaySource.Contains("BattlePerceptionOverlayToggled", StringComparison.Ordinal),
            "battle runtime should toggle perception range debug overlay through the Input Map action bound to E by default");
        AssertTrue(
            siteRootSource.Contains("EnableBattlePerceptionOverlayForRuntime();", StringComparison.Ordinal) &&
            overlaySource.Contains("_battlePerceptionOverlayVisible = true;", StringComparison.Ordinal) &&
            overlaySource.Contains("RefreshBattlePerceptionOverlay();", StringComparison.Ordinal),
            "battle runtime should show unit perception ranges by default while preserving the overlay toggle action");
        AssertTrue(
            highlightKindSource.Contains("FriendlyPerception", StringComparison.Ordinal) &&
            highlightKindSource.Contains("EnemyPerception", StringComparison.Ordinal) &&
            highlightSource.Contains("FriendlyPerceptionColor", StringComparison.Ordinal) &&
            highlightSource.Contains("EnemyPerceptionColor", StringComparison.Ordinal),
            "highlight overlay should expose separate player and enemy perception layers");
        AssertTrue(
            overlaySource.Contains("SetCellsBatch", StringComparison.Ordinal) &&
            overlaySource.Contains("BattleGridHighlightKind.FriendlyPerception", StringComparison.Ordinal) &&
            overlaySource.Contains("BattleGridHighlightKind.EnemyPerception", StringComparison.Ordinal) &&
            overlaySource.Contains("GetAxisGap", StringComparison.Ordinal),
            "perception overlay should draw footprint-aware ranges for both factions without presentation pathfinding");
        AssertTrue(File.Exists(perceptionShaderPath), "perception range overlay should use an authored shader resource");
        string perceptionShader = File.Exists(perceptionShaderPath)
            ? File.ReadAllText(perceptionShaderPath)
            : "";
        AssertTrue(
            perceptionShader.Contains("shader_type canvas_item;", StringComparison.Ordinal) &&
            perceptionShader.Contains("edge_glow", StringComparison.Ordinal) &&
            perceptionShader.Contains("scanline_strength", StringComparison.Ordinal) &&
            perceptionShader.Contains("TIME", StringComparison.Ordinal),
            "perception range shader should own soft edge glow and low-noise interior motion");
        AssertTrue(
            !perceptionShader.Contains("return;", StringComparison.Ordinal),
            "Godot fragment shaders should avoid return statements because Godot 4 rejects them in processor functions");
        AssertTrue(
            highlightSource.Contains("PerceptionRangeShaderPath", StringComparison.Ordinal) &&
            highlightSource.Contains("ApplyPerceptionRangeShader", StringComparison.Ordinal) &&
            highlightSource.Contains("BattleGridHighlightTileShape.SoftAura", StringComparison.Ordinal),
            "perception layers should bind a dedicated shader and use soft aura tiles instead of hard diamond cells");
        AssertTrue(
            tileFactorySource.Contains("DrawSoftAuraHighlightTile", StringComparison.Ordinal) &&
            tileRendererSource.Contains("configureLayer?.Invoke(layer, kind)", StringComparison.Ordinal),
            "tile highlight rendering should support shader-backed soft perception layers without line drawing");
    }
}
