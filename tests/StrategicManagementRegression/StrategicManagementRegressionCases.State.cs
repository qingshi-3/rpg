using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Application.Config;
using Rpg.Application.StrategicBattleBridge;
using Rpg.Application.StrategicManagement;
using Rpg.Application.World;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;

internal static partial class StrategicManagementRegressionCases
{
    internal static void StrategicManagementFoundationBuildingDefinitionsLoadFromConfig()
    {
        string root = ProjectRoot();
        string configPath = Path.Combine(root, "config", "strategic_management", "cities", "buildings_foundation.json");
        string retiredConfigPath = Path.Combine(root, "config", "strategic_management", "first_slice_buildings.json");
        string definitionsPath = Path.Combine(root, "src", "Definitions", "StrategicManagement", "FirstStrategicManagementDefinitions.cs");
        string loaderPath = Path.Combine(root, "src", "Application", "Config", "StrategicManagementBuildingDefinitionConfigLoader.cs");

        AssertTrue(File.Exists(configPath), "foundation strategic building definitions should live under config/strategic_management/cities");
        AssertTrue(!File.Exists(retiredConfigPath), "strategic building config should not remain as a root first_slice_buildings.json file");
        string configText = File.ReadAllText(configPath);
        string definitionsSource = File.ReadAllText(definitionsPath);
        string loaderSource = File.ReadAllText(loaderPath);

        AssertTrue(
            configText.Contains("\"buildingDefinitionId\": \"building_training_ground\"", StringComparison.Ordinal) &&
            configText.Contains("\"buildingDefinitionId\": \"building_farm\"", StringComparison.Ordinal) &&
            configText.Contains("\"categoryId\"", StringComparison.Ordinal) &&
            configText.Contains("\"iconPath\"", StringComparison.Ordinal) &&
            configText.Contains("\"footprintWidth\"", StringComparison.Ordinal) &&
            configText.Contains("\"buildCost\"", StringComparison.Ordinal) &&
            configText.Contains("\"providedCapabilities\"", StringComparison.Ordinal) &&
            configText.Contains("\"resourceProductionPerWorldTimePulse\"", StringComparison.Ordinal),
            "building config should carry ids, icons, categories, footprints, build costs, and first-slice provided capabilities");
        foreach (string retiredField in new[]
        {
            "productionPerWorldTimePulse",
            "cityForceCapacityBonus",
            "reserveRecoveryPerWorldTimePulse"
        })
        {
            AssertTrue(
                !configText.Contains(retiredField, StringComparison.Ordinal),
                $"building config should not keep retired direct building effect field={retiredField}");
        }
        AssertTrue(
            loaderSource.Contains("res://config/strategic_management/cities/buildings_foundation.json", StringComparison.Ordinal) &&
            definitionsSource.Contains("StrategicManagementContentConfigLoader.LoadDefaultContent", StringComparison.Ordinal) &&
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
        AssertTrue(
            typeof(StrategicBuildingDefinition).GetProperty("ProvidedCapabilities") != null,
            "building definitions should expose a thin provided-capability object instead of retired direct scalar fields");
        foreach (string retiredProperty in new[]
        {
            "ProductionPerWorldTimePulse",
            "CityForceCapacityBonus",
            "ReserveRecoveryPerWorldTimePulse"
        })
        {
            AssertTrue(
                typeof(StrategicBuildingDefinition).GetProperty(retiredProperty) == null,
                $"building definitions should not expose retired direct scalar effect property={retiredProperty}");
        }

        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicBuildingDefinition trainingGround = definitions.Buildings[StrategicManagementIds.BuildingTrainingGround];
        StrategicBuildingDefinition farm = definitions.Buildings[StrategicManagementIds.BuildingFarm];
        string trainingIconPath = (string)(typeof(StrategicBuildingDefinition).GetProperty("IconPath")?.GetValue(trainingGround) ?? "");
        string farmIconPath = (string)(typeof(StrategicBuildingDefinition).GetProperty("IconPath")?.GetValue(farm) ?? "");
        AssertEqual("训练场", trainingGround.DisplayName, "training ground display name should come from config");
        AssertEqual(StrategicManagementIds.BuildingCategoryMilitary, trainingGround.CategoryId, "training ground should be military");
        AssertTrue(
            trainingIconPath == "res://resource/ui/icons/buildings/foundation/training_ground_icon.tres" &&
            farmIconPath == "res://resource/ui/icons/buildings/foundation/farm_icon.tres",
            "first-slice building icons should use focused AtlasTexture resources instead of whole building sprite sheets");
        foreach (StrategicBuildingDefinition building in definitions.Buildings.Values)
        {
            AssertTrue(
                building.IconPath.StartsWith("res://resource/ui/icons/buildings/foundation/", StringComparison.Ordinal) &&
                building.IconPath.EndsWith("_icon.tres", StringComparison.Ordinal),
                $"building icon should point at a single authored atlas texture resource id={building.BuildingDefinitionId} path={building.IconPath}");
            string localIconPath = Path.Combine(root, building.IconPath["res://".Length..].Replace('/', Path.DirectorySeparatorChar));
            AssertTrue(File.Exists(localIconPath), $"building icon atlas resource should exist id={building.BuildingDefinitionId} path={localIconPath}");
            string iconSource = File.ReadAllText(localIconPath);
            AssertTrue(
                iconSource.Contains("[gd_resource type=\"AtlasTexture\"", StringComparison.Ordinal) &&
                iconSource.Contains("atlas = ExtResource(", StringComparison.Ordinal) &&
                iconSource.Contains("region = Rect2(", StringComparison.Ordinal),
                $"building icon should be an AtlasTexture with an explicit single-building region id={building.BuildingDefinitionId}");
            AssertTrue(
                !iconSource.Contains("region = Rect2(0, 0, 80, 16)", StringComparison.Ordinal) &&
                !iconSource.Contains("region = Rect2(0, 0, 48, 64)", StringComparison.Ordinal) &&
                !iconSource.Contains("region = Rect2(0, 0, 48, 80)", StringComparison.Ordinal),
                $"building icon atlas region must not reuse a whole multi-building sheet id={building.BuildingDefinitionId}");
        }
        AssertEqual(StrategicManagementIds.BuildingCategoryEconomy, farm.CategoryId, "farm should be an economy building");
        object capabilities = GetRequiredProperty<object>(farm, "ProvidedCapabilities");
        IReadOnlyCollection<StrategicResourceAmount> production =
            GetRequiredProperty<IReadOnlyCollection<StrategicResourceAmount>>(capabilities, "ResourceProductionPerWorldTimePulse");
        AssertEqual(
            8,
            FindStrategicAmount(production, StrategicManagementIds.ResourceFood),
            "farm should provide first-slice food production through building capabilities");
    }

    internal static void StrategicManagementFoundationContentUsesModuleConfigAuthority()
    {
        string root = ProjectRoot();
        string configRoot = Path.Combine(root, "config", "strategic_management");
        string definitionsPath = Path.Combine(root, "src", "Definitions", "StrategicManagement", "FirstStrategicManagementDefinitions.cs");
        string rulesPath = Path.Combine(root, "src", "Application", "StrategicManagement", "StrategicManagementRules.cs");
        string loaderDir = Path.Combine(root, "src", "Application", "Config");

        foreach (string requiredPath in new[]
        {
            Path.Combine(configRoot, "economy", "resources.json"),
            Path.Combine(configRoot, "cities", "buildings_foundation.json"),
            Path.Combine(configRoot, "military", "corps_common.json")
        })
        {
            AssertTrue(File.Exists(requiredPath), $"Strategic Management foundation content should be split by module path={requiredPath}");
        }

        foreach (string forbiddenDirectory in new[]
        {
            Path.Combine(configRoot, "packs"),
            Path.Combine(configRoot, "content_sets")
        })
        {
            AssertTrue(!Directory.Exists(forbiddenDirectory), $"first version should not introduce content-pack directories path={forbiddenDirectory}");
        }

        string definitionsSource = File.ReadAllText(definitionsPath);
        string rulesSource = File.ReadAllText(rulesPath);
        string configLoaderSource = string.Join(
            "\n",
            Directory.GetFiles(loaderDir, "StrategicManagement*.cs", SearchOption.AllDirectories)
                .OrderBy(path => path)
                .Select(File.ReadAllText));

        AssertTrue(
            configLoaderSource.Contains("StrategicManagementContentConfigLoader", StringComparison.Ordinal) &&
            configLoaderSource.Contains("economy/resources.json", StringComparison.Ordinal) &&
            configLoaderSource.Contains("military/corps_common.json", StringComparison.Ordinal),
            "Strategic Management should use module-aware content loaders for foundation economy, buildings, and common corps");
        AssertTrue(
            !definitionsSource.Contains("new StrategicResourceDefinition", StringComparison.Ordinal) &&
            !definitionsSource.Contains("CommonCorps(", StringComparison.Ordinal),
            "first strategic definitions should not hardcode resources or common corps cost literals in C#");
        AssertTrue(
            !rulesSource.Contains("Conscription", StringComparison.Ordinal) &&
            typeof(StrategicManagementDefinitionSet).GetProperty("Conscription") == null,
            "Strategic Management should not retain retired conscription policy definitions or rules");

        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        AssertEqual("资金", definitions.Resources[StrategicManagementIds.ResourceMoney].DisplayName, "money display name should load from economy resource config");
        AssertEqual(30, definitions.Corps[StrategicManagementIds.CorpsShieldLine].SoldierCapacityCost, "shield corps soldier cost should load from common corps config");
        AssertEqual(
            45,
            FindStrategicAmount(definitions.Corps[StrategicManagementIds.CorpsCavalryLine].CreationCost, StrategicManagementIds.ResourceMoney),
            "cavalry corps money cost should load from common corps config");
    }

    internal static void StrategicManagementLoadsPassiveReserveRecoveryFromEconomyConfig()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        AssertEqual(
            2,
            definitions.ReserveRecoveryPerElapsedPulse,
            "first-version economy config should define passive reserve recovery at two soldiers per elapsed pulse");
    }

    internal static void StrategicManagementRejectsNonPositivePassiveReserveRecovery()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"rpg-strategic-recovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        string configPath = Path.Combine(tempRoot, "invalid_recovery.json");
        try
        {
            File.WriteAllText(
                configPath,
                """
                {
                  "reserveRecoveryPerElapsedPulse": 0,
                  "resources": [
                    { "resourceId": "resource_money", "displayName": "Money" }
                  ]
                }
                """);

            AssertThrowsInvalidOperation(
                () => StrategicManagementContentConfigLoader.LoadResourceEconomy(configPath),
                "passive reserve recovery must be positive");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    internal static void StrategicManagementConfigRejectsInvalidResourceAmountLists()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), $"rpg-strategic-config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        string emptyBuildCostPath = Path.Combine(tempRoot, "empty_build_cost.json");
        string duplicateBuildCostPath = Path.Combine(tempRoot, "duplicate_build_cost.json");
        try
        {
            File.WriteAllText(
                emptyBuildCostPath,
                """
                {
                  "buildings": [
                    {
                      "buildingDefinitionId": "building_test",
                      "iconPath": "res://icon.tres",
                      "displayName": "Test Building",
                      "categoryId": "building_category_test",
                      "footprintWidth": 1,
                      "footprintHeight": 1,
                      "buildCost": []
                    }
                  ]
                }
                """);
            File.WriteAllText(
                duplicateBuildCostPath,
                """
                {
                  "buildings": [
                    {
                      "buildingDefinitionId": "building_test",
                      "iconPath": "res://icon.tres",
                      "displayName": "Test Building",
                      "categoryId": "building_category_test",
                      "footprintWidth": 1,
                      "footprintHeight": 1,
                      "buildCost": [
                        { "resourceId": "resource_money", "amount": 10 },
                        { "resourceId": "resource_money", "amount": 5 }
                      ]
                    }
                  ]
                }
                """);
            AssertThrowsInvalidOperation(
                () => StrategicManagementBuildingDefinitionConfigLoader.LoadBuildings(emptyBuildCostPath),
                "empty build cost should be rejected because required cost lists must be explicit");
            AssertThrowsInvalidOperation(
                () => StrategicManagementBuildingDefinitionConfigLoader.LoadBuildings(duplicateBuildCostPath),
                "duplicate building build-cost resources should be rejected");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
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
        AssertTrue(state.Cities.ContainsKey(StrategicManagementIds.LocationQingheCore), "first core city should be initialized");
        AssertTrue(state.Heroes.ContainsKey(StrategicManagementIds.HeroCavalryCaptain), "first strategic heroes should be initialized");
        AssertTrue(
            typeof(StrategicCityState).GetProperty("Facilities") == null &&
            typeof(StrategicCityState).GetProperty("FacilitySlotCount") == null,
            "city state should not keep old facility-slot authority");
        AssertNoLegacyWorldReferences(typeof(StrategicManagementState));
    }

    internal static void StrategicManagementStateSavesAndLoadsFoundationCityMutations()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
        StrategicConstructionRegionDefinition economy = FindRegion(definitions, StrategicManagementIds.RegionPlainsEconomy);

        StrategicCommandResult build = commands.BuildCityBuilding(
            state,
            StrategicManagementIds.LocationQingheCore,
            StrategicManagementIds.BuildingFarm,
            StrategicManagementIds.RegionPlainsEconomy,
            economy.OriginX,
            economy.OriginY);
        AssertTrue(build.Success, $"setup build should succeed, got {build.FailureReason}");

        string savePath = Path.Combine(Path.GetTempPath(), $"rpg-strategic-management-save-{Guid.NewGuid():N}.json");
        try
        {
            StrategicManagementSaveService saveService = new();
            saveService.Save(state, savePath);
            AssertTrue(File.Exists(savePath), "strategic management save should write a JSON file");
            string json = File.ReadAllText(savePath);
            AssertTrue(
                json.Contains("\"version\"", StringComparison.OrdinalIgnoreCase) &&
                json.Contains(StrategicManagementIds.BuildingFarm, StringComparison.Ordinal) &&
                !json.Contains(".tres", StringComparison.Ordinal),
                "strategic management save should be versioned JSON state, not resource serialization");

            StrategicManagementState loaded = saveService.Load(savePath);
            StrategicCityState loadedCity = loaded.Cities[StrategicManagementIds.LocationQingheCore];
            StrategicBuildingInstanceState loadedBuilding = loadedCity.Buildings.Single();
            AssertEqual(StrategicManagementIds.BuildingFarm, loadedBuilding.BuildingDefinitionId, "loaded save should keep placed building definition");
            AssertEqual(economy.OriginX, loadedBuilding.GridX, "loaded save should keep building grid x");
            AssertEqual(economy.OriginY, loadedBuilding.GridY, "loaded save should keep building grid y");
            AssertEqual(
                state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceMoney),
                loaded.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceMoney),
                "loaded save should keep spent resource amounts");
            AssertEqual(state.NextBuildingSerial, loaded.NextBuildingSerial, "loaded save should keep next building serial");
        }
        finally
        {
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }
        }

        AssertTrue(
            typeof(StrategicManagementRuntime).GetMethod("SaveCurrentState") != null &&
            typeof(StrategicManagementRuntime).GetMethod("LoadSavedState") != null,
            "strategic management runtime should expose save/load entry points without reviving legacy world save service");
    }

    internal static void StrategicManagementLoadsVersionOneSaveWithRetiredConscriptionField()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
        StrategicConstructionRegionDefinition economy = FindRegion(definitions, StrategicManagementIds.RegionPlainsEconomy);
        StrategicCommandResult build = commands.BuildCityBuilding(
            state,
            StrategicManagementIds.LocationQingheCore,
            StrategicManagementIds.BuildingFarm,
            StrategicManagementIds.RegionPlainsEconomy,
            economy.OriginX,
            economy.OriginY);
        AssertTrue(build.Success, $"legacy-save setup build should succeed, got {build.FailureReason}");
        state.Cities[StrategicManagementIds.LocationQingheCore].ReserveForces = 73;
        int expectedCorpsCount = state.CorpsInstances.Count;

        string savePath = Path.Combine(Path.GetTempPath(), $"rpg-strategic-management-legacy-conscription-{Guid.NewGuid():N}.json");
        try
        {
            StrategicManagementSaveService saveService = new();
            saveService.Save(state, savePath);
            System.Text.Json.Nodes.JsonObject document =
                System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(savePath))?.AsObject()
                ?? throw new InvalidOperationException("version-one save should parse as a JSON object");
            System.Text.Json.Nodes.JsonObject city = document["State"]?["Cities"]?[StrategicManagementIds.LocationQingheCore]?.AsObject()
                ?? throw new InvalidOperationException("version-one save should contain the first city object");
            city["AutoConscriptionIntensityId"] = "conscription_high";
            File.WriteAllText(savePath, document.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

            StrategicManagementState loaded = saveService.Load(savePath);
            StrategicCityState loadedCity = loaded.Cities[StrategicManagementIds.LocationQingheCore];
            AssertEqual(73, loadedCity.ReserveForces, "legacy policy field should not disturb reserve soldiers");
            AssertEqual(StrategicManagementIds.BuildingFarm, loadedCity.Buildings.Single().BuildingDefinitionId, "legacy policy field should not disturb buildings");
            AssertEqual(expectedCorpsCount, loaded.CorpsInstances.Count, "legacy policy field should not disturb corps instances");
        }
        finally
        {
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }
        }
    }

    internal static void StrategicManagementRuntimeRepairsCapturedCityCompanyOwnershipOnLoad()
    {
        var setup = CreateStrategicAssaultExpedition();
        StrategicBattleBridgeService bridge = new(setup.Definitions);
        StrategicBattleSession session = bridge.CreateSession(
            setup.State,
            setup.ExpeditionId,
            "res://return_to_world.tscn",
            "res://scenes/world/sites/WorldSiteRoot.tscn").Session;
        StrategicBattleResultSummary summary = new()
        {
            SessionId = session.SessionId,
            ExpeditionId = setup.ExpeditionId,
            TargetLocationId = StrategicManagementIds.LocationChiyanHighBasin,
            Outcome = BattleOutcome.Victory,
            ObjectiveSucceeded = true
        };
        summary.Participants.Add(new StrategicBattleParticipantResult
        {
            HeroId = StrategicManagementIds.HeroOrdinaryCommander,
            CorpsInstanceId = setup.CorpsInstanceId,
            RemainingCorpsStrength = 72
        });
        CompleteDirectBattleResultSummaryForTest(summary, session);
        StrategicCommandResult applied = setup.Commands.ApplyBattleResultSummary(setup.State, summary);
        AssertTrue(applied.Success, $"setup victory should apply, got {applied.FailureReason}");
        setup.State.CorpsInstances[setup.CorpsInstanceId].HomeCityId = StrategicManagementIds.LocationQingheCore;

        string savePath = Path.Combine(Path.GetTempPath(), $"rpg-strategic-management-bad-company-home-{Guid.NewGuid():N}.json");
        try
        {
            new StrategicManagementSaveService().Save(setup.State, savePath);
            StrategicManagementRuntime.Reset();
            StrategicManagementRuntime.LoadSavedState(savePath);

            StrategicManagementState loaded = StrategicManagementRuntime.State;
            AssertEqual(
                StrategicManagementIds.LocationChiyanHighBasin,
                loaded.CorpsInstances[setup.CorpsInstanceId].HomeCityId,
                "runtime load should repair surviving resolved expedition corps into the captured managed city");
            StrategicManagementDashboardViewModel sourceDashboard = StrategicManagementRuntime.BuildDashboard(
                StrategicManagementIds.FactionPlayer,
                StrategicManagementIds.LocationQingheCore);
            StrategicManagementDashboardViewModel targetDashboard = StrategicManagementRuntime.BuildDashboard(
                StrategicManagementIds.FactionPlayer,
                StrategicManagementIds.LocationChiyanHighBasin);
            AssertTrue(
                !sourceDashboard.SelectedCity.HeroCompanies.Any(company => company.HeroId == StrategicManagementIds.HeroOrdinaryCommander),
                "repaired source city should not expose the captured expedition company");
            AssertEqual(1, targetDashboard.SelectedCity.HeroCompanies.Count, "repaired captured city should expose the surviving expedition company");
        }
        finally
        {
            StrategicManagementRuntime.Reset();
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }
        }
    }

    internal static void FirstCityInitializesConstructionRegionsReserveAndForceCapacity()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicCityState city = state.Cities[StrategicManagementIds.LocationQingheCore];

        AssertTrue(city.ConstructionRegionIds.Count >= 3, "first city should expose authored construction regions");
        AssertContains(city.ConstructionRegionIds, StrategicManagementIds.RegionPlainsEconomy, "first city should include an economy construction region");
        AssertContains(city.ConstructionRegionIds, StrategicManagementIds.RegionPlainsMilitary, "first city should include a military construction region");
        AssertTrue(typeof(StrategicConstructionRegionDefinition).GetProperty("AllowedCategoryIds") == null, "construction region definitions should not restrict building categories");
        AssertTrue(typeof(StrategicConstructionRegionViewModel).GetProperty("AllowedCategoryIds") == null, "construction region view models should not expose category restrictions");
        AssertEqual(10, FindRegion(definitions, StrategicManagementIds.RegionPlainsEconomy).OriginX, "economy region x should align with demo_site marker");
        AssertEqual(6, FindRegion(definitions, StrategicManagementIds.RegionPlainsEconomy).OriginY, "economy region y should align with demo_site marker");
        AssertEqual(21, FindRegion(definitions, StrategicManagementIds.RegionPlainsMilitary).OriginX, "military region x should align with demo_site marker");
        AssertEqual(18, FindRegion(definitions, StrategicManagementIds.RegionPlainsMilitary).OriginY, "military region y should align with demo_site marker");
        AssertEqual(12, FindRegion(definitions, StrategicManagementIds.RegionPlainsCivic).OriginX, "civic region x should align with demo_site marker");
        AssertEqual(28, FindRegion(definitions, StrategicManagementIds.RegionPlainsCivic).OriginY, "civic region y should align with demo_site marker");
        AssertEqual(220, city.CityForceCapacity, "first city should start with the accepted foundation force capacity");
        AssertEqual(80, city.ReserveForces, "first city should start with prepared reserve soldiers");
        AssertEqual(0, city.Buildings.Count, "first city should start with no placed city buildings");

        StrategicManagementRules rules = new(definitions);
        AssertEqual(100, rules.GetActiveForces(state, city.LocationId), "starting active forces should be derived from the three starting corps");
        AssertEqual(40, rules.GetRemainingCityForceCapacity(state, city.LocationId), "remaining capacity should be capacity minus active plus reserve");
    }

    internal static void StrategicManagementRetiresConscriptionPolicyContracts()
    {
        AssertTrue(
            typeof(StrategicCityState).GetProperty("AutoConscriptionIntensityId") == null,
            "city state must not persist a retired conscription policy");
        AssertTrue(
            typeof(StrategicManagementCommandService).GetMethod(
                "ManualConscriptReserveForces",
                new[] { typeof(StrategicManagementState), typeof(string) }) == null,
            "manual conscription must not remain as a command contract");
        AssertTrue(
            typeof(StrategicManagementCommandService).GetMethod(
                "SetAutoConscriptionIntensity",
                new[] { typeof(StrategicManagementState), typeof(string), typeof(string) }) == null,
            "automatic conscription must not remain as a command contract");
    }

    internal static void FirstPlayableStartsWithThreeDispatchableHeroCompanies()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementViewModelService viewModels = new(definitions, new StrategicManagementRules(definitions));

        StrategicManagementDashboardViewModel dashboard = viewModels.BuildDashboard(
            state,
            StrategicManagementIds.FactionPlayer,
            StrategicManagementIds.LocationQingheCore);

        AssertEqual(3, dashboard.Heroes.Count, "first playable should expose three strategic heroes");
        AssertEqual(3, dashboard.SelectedCity.HeroCompanies.Count, "first playable should start with three dispatchable battle groups");
        AssertTrue(dashboard.SelectedCity.HeroCompanies.All(company => company.CanCreateExpedition), "all starting battle groups should be dispatchable");
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

    internal static void StrategicManagementDashboardCarriesMilitaryPreviewBattleUnitIds()
    {
        string root = ProjectRoot();
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementDashboardViewModel dashboard = new StrategicManagementViewModelService(
                definitions,
                new StrategicManagementRules(definitions))
            .BuildDashboard(
                state,
                StrategicManagementIds.FactionPlayer,
                StrategicManagementIds.LocationQingheCore);

        AssertTrue(
            typeof(StrategicHeroDefinition).GetProperty("BattleUnitId") != null &&
            typeof(StrategicHeroDefinition).GetProperty("IconPath") == null &&
            typeof(StrategicCorpsDefinition).GetProperty("BattleUnitId") != null &&
            typeof(StrategicCorpsDefinition).GetProperty("IconPath") == null &&
            typeof(StrategicHeroAssignmentViewModel).GetProperty("BattleUnitId") != null &&
            typeof(StrategicHeroAssignmentViewModel).GetProperty("IconPath") == null &&
            typeof(StrategicMusterTemplateViewModel).GetProperty("BattleUnitId") != null &&
            typeof(StrategicMusterTemplateViewModel).GetProperty("IconPath") == null &&
            typeof(StrategicCorpsInstanceViewModel).GetProperty("BattleUnitId") != null &&
            typeof(StrategicCorpsInstanceViewModel).GetProperty("IconPath") == null &&
            typeof(StrategicHeroCompanyViewModel).GetProperty("HeroBattleUnitId") != null &&
            typeof(StrategicHeroCompanyViewModel).GetProperty("CorpsBattleUnitId") != null,
            "military definitions and view models should carry battle unit ids only; raw PNG icon paths are not a military preview contract");

        IReadOnlyDictionary<string, string> unitDefinitionIndex = BattleUnitDefinitionIndexLoader.LoadDefaultPathIndex();
        var expectedHeroUnitIds = new Dictionary<string, string>
        {
            [StrategicManagementIds.HeroOrdinaryCommander] = FirstSliceHeroCompanyIds.ShieldHeroUnit,
            [StrategicManagementIds.HeroArcherCaptain] = FirstSliceHeroCompanyIds.ArcherHeroUnit,
            [StrategicManagementIds.HeroCavalryCaptain] = FirstSliceHeroCompanyIds.AssaultHeroUnit
        };
        foreach ((string heroId, string expectedUnitId) in expectedHeroUnitIds)
        {
            StrategicHeroAssignmentViewModel hero = FindHero(dashboard, heroId);
            StrategicHeroDefinition definition = definitions.Heroes[hero.HeroDefinitionId];
            AssertEqual(expectedUnitId, definition.BattleUnitId, $"hero definition should reference the battle unit id hero={heroId}");
            AssertEqual(expectedUnitId, GetRequiredProperty<string>(hero, "BattleUnitId"), $"hero dashboard card should expose the preview battle unit id hero={heroId}");
            AssertPreviewUnitResourceExists(root, unitDefinitionIndex, expectedUnitId, $"hero preview unit should resolve through the battle unit index hero={heroId}");
            AssertTrue(
                dashboard.SelectedCity.HeroCompanies.Any(company =>
                    company.HeroId == heroId &&
                    GetRequiredProperty<string>(company, "HeroBattleUnitId") == expectedUnitId),
                $"hero company view model should expose hero preview unit id hero={heroId}");
        }

        var expectedCorpsUnitIds = new Dictionary<string, string>
        {
            [StrategicManagementIds.CorpsShieldLine] = FirstSliceHeroCompanyIds.ShieldCorpsUnit,
            [StrategicManagementIds.CorpsArcherLine] = FirstSliceHeroCompanyIds.ArcherCorpsUnit,
            [StrategicManagementIds.CorpsCavalryLine] = FirstSliceHeroCompanyIds.AssaultCorpsUnit
        };
        foreach ((string corpsDefinitionId, string expectedUnitId) in expectedCorpsUnitIds)
        {
            StrategicMusterTemplateViewModel template = FindMusterTemplate(dashboard, corpsDefinitionId);
            StrategicCorpsInstanceViewModel corps = dashboard.SelectedCity.CorpsInstances
                .First(item => item.CorpsDefinitionId == corpsDefinitionId);
            AssertEqual(expectedUnitId, definitions.Corps[corpsDefinitionId].BattleUnitId, $"corps definition should reference the battle unit id id={corpsDefinitionId}");
            AssertEqual(expectedUnitId, GetRequiredProperty<string>(template, "BattleUnitId"), $"muster template should expose preview battle unit id={corpsDefinitionId}");
            AssertEqual(expectedUnitId, GetRequiredProperty<string>(corps, "BattleUnitId"), $"corps row should expose preview battle unit id={corpsDefinitionId}");
            AssertPreviewUnitResourceExists(root, unitDefinitionIndex, expectedUnitId, $"corps preview unit should resolve through the battle unit index id={corpsDefinitionId}");
            AssertTrue(
                dashboard.SelectedCity.HeroCompanies.Any(company =>
                    company.CorpsDefinitionId == corpsDefinitionId &&
                    GetRequiredProperty<string>(company, "CorpsBattleUnitId") == expectedUnitId),
                $"hero company view model should expose corps preview unit id id={corpsDefinitionId}");
        }
    }

    internal static void TemporaryLegacySiteAdapterResolvesOnlyRetainedFixedIds()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();

        AssertTrue(
            TemporaryLegacyStrategicSiteIdentityAdapter.TryResolveLocationId(
                TemporaryLegacyStrategicSiteIdentityAdapter.LegacyPlayerCampSiteId,
                out string playerCampLocationId),
            "player camp map site should resolve to a strategic location");
        AssertEqual(
            StrategicManagementIds.LocationQingheCore,
            playerCampLocationId,
            "player camp should map to the first managed city");
        AssertTrue(
            TemporaryLegacyStrategicSiteIdentityAdapter.TryResolveCityId(
                TemporaryLegacyStrategicSiteIdentityAdapter.LegacyPlayerCampSiteId,
                out string playerCampCityId),
            "player camp should resolve to a managed city");
        AssertEqual(
            StrategicManagementIds.LocationQingheCore,
            playerCampCityId,
            "player camp city mapping should be the plains city");

        AssertTrue(
            TemporaryLegacyStrategicSiteIdentityAdapter.TryResolveLocationId(
                TemporaryLegacyStrategicSiteIdentityAdapter.LegacyBonefieldSiteId,
                out string bonefieldLocationId),
            "bonefield map site should resolve to a strategic location");
        AssertEqual(
            StrategicManagementIds.LocationChiyanHighBasin,
            bonefieldLocationId,
            "bonefield should map to the first hostile foundation target");
        AssertTrue(
            TemporaryLegacyStrategicSiteIdentityAdapter.TryResolveCityId(
                TemporaryLegacyStrategicSiteIdentityAdapter.LegacyBonefieldSiteId,
                out string bonefieldCityId),
            "Bonefield is the first hostile managed stronghold and should resolve through the city mapping gate");
        AssertEqual(
            StrategicManagementIds.LocationChiyanHighBasin,
            bonefieldCityId,
            "Bonefield city mapping should preserve the hostile target location id");

        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        AssertTrue(
            state.Cities.ContainsKey(StrategicManagementIds.LocationChiyanHighBasin),
            "Bonefield should start with isolated managed-city state even before the player captures it");

        AssertTrue(
            !TemporaryLegacyStrategicSiteIdentityAdapter.TryResolveLocationId("unknown_site", out string unknownLocationId),
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
                StrategicManagementIds.LocationQingheCore);

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
            StrategicManagementIds.LocationQingheCore,
            StrategicManagementIds.LocationChiyanHighBasin,
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
