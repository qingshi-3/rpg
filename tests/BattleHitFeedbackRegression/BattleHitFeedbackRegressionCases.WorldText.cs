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
internal static void WorldResourceAndFactionLabelsResolveThroughDefinitions()
{
    StrategicWorldDefinition definition = BuildResourceDisplayNameTestDefinition();
    StrategicWorldDefinitionQueries queries = new(definition);

    AssertEqual("Labor", StrategicWorldDisplayNames.GetResourceLabel(queries, StrategicWorldIds.ResourcePopulation), "resource label should use custom DisplayName");
    AssertEqual("Ash Court", StrategicWorldDisplayNames.GetFactionLabel(queries, StrategicWorldIds.FactionUndead), "faction label should use custom DisplayName");

    definition.ResourceDefinitions.Single(item => item.Id == StrategicWorldIds.ResourceEconomy).DisplayName = "";
    definition.FactionDefinitions.Single(item => item.Id == StrategicWorldIds.FactionUndead).DisplayName = "";
    queries = new StrategicWorldDefinitionQueries(definition);

    AssertEqual(StrategicWorldIds.ResourceEconomy, StrategicWorldDisplayNames.GetResourceLabel(queries, StrategicWorldIds.ResourceEconomy), "blank resource DisplayName should fall back to id");
    AssertEqual(StrategicWorldIds.FactionUndead, StrategicWorldDisplayNames.GetFactionLabel(queries, StrategicWorldIds.FactionUndead), "blank faction DisplayName should fall back to id");
    AssertEqual("无", StrategicWorldDisplayNames.GetResourceLabel(queries, ""), "blank resource id should use explicit fallback");
    AssertEqual("无", StrategicWorldDisplayNames.GetFactionLabel(queries, ""), "blank faction id should use explicit fallback");
}

internal static void WorldSiteAndFacilityLabelsResolveThroughDefinitions()
{
    StrategicWorldDefinition definition = BuildResourceDisplayNameTestDefinition();
    StrategicWorldDefinitionQueries queries = new(definition);

    AssertEqual("Test Quarry", StrategicWorldDisplayNames.GetSiteLabel(queries, StrategicWorldIds.SiteBonefield), "site label should use custom DisplayName");
    AssertEqual("Deep Quarry", StrategicWorldDisplayNames.GetFacilityLabel(queries, StrategicWorldIds.FacilityMine), "facility label should use custom DisplayName");

    definition.SiteDefinitions.Single(item => item.Id == StrategicWorldIds.SiteBonefield).DisplayName = "";
    definition.FacilityDefinitions.Single(item => item.Id == StrategicWorldIds.FacilityMine).DisplayName = "";
    queries = new StrategicWorldDefinitionQueries(definition);

    AssertEqual(StrategicWorldIds.SiteBonefield, StrategicWorldDisplayNames.GetSiteLabel(queries, StrategicWorldIds.SiteBonefield), "blank site DisplayName should fall back to id");
    AssertEqual(StrategicWorldIds.FacilityMine, StrategicWorldDisplayNames.GetFacilityLabel(queries, StrategicWorldIds.FacilityMine), "blank facility DisplayName should fall back to id");
    AssertEqual("missing_site", StrategicWorldDisplayNames.GetSiteLabel(queries, "missing_site"), "missing site definition should fall back to id");
    AssertEqual("missing_facility", StrategicWorldDisplayNames.GetFacilityLabel(queries, "missing_facility"), "missing facility definition should fall back to id");
    AssertEqual("无", StrategicWorldDisplayNames.GetSiteLabel(queries, ""), "blank site id should use default fallback");
    AssertEqual("无", StrategicWorldDisplayNames.GetFacilityLabel(queries, ""), "blank facility id should use default fallback");
    AssertEqual("Fallback Site", StrategicWorldDisplayNames.GetSiteLabel(queries, "missing_site", "Fallback Site"), "missing site should use explicit fallback when provided");
    AssertEqual("Fallback Facility", StrategicWorldDisplayNames.GetFacilityLabel(queries, "", "Fallback Facility"), "blank facility id should use explicit fallback when provided");
}

internal static void WorldActionResourceTextUsesCustomDisplayNames()
{
    StrategicWorldDefinition definition = BuildResourceDisplayNameTestDefinition();
    StrategicWorldState state = BuildResourceDisplayNameTestState();
    state.PlayerResources.Set(StrategicWorldIds.ResourcePopulation, 0);

    WorldActionViewModel action = new WorldActionResolver()
        .GetAvailableActions(state, definition, StrategicWorldIds.SiteBonefield)
        .Single(item => item.ActionId == StrategicWorldIds.ActionBuildMine);

    AssertEqual(false, action.IsEnabled, "build mine should be disabled when custom population resource is missing");
    AssertEqual("Labor不足", action.DisabledReason, "population shortage should use custom resource display name");
    AssertTrue(
        action.EffectLines.Contains("占用Labor 1"),
        "build mine effect text should use custom population display name");
    AssertTrue(
        action.EffectLines.Contains("每世界步Granite +2"),
        "build mine effect text should use custom stone display name");
}

internal static void WorldActionSiteAndFacilityPreviewTextUsesCustomDisplayNames()
{
    StrategicWorldDefinition definition = BuildResourceDisplayNameTestDefinition();
    StrategicWorldState state = BuildResourceDisplayNameTestState();
    state.SiteStates[StrategicWorldIds.SiteBonefield].Facilities.Add(new FacilityInstance
    {
        InstanceId = "tower:test",
        FacilityId = StrategicWorldIds.FacilityDefenseTower,
        SiteId = StrategicWorldIds.SiteBonefield,
        State = FacilityState.Active
    });
    WorldActionResolver resolver = new(unitTypeId =>
        unitTypeId == StrategicWorldIds.UnitMilitia ? "Guard Recruit" : unitTypeId);
    WorldActionViewModel buildMine = resolver
        .GetAvailableActions(state, definition, StrategicWorldIds.SiteBonefield)
        .Single(item => item.ActionId == StrategicWorldIds.ActionBuildMine);
    WorldActionViewModel buildDefenseTower = resolver
        .GetAvailableActions(state, definition, StrategicWorldIds.SiteBonefield)
        .Single(item => item.ActionId == StrategicWorldIds.ActionBuildDefenseTower);
    WorldActionViewModel trainMilitia = resolver
        .GetAvailableActions(state, definition, StrategicWorldIds.SitePlayerCamp)
        .Single(item => item.ActionId == StrategicWorldIds.ActionTrainMilitia);
    AssertTrue(buildMine.EffectLines.Any(line => line.Contains("Test Quarry", StringComparison.Ordinal) && line.Contains("Deep Quarry", StringComparison.Ordinal)), "build mine preview should use custom site and mine names");
    AssertTrue(!buildMine.EffectLines.Any(line => line.Contains("埋骨地", StringComparison.Ordinal) || line.Contains("矿场", StringComparison.Ordinal)), "build mine preview should not hardcode default site or mine names");

    AssertTrue(buildDefenseTower.EffectLines.Any(line => line.Contains("Test Quarry", StringComparison.Ordinal)), "build defense tower preview should use custom site name");
    AssertTrue(buildDefenseTower.EffectLines.Any(line => line.Contains("Signal Spire", StringComparison.Ordinal)), "build defense tower preview should use custom tower name");
    AssertTrue(!buildDefenseTower.EffectLines.Any(line => line.Contains("埋骨地", StringComparison.Ordinal) || line.Contains("防御塔", StringComparison.Ordinal)), "build defense tower preview should not hardcode default site or tower names");

    AssertTrue(trainMilitia.EffectLines.Any(line => line.Contains("Forward Camp", StringComparison.Ordinal)), "train militia preview should use custom player camp name");
    AssertTrue(!trainMilitia.EffectLines.Any(line => line.Contains("玩家营地", StringComparison.Ordinal)), "train militia preview should not hardcode default player camp name");
    AssertTrue(trainMilitia.EffectLines.Any(line => line.Contains("Guard Recruit", StringComparison.Ordinal)), "train militia preview should use injected unit display name");
    AssertTrue(!trainMilitia.EffectLines.Any(line => line.Contains("民兵", StringComparison.Ordinal)), "train militia preview should not hardcode default militia name");
}
}
