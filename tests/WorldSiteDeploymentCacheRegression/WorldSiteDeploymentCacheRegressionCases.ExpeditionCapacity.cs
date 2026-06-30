using Godot;
using Rpg.Application.StrategicManagement;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.World;

internal static partial class WorldSiteDeploymentCacheRegressionCases
{
internal static void FirstSliceExpeditionCapacityAllowsMultipleSeparateArmies()
{
    StrategicWorldDefinition definition = StrategicWorldV1DefinitionFactory.Create(loadInitialStateConfig: false);
    StrategicWorldState state = new StrategicWorldService().CreateInitialState(definition);
    WorldSiteState playerSite = state.SiteStates[StrategicWorldIds.SitePlayerCamp];
    playerSite.OwnerFactionId = StrategicWorldIds.FactionPlayer;
    playerSite.ControlState = SiteControlState.PlayerHeld;
    playerSite.Garrison.Clear();
    playerSite.Garrison.Add(new GarrisonState { UnitTypeId = "f1_grandmasterzir", Count = 2, Morale = 80 });
    playerSite.Garrison.Add(new GarrisonState { UnitTypeId = "f1_windbladecommander", Count = 1, Morale = 80 });
    playerSite.Garrison.Add(new GarrisonState { UnitTypeId = "f1_elyxstormblade", Count = 1, Morale = 80 });

    WorldExpeditionService service = new();
    AssertEqual(3, WorldExpeditionService.FirstSliceMaxActivePlayerExpeditions, "first slice hardcoded expedition queue should cover the three selectable hero companies");
    AssertTrue(
        service.HasAvailablePlayerExpeditionCapacity(state, out int initialActiveCount, out int maxActiveExpeditions) &&
        initialActiveCount == 0 &&
        maxActiveExpeditions == 3,
        "empty strategic world should have all first-slice expedition slots available");

    AssertTrue(CreateExpedition(service, state, definition, "f1_grandmasterzir", out _), "first expedition should be created");
    AssertTrue(CreateExpedition(service, state, definition, "f1_windbladecommander", out _), "second expedition should be created");
    AssertTrue(CreateExpedition(service, state, definition, "f1_elyxstormblade", out _), "third expedition should be created");
    AssertEqual(3, WorldExpeditionService.CountActivePlayerExpeditions(state), "three created player armies should consume all first-slice expedition slots");

    bool fourthCreated = CreateExpedition(service, state, definition, "f1_grandmasterzir", out string fourthFailureReason);
    AssertTrue(!fourthCreated, "fourth concurrent expedition should be rejected while the queue is full");
    AssertEqual("expedition_capacity_full", fourthFailureReason, "capacity rejection should use an explicit failure reason");

    WorldArmyState resolvedArmy = state.ArmyStates.Values.First();
    resolvedArmy.Status = WorldArmyStatus.Garrisoned;
    AssertTrue(
        service.HasAvailablePlayerExpeditionCapacity(state, out int activeAfterResolved, out _) &&
        activeAfterResolved == 2,
        "garrisoned player armies should no longer consume active expedition capacity");
    AssertTrue(CreateExpedition(service, state, definition, "f1_grandmasterzir", out _), "resolved expedition slot should be reusable");
}

internal static void FirstSliceExpeditionDraftKeepsHeroCompanySelectionsIndependent()
{
    string rootSource = ReadStrategicWorldRootSource();
    string beginBody = ExtractMethodBody(rootSource, "private void BeginExpeditionDraft()");
    string controlsBody = ExtractMethodBody(rootSource, "private bool RefreshExpeditionControls()");
    string createBody = ExtractMethodBody(rootSource, "private bool TryCreateExpedition(string targetSiteId, Vector2 destination, WorldArmyIntent intent)");
    string availableBody = ExtractMethodBody(rootSource, "private IReadOnlyList<StrategicHeroCompanyViewModel> GetAvailableExpeditionHeroCompanies(");

    AssertTrue(
        beginBody.Contains("_expeditionHeroIds.Clear();", StringComparison.Ordinal) &&
        controlsBody.Contains("GetAvailableExpeditionHeroCompanies(_expeditionSourceSiteId)", StringComparison.Ordinal),
        "expedition draft should start from an empty selection and read dispatchable hero companies through the Strategic Management helper when binding the draft UI");
    AssertTrue(
        availableBody.Contains("StrategicManagementRuntime.LocationMappings.TryResolveCityIdForMapSite(", StringComparison.Ordinal) &&
        availableBody.Contains("StrategicManagementRuntime.BuildDashboard(", StringComparison.Ordinal) &&
        availableBody.Contains("dashboard.SelectedCity.HeroCompanies", StringComparison.Ordinal) &&
        !availableBody.Contains("site.Garrison", StringComparison.Ordinal),
        "available expedition companies should resolve the selected strategic city and must not read legacy garrison");
    AssertTrue(
        createBody.Contains("StrategicManagementRuntime.Commands.CreateExpedition(", StringComparison.Ordinal) &&
        createBody.Contains("_strategicExpeditionWorldArmyAdapter.CreateWorldArmy(", StringComparison.Ordinal),
        "expedition creation should mutate Strategic Management first and then create a movement adapter army");
    AssertTrue(
        rootSource.Contains("HashSet<string> _expeditionHeroIds", StringComparison.Ordinal) &&
        !rootSource.Contains("Dictionary<string, int> _expeditionUnitCounts", StringComparison.Ordinal),
        "expedition draft selection should store selected strategic hero ids instead of old unit counts");
    AssertTrue(
        !rootSource.Contains("_expeditionService.TryCreateExpedition(", StringComparison.Ordinal) &&
        !rootSource.Contains("AttachDefaultCorpsToHeroExpedition", StringComparison.Ordinal),
        "large-map expedition formation must not use legacy garrison expedition creation or default-corps injection");
}

internal static void StrategicWorldExpeditionDraftStartsEmptyAndAllowsDeselect()
{
    string rootSource = ReadStrategicWorldRootSource();
    string expeditionHudSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "World", "StrategicWorldRoot.ExpeditionHud.cs"));
    string beginBody = ExtractMethodBody(rootSource, "private void BeginExpeditionDraft()");
    string adjustBody = ExtractMethodBody(rootSource, "private void AdjustExpeditionHeroCompanySelection(string heroId, int delta)");
    string controlsBody = ExtractMethodBody(expeditionHudSource, "private bool RefreshExpeditionControls()");

    AssertTrue(
        beginBody.Contains("_expeditionHeroIds.Clear();", StringComparison.Ordinal) &&
        !beginBody.Contains("_expeditionHeroIds.Add(", StringComparison.Ordinal) &&
        !beginBody.Contains("FirstOrDefault(company => company.CanCreateExpedition)", StringComparison.Ordinal),
        "starting an expedition draft should leave the hero-company selection empty until the player explicitly chooses a company");
    AssertTrue(
        adjustBody.Contains("else if (delta < 0)", StringComparison.Ordinal) &&
        adjustBody.Contains("_expeditionHeroIds.Remove(heroId);", StringComparison.Ordinal),
        "clicking the minus button on a selected expedition company should be able to remove that company from the draft");
    AssertTrue(
        controlsBody.Contains("AddExpeditionTargetButton(HasSelectedExpeditionUnits())", StringComparison.Ordinal),
        "the choose-target button should stay disabled while the expedition draft has no selected hero company");
}

internal static void StrategicWorldSelectionContextClearsExpeditionDraft()
{
    string rootSource = ReadStrategicWorldRootSource();
    string selectSiteBody = ExtractMethodBody(rootSource, "private void SelectSite(string siteId)");
    string clearDetailBody = ExtractMethodBody(rootSource, "private void ClearSelectedWorldDetail(");
    string cancelDraftBody = ExtractMethodBody(rootSource, "private void CancelExpeditionDraft()");

    AssertTrue(
        rootSource.Contains("private void ClearExpeditionDraftSelectionContext(", StringComparison.Ordinal),
        "strategic world should centralize transient expedition draft cleanup for ordinary map selection changes");

    string cleanupBody = ExtractMethodBody(rootSource, "private void ClearExpeditionDraftSelectionContext(");
    foreach (string required in new[]
    {
        "_isExpeditionDrafting = false",
        "_isExpeditionTargeting = false",
        "_expeditionSourceSiteId = \"\"",
        "_expeditionHeroIds.Clear()"
    })
    {
        AssertTrue(
            cleanupBody.Contains(required, StringComparison.Ordinal),
            $"expedition draft cleanup should clear transient UI state fragment={required}");
    }

    AssertTrue(
        selectSiteBody.Contains("ClearExpeditionDraftSelectionContext(", StringComparison.Ordinal),
        "clicking a site during a draft should return to ordinary site selection instead of keeping the draft UI active");
    AssertTrue(
        clearDetailBody.Contains("ClearExpeditionDraftSelectionContext(", StringComparison.Ordinal),
        "clicking empty map space or selecting an army should clear the draft before hiding the detail panel");
    AssertTrue(
        cancelDraftBody.Contains("ClearExpeditionDraftSelectionContext(", StringComparison.Ordinal),
        "the explicit cancel button should use the same expedition draft cleanup path");
}

internal static void StrategicWorldSiteSelectionClearsArmySelection()
{
    string rootSource = ReadStrategicWorldRootSource();
    string selectSiteBody = ExtractMethodBody(rootSource, "private void SelectSite(string siteId)");
    string opportunityBody = ExtractMethodBody(rootSource, "private void SelectOpportunity(string opportunityId)");

    AssertTrue(
        selectSiteBody.Contains("_selectedArmyIds.Clear();", StringComparison.Ordinal),
        "clicking a city/site on the large map should clear selected world armies so army highlight state does not remain active behind the site panel");
    AssertTrue(
        opportunityBody.Contains("_selectedArmyIds.Clear();", StringComparison.Ordinal),
        "opportunity selection should keep clearing selected world armies as the existing selection-state pattern");
}

private static bool CreateExpedition(
    WorldExpeditionService service,
    StrategicWorldState state,
    StrategicWorldDefinition definition,
    string heroUnitId,
    out string failureReason)
{
    return service.TryCreateExpedition(
        state,
        definition,
        StrategicWorldIds.SitePlayerCamp,
        new Vector2(0, 0),
        "",
        new Vector2(100, 0),
        WorldArmyIntent.MoveToPosition,
        new Dictionary<string, int> { [heroUnitId] = 1 },
        out _,
        out failureReason);
}
}
