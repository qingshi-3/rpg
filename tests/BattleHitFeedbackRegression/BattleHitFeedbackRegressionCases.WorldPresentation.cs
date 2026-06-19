using Rpg.Presentation.Battle.Actions;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Debug;
using Rpg.Presentation.Battle.Feedback;
using Rpg.Presentation.Battle.Flow;
using Rpg.Presentation.Battle.Preview;
using Rpg.Presentation.Common;
using Rpg.Presentation.World;
using Rpg.Definitions.Battle.Audio;
using Rpg.Application.Battle;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using System.Text.Json;

internal static partial class BattleHitFeedbackRegressionCases
{
internal static void WorldSiteHoverSummaryUsesLocalResourcesAndForceCounts()
{
    StrategicWorldDefinition definition = BuildResourceDisplayNameTestDefinition();
    StrategicWorldDefinitionQueries queries = new(definition);
    StrategicWorldState state = BuildResourceDisplayNameTestState();
    WorldSiteState site = state.SiteStates[StrategicWorldIds.SiteBonefield];
    site.LocalResources.Set(StrategicWorldIds.ResourcePopulation, 5);
    site.LocalResources.Reserve(StrategicWorldIds.ResourcePopulation, 2, "bonefield:test", "test");
    site.LocalResources.Set(StrategicWorldIds.ResourceEconomy, 8);
    site.LocalResources.Set(StrategicWorldIds.ResourceStone, 12);
    site.Garrison.Add(new GarrisonState { UnitTypeId = StrategicWorldIds.UnitMilitia, Count = 4 });
    site.Garrison.Add(new GarrisonState { UnitTypeId = FirstSliceHeroCompanyIds.ShieldHeroUnit, Count = 1 });

    WorldSiteDefinition siteDefinition = queries.GetSite(StrategicWorldIds.SiteBonefield);
    WorldSiteHoverSummaryData summary = WorldSiteHoverSummaryPresenter.Build(queries, siteDefinition, site);

    AssertEqual("Test Quarry", summary.Title, "hover summary title should use configured site display name");
    AssertEqual("Labor 3/5　Coin 8　Granite 12", summary.ResourceText, "hover summary should use local resources and configured resource labels");
    AssertEqual("兵团 4　英雄 1", summary.ForceText, "hover summary should count non-hero troops separately from heroes");
}

internal static void WorldSiteHoverSummaryStaysInsideViewport()
{
    var viewport = new Godot.Vector2(1280f, 720f);
    var panelSize = new Godot.Vector2(190f, 78f);
    var rightEdgeAnchor = new Godot.Rect2(new Godot.Vector2(1240f, 240f), new Godot.Vector2(50f, 70f));

    Godot.Vector2 position = WorldSiteHoverSummaryPresenter.CalculatePanelPosition(
        rightEdgeAnchor,
        panelSize,
        viewport);

    AssertFloatEqual(1078f, position.X, 0.001f, "hover summary should clamp to the right viewport edge");
    AssertFloatEqual(154f, position.Y, 0.001f, "hover summary should prefer above the site visual");

    Godot.Vector2 topPosition = WorldSiteHoverSummaryPresenter.CalculatePanelPosition(
        new Godot.Rect2(new Godot.Vector2(60f, 24f), new Godot.Vector2(80f, 48f)),
        panelSize,
        viewport);

    AssertFloatEqual(80f, topPosition.Y, 0.001f, "hover summary should move below the site when there is no space above");
}

internal static void GameUiSkinInstallsProjectCursorAssets()
{
    string skin = File.ReadAllText(Path.Combine("src", "Presentation", "Common", "GameUiSkin.cs"));
    string strategicRoot = ReadStrategicWorldRootSource();
    string siteRoot = ReadWorldSiteRootSource();
    string cursorDir = Path.Combine("assets", "textures", "ui", "cursors");

    string handCursorPath = Path.Combine(cursorDir, "cursor_hand.png");
    AssertTrue(File.Exists(handCursorPath), "project cursor hand asset should exist.");
    AssertEqual((32, 32), ReadPngSize(handCursorPath), "project cursor hand asset should be compact enough for normal gameplay use");

    string cursorThemeBody = ExtractMethodBlock(skin, "public static void ApplyGameCursorTheme()");
    AssertTrue(
        cursorThemeBody.Contains("ApplyCursorTexture", StringComparison.Ordinal) &&
        skin.Contains("Input.SetCustomMouseCursor", StringComparison.Ordinal) &&
        skin.Contains("ImageTexture.CreateFromImage(image)", StringComparison.Ordinal),
        "GameUiSkin should expose one global project cursor installation entry point.");
    AssertTrue(
        cursorThemeBody.Contains("Texture2D hand = LoadCursorTexture(\"cursor_hand.png\")", StringComparison.Ordinal) &&
        !skin.Contains("cursor_arrow.png", StringComparison.Ordinal) &&
        !skin.Contains("cursor_interact.png", StringComparison.Ordinal) &&
        !skin.Contains("cursor_command.png", StringComparison.Ordinal) &&
        !skin.Contains("cursor_forbidden.png", StringComparison.Ordinal),
        "project cursor theme should use one hand asset instead of separate state assets.");
    AssertTrue(
        cursorThemeBody.Contains("Input.CursorShape.Arrow", StringComparison.Ordinal) &&
        cursorThemeBody.Contains("Input.CursorShape.Cross", StringComparison.Ordinal) &&
        cursorThemeBody.Contains("Input.CursorShape.Drag", StringComparison.Ordinal) &&
        cursorThemeBody.Contains("Input.CursorShape.CanDrop", StringComparison.Ordinal) &&
        cursorThemeBody.Contains("Input.CursorShape.PointingHand", StringComparison.Ordinal) &&
        cursorThemeBody.Contains("Input.CursorShape.Forbidden", StringComparison.Ordinal),
        "project cursor theme should map common pointer states to the shared hand asset.");
    AssertTrue(
        strategicRoot.Contains("GameUiSkin.ApplyGameCursorTheme();", StringComparison.Ordinal),
        "strategic world entry should install project cursors on scene entry.");
    AssertTrue(
        siteRoot.Contains("GameUiSkin.ApplyGameCursorTheme();", StringComparison.Ordinal),
        "world site entry should reinstall project cursors after scene transitions.");
}

private static (int Width, int Height) ReadPngSize(string path)
{
    byte[] bytes = File.ReadAllBytes(path);
    if (bytes.Length < 24 ||
        bytes[0] != 0x89 ||
        bytes[1] != 0x50 ||
        bytes[2] != 0x4e ||
        bytes[3] != 0x47)
    {
        throw new InvalidOperationException($"not a png file: {path}");
    }

    int width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
    int height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
    return (width, height);
}

internal static void StrategicWorldForwardsMiddleMouseCameraNavigation()
{
    string strategicRoot = ReadStrategicWorldRootSource();
    string cameraController = File.ReadAllText(Path.Combine("src", "Presentation", "Common", "MapCameraController.cs"));

    AssertTrue(
        cameraController.Contains("public bool TryHandlePointerNavigationAndZoomInput(InputEvent @event)", StringComparison.Ordinal) &&
        cameraController.Contains("HandleMouseWheelInput(mouseButton)", StringComparison.Ordinal),
        "MapCameraController should expose one public pointer entry point for middle-drag panning and mouse-wheel zoom.");
    AssertTrue(
        strategicRoot.Contains("TryHandleWorldCameraPointerInput(@event)", StringComparison.Ordinal),
        "strategic world root should forward pointer camera navigation before world army input");
    AssertTrue(
        strategicRoot.Contains("_worldCamera.TryHandlePointerNavigationAndZoomInput(@event)", StringComparison.Ordinal),
        "strategic world root should delegate middle mouse navigation and wheel zoom to MapCameraController");
}

internal static void StrategicWorldCameraInputStaysInsideMapViewport()
{
    string strategicRoot = ReadStrategicWorldRootSource();
    string cameraController = File.ReadAllText(Path.Combine("src", "Presentation", "Common", "MapCameraController.cs"));
    string rootGuiBody = ExtractMethodBlock(strategicRoot, "public override void _GuiInput(InputEvent @event)");
    string processBody = ExtractMethodBlock(cameraController, "public override void _Process(double delta)");
    string inputBody = ExtractMethodBlock(cameraController, "public override void _Input(InputEvent @event)");
    string unhandledInputBody = ExtractMethodBlock(cameraController, "public override void _UnhandledInput(InputEvent @event)");

    AssertTrue(
        rootGuiBody.Contains("IsRootScreenMapInput(@event)", StringComparison.Ordinal) &&
        rootGuiBody.IndexOf("IsRootScreenMapInput(@event)", StringComparison.Ordinal) <
        rootGuiBody.IndexOf("TryHandleWorldCameraPointerInput(@event)", StringComparison.Ordinal),
        "strategic root-level pointer input should be gated to the map viewport before reaching camera navigation");
    AssertTrue(
        strategicRoot.Contains("private bool IsRootScreenMapInput(InputEvent @event)", StringComparison.Ordinal) &&
        strategicRoot.Contains("ResolveMainWorldViewportRect().HasPoint(", StringComparison.Ordinal),
        "strategic root-level input gate should use the resolved map viewport rect so HUD panels cannot leak scroll or drag events to the map camera");
    AssertTrue(
        strategicRoot.Contains("_isArmyBoxSelecting", StringComparison.Ordinal) &&
        strategicRoot.Contains("_worldCamera?.IsPointerNavigationActive == true", StringComparison.Ordinal) &&
        cameraController.Contains("public bool IsPointerNavigationActive => _isMiddleMouseDragging", StringComparison.Ordinal),
        "active map-origin gestures should keep receiving motion/release events even when the pointer leaves the viewport");

    foreach ((string body, string name) in new[]
    {
        (processBody, "_Process"),
        (inputBody, "_Input"),
        (unhandledInputBody, "_UnhandledInput")
    })
    {
        AssertTrue(
            body.Contains("CanProcessViewportCameraInput()", StringComparison.Ordinal),
            $"MapCameraController {name} should ignore global engine input when the controller is used as a non-viewport camera state object");
    }
}

internal static void StrategicWorldResetsCameraNavigationInputOnSceneEntry()
{
    string strategicRoot = ReadStrategicWorldRootSource();
    string readyBody = ExtractMethodBlock(strategicRoot, "public override void _Ready()");
    string cameraController = File.ReadAllText(Path.Combine("src", "Presentation", "Common", "MapCameraController.cs"));
    string projectConfig = File.ReadAllText("project.godot");
    string resetBody = ExtractMethodBlock(cameraController, "public void ResetNavigationInputState");
    string exitTreeBody = ExtractMethodBlock(cameraController, "public override void _ExitTree()");
    string moveDirectionBody = ExtractMethodBlock(cameraController, "private Vector2 GetMoveDirection()");
    string suppressionBody = ExtractMethodBlock(cameraController, "private bool ShouldSuppressPolledKeyboard()");

    AssertTrue(
        readyBody.Contains("_worldCamera.ResetNavigationInputState(\"strategic_world_ready\")", StringComparison.Ordinal) ||
        readyBody.Contains("_worldCamera?.ResetNavigationInputState(\"strategic_world_ready\")", StringComparison.Ordinal),
        "strategic world entry should clear camera navigation state before the first world-camera update");
    AssertTrue(
        resetBody.Contains("_moveUpPressed = false", StringComparison.Ordinal) &&
        resetBody.Contains("_moveDownPressed = false", StringComparison.Ordinal) &&
        resetBody.Contains("_moveLeftPressed = false", StringComparison.Ordinal) &&
        resetBody.Contains("_moveRightPressed = false", StringComparison.Ordinal) &&
        resetBody.Contains("_isMiddleMouseDragging = false", StringComparison.Ordinal),
        "map camera reset should clear event-backed keyboard and middle-drag state");
    AssertTrue(
        resetBody.Contains("_suppressPolledKeyboardUntilRelease = true", StringComparison.Ordinal) &&
        moveDirectionBody.Contains("ShouldSuppressPolledKeyboard()", StringComparison.Ordinal) &&
        moveDirectionBody.Contains("Input.GetVector(CameraMoveLeftAction, CameraMoveRightAction, CameraMoveUpAction, CameraMoveDownAction)", StringComparison.Ordinal) &&
        suppressionBody.Contains("_suppressPolledKeyboardUntilRelease", StringComparison.Ordinal) &&
        suppressionBody.Contains("AnyPolledMoveActionPressed()", StringComparison.Ordinal) &&
        !cameraController.Contains("Input.IsKeyPressed(Key.W)", StringComparison.Ordinal) &&
        !cameraController.Contains("Input.IsKeyPressed(Key.A)", StringComparison.Ordinal) &&
        !cameraController.Contains("Input.IsKeyPressed(Key.S)", StringComparison.Ordinal) &&
        !cameraController.Contains("Input.IsKeyPressed(Key.D)", StringComparison.Ordinal),
        "map camera reset should ignore already-held or stale polled movement actions until they are released");
    AssertTrue(
        projectConfig.Contains("camera_move_left", StringComparison.Ordinal) &&
        projectConfig.Contains("camera_move_right", StringComparison.Ordinal) &&
        projectConfig.Contains("camera_move_up", StringComparison.Ordinal) &&
        projectConfig.Contains("camera_move_down", StringComparison.Ordinal) &&
        projectConfig.Contains("\"physical_keycode\":65", StringComparison.Ordinal) &&
        projectConfig.Contains("\"physical_keycode\":68", StringComparison.Ordinal) &&
        projectConfig.Contains("\"physical_keycode\":87", StringComparison.Ordinal) &&
        projectConfig.Contains("\"physical_keycode\":83", StringComparison.Ordinal),
        "project Input Map should define camera movement actions bound to A/D/W/S by default");
    AssertTrue(
        exitTreeBody.Contains("ResetNavigationInputState(\"exit_tree\")", StringComparison.Ordinal),
        "map camera should clear transient navigation state when leaving the scene tree");
}

internal static void MapCameraIgnoresStaleMovePressEventsAfterSceneReset()
{
    string cameraController = File.ReadAllText(Path.Combine("src", "Presentation", "Common", "MapCameraController.cs"));
    string inputBody = ExtractMethodBlock(cameraController, "public override void _Input(InputEvent @event)");
    string stalePressGuardBody = ExtractMethodBlock(cameraController, "private bool ShouldIgnoreSuppressedMoveActionEvent");

    AssertTrue(
        inputBody.Contains("ShouldIgnoreSuppressedMoveActionEvent(@event)", StringComparison.Ordinal),
        "map camera input should reject queued move-action press events while scene-entry suppression is active");
    AssertTrue(
        stalePressGuardBody.Contains("_suppressPolledKeyboardUntilRelease", StringComparison.Ordinal) &&
        stalePressGuardBody.Contains("IsMoveActionPressed(@event", StringComparison.Ordinal) &&
        stalePressGuardBody.Contains("IsAnyMoveActionEvent(@event)", StringComparison.Ordinal),
        "stale move-action guard should only suppress already-held movement press events after a reset");
    AssertTrue(
        stalePressGuardBody.Contains("_suppressPolledKeyboardUntilRelease = false", StringComparison.Ordinal) &&
        stalePressGuardBody.Contains("!AnyPolledMoveActionPressed()", StringComparison.Ordinal),
        "move-action suppression should end after all movement actions are released so normal camera control still works");
}
}
