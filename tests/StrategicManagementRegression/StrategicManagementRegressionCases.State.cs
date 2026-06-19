using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Application.StrategicBattleBridge;
using Rpg.Application.StrategicManagement;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;
internal static partial class StrategicManagementRegressionCases
{
    internal static void StrategicManagementFacilityDefinitionsLoadFromConfig()
    {
        string root = ProjectRoot();
        string configPath = Path.Combine(root, "config", "strategic_management", "first_slice_facilities.json");
        string definitionsPath = Path.Combine(root, "src", "Definitions", "StrategicManagement", "FirstStrategicManagementDefinitions.cs");
        string loaderPath = Path.Combine(root, "src", "Application", "Config", "StrategicManagementFacilityDefinitionConfigLoader.cs");

        AssertTrue(File.Exists(configPath), "first-slice strategic facility definitions should live under config/strategic_management");
        string configText = File.ReadAllText(configPath);
        string definitionsSource = File.ReadAllText(definitionsPath);
        string loaderSource = File.ReadAllText(loaderPath);

        AssertTrue(
            configText.Contains("\"facilityDefinitionId\": \"facility_training_ground\"", StringComparison.Ordinal) &&
            configText.Contains("\"facilityDefinitionId\": \"facility_beast_pen\"", StringComparison.Ordinal) &&
            configText.Contains("\"providedTags\"", StringComparison.Ordinal) &&
            configText.Contains("\"buildCost\"", StringComparison.Ordinal),
            "facility config should carry ids, provided tags, and build costs");
        AssertTrue(
            definitionsSource.Contains("StrategicManagementFacilityDefinitionConfigLoader.LoadDefaultFacilities", StringComparison.Ordinal) &&
            !definitionsSource.Contains("new StrategicFacilityDefinition", StringComparison.Ordinal),
            "first-slice strategic definitions should use the facility config loader instead of inline facility content literals");
        AssertTrue(
            loaderSource.Contains("ProjectConfigFileReader.ReadAllText", StringComparison.Ordinal) &&
            loaderSource.Contains("ProjectJson.Options", StringComparison.Ordinal) &&
            loaderSource.Contains("Duplicate strategic management facility id", StringComparison.Ordinal),
            "facility config loader should use the project config reader and fail explicitly on invalid content");

        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicFacilityDefinition trainingGround = definitions.Facilities[StrategicManagementIds.FacilityTrainingGround];
        StrategicFacilityDefinition beastPen = definitions.Facilities[StrategicManagementIds.FacilityBeastPen];
        AssertEqual("训练场", trainingGround.DisplayName, "training ground display name should come from config");
        AssertContains(trainingGround.ProvidedTags, StrategicManagementIds.FacilityTagCommonTraining, "training ground should provide common training");
        AssertEqual(40, trainingGround.BuildCost.First(cost => cost.ResourceId == StrategicManagementIds.ResourceBuildingMaterials).Amount, "training ground material cost should come from config");
        AssertEqual("兽栏", beastPen.DisplayName, "beast pen display name should come from config");
        AssertContains(beastPen.ProvidedTags, StrategicManagementIds.FacilityTagBeastPen, "beast pen should provide beast pen tag");
    }

    internal static void StrategicManagementStateInitializesWithoutLegacyWorldState()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);

        AssertTrue(state.FactionResources.ContainsKey(StrategicManagementIds.FactionPlayer), "player faction resources should be initialized");
        AssertTrue(state.Cities.ContainsKey(StrategicManagementIds.LocationPlainsCity), "first core city should be initialized");
        AssertTrue(state.Heroes.ContainsKey(StrategicManagementIds.HeroBeastTamer), "first strategic hero should be initialized");
        AssertNoLegacyWorldReferences(typeof(StrategicManagementState));
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
                company.HeroId == StrategicManagementIds.HeroBeastTamer &&
                company.CorpsDefinitionId == StrategicManagementIds.CorpsCavalryLine),
            "beast tamer should start with the cavalry-style assault company");
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
            StrategicManagementIds.LocationBeastDen,
            bonefieldLocationId,
            "bonefield should map to the first enemy beast-source target");
        AssertTrue(
            !resolver.TryResolveCityId(StrategicManagementIds.MapSiteBonefield, out string bonefieldCityId),
            "non-city beast-source sites must not resolve as managed cities");
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
            StrategicManagementIds.LocationBeastDen,
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
