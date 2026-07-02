using System;
using System.IO;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.World;

internal static partial class WorldSiteDeploymentCacheRegressionCases
{
internal static void FirstSliceHeroCompaniesUseAuthoredUnitResources()
{
    string root = ProjectRoot();
    string idsPath = Path.Combine(root, "src", "Application", "World", "FirstSliceHeroCompanyIds.cs");
    string initialStatePath = Path.Combine(root, "config", "world", "strategic_world_v1_initial_state.json");
    string companyConfigPath = Path.Combine(root, "config", "battle", "first_slice_hero_companies.json");
    string unitIndexPath = Path.Combine(root, "config", "battle", "unit_definition_index.json");
    string oldInitialStatePath = Path.Combine(root, "assets", "definitions", "world", "strategic_world_v1_initial_state.tres");
    AssertTrue(File.Exists(idsPath), "first-slice content should use a three-company id catalog instead of the old one-hero V0 constants");
    AssertTrue(File.Exists(initialStatePath), "strategic initial roster config should live under config/world");
    AssertTrue(File.Exists(companyConfigPath), "first-slice battle-group mappings should live under config/battle");
    AssertTrue(File.Exists(unitIndexPath), "configured unit resource path index should live under config/battle");
    AssertTrue(!File.Exists(oldInitialStatePath), "strategic initial roster config should no longer live under assets/definitions");

    string idsSource = File.ReadAllText(idsPath);
    string initialState = File.ReadAllText(initialStatePath);
    string companyConfig = File.ReadAllText(companyConfigPath);
    string unitIndex = File.ReadAllText(unitIndexPath);

    AssertTrue(
        idsSource.Contains("FirstSliceHeroCompanyConfigLoader", StringComparison.Ordinal) &&
        !idsSource.Contains("new FirstSliceHeroCompanyDefinition", StringComparison.Ordinal),
        "first-slice company query wrapper should load mappings from config instead of hardcoding the content list");
    foreach (string id in new[]
             {
                 "f1_grandmasterzir",
                 "f1_azuritelion",
                 "f1_windbladecommander",
                 "f1_backlinearcher",
                 "f1_elyxstormblade",
                 "f1_radiantdragoon",
                 "f6_draugarlord",
                 "f6_spiritwolf",
                 "f4_skullcaster"
             })
    {
        AssertTrue(initialState.Contains(id, StringComparison.Ordinal) || companyConfig.Contains(id, StringComparison.Ordinal), $"first-slice config should reference unit id={id}");
        AssertTrue(unitIndex.Contains(id, StringComparison.Ordinal), $"unit resource index should map unit id={id}");
    }

    AssertTrue(!initialState.Contains("res://assets/battle/units", StringComparison.Ordinal), "strategic initial state config should store unit ids, not resource paths");
    AssertTrue(companyConfig.Contains("\"defaultCorpsCount\": 3", StringComparison.Ordinal), "selected battle group should attach three corps soldiers through config");
    AssertTrue(
        companyConfig.Contains("\"heroUnit\": \"f1_elyxstormblade\"", StringComparison.Ordinal) &&
        companyConfig.Contains("\"skillDefinitionIds\"", StringComparison.Ordinal) &&
        companyConfig.Contains("\"skill_thunder_tag_throw\"", StringComparison.Ordinal) &&
        companyConfig.Contains("\"skill_thunder_mark_fold\"", StringComparison.Ordinal) &&
        companyConfig.Contains("\"skill_thunder_spiral_break\"", StringComparison.Ordinal) &&
        !companyConfig.Contains("\"first_slice_skill_whirling_break\"", StringComparison.Ordinal),
        "assault battle group should advertise the configured thunder demo skill grant list instead of the old placeholder");
    AssertConfiguredUnitPath(unitIndex, "assets/battle/units/莱昂纳王国/f1_宗师Zir/unit.tres", "shield hero index path");
    AssertConfiguredUnitPath(unitIndex, "assets/battle/units/莱昂纳王国/f1_风刃指挥官/unit.tres", "archer hero index path");
    AssertConfiguredUnitPath(unitIndex, "assets/battle/units/莱昂纳王国/f1_Elyx风暴刃/unit.tres", "assault hero index path");
    AssertConfiguredUnitPath(unitIndex, "assets/battle/units/霜原部盟/f6_Draugar领主/unit.tres", "Bonefield leader index path");
    AssertConfiguredUnitPath(unitIndex, "assets/battle/units/霜原部盟/f6_灵魂狼/unit.tres", "Bonefield harassment index path");
    AssertConfiguredUnitPath(unitIndex, "assets/battle/units/深渊军团/f4_Skull施法者/unit.tres", "Bonefield ranged index path");
    AssertUnitDisplayName("assets/battle/units/莱昂纳王国/f1_宗师Zir/unit.tres", "曦盾执旗者", "shield hero display name");
    AssertUnitDisplayName("assets/battle/units/莱昂纳王国/f1_天蓝石狮/unit.tres", "天蓝石狮卫", "shield corps display name");
    AssertUnitDisplayName("assets/battle/units/莱昂纳王国/f1_风刃指挥官/unit.tres", "逐日号令官", "archer hero display name");
    AssertUnitDisplayName("assets/battle/units/莱昂纳王国/f1_后排弓手/unit.tres", "穿阳弓手", "archer corps display name");
    AssertUnitDisplayName("assets/battle/units/莱昂纳王国/f1_Elyx风暴刃/unit.tres", "裂光剑卫", "assault hero display name");
    AssertUnitDisplayName("assets/battle/units/莱昂纳王国/f1_辉光龙骑兵/unit.tres", "辉光龙骑", "assault corps display name");
    AssertUnitDisplayName("assets/battle/units/霜原部盟/f6_Draugar领主/unit.tres", "寒骨冢主", "Bonefield leader display name");
    AssertUnitDisplayName("assets/battle/units/霜原部盟/f6_灵魂狼/unit.tres", "霜魂猎犬", "Bonefield harassment enemy display name");
    AssertUnitDisplayName("assets/battle/units/深渊军团/f4_Skull施法者/unit.tres", "颅火先知", "Bonefield ranged enemy display name");
    AssertUnitFootprint("assets/battle/units/莱昂纳王国/f1_天蓝石狮/unit.tres", 2, 1, "azurite lion corps");
    AssertUnitFootprint("assets/battle/units/莱昂纳王国/f1_宗师Zir/unit.tres", 2, 1, "grandmaster Zir hero");
    AssertUnitFootprint("assets/battle/units/霜原部盟/f6_Draugar领主/unit.tres", 2, 2, "draugar lord enemy hero");
}

internal static void FirstSliceAssaultRequestUsesSelectedHeroCompanyAndBonefieldRoster()
{
    StrategicWorldDefinition definition = StrategicWorldV1DefinitionFactory.Create(loadInitialStateConfig: false);
    StrategicWorldState state = BuildFirstSliceAssaultState(
        definition,
        "army_first_slice_archer",
        heroUnitId: "f1_windbladecommander",
        corpsUnitId: "f1_backlinearcher");

    BattleStartRequest request = new WorldBattleRequestBuilder().BuildAssaultBonefieldRequest(
        state,
        definition,
        "res://scenes/world/StrategicWorldRoot.tscn",
        "res://scenes/world/sites/WorldSiteRoot.tscn",
        "army_first_slice_archer");

    AssertEqual(2, request.PlayerForces.Count, "first slice assault should contain one selected hero force and one default corps force");
    AssertEqual(4, request.PlayerForces.Sum(force => force.Count), "first slice assault should field the selected hero plus three corps units");
    AssertEqual(6, request.EnemyForces.Sum(force => force.Count), "first slice Bonefield should field leader plus two readable enemy roles");
    AssertTrue(
        request.PlayerForces.Any(force => force.UnitDefinitionId == "f1_windbladecommander" && force.Count == 1),
        "first slice assault should read the selected hero from the source army");
    AssertTrue(
        request.PlayerForces.Any(force => force.UnitDefinitionId == "f1_backlinearcher" && force.Count == 3),
        "first slice assault should read the selected hero's attached default corps from the source army");
    AssertTrue(
        request.PlayerForces.All(force => force.UnitDefinitionId != "f1_grandmasterzir" && force.UnitDefinitionId != "f1_elyxstormblade"),
        "first slice assault should not deploy unselected battle groups");
    AssertTrue(
        request.EnemyForces.Any(force => force.UnitDefinitionId == "f6_draugarlord" && force.Count == 2),
        "first slice assault should read the Bonefield leader from the target site garrison");
    AssertTrue(
        request.EnemyForces.Any(force => force.UnitDefinitionId == "f6_spiritwolf" && force.Count == 2),
        "first slice assault should include the Bonefield harassment enemy role");
    AssertTrue(
        request.EnemyForces.Any(force => force.UnitDefinitionId == "f4_skullcaster" && force.Count == 2),
        "first slice assault should include the Bonefield ranged enemy role");
}

internal static void FirstSliceAssaultRequestSplitsCarriedCompaniesIntoCommandGroups()
{
    const string armyId = "army_first_slice_multi_company";
    StrategicWorldDefinition definition = StrategicWorldV1DefinitionFactory.Create(loadInitialStateConfig: false);
    StrategicWorldState state = BuildFirstSliceAssaultState(
        definition,
        armyId,
        heroUnitId: "f1_grandmasterzir",
        corpsUnitId: "f1_azuritelion");
    state.ArmyStates[armyId].GarrisonUnits.Add(new GarrisonState
    {
        UnitTypeId = "f1_windbladecommander",
        Count = 1,
        Morale = 80
    });
    state.ArmyStates[armyId].GarrisonUnits.Add(new GarrisonState
    {
        UnitTypeId = "f1_backlinearcher",
        Count = 3,
        Morale = 70
    });

    BattleStartRequest request = new WorldBattleRequestBuilder().BuildAssaultBonefieldRequest(
        state,
        definition,
        "res://scenes/world/StrategicWorldRoot.tscn",
        "res://scenes/world/sites/WorldSiteRoot.tscn",
        armyId);

    AssertEqual(4, request.PlayerForces.Count, "multi-battle-group assault should carry two battle-group force pairs");
    AssertEqual(8, request.PlayerForces.Sum(force => force.Count), "two carried companies should field two heroes plus six corps soldiers before reserve pruning");
    AssertTrue(
        request.PlayerForces.All(force =>
            string.Equals(force.SourceKind, "PlayerArmy", StringComparison.Ordinal) &&
            string.Equals(force.SourceId, armyId, StringComparison.Ordinal)),
        "settlement source identity should remain the strategic army id for every carried company force");

    IGrouping<string, BattleForceRequest>[] commandGroups = request.PlayerForces
        .GroupBy(ReadCommandGroupId, StringComparer.Ordinal)
        .Where(group => !string.IsNullOrWhiteSpace(group.Key))
        .ToArray();
    AssertEqual(2, commandGroups.Length, "battle preparation should see each carried battle group as a separate command group");
    AssertTrue(
        commandGroups.Any(group =>
            group.Any(force => force.UnitDefinitionId == "f1_grandmasterzir") &&
            group.Any(force => force.UnitDefinitionId == "f1_azuritelion")),
        "shield hero and shield corps should share one command group");
    AssertTrue(
        commandGroups.Any(group =>
            group.Any(force => force.UnitDefinitionId == "f1_windbladecommander") &&
            group.Any(force => force.UnitDefinitionId == "f1_backlinearcher")),
        "archer hero and archer corps should share one command group");
}

internal static void FirstSliceAssaultImportsSelectedHeroCompanyIntoTargetSiteUnitPoolOnce()
{
    const string armyId = "army_first_slice_assault";
    StrategicWorldDefinition definition = StrategicWorldV1DefinitionFactory.Create(loadInitialStateConfig: false);
    StrategicWorldState state = BuildFirstSliceAssaultState(
        definition,
        armyId,
        heroUnitId: "f1_elyxstormblade",
        corpsUnitId: "f1_radiantdragoon");
    WorldSiteState site = state.SiteStates[StrategicWorldIds.SiteBonefield];

    BattleStartRequest request = new WorldBattleRequestBuilder().BuildAssaultBonefieldRequest(
        state,
        definition,
        "res://scenes/world/StrategicWorldRoot.tscn",
        "res://scenes/world/sites/WorldSiteRoot.tscn",
        armyId);
    BattleResult battleResult = BuildAssaultVictoryResult(request);
    new WorldBattleResultApplier().Apply(state, definition, request, battleResult);

    AssertEqual(
        1,
        site.Garrison.Where(garrison =>
            garrison.UnitTypeId == "f1_elyxstormblade" &&
            garrison.SourceKind == "PlayerArmy" &&
            garrison.SourceId == armyId).Sum(garrison => garrison.Count),
        "target site pool should contain exactly one imported hero after victory");
    AssertEqual(
        3,
        site.Garrison.Where(garrison =>
            garrison.UnitTypeId == "f1_radiantdragoon" &&
            garrison.SourceKind == "PlayerArmy" &&
            garrison.SourceId == armyId).Sum(garrison => garrison.Count),
        "target site pool should contain exactly the imported default corps after victory");
    AssertEqual(
        0,
        site.Garrison.Where(garrison =>
            (garrison.UnitTypeId == "f6_draugarlord" ||
             garrison.UnitTypeId == "f6_spiritwolf" ||
             garrison.UnitTypeId == "f4_skullcaster") &&
            garrison.FactionId == StrategicWorldIds.FactionUndead).Sum(garrison => garrison.Count),
        "target site pool should remove defeated Bonefield defender units after victory");
}

internal static void FirstSliceStrategicAssaultRequestDoesNotImportHeroCompanyIntoTargetSiteGarrison()
{
    const string armyId = "army_first_slice_strategic_assault";
    StrategicWorldDefinition definition = StrategicWorldV1DefinitionFactory.Create(loadInitialStateConfig: false);
    StrategicWorldState state = BuildFirstSliceAssaultState(
        definition,
        armyId,
        heroUnitId: "f1_elyxstormblade",
        corpsUnitId: "f1_radiantdragoon");
    state.ArmyStates[armyId].StrategicExpeditionId = "expedition_strategic_assault";
    state.ArmyStates[armyId].GarrisonUnits[0].StrategicParticipantId = "strategic_participant:expedition_strategic_assault:hero:corps";
    state.ArmyStates[armyId].GarrisonUnits[1].StrategicParticipantId = "strategic_participant:expedition_strategic_assault:hero:corps";
    WorldSiteState site = state.SiteStates[StrategicWorldIds.SiteBonefield];
    int garrisonCountBefore = site.Garrison.Count;

    BattleStartRequest request = new WorldBattleRequestBuilder().BuildAssaultBonefieldRequest(
        state,
        definition,
        "res://scenes/world/StrategicWorldRoot.tscn",
        "res://scenes/world/sites/WorldSiteRoot.tscn",
        armyId);

    AssertEqual(2, request.PlayerForces.Count, "strategic assault request should still expose the selected hero and corps forces");
    AssertTrue(
        request.PlayerForces.All(force =>
            string.Equals(force.SourceKind, "PlayerArmy", StringComparison.Ordinal) &&
            string.Equals(force.SourceId, armyId, StringComparison.Ordinal)),
        "strategic compatibility request should keep settlement source identity on player forces");
    AssertEqual(garrisonCountBefore, site.Garrison.Count, "strategic assault request must not add target-site garrison rows");
    AssertEqual(
        0,
        site.Garrison.Where(garrison =>
            garrison.SourceKind == "PlayerArmy" &&
            garrison.SourceId == armyId).Sum(garrison => garrison.Count),
        "strategic assault request must not import the battle group into legacy target garrison");
}

internal static void StrategicBattleLegacyGarrisonCleanupRemovesStalePlayerArmyRows()
{
    const string armyId = "army_stale_strategic_rows";
    WorldSiteState site = new()
    {
        SiteId = StrategicWorldIds.SiteBonefield,
        OwnerFactionId = StrategicWorldIds.FactionUndead
    };
    site.Garrison.Add(new GarrisonState
    {
        UnitTypeId = "f1_elyxstormblade",
        Count = 1,
        FactionId = StrategicWorldIds.FactionPlayer,
        SourceKind = "PlayerArmy",
        SourceId = armyId,
        StrategicParticipantId = "strategic_participant:expedition:hero:corps"
    });
    site.Garrison.Add(new GarrisonState
    {
        UnitTypeId = "f1_radiantdragoon",
        Count = 3,
        FactionId = StrategicWorldIds.FactionPlayer,
        SourceKind = "PlayerArmy",
        SourceId = armyId,
        StrategicParticipantId = "strategic_participant:expedition:hero:corps"
    });
    site.Garrison.Add(new GarrisonState
    {
        UnitTypeId = "f6_spiritwolf",
        Count = 2,
        FactionId = StrategicWorldIds.FactionUndead,
        SourceKind = "DefenderSite",
        SourceId = StrategicWorldIds.SiteBonefield
    });
    System.Reflection.MethodInfo? cleanup = typeof(WorldSiteBattleUnitPoolService).GetMethod(
        "RemoveImportedArmyForSiteBattle",
        new[] { typeof(WorldSiteState), typeof(string) });

    AssertTrue(cleanup != null, "battle unit pool service should expose imported army garrison cleanup");
    int removed = (int)cleanup!.Invoke(new WorldSiteBattleUnitPoolService(), new object[] { site, armyId })!;

    AssertEqual(4, removed, "cleanup should report the removed stale strategic unit count");
    AssertEqual(
        0,
        site.Garrison.Where(garrison =>
            garrison.SourceKind == "PlayerArmy" &&
            garrison.SourceId == armyId).Sum(garrison => garrison.Count),
        "cleanup should remove stale PlayerArmy garrison rows for the strategic army");
    AssertEqual(
        2,
        site.Garrison.Where(garrison => garrison.UnitTypeId == "f6_spiritwolf").Sum(garrison => garrison.Count),
        "cleanup must not remove unrelated resident defender rows");
}

internal static void FirstSliceAssaultSettlementKeepsUndeployedReserveCompanyOutOfCasualties()
{
    const string armyId = "army_first_slice_reserve";
    StrategicWorldDefinition definition = StrategicWorldV1DefinitionFactory.Create(loadInitialStateConfig: false);
    StrategicWorldState state = BuildFirstSliceAssaultState(
        definition,
        armyId,
        heroUnitId: "f1_grandmasterzir",
        corpsUnitId: "f1_azuritelion");
    state.ArmyStates[armyId].GarrisonUnits.Add(new GarrisonState
    {
        UnitTypeId = "f1_windbladecommander",
        Count = 1,
        Morale = 80
    });
    state.ArmyStates[armyId].GarrisonUnits.Add(new GarrisonState
    {
        UnitTypeId = "f1_backlinearcher",
        Count = 3,
        Morale = 70
    });
    WorldSiteState site = state.SiteStates[StrategicWorldIds.SiteBonefield];

    BattleStartRequest request = new WorldBattleRequestBuilder().BuildAssaultBonefieldRequest(
        state,
        definition,
        "res://scenes/world/StrategicWorldRoot.tscn",
        "res://scenes/world/sites/WorldSiteRoot.tscn",
        armyId);
    string deployedGroupId = ReadCommandGroupId(request.PlayerForces.First(force => force.UnitDefinitionId == "f1_grandmasterzir"));
    request.PlayerForces = request.PlayerForces
        .Where(force => string.Equals(ReadCommandGroupId(force), deployedGroupId, StringComparison.Ordinal))
        .ToList();

    BattleResult battleResult = BuildAssaultVictoryResult(request);
    new WorldBattleResultApplier().Apply(state, definition, request, battleResult);

    WorldArmyState army = state.ArmyStates[armyId];
    AssertEqual(
        1,
        army.GarrisonUnits.Where(unit => unit.UnitTypeId == "f1_windbladecommander").Sum(unit => unit.Count),
        "undeployed reserve hero should remain in the strategic army after battle settlement");
    AssertEqual(
        3,
        army.GarrisonUnits.Where(unit => unit.UnitTypeId == "f1_backlinearcher").Sum(unit => unit.Count),
        "undeployed reserve corps should remain in the strategic army after battle settlement");
    AssertEqual(
        0,
        site.Garrison.Where(garrison =>
            (garrison.UnitTypeId == "f1_windbladecommander" || garrison.UnitTypeId == "f1_backlinearcher") &&
            garrison.SourceKind == "PlayerArmy" &&
            garrison.SourceId == armyId).Sum(garrison => garrison.Count),
        "undeployed reserve company should not be left in the target battle pool as a deployed garrison");
}

private static void AssertConfiguredUnitPath(string indexText, string relativePath, string label)
{
    string normalized = relativePath.Replace('\\', '/');
    AssertTrue(
        indexText.Contains(normalized, StringComparison.Ordinal) &&
        File.Exists(Path.Combine(new[] { ProjectRoot() }.Concat(relativePath.Split('/')).ToArray())),
        label);
}

private static void AssertUnitFootprint(string relativePath, int expectedWidth, int expectedHeight, string label)
{
    string unitText = File.ReadAllText(Path.Combine(ProjectRoot(), relativePath));
    AssertTrue(unitText.Contains($"FootprintWidth = {expectedWidth}", StringComparison.Ordinal), $"{label} footprint width");
    AssertTrue(unitText.Contains($"FootprintHeight = {expectedHeight}", StringComparison.Ordinal), $"{label} footprint height");
}

private static void AssertUnitDisplayName(string relativePath, string expectedDisplayName, string label)
{
    string unitText = File.ReadAllText(Path.Combine(ProjectRoot(), relativePath));
    AssertTrue(unitText.Contains($"DisplayName = \"{expectedDisplayName}\"", StringComparison.Ordinal), label);
}

private static string ReadCommandGroupId(BattleForceRequest force)
{
    System.Reflection.PropertyInfo? commandGroupId = typeof(BattleForceRequest).GetProperty("CommandGroupId");
    AssertTrue(commandGroupId != null, "battle force request should carry a command-group id separate from settlement source id");
    return commandGroupId!.GetValue(force) as string ?? "";
}
}
