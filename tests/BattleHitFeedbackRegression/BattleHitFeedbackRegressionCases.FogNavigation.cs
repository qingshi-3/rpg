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
internal static void StrategicFogStampsPixelCircleIndependentOfTileCells()
{
    StrategicFogOfWarSettings settings = new()
    {
        FogTexelWorldSize = 10f,
        SiteVisionRadius = 20f,
        ArmyVisionRadius = 20f
    };

    HashSet<string> visible = StrategicFogOfWarService.BuildVisibleCellKeys(
        new[] { new StrategicFogVisionSource(new Godot.Vector2(0f, 0f), 20f) },
        settings);

    AssertTrue(visible.Contains("0:0"), "fog circle should include the source cell");
    AssertTrue(visible.Contains("1:1"), "fog circle should include diagonal cells inside the pixel radius");
    AssertTrue(visible.Contains("-1:0"), "fog circle should include negative x cells around the source");
    AssertTrue(!visible.Contains("2:2"), "fog circle should exclude diagonal cells outside the pixel radius");
}

internal static void StrategicFogDefaultTexelStaysBelowTileSizedChunks()
{
    AssertFloatEqual(16f, StrategicFogOfWarService.DefaultFogTexelWorldSize, 0.001f, "default fog texel should be fine enough to avoid cell-sized chunky edges");
    StrategicFogOfWarSettings settings = new();

    AssertFloatEqual(16f, settings.FogTexelWorldSize, 0.001f, "new fog settings should use the shared default texel size");
}

internal static void StrategicFogPersistsExploredCellsWhileVisibleIsDerived()
{
    StrategicWorldDefinition definition = BuildResourceDisplayNameTestDefinition();
    StrategicWorldState state = BuildResourceDisplayNameTestState();
    state.PlayerFactionId = StrategicWorldIds.FactionPlayer;
    definition.SiteDefinitions.Single(site => site.Id == StrategicWorldIds.SitePlayerCamp).MapPosition = new Godot.Vector2(0f, 0f);
    definition.SiteDefinitions.Single(site => site.Id == StrategicWorldIds.SiteBonefield).MapPosition = new Godot.Vector2(12f, 0f);
    definition.SiteDefinitions.Single(site => site.Id == StrategicWorldIds.SiteGraveyard).MapPosition = new Godot.Vector2(80f, 0f);
    state.SiteStates[StrategicWorldIds.SitePlayerCamp].OwnerFactionId = StrategicWorldIds.FactionPlayer;
    state.SiteStates[StrategicWorldIds.SiteBonefield].OwnerFactionId = StrategicWorldIds.FactionUndead;

    StrategicFogOfWarSettings settings = new()
    {
        FogTexelWorldSize = 10f,
        SiteVisionRadius = 15f,
        ArmyVisionRadius = 15f
    };

    StrategicFogOfWarService.RefreshVisibility(state, definition, settings);

    AssertEqual(WorldIntelVisibility.Visible, StrategicFogOfWarService.GetSiteVisibility(state.Intel, definition.SiteDefinitions.Single(site => site.Id == StrategicWorldIds.SiteBonefield), settings), "nearby site should be visible");
    AssertEqual(WorldIntelVisibility.Unknown, StrategicFogOfWarService.GetSiteVisibility(state.Intel, definition.SiteDefinitions.Single(site => site.Id == StrategicWorldIds.SiteGraveyard), settings), "far site should remain unknown");
    AssertTrue(state.Intel.ExploredCells.Contains("1:0"), "visible cells should be merged into explored cells");

    state.SiteStates[StrategicWorldIds.SitePlayerCamp].OwnerFactionId = StrategicWorldIds.FactionUndead;
    StrategicFogOfWarService.RefreshVisibility(state, definition, settings);

    AssertTrue(state.Intel.VisibleCells.Count == 0, "visible cells should be derived fresh each refresh");
    AssertTrue(state.Intel.ExploredCells.Contains("1:0"), "explored cells should persist after vision source is gone");
}

internal static void StrategicFogKeepsStaleSiteIntelAfterLeavingVision()
{
    StrategicWorldDefinition definition = BuildResourceDisplayNameTestDefinition();
    StrategicWorldState state = BuildResourceDisplayNameTestState();
    state.PlayerFactionId = StrategicWorldIds.FactionPlayer;
    WorldSiteDefinition camp = definition.SiteDefinitions.Single(site => site.Id == StrategicWorldIds.SitePlayerCamp);
    WorldSiteDefinition target = definition.SiteDefinitions.Single(site => site.Id == StrategicWorldIds.SiteBonefield);
    camp.MapPosition = new Godot.Vector2(0f, 0f);
    target.MapPosition = new Godot.Vector2(12f, 0f);
    state.SiteStates[StrategicWorldIds.SitePlayerCamp].OwnerFactionId = StrategicWorldIds.FactionPlayer;
    WorldSiteState targetState = state.SiteStates[StrategicWorldIds.SiteBonefield];
    targetState.OwnerFactionId = StrategicWorldIds.FactionPlayer;
    targetState.LocalResources.Set(StrategicWorldIds.ResourceStone, 7);

    StrategicFogOfWarSettings settings = new()
    {
        FogTexelWorldSize = 10f,
        SiteVisionRadius = 15f,
        ArmyVisionRadius = 15f
    };

    state.WorldTick = 3;
    StrategicFogOfWarService.RefreshVisibility(state, definition, settings);
    targetState.LocalResources.Set(StrategicWorldIds.ResourceStone, 12);
    state.SiteStates[StrategicWorldIds.SitePlayerCamp].OwnerFactionId = StrategicWorldIds.FactionUndead;
    targetState.OwnerFactionId = StrategicWorldIds.FactionUndead;
    state.WorldTick = 4;
    StrategicFogOfWarService.RefreshVisibility(state, definition, settings);

    AssertEqual(WorldIntelVisibility.Revealed, StrategicFogOfWarService.GetSiteVisibility(state.Intel, target, settings), "known site should become revealed stale intel after leaving vision");
    AssertEqual(3, state.Intel.KnownSites[StrategicWorldIds.SiteBonefield].LastSeenWorldTick, "stale site intel should preserve last visible tick");
    AssertEqual(7, state.Intel.KnownSites[StrategicWorldIds.SiteBonefield].KnownLocalResources.GetAmount(StrategicWorldIds.ResourceStone), "stale site intel should not refresh while outside vision");
}

internal static void StrategicNavigationTargetLookupIgnoresFogVisibility()
{
    string strategicRoot = ReadStrategicWorldRootSource();
    string findSiteAtBody = ExtractMethodBlock(strategicRoot, "private WorldSiteDefinition FindSiteAt");
    AssertTrue(
        !findSiteAtBody.Contains("GetSiteIntelVisibility", StringComparison.Ordinal),
        "site target lookup is used by navigation commands and must not depend on fog visibility");
}

internal static void StrategicNavigationCommandFlowStaysIndependentFromFog()
{
    string strategicRoot = ReadStrategicWorldRootSource();
    foreach (string methodSignature in new[]
             {
                 "private bool TryCommandSelectedArmies",
                 "private bool TryCommandSelectedArmiesToSite",
                 "private bool TryIssueExpeditionToTarget",
                 "private bool TryIssueExpeditionToSite",
                 "private bool TryCreateExpedition",
                 "private bool TryResolveExpeditionNavigation",
                 "private bool TryBuildCommandPaths"
             })
    {
        string methodBody = ExtractMethodBlock(strategicRoot, methodSignature);
        AssertTrue(!methodBody.Contains("GetSiteIntelVisibility", StringComparison.Ordinal), $"{methodSignature} must not read site fog visibility");
        AssertTrue(!methodBody.Contains("IsMapPositionVisible", StringComparison.Ordinal), $"{methodSignature} must not read map fog visibility");
        AssertTrue(!methodBody.Contains("IsScreenPositionVisible", StringComparison.Ordinal), $"{methodSignature} must not read screen fog visibility");
    }
}

internal static void StrategicNavigationLayerIsIsolatedFromCameraTransform()
{
    string strategicRoot = ReadStrategicWorldRootSource();
    string navigationContext = File.ReadAllText(Path.Combine("src", "Application", "World", "StrategicNavigationContext.cs"));
    AssertTrue(
        strategicRoot.Contains("EnsureStrategicNavigationLayerIsStable", StringComparison.Ordinal),
        "strategic root should move navigation data under a stable root before camera transforms WorldMapRoot");
    AssertTrue(
        strategicRoot.Contains("_strategicNavigationRoot", StringComparison.Ordinal),
        "navigation context should use a root that is not panned or scaled as the visual map camera");

    string updateCameraBody = ExtractMethodBlock(strategicRoot, "private bool UpdateWorldCameraView");
    AssertTrue(
        !updateCameraBody.Contains("_strategicNavigationRoot.Global", StringComparison.Ordinal),
        "camera view updates must not transform the stable navigation root");
    AssertTrue(
        !navigationContext.Contains("NavigationServer2D", StringComparison.Ordinal),
        "strategic map navigation should not depend on Godot NavigationServer2D synchronization");
    AssertTrue(
        navigationContext.Contains("StrategicNavigationGrid", StringComparison.Ordinal),
        "strategic map navigation should use the project-owned grid provider");
}

internal static void StrategicFogOverlayUsesCircularVisibilityMask()
{
    string overlay = File.ReadAllText(Path.Combine("src", "Presentation", "World", "StrategicWorldFogOverlay.cs"));
    string strategicRoot = ReadStrategicWorldRootSource();
    string shader = File.ReadAllText(Path.Combine("assets", "world", "shaders", "strategic_fog_of_war.gdshader"));
    string refreshFogBody = ExtractMethodBlock(strategicRoot, "private void RefreshStrategicFogOverlay");
    AssertTrue(overlay.Contains("StrategicWorldFogOverlayCircle", StringComparison.Ordinal), "fog overlay should receive circular visible masks");
    AssertTrue(overlay.Contains("ShaderMaterial", StringComparison.Ordinal), "fog overlay should use a shader material for smooth fog movement");
    AssertTrue(shader.Contains("distance(sample_pixel, circle.xy)", StringComparison.Ordinal), "fog shader should draw circular visibility by pixel distance");
    AssertTrue(!shader.Contains("step(0.5, explored)", StringComparison.Ordinal), "explored fog should not use a hard cell-mask threshold");
    AssertTrue(shader.Contains("explored_amount", StringComparison.Ordinal), "explored fog should blend through a soft mask amount");
    AssertTrue(!shader.Contains("return;", StringComparison.Ordinal), "Godot canvas fragment shaders must not use return statements");
    AssertTrue(overlay.Contains("Visible = false", StringComparison.Ordinal), "fog overlay should stay hidden if the shader cannot be applied");
    AssertTrue(!overlay.Contains("DrawRect(cell.ScreenRect", StringComparison.Ordinal), "fog overlay should not render the full fog edge as raw cell rectangles");
    AssertTrue(overlay.Contains("FillMaskSoftCircle", StringComparison.Ordinal), "explored fog mask should stamp soft circular cells instead of hard rectangles");
    AssertTrue(!refreshFogBody.Contains("visible.Contains(cellKey)", StringComparison.Ordinal), "explored fog mask should keep current visible cells so circular edge feather does not expose unknown-color holes");
}
}
