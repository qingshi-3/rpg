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
internal static void BattleResultApplierMessagesUseConfiguredDisplayNames()
{
    StrategicWorldDefinition definition = BuildBattleResultApplierTestDefinition();
    WorldBattleResultApplier applier = new();

    BattleStartRequest assaultVictoryRequest = BuildBattleResultMessageRequest(BattleKind.AssaultSite, BattleOutcome.Victory);
    WorldActionResult assaultVictory = applier.Apply(
        new StrategicWorldService().CreateInitialState(definition),
        definition,
        assaultVictoryRequest,
        BuildVictoryResult(assaultVictoryRequest, "occupy_bonefield"));
    AssertTrue(assaultVictory.Message.Contains("Test Quarry", StringComparison.Ordinal), "assault victory message should use configured site name");
    AssertTrue(assaultVictory.Message.Contains("Deep Quarry", StringComparison.Ordinal), "assault victory message should use configured mine name");
    AssertTrue(assaultVictory.Message.Contains("Signal Spire", StringComparison.Ordinal), "assault victory message should use configured tower name");
    AssertTrue(!assaultVictory.Message.Contains("埋骨地", StringComparison.Ordinal) && !assaultVictory.Message.Contains("矿场", StringComparison.Ordinal) && !assaultVictory.Message.Contains("防御塔", StringComparison.Ordinal), "assault victory message should not hardcode default entity names");

    BattleStartRequest assaultFailureRequest = BuildBattleResultMessageRequest(BattleKind.AssaultSite, BattleOutcome.Defeat);
    WorldActionResult assaultFailure = applier.Apply(
        new StrategicWorldService().CreateInitialState(definition),
        definition,
        assaultFailureRequest,
        new BattleResult
        {
            RequestId = assaultFailureRequest.RequestId,
            BattleKind = assaultFailureRequest.BattleKind,
            Outcome = BattleOutcome.Defeat
        });
    AssertTrue(assaultFailure.Message.Contains("Test Quarry", StringComparison.Ordinal), "assault failure message should use configured site name");
    AssertTrue(!assaultFailure.Message.Contains("埋骨地", StringComparison.Ordinal), "assault failure message should not hardcode default site name");

    BattleStartRequest defenseVictoryRequest = BuildBattleResultMessageRequest(BattleKind.DefenseRaid, BattleOutcome.Victory);
    WorldActionResult defenseVictory = applier.Apply(
        new StrategicWorldService().CreateInitialState(definition),
        definition,
        defenseVictoryRequest,
        BuildVictoryResult(defenseVictoryRequest, "defend_bonefield"));
    AssertTrue(defenseVictory.Message.Contains("Test Quarry", StringComparison.Ordinal), "defense victory message should use configured site name");
    AssertTrue(defenseVictory.Message.Contains("Ash Court", StringComparison.Ordinal), "defense victory message should use configured attacker faction name");
    AssertTrue(!defenseVictory.Message.Contains("埋骨地", StringComparison.Ordinal) && !defenseVictory.Message.Contains("亡灵", StringComparison.Ordinal), "defense victory message should not hardcode default site or faction names");

    BattleStartRequest defenseFailureRequest = BuildBattleResultMessageRequest(BattleKind.DefenseRaid, BattleOutcome.Defeat);
    WorldActionResult defenseFailure = applier.Apply(
        new StrategicWorldService().CreateInitialState(definition),
        definition,
        defenseFailureRequest,
        new BattleResult
        {
            RequestId = defenseFailureRequest.RequestId,
            BattleKind = defenseFailureRequest.BattleKind,
            Outcome = BattleOutcome.Defeat
        });
    AssertTrue(defenseFailure.Message.Contains("Test Quarry", StringComparison.Ordinal), "defense failure message should use configured site name");
    AssertTrue(defenseFailure.Message.Contains("Ash Court", StringComparison.Ordinal), "defense failure message should use configured attacker faction name");
    AssertTrue(!defenseFailure.Message.Contains("埋骨地", StringComparison.Ordinal) && !defenseFailure.Message.Contains("亡灵", StringComparison.Ordinal), "defense failure message should not hardcode default site or faction names");
}

internal static void BattleUnitFactoryKeepsDefinitionCachesShared()
{
    string factory = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "BattleUnitFactory.cs"));

    AssertTrue(
        factory.Contains("SharedDefinitions", StringComparison.Ordinal),
        "battle unit definitions should be cached in a shared resident metadata cache");
    AssertTrue(
        factory.Contains("SharedDefinitionPathIndex", StringComparison.Ordinal),
        "nested unit definition path index should be shared instead of rebuilt per scene");
    AssertTrue(
        !factory.Contains("private readonly Dictionary<string, BattleUnitDefinition> _definitions", StringComparison.Ordinal),
        "per-scene unit definition cache rebuilds cause world detail clicks to rescan unit resources");
}

internal static void BattleResultApplierUsesSurvivorCountsWhenGarrisoningAssaultArmy()
{
    StrategicWorldDefinition definition = BuildBattleResultApplierTestDefinition();
    StrategicWorldState state = new StrategicWorldService().CreateInitialState(definition);
    WorldSiteState targetSite = state.SiteStates[StrategicWorldIds.SiteBonefield];
    targetSite.Garrison.Clear();
    targetSite.Garrison.Add(new GarrisonState { UnitTypeId = StrategicWorldIds.UnitSkeletonWarrior, Count = 1 });

    WorldArmyState army = new()
    {
        ArmyId = "assault:survivor-test",
        OwnerFactionId = state.PlayerFactionId,
        SourceSiteId = StrategicWorldIds.SitePlayerCamp,
        TargetSiteId = StrategicWorldIds.SiteBonefield,
        Status = WorldArmyStatus.Attacking,
        Intent = WorldArmyIntent.AssaultSite
    };
    army.GarrisonUnits.Add(new GarrisonState { UnitTypeId = StrategicWorldIds.UnitMilitia, Count = 3 });
    state.ArmyStates[army.ArmyId] = army;

    BattleStartRequest request = new()
    {
        RequestId = "assault-survivor-request",
        BattleKind = BattleKind.AssaultSite,
        SourceArmyId = army.ArmyId,
        TargetSiteId = StrategicWorldIds.SiteBonefield,
        AttackerFactionId = state.PlayerFactionId,
        DefenderFactionId = StrategicWorldIds.FactionUndead
    };
    request.ObjectiveIds.Add("occupy_bonefield");
    request.PlayerForces.Add(new BattleForceRequest
    {
        ForceId = "player:militia",
        SourceKind = "PlayerArmy",
        SourceId = army.ArmyId,
        UnitDefinitionId = StrategicWorldIds.UnitMilitia,
        Count = 3,
        FactionId = state.PlayerFactionId
    });
    request.EnemyForces.Add(new BattleForceRequest
    {
        ForceId = "defender:skeleton",
        SourceKind = "DefenderSite",
        SourceId = StrategicWorldIds.SiteBonefield,
        UnitDefinitionId = StrategicWorldIds.UnitSkeletonWarrior,
        Count = 1,
        FactionId = StrategicWorldIds.FactionUndead
    });

    BattleResult result = BuildVictoryResult(request, "occupy_bonefield");
    result.ForceResults.Add(new BattleForceResult
    {
        SourceKind = "PlayerArmy",
        SourceId = army.ArmyId,
        UnitDefinitionId = StrategicWorldIds.UnitMilitia,
        InitialCount = 3,
        SurvivedCount = 1,
        DefeatedCount = 2
    });
    result.ForceResults.Add(new BattleForceResult
    {
        SourceKind = "DefenderSite",
        SourceId = StrategicWorldIds.SiteBonefield,
        UnitDefinitionId = StrategicWorldIds.UnitSkeletonWarrior,
        InitialCount = 1,
        SurvivedCount = 0,
        DefeatedCount = 1
    });

    new WorldBattleResultApplier().Apply(state, definition, request, result);

    AssertEqual(1, targetSite.Garrison.Where(item => item.UnitTypeId == StrategicWorldIds.UnitMilitia).Sum(item => item.Count), "only surviving attacker units should garrison captured site");
    AssertEqual(0, army.GarrisonUnits.Sum(item => item.Count), "assault army should be emptied after survivor transfer");
}

internal static void BattleResultApplierKeepsSurvivingDefendingGarrisonAfterDefenseVictory()
{
    StrategicWorldDefinition definition = BuildBattleResultApplierTestDefinition();
    StrategicWorldState state = new StrategicWorldService().CreateInitialState(definition);
    WorldSiteState targetSite = state.SiteStates[StrategicWorldIds.SiteBonefield];
    targetSite.OwnerFactionId = state.PlayerFactionId;
    targetSite.ControlState = SiteControlState.PlayerHeld;
    targetSite.Garrison.Clear();
    targetSite.Garrison.Add(new GarrisonState { UnitTypeId = StrategicWorldIds.UnitMilitia, Count = 4 });

    BattleStartRequest request = new()
    {
        RequestId = "defense-garrison-survivor-request",
        BattleKind = BattleKind.DefenseRaid,
        TargetSiteId = StrategicWorldIds.SiteBonefield,
        AttackerFactionId = StrategicWorldIds.FactionUndead,
        DefenderFactionId = state.PlayerFactionId
    };
    request.ObjectiveIds.Add("defend_bonefield");
    request.PlayerForces.Add(new BattleForceRequest
    {
        ForceId = "garrison:militia",
        SourceKind = "Garrison",
        SourceId = StrategicWorldIds.SiteBonefield,
        UnitDefinitionId = StrategicWorldIds.UnitMilitia,
        Count = 4,
        FactionId = state.PlayerFactionId
    });

    BattleResult result = BuildVictoryResult(request, "defend_bonefield");
    result.ForceResults.Add(new BattleForceResult
    {
        ForceId = "garrison:militia",
        SourceKind = "Garrison",
        SourceId = StrategicWorldIds.SiteBonefield,
        UnitDefinitionId = StrategicWorldIds.UnitMilitia,
        InitialCount = 4,
        SurvivedCount = 2,
        DefeatedCount = 2
    });

    new WorldBattleResultApplier().Apply(state, definition, request, result);

    AssertEqual(2, targetSite.Garrison.Where(item => item.UnitTypeId == StrategicWorldIds.UnitMilitia).Sum(item => item.Count), "defending site garrison should lose only defeated units");
}

internal static StrategicWorldDefinition BuildBattleResultApplierTestDefinition()
{
    return new StrategicWorldDefinition
    {
        Id = "battle-result-applier-test",
        PlayerFactionId = StrategicWorldIds.FactionPlayer,
        FactionDefinitions =
        {
            new FactionDefinition { Id = StrategicWorldIds.FactionPlayer, DisplayName = "Guild" },
            new FactionDefinition { Id = StrategicWorldIds.FactionUndead, DisplayName = "Ash Court" }
        },
        FacilityDefinitions =
        {
            new FacilityDefinition { Id = StrategicWorldIds.FacilityMine, DisplayName = "Deep Quarry" },
            new FacilityDefinition { Id = StrategicWorldIds.FacilityDefenseTower, DisplayName = "Signal Spire" }
        },
        SiteDefinitions =
        {
            BuildBattleResultApplierTestSite(StrategicWorldIds.SitePlayerCamp, StrategicWorldIds.FactionPlayer, SiteControlState.PlayerHeld),
            BuildBattleResultApplierTestSite(StrategicWorldIds.SiteBonefield, StrategicWorldIds.FactionUndead, SiteControlState.Hostile)
        }
    };
}

internal static BattleStartRequest BuildBattleResultMessageRequest(BattleKind kind, BattleOutcome outcome)
{
    BattleStartRequest request = new()
    {
        RequestId = $"message:{kind}:{outcome}",
        BattleKind = kind,
        TargetSiteId = StrategicWorldIds.SiteBonefield,
        AttackerFactionId = kind == BattleKind.DefenseRaid ? StrategicWorldIds.FactionUndead : StrategicWorldIds.FactionPlayer,
        DefenderFactionId = kind == BattleKind.DefenseRaid ? StrategicWorldIds.FactionPlayer : StrategicWorldIds.FactionUndead
    };
    request.ObjectiveIds.Add(kind == BattleKind.DefenseRaid ? "defend_bonefield" : "occupy_bonefield");
    return request;
}

internal static StrategicWorldDefinition BuildResourceDisplayNameTestDefinition()
{
    return new StrategicWorldDefinition
    {
        Id = "resource-display-name-test",
        PlayerFactionId = StrategicWorldIds.FactionPlayer,
        FactionDefinitions =
        {
            new FactionDefinition { Id = StrategicWorldIds.FactionPlayer, DisplayName = "Guild" },
            new FactionDefinition { Id = StrategicWorldIds.FactionUndead, DisplayName = "Ash Court" }
        },
        ResourceDefinitions =
        {
            new ResourceDefinition { Id = StrategicWorldIds.ResourcePopulation, DisplayName = "Labor" },
            new ResourceDefinition { Id = StrategicWorldIds.ResourceStone, DisplayName = "Granite" },
            new ResourceDefinition { Id = StrategicWorldIds.ResourceEconomy, DisplayName = "Coin" }
        },
        FacilityDefinitions =
        {
            new FacilityDefinition { Id = StrategicWorldIds.FacilityMine, DisplayName = "Deep Quarry" },
            new FacilityDefinition { Id = StrategicWorldIds.FacilityDefenseTower, DisplayName = "Signal Spire" }
        },
        SiteDefinitions =
        {
            new WorldSiteDefinition
            {
                Id = StrategicWorldIds.SitePlayerCamp,
                DisplayName = "Forward Camp",
                InitialOwnerFactionId = StrategicWorldIds.FactionPlayer,
                InitialControlState = SiteControlState.PlayerHeld
            },
            new WorldSiteDefinition
            {
                Id = StrategicWorldIds.SiteBonefield,
                DisplayName = "Test Quarry",
                InitialOwnerFactionId = StrategicWorldIds.FactionPlayer,
                InitialControlState = SiteControlState.PlayerHeld
            },
            new WorldSiteDefinition
            {
                Id = StrategicWorldIds.SiteGraveyard,
                DisplayName = "Ash Gate",
                InitialOwnerFactionId = StrategicWorldIds.FactionUndead,
                InitialControlState = SiteControlState.Hostile
            }
        },
        ActionDefinitions =
        {
            new WorldActionDefinition
            {
                Id = StrategicWorldIds.ActionBuildMine,
                DisplayName = "Build Test Mine",
                Scope = WorldActionScope.Site,
                Costs = { new ResourceAmountDefinition(StrategicWorldIds.ResourcePopulation, 1) }
            },
            new WorldActionDefinition
            {
                Id = StrategicWorldIds.ActionBuildDefenseTower,
                DisplayName = "Build Test Tower",
                Scope = WorldActionScope.Site
            },
            new WorldActionDefinition
            {
                Id = StrategicWorldIds.ActionTrainMilitia,
                DisplayName = "Train Test Militia",
                Scope = WorldActionScope.Site
            },
            new WorldActionDefinition
            {
                Id = StrategicWorldIds.ActionAutoResolveRaid,
                DisplayName = "Auto Resolve Test Raid",
                Scope = WorldActionScope.Threat
            },
            new WorldActionDefinition
            {
                Id = "test_economy_cost_action",
                DisplayName = "Spend Coin",
                Scope = WorldActionScope.Site,
                Costs = { new ResourceAmountDefinition(StrategicWorldIds.ResourceEconomy, 5) }
            }
        }
    };
}

internal static StrategicWorldState BuildResourceDisplayNameTestState()
{
    return new StrategicWorldState
    {
        PlayerFactionId = StrategicWorldIds.FactionPlayer,
        SiteStates =
        {
            [StrategicWorldIds.SitePlayerCamp] = new WorldSiteState
            {
                SiteId = StrategicWorldIds.SitePlayerCamp,
                OwnerFactionId = StrategicWorldIds.FactionPlayer,
                ControlState = SiteControlState.PlayerHeld,
                SiteMode = WorldSiteMode.Peacetime
            },
            [StrategicWorldIds.SiteBonefield] = new WorldSiteState
            {
                SiteId = StrategicWorldIds.SiteBonefield,
                OwnerFactionId = StrategicWorldIds.FactionPlayer,
                ControlState = SiteControlState.PlayerHeld,
                SiteMode = WorldSiteMode.Peacetime
            },
            [StrategicWorldIds.SiteGraveyard] = new WorldSiteState
            {
                SiteId = StrategicWorldIds.SiteGraveyard,
                OwnerFactionId = StrategicWorldIds.FactionUndead,
                ControlState = SiteControlState.Hostile,
                SiteMode = WorldSiteMode.Peacetime
            }
        }
    };
}

internal static StrategicWorldState BuildThreatAutoResolveState(int militia, int towers)
{
    StrategicWorldState state = BuildResourceDisplayNameTestState();
    WorldSiteState site = state.SiteStates[StrategicWorldIds.SiteBonefield];
    site.PendingThreatIds.Add("threat:auto");
    if (militia > 0)
    {
        site.Garrison.Add(new GarrisonState
        {
            UnitTypeId = StrategicWorldIds.UnitMilitia,
            Count = militia
        });
    }

    for (int index = 0; index < towers; index++)
    {
        site.Facilities.Add(new FacilityInstance
        {
            InstanceId = $"tower:auto:{index}",
            FacilityId = StrategicWorldIds.FacilityDefenseTower,
            SiteId = StrategicWorldIds.SiteBonefield,
            State = FacilityState.Active
        });
    }

    site.Facilities.Add(new FacilityInstance
    {
        InstanceId = "mine:auto",
        FacilityId = StrategicWorldIds.FacilityMine,
        SiteId = StrategicWorldIds.SiteBonefield,
        State = FacilityState.Active
    });

    state.ThreatPlans["threat:auto"] = new EnemyThreatPlan
    {
        Id = "threat:auto",
        SourceSiteId = StrategicWorldIds.SiteGraveyard,
        TargetSiteId = StrategicWorldIds.SiteBonefield,
        Stage = ThreatStage.Attacking
    };
    return state;
}

internal static WorldSiteDefinition BuildBattleResultApplierTestSite(
    string siteId,
    string factionId,
    SiteControlState controlState)
{
    SiteDeploymentZoneDefinition zone = new()
    {
        ZoneId = WorldSiteDeploymentService.DefaultGarrisonZoneId,
        ZoneKind = SiteDeploymentZoneKind.DefaultGarrison,
        Capacity = 12
    };
    for (int i = 0; i < zone.Capacity; i++)
    {
        zone.Cells.Add(new Godot.Vector2I(i, 0));
    }

    return new WorldSiteDefinition
    {
        Id = siteId,
        DisplayName = siteId == StrategicWorldIds.SiteBonefield ? "Test Quarry" : "Forward Camp",
        InitialOwnerFactionId = factionId,
        InitialControlState = controlState,
        DefaultGarrisonZoneId = zone.ZoneId,
        DeploymentZones = { zone }
    };
}

internal static BattleResult BuildVictoryResult(BattleStartRequest request, string objectiveId)
{
    BattleResult result = new()
    {
        RequestId = request.RequestId,
        ContextId = request.ContextId,
        BattleKind = request.BattleKind,
        Outcome = BattleOutcome.Victory
    };
    result.ObjectiveResults.Add(new BattleObjectiveResult
    {
        ObjectiveId = objectiveId,
        State = BattleObjectiveState.Succeeded
    });
    return result;
}

internal static void UnitDisplayNameTranslationReportQuality()
{
    string reportPath = Path.Combine("assets", "battle", "units", "_display_name_translation_report.json");
    using JsonDocument report = JsonDocument.Parse(File.ReadAllText(reportPath));
    JsonElement summary = report.RootElement.GetProperty("summary");

    int lowConfidenceCount = summary.GetProperty("lowConfidenceCount").GetInt32();
    int duelystSourceNameCount = summary.GetProperty("duelystSourceNameCount").GetInt32();

    AssertTrue(
        lowConfidenceCount <= 120,
        $"translation report should leave only a bounded manual review queue, actual={lowConfidenceCount}");
    AssertTrue(
        duelystSourceNameCount >= 400,
        $"translation report should use Duelyst source names for most assets, actual={duelystSourceNameCount}");
}
}
