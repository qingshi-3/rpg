using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Application.StrategicBattleBridge;
using Rpg.Application.StrategicManagement;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;

internal static partial class StrategicManagementRegressionCases
{
    internal static void CommonCityIdentityDerivesCommonMusterTemplates()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementRules rules = new(definitions);

        IReadOnlyList<StrategicMusterTemplateAvailability> templates =
            rules.GetMusterTemplates(state, StrategicManagementIds.LocationPlainsCity);

        AssertAvailable(templates, StrategicManagementIds.CorpsShieldLine);
        AssertAvailable(templates, StrategicManagementIds.CorpsArcherLine);
        AssertAvailable(templates, StrategicManagementIds.CorpsCavalryLine);
        AssertTrue(
            templates.All(template => template.CorpsDefinitionId != "corps_wolf_pack" && template.CorpsDefinitionId != "corps_great_beast"),
            "foundation muster templates should not expose beast-route corps");
    }

    internal static void BuildCityBuildingConsumesResourcesAndRecordsPlacement()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
        StrategicConstructionRegionDefinition economy = FindRegion(definitions, StrategicManagementIds.RegionPlainsEconomy);
        int beforeMoney = state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceMoney);
        int beforeWood = state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceWood);

        StrategicCommandResult result = commands.BuildCityBuilding(
            state,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.BuildingFarm,
            StrategicManagementIds.RegionPlainsEconomy,
            economy.OriginX,
            economy.OriginY);

        AssertTrue(result.Success, $"farm build should succeed, got {result.FailureReason}");
        StrategicCityState city = state.Cities[StrategicManagementIds.LocationPlainsCity];
        AssertEqual(1, city.Buildings.Count, "city should store one placed building instance");
        StrategicBuildingInstanceState building = city.Buildings[0];
        AssertEqual(StrategicManagementIds.BuildingFarm, building.BuildingDefinitionId, "building instance should record definition id");
        AssertEqual(StrategicManagementIds.RegionPlainsEconomy, building.ConstructionRegionId, "building instance should record construction region");
        AssertEqual(economy.OriginX, building.GridX, "building instance should record grid x");
        AssertEqual(economy.OriginY, building.GridY, "building instance should record grid y");
        AssertEqual(1, building.Level, "new building should start at level 1");
        AssertEqual("", building.BattleAnchorId, "foundation slice should keep future battle anchor optional and empty");
        AssertTrue(state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceMoney) < beforeMoney, "money should be spent");
        AssertTrue(state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceWood) < beforeWood, "wood should be spent");
        AssertTrue(result.Events.Any(item => item.Kind == "StrategicCityBuildingPlaced"), "building placement should emit a low-noise event");
    }

    internal static void BuildCityBuildingRejectsInvalidPlacementWithoutMutation()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementRules rules = new(definitions);
        StrategicManagementCommandService commands = new(definitions, rules);
        StrategicConstructionRegionDefinition economy = FindRegion(definitions, StrategicManagementIds.RegionPlainsEconomy);

        StrategicCommandResult valid = commands.BuildCityBuilding(
            state,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.BuildingFarm,
            StrategicManagementIds.RegionPlainsEconomy,
            economy.OriginX,
            economy.OriginY);
        AssertTrue(valid.Success, $"setup farm build should succeed, got {valid.FailureReason}");

        StrategicCommandResult overlap = commands.BuildCityBuilding(
            state,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.BuildingMarket,
            StrategicManagementIds.RegionPlainsEconomy,
            economy.OriginX,
            economy.OriginY);
        AssertTrue(!overlap.Success, "overlapping building placement should fail");
        AssertEqual(StrategicFailureReasons.BuildingPlacementOccupied, overlap.FailureReason, "overlap should report occupied cells");

        StrategicCommandResult outside = commands.BuildCityBuilding(
            state,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.BuildingMarket,
            StrategicManagementIds.RegionPlainsEconomy,
            economy.OriginX + economy.Width,
            economy.OriginY + economy.Height);
        AssertTrue(!outside.Success, "outside-region building placement should fail");
        AssertEqual(StrategicFailureReasons.BuildingPlacementOutOfBounds, outside.FailureReason, "out-of-bounds should be explicit");

        StrategicCommandResult crossCategory = commands.BuildCityBuilding(
            state,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.BuildingTrainingGround,
            StrategicManagementIds.RegionPlainsEconomy,
            economy.OriginX + 2,
            economy.OriginY);
        AssertTrue(crossCategory.Success, $"cross-category placement inside a buildable region should succeed, got {crossCategory.FailureReason}");

        int beforeBuildings = state.Cities[StrategicManagementIds.LocationPlainsCity].Buildings.Count;
        int beforeMoney = state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceMoney);

        state.SetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceWood, 0);
        StrategicCommandResult noResources = commands.BuildCityBuilding(
            state,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.BuildingMarket,
            StrategicManagementIds.RegionPlainsEconomy,
            economy.OriginX + 5,
            economy.OriginY);
        AssertTrue(!noResources.Success, "building placement should fail without resources");
        AssertEqual(StrategicFailureReasons.InsufficientResources, noResources.FailureReason, "resource shortage should be explicit");

        AssertEqual(beforeBuildings, state.Cities[StrategicManagementIds.LocationPlainsCity].Buildings.Count, "failed placements must not add buildings");
        AssertEqual(beforeMoney, state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceMoney), "failed placements must not spend money");
        AssertEqual(0, state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceWood), "test resource mutation should stay the only wood change after failed placement");
    }

    internal static void CreateCorpsConsumesResourcesReserveAndCreatesPersistentCorpsInstance()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
        StrategicCityState city = state.Cities[StrategicManagementIds.LocationPlainsCity];
        int beforeMoney = state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceMoney);
        int beforeFood = state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceFood);
        int beforeReserve = city.ReserveForces;

        StrategicCommandResult result = commands.CreateCorps(
            state,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.CorpsShieldLine);

        AssertTrue(result.Success, $"shield corps creation should succeed, got {result.FailureReason}");
        AssertTrue(state.CorpsInstances.ContainsKey(result.CreatedEntityId), "created corps instance should be durable state");
        StrategicCorpsInstanceState corps = state.CorpsInstances[result.CreatedEntityId];
        AssertEqual(100, corps.Strength, "new corps should start at full strength");
        AssertEqual(StrategicCorpsInstanceStatus.Garrisoned, corps.Status, "new corps should start in city garrison");
        AssertEqual(beforeMoney - 30, state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceMoney), "money should be spent");
        AssertEqual(beforeFood - 20, state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceFood), "food should be spent");
        AssertEqual(beforeReserve - definitions.Corps[StrategicManagementIds.CorpsShieldLine].SoldierCapacityCost, city.ReserveForces, "reserve soldiers should be consumed");
    }

    internal static void CreateCorpsFailureLeavesResourcesReserveAndCorpsListUnchanged()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
        StrategicCityState city = state.Cities[StrategicManagementIds.LocationPlainsCity];
        city.ReserveForces = 0;
        int beforeMoney = state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceMoney);
        int beforeFood = state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceFood);
        int beforeCorps = state.CorpsInstances.Count;

        StrategicCommandResult result = commands.CreateCorps(
            state,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.CorpsShieldLine);

        AssertTrue(!result.Success, "shield corps creation should fail without reserve soldiers");
        AssertEqual(StrategicFailureReasons.InsufficientReserveForces, result.FailureReason, "reserve shortage should be reported first");
        AssertEqual(beforeCorps, state.CorpsInstances.Count, "corps list should not change");
        AssertEqual(beforeMoney, state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceMoney), "money should not change");
        AssertEqual(beforeFood, state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceFood), "food should not change");
        AssertEqual(0, city.ReserveForces, "reserve should not change");
    }

    internal static void ReplenishCorpsConsumesResourcesReserveAndRestoresStrength()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementRules rules = new(definitions);
        StrategicManagementCommandService commands = new(definitions, rules);
        string corpsInstanceId = state.Heroes[StrategicManagementIds.HeroOrdinaryCommander].AssignedCorpsInstanceId;
        StrategicCorpsInstanceState corps = state.CorpsInstances[corpsInstanceId];
        StrategicCityState city = state.Cities[StrategicManagementIds.LocationPlainsCity];
        corps.Strength = 60;
        int beforeReserve = city.ReserveForces;
        int beforeFood = state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceFood);
        int expectedReserveCost = rules.GetCorpsReplenishmentReserveCost(state, corpsInstanceId, 100);

        StrategicCommandResult result = commands.ReplenishCorps(
            state,
            StrategicManagementIds.LocationPlainsCity,
            corpsInstanceId,
            100);

        AssertTrue(result.Success, $"replenishment should succeed, got {result.FailureReason}");
        AssertEqual(100, corps.Strength, "replenishment should restore corps strength to requested target");
        AssertEqual(beforeReserve - expectedReserveCost, city.ReserveForces, "reserve soldiers should be consumed by replenishment");
        AssertTrue(state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceFood) < beforeFood, "food should be consumed by replenishment");
        AssertTrue(result.Events.Any(item => item.Kind == "StrategicCorpsReplenished"), "replenishment should emit a low-noise event");
    }

    internal static void ReplenishCorpsFailureLeavesResourcesReserveAndStrengthUnchanged()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
        string corpsInstanceId = state.Heroes[StrategicManagementIds.HeroOrdinaryCommander].AssignedCorpsInstanceId;
        StrategicCorpsInstanceState corps = state.CorpsInstances[corpsInstanceId];
        StrategicCityState city = state.Cities[StrategicManagementIds.LocationPlainsCity];
        corps.Strength = 40;
        city.ReserveForces = 0;
        int beforeFood = state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceFood);

        StrategicCommandResult result = commands.ReplenishCorps(
            state,
            StrategicManagementIds.LocationPlainsCity,
            corpsInstanceId,
            100);

        AssertTrue(!result.Success, "replenishment should fail without reserve soldiers");
        AssertEqual(StrategicFailureReasons.InsufficientReserveForces, result.FailureReason, "reserve shortage should be explicit");
        AssertEqual(40, corps.Strength, "failed replenishment must not change strength");
        AssertEqual(0, city.ReserveForces, "failed replenishment must not change reserve");
        AssertEqual(beforeFood, state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceFood), "failed replenishment must not spend resources");
    }

    internal static void AssignCorpsToHeroRecordsAptitudeWithoutRandomFailure()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
        StrategicCommandResult unassign = commands.UnassignCorpsFromHero(state, StrategicManagementIds.HeroCavalryCaptain);
        AssertTrue(unassign.Success, $"test setup unassignment should succeed, got {unassign.FailureReason}");
        StrategicCommandResult create = commands.CreateCorps(
            state,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.CorpsCavalryLine);

        StrategicCommandResult assign = commands.AssignCorpsToHero(
            state,
            StrategicManagementIds.HeroCavalryCaptain,
            create.CreatedEntityId);

        AssertTrue(assign.Success, $"assignment should succeed, got {assign.FailureReason}");
        AssertEqual(create.CreatedEntityId, state.Heroes[StrategicManagementIds.HeroCavalryCaptain].AssignedCorpsInstanceId, "hero should reference assigned corps");
        AssertEqual(StrategicManagementIds.HeroCavalryCaptain, state.CorpsInstances[create.CreatedEntityId].AssignedHeroId, "corps should reference assigned hero");
        AssertEqual(StrategicHeroCorpsAptitudeGrade.B, assign.AptitudeGrade, "common cavalry assignment should record baseline aptitude");
        AssertTrue(!assign.Events.Any(item => item.Kind == "RandomBeastControlFailure"), "assignment must not create random beast failure");
    }
}
