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

internal static void WorldSiteLabelsResolveThroughDefinitions()
{
    StrategicWorldDefinition definition = BuildResourceDisplayNameTestDefinition();
    StrategicWorldDefinitionQueries queries = new(definition);

    AssertEqual("Test Quarry", StrategicWorldDisplayNames.GetSiteLabel(queries, StrategicWorldIds.SiteBonefield), "site label should use custom DisplayName");

    definition.SiteDefinitions.Single(item => item.Id == StrategicWorldIds.SiteBonefield).DisplayName = "";
    queries = new StrategicWorldDefinitionQueries(definition);

    AssertEqual(StrategicWorldIds.SiteBonefield, StrategicWorldDisplayNames.GetSiteLabel(queries, StrategicWorldIds.SiteBonefield), "blank site DisplayName should fall back to id");
    AssertEqual("missing_site", StrategicWorldDisplayNames.GetSiteLabel(queries, "missing_site"), "missing site definition should fall back to id");
    AssertEqual("无", StrategicWorldDisplayNames.GetSiteLabel(queries, ""), "blank site id should use default fallback");
    AssertEqual("Fallback Site", StrategicWorldDisplayNames.GetSiteLabel(queries, "missing_site", "Fallback Site"), "missing site should use explicit fallback when provided");
}

internal static void WorldActionResourceTextUsesCustomDisplayNames()
{
    StrategicWorldDefinition definition = BuildResourceDisplayNameTestDefinition();
    StrategicWorldState state = BuildResourceDisplayNameTestState();
    state.PlayerResources.Set(StrategicWorldIds.ResourceEconomy, 0);

    WorldActionViewModel action = new WorldActionResolver()
        .GetAvailableActions(state, definition, StrategicWorldIds.SiteBonefield)
        .Single(item => item.ActionId == "test_economy_cost_action");

    AssertEqual(false, action.IsEnabled, "world action should be disabled when custom economy resource is missing");
    AssertEqual("Coin不足", action.DisabledReason, "resource shortage should use custom resource display name");
    AssertTrue(
        action.CostLines.Contains("Coin 5"),
        "action cost text should use custom resource display name");
}

internal static void WorldActionSiteAndUnitPreviewTextUsesCustomDisplayNames()
{
    StrategicWorldDefinition definition = BuildResourceDisplayNameTestDefinition();
    StrategicWorldState state = BuildResourceDisplayNameTestState();
    WorldActionResolver resolver = new(unitTypeId =>
        unitTypeId == StrategicWorldIds.UnitMilitia ? "Guard Recruit" : unitTypeId);
    WorldActionViewModel trainMilitia = resolver
        .GetAvailableActions(state, definition, StrategicWorldIds.SitePlayerCamp)
        .Single(item => item.ActionId == StrategicWorldIds.ActionTrainMilitia);

    AssertTrue(trainMilitia.EffectLines.Any(line => line.Contains("Forward Camp", StringComparison.Ordinal)), "train militia preview should use custom player camp name");
    AssertTrue(!trainMilitia.EffectLines.Any(line => line.Contains("玩家营地", StringComparison.Ordinal)), "train militia preview should not hardcode default player camp name");
    AssertTrue(trainMilitia.EffectLines.Any(line => line.Contains("Guard Recruit", StringComparison.Ordinal)), "train militia preview should use injected unit display name");
    AssertTrue(!trainMilitia.EffectLines.Any(line => line.Contains("民兵", StringComparison.Ordinal)), "train militia preview should not hardcode default militia name");
}
}
