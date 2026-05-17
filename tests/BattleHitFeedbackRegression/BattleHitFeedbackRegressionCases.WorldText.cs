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
    state.ThreatPlans["threat:preview"] = new EnemyThreatPlan
    {
        Id = "threat:preview",
        SourceSiteId = StrategicWorldIds.SiteGraveyard,
        TargetSiteId = StrategicWorldIds.SiteBonefield,
        Stage = ThreatStage.Attacking
    };

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
    WorldActionViewModel autoResolveRaid = resolver
        .GetAvailableActions(state, definition, "", "threat:preview")
        .Single(item => item.ActionId == StrategicWorldIds.ActionAutoResolveRaid);

    AssertTrue(buildMine.EffectLines.Any(line => line.Contains("Test Quarry", StringComparison.Ordinal) && line.Contains("Deep Quarry", StringComparison.Ordinal)), "build mine preview should use custom site and mine names");
    AssertTrue(!buildMine.EffectLines.Any(line => line.Contains("埋骨地", StringComparison.Ordinal) || line.Contains("矿场", StringComparison.Ordinal)), "build mine preview should not hardcode default site or mine names");

    AssertTrue(buildDefenseTower.EffectLines.Any(line => line.Contains("Test Quarry", StringComparison.Ordinal)), "build defense tower preview should use custom site name");
    AssertTrue(buildDefenseTower.EffectLines.Any(line => line.Contains("Signal Spire", StringComparison.Ordinal)), "build defense tower preview should use custom tower name");
    AssertTrue(!buildDefenseTower.EffectLines.Any(line => line.Contains("埋骨地", StringComparison.Ordinal) || line.Contains("防御塔", StringComparison.Ordinal)), "build defense tower preview should not hardcode default site or tower names");

    AssertTrue(trainMilitia.EffectLines.Any(line => line.Contains("Forward Camp", StringComparison.Ordinal)), "train militia preview should use custom player camp name");
    AssertTrue(!trainMilitia.EffectLines.Any(line => line.Contains("玩家营地", StringComparison.Ordinal)), "train militia preview should not hardcode default player camp name");
    AssertTrue(trainMilitia.EffectLines.Any(line => line.Contains("Guard Recruit", StringComparison.Ordinal)), "train militia preview should use injected unit display name");
    AssertTrue(!trainMilitia.EffectLines.Any(line => line.Contains("民兵", StringComparison.Ordinal)), "train militia preview should not hardcode default militia name");

    AssertTrue(autoResolveRaid.EffectLines.Any(line => line.Contains("Signal Spire", StringComparison.Ordinal)), "auto resolve preview should use custom tower name");
    AssertTrue(autoResolveRaid.WarningLines.Any(line => line.Contains("Signal Spire", StringComparison.Ordinal)), "auto resolve warning should use custom tower name");
    AssertTrue(!autoResolveRaid.EffectLines.Concat(autoResolveRaid.WarningLines).Any(line => line.Contains("防御塔", StringComparison.Ordinal)), "auto resolve text should not hardcode default tower name");
    AssertTrue(autoResolveRaid.WarningLines.Any(line => line.Contains("Guard Recruit", StringComparison.Ordinal)), "auto resolve warning should use injected militia display name");
    AssertTrue(!autoResolveRaid.WarningLines.Any(line => line.Contains("民兵", StringComparison.Ordinal)), "auto resolve warning should not hardcode default militia name");
}

internal static void WorldThreatAutoResolveMessagesUseConfiguredDisplayNames()
{
    StrategicWorldDefinition definition = BuildResourceDisplayNameTestDefinition();
    WorldThreatService service = new(unitTypeId =>
        unitTypeId == StrategicWorldIds.UnitMilitia ? "Guard Recruit" : unitTypeId);

    WorldActionResult strongDefense = service.ResolveRaidAutomatically(
        BuildThreatAutoResolveState(militia: 4, towers: 0),
        definition,
        "threat:auto");
    AssertTrue(strongDefense.Message.Contains("Test Quarry", StringComparison.Ordinal), "strong defense message should use configured target site name");
    AssertTrue(strongDefense.Message.Contains("Ash Court", StringComparison.Ordinal), "strong defense message should use configured attacker faction name");
    AssertTrue(!strongDefense.Message.Contains("埋骨地", StringComparison.Ordinal) && !strongDefense.Message.Contains("亡灵", StringComparison.Ordinal), "strong defense message should not hardcode default site or faction names");

    WorldActionResult costlyDefense = service.ResolveRaidAutomatically(
        BuildThreatAutoResolveState(militia: 2, towers: 0),
        definition,
        "threat:auto");
    AssertTrue(costlyDefense.Message.Contains("Guard Recruit", StringComparison.Ordinal), "costly defense message should use injected unit display name");
    AssertTrue(!costlyDefense.Message.Contains("民兵", StringComparison.Ordinal), "costly defense message should not hardcode default militia name");

    WorldActionResult damagedDefense = service.ResolveRaidAutomatically(
        BuildThreatAutoResolveState(militia: 1, towers: 0),
        definition,
        "threat:auto");
    AssertTrue(damagedDefense.Message.Contains("Test Quarry", StringComparison.Ordinal), "damaged defense message should use configured site name");
    AssertTrue(damagedDefense.Message.Contains("Deep Quarry", StringComparison.Ordinal), "damaged defense message should use configured mine name");
    AssertTrue(!damagedDefense.Message.Contains("埋骨地", StringComparison.Ordinal) && !damagedDefense.Message.Contains("矿场", StringComparison.Ordinal), "damaged defense message should not hardcode default site or mine names");

    WorldActionResult lostDefense = service.ResolveRaidAutomatically(
        BuildThreatAutoResolveState(militia: 0, towers: 0),
        definition,
        "threat:auto");
    AssertTrue(lostDefense.Message.Contains("Test Quarry", StringComparison.Ordinal), "lost defense message should use configured site name");
    AssertTrue(lostDefense.Message.Contains("Ash Court", StringComparison.Ordinal), "lost defense message should use configured faction name");
    AssertTrue(!lostDefense.Message.Contains("埋骨地", StringComparison.Ordinal) && !lostDefense.Message.Contains("亡灵", StringComparison.Ordinal), "lost defense message should not hardcode default site or faction names");
}

internal static void WorldActionNonPopulationShortageUsesCustomDisplayName()
{
    StrategicWorldDefinition definition = BuildResourceDisplayNameTestDefinition();
    StrategicWorldState state = BuildResourceDisplayNameTestState();
    state.PlayerResources.Set(StrategicWorldIds.ResourceEconomy, 0);

    WorldActionViewModel action = new WorldActionResolver()
        .GetAvailableActions(state, definition, StrategicWorldIds.SiteBonefield)
        .Single(item => item.ActionId == "test_economy_cost_action");

    AssertEqual(false, action.IsEnabled, "economy-cost action should be disabled when custom economy resource is missing");
    AssertEqual("Coin不足", action.DisabledReason, "non-population shortage should use the missing resource display name");
    AssertTrue(
        !action.DisabledReason.Contains("资源不足", StringComparison.Ordinal),
        "non-population shortage should not use the generic resource shortage label");
    AssertTrue(
        !action.DisabledReason.Contains(StrategicWorldIds.ResourceEconomy, StringComparison.Ordinal),
        "non-population shortage should not expose the resource id when a display name exists");

    WorldActionResult result = new WorldActionResolver().Apply(
        state,
        definition,
        new WorldActionRequest
        {
            ActionId = "test_economy_cost_action",
            SourceSiteId = StrategicWorldIds.SiteBonefield,
            TargetSiteId = StrategicWorldIds.SiteBonefield
        },
        "",
        "");

    AssertEqual(false, result.Success, "economy-cost action should fail when applied without enough custom economy resource");
    AssertEqual("Coin不足", result.Message, "failed action result should use the missing resource display name");
    AssertTrue(
        result.FailureReason.Contains(StrategicWorldIds.ResourceEconomy, StringComparison.Ordinal),
        "failure reason should carry the concrete missing resource id for formatting");
}

internal static void WorldActionBlankResourceDisplayNameFallsBackToId()
{
    StrategicWorldDefinition definition = BuildResourceDisplayNameTestDefinition();
    definition.ResourceDefinitions.Single(item => item.Id == StrategicWorldIds.ResourceEconomy).DisplayName = "";
    StrategicWorldState state = BuildResourceDisplayNameTestState();
    state.PlayerResources.Set(StrategicWorldIds.ResourceEconomy, 0);

    WorldActionViewModel action = new WorldActionResolver()
        .GetAvailableActions(state, definition, StrategicWorldIds.SiteBonefield)
        .Single(item => item.ActionId == "test_economy_cost_action");

    AssertEqual(
        $"{StrategicWorldIds.ResourceEconomy}不足",
        action.DisabledReason,
        "blank resource DisplayName should fall back to the resource id instead of an empty label");
}

internal static void WorldOpportunityRewardTextUsesCustomResourceDisplayName()
{
    StrategicWorldDefinition definition = BuildResourceDisplayNameTestDefinition();
    definition.OpportunityDefinitions.Add(new WorldOpportunityDefinition
    {
        Id = "test_opportunity",
        DisplayName = "Test Cache",
        CompletionRewards = { new ResourceAmountDefinition(StrategicWorldIds.ResourceStone, 3) }
    });
    StrategicWorldState state = BuildResourceDisplayNameTestState();
    state.OpportunityStates["opportunity:test"] = new WorldOpportunityState
    {
        OpportunityId = "opportunity:test",
        DefinitionId = "test_opportunity",
        Status = WorldOpportunityStatus.Active,
        SpawnPointId = "spawn:test"
    };

    WorldActionResult result = new WorldOpportunityService().CompleteOpportunity(
        state,
        definition,
        "opportunity:test");

    AssertEqual(true, result.Success, "opportunity completion should succeed");
    AssertTrue(
        result.Message.Contains("Granite +3", StringComparison.Ordinal),
        "opportunity reward text should use custom resource display name");
    AssertTrue(
        !result.Message.Contains("石材 +3", StringComparison.Ordinal),
        "opportunity reward text should not hardcode the default stone label");
}

internal static void WorldTickProductionTextUsesCustomDisplayNames()
{
    StrategicWorldDefinition definition = BuildResourceDisplayNameTestDefinition();
    StrategicWorldState state = BuildResourceDisplayNameTestState();
    state.SiteStates[StrategicWorldIds.SiteBonefield].Facilities.Add(new FacilityInstance
    {
        InstanceId = "mine:test",
        FacilityId = StrategicWorldIds.FacilityMine,
        SiteId = StrategicWorldIds.SiteBonefield,
        State = FacilityState.Active,
        AssignedPopulation = 1
    });

    WorldTickResult result = new WorldTickService().AdvanceWorldTick(state, definition);

    AssertTrue(
        result.Messages.Any(message => message.Contains("Granite +2", StringComparison.Ordinal)),
        "mine production message should use custom stone display name");
    AssertTrue(
        result.Messages.Any(message => message.Contains("Deep Quarry", StringComparison.Ordinal)),
        "mine production message should use custom mine display name");
    AssertTrue(
        !result.Messages.Any(message => message.Contains("石材 +2", StringComparison.Ordinal) || message.Contains("矿场", StringComparison.Ordinal)),
        "mine production message should not hardcode default resource or facility labels");
}

internal static void WorldTickThreatFeedUsesConfiguredDisplayNames()
{
    StrategicWorldDefinition spawnDefinition = BuildResourceDisplayNameTestDefinition();
    spawnDefinition.ThreatRules.Add(new ThreatRuleDefinition
    {
        Id = "test_threat_rule",
        SourceSiteId = StrategicWorldIds.SiteGraveyard,
        TargetSiteId = StrategicWorldIds.SiteBonefield,
        InitialCountdownTicks = 3,
        ThreatType = ThreatType.Raid,
        EnemyForces = { new GarrisonDefinition { UnitTypeId = StrategicWorldIds.UnitMilitia, Count = 1 } }
    });
    StrategicWorldState spawnState = BuildResourceDisplayNameTestState();
    spawnState.SiteStates[StrategicWorldIds.SiteGraveyard].Garrison.Add(new GarrisonState
    {
        UnitTypeId = StrategicWorldIds.UnitMilitia,
        Count = 1
    });

    WorldTickResult spawnResult = new WorldTickService().AdvanceWorldTick(spawnState, spawnDefinition);

    AssertTrue(
        spawnResult.Messages.Any(message =>
            message.Contains("Ash Gate", StringComparison.Ordinal) &&
            message.Contains("Ash Court", StringComparison.Ordinal) &&
            message.Contains("Test Quarry", StringComparison.Ordinal)),
        "threat spawn message should use configured source site, faction, and target site names");
    AssertTrue(
        !spawnResult.Messages.Any(message => message.Contains("敌军", StringComparison.Ordinal)),
        "threat spawn message should not use generic hardcoded enemy label when a faction display name exists");

    StrategicWorldDefinition arrivalDefinition = BuildResourceDisplayNameTestDefinition();
    StrategicWorldState arrivalState = BuildResourceDisplayNameTestState();
    arrivalState.ThreatPlans["threat:arrival"] = new EnemyThreatPlan
    {
        Id = "threat:arrival",
        SourceSiteId = StrategicWorldIds.SiteGraveyard,
        TargetSiteId = StrategicWorldIds.SiteBonefield,
        Stage = ThreatStage.Marching,
        InitialCountdownTicks = 1,
        CountdownTicks = 1,
        CreatedTick = 0
    };

    WorldTickResult arrivalResult = new WorldTickService().AdvanceWorldTick(arrivalState, arrivalDefinition);

    AssertTrue(
        arrivalResult.Messages.Any(message =>
            message.Contains("Ash Court", StringComparison.Ordinal) &&
            message.Contains("Test Quarry", StringComparison.Ordinal)),
        "threat arrival message should use configured faction and target site names");
    AssertTrue(
        !arrivalResult.Messages.Any(message => message.Contains("敌方", StringComparison.Ordinal)),
        "threat arrival message should not use generic hardcoded enemy label when a faction display name exists");
}
}
