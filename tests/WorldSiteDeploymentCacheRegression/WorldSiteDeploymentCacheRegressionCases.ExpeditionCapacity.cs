using Godot;
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
    string adjustBody = ExtractMethodBody(rootSource, "private void AdjustExpeditionUnitCount(");
    string attachBody = ExtractMethodBody(rootSource, "private static void AttachDefaultCorpsToHeroExpedition(");

    AssertTrue(
        !adjustBody.Contains("_expeditionUnitCounts.Remove(otherHeroUnitId)", StringComparison.Ordinal),
        "selecting one hero company in the expedition draft should not clear other selected hero companies");
    AssertTrue(
        !rootSource.Contains("BuildSelectedExpeditionUnitBatches", StringComparison.Ordinal),
        "a multi-company draft should create one strategic expedition army instead of splitting into one-company armies");
    AssertTrue(
        rootSource.Contains("Dictionary<string, int> selectedUnits = BuildSelectedExpeditionUnits();", StringComparison.Ordinal),
        "expedition creation should pass the whole selected-company unit set into one strategic army");
    AssertTrue(
        rootSource.Contains("HasAvailablePlayerExpeditionCapacity(State, out _, out _)", StringComparison.Ordinal) &&
        !rootSource.Contains("HasAvailablePlayerExpeditionCapacity(State, expeditionUnitBatches.Count", StringComparison.Ordinal),
        "target confirmation should reserve one expedition slot for the carried multi-company army");
    AssertTrue(
        attachBody.Contains("foreach", StringComparison.Ordinal) &&
        attachBody.Contains("TryGetCompanyByHeroUnit", StringComparison.Ordinal) &&
        !attachBody.Contains("GarrisonState hero = army.GarrisonUnits.FirstOrDefault", StringComparison.Ordinal),
        "default corps attachment should loop over every selected hero company in the army");
    AssertTrue(
        rootSource.Contains("BuildSelectedDefaultCorpsText", StringComparison.Ordinal),
        "the expedition panel should summarize default corps for every selected hero company");
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
