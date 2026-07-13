using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rpg.Application.StrategicManagement;
using Rpg.Application.StrategicMap;
using Rpg.Application.World;
using Rpg.Definitions.StrategicManagement;
using Rpg.Definitions.StrategicMap;
using Rpg.Domain.StrategicManagement;

internal static partial class StrategicManagementRegressionCases
{
    private const string LegacyPlainsCityFixtureId = "location_plains_city";
    private const string LegacyBonefieldFixtureId = "location_bonefield_outpost";

    internal static void StrategicManagementConvergesAllCanonicalCityControlsWithoutAuxiliaryContent()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);

        AssertEqual(11, definitions.CanonicalGeography.Cities.Count, "canonical city convergence count");
        AssertEqual(11, definitions.Locations.Values.Count(location => location.Kind == StrategicLocationKind.City), "strategic city definition count");
        AssertEqual(11, definitions.CanonicalGeography.Cities.Keys.Count(state.Locations.ContainsKey), "canonical city control count");
        AssertEqual(5, definitions.CanonicalGeography.Cities.Values.Count(city => city.ProvinceId == "qinghe"), "Qinghe member count");
        AssertEqual(6, definitions.CanonicalGeography.Cities.Values.Count(city => city.ProvinceId == "chiyan"), "Chiyan member count");
        AssertEqual(2, state.Cities.Count, "only retained main-city management content should initialize city state");

        foreach (StrategicManagementCityReference city in definitions.CanonicalGeography.Cities.Values)
        {
            StrategicLocationState control = state.Locations[city.LocationId];
            AssertEqual(city.LocationId, control.LocationId, $"control record identity location={city.LocationId}");
            AssertEqual(city.ProvinceId == "qinghe" ? "qinghe_layout" : "chiyan_layout", city.LayoutId, $"province layout lineage location={city.LocationId}");
            AssertEqual(
                city.ProvinceId == "qinghe" ? StrategicManagementIds.FactionPlayer : StrategicManagementIds.FactionEnemy,
                control.OwnerFactionId,
                $"initial owner comes from accepted campaign role location={city.LocationId}");

            Rpg.Definitions.StrategicManagement.StrategicLocationDefinition definition = definitions.Locations[city.LocationId];
            if (city.LocationType == StrategicLocationType.AuxiliaryCity)
            {
                AssertEqual("", definition.DisplayName, $"auxiliary display name remains unassigned location={city.LocationId}");
                AssertEqual("", definition.CityIdentityId, $"auxiliary city identity remains unassigned location={city.LocationId}");
                AssertEqual(0, definition.ConstructionRegions.Count, $"auxiliary construction content remains unassigned location={city.LocationId}");
                AssertEqual("", definition.BattleEncounterId, $"auxiliary battle content remains unassigned location={city.LocationId}");
                AssertEqual(0, definition.ProductionPerWorldTimePulse.Count, $"auxiliary production remains unassigned location={city.LocationId}");
                AssertTrue(!state.Cities.ContainsKey(city.LocationId), $"auxiliary city must not receive cloned management state location={city.LocationId}");
            }
        }

        AssertTrue(definitions.Locations.ContainsKey(StrategicManagementIds.LocationTimberSite), "retained timber content identity remains defined");
        AssertTrue(!definitions.CanonicalGeography.Cities.ContainsKey(StrategicManagementIds.LocationTimberSite), "timber content identity is not fabricated into canonical city geography");
        new StrategicManagementGeographyInvariantService().ThrowIfInvalid(definitions, state, "regression");
    }

    internal static void StrategicManagementRejectsInvalidCanonicalIdentityLineageWithStableIds()
    {
        string projectRoot = ProjectRoot();
        StrategicMapPackageSelection selection = StrategicMapPackageLoader.LoadSelection(
            projectRoot,
            "res://config/world/strategic-map-selection.json");
        StrategicMapCanonicalDefinition canonical = StrategicMapPackageLoader.LoadSelected(projectRoot, selection).Canonical;

        StrategicManagementDefinitionSet missingDefinitions = FirstStrategicManagementDefinitions.Create(canonical);
        missingDefinitions.Locations.Remove("qinghe_fan");
        InvalidOperationException missingDefinition = CaptureInvalidOperation(() =>
            StrategicManagementGeographyConvergenceService.Converge(missingDefinitions, canonical));
        AssertTrue(
            missingDefinition.Message.Contains("ProvinceId=qinghe LocationId=qinghe_fan LayoutId=qinghe_layout", StringComparison.Ordinal),
            $"missing definition failure should preserve canonical lineage message={missingDefinition.Message}");

        StrategicManagementDefinitionSet extraDefinitions = FirstStrategicManagementDefinitions.Create(canonical);
        extraDefinitions.Locations.Add("rogue_city", new Rpg.Definitions.StrategicManagement.StrategicLocationDefinition
        {
            LocationId = "rogue_city",
            Kind = StrategicLocationKind.City
        });
        InvalidOperationException extraDefinition = CaptureInvalidOperation(() =>
            StrategicManagementGeographyConvergenceService.Converge(extraDefinitions, canonical));
        AssertTrue(
            extraDefinition.Message.Contains("ProvinceId=<missing> LocationId=rogue_city LayoutId=<missing>", StringComparison.Ordinal),
            $"extra definition failure should preserve offending identity message={extraDefinition.Message}");

        StrategicManagementDefinitionSet stateDefinitions = FirstStrategicManagementDefinitions.Create(canonical);
        StrategicManagementState missingControlState = FirstStrategicManagementStateFactory.CreatePlayerStart(stateDefinitions);
        missingControlState.Locations.Remove(StrategicManagementIds.LocationQingheCore);
        InvalidDataException missingControl = CaptureInvalidData(() =>
            new StrategicManagementGeographyInvariantService().ThrowIfInvalid(stateDefinitions, missingControlState, "regression-missing"));
        AssertTrue(
            missingControl.Message.Contains("reason=missing-control ProvinceId=qinghe LocationId=qinghe_core LayoutId=qinghe_layout", StringComparison.Ordinal),
            $"missing control failure should preserve canonical lineage message={missingControl.Message}");

        StrategicManagementState extraControlState = FirstStrategicManagementStateFactory.CreatePlayerStart(stateDefinitions);
        extraControlState.Locations.Add("rogue_control", new StrategicLocationState { LocationId = "rogue_control" });
        InvalidDataException extraControl = CaptureInvalidData(() =>
            new StrategicManagementGeographyInvariantService().ThrowIfInvalid(stateDefinitions, extraControlState, "regression-extra"));
        AssertTrue(
            extraControl.Message.Contains("reason=extra-control ProvinceId=<unknown> LocationId=rogue_control LayoutId=<unknown>", StringComparison.Ordinal),
            $"extra control failure should preserve offending identity message={extraControl.Message}");

        StrategicMapCanonicalDefinition emptyLayout = canonical with
        {
            Geography = canonical.Geography with
            {
                Provinces = canonical.Geography.Provinces
                    .Select(province => province.ProvinceId == "qinghe" ? province with { LayoutId = "" } : province)
                    .ToArray()
            }
        };
        InvalidOperationException layoutFailure = CaptureInvalidOperation(() =>
            FirstStrategicManagementDefinitions.Create(emptyLayout));
        AssertTrue(
            layoutFailure.Message.Contains("ProvinceId=qinghe LocationId=qinghe_core LayoutId=<empty>", StringComparison.Ordinal),
            $"empty layout failure should preserve province and location ids message={layoutFailure.Message}");

        StrategicMapCanonicalDefinition crossProvince = canonical with
        {
            Geography = canonical.Geography with
            {
                Locations = canonical.Geography.Locations
                    .Select(location => location.LocationId == StrategicManagementIds.LocationQingheCore
                        ? location with { ProvinceId = "chiyan" }
                        : location)
                    .ToArray()
            }
        };
        InvalidOperationException crossProvinceFailure = CaptureInvalidOperation(() =>
            FirstStrategicManagementDefinitions.Create(crossProvince));
        AssertTrue(
            crossProvinceFailure.Message.Contains("LOCATION_GEOMETRY_PROVINCE_MISMATCH:qinghe_core", StringComparison.Ordinal),
            $"cross-province failure should preserve the offending LocationId message={crossProvinceFailure.Message}");

        Rpg.Definitions.StrategicMap.StrategicLocationDefinition duplicate = canonical.Geography.Locations
            .Single(location => location.LocationId == StrategicManagementIds.LocationQingheCore);
        StrategicMapCanonicalDefinition ambiguous = canonical with
        {
            Geography = canonical.Geography with
            {
                Locations = canonical.Geography.Locations.Append(duplicate).ToArray()
            }
        };
        InvalidOperationException ambiguousFailure = CaptureInvalidOperation(() =>
            FirstStrategicManagementDefinitions.Create(ambiguous));
        AssertTrue(
            ambiguousFailure.Message.Contains("LOCATION_ID_DUPLICATE:qinghe_core", StringComparison.Ordinal),
            $"ambiguous identity failure should preserve the duplicate LocationId message={ambiguousFailure.Message}");
    }

    internal static void StrategicMapCampaignPortExposesImmutableCanonicalControlLineage()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        IStrategicMapCampaignPresentationPort port = new StrategicManagementCampaignPresentationPort(definitions, state);
        StrategicMapCampaignPresentationView snapshot = port.Read();

        AssertEqual(2, snapshot.Provinces.Count, "campaign port province count");
        AssertEqual(11, snapshot.Locations.Count, "campaign port location count");
        StrategicMapLocationControlView qinghe = snapshot.Locations.Single(location => location.LocationId == StrategicManagementIds.LocationQingheCore);
        StrategicMapLocationControlView chiyan = snapshot.Locations.Single(location => location.LocationId == StrategicManagementIds.LocationChiyanHighBasin);
        AssertEqual("qinghe", qinghe.ProvinceId, "Qinghe province lineage");
        AssertEqual("qinghe_layout", qinghe.LayoutId, "Qinghe layout lineage");
        AssertEqual(StrategicMapCampaignControl.Player, qinghe.Control, "Qinghe initial player control");
        AssertEqual("chiyan", chiyan.ProvinceId, "Chiyan province lineage");
        AssertEqual("chiyan_layout", chiyan.LayoutId, "Chiyan layout lineage");
        AssertEqual(StrategicMapCampaignControl.Enemy, chiyan.Control, "Chiyan initial enemy control");
        AssertTrue(((IList)snapshot.Locations).IsReadOnly, "location snapshot collection must be read-only");
        AssertTrue(((IList)snapshot.Provinces).IsReadOnly, "province snapshot collection must be read-only");

        state.Locations[StrategicManagementIds.LocationQingheCore].OwnerFactionId = StrategicManagementIds.FactionEnemy;
        state.Locations[StrategicManagementIds.LocationQingheCore].ControlState = StrategicLocationControlState.EnemyHeld;
        AssertEqual(StrategicMapCampaignControl.Player, qinghe.Control, "previous immutable snapshot must not mirror later state mutation");
        AssertEqual(
            StrategicMapCampaignControl.Enemy,
            port.Read().Locations.Single(location => location.LocationId == StrategicManagementIds.LocationQingheCore).Control,
            "a new port read should observe current Strategic Management authority");

        AssertTrue(typeof(Rpg.Definitions.StrategicManagement.StrategicLocationDefinition).GetProperty("MapSiteId") == null, "canonical strategic definitions must not embed legacy map-site mapping");
        AssertTrue(typeof(StrategicLocationDashboardViewModel).GetProperty("MapSiteId") == null, "canonical strategic views must not leak legacy map-site mapping");
    }

    internal static void StrategicScenarioOwnsCampaignStartsAndSupportsReplacementPackage()
    {
        StrategicManagementDefinitionSet mock = FirstStrategicManagementDefinitions.Create();
        AssertEqual("mock_qinghe_chiyan", mock.ContentIdentity.MapId, "mock MapId identity");
        AssertEqual("mock_qinghe_chiyan_campaign", mock.ContentIdentity.ScenarioId, "mock ScenarioId identity");
        AssertEqual(StrategicScenarioProvinceRole.PlayerStart, mock.CanonicalGeography.Provinces["qinghe"].ScenarioRole, "Qinghe scenario role");
        AssertEqual(StrategicScenarioProvinceRole.FirstHostile, mock.CanonicalGeography.Provinces["chiyan"].ScenarioRole, "Chiyan scenario role");
        AssertTrue(typeof(ProvinceDefinition).GetProperty("CampaignRole") == null, "map geography has no campaign role property");

        StrategicManagementDefinitionSet fixture = FirstStrategicManagementDefinitions.CreateFromSelection(
            "res://config/world/strategic-map-selection-fixture.json");
        StrategicManagementState fixtureState = FirstStrategicManagementStateFactory.CreatePlayerStart(fixture);
        AssertEqual("fixture_north_pass", fixture.ContentIdentity.MapId, "fixture MapId identity");
        AssertEqual("fixture_north_pass_campaign", fixture.ContentIdentity.ScenarioId, "fixture ScenarioId identity");
        AssertEqual(2, fixture.CanonicalGeography.Cities.Count, "fixture city composition");
        AssertEqual(2, fixture.CanonicalGeography.Cities.Keys.Count(fixtureState.Locations.ContainsKey), "fixture scenario initializes every canonical city");
        AssertTrue(!fixture.Locations.ContainsKey(StrategicManagementIds.LocationQingheCore), "fixture definitions do not retain mock city identity");
        AssertTrue(fixtureState.Locations.Values.All(location => location.ControlState == StrategicLocationControlState.PlayerHeld), "fixture scenario owns player-held start facts");
    }

    internal static void StrategicScenarioRejectsInvalidAssignmentsBeforeStateCreation()
    {
        string projectRoot = ProjectRoot();
        StrategicMapPackageSelection selection = StrategicMapPackageLoader.LoadSelection(
            projectRoot,
            FirstStrategicManagementDefinitions.DefaultSelectionPath);
        StrategicMapLoadedContext context = StrategicMapPackageLoader.LoadSelected(projectRoot, selection);
        StrategicManagementScenarioDefinition valid = StrategicManagementScenarioLoader.LoadSelected(
            projectRoot,
            selection.ScenarioPath,
            context.Package,
            context.Canonical);
        StrategicManagementContentIdentity identity = new(
            context.Package.MapId,
            valid.ScenarioId,
            context.Package.CompatibilityRevision,
            valid.ScenarioContentRevision);

        AssertScenarioRejectedBeforeState(
            context.Canonical,
            valid with
            {
                Locations = valid.Locations.Append(new StrategicScenarioLocationStart(
                    "unknown_location",
                    StrategicManagementIds.FactionPlayer,
                    StrategicScenarioControl.PlayerHeld)).ToArray()
            },
            identity,
            "unknown LocationId");
        AssertScenarioRejectedBeforeState(
            context.Canonical,
            valid with { Locations = valid.Locations.Append(valid.Locations[0]).ToArray() },
            identity,
            "duplicate LocationId");
        AssertScenarioRejectedBeforeState(
            context.Canonical,
            valid with
            {
                Locations = valid.Locations.Append(new StrategicScenarioLocationStart(
                    StrategicManagementIds.LocationQingheCore,
                    StrategicManagementIds.FactionPlayer,
                    StrategicScenarioControl.PlayerHeld)).ToArray()
            },
            identity,
            "both province and location");
        AssertScenarioRejectedBeforeState(
            context.Canonical,
            valid with { Provinces = valid.Provinces.Append(valid.Provinces[0]).ToArray() },
            identity,
            "duplicate ProvinceId");
        AssertScenarioRejectedBeforeState(
            context.Canonical,
            valid with { Resources = valid.Resources.Append(valid.Resources[0]).ToArray() },
            identity,
            "duplicate resource assignment");
    }

    private static void AssertScenarioRejectedBeforeState(
        StrategicMapCanonicalDefinition canonical,
        StrategicManagementScenarioDefinition scenario,
        StrategicManagementContentIdentity identity,
        string expectedMessage)
    {
        StrategicManagementState? createdState = null;
        try
        {
            StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create(canonical, scenario, identity);
            createdState = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains(expectedMessage, StringComparison.Ordinal))
        {
            AssertTrue(createdState == null, $"invalid scenario must not create partial state expected={expectedMessage}");
            return;
        }

        throw new InvalidOperationException($"invalid scenario should fail before state creation expected={expectedMessage}");
    }

    internal static void StrategicSaveVersionFiveValidatesContentIdentityAndMigratesOnlyMockV4()
    {
        StrategicManagementDefinitionSet mock = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState source = FirstStrategicManagementStateFactory.CreatePlayerStart(mock);
        StrategicManagementSaveService saves = new(mock);
        string path = Path.Combine(Path.GetTempPath(), $"rpg-stage25-save-identity-{Guid.NewGuid():N}.json");
        string sourceBefore = JsonSerializer.Serialize(source);
        try
        {
            saves.Save(source, path);
            JsonObject current = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            AssertEqual(5, current["Version"]!.GetValue<int>(), "current save version");
            AssertEqual(mock.ContentIdentity.MapId, current["MapId"]!.GetValue<string>(), "saved MapId");
            AssertEqual(mock.ContentIdentity.ScenarioId, current["ScenarioId"]!.GetValue<string>(), "saved ScenarioId");

            JsonObject versionFour = current.DeepClone().AsObject();
            versionFour["Version"] = 4;
            versionFour.Remove("MapId");
            versionFour.Remove("ScenarioId");
            versionFour.Remove("PackageCompatibilityRevision");
            versionFour.Remove("ScenarioContentRevision");
            File.WriteAllText(path, versionFour.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            StrategicManagementState migrated = saves.Load(path);
            AssertEqual(source.Locations.Count, migrated.Locations.Count, "v4 mock migration state count");

            JsonObject mismatch = current.DeepClone().AsObject();
            mismatch["MapId"] = "fixture_north_pass";
            File.WriteAllText(path, mismatch.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            InvalidOperationException mismatchFailure = CaptureInvalidOperation(() => saves.Load(path));
            AssertTrue(mismatchFailure.ToString().Contains("content identity mismatch", StringComparison.Ordinal), "v5 MapId mismatch fails explicitly");

            File.WriteAllText(path, versionFour.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            StrategicManagementDefinitionSet fixture = FirstStrategicManagementDefinitions.CreateFromSelection(
                "res://config/world/strategic-map-selection-fixture.json");
            InvalidOperationException migrationFailure = CaptureInvalidOperation(() => new StrategicManagementSaveService(fixture).Load(path));
            AssertTrue(migrationFailure.ToString().Contains("only to the explicit mock identity", StringComparison.Ordinal), "v4 cannot migrate into replacement map identity");
            AssertEqual(sourceBefore, JsonSerializer.Serialize(source), "failed identity loads do not mutate live source state");
        }
        finally
        {
            DeleteSaveFamily(path);
        }
    }

    internal static void StrategicSaveMigratesLegacyCityIdentitiesAtomically()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
        StrategicCommandResult expedition = commands.CreateExpedition(
            state,
            StrategicManagementIds.LocationQingheCore,
            StrategicManagementIds.LocationChiyanHighBasin,
            StrategicExpeditionIntent.AssaultLocation,
            StrategicManagementIds.HeroOrdinaryCommander);
        AssertTrue(expedition.Success, $"migration fixture expedition should be valid reason={expedition.FailureReason}");
        state.BattleFeedbackRecords["feedback_migration_fixture"] = new StrategicBattleFeedbackRecord
        {
            FeedbackId = "feedback_migration_fixture",
            ExpeditionId = expedition.CreatedEntityId,
            TargetLocationId = StrategicManagementIds.LocationChiyanHighBasin
        };

        StrategicManagementSaveService saves = new(definitions);
        string path = Path.Combine(Path.GetTempPath(), $"rpg-stage2-identity-migration-{Guid.NewGuid():N}.json");
        try
        {
            JsonObject versionThree = BuildVersionThreeLegacyIdentityDocument(saves, state, path);
            File.WriteAllText(path, versionThree.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            StrategicManagementState migrated = saves.Load(path);

            AssertEqual(11, definitions.CanonicalGeography.Cities.Keys.Count(migrated.Locations.ContainsKey), "migration initializes every canonical city control");
            AssertTrue(!migrated.Locations.ContainsKey(LegacyPlainsCityFixtureId), "legacy plains key removed");
            AssertTrue(!migrated.Locations.ContainsKey(LegacyBonefieldFixtureId), "legacy Bonefield key removed");
            AssertTrue(migrated.Cities.ContainsKey(StrategicManagementIds.LocationQingheCore), "Qinghe city key migrated");
            AssertTrue(migrated.Cities.ContainsKey(StrategicManagementIds.LocationChiyanHighBasin), "Chiyan city key migrated");
            StrategicExpeditionState migratedExpedition = migrated.Expeditions[expedition.CreatedEntityId];
            AssertEqual(StrategicManagementIds.LocationQingheCore, migratedExpedition.SourceLocationId, "expedition source migrated");
            AssertEqual(StrategicManagementIds.LocationChiyanHighBasin, migratedExpedition.TargetLocationId, "expedition target migrated");
            AssertEqual(StrategicManagementIds.LocationQingheCore, migratedExpedition.Participants[0].RollbackStationLocationId, "rollback station migrated");
            AssertEqual(StrategicManagementIds.LocationChiyanHighBasin, migrated.BattleFeedbackRecords["feedback_migration_fixture"].TargetLocationId, "feedback target migrated");
            AssertTrue(migrated.CorpsInstances.Values.All(corps => corps.HomeCityId != LegacyPlainsCityFixtureId && corps.HomeCityId != LegacyBonefieldFixtureId), "corps home references migrated");
        }
        finally
        {
            DeleteSaveFamily(path);
        }
    }

    internal static void StrategicSaveRejectsIdentityCollisionsPartialGraphsAndCurrentLegacyIds()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementSaveService saves = new(definitions);
        string seedPath = Path.Combine(Path.GetTempPath(), $"rpg-stage2-identity-seed-{Guid.NewGuid():N}.json");
        string before = JsonSerializer.Serialize(state);
        try
        {
            JsonObject legacy = BuildVersionThreeLegacyIdentityDocument(saves, state, seedPath);

            JsonObject collision = legacy.DeepClone().AsObject();
            JsonObject collisionLocations = collision["State"]!["Locations"]!.AsObject();
            JsonObject canonicalCollision = collisionLocations[LegacyPlainsCityFixtureId]!.DeepClone().AsObject();
            canonicalCollision["LocationId"] = StrategicManagementIds.LocationQingheCore;
            collisionLocations[StrategicManagementIds.LocationQingheCore] = canonicalCollision;
            AssertIdentityDocumentRejected(saves, collision, "collision");

            JsonObject mismatch = legacy.DeepClone().AsObject();
            mismatch["State"]!["Locations"]![LegacyPlainsCityFixtureId]!["LocationId"] = "mismatched_location";
            AssertIdentityDocumentRejected(saves, mismatch, "key-value-mismatch");

            JsonObject incomplete = legacy.DeepClone().AsObject();
            incomplete["State"]!["Locations"]!.AsObject().Remove(LegacyBonefieldFixtureId);
            AssertIdentityDocumentRejected(saves, incomplete, "incomplete-graph");

            JsonObject currentLegacy = legacy.DeepClone().AsObject();
            currentLegacy["Version"] = StrategicManagementSaveService.CurrentVersion;
            AssertIdentityDocumentRejected(saves, currentLegacy, "current-version-legacy-id");

            AssertEqual(before, JsonSerializer.Serialize(state), "failed candidate loads must not mutate or publish the source state");
        }
        finally
        {
            DeleteSaveFamily(seedPath);
        }
    }

    internal static void TemporaryLegacySiteAdapterIsIsolatedFromCanonicalModules()
    {
        string root = ProjectRoot();
        string adapterPath = Path.Combine(root, "src", "Application", "World", "TemporaryLegacyStrategicSiteIdentityAdapter.cs");
        string[] legacyIdOwners = Directory.GetFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path =>
            {
                string text = File.ReadAllText(path);
                bool hasLegacySite = text.Contains("\"player_camp\"", StringComparison.Ordinal) ||
                                     text.Contains("\"bonefield\"", StringComparison.Ordinal);
                bool hasCanonicalCity = text.Contains("qinghe_core", StringComparison.Ordinal) ||
                                        text.Contains("chiyan_high_basin", StringComparison.Ordinal) ||
                                        text.Contains("LocationQingheCore", StringComparison.Ordinal) ||
                                        text.Contains("LocationChiyanHighBasin", StringComparison.Ordinal);
                return hasLegacySite && hasCanonicalCity;
            })
            .ToArray();
        AssertEqual(1, legacyIdOwners.Length, "legacy-to-canonical site translation should have one source owner");
        AssertEqual(Path.GetFullPath(adapterPath), Path.GetFullPath(legacyIdOwners[0]), "legacy-to-canonical site translation belongs only to the temporary adapter");

        foreach (string canonicalRoot in new[]
                 {
                     Path.Combine(root, "src", "Definitions", "StrategicMap"),
                     Path.Combine(root, "src", "Application", "StrategicMap"),
                     Path.Combine(root, "src", "Presentation", "StrategicMap"),
                     Path.Combine(root, "scenes", "world", "strategic_map"),
                     Path.Combine(root, "resource", "world", "strategic_map")
                 })
        {
            foreach (string path in Directory.GetFiles(canonicalRoot, "*", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(path);
                AssertTrue(!text.Contains("player_camp", StringComparison.Ordinal) && !text.Contains("bonefield", StringComparison.Ordinal), $"replacement map contains legacy id path={path}");
            }
        }
    }

    private static JsonObject BuildVersionThreeLegacyIdentityDocument(
        StrategicManagementSaveService saves,
        StrategicManagementState state,
        string path)
    {
        DeleteSaveFamily(path);
        saves.Save(state, path);
        JsonObject document = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        document["Version"] = 3;
        JsonObject stateNode = document["State"]!.AsObject();
        JsonObject locations = stateNode["Locations"]!.AsObject();
        foreach (string canonicalCityId in state.Locations.Keys
                     .Where(id => id != StrategicManagementIds.LocationQingheCore && id != StrategicManagementIds.LocationChiyanHighBasin && id != StrategicManagementIds.LocationTimberSite)
                     .ToArray())
        {
            locations.Remove(canonicalCityId);
        }
        RenameLocationRecord(locations, StrategicManagementIds.LocationQingheCore, LegacyPlainsCityFixtureId);
        RenameLocationRecord(locations, StrategicManagementIds.LocationChiyanHighBasin, LegacyBonefieldFixtureId);
        JsonObject cities = stateNode["Cities"]!.AsObject();
        RenameLocationRecord(cities, StrategicManagementIds.LocationQingheCore, LegacyPlainsCityFixtureId);
        RenameLocationRecord(cities, StrategicManagementIds.LocationChiyanHighBasin, LegacyBonefieldFixtureId);

        foreach ((_, JsonNode? corpsNode) in stateNode["CorpsInstances"]!.AsObject())
        {
            ReplaceNodeLocation(corpsNode!.AsObject(), "HomeCityId");
        }
        foreach ((_, JsonNode? expeditionNode) in stateNode["Expeditions"]!.AsObject())
        {
            JsonObject expedition = expeditionNode!.AsObject();
            ReplaceNodeLocation(expedition, "SourceLocationId");
            ReplaceNodeLocation(expedition, "TargetLocationId");
            foreach (JsonNode? participantNode in expedition["Participants"]!.AsArray())
            {
                ReplaceNodeLocation(participantNode!.AsObject(), "RollbackStationLocationId");
            }
        }
        foreach ((_, JsonNode? feedbackNode) in stateNode["BattleFeedbackRecords"]!.AsObject())
        {
            ReplaceNodeLocation(feedbackNode!.AsObject(), "TargetLocationId");
        }
        return document;
    }

    private static void RenameLocationRecord(JsonObject records, string canonicalId, string legacyId)
    {
        JsonObject record = records[canonicalId]!.DeepClone().AsObject();
        records.Remove(canonicalId);
        record["LocationId"] = legacyId;
        records[legacyId] = record;
    }

    private static void ReplaceNodeLocation(JsonObject record, string propertyName)
    {
        string value = record[propertyName]?.GetValue<string>() ?? "";
        record[propertyName] = value switch
        {
            StrategicManagementIds.LocationQingheCore => LegacyPlainsCityFixtureId,
            StrategicManagementIds.LocationChiyanHighBasin => LegacyBonefieldFixtureId,
            _ => value
        };
    }

    private static void AssertIdentityDocumentRejected(
        StrategicManagementSaveService saves,
        JsonObject document,
        string caseName)
    {
        string path = Path.Combine(Path.GetTempPath(), $"rpg-stage2-identity-{caseName}-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            AssertThrowsInvalidOperation(() => saves.Load(path), $"identity migration case must fail case={caseName}");
        }
        finally
        {
            DeleteSaveFamily(path);
        }
    }

    private static InvalidOperationException CaptureInvalidOperation(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException exception)
        {
            return exception;
        }

        throw new InvalidOperationException("Expected InvalidOperationException.");
    }

    private static InvalidDataException CaptureInvalidData(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidDataException exception)
        {
            return exception;
        }

        throw new InvalidOperationException("Expected InvalidDataException.");
    }
}
