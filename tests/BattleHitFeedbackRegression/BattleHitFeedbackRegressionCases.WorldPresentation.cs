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
    site.Garrison.Add(new GarrisonState { UnitTypeId = HeroCorpsV0PlayableSliceIds.HeroUnit, Count = 1 });

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
}
