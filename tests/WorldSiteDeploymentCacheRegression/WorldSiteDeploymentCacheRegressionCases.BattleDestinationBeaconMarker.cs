internal static partial class WorldSiteDeploymentCacheRegressionCases
{
internal static void BattleDestinationBeaconUsesReusableSceneWithHeroTextureVariable()
{
    string root = ProjectRoot();
    string markerScenePath = Path.Combine(root, "scenes", "world", "sites", "BattleDestinationBeaconMarker.tscn");
    string animatedPreviewScenePath = Path.Combine(root, "scenes", "ui", "common", "BattleUnitAnimatedPreview.tscn");
    string plinthPreviewScenePath = Path.Combine(root, "scenes", "ui", "common", "BattleUnitPlinthPreview.tscn");
    string cellFrameScenePath = Path.Combine(root, "scenes", "world", "ui", "BattleDestinationBeaconCellFrame.tscn");
    string animatedPreviewSourcePath = Path.Combine(root, "src", "Presentation", "Common", "BattleUnitAnimatedPreview.cs");
    string plinthPreviewSourcePath = Path.Combine(root, "src", "Presentation", "Common", "BattleUnitPlinthPreview.cs");
    string cellFrameSourcePath = Path.Combine(root, "src", "Presentation", "World", "Sites", "BattleDestinationBeaconCellFrame.cs");
    string markerSourcePath = Path.Combine(root, "src", "Presentation", "World", "Sites", "BattleDestinationBeaconMarker.cs");
    string presenterSourcePath = Path.Combine(root, "src", "Presentation", "World", "Sites", "BattleDestinationBeaconMarkerPresenter.cs");
    string rootSource = ReadWorldSiteRootSource();
    string beaconSource = File.ReadAllText(Path.Combine(
        root,
        "src",
        "Presentation",
        "World",
        "Sites",
        "WorldSiteRoot.BattleRuntimeDestinationBeacon.cs"));

    AssertTrue(File.Exists(markerScenePath), "destination beacon visual should be an authored reusable Godot scene.");
    AssertTrue(File.Exists(animatedPreviewScenePath), "battle unit animated preview should be an authored reusable common UI scene.");
    AssertTrue(File.Exists(plinthPreviewScenePath), "battle unit plinth preview should compose the reusable plinth plus animated hero display.");
    AssertTrue(File.Exists(cellFrameScenePath), "destination beacon should use an authored reusable target-cell frame scene.");
    AssertTrue(File.Exists(animatedPreviewSourcePath), "battle unit animated preview should expose a focused scene script.");
    AssertTrue(File.Exists(plinthPreviewSourcePath), "battle unit plinth preview should expose a focused scene script.");
    AssertTrue(File.Exists(cellFrameSourcePath), "destination beacon target-cell frame should expose a focused drawing script.");
    AssertTrue(File.Exists(markerSourcePath), "destination beacon visual should expose a focused scene script.");
    AssertTrue(File.Exists(presenterSourcePath), "destination beacon marker lifecycle should live in a focused presenter.");

    string markerScene = File.Exists(markerScenePath) ? File.ReadAllText(markerScenePath) : "";
    string animatedPreviewScene = File.Exists(animatedPreviewScenePath) ? File.ReadAllText(animatedPreviewScenePath) : "";
    string plinthPreviewScene = File.Exists(plinthPreviewScenePath) ? File.ReadAllText(plinthPreviewScenePath) : "";
    string cellFrameScene = File.Exists(cellFrameScenePath) ? File.ReadAllText(cellFrameScenePath) : "";
    string cellFrameSource = File.Exists(cellFrameSourcePath) ? File.ReadAllText(cellFrameSourcePath) : "";
    string animatedPreviewSource = File.Exists(animatedPreviewSourcePath) ? File.ReadAllText(animatedPreviewSourcePath) : "";
    string plinthPreviewSource = File.Exists(plinthPreviewSourcePath) ? File.ReadAllText(plinthPreviewSourcePath) : "";
    string markerSource = File.Exists(markerSourcePath) ? File.ReadAllText(markerSourcePath) : "";
    string presenterSource = File.Exists(presenterSourcePath) ? File.ReadAllText(presenterSourcePath) : "";
    string preparationRefreshBody = ExtractMethodBody(beaconSource, "private void RefreshBattlePreparationDestinationBeaconOverlays()");
    string runtimeRefreshBody = ExtractMethodBody(beaconSource, "private void RefreshBattleRuntimeDestinationBeaconOverlays()");

    AssertTrue(
        markerScene.Contains("20250425downArrow-Sheet.png", StringComparison.Ordinal) &&
        markerScene.Contains("BattleUnitPlinthPreview.tscn", StringComparison.Ordinal) &&
        markerScene.Contains("Arrow", StringComparison.Ordinal) &&
        markerScene.Contains("TargetCellFrame", StringComparison.Ordinal) &&
        markerScene.Contains("PlinthPreview", StringComparison.Ordinal) &&
        markerScene.Contains("recruitment_unit_plinth_disabled.png", StringComparison.Ordinal),
        "beacon marker scene should compose the down-arrow sheet, target-cell frame, and the reusable plinth-preview display.");
    AssertTrue(
        markerScene.Contains("scale = Vector2(1.35, 1.35)", StringComparison.Ordinal) &&
        markerScene.Contains("scale = Vector2(0.28, 0.28)", StringComparison.Ordinal) &&
        markerScene.Contains("[node name=\"ArrowFloatAnimator\" type=\"AnimationPlayer\" parent=\".\"]", StringComparison.Ordinal) &&
        markerScene.Contains("process_mode = 3", StringComparison.Ordinal) &&
        markerScene.Contains("autoplay = \"float\"", StringComparison.Ordinal) &&
        markerScene.Contains("loop_mode = 1", StringComparison.Ordinal) &&
        markerScene.Contains("NodePath(\"Arrow:position\")", StringComparison.Ordinal) &&
        markerScene.Contains("Vector2(0, -16)", StringComparison.Ordinal) &&
        !markerScene.Contains("PlinthSize =", StringComparison.Ordinal) &&
        !markerScene.Contains("HeroOffset =", StringComparison.Ordinal) &&
        !markerScene.Contains("HeroMaxSize =", StringComparison.Ordinal) &&
        !markerScene.Contains("HeroPreviewLayoutMode =", StringComparison.Ordinal) &&
        markerSource.Contains("public float SelectionOutlineWidth { get; set; } = 1.5f;", StringComparison.Ordinal),
        "beacon marker scene should keep the pointer readable, float the arrow, and scale the reusable plinth preview as one fixed display.");
    AssertTrue(
        markerScene.Contains("[node name=\"TargetCellFrame\" parent=\".\" instance=", StringComparison.Ordinal) &&
        cellFrameScene.Contains("BattleDestinationBeaconCellFrame.cs", StringComparison.Ordinal) &&
        cellFrameSource.Contains("DrawLine", StringComparison.Ordinal) &&
        cellFrameSource.Contains("CornerLengthRatio", StringComparison.Ordinal),
        "destination beacon should draw a hover-style four-corner frame around the target cell.");
    AssertTrue(
        !markerScene.Contains("y_sort_enabled = true", StringComparison.Ordinal) &&
        markerScene.Contains("[node name=\"DecorationRoot\" type=\"Node2D\" parent=\".\"]", StringComparison.Ordinal) &&
        markerScene.Contains("[node name=\"PlinthPreview\" parent=\"DecorationRoot\" instance=", StringComparison.Ordinal) &&
        !markerScene.Contains("[node name=\"Plinth\" type=\"Sprite2D\" parent=\"DecorationRoot\"]", StringComparison.Ordinal) &&
        !markerScene.Contains("[node name=\"HeroPreview\" parent=\"DecorationRoot\" instance=", StringComparison.Ordinal),
        "beacon marker scene should delegate hero-over-plinth stacking to the reusable plinth preview instead of owning those nodes directly.");
    AssertTrue(
        markerSource.Contains("public NodePath DecorationRootPath", StringComparison.Ordinal) &&
        markerSource.Contains("ApplyViewportAvoidance", StringComparison.Ordinal) &&
        markerScene.Contains("position = Vector2(0, -42)", StringComparison.Ordinal) &&
        markerSource.Contains("public Vector2 DecorationAboveOffset { get; set; } = new(0f, -42f);", StringComparison.Ordinal) &&
        markerSource.Contains("public float DecorationTopExtent { get; set; } = 66f;", StringComparison.Ordinal) &&
        presenterSource.Contains("ApplyViewportAvoidance", StringComparison.Ordinal),
        "beacon marker should keep the target arrow anchored while placing the hero/plinth decoration close above it and only moving the decoration below the cell near the top viewport edge.");
    AssertTrue(
        animatedPreviewScene.Contains("BattleUnitAnimatedPreview.cs", StringComparison.Ordinal) &&
        animatedPreviewScene.Contains("AnimatedSprite2D", StringComparison.Ordinal) &&
        animatedPreviewSource.Contains("public partial class BattleUnitAnimatedPreview", StringComparison.Ordinal) &&
        animatedPreviewSource.Contains("public SpriteFrames SpriteFrames", StringComparison.Ordinal) &&
        animatedPreviewSource.Contains("public string AnimationName", StringComparison.Ordinal) &&
        animatedPreviewSource.Contains("Play(", StringComparison.Ordinal),
        "shared battle unit animated preview should live under common UI scenes and play an AnimatedSprite2D animation instead of displaying one texture frame.");
    AssertTrue(
        plinthPreviewScene.Contains("BattleUnitPlinthPreview.cs", StringComparison.Ordinal) &&
        plinthPreviewScene.Contains("recruitment_unit_plinth_normal.png", StringComparison.Ordinal) &&
        plinthPreviewScene.Contains("res://scenes/ui/common/BattleUnitAnimatedPreview.tscn", StringComparison.Ordinal) &&
        plinthPreviewScene.Contains("[node name=\"Plinth\" type=\"Sprite2D\" parent=\".\"]", StringComparison.Ordinal) &&
        plinthPreviewScene.Contains("[node name=\"HeroPreview\" parent=\".\" instance=", StringComparison.Ordinal) &&
        plinthPreviewSource.Contains("public partial class BattleUnitPlinthPreview", StringComparison.Ordinal) &&
        plinthPreviewSource.Contains("public Texture2D PlinthTexture", StringComparison.Ordinal) &&
        plinthPreviewSource.Contains("private static readonly Vector2 PlinthSize = new(176f, 80f);", StringComparison.Ordinal) &&
        plinthPreviewSource.Contains("private static readonly Vector2 HeroOffset = new(0f, -39f);", StringComparison.Ordinal) &&
        plinthPreviewSource.Contains("private static readonly Vector2 HeroMaxSize = new(188f, 130f);", StringComparison.Ordinal) &&
        !plinthPreviewSource.Contains("[Export]\n    public Vector2 PlinthSize", StringComparison.Ordinal) &&
        !plinthPreviewSource.Contains("[Export]\n    public Vector2 HeroOffset", StringComparison.Ordinal) &&
        !plinthPreviewSource.Contains("[Export]\n    public Vector2 HeroMaxSize", StringComparison.Ordinal) &&
        plinthPreviewSource.Contains("public void Bind(BattleUnitAnimatedPreviewModel preview)", StringComparison.Ordinal),
        "shared battle unit plinth preview should own the fixed plinth plus animated hero alignment contract.");
    AssertTrue(
        markerSource.Contains("public SpriteFrames HeroSpriteFrames", StringComparison.Ordinal) &&
        markerSource.Contains("public string HeroAnimationName", StringComparison.Ordinal) &&
        markerScene.Contains("res://scenes/ui/common/BattleUnitPlinthPreview.tscn", StringComparison.Ordinal) &&
        markerSource.Contains("BattleUnitPlinthPreview", StringComparison.Ordinal) &&
        !markerSource.Contains("public Texture2D HeroTexture", StringComparison.Ordinal),
        "beacon marker should expose hero SpriteFrames and animation name rather than a single hero texture frame.");
    AssertTrue(
        markerScene.Contains("[node name=\"PlinthPreview\" parent=\"DecorationRoot\" instance=", StringComparison.Ordinal) &&
        markerSource.Contains("public NodePath PlinthPreviewPath", StringComparison.Ordinal) &&
        markerSource.Contains("new(\"DecorationRoot/PlinthPreview\")", StringComparison.Ordinal),
        "beacon marker script should bind the reusable plinth preview at its authored scene path so moving decoration layers cannot drop the animation.");
    AssertTrue(
        rootSource.Contains("private readonly BattleDestinationBeaconMarkerPresenter _battleDestinationBeaconMarkerPresenter", StringComparison.Ordinal) &&
        presenterSource.Contains("BattleDestinationBeaconMarkerScenePath", StringComparison.Ordinal) &&
        presenterSource.Contains("private readonly Dictionary<string, BattleDestinationBeaconMarker> _battleDestinationBeaconMarkers", StringComparison.Ordinal),
        "WorldSiteRoot should delegate reusable destination beacon marker instances to a focused presenter.");
    AssertTrue(
        presenterSource.Contains("BattleUnitPreviewResolver.ResolveAnimatedPreview", StringComparison.Ordinal) &&
        presenterSource.Contains("RefreshBattleDestinationBeaconMarkers", StringComparison.Ordinal) &&
        presenterSource.Contains("BuildBattlePreparationDestinationBeaconMarkerModels", StringComparison.Ordinal) &&
        presenterSource.Contains("BuildBattleRuntimeDestinationBeaconMarkerModels", StringComparison.Ordinal) &&
        presenterSource.Contains("OwnerBattleGroupIds.Count == 0", StringComparison.Ordinal) &&
        preparationRefreshBody.Contains("_battleDestinationBeaconMarkerPresenter.RefreshPreparation", StringComparison.Ordinal) &&
        runtimeRefreshBody.Contains("_battleDestinationBeaconMarkerPresenter.RefreshRuntime", StringComparison.Ordinal),
        "preparation and runtime beacon refresh should bind hero idle animations through the shared preview resolver and ignore ownerless runtime beacons.");
    AssertTrue(
        !preparationRefreshBody.Contains("_highlightOverlay?.SetCellsBatch((BattleGridHighlightKind.DestinationBeacon", StringComparison.Ordinal) &&
        !runtimeRefreshBody.Contains("_highlightOverlay?.SetCellsBatch((BattleGridHighlightKind.DestinationBeacon", StringComparison.Ordinal),
        "destination beacon presentation should use the marker scene instead of only painting tile highlights.");
}

internal static void BattleRuntimeSelectedDestinationBeaconUsesUnitOutlineShader()
{
    string root = ProjectRoot();
    string markerSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "Sites", "BattleDestinationBeaconMarker.cs"));
    string presenterSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "Sites", "BattleDestinationBeaconMarkerPresenter.cs"));
    string beaconSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRuntimeDestinationBeacon.cs"));
    string runtimeRefreshBody = ExtractMethodBody(beaconSource, "private void RefreshBattleRuntimeDestinationBeaconOverlays()");
    string selectBody = ExtractMethodBody(beaconSource, "private void SelectBattleRuntimeCommandGroup(string groupKey, bool additive)");
    string markerBuildBody = ExtractMethodBody(presenterSource, "private IEnumerable<BattleDestinationBeaconMarkerModel> BuildBattleRuntimeDestinationBeaconMarkerModels(");

    AssertTrue(
        runtimeRefreshBody.Contains("_selectedBattleRuntimeGroupKey", StringComparison.Ordinal) &&
        selectBody.Contains("RefreshBattleRuntimeDestinationBeaconOverlayVisibility()", StringComparison.Ordinal),
        "runtime beacon overlays should refresh selected-beacon styling when the paused selected hero changes.");
    AssertTrue(
        presenterSource.Contains("string selectedGroupKey", StringComparison.Ordinal) &&
        markerBuildBody.Contains("OwnerBattleGroupIds.Any", StringComparison.Ordinal) &&
        markerBuildBody.Contains("selectedGroupKey", StringComparison.Ordinal) &&
        markerBuildBody.Contains("IsSelected", StringComparison.Ordinal) &&
        presenterSource.Contains("model.IsSelected", StringComparison.Ordinal),
        "beacon presenter should mark only Runtime beacons owned by the current selected battle group as selected.");
    AssertTrue(
        markerSource.Contains("UnitBodyOutlineShaderPath = \"res://resource/shaders/battle/unit_body_outline.gdshader\"", StringComparison.Ordinal) &&
        markerSource.Contains("ShaderMaterial", StringComparison.Ordinal) &&
        markerSource.Contains("active_outline_enabled", StringComparison.Ordinal) &&
        markerSource.Contains("active_outline_color", StringComparison.Ordinal) &&
        markerSource.Contains("active_outline_width", StringComparison.Ordinal) &&
        markerSource.Contains("SelectionOutlineColor", StringComparison.Ordinal) &&
        markerSource.Contains("PlinthPath", StringComparison.Ordinal) &&
        markerSource.Contains("DecorationRoot/PlinthPreview/Plinth", StringComparison.Ordinal) &&
        markerSource.Contains("DecorationRoot/PlinthPreview/HeroPreview/AnimatedSprite2D", StringComparison.Ordinal) &&
        !markerSource.Contains("ApplySelectionOutlineMaterial(_arrow", StringComparison.Ordinal) &&
        !markerSource.Contains("_arrowOutlineMaterial", StringComparison.Ordinal),
        "selected beacon visuals should reuse the existing unit body outline shader on the plinth and hero idle sprite, while leaving the arrow unshaded.");
}

internal static void BattlePreparationSelectedBeaconAndGroupUseUnitOutlineShader()
{
    string root = ProjectRoot();
    string markerSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "Sites", "BattleDestinationBeaconMarker.cs"));
    string presenterSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "Sites", "BattleDestinationBeaconMarkerPresenter.cs"));
    string beaconSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRuntimeDestinationBeacon.cs"));
    string dragSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "Sites", "WorldSiteRoot.BattlePreparationDrag.cs"));
    string hudSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "Sites", "WorldSiteRoot.BattlePreparationHud.cs"));
    string refreshSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "Sites", "BattlePreparationRefresh.cs"));
    string requestDeploymentSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRequestDeployment.cs"));
    string selectionPresenterSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "Sites", "BattlePreparationCommandSelectionPresenter.cs"));
    string preparationRefreshBody = ExtractMethodBody(beaconSource, "private void RefreshBattlePreparationDestinationBeaconOverlays()");
    string preparationMarkerBuildBody = ExtractMethodBody(presenterSource, "private IEnumerable<BattleDestinationBeaconMarkerModel> BuildBattlePreparationDestinationBeaconMarkerModels(");
    string selectionBody = ExtractMethodBody(hudSource, "private void OnBattlePreparationCompanySelected(");
    string dragStartBody = ExtractMethodBody(dragSource, "private void BeginBattlePreparationCompanyDrag(");
    string mapRefreshBody = ExtractMethodBody(hudSource, "private void RefreshBattlePreparationMapEntities()");
    string dragEndRefreshBody = ExtractMethodBody(refreshSource, "private void RefreshBattlePreparationAfterCompanyDrag(");

    AssertTrue(
        presenterSource.Contains("RefreshPreparation(", StringComparison.Ordinal) &&
        presenterSource.Contains("string selectedGroupKey", StringComparison.Ordinal) &&
        preparationRefreshBody.Contains("_draggedBattlePreparationGroupKey", StringComparison.Ordinal) &&
        preparationRefreshBody.Contains("_selectedBattlePreparationPlanGroupKey", StringComparison.Ordinal),
        "preparation beacon refresh should tell the presenter which current deployment group owns the selected beacon.");
    AssertTrue(
        preparationMarkerBuildBody.Contains("normalizedSelectedGroupKey", StringComparison.Ordinal) &&
        preparationMarkerBuildBody.Contains("plannedGroup.Any", StringComparison.Ordinal) &&
        preparationMarkerBuildBody.Contains("IsSelected", StringComparison.Ordinal),
        "preparation beacon presenter should mark the beacon selected when the current deployment group owns that planned destination.");
    AssertTrue(
        markerSource.Contains("UnitBodyOutlineShaderPath = \"res://resource/shaders/battle/unit_body_outline.gdshader\"", StringComparison.Ordinal) &&
        markerSource.Contains("PlinthPath", StringComparison.Ordinal) &&
        markerSource.Contains("DecorationRoot/PlinthPreview/Plinth", StringComparison.Ordinal) &&
        markerSource.Contains("DecorationRoot/PlinthPreview/HeroPreview/AnimatedSprite2D", StringComparison.Ordinal) &&
        !markerSource.Contains("ApplySelectionOutlineMaterial(_arrow", StringComparison.Ordinal),
        "preparation selected beacons should reuse the unit outline shader on the plinth and hero sprite only.");
    AssertTrue(
        File.Exists(Path.Combine(root, "src", "Presentation", "World", "Sites", "BattlePreparationCommandSelectionPresenter.cs")) &&
        selectionPresenterSource.Contains("BattleRuntimeActorIdentity.BuildPresentationEntityToRuntimeActorMap", StringComparison.Ordinal) &&
        selectionPresenterSource.Contains("SetCommandSelectionByEntityIds", StringComparison.Ordinal) &&
        selectionPresenterSource.Contains("new HashSet<string>(StringComparer.Ordinal)", StringComparison.Ordinal),
        "preparation selected group highlighting should reuse the existing unit command-selection shader path without inventing another shader.");
    AssertTrue(
        selectionBody.Contains("BattlePreparationCommandSelectionPresenter.Apply", StringComparison.Ordinal) &&
        selectionBody.Contains("RefreshBattlePreparationDestinationBeaconOverlays()", StringComparison.Ordinal) &&
        dragStartBody.Contains("BattlePreparationCommandSelectionPresenter.Apply", StringComparison.Ordinal) &&
        mapRefreshBody.Contains("BattlePreparationCommandSelectionPresenter.Apply", StringComparison.Ordinal) &&
        dragEndRefreshBody.Contains("BattlePreparationCommandSelectionPresenter.Apply", StringComparison.Ordinal) &&
        dragEndRefreshBody.IndexOf("RebuildBattlePreparationCompanyMapEntities", StringComparison.Ordinal) <
        dragEndRefreshBody.IndexOf("BattlePreparationCommandSelectionPresenter.Apply", StringComparison.Ordinal),
        "selecting, deploying, or rebuilding preparation units should keep the current group and its beacon highlighted.");
}

internal static void BattleUnitCommandSelectionSkipsDisposedCachedEntities()
{
    string root = ProjectRoot();
    string unitRootSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "Battle", "Entities", "BattleUnitRoot.cs"));
    string selectionBody = ExtractMethodBody(unitRootSource, "private void SetCommandSelection(");

    AssertTrue(
        unitRootSource.Contains("private static bool IsLiveCommandSelectionEntity", StringComparison.Ordinal) &&
        unitRootSource.Contains("GodotObject.IsInstanceValid(entity)", StringComparison.Ordinal) &&
        unitRootSource.Contains("!entity.IsQueuedForDeletion()", StringComparison.Ordinal),
        "battle unit command selection should define one live-node guard that rejects disposed or queued battle entities.");
    AssertTrue(
        selectionBody.Contains("IsLiveCommandSelectionEntity(entity)", StringComparison.Ordinal) &&
        selectionBody.Contains("_commandSelectedEntities.Remove(entity)", StringComparison.Ordinal) &&
        selectionBody.IndexOf("IsLiveCommandSelectionEntity(entity)", StringComparison.Ordinal) <
        selectionBody.IndexOf("SetSelected(false)", StringComparison.Ordinal),
        "command selection cleanup should remove stale cached entities before calling SetSelected on live entities.");
}
}
