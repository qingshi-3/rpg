using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Application.StrategicBattleBridge;
using Rpg.Application.StrategicManagement;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;

internal static partial class StrategicManagementRegressionCases
{
    internal static void StrategicManagementFoundationBuildingDefinitionsLoadFromConfig()
    {
        string root = ProjectRoot();
        string configPath = Path.Combine(root, "config", "strategic_management", "first_slice_buildings.json");
        string definitionsPath = Path.Combine(root, "src", "Definitions", "StrategicManagement", "FirstStrategicManagementDefinitions.cs");
        string loaderPath = Path.Combine(root, "src", "Application", "Config", "StrategicManagementBuildingDefinitionConfigLoader.cs");

        AssertTrue(File.Exists(configPath), "first-slice strategic building definitions should live under config/strategic_management");
        string configText = File.ReadAllText(configPath);
        string definitionsSource = File.ReadAllText(definitionsPath);
        string loaderSource = File.ReadAllText(loaderPath);

        AssertTrue(
            configText.Contains("\"buildingDefinitionId\": \"building_training_ground\"", StringComparison.Ordinal) &&
            configText.Contains("\"buildingDefinitionId\": \"building_farm\"", StringComparison.Ordinal) &&
            configText.Contains("\"categoryId\"", StringComparison.Ordinal) &&
            configText.Contains("\"iconPath\"", StringComparison.Ordinal) &&
            configText.Contains("\"footprintWidth\"", StringComparison.Ordinal) &&
            configText.Contains("\"buildCost\"", StringComparison.Ordinal),
            "building config should carry ids, icons, categories, footprints, and build costs");
        AssertTrue(
            definitionsSource.Contains("StrategicManagementBuildingDefinitionConfigLoader.LoadDefaultBuildings", StringComparison.Ordinal) &&
            !definitionsSource.Contains("new StrategicFacilityDefinition", StringComparison.Ordinal),
            "first-slice strategic definitions should use the building config loader instead of inline facility content literals");
        AssertTrue(
            loaderSource.Contains("ProjectConfigFileReader.ReadAllText", StringComparison.Ordinal) &&
            loaderSource.Contains("ProjectJson.Options", StringComparison.Ordinal) &&
            loaderSource.Contains("Duplicate strategic management building id", StringComparison.Ordinal),
            "building config loader should use the project config reader and fail explicitly on invalid content");
        AssertTrue(
            typeof(StrategicBuildingDefinition).GetProperty("IconPath") != null &&
            typeof(StrategicBuildingOptionViewModel).GetProperty("IconPath") != null,
            "building definitions and dashboard options should carry a Presentation icon path for the RTS-style build picker");

        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicBuildingDefinition trainingGround = definitions.Buildings[StrategicManagementIds.BuildingTrainingGround];
        StrategicBuildingDefinition farm = definitions.Buildings[StrategicManagementIds.BuildingFarm];
        string trainingIconPath = (string)(typeof(StrategicBuildingDefinition).GetProperty("IconPath")?.GetValue(trainingGround) ?? "");
        string farmIconPath = (string)(typeof(StrategicBuildingDefinition).GetProperty("IconPath")?.GetValue(farm) ?? "");
        AssertEqual("训练场", trainingGround.DisplayName, "training ground display name should come from config");
        AssertEqual(StrategicManagementIds.BuildingCategoryMilitary, trainingGround.CategoryId, "training ground should be military");
        AssertEqual(60, trainingGround.CityForceCapacityBonus, "training ground should increase city force capacity");
        AssertEqual(12, trainingGround.ReserveRecoveryPerWorldTimePulse, "training ground should recover reserve soldiers over world-map time");
        AssertTrue(
            trainingIconPath == "res://assets/textures/world/Buildings/Wood/Barracks.png" &&
            farmIconPath.StartsWith("res://assets/textures/world/Buildings/Wood/", StringComparison.Ordinal),
            "first-slice building icons should use existing authored building textures");
        AssertEqual(StrategicManagementIds.BuildingCategoryEconomy, farm.CategoryId, "farm should be an economy building");
        AssertEqual(18, farm.ProductionPerWorldTimePulse.First(cost => cost.ResourceId == StrategicManagementIds.ResourceFood).Amount, "farm food income should come from config");
    }

    internal static void StrategicManagementFoundationResourcesReplaceObsoleteFirstLoopResources()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();

        AssertTrue(definitions.Resources.ContainsKey(StrategicManagementIds.ResourceMoney), "foundation resources should include money");
        AssertTrue(definitions.Resources.ContainsKey(StrategicManagementIds.ResourceFood), "foundation resources should include food");
        AssertTrue(definitions.Resources.ContainsKey(StrategicManagementIds.ResourceWood), "foundation resources should include wood");
        AssertTrue(definitions.Resources.ContainsKey(StrategicManagementIds.ResourceOre), "foundation resources should include ore");
        AssertTrue(!definitions.Resources.ContainsKey("resource_building_materials"), "foundation resources should not expose old building materials");
        AssertTrue(!definitions.Resources.ContainsKey("resource_beast_materials"), "foundation resources should not expose beast materials");
        AssertTrue(
            typeof(StrategicManagementDefinitionSet).GetProperty("Facilities") == null,
            "Strategic Management definitions should not keep facilities as city-development authority");
        AssertTrue(
            typeof(StrategicLocationDefinition).GetProperty("FacilitySlotCount") == null,
            "location definitions should not keep facility slots as city-development authority");
    }

    internal static void StrategicManagementStateInitializesWithoutLegacyWorldState()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);

        AssertTrue(state.FactionResources.ContainsKey(StrategicManagementIds.FactionPlayer), "player faction resources should be initialized");
        AssertTrue(state.Cities.ContainsKey(StrategicManagementIds.LocationPlainsCity), "first core city should be initialized");
        AssertTrue(state.Heroes.ContainsKey(StrategicManagementIds.HeroCavalryCaptain), "first strategic heroes should be initialized");
        AssertTrue(
            typeof(StrategicCityState).GetProperty("Facilities") == null &&
            typeof(StrategicCityState).GetProperty("FacilitySlotCount") == null,
            "city state should not keep old facility-slot authority");
        AssertNoLegacyWorldReferences(typeof(StrategicManagementState));
    }

    internal static void FirstCityInitializesConstructionRegionsReserveAndForceCapacity()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicCityState city = state.Cities[StrategicManagementIds.LocationPlainsCity];

        AssertTrue(city.ConstructionRegionIds.Count >= 3, "first city should expose authored construction regions");
        AssertContains(city.ConstructionRegionIds, StrategicManagementIds.RegionPlainsEconomy, "first city should include an economy construction region");
        AssertContains(city.ConstructionRegionIds, StrategicManagementIds.RegionPlainsMilitary, "first city should include a military construction region");
        AssertEqual(220, city.CityForceCapacity, "first city should start with the accepted foundation force capacity");
        AssertEqual(80, city.ReserveForces, "first city should start with prepared reserve soldiers");
        AssertEqual(0, city.Buildings.Count, "first city should start with no placed city buildings");

        StrategicManagementRules rules = new(definitions);
        AssertEqual(100, rules.GetActiveForces(state, city.LocationId), "starting active forces should be derived from the three starting corps");
        AssertEqual(40, rules.GetRemainingCityForceCapacity(state, city.LocationId), "remaining capacity should be capacity minus active plus reserve");
    }

    internal static void FirstPlayableStartsWithThreeDispatchableHeroCompanies()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementViewModelService viewModels = new(definitions, new StrategicManagementRules(definitions));

        StrategicManagementDashboardViewModel dashboard = viewModels.BuildDashboard(
            state,
            StrategicManagementIds.FactionPlayer,
            StrategicManagementIds.LocationPlainsCity);

        AssertEqual(3, dashboard.Heroes.Count, "first playable should expose three strategic heroes");
        AssertEqual(3, dashboard.SelectedCity.HeroCompanies.Count, "first playable should start with three dispatchable hero companies");
        AssertTrue(dashboard.SelectedCity.HeroCompanies.All(company => company.CanCreateExpedition), "all starting hero companies should be dispatchable");
        AssertTrue(
            dashboard.SelectedCity.HeroCompanies.Any(company =>
                company.HeroId == StrategicManagementIds.HeroOrdinaryCommander &&
                company.CorpsDefinitionId == StrategicManagementIds.CorpsShieldLine),
            "ordinary commander should start with the shield corps company");
        AssertTrue(
            dashboard.SelectedCity.HeroCompanies.Any(company =>
                company.HeroId == StrategicManagementIds.HeroArcherCaptain &&
                company.CorpsDefinitionId == StrategicManagementIds.CorpsArcherLine),
            "archer captain should start with the archer corps company");
        AssertTrue(
            dashboard.SelectedCity.HeroCompanies.Any(company =>
                company.HeroId == StrategicManagementIds.HeroCavalryCaptain &&
                company.CorpsDefinitionId == StrategicManagementIds.CorpsCavalryLine),
            "cavalry captain should start with the cavalry company");
    }

    internal static void StrategicManagementResolvesMapSiteIdsWithoutSilentCityFallback()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementMapSiteResolver resolver = new(definitions);

        AssertTrue(
            resolver.TryResolveLocationId(StrategicManagementIds.MapSitePlayerCamp, out string playerCampLocationId),
            "player camp map site should resolve to a strategic location");
        AssertEqual(
            StrategicManagementIds.LocationPlainsCity,
            playerCampLocationId,
            "player camp should map to the first managed city");
        AssertTrue(
            resolver.TryResolveCityId(StrategicManagementIds.MapSitePlayerCamp, out string playerCampCityId),
            "player camp should resolve to a managed city");
        AssertEqual(
            StrategicManagementIds.LocationPlainsCity,
            playerCampCityId,
            "player camp city mapping should be the plains city");

        AssertTrue(
            resolver.TryResolveLocationId(StrategicManagementIds.MapSiteBonefield, out string bonefieldLocationId),
            "bonefield map site should resolve to a strategic location");
        AssertEqual(
            StrategicManagementIds.LocationBonefieldOutpost,
            bonefieldLocationId,
            "bonefield should map to the first hostile foundation target");
        AssertTrue(
            !resolver.TryResolveCityId(StrategicManagementIds.MapSiteBonefield, out string bonefieldCityId),
            "non-city hostile targets must not resolve as managed cities");
        AssertEqual("", bonefieldCityId, "non-city city mapping output should stay empty");

        AssertTrue(
            !resolver.TryResolveLocationId("unknown_site", out string unknownLocationId),
            "unknown map sites must not silently fall back to the first city");
        AssertEqual("", unknownLocationId, "unknown map-site mapping output should stay empty");
    }

    internal static void StrategicManagementHasNoStrategicBattlePreparationChoiceSystem()
    {
        AssertTrue(
            typeof(StrategicManagementDefinitionSet).GetProperty("BattlePreparations") == null,
            "Strategic Management definitions should not expose first-slice strategic battle-preparation options");
        AssertTrue(
            typeof(StrategicManagementState).GetProperty("SelectedBattlePreparations") == null,
            "Strategic Management state should not store strategic battle-preparation selections");
        AssertTrue(
            typeof(StrategicManagementCommandService).GetMethod("SelectBattlePreparation") == null,
            "Strategic Management commands should not expose strategic battle-preparation selection");
        AssertTrue(
            typeof(StrategicManagementRules).GetMethod("GetBattlePreparationSelectionFailureReason") == null,
            "Strategic Management rules should not gate battle entry on strategic preparation selection");
    }

    internal static void StrategicManagementDashboardHidesStrategicBattlePreparationOptions()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementDashboardViewModel dashboard = new StrategicManagementViewModelService(
                definitions,
                new StrategicManagementRules(definitions))
            .BuildDashboard(
                state,
                StrategicManagementIds.FactionPlayer,
                StrategicManagementIds.LocationPlainsCity);

        AssertTrue(
            dashboard.SelectedCity.GetType().GetProperty("BattlePreparations") == null,
            "selected city dashboard should not expose strategic battle-preparation options");
    }

    internal static void BonefieldAssaultCreatesExpeditionWithoutStrategicPreparation()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));

        StrategicCommandResult result = commands.CreateExpedition(
            state,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.LocationBonefieldOutpost,
            StrategicExpeditionIntent.AssaultLocation,
            StrategicManagementIds.HeroOrdinaryCommander);

        AssertTrue(result.Success, $"Bonefield assault should create an expedition without strategic preparation, got {result.FailureReason}");
        AssertEqual(1, state.Expeditions.Count, "successful assault expedition should mutate expedition state");
        AssertEqual(
            result.CreatedEntityId,
            state.Heroes[StrategicManagementIds.HeroOrdinaryCommander].CurrentExpeditionId,
            "successful assault expedition should lock the selected hero");
    }
}
