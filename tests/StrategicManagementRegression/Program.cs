using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Application.StrategicBattleBridge;
using Rpg.Application.StrategicManagement;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;

System.Environment.SetEnvironmentVariable(
    "RPG_GAMELOG_DIR",
    Path.Combine(Path.GetTempPath(), "rpg-strategic-management-tests"));

Run("strategic management state initializes without legacy world state", StrategicManagementStateInitializesWithoutLegacyWorldState);
Run("first playable starts with three dispatchable hero companies", FirstPlayableStartsWithThreeDispatchableHeroCompanies);
Run("strategic management resolves map site ids without silent city fallback", StrategicManagementResolvesMapSiteIdsWithoutSilentCityFallback);
Run("strategic management has no strategic battle preparation choice system", StrategicManagementHasNoStrategicBattlePreparationChoiceSystem);
Run("strategic management dashboard hides strategic battle preparation options", StrategicManagementDashboardHidesStrategicBattlePreparationOptions);
Run("Bonefield assault creates expedition without strategic preparation", BonefieldAssaultCreatesExpeditionWithoutStrategicPreparation);
Run("common city identity derives common muster templates", CommonCityIdentityDerivesCommonMusterTemplates);
Run("beast muster requires controlled beast source and beast pen", BeastMusterRequiresControlledBeastSourceAndBeastPen);
Run("losing beast source keeps existing corps but blocks new beast creation", LosingBeastSourceKeepsExistingCorpsButBlocksNewBeastCreation);
Run("build facility consumes resources and facility slot", BuildFacilityConsumesResourcesAndFacilitySlot);
Run("build facility failure leaves resources and slots unchanged", BuildFacilityFailureLeavesResourcesAndSlotsUnchanged);
Run("create corps consumes resources and creates persistent corps instance", CreateCorpsConsumesResourcesAndCreatesPersistentCorpsInstance);
Run("create corps failure leaves resources and corps list unchanged", CreateCorpsFailureLeavesResourcesAndCorpsListUnchanged);
Run("assign corps to hero records aptitude without random failure", AssignCorpsToHeroRecordsAptitudeWithoutRandomFailure);
Run("create expedition locks assigned hero company", CreateExpeditionLocksAssignedHeroCompany);
Run("create expedition locks selected hero companies", CreateExpeditionLocksSelectedHeroCompanies);
Run("create expedition rejects hero without assigned corps", CreateExpeditionRejectsHeroWithoutAssignedCorps);
Run("strategic battle bridge creates assault session from expedition", StrategicBattleBridgeCreatesAssaultSessionFromExpedition);
Run("strategic battle bridge creates session for all expedition participants", StrategicBattleBridgeCreatesSessionForAllExpeditionParticipants);
Run("strategic battle bridge creates session without strategic preparation metadata", StrategicBattleBridgeCreatesSessionWithoutStrategicPreparationMetadata);
Run("strategic battle bridge creates active context", StrategicBattleBridgeCreatesActiveContext);
Run("retarget moving expedition to assault updates strategic battle session authority", RetargetMovingExpeditionToAssaultUpdatesStrategicBattleSessionAuthority);
Run("retarget moving expedition to assault creates bridge session without preparation gate", RetargetMovingExpeditionToAssaultCreatesBridgeSessionWithoutPreparationGate);
Run("strategic battle result summary omits strategic preparation feedback", StrategicBattleResultSummaryOmitsStrategicPreparationFeedback);
Run("strategic battle bridge maps duplicate battle unit participants by participant identity", StrategicBattleBridgeMapsDuplicateBattleUnitParticipantsByParticipantIdentity);
Run("strategic battle bridge snapshot preserves strategic participant identity", StrategicBattleBridgeSnapshotPreservesStrategicParticipantIdentity);
Run("strategic battle result summary applies victory consequences", StrategicBattleResultSummaryAppliesVictoryConsequences);
Run("strategic battle result records reward hero feedback and equipment sample", StrategicBattleResultRecordsRewardHeroFeedbackAndEquipmentSample);
Run("strategic battle result records defeat feedback and recovery reason", StrategicBattleResultRecordsDefeatFeedbackAndRecoveryReason);
Run("strategic management dashboard exposes latest battle feedback", StrategicManagementDashboardExposesLatestBattleFeedback);
Run("strategic battle result rejects duplicate reward application", StrategicBattleResultRejectsDuplicateRewardApplication);
Run("strategic battle result grants one time target reward across expeditions", StrategicBattleResultGrantsOneTimeTargetRewardAcrossExpeditions);
Run("strategic battle result rejects null participant summary without mutation", StrategicBattleResultRejectsNullParticipantSummaryWithoutMutation);
Run("strategic battle result summary applies defeat consequences", StrategicBattleResultSummaryAppliesDefeatConsequences);
Run("strategic battle result summary applies all expedition participant consequences", StrategicBattleResultSummaryAppliesAllExpeditionParticipantConsequences);
Run("strategic battle result summary rejects mismatched result", StrategicBattleResultSummaryRejectsMismatchedResult);
Run("strategic battle result summary rejects missing participant result", StrategicBattleResultSummaryRejectsMissingParticipantResult);
Run("strategic battle bridge rejects location without battle entry metadata", StrategicBattleBridgeRejectsLocationWithoutBattleEntryMetadata);
Run("strategic management dashboard summarizes city resources facilities corps and heroes", StrategicManagementDashboardSummarizesCityResourcesFacilitiesCorpsAndHeroes);
Run("strategic management dashboard exposes dispatchable hero companies", StrategicManagementDashboardExposesDispatchableHeroCompanies);
Run("strategic management dashboard explains unavailable beast muster reasons", StrategicManagementDashboardExplainsUnavailableBeastMusterReasons);
Run("strategic management dashboard reflects command mutations", StrategicManagementDashboardReflectsCommandMutations);
Run("strategic management dashboard summarizes non-city location", StrategicManagementDashboardSummarizesNonCityLocation);
Run("strategic management settles controlled resource site production", StrategicManagementSettlesControlledResourceSiteProduction);
Run("strategic management uses elapsed world time naming instead of step naming", StrategicManagementUsesElapsedWorldTimeNamingInsteadOfStepNaming);
Run("strategic management settles elapsed world time and controlled production", StrategicManagementSettlesElapsedWorldTimeAndControlledProduction);
Run("strategic management elapsed world time skips enemy held production", StrategicManagementElapsedWorldTimeSkipsEnemyHeldProduction);
Run("strategic management elapsed world time rejects invalid pulse count without mutation", StrategicManagementElapsedWorldTimeRejectsInvalidPulseCountWithoutMutation);
Run("strategic management runtime blocks elapsed time while city management paused", StrategicManagementRuntimeBlocksElapsedTimeWhileCityManagementPaused);
Run("strategic management runtime settles elapsed time after world map resumes", StrategicManagementRuntimeSettlesElapsedTimeAfterWorldMapResumes);
Run("strategic management runtime builds dashboard from retained command state", StrategicManagementRuntimeBuildsDashboardFromRetainedCommandState);
Run("strategic management application has no legacy world state dependency", StrategicManagementApplicationHasNoLegacyWorldStateDependency);

static void StrategicManagementStateInitializesWithoutLegacyWorldState()
{
    StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
    StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);

    AssertTrue(state.FactionResources.ContainsKey(StrategicManagementIds.FactionPlayer), "player faction resources should be initialized");
    AssertTrue(state.Cities.ContainsKey(StrategicManagementIds.LocationPlainsCity), "first core city should be initialized");
    AssertTrue(state.Heroes.ContainsKey(StrategicManagementIds.HeroBeastTamer), "first strategic hero should be initialized");
    AssertNoLegacyWorldReferences(typeof(StrategicManagementState));
}

static void FirstPlayableStartsWithThreeDispatchableHeroCompanies()
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

static void StrategicManagementResolvesMapSiteIdsWithoutSilentCityFallback()
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

static void StrategicManagementHasNoStrategicBattlePreparationChoiceSystem()
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

static void StrategicManagementDashboardHidesStrategicBattlePreparationOptions()
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

static void BonefieldAssaultCreatesExpeditionWithoutStrategicPreparation()
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

static void CommonCityIdentityDerivesCommonMusterTemplates()
{
    StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
    StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
    StrategicManagementRules rules = new(definitions);

    IReadOnlyList<StrategicMusterTemplateAvailability> templates =
        rules.GetMusterTemplates(state, StrategicManagementIds.LocationPlainsCity);

    AssertAvailable(templates, StrategicManagementIds.CorpsShieldLine);
    AssertAvailable(templates, StrategicManagementIds.CorpsArcherLine);
    AssertAvailable(templates, StrategicManagementIds.CorpsCavalryLine);
}

static void BeastMusterRequiresControlledBeastSourceAndBeastPen()
{
    StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
    StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
    StrategicManagementRules rules = new(definitions);
    StrategicManagementCommandService commands = new(definitions, rules);

    StrategicMusterTemplateAvailability initial =
        FindTemplate(rules.GetMusterTemplates(state, StrategicManagementIds.LocationPlainsCity), StrategicManagementIds.CorpsWolfPack);
    AssertTrue(!initial.IsAvailable, "wolf pack should start unavailable");
    AssertContains(initial.FailureReasons, StrategicFailureReasons.MissingSourcePermission, "wolf pack should require beast source permission");
    AssertContains(initial.FailureReasons, StrategicFailureReasons.MissingFacility, "wolf pack should require beast pen");

    StrategicCommandResult occupy = commands.OccupyLocation(
        state,
        StrategicManagementIds.LocationBeastDen,
        StrategicManagementIds.FactionPlayer);
    AssertTrue(occupy.Success, "occupying beast den should succeed");

    StrategicCommandResult build = commands.BuildFacility(
        state,
        StrategicManagementIds.LocationPlainsCity,
        StrategicManagementIds.FacilityBeastPen);
    AssertTrue(build.Success, $"building beast pen should succeed, got {build.FailureReason}");

    StrategicMusterTemplateAvailability afterUnlock =
        FindTemplate(rules.GetMusterTemplates(state, StrategicManagementIds.LocationPlainsCity), StrategicManagementIds.CorpsWolfPack);
    AssertTrue(afterUnlock.IsAvailable, $"wolf pack should become available, got {string.Join(",", afterUnlock.FailureReasons)}");
}

static void LosingBeastSourceKeepsExistingCorpsButBlocksNewBeastCreation()
{
    StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
    StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
    StrategicManagementRules rules = new(definitions);
    StrategicManagementCommandService commands = new(definitions, rules);

    commands.OccupyLocation(state, StrategicManagementIds.LocationBeastDen, StrategicManagementIds.FactionPlayer);
    commands.BuildFacility(state, StrategicManagementIds.LocationPlainsCity, StrategicManagementIds.FacilityBeastPen);
    StrategicCommandResult unassign = commands.UnassignCorpsFromHero(state, StrategicManagementIds.HeroBeastTamer);
    AssertTrue(unassign.Success, $"test setup unassignment should succeed, got {unassign.FailureReason}");
    StrategicCommandResult create = commands.CreateCorps(
        state,
        StrategicManagementIds.LocationPlainsCity,
        StrategicManagementIds.CorpsWolfPack);
    AssertTrue(create.Success, $"creating wolf pack should succeed, got {create.FailureReason}");
    string existingCorpsId = create.CreatedEntityId;

    StrategicCommandResult lose = commands.LoseLocation(
        state,
        StrategicManagementIds.LocationBeastDen,
        StrategicManagementIds.FactionEnemy);
    AssertTrue(lose.Success, "losing beast source should succeed");

    AssertTrue(state.CorpsInstances.ContainsKey(existingCorpsId), "existing beast corps should remain after source loss");
    StrategicCommandResult secondCreate = commands.CreateCorps(
        state,
        StrategicManagementIds.LocationPlainsCity,
        StrategicManagementIds.CorpsWolfPack);
    AssertTrue(!secondCreate.Success, "new wolf pack creation should be blocked after source loss");
    AssertEqual(StrategicFailureReasons.MissingSourcePermission, secondCreate.FailureReason, "source loss should be the rejection reason");
}

static void BuildFacilityConsumesResourcesAndFacilitySlot()
{
    StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
    StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
    StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
    int beforeMaterials = state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceBuildingMaterials);

    StrategicCommandResult result = commands.BuildFacility(
        state,
        StrategicManagementIds.LocationPlainsCity,
        StrategicManagementIds.FacilityTrainingGround);

    AssertTrue(result.Success, $"training ground build should succeed, got {result.FailureReason}");
    StrategicCityState city = state.Cities[StrategicManagementIds.LocationPlainsCity];
    AssertEqual(1, city.Facilities.Count, "city should consume one facility slot");
    AssertEqual(
        beforeMaterials - 40,
        state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceBuildingMaterials),
        "building materials should be spent");
}

static void BuildFacilityFailureLeavesResourcesAndSlotsUnchanged()
{
    StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
    StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
    state.SetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceBuildingMaterials, 0);
    StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
    int beforeFacilities = state.Cities[StrategicManagementIds.LocationPlainsCity].Facilities.Count;
    int beforeMaterials = state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceBuildingMaterials);

    StrategicCommandResult result = commands.BuildFacility(
        state,
        StrategicManagementIds.LocationPlainsCity,
        StrategicManagementIds.FacilityTrainingGround);

    AssertTrue(!result.Success, "facility build should fail without resources");
    AssertEqual(StrategicFailureReasons.InsufficientResources, result.FailureReason, "failure reason should be resources");
    AssertEqual(beforeFacilities, state.Cities[StrategicManagementIds.LocationPlainsCity].Facilities.Count, "facility list should not change");
    AssertEqual(beforeMaterials, state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceBuildingMaterials), "resources should not change");
}

static void CreateCorpsConsumesResourcesAndCreatesPersistentCorpsInstance()
{
    StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
    StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
    StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
    int beforeMoney = state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceMoney);

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
}

static void CreateCorpsFailureLeavesResourcesAndCorpsListUnchanged()
{
    StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
    StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
    StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
    int beforeMoney = state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceMoney);
    int beforeCorps = state.CorpsInstances.Count;

    StrategicCommandResult result = commands.CreateCorps(
        state,
        StrategicManagementIds.LocationPlainsCity,
        StrategicManagementIds.CorpsGreatBeast);

    AssertTrue(!result.Success, "great beast should be unavailable without beast chain");
    AssertEqual(StrategicFailureReasons.MissingSourcePermission, result.FailureReason, "missing source should be reported first");
    AssertEqual(beforeCorps, state.CorpsInstances.Count, "corps list should not change");
    AssertEqual(beforeMoney, state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceMoney), "resources should not change");
}

static void AssignCorpsToHeroRecordsAptitudeWithoutRandomFailure()
{
    StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
    StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
    StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
    commands.OccupyLocation(state, StrategicManagementIds.LocationBeastDen, StrategicManagementIds.FactionPlayer);
    commands.BuildFacility(state, StrategicManagementIds.LocationPlainsCity, StrategicManagementIds.FacilityBeastPen);
    StrategicCommandResult unassign = commands.UnassignCorpsFromHero(state, StrategicManagementIds.HeroBeastTamer);
    AssertTrue(unassign.Success, $"test setup unassignment should succeed, got {unassign.FailureReason}");
    StrategicCommandResult create = commands.CreateCorps(
        state,
        StrategicManagementIds.LocationPlainsCity,
        StrategicManagementIds.CorpsWolfPack);

    StrategicCommandResult assign = commands.AssignCorpsToHero(
        state,
        StrategicManagementIds.HeroBeastTamer,
        create.CreatedEntityId);

    AssertTrue(assign.Success, $"assignment should succeed, got {assign.FailureReason}");
    AssertEqual(create.CreatedEntityId, state.Heroes[StrategicManagementIds.HeroBeastTamer].AssignedCorpsInstanceId, "hero should reference assigned corps");
    AssertEqual(StrategicManagementIds.HeroBeastTamer, state.CorpsInstances[create.CreatedEntityId].AssignedHeroId, "corps should reference assigned hero");
    AssertEqual(StrategicHeroCorpsAptitudeGrade.A, assign.AptitudeGrade, "beast tamer should record beast aptitude");
    AssertTrue(!assign.Events.Any(item => item.Kind == "RandomBeastControlFailure"), "assignment must not create random beast failure");
}

static void CreateExpeditionLocksAssignedHeroCompany()
{
    StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
    StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
    StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
    string corpsInstanceId = state.Heroes[StrategicManagementIds.HeroOrdinaryCommander].AssignedCorpsInstanceId;
    StrategicCommandResult expedition = commands.CreateExpedition(
        state,
        StrategicManagementIds.LocationPlainsCity,
        StrategicManagementIds.LocationBeastDen,
        StrategicExpeditionIntent.AssaultLocation,
        StrategicManagementIds.HeroOrdinaryCommander);

    AssertTrue(expedition.Success, $"expedition creation should succeed, got {expedition.FailureReason}");
    AssertTrue(state.Expeditions.ContainsKey(expedition.CreatedEntityId), "created expedition should be durable strategic state");
    StrategicExpeditionState expeditionState = state.Expeditions[expedition.CreatedEntityId];
    AssertEqual(StrategicManagementIds.HeroOrdinaryCommander, expeditionState.HeroId, "expedition should reference the hero");
    AssertEqual(corpsInstanceId, expeditionState.CorpsInstanceId, "expedition should reference the assigned corps instance");
    AssertEqual(1, expeditionState.Participants.Count, "single-company expedition should retain one participant");
    AssertEqual(StrategicManagementIds.LocationPlainsCity, expeditionState.SourceLocationId, "expedition source should be the source city");
    AssertEqual(StrategicManagementIds.LocationBeastDen, expeditionState.TargetLocationId, "expedition target should be the selected strategic location");
    AssertEqual(StrategicExpeditionStatus.Moving, expeditionState.Status, "new expedition should start as moving");
    AssertEqual(expedition.CreatedEntityId, state.Heroes[StrategicManagementIds.HeroOrdinaryCommander].CurrentExpeditionId, "hero should be locked to the expedition");
    AssertEqual(expedition.CreatedEntityId, state.CorpsInstances[corpsInstanceId].CurrentExpeditionId, "corps should be locked to the expedition");
    AssertEqual(StrategicCorpsInstanceStatus.Expedition, state.CorpsInstances[corpsInstanceId].Status, "corps status should move to expedition");
}

static void CreateExpeditionLocksSelectedHeroCompanies()
{
    StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
    StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
    StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
    string[] heroIds =
    {
        StrategicManagementIds.HeroOrdinaryCommander,
        StrategicManagementIds.HeroArcherCaptain,
        StrategicManagementIds.HeroBeastTamer
    };

    StrategicCommandResult expedition = commands.CreateExpedition(
        state,
        StrategicManagementIds.LocationPlainsCity,
        StrategicManagementIds.LocationBeastDen,
        StrategicExpeditionIntent.AssaultLocation,
        heroIds);

    AssertTrue(expedition.Success, $"multi-company expedition creation should succeed, got {expedition.FailureReason}");
    StrategicExpeditionState expeditionState = state.Expeditions[expedition.CreatedEntityId];
    AssertEqual(3, expeditionState.Participants.Count, "expedition should retain all selected hero-company participants");
    AssertEqual(heroIds[0], expeditionState.HeroId, "compatibility hero alias should retain the first selected participant");
    AssertEqual(state.Heroes[heroIds[0]].AssignedCorpsInstanceId, expeditionState.CorpsInstanceId, "compatibility corps alias should retain the first selected participant");
    foreach (string heroId in heroIds)
    {
        string corpsInstanceId = state.Heroes[heroId].AssignedCorpsInstanceId;
        AssertTrue(
            expeditionState.Participants.Any(participant =>
                participant.HeroId == heroId &&
                participant.CorpsInstanceId == corpsInstanceId),
            $"{heroId} should be recorded as an expedition participant");
        AssertEqual(expedition.CreatedEntityId, state.Heroes[heroId].CurrentExpeditionId, $"{heroId} should be locked to the expedition");
        AssertEqual(expedition.CreatedEntityId, state.CorpsInstances[corpsInstanceId].CurrentExpeditionId, $"{corpsInstanceId} should be locked to the expedition");
        AssertEqual(StrategicCorpsInstanceStatus.Expedition, state.CorpsInstances[corpsInstanceId].Status, $"{corpsInstanceId} should move to expedition status");
    }
}

static void CreateExpeditionRejectsHeroWithoutAssignedCorps()
{
    StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
    StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
    StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
    StrategicCommandResult unassign = commands.UnassignCorpsFromHero(state, StrategicManagementIds.HeroOrdinaryCommander);
    AssertTrue(unassign.Success, $"test setup unassignment should succeed, got {unassign.FailureReason}");

    StrategicCommandResult result = commands.CreateExpedition(
        state,
        StrategicManagementIds.LocationPlainsCity,
        StrategicManagementIds.LocationBeastDen,
        StrategicExpeditionIntent.AssaultLocation,
        StrategicManagementIds.HeroOrdinaryCommander);

    AssertTrue(!result.Success, "hero without assigned corps should not create an expedition");
    AssertEqual(StrategicFailureReasons.HeroHasNoAssignedCorps, result.FailureReason, "failure reason should explain the missing assigned corps");
    AssertEqual(0, state.Expeditions.Count, "failed expedition creation must not mutate expedition state");
    AssertEqual("", state.Heroes[StrategicManagementIds.HeroOrdinaryCommander].CurrentExpeditionId, "failed expedition creation must not lock the hero");
}

static void StrategicBattleBridgeCreatesAssaultSessionFromExpedition()
{
    var setup = CreateStrategicAssaultExpedition();
    StrategicManagementDefinitionSet definitions = setup.Definitions;
    StrategicManagementState state = setup.State;
    string expeditionId = setup.ExpeditionId;
    string corpsId = setup.CorpsInstanceId;
    StrategicBattleBridgeService bridge = new(definitions);

    StrategicBattleSessionResult result = bridge.CreateSession(
        state,
        expeditionId,
        "res://return_to_world.tscn",
        "res://scenes/world/sites/WorldSiteRoot.tscn");

    AssertTrue(result.Success, $"bridge session should be created, got {result.FailureReason}");
    StrategicBattleSession session = result.Session;
    AssertEqual(expeditionId, session.ExpeditionId, "bridge session should retain expedition id");
    AssertEqual(StrategicManagementIds.LocationPlainsCity, session.SourceLocationId, "bridge session should retain source strategic location");
    AssertEqual(StrategicManagementIds.LocationBeastDen, session.TargetLocationId, "bridge session should retain target strategic location");
    AssertEqual("bonefield_assault_v1", session.MapDefinitionId, "bridge session should use target location battle metadata");
    AssertEqual("assault_bonefield", session.EncounterId, "bridge session should expose encounter metadata");
    AssertEqual(1, session.Participants.Count, "first bridge slice should expose the selected hero company as one participant");
    AssertEqual(StrategicManagementIds.HeroOrdinaryCommander, session.Participants[0].HeroId, "participant should retain strategic hero id");
    AssertEqual(corpsId, session.Participants[0].CorpsInstanceId, "participant should retain strategic corps instance id");
    AssertEqual(100, session.Participants[0].PreBattleCorpsStrength, "participant should snapshot pre-battle corps strength");
}

static void StrategicBattleBridgeCreatesSessionForAllExpeditionParticipants()
{
    StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
    StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
    StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
    string[] heroIds =
    {
        StrategicManagementIds.HeroOrdinaryCommander,
        StrategicManagementIds.HeroArcherCaptain,
        StrategicManagementIds.HeroBeastTamer
    };
    StrategicCommandResult expedition = commands.CreateExpedition(
        state,
        StrategicManagementIds.LocationPlainsCity,
        StrategicManagementIds.LocationBeastDen,
        StrategicExpeditionIntent.AssaultLocation,
        heroIds);
    AssertTrue(expedition.Success, $"multi-company expedition creation should succeed, got {expedition.FailureReason}");
    StrategicBattleBridgeService bridge = new(definitions);

    StrategicBattleSessionResult result = bridge.CreateSession(
        state,
        expedition.CreatedEntityId,
        "res://return_to_world.tscn",
        "res://scenes/world/sites/WorldSiteRoot.tscn");

    AssertTrue(result.Success, $"bridge session should be created, got {result.FailureReason}");
    StrategicBattleSession session = result.Session;
    AssertEqual(3, session.Participants.Count, "bridge session should expose every selected strategic participant");
    foreach (string heroId in heroIds)
    {
        string corpsInstanceId = state.Heroes[heroId].AssignedCorpsInstanceId;
        StrategicBattleParticipantReference? participant = session.Participants.FirstOrDefault(item =>
            item.HeroId == heroId &&
            item.CorpsInstanceId == corpsInstanceId);
        if (participant == null)
        {
            throw new InvalidOperationException($"Missing bridge participant for {heroId}:{corpsInstanceId}");
        }

        AssertEqual(100, participant.PreBattleCorpsStrength, $"{heroId} should snapshot pre-battle corps strength");
        AssertTrue(
            participant.ParticipantId.Contains(heroId, StringComparison.Ordinal) &&
            participant.ParticipantId.Contains(corpsInstanceId, StringComparison.Ordinal),
            "participant id should carry strategic hero and corps identity for legacy bridging");
    }
}

static void StrategicBattleBridgeCreatesSessionWithoutStrategicPreparationMetadata()
{
    StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
    StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
    StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
    StrategicCommandResult expedition = commands.CreateExpedition(
        state,
        StrategicManagementIds.LocationPlainsCity,
        StrategicManagementIds.LocationBeastDen,
        StrategicExpeditionIntent.AssaultLocation,
        StrategicManagementIds.HeroOrdinaryCommander);
    AssertTrue(expedition.Success, $"assault expedition should be created without strategic preparation, got {expedition.FailureReason}");
    StrategicBattleBridgeService bridge = new(definitions);

    StrategicBattleSession session = bridge.CreateSession(
        state,
        expedition.CreatedEntityId,
        "res://return_to_world.tscn",
        "res://scenes/world/sites/WorldSiteRoot.tscn").Session;

    AssertTrue(session.GetType().GetProperty("StrategicPreparationId") == null, "bridge session should not carry strategic preparation id");
    AssertTrue(session.GetType().GetProperty("StrategicPreparationDisplayName") == null, "bridge session should not carry strategic preparation display text");
    AssertTrue(session.GetType().GetProperty("StrategicPreparationBriefingText") == null, "bridge session should not carry strategic preparation briefing text");
    AssertTrue(session.GetType().GetProperty("StrategicPreparationReportText") == null, "bridge session should not carry strategic preparation report text");

    BattleStartRequest request = new()
    {
        RequestId = "request_direct_trigger_metadata",
        BattleKind = BattleKind.AssaultSite
    };
    request.PlayerForces.Add(new BattleForceRequest
    {
        ForceId = "ordinary_hero",
        UnitDefinitionId = definitions.Heroes[StrategicManagementIds.HeroOrdinaryCommander].BattleUnitId,
        Count = 1
    });

    bridge.AttachSessionToLegacyRequest(session, request);

    AssertTrue(request.GetType().GetProperty("StrategicPreparationId") == null, "legacy request adapter should not carry strategic preparation id");
    AssertTrue(request.GetType().GetProperty("StrategicPreparationDisplayName") == null, "legacy request adapter should not carry strategic preparation display text");
    AssertTrue(request.GetType().GetProperty("StrategicPreparationBriefingText") == null, "legacy request adapter should not carry strategic preparation briefing text");
    AssertTrue(request.GetType().GetProperty("StrategicPreparationReportText") == null, "legacy request adapter should not carry strategic preparation report text");
}

static void StrategicBattleBridgeCreatesActiveContext()
{
    var setup = CreateStrategicAssaultExpedition();
    StrategicManagementDefinitionSet definitions = setup.Definitions;
    StrategicManagementState state = setup.State;
    StrategicBattleBridgeService bridge = new(definitions);
    StrategicBattleSession session = bridge.CreateSession(
        state,
        setup.ExpeditionId,
        "res://return_to_world.tscn",
        "res://scenes/world/sites/WorldSiteRoot.tscn").Session;
    BattleStartRequest request = BuildStrategicBattleRequestForHero(
        definitions,
        state,
        bridge,
        session,
        StrategicManagementIds.HeroOrdinaryCommander,
        "request_active_context");

    StrategicBattleActiveContextResult result = bridge.CreateActiveContext(state, session, request);

    AssertTrue(result.Success, $"active context should be created, got {result.FailureReason}");
    StrategicBattleActiveContext context = result.Context;
    AssertEqual(session.SessionId, context.ContextId, "active context id should match the strategic battle session");
    AssertEqual(session.SessionId, context.Session.SessionId, "active context should retain bridge session");
    AssertEqual(session.SessionId, context.Snapshot.BattleId, "active context snapshot should target the bridge session");
    AssertEqual(request, context.CompatibilityRequest, "compatibility request should be retained only as the presentation adapter");
    AssertEqual(session.SessionId, context.CompatibilityRequest.StrategicBattleSessionId, "compatibility request should be projected with session id");
    AssertTrue(
        context.CompatibilityRequest.PlayerForces.All(force => !string.IsNullOrWhiteSpace(force.StrategicCorpsInstanceId)),
        "active context projection should preserve strategic corps identity on player forces");
}

static void RetargetMovingExpeditionToAssaultUpdatesStrategicBattleSessionAuthority()
{
    StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
    StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
    StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));

    StrategicCommandResult expedition = commands.CreateExpedition(
        state,
        StrategicManagementIds.LocationPlainsCity,
        "",
        StrategicExpeditionIntent.MoveToPosition,
        StrategicManagementIds.HeroOrdinaryCommander);
    AssertTrue(expedition.Success, $"move expedition creation should succeed, got {expedition.FailureReason}");

    StrategicCommandResult retarget = commands.RetargetExpedition(
        state,
        expedition.CreatedEntityId,
        StrategicManagementIds.LocationBeastDen,
        StrategicExpeditionIntent.AssaultLocation);

    AssertTrue(retarget.Success, $"retarget to Bonefield assault should succeed, got {retarget.FailureReason}");
    StrategicExpeditionState retargeted = state.Expeditions[expedition.CreatedEntityId];
    AssertEqual(StrategicManagementIds.LocationBeastDen, retargeted.TargetLocationId, "retarget should update strategic target location");
    AssertEqual(StrategicExpeditionIntent.AssaultLocation, retargeted.Intent, "retarget should update strategic expedition intent");

    StrategicBattleSessionResult session = new StrategicBattleBridgeService(definitions).CreateSession(
        state,
        expedition.CreatedEntityId,
        "res://return_to_world.tscn",
        "res://scenes/world/sites/WorldSiteRoot.tscn");

    AssertTrue(session.Success, $"retargeted assault expedition should be accepted by bridge, got {session.FailureReason}");
    AssertEqual(StrategicManagementIds.LocationBeastDen, session.Session.TargetLocationId, "bridge session should read the retargeted strategic target");
}

static void RetargetMovingExpeditionToAssaultCreatesBridgeSessionWithoutPreparationGate()
{
    StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
    StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
    StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));

    StrategicCommandResult expedition = commands.CreateExpedition(
        state,
        StrategicManagementIds.LocationPlainsCity,
        "",
        StrategicExpeditionIntent.MoveToPosition,
        StrategicManagementIds.HeroOrdinaryCommander);
    AssertTrue(expedition.Success, $"move expedition creation should succeed, got {expedition.FailureReason}");

    StrategicCommandResult retarget = commands.RetargetExpedition(
        state,
        expedition.CreatedEntityId,
        StrategicManagementIds.LocationBeastDen,
        StrategicExpeditionIntent.AssaultLocation);

    AssertTrue(retarget.Success, $"retarget to Bonefield assault should allow travel, got {retarget.FailureReason}");
    StrategicExpeditionState retargeted = state.Expeditions[expedition.CreatedEntityId];
    AssertEqual(StrategicManagementIds.LocationBeastDen, retargeted.TargetLocationId, "retarget should keep the assault target for travel");
    AssertEqual(StrategicExpeditionIntent.AssaultLocation, retargeted.Intent, "retarget should record the intended assault for later bridge entry");

    StrategicBattleSessionResult session = new StrategicBattleBridgeService(definitions).CreateSession(
        state,
        expedition.CreatedEntityId,
        "res://return_to_world.tscn",
        "res://scenes/world/sites/WorldSiteRoot.tscn");
    AssertTrue(session.Success, $"retargeted assault should enter bridge without strategic preparation, got {session.FailureReason}");
}

static void StrategicBattleResultSummaryOmitsStrategicPreparationFeedback()
{
    StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
    StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
    StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
    StrategicCommandResult expedition = commands.CreateExpedition(
        state,
        StrategicManagementIds.LocationPlainsCity,
        StrategicManagementIds.LocationBeastDen,
        StrategicExpeditionIntent.AssaultLocation,
        StrategicManagementIds.HeroOrdinaryCommander);
    AssertTrue(expedition.Success, $"assault expedition should be created without strategic preparation, got {expedition.FailureReason}");
    StrategicBattleBridgeService bridge = new(definitions);
    StrategicBattleSession session = bridge.CreateSession(
        state,
        expedition.CreatedEntityId,
        "res://return_to_world.tscn",
        "res://scenes/world/sites/WorldSiteRoot.tscn").Session;
    BattleStartRequest request = BuildStrategicBattleRequestForHero(
        definitions,
        state,
        bridge,
        session,
        StrategicManagementIds.HeroOrdinaryCommander,
        "request_direct_trigger_result");
    BattleResult battleResult = BuildVictoryResult(request, survivedCount: request.PlayerForces.Sum(force => force.Count));

    StrategicBattleResultSummary summary = bridge.BuildResultSummary(request, battleResult);
    StrategicCommandResult result = commands.ApplyBattleResultSummary(state, summary);

    AssertTrue(result.Success, $"battle summary should apply, got {result.FailureReason}");
    AssertTrue(summary.GetType().GetProperty("StrategicPreparationId") == null, "result summary should not carry strategic preparation id");
    StrategicBattleFeedbackRecord feedback = state.BattleFeedbackRecords[result.CreatedEntityId];
    AssertTrue(feedback.GetType().GetProperty("PreparationId") == null, "battle feedback should not store strategic preparation id");
    AssertTrue(feedback.GetType().GetProperty("PreparationText") == null, "battle feedback should not store strategic preparation text");
}

static void StrategicBattleBridgeMapsDuplicateBattleUnitParticipantsByParticipantIdentity()
{
    StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
    StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
    StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
    definitions.Corps[StrategicManagementIds.CorpsArcherLine].BattleUnitId =
        definitions.Corps[StrategicManagementIds.CorpsShieldLine].BattleUnitId;
    string[] heroIds =
    {
        StrategicManagementIds.HeroOrdinaryCommander,
        StrategicManagementIds.HeroArcherCaptain
    };
    StrategicCommandResult expedition = commands.CreateExpedition(
        state,
        StrategicManagementIds.LocationPlainsCity,
        StrategicManagementIds.LocationBeastDen,
        StrategicExpeditionIntent.AssaultLocation,
        heroIds);
    AssertTrue(expedition.Success, $"duplicate-unit expedition should be created, got {expedition.FailureReason}");
    StrategicBattleBridgeService bridge = new(definitions);
    StrategicBattleSession session = bridge.CreateSession(
        state,
        expedition.CreatedEntityId,
        "res://return_to_world.tscn",
        "res://scenes/world/sites/WorldSiteRoot.tscn").Session;
    StrategicBattleParticipantReference ordinary = session.Participants.First(item => item.HeroId == StrategicManagementIds.HeroOrdinaryCommander);
    StrategicBattleParticipantReference archer = session.Participants.First(item => item.HeroId == StrategicManagementIds.HeroArcherCaptain);
    string sharedUnitId = definitions.Corps[StrategicManagementIds.CorpsShieldLine].BattleUnitId;
    BattleStartRequest request = new()
    {
        RequestId = "request_duplicate_units",
        BattleKind = BattleKind.AssaultSite
    };
    request.PlayerForces.Add(new BattleForceRequest
    {
        ForceId = "ordinary_corps",
        UnitDefinitionId = sharedUnitId,
        StrategicParticipantId = ordinary.ParticipantId,
        Count = 4
    });
    request.PlayerForces.Add(new BattleForceRequest
    {
        ForceId = "archer_corps",
        UnitDefinitionId = sharedUnitId,
        StrategicParticipantId = archer.ParticipantId,
        Count = 4
    });

    bridge.AttachSessionToLegacyRequest(session, request);

    AssertEqual(ordinary.CorpsInstanceId, request.PlayerForces[0].StrategicCorpsInstanceId, "first duplicate force should keep its explicit participant identity");
    AssertEqual(archer.CorpsInstanceId, request.PlayerForces[1].StrategicCorpsInstanceId, "second duplicate force should not be remapped by shared battle unit id");
    BattleResult battleResult = new()
    {
        RequestId = request.RequestId,
        BattleKind = BattleKind.AssaultSite,
        Outcome = BattleOutcome.Victory
    };
    battleResult.ForceResults.Add(new BattleForceResult { ForceId = "ordinary_corps", InitialCount = 4, SurvivedCount = 4 });
    battleResult.ForceResults.Add(new BattleForceResult { ForceId = "archer_corps", InitialCount = 4, SurvivedCount = 0 });

    StrategicBattleResultSummary summary = bridge.BuildResultSummary(request, battleResult);
    StrategicCommandResult result = commands.ApplyBattleResultSummary(state, summary);

    AssertTrue(result.Success, $"duplicate-unit participant results should apply, got {result.FailureReason}");
    AssertEqual(100, state.CorpsInstances[ordinary.CorpsInstanceId].Strength, "first duplicate participant should keep its own full survival result");
    AssertEqual(0, state.CorpsInstances[archer.CorpsInstanceId].Strength, "second duplicate participant should keep its own routed result");
}

static void StrategicBattleBridgeSnapshotPreservesStrategicParticipantIdentity()
{
    var setup = CreateStrategicAssaultExpedition();
    StrategicManagementDefinitionSet definitions = setup.Definitions;
    StrategicManagementState state = setup.State;
    string expeditionId = setup.ExpeditionId;
    string corpsId = setup.CorpsInstanceId;
    StrategicBattleBridgeService bridge = new(definitions);
    StrategicBattleSession session = bridge.CreateSession(
        state,
        expeditionId,
        "res://return_to_world.tscn",
        "res://scenes/world/sites/WorldSiteRoot.tscn").Session;

    StrategicBattleSnapshotResult snapshotResult = bridge.CompileStartSnapshot(state, session);

    AssertTrue(snapshotResult.Success, $"bridge snapshot should compile, got {snapshotResult.FailureReason}");
    BattleStartSnapshot snapshot = snapshotResult.Snapshot;
    AssertEqual(session.SessionId, snapshot.BattleId, "snapshot battle id should match the bridge session");
    AssertEqual(StrategicManagementIds.LocationBeastDen, snapshot.TargetLocationId, "snapshot target should be the strategic target location");
    BattleGroupSnapshot? playerGroup = snapshot.BattleGroups.FirstOrDefault(group =>
        string.Equals(group.FactionId, StrategicManagementIds.FactionPlayer, StringComparison.Ordinal));
    if (playerGroup == null)
    {
        throw new InvalidOperationException("snapshot should contain a player battle group");
    }

    AssertEqual(StrategicManagementIds.HeroOrdinaryCommander, playerGroup.HeroId, "snapshot hero id should come from Strategic Management");
    AssertEqual(corpsId, playerGroup.CorpsId, "snapshot corps id should come from Strategic Management corps instance");
    AssertEqual(StrategicManagementIds.CorpsShieldLine, playerGroup.CorpsDefinitionId, "snapshot corps definition should come from Strategic Management");
    AssertEqual(StrategicManagementIds.LocationPlainsCity, playerGroup.SourceLocationId, "snapshot source location should come from the expedition source");
    AssertEqual(100, playerGroup.CorpsStrength, "snapshot should preserve pre-battle strategic corps strength");
    AssertTrue(
        !playerGroup.HeroId.StartsWith("probe_", StringComparison.Ordinal) &&
        !playerGroup.CorpsId.StartsWith("probe_", StringComparison.Ordinal),
        "bridge snapshots must not use legacy probe hero/corps identities for strategic participants");
}

static void StrategicBattleResultSummaryAppliesVictoryConsequences()
{
    var setup = CreateStrategicAssaultExpedition();
    StrategicManagementDefinitionSet definitions = setup.Definitions;
    StrategicManagementState state = setup.State;
    StrategicManagementCommandService commands = setup.Commands;
    string expeditionId = setup.ExpeditionId;
    string corpsId = setup.CorpsInstanceId;
    StrategicBattleBridgeService bridge = new(definitions);
    StrategicBattleSession session = bridge.CreateSession(
        state,
        expeditionId,
        "res://return_to_world.tscn",
        "res://scenes/world/sites/WorldSiteRoot.tscn").Session;
    StrategicBattleResultSummary summary = new()
    {
        SessionId = session.SessionId,
        ExpeditionId = expeditionId,
        TargetLocationId = StrategicManagementIds.LocationBeastDen,
        Outcome = BattleOutcome.Victory,
        ObjectiveSucceeded = true
    };
    summary.Participants.Add(new StrategicBattleParticipantResult
    {
        HeroId = StrategicManagementIds.HeroOrdinaryCommander,
        CorpsInstanceId = corpsId,
        RemainingCorpsStrength = 72
    });

    StrategicCommandResult result = commands.ApplyBattleResultSummary(state, summary);

    AssertTrue(result.Success, $"battle result summary should apply, got {result.FailureReason}");
    AssertEqual(StrategicManagementIds.FactionPlayer, state.Locations[StrategicManagementIds.LocationBeastDen].OwnerFactionId, "victory should transfer target location to player control");
    AssertEqual(StrategicLocationControlState.PlayerHeld, state.Locations[StrategicManagementIds.LocationBeastDen].ControlState, "victory should mark target location player-held");
    AssertEqual(StrategicExpeditionStatus.Resolved, state.Expeditions[expeditionId].Status, "victory should resolve the expedition");
    AssertEqual("", state.Heroes[StrategicManagementIds.HeroOrdinaryCommander].CurrentExpeditionId, "victory should unlock the hero from active expedition");
    AssertEqual("", state.CorpsInstances[corpsId].CurrentExpeditionId, "victory should unlock the corps from active expedition");
    AssertEqual(72, state.CorpsInstances[corpsId].Strength, "victory should write remaining corps strength");
    AssertEqual(StrategicCorpsInstanceStatus.AssignedToHero, state.CorpsInstances[corpsId].Status, "surviving corps should return to assigned hero status");
}

static void StrategicBattleResultRecordsRewardHeroFeedbackAndEquipmentSample()
{
    var setup = CreateStrategicAssaultExpedition();
    StrategicManagementDefinitionSet definitions = setup.Definitions;
    StrategicManagementState state = setup.State;
    StrategicManagementCommandService commands = setup.Commands;
    string expeditionId = setup.ExpeditionId;
    string corpsId = setup.CorpsInstanceId;
    int beforeBeastMaterials = state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceBeastMaterials);
    int beforeBuildingMaterials = state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceBuildingMaterials);
    StrategicBattleBridgeService bridge = new(definitions);
    StrategicBattleSession session = bridge.CreateSession(
        state,
        expeditionId,
        "res://return_to_world.tscn",
        "res://scenes/world/sites/WorldSiteRoot.tscn").Session;
    StrategicBattleResultSummary summary = new()
    {
        SessionId = session.SessionId,
        ExpeditionId = expeditionId,
        TargetLocationId = StrategicManagementIds.LocationBeastDen,
        Outcome = BattleOutcome.Victory,
        ObjectiveSucceeded = true
    };
    summary.Participants.Add(new StrategicBattleParticipantResult
    {
        HeroId = StrategicManagementIds.HeroOrdinaryCommander,
        CorpsInstanceId = corpsId,
        RemainingCorpsStrength = 72
    });

    StrategicCommandResult result = commands.ApplyBattleResultSummary(state, summary);

    AssertTrue(result.Success, $"victory summary should apply, got {result.FailureReason}");
    AssertTrue(!string.IsNullOrWhiteSpace(result.CreatedEntityId), "battle result writeback should expose the created feedback record id");
    AssertTrue(state.BattleFeedbackRecords.ContainsKey(result.CreatedEntityId), "strategic state should retain the battle feedback record");
    StrategicBattleFeedbackRecord feedback = state.BattleFeedbackRecords[result.CreatedEntityId];
    AssertEqual(summary.SessionId, feedback.SessionId, "feedback should retain bridge session identity");
    AssertEqual(StrategicManagementIds.LocationBeastDen, feedback.TargetLocationId, "feedback should retain target location identity");
    AssertTrue(feedback.Victory, "victory feedback should be marked as victory");
    AssertTrue(feedback.WorldChangeText.Contains("控制", StringComparison.Ordinal), "feedback should explain the world control change");
    AssertTrue(feedback.GetType().GetProperty("PreparationText") == null, "victory feedback should not carry strategic preparation text");
    AssertTrue(feedback.RewardLines.Any(line => line.Contains("野兽", StringComparison.Ordinal)), "victory feedback should show a strategic reward or unlock");
    AssertTrue(feedback.RewardLines.Any(line => line.Contains("白骨号角", StringComparison.Ordinal)), "victory feedback should name the gained equipment sample");
    AssertEqual(3, feedback.EquipmentSamples.Count, "first slice should expose weapon, armor, and token equipment samples");
    AssertTrue(feedback.EquipmentSamples.Any(item => item.SlotKind == "weapon"), "equipment sample set should include a weapon");
    AssertTrue(feedback.EquipmentSamples.Any(item => item.SlotKind == "armor"), "equipment sample set should include armor");
    AssertTrue(feedback.EquipmentSamples.Any(item => item.SlotKind == "token"), "equipment sample set should include a token or command item");
    AssertTrue(feedback.EquipmentSamples.Any(item => item.EquipmentSampleId == StrategicManagementIds.EquipmentBonefieldCommandHorn && item.IsReward), "Bonefield victory should mark one equipment sample as the reward");
    AssertTrue(feedback.HeroFeedback.Any(item =>
            item.HeroId == StrategicManagementIds.HeroOrdinaryCommander &&
            item.ReactionText.Contains("曦盾执旗者", StringComparison.Ordinal)),
        "feedback should include a named selected hero reaction line");
    AssertTrue(feedback.ParticipantFeedback.Any(item =>
            item.CorpsInstanceId == corpsId &&
            item.RemainingCorpsStrength == 72 &&
            item.StrengthLoss == 28),
        "feedback should summarize corps loss from strategic participant results");
    AssertTrue(
        state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceBeastMaterials) > beforeBeastMaterials,
        "victory should grant beast materials as a visible strategic reward");
    AssertTrue(
        state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceBuildingMaterials) > beforeBuildingMaterials,
        "victory should grant building materials as a visible strategic reward");
}

static void StrategicBattleResultRecordsDefeatFeedbackAndRecoveryReason()
{
    var setup = CreateStrategicAssaultExpedition();
    StrategicManagementDefinitionSet definitions = setup.Definitions;
    StrategicManagementState state = setup.State;
    StrategicManagementCommandService commands = setup.Commands;
    string expeditionId = setup.ExpeditionId;
    string corpsId = setup.CorpsInstanceId;
    StrategicBattleBridgeService bridge = new(definitions);
    StrategicBattleSession session = bridge.CreateSession(
        state,
        expeditionId,
        "res://return_to_world.tscn",
        "res://scenes/world/sites/WorldSiteRoot.tscn").Session;
    StrategicBattleResultSummary summary = new()
    {
        SessionId = session.SessionId,
        ExpeditionId = expeditionId,
        TargetLocationId = StrategicManagementIds.LocationBeastDen,
        Outcome = BattleOutcome.Defeat,
        ObjectiveSucceeded = false
    };
    summary.Participants.Add(new StrategicBattleParticipantResult
    {
        HeroId = StrategicManagementIds.HeroOrdinaryCommander,
        CorpsInstanceId = corpsId,
        RemainingCorpsStrength = 0
    });

    StrategicCommandResult result = commands.ApplyBattleResultSummary(state, summary);

    AssertTrue(result.Success, $"defeat summary should apply, got {result.FailureReason}");
    StrategicBattleFeedbackRecord feedback = state.BattleFeedbackRecords[result.CreatedEntityId];
    AssertTrue(!feedback.Victory, "defeat feedback should not be marked as victory");
    AssertTrue(feedback.FailureReasonText.Contains("阵线", StringComparison.Ordinal), "defeat feedback should give an actionable failure reason");
    AssertTrue(feedback.ProgressionText.Contains("重整", StringComparison.Ordinal), "defeat feedback should point to recovery or next-step progression");
    AssertTrue(feedback.RewardLines.Any(line => line.Contains("未获得", StringComparison.Ordinal)), "defeat feedback should explain that Bonefield reward was not gained");
    AssertTrue(feedback.HeroFeedback.Any(item =>
            item.HeroId == StrategicManagementIds.HeroOrdinaryCommander &&
            item.ReactionText.Contains("曦盾执旗者", StringComparison.Ordinal)),
        "defeat feedback should still include named hero reaction");
}

static void StrategicManagementDashboardExposesLatestBattleFeedback()
{
    var setup = CreateStrategicAssaultExpedition();
    StrategicManagementDefinitionSet definitions = setup.Definitions;
    StrategicManagementState state = setup.State;
    StrategicManagementCommandService commands = setup.Commands;
    StrategicManagementRules rules = new(definitions);
    StrategicManagementViewModelService viewModels = new(definitions, rules);
    string expeditionId = setup.ExpeditionId;
    string corpsId = setup.CorpsInstanceId;
    StrategicBattleBridgeService bridge = new(definitions);
    StrategicBattleSession session = bridge.CreateSession(
        state,
        expeditionId,
        "res://return_to_world.tscn",
        "res://scenes/world/sites/WorldSiteRoot.tscn").Session;
    StrategicBattleResultSummary summary = new()
    {
        SessionId = session.SessionId,
        ExpeditionId = expeditionId,
        TargetLocationId = StrategicManagementIds.LocationBeastDen,
        Outcome = BattleOutcome.Victory,
        ObjectiveSucceeded = true
    };
    summary.Participants.Add(new StrategicBattleParticipantResult
    {
        HeroId = StrategicManagementIds.HeroOrdinaryCommander,
        CorpsInstanceId = corpsId,
        RemainingCorpsStrength = 88
    });
    StrategicCommandResult result = commands.ApplyBattleResultSummary(state, summary);
    AssertTrue(result.Success, $"victory summary should apply, got {result.FailureReason}");

    StrategicManagementDashboardViewModel dashboard = viewModels.BuildLocationDashboard(
        state,
        StrategicManagementIds.FactionPlayer,
        StrategicManagementIds.LocationBeastDen);

    AssertEqual(result.CreatedEntityId, dashboard.LatestBattleFeedback.FeedbackId, "dashboard should expose latest feedback for the selected battle target");
    AssertTrue(dashboard.LatestBattleFeedback.RewardLines.Any(line => line.Contains("白骨号角", StringComparison.Ordinal)), "dashboard feedback should show reward equipment text");
    AssertTrue(dashboard.LatestBattleFeedback.HeroFeedback.Any(item => item.ReactionText.Contains("曦盾执旗者", StringComparison.Ordinal)), "dashboard feedback should show named hero reaction");
    AssertTrue(dashboard.LatestBattleFeedback.EquipmentSamples.Any(item => item.IsReward), "dashboard feedback should expose which equipment sample was gained");
}

static void StrategicBattleResultRejectsDuplicateRewardApplication()
{
    var setup = CreateStrategicAssaultExpedition();
    StrategicManagementDefinitionSet definitions = setup.Definitions;
    StrategicManagementState state = setup.State;
    StrategicManagementCommandService commands = setup.Commands;
    string expeditionId = setup.ExpeditionId;
    string corpsId = setup.CorpsInstanceId;
    StrategicBattleBridgeService bridge = new(definitions);
    StrategicBattleSession session = bridge.CreateSession(
        state,
        expeditionId,
        "res://return_to_world.tscn",
        "res://scenes/world/sites/WorldSiteRoot.tscn").Session;
    StrategicBattleResultSummary summary = new()
    {
        SessionId = session.SessionId,
        ExpeditionId = expeditionId,
        TargetLocationId = StrategicManagementIds.LocationBeastDen,
        Outcome = BattleOutcome.Victory,
        ObjectiveSucceeded = true
    };
    summary.Participants.Add(new StrategicBattleParticipantResult
    {
        HeroId = StrategicManagementIds.HeroOrdinaryCommander,
        CorpsInstanceId = corpsId,
        RemainingCorpsStrength = 80
    });
    StrategicCommandResult first = commands.ApplyBattleResultSummary(state, summary);
    AssertTrue(first.Success, $"first result application should succeed, got {first.FailureReason}");
    int beastMaterialsAfterFirst = state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceBeastMaterials);
    int feedbackCountAfterFirst = state.BattleFeedbackRecords.Count;

    StrategicCommandResult duplicate = commands.ApplyBattleResultSummary(state, summary);

    AssertTrue(!duplicate.Success, "duplicate result application should be rejected");
    AssertEqual(StrategicFailureReasons.BattleResultAlreadyApplied, duplicate.FailureReason, "duplicate rejection should be explicit");
    AssertEqual(beastMaterialsAfterFirst, state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceBeastMaterials), "duplicate result must not grant rewards twice");
    AssertEqual(feedbackCountAfterFirst, state.BattleFeedbackRecords.Count, "duplicate result must not create another feedback record");
}

static void StrategicBattleResultGrantsOneTimeTargetRewardAcrossExpeditions()
{
    StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
    StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
    StrategicManagementRules rules = new(definitions);
    StrategicManagementCommandService commands = new(definitions, rules);
    StrategicCommandResult firstExpedition = commands.CreateExpedition(
        state,
        StrategicManagementIds.LocationPlainsCity,
        StrategicManagementIds.LocationBeastDen,
        StrategicExpeditionIntent.AssaultLocation,
        StrategicManagementIds.HeroOrdinaryCommander);
    AssertTrue(firstExpedition.Success, $"first expedition should be created, got {firstExpedition.FailureReason}");
    StrategicCommandResult secondExpedition = commands.CreateExpedition(
        state,
        StrategicManagementIds.LocationPlainsCity,
        StrategicManagementIds.LocationBeastDen,
        StrategicExpeditionIntent.AssaultLocation,
        StrategicManagementIds.HeroArcherCaptain);
    AssertTrue(secondExpedition.Success, $"second simultaneous expedition should be created, got {secondExpedition.FailureReason}");
    StrategicBattleBridgeService bridge = new(definitions);
    int initialBeastMaterials = state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceBeastMaterials);

    StrategicCommandResult firstResult = ApplyVictoryForSingleHeroExpedition(
        definitions,
        state,
        commands,
        bridge,
        firstExpedition.CreatedEntityId,
        StrategicManagementIds.HeroOrdinaryCommander,
        "request_one_time_reward_a");
    AssertTrue(firstResult.Success, $"first victory should apply, got {firstResult.FailureReason}");
    int afterFirstReward = state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceBeastMaterials);
    AssertTrue(afterFirstReward > initialBeastMaterials, "first Bonefield victory should grant the target reward");

    StrategicCommandResult secondResult = ApplyVictoryForSingleHeroExpedition(
        definitions,
        state,
        commands,
        bridge,
        secondExpedition.CreatedEntityId,
        StrategicManagementIds.HeroArcherCaptain,
        "request_one_time_reward_b");

    AssertTrue(secondResult.Success, $"second victory should still resolve, got {secondResult.FailureReason}");
    AssertEqual(afterFirstReward, state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceBeastMaterials), "second Bonefield victory must not grant the one-time reward again");
    AssertTrue(
        state.BattleFeedbackRecords[secondResult.CreatedEntityId].RewardLines.Any(line => line.Contains("已领取", StringComparison.Ordinal)),
        "second feedback should explain that the one-time reward was already claimed");
}

static void StrategicBattleResultRejectsNullParticipantSummaryWithoutMutation()
{
    var setup = CreateStrategicAssaultExpedition();
    StrategicManagementState state = setup.State;
    string expeditionId = setup.ExpeditionId;
    string corpsId = setup.CorpsInstanceId;
    StrategicBattleResultSummary summary = new()
    {
        SessionId = "session_null_participant",
        ExpeditionId = expeditionId,
        TargetLocationId = StrategicManagementIds.LocationBeastDen,
        Outcome = BattleOutcome.Victory,
        ObjectiveSucceeded = true
    };
    summary.Participants.Add(null);

    StrategicCommandResult result = setup.Commands.ApplyBattleResultSummary(state, summary);

    AssertTrue(!result.Success, "summary with null participant result should be rejected");
    AssertEqual(StrategicFailureReasons.MissingBattleParticipantResult, result.FailureReason, "null participant rejection should be explicit");
    AssertEqual(StrategicManagementIds.FactionEnemy, state.Locations[StrategicManagementIds.LocationBeastDen].OwnerFactionId, "null participant result must not transfer location control");
    AssertEqual(StrategicExpeditionStatus.Moving, state.Expeditions[expeditionId].Status, "null participant result must not resolve expedition");
    AssertEqual(100, state.CorpsInstances[corpsId].Strength, "null participant result must not mutate corps strength");
    AssertEqual(0, state.BattleFeedbackRecords.Count, "null participant result must not create feedback");

    summary.Participants = null;
    StrategicCommandResult nullListResult = setup.Commands.ApplyBattleResultSummary(state, summary);
    AssertTrue(!nullListResult.Success, "summary with null participant list should be rejected");
    AssertEqual(StrategicFailureReasons.MissingBattleParticipantResult, nullListResult.FailureReason, "null participant list rejection should be explicit");
    AssertEqual(StrategicExpeditionStatus.Moving, state.Expeditions[expeditionId].Status, "null participant list must not resolve expedition");
}

static void StrategicBattleResultSummaryAppliesDefeatConsequences()
{
    var setup = CreateStrategicAssaultExpedition();
    StrategicManagementDefinitionSet definitions = setup.Definitions;
    StrategicManagementState state = setup.State;
    StrategicManagementCommandService commands = setup.Commands;
    string expeditionId = setup.ExpeditionId;
    string corpsId = setup.CorpsInstanceId;
    StrategicBattleBridgeService bridge = new(definitions);
    StrategicBattleSession session = bridge.CreateSession(
        state,
        expeditionId,
        "res://return_to_world.tscn",
        "res://scenes/world/sites/WorldSiteRoot.tscn").Session;
    StrategicBattleResultSummary summary = new()
    {
        SessionId = session.SessionId,
        ExpeditionId = expeditionId,
        TargetLocationId = StrategicManagementIds.LocationBeastDen,
        Outcome = BattleOutcome.Defeat,
        ObjectiveSucceeded = false
    };
    summary.Participants.Add(new StrategicBattleParticipantResult
    {
        HeroId = StrategicManagementIds.HeroOrdinaryCommander,
        CorpsInstanceId = corpsId,
        RemainingCorpsStrength = 0
    });

    StrategicCommandResult result = commands.ApplyBattleResultSummary(state, summary);

    AssertTrue(result.Success, $"defeat summary should apply, got {result.FailureReason}");
    AssertEqual(StrategicManagementIds.FactionEnemy, state.Locations[StrategicManagementIds.LocationBeastDen].OwnerFactionId, "defeat should not transfer the target location");
    AssertEqual(StrategicLocationControlState.EnemyHeld, state.Locations[StrategicManagementIds.LocationBeastDen].ControlState, "defeat should leave target enemy-held");
    AssertEqual(StrategicExpeditionStatus.Resolved, state.Expeditions[expeditionId].Status, "defeat should still resolve the expedition");
    AssertEqual("", state.Heroes[StrategicManagementIds.HeroOrdinaryCommander].CurrentExpeditionId, "defeat should unlock the hero from active expedition");
    AssertEqual("", state.CorpsInstances[corpsId].CurrentExpeditionId, "defeat should unlock the corps from active expedition");
    AssertEqual(0, state.CorpsInstances[corpsId].Strength, "defeat should write routed corps strength");
    AssertEqual(StrategicCorpsInstanceStatus.Routed, state.CorpsInstances[corpsId].Status, "zero-strength corps should become routed instead of disappearing");
}

static void StrategicBattleResultSummaryAppliesAllExpeditionParticipantConsequences()
{
    StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
    StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
    StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
    string[] heroIds =
    {
        StrategicManagementIds.HeroOrdinaryCommander,
        StrategicManagementIds.HeroArcherCaptain,
        StrategicManagementIds.HeroBeastTamer
    };
    StrategicCommandResult expedition = commands.CreateExpedition(
        state,
        StrategicManagementIds.LocationPlainsCity,
        StrategicManagementIds.LocationBeastDen,
        StrategicExpeditionIntent.AssaultLocation,
        heroIds);
    AssertTrue(expedition.Success, $"multi-company expedition should be created, got {expedition.FailureReason}");
    StrategicBattleBridgeService bridge = new(definitions);
    StrategicBattleSession session = bridge.CreateSession(
        state,
        expedition.CreatedEntityId,
        "res://return_to_world.tscn",
        "res://scenes/world/sites/WorldSiteRoot.tscn").Session;
    BattleStartRequest request = new()
    {
        RequestId = "request_multi_participant",
        BattleKind = BattleKind.AssaultSite
    };
    foreach (string heroId in heroIds)
    {
        StrategicHeroState hero = state.Heroes[heroId];
        StrategicCorpsInstanceState corps = state.CorpsInstances[hero.AssignedCorpsInstanceId];
        request.PlayerForces.Add(new BattleForceRequest
        {
            ForceId = $"{heroId}:hero",
            UnitDefinitionId = definitions.Heroes[hero.HeroDefinitionId].BattleUnitId,
            Count = 1
        });
        request.PlayerForces.Add(new BattleForceRequest
        {
            ForceId = $"{corps.CorpsInstanceId}:corps",
            UnitDefinitionId = definitions.Corps[corps.CorpsDefinitionId].BattleUnitId,
            Count = definitions.Corps[corps.CorpsDefinitionId].BattleUnitCount
        });
    }

    bridge.AttachSessionToLegacyRequest(session, request);
    AssertForceMappedToHero(request, StrategicManagementIds.HeroOrdinaryCommander);
    AssertForceMappedToHero(request, StrategicManagementIds.HeroArcherCaptain);
    AssertForceMappedToHero(request, StrategicManagementIds.HeroBeastTamer);
    BattleResult battleResult = new()
    {
        RequestId = request.RequestId,
        BattleKind = BattleKind.AssaultSite,
        Outcome = BattleOutcome.Victory
    };
    foreach (BattleForceRequest force in request.PlayerForces)
    {
        int survived = force.StrategicHeroId switch
        {
            StrategicManagementIds.HeroOrdinaryCommander => force.Count,
            StrategicManagementIds.HeroArcherCaptain => force.Count == 1 ? 1 : 1,
            StrategicManagementIds.HeroBeastTamer => 0,
            _ => 0
        };
        battleResult.ForceResults.Add(new BattleForceResult
        {
            ForceId = force.ForceId,
            InitialCount = force.Count,
            SurvivedCount = survived
        });
    }

    StrategicBattleResultSummary summary = bridge.BuildResultSummary(request, battleResult);
    StrategicCommandResult result = commands.ApplyBattleResultSummary(state, summary);

    AssertTrue(result.Success, $"multi-participant battle result should apply, got {result.FailureReason}");
    AssertEqual(StrategicLocationControlState.PlayerHeld, state.Locations[StrategicManagementIds.LocationBeastDen].ControlState, "victory should still occupy the target");
    AssertEqual(100, FindAssignedCorps(state, StrategicManagementIds.HeroOrdinaryCommander).Strength, "ordinary commander corps should keep full strength");
    AssertEqual(50, FindAssignedCorps(state, StrategicManagementIds.HeroArcherCaptain).Strength, "archer captain corps should keep proportional remaining strength");
    AssertEqual(0, FindAssignedCorps(state, StrategicManagementIds.HeroBeastTamer).Strength, "beast tamer corps should be routed");
    AssertEqual(StrategicCorpsInstanceStatus.Routed, FindAssignedCorps(state, StrategicManagementIds.HeroBeastTamer).Status, "zero-strength participant should route");
    foreach (string heroId in heroIds)
    {
        AssertEqual("", state.Heroes[heroId].CurrentExpeditionId, $"{heroId} should be unlocked after battle result writeback");
    }
}

static void StrategicBattleResultSummaryRejectsMismatchedResult()
{
    var setup = CreateStrategicAssaultExpedition();
    StrategicManagementDefinitionSet definitions = setup.Definitions;
    StrategicManagementState state = setup.State;
    StrategicBattleBridgeService bridge = new(definitions);
    StrategicBattleSession session = bridge.CreateSession(
        state,
        setup.ExpeditionId,
        "res://return_to_world.tscn",
        "res://scenes/world/sites/WorldSiteRoot.tscn").Session;
    BattleStartRequest request = new()
    {
        RequestId = "request_a",
        BattleKind = BattleKind.AssaultSite,
        StrategicBattleSessionId = session.SessionId,
        StrategicExpeditionId = session.ExpeditionId,
        StrategicTargetLocationId = session.TargetLocationId
    };
    request.PlayerForces.Add(new BattleForceRequest
    {
        ForceId = "force_a",
        Count = 1,
        StrategicCorpsInstanceId = setup.CorpsInstanceId,
        StrategicPreBattleCorpsStrength = 100
    });

    BattleResult mismatched = new()
    {
        RequestId = "request_b",
        BattleKind = BattleKind.AssaultSite,
        Outcome = BattleOutcome.Victory
    };
    mismatched.ForceResults.Add(new BattleForceResult
    {
        ForceId = "force_a",
        InitialCount = 1,
        SurvivedCount = 1
    });

    StrategicBattleResultSummary summary = bridge.BuildResultSummary(request, mismatched);

    AssertEqual(0, summary.Participants.Count, "mismatched battle results must not produce participant consequences");
    StrategicCommandResult result = setup.Commands.ApplyBattleResultSummary(state, summary);
    AssertTrue(!result.Success, "mismatched summary should be rejected by Strategic Management");
    AssertEqual(StrategicFailureReasons.MissingBattleParticipantResult, result.FailureReason, "rejection should be missing mapped participant result");
    AssertEqual(StrategicManagementIds.FactionEnemy, state.Locations[StrategicManagementIds.LocationBeastDen].OwnerFactionId, "mismatched result must not transfer location control");
    AssertEqual(StrategicExpeditionStatus.Moving, state.Expeditions[setup.ExpeditionId].Status, "mismatched result must not resolve expedition");
}

static void StrategicBattleResultSummaryRejectsMissingParticipantResult()
{
    var setup = CreateStrategicAssaultExpedition();
    StrategicManagementState state = setup.State;
    StrategicBattleResultSummary summary = new()
    {
        SessionId = "session_missing_participant",
        ExpeditionId = setup.ExpeditionId,
        TargetLocationId = StrategicManagementIds.LocationBeastDen,
        Outcome = BattleOutcome.Victory,
        ObjectiveSucceeded = true
    };

    StrategicCommandResult result = setup.Commands.ApplyBattleResultSummary(state, summary);

    AssertTrue(!result.Success, "summary without mapped participant result should be rejected");
    AssertEqual(StrategicFailureReasons.MissingBattleParticipantResult, result.FailureReason, "missing participant rejection should be explicit");
    AssertEqual(StrategicManagementIds.FactionEnemy, state.Locations[StrategicManagementIds.LocationBeastDen].OwnerFactionId, "missing participant result must not transfer location control");
    AssertEqual(StrategicExpeditionStatus.Moving, state.Expeditions[setup.ExpeditionId].Status, "missing participant result must not resolve expedition");
    AssertEqual(100, state.CorpsInstances[setup.CorpsInstanceId].Strength, "missing participant result must not fabricate corps survival or losses");
}

static void StrategicBattleBridgeRejectsLocationWithoutBattleEntryMetadata()
{
    var setup = CreateStrategicAssaultExpedition();
    StrategicManagementDefinitionSet definitions = setup.Definitions;
    StrategicManagementState state = setup.State;
    string expeditionId = setup.ExpeditionId;
    definitions.Locations[StrategicManagementIds.LocationBeastDen].BattleMapDefinitionId = "";
    StrategicBattleBridgeService bridge = new(definitions);

    StrategicBattleSessionResult result = bridge.CreateSession(
        state,
        expeditionId,
        "res://return_to_world.tscn",
        "res://scenes/world/sites/WorldSiteRoot.tscn");

    AssertTrue(!result.Success, "bridge session should fail when target lacks battle entry metadata");
    AssertEqual(StrategicFailureReasons.MissingBattleEntryMetadata, result.FailureReason, "bridge failure reason should explain missing battle metadata");
    AssertEqual(StrategicExpeditionStatus.Moving, state.Expeditions[expeditionId].Status, "failed bridge session creation must not mutate expedition state");
}

static void StrategicManagementDashboardSummarizesCityResourcesFacilitiesCorpsAndHeroes()
{
    StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
    StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
    StrategicManagementViewModelService viewModels = new(definitions, new StrategicManagementRules(definitions));

    StrategicManagementDashboardViewModel dashboard = viewModels.BuildDashboard(
        state,
        StrategicManagementIds.FactionPlayer,
        StrategicManagementIds.LocationPlainsCity);

    AssertEqual(StrategicManagementIds.FactionPlayer, dashboard.FactionId, "dashboard should preserve faction scope");
    AssertEqual(StrategicManagementIds.LocationPlainsCity, dashboard.SelectedCity.LocationId, "dashboard should preserve selected city");
    AssertEqual("苍原城", dashboard.SelectedCity.DisplayName, "city display name should come from definitions");
    AssertEqual("平原人类城池", dashboard.SelectedCity.CityIdentityDisplayName, "city identity display name should come from definitions");
    AssertEqual(0, dashboard.SelectedCity.FacilitySlotsUsed, "new city should have no built facilities");
    AssertEqual(3, dashboard.SelectedCity.FacilitySlotsTotal, "city should expose total facility slots");
    AssertEqual(500, FindResource(dashboard, StrategicManagementIds.ResourceMoney).Amount, "money should be summarized");
    AssertTrue(FindFacilityOption(dashboard, StrategicManagementIds.FacilityTrainingGround).CanBuild, "training ground should be buildable initially");
    AssertTrue(FindMusterTemplate(dashboard, StrategicManagementIds.CorpsShieldLine).CanCreate, "shield line should be creatable initially");
    AssertEqual(3, dashboard.Heroes.Count, "dashboard should expose the first playable strategic heroes");
    AssertTrue(
        !string.IsNullOrWhiteSpace(FindHero(dashboard, StrategicManagementIds.HeroBeastTamer).AssignedCorpsInstanceId),
        "beast tamer should start with an assigned cavalry-style corps company");
}

static void StrategicManagementDashboardExposesDispatchableHeroCompanies()
{
    StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
    StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
    StrategicManagementRules rules = new(definitions);
    StrategicManagementCommandService commands = new(definitions, rules);
    StrategicManagementViewModelService viewModels = new(definitions, rules);
    string corpsInstanceId = state.Heroes[StrategicManagementIds.HeroOrdinaryCommander].AssignedCorpsInstanceId;

    StrategicManagementDashboardViewModel beforeDispatch = viewModels.BuildDashboard(
        state,
        StrategicManagementIds.FactionPlayer,
        StrategicManagementIds.LocationPlainsCity);
    StrategicHeroCompanyViewModel company = FindHeroCompany(beforeDispatch, StrategicManagementIds.HeroOrdinaryCommander);
    AssertTrue(company.CanCreateExpedition, $"assigned hero company should be dispatchable, got {company.DisabledReason}");
    AssertEqual(corpsInstanceId, company.CorpsInstanceId, "hero company should expose the assigned corps instance");
    AssertEqual(StrategicManagementIds.CorpsShieldLine, company.CorpsDefinitionId, "hero company should expose the corps definition");

    commands.CreateExpedition(
        state,
        StrategicManagementIds.LocationPlainsCity,
        StrategicManagementIds.LocationBeastDen,
        StrategicExpeditionIntent.AssaultLocation,
        StrategicManagementIds.HeroOrdinaryCommander);
    StrategicManagementDashboardViewModel afterDispatch = viewModels.BuildDashboard(
        state,
        StrategicManagementIds.FactionPlayer,
        StrategicManagementIds.LocationPlainsCity);
    StrategicHeroCompanyViewModel dispatched = FindHeroCompany(afterDispatch, StrategicManagementIds.HeroOrdinaryCommander);
    AssertTrue(!dispatched.CanCreateExpedition, "expedition hero company should no longer be dispatchable");
    AssertEqual(StrategicFailureReasons.HeroAlreadyOnExpedition, dispatched.DisabledReason, "disabled reason should explain expedition lock");
}

static void StrategicManagementDashboardExplainsUnavailableBeastMusterReasons()
{
    StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
    StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
    StrategicManagementViewModelService viewModels = new(definitions, new StrategicManagementRules(definitions));

    StrategicMusterTemplateViewModel wolfPack = FindMusterTemplate(
        viewModels.BuildDashboard(state, StrategicManagementIds.FactionPlayer, StrategicManagementIds.LocationPlainsCity),
        StrategicManagementIds.CorpsWolfPack);

    AssertTrue(!wolfPack.CanCreate, "wolf pack should start unavailable");
    AssertContains(wolfPack.DisabledReasons, StrategicFailureReasons.MissingSourcePermission, "wolf pack should explain missing beast source");
    AssertContains(wolfPack.DisabledReasons, StrategicFailureReasons.MissingFacility, "wolf pack should explain missing beast pen");
    AssertEqual("霜魂狼群", wolfPack.DisplayName, "muster template display name should come from corps definition");
}

static void StrategicManagementDashboardReflectsCommandMutations()
{
    StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
    StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
    StrategicManagementRules rules = new(definitions);
    StrategicManagementCommandService commands = new(definitions, rules);
    StrategicManagementViewModelService viewModels = new(definitions, rules);

    commands.OccupyLocation(state, StrategicManagementIds.LocationBeastDen, StrategicManagementIds.FactionPlayer);
    commands.BuildFacility(state, StrategicManagementIds.LocationPlainsCity, StrategicManagementIds.FacilityBeastPen);
    StrategicCommandResult unassign = commands.UnassignCorpsFromHero(state, StrategicManagementIds.HeroBeastTamer);
    AssertTrue(unassign.Success, $"test setup unassignment should succeed, got {unassign.FailureReason}");
    StrategicCommandResult create = commands.CreateCorps(
        state,
        StrategicManagementIds.LocationPlainsCity,
        StrategicManagementIds.CorpsWolfPack);
    commands.AssignCorpsToHero(state, StrategicManagementIds.HeroBeastTamer, create.CreatedEntityId);

    StrategicManagementDashboardViewModel dashboard = viewModels.BuildDashboard(
        state,
        StrategicManagementIds.FactionPlayer,
        StrategicManagementIds.LocationPlainsCity);

    AssertEqual(1, dashboard.SelectedCity.FacilitySlotsUsed, "built beast pen should consume one city slot");
    AssertEqual(StrategicManagementIds.FacilityBeastPen, dashboard.SelectedCity.BuiltFacilities[0].FacilityDefinitionId, "built facility should be listed");
    AssertTrue(FindMusterTemplate(dashboard, StrategicManagementIds.CorpsWolfPack).CanCreate, "wolf pack should be available after source and facility unlock");
    StrategicCorpsInstanceViewModel corps = FindCorps(dashboard, create.CreatedEntityId);
    AssertEqual("霜魂狼群", corps.DisplayName, "created corps should use definition display name");
    AssertEqual(StrategicCorpsInstanceStatus.AssignedToHero, corps.Status, "assigned corps status should be reflected");
    StrategicHeroAssignmentViewModel hero = FindHero(dashboard, StrategicManagementIds.HeroBeastTamer);
    AssertEqual(create.CreatedEntityId, hero.AssignedCorpsInstanceId, "hero row should show assigned corps");
    AssertEqual("霜魂狼群", hero.AssignedCorpsDisplayName, "hero row should show assigned corps display name");
    AssertEqual(StrategicHeroCorpsAptitudeGrade.A, hero.AptitudeGrade, "hero row should show derived aptitude");
}

static void StrategicManagementDashboardSummarizesNonCityLocation()
{
    StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
    StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
    StrategicManagementRules rules = new(definitions);
    StrategicManagementCommandService commands = new(definitions, rules);
    StrategicManagementViewModelService viewModels = new(definitions, rules);

    StrategicManagementDashboardViewModel timberDashboard = InvokeLocationDashboard(
        viewModels,
        state,
        StrategicManagementIds.FactionPlayer,
        StrategicManagementIds.LocationTimberSite);
    object timberLocation = GetRequiredProperty<object>(timberDashboard, "SelectedLocation");

    AssertEqual(StrategicManagementIds.LocationTimberSite, GetRequiredProperty<string>(timberLocation, "LocationId"), "location dashboard should preserve selected resource site id");
    AssertEqual("", GetRequiredProperty<string>(timberLocation, "MapSiteId"), "timber resource site is strategic-only in the first map and should not claim the Bonefield map-site id");
    AssertEqual("旧林伐场", GetRequiredProperty<string>(timberLocation, "DisplayName"), "location dashboard should use definition display name");
    AssertEqual(StrategicLocationKind.ResourceSite, GetRequiredProperty<StrategicLocationKind>(timberLocation, "Kind"), "timber site should stay a resource site");
    AssertEqual("资源点", GetRequiredProperty<string>(timberLocation, "KindDisplayName"), "resource-site kind should have a player-readable display name");
    AssertEqual(false, GetRequiredProperty<bool>(timberLocation, "CanManageCity"), "resource site should not open city management");
    AssertEqual(StrategicManagementIds.FactionPlayer, GetRequiredProperty<string>(timberLocation, "OwnerFactionId"), "timber site owner should come from state");
    AssertEqual(StrategicLocationControlState.PlayerHeld, GetRequiredProperty<StrategicLocationControlState>(timberLocation, "ControlState"), "timber site control should come from state");
    AssertEqual("玩家控制", GetRequiredProperty<string>(timberLocation, "ControlStateDisplayName"), "player-held control should have a readable label");
    AssertEqual("", timberDashboard.SelectedCity.LocationId, "non-city location dashboard should not pretend to select a managed city");
    AssertEqual(500, FindResource(timberDashboard, StrategicManagementIds.ResourceMoney).Amount, "location dashboard should still show shared faction resources");

    StrategicManagementDashboardViewModel beastDashboard = InvokeLocationDashboard(
        viewModels,
        state,
        StrategicManagementIds.FactionPlayer,
        StrategicManagementIds.LocationBeastDen);
    object beastLocation = GetRequiredProperty<object>(beastDashboard, "SelectedLocation");
    AssertEqual(StrategicManagementIds.MapSiteBonefield, GetRequiredProperty<string>(beastLocation, "MapSiteId"), "beast den should expose the Bonefield map-site id");
    AssertEqual(StrategicLocationKind.BeastMinorSite, GetRequiredProperty<StrategicLocationKind>(beastLocation, "Kind"), "beast den should stay a beast minor site");
    AssertContains(
        GetRequiredProperty<IReadOnlyCollection<string>>(beastLocation, "SourcePermissionTags"),
        StrategicManagementIds.SourceTagBeast,
        "beast den should expose beast source permission");
    AssertEqual("野兽来源", GetRequiredProperty<string>(beastLocation, "SourcePermissionDisplayText"), "source permission text should expose player-readable source tags");
    AssertEqual(StrategicManagementIds.FactionEnemy, GetRequiredProperty<string>(beastLocation, "OwnerFactionId"), "beast den should start enemy-held");
    AssertEqual(StrategicLocationControlState.EnemyHeld, GetRequiredProperty<StrategicLocationControlState>(beastLocation, "ControlState"), "beast den should start enemy-held");

    StrategicCommandResult occupy = commands.OccupyLocation(
        state,
        StrategicManagementIds.LocationBeastDen,
        StrategicManagementIds.FactionPlayer);
    AssertTrue(occupy.Success, "occupying beast den should succeed before dashboard refresh");

    StrategicManagementDashboardViewModel occupiedBeastDashboard = InvokeLocationDashboard(
        viewModels,
        state,
        StrategicManagementIds.FactionPlayer,
        StrategicManagementIds.LocationBeastDen);
    object occupiedBeastLocation = GetRequiredProperty<object>(occupiedBeastDashboard, "SelectedLocation");
    AssertEqual(StrategicManagementIds.FactionPlayer, GetRequiredProperty<string>(occupiedBeastLocation, "OwnerFactionId"), "location dashboard should reflect command-mutated owner");
    AssertEqual(StrategicLocationControlState.PlayerHeld, GetRequiredProperty<StrategicLocationControlState>(occupiedBeastLocation, "ControlState"), "location dashboard should reflect command-mutated control");

    StrategicManagementRuntime.Reset();
    StrategicManagementDashboardViewModel runtimeDashboard = InvokeRuntimeLocationDashboard(
        StrategicManagementIds.FactionPlayer,
        StrategicManagementIds.LocationTimberSite);
    object runtimeLocation = GetRequiredProperty<object>(runtimeDashboard, "SelectedLocation");
    AssertEqual(StrategicManagementIds.LocationTimberSite, GetRequiredProperty<string>(runtimeLocation, "LocationId"), "runtime location dashboard should use retained Strategic Management state");
}

static void StrategicManagementSettlesControlledResourceSiteProduction()
{
    StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
    StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
    StrategicManagementRules rules = new(definitions);
    StrategicManagementCommandService commands = new(definitions, rules);
    StrategicManagementViewModelService viewModels = new(definitions, rules);

    StrategicLocationDefinition timberDefinition = definitions.Locations[StrategicManagementIds.LocationTimberSite];
    IReadOnlyCollection<StrategicResourceAmount> productionPerWorldTimePulse =
        GetRequiredProperty<IReadOnlyCollection<StrategicResourceAmount>>(timberDefinition, "ProductionPerWorldTimePulse");
    AssertEqual(
        12,
        FindStrategicAmount(productionPerWorldTimePulse, StrategicManagementIds.ResourceBuildingMaterials),
        "timber site should define first-slice building-materials production");

    IReadOnlyList<StrategicResourceAmount> projected = InvokeLocationProduction(
        rules,
        state,
        StrategicManagementIds.LocationTimberSite,
        StrategicManagementIds.FactionPlayer,
        2);
    AssertEqual(
        24,
        FindStrategicAmount(projected, StrategicManagementIds.ResourceBuildingMaterials),
        "rules should project timber production by requested elapsed world-map pulses");

    StrategicManagementDashboardViewModel dashboard = viewModels.BuildLocationDashboard(
        state,
        StrategicManagementIds.FactionPlayer,
        StrategicManagementIds.LocationTimberSite);
    object location = GetRequiredProperty<object>(dashboard, "SelectedLocation");
    AssertEqual(
        "建材 +12 / 大地图时间",
        GetRequiredProperty<string>(location, "ProductionDisplayText"),
        "location dashboard should expose production summary");
    IEnumerable<object> productionView = GetRequiredProperty<IEnumerable<object>>(location, "ProductionPerWorldTimePulse");
    AssertEqual(
        12,
        FindReflectedAmount(productionView, StrategicManagementIds.ResourceBuildingMaterials),
        "location dashboard should expose production amounts as view models");

    int beforeMaterials = state.GetResourceAmount(
        StrategicManagementIds.FactionPlayer,
        StrategicManagementIds.ResourceBuildingMaterials);
    StrategicCommandResult settle = InvokeSettleLocationProduction(
        commands,
        state,
        StrategicManagementIds.LocationTimberSite,
        StrategicManagementIds.FactionPlayer,
        2);
    AssertTrue(settle.Success, $"settling player-held timber production should succeed, got {settle.FailureReason}");
    AssertEqual(
        beforeMaterials + 24,
        state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceBuildingMaterials),
        "settlement should add production into faction-shared resources");
    AssertTrue(
        settle.Events.Any(item => item.Kind == "StrategicLocationProductionSettled"),
        "settlement command should emit a low-noise production event");

    StrategicCommandResult lose = commands.LoseLocation(
        state,
        StrategicManagementIds.LocationTimberSite,
        StrategicManagementIds.FactionEnemy);
    AssertTrue(lose.Success, "losing timber site should succeed before rejection check");

    int beforeRejectedMaterials = state.GetResourceAmount(
        StrategicManagementIds.FactionPlayer,
        StrategicManagementIds.ResourceBuildingMaterials);
    StrategicCommandResult rejected = InvokeSettleLocationProduction(
        commands,
        state,
        StrategicManagementIds.LocationTimberSite,
        StrategicManagementIds.FactionPlayer,
        1);
    AssertTrue(!rejected.Success, "player production settlement should fail after the resource site is enemy-held");
    AssertEqual(StrategicFailureReasons.FactionMismatch, rejected.FailureReason, "enemy-held production rejection should report faction mismatch");
    AssertEqual(
        beforeRejectedMaterials,
        state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceBuildingMaterials),
        "rejected settlement must not mutate resources");
}

static void StrategicManagementUsesElapsedWorldTimeNamingInsteadOfStepNaming()
{
    AssertTrue(
        typeof(StrategicManagementState).GetProperty("ElapsedWorldTimePulses") != null,
        "strategic state should expose elapsed world-map time pulses");
    AssertTrue(
        typeof(StrategicManagementState).GetProperty("StrategicStep") == null,
        "strategic state should not expose step-oriented time naming");
    AssertTrue(
        typeof(StrategicLocationDefinition).GetProperty("ProductionPerWorldTimePulse") != null,
        "location definitions should name passive production by world-time pulses");
    AssertTrue(
        typeof(StrategicLocationDefinition).GetProperty("ProductionPerStep") == null,
        "location definitions should not expose step-oriented production naming");
    AssertTrue(
        typeof(StrategicLocationDashboardViewModel).GetProperty("ProductionPerWorldTimePulse") != null,
        "location dashboards should name passive production by world-time pulses");
    AssertTrue(
        typeof(StrategicLocationDashboardViewModel).GetProperty("ProductionPerStep") == null,
        "location dashboards should not expose step-oriented production naming");
    AssertTrue(
        typeof(StrategicFailureReasons).GetField("InvalidStepCount") == null,
        "strategic failures should not keep step-count rejection naming");
    AssertTrue(
        typeof(StrategicManagementCommandService).GetMethod(
            "SettleElapsedWorldTime",
            new[] { typeof(StrategicManagementState), typeof(string), typeof(int) }) != null,
        "command service should expose SettleElapsedWorldTime(state, factionId, elapsedPulses)");
    AssertTrue(
        typeof(StrategicManagementCommandService).GetMethod(
            "AdvanceStrategicStep",
            new[] { typeof(StrategicManagementState), typeof(string), typeof(int) }) == null,
        "command service should not keep AdvanceStrategicStep as a public compatibility wrapper");
    AssertTrue(
        typeof(StrategicManagementRuntime).GetMethod("SettleElapsedWorldTime", new[] { typeof(int) }) != null,
        "runtime should expose SettleElapsedWorldTime(elapsedPulses)");
    AssertTrue(
        typeof(StrategicManagementRuntime).GetMethod("AdvanceStrategicStep", new[] { typeof(string), typeof(int) }) == null,
        "runtime should not keep AdvanceStrategicStep as a public compatibility wrapper");
}

static void StrategicManagementSettlesElapsedWorldTimeAndControlledProduction()
{
    StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
    StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
    StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
    int beforeMaterials = state.GetResourceAmount(
        StrategicManagementIds.FactionPlayer,
        StrategicManagementIds.ResourceBuildingMaterials);

    AssertEqual(0, GetElapsedWorldTimePulses(state), "player start should begin before elapsed world-map time has been settled");

    StrategicCommandResult result = InvokeSettleElapsedWorldTime(
        commands,
        state,
        StrategicManagementIds.FactionPlayer,
        2);

    AssertTrue(result.Success, $"settling elapsed world time should succeed, got {result.FailureReason}");
    AssertEqual(2, GetElapsedWorldTimePulses(state), "settlement should add requested pulses to durable world-map time");
    AssertEqual(
        beforeMaterials + 24,
        state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceBuildingMaterials),
        "controlled resource-site production should be settled for every elapsed pulse");
    AssertTrue(
        result.Events.Any(item => item.Kind == "StrategicWorldTimeSettled"),
        "elapsed-time settlement should emit a command-level time event");
    AssertTrue(
        result.Events.Any(item => item.Kind == "StrategicLocationProductionSettled"),
        "global advancement should include production-settlement events for producing locations");
}

static void StrategicManagementElapsedWorldTimeSkipsEnemyHeldProduction()
{
    StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
    StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
    StrategicManagementRules rules = new(definitions);
    StrategicManagementCommandService commands = new(definitions, rules);
    StrategicCommandResult lose = commands.LoseLocation(
        state,
        StrategicManagementIds.LocationTimberSite,
        StrategicManagementIds.FactionEnemy);
    AssertTrue(lose.Success, "losing timber site should succeed before advancement");
    int beforeMaterials = state.GetResourceAmount(
        StrategicManagementIds.FactionPlayer,
        StrategicManagementIds.ResourceBuildingMaterials);

    StrategicCommandResult result = InvokeSettleElapsedWorldTime(
        commands,
        state,
        StrategicManagementIds.FactionPlayer,
        1);

    AssertTrue(result.Success, $"settling time without controlled production should still succeed, got {result.FailureReason}");
    AssertEqual(1, GetElapsedWorldTimePulses(state), "world-map time should advance even when no controlled production is available");
    AssertEqual(
        beforeMaterials,
        state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceBuildingMaterials),
        "enemy-held resource sites must not produce for the player");
    AssertTrue(
        !result.Events.Any(item => item.Kind == "StrategicLocationProductionSettled"),
        "enemy-held sites should not emit player production settlement events");
}

static void StrategicManagementElapsedWorldTimeRejectsInvalidPulseCountWithoutMutation()
{
    StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
    StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
    StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
    int beforePulses = GetElapsedWorldTimePulses(state);
    int beforeMaterials = state.GetResourceAmount(
        StrategicManagementIds.FactionPlayer,
        StrategicManagementIds.ResourceBuildingMaterials);

    StrategicCommandResult result = InvokeSettleElapsedWorldTime(
        commands,
        state,
        StrategicManagementIds.FactionPlayer,
        0);

    AssertTrue(!result.Success, "settling zero elapsed pulses should fail");
    AssertEqual("invalid_elapsed_world_time_pulses", result.FailureReason, "invalid elapsed pulse count should be explicit");
    AssertEqual(beforePulses, GetElapsedWorldTimePulses(state), "failed settlement must not mutate strategic time");
    AssertEqual(
        beforeMaterials,
        state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceBuildingMaterials),
        "failed settlement must not mutate resources");
}

static void StrategicManagementRuntimeBlocksElapsedTimeWhileCityManagementPaused()
{
    StrategicManagementRuntime.Reset();
    InvokeRuntimePauseWorldTimeForCityManagement();
    int beforePulses = GetElapsedWorldTimePulses(StrategicManagementRuntime.State);
    int beforeMaterials = StrategicManagementRuntime.State.GetResourceAmount(
        StrategicManagementIds.FactionPlayer,
        StrategicManagementIds.ResourceBuildingMaterials);

    StrategicCommandResult result = InvokeRuntimeSettleElapsedWorldTime(1);

    AssertTrue(!result.Success, "runtime elapsed-time settlement should fail while city management pauses world time");
    AssertEqual("world_time_paused", result.FailureReason, "paused settlement should report the pause boundary");
    AssertEqual(beforePulses, GetElapsedWorldTimePulses(StrategicManagementRuntime.State), "paused settlement must not mutate retained strategic time");
    AssertEqual(
        beforeMaterials,
        StrategicManagementRuntime.State.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceBuildingMaterials),
        "paused settlement must not mutate retained resource production");
}

static void StrategicManagementRuntimeSettlesElapsedTimeAfterWorldMapResumes()
{
    StrategicManagementRuntime.Reset();
    InvokeRuntimePauseWorldTimeForCityManagement();
    InvokeRuntimeResumeWorldMapTime();
    int beforePulses = GetElapsedWorldTimePulses(StrategicManagementRuntime.State);
    int beforeMaterials = StrategicManagementRuntime.State.GetResourceAmount(
        StrategicManagementIds.FactionPlayer,
        StrategicManagementIds.ResourceBuildingMaterials);

    StrategicCommandResult result = InvokeRuntimeSettleElapsedWorldTime(1);

    AssertTrue(result.Success, $"runtime elapsed-time settlement should succeed after world map resumes, got {result.FailureReason}");
    AssertEqual(beforePulses + 1, GetElapsedWorldTimePulses(StrategicManagementRuntime.State), "runtime helper should mutate retained world-map time");
    AssertEqual(
        beforeMaterials + 12,
        StrategicManagementRuntime.State.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceBuildingMaterials),
        "runtime helper should settle retained resource-site production while world time runs");
}

static void StrategicManagementRuntimeBuildsDashboardFromRetainedCommandState()
{
    StrategicManagementRuntime.Reset();

    StrategicCommandResult build = StrategicManagementRuntime.Commands.BuildFacility(
        StrategicManagementRuntime.State,
        StrategicManagementIds.LocationPlainsCity,
        StrategicManagementIds.FacilityTrainingGround);
    AssertTrue(build.Success, $"runtime command should build training ground, got {build.FailureReason}");

    StrategicManagementDashboardViewModel dashboard = StrategicManagementRuntime.BuildDashboard(
        StrategicManagementIds.FactionPlayer,
        StrategicManagementIds.LocationPlainsCity);

    AssertEqual(1, dashboard.SelectedCity.FacilitySlotsUsed, "runtime dashboard should reflect command-mutated state");
    AssertEqual(StrategicManagementIds.FacilityTrainingGround, dashboard.SelectedCity.BuiltFacilities[0].FacilityDefinitionId, "runtime dashboard should show built training ground");
}

static void StrategicManagementApplicationHasNoLegacyWorldStateDependency()
{
    Type[] applicationTypes = typeof(StrategicManagementCommandService).Assembly
        .GetTypes()
        .Where(type => type.Namespace?.StartsWith("Rpg.Application.StrategicManagement", StringComparison.Ordinal) == true)
        .ToArray();

    foreach (Type type in applicationTypes)
    {
        AssertNoLegacyWorldReferences(type);
    }
}

static (
    StrategicManagementDefinitionSet Definitions,
    StrategicManagementState State,
    StrategicManagementCommandService Commands,
    string ExpeditionId,
    string CorpsInstanceId) CreateStrategicAssaultExpedition()
{
    StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
    StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
    StrategicManagementRules rules = new(definitions);
    StrategicManagementCommandService commands = new(definitions, rules);
    string corpsInstanceId = state.Heroes[StrategicManagementIds.HeroOrdinaryCommander].AssignedCorpsInstanceId;
    StrategicCommandResult expedition = commands.CreateExpedition(
        state,
        StrategicManagementIds.LocationPlainsCity,
        StrategicManagementIds.LocationBeastDen,
        StrategicExpeditionIntent.AssaultLocation,
        StrategicManagementIds.HeroOrdinaryCommander);
    AssertTrue(expedition.Success, $"expedition creation should succeed, got {expedition.FailureReason}");

    return (definitions, state, commands, expedition.CreatedEntityId, corpsInstanceId);
}

static BattleStartRequest BuildStrategicBattleRequestForHero(
    StrategicManagementDefinitionSet definitions,
    StrategicManagementState state,
    StrategicBattleBridgeService bridge,
    StrategicBattleSession session,
    string heroId,
    string requestId)
{
    StrategicHeroState hero = state.Heroes[heroId];
    StrategicCorpsInstanceState corps = state.CorpsInstances[hero.AssignedCorpsInstanceId];
    BattleStartRequest request = new()
    {
        RequestId = requestId,
        BattleKind = BattleKind.AssaultSite
    };
    request.PlayerForces.Add(new BattleForceRequest
    {
        ForceId = $"{heroId}:hero",
        UnitDefinitionId = definitions.Heroes[hero.HeroDefinitionId].BattleUnitId,
        Count = 1
    });
    request.PlayerForces.Add(new BattleForceRequest
    {
        ForceId = $"{corps.CorpsInstanceId}:corps",
        UnitDefinitionId = definitions.Corps[corps.CorpsDefinitionId].BattleUnitId,
        Count = definitions.Corps[corps.CorpsDefinitionId].BattleUnitCount
    });
    bridge.AttachSessionToLegacyRequest(session, request);
    return request;
}

static BattleResult BuildVictoryResult(BattleStartRequest request, int survivedCount)
{
    BattleResult result = new()
    {
        RequestId = request.RequestId,
        ContextId = request.ContextId,
        BattleKind = BattleKind.AssaultSite,
        Outcome = BattleOutcome.Victory
    };
    foreach (BattleForceRequest force in request.PlayerForces)
    {
        result.ForceResults.Add(new BattleForceResult
        {
            ForceId = force.ForceId,
            InitialCount = force.Count,
            SurvivedCount = Math.Min(force.Count, survivedCount)
        });
    }

    return result;
}

static StrategicCommandResult ApplyVictoryForSingleHeroExpedition(
    StrategicManagementDefinitionSet definitions,
    StrategicManagementState state,
    StrategicManagementCommandService commands,
    StrategicBattleBridgeService bridge,
    string expeditionId,
    string heroId,
    string requestId)
{
    StrategicBattleSession session = bridge.CreateSession(
        state,
        expeditionId,
        "res://return_to_world.tscn",
        "res://scenes/world/sites/WorldSiteRoot.tscn").Session;
    BattleStartRequest request = BuildStrategicBattleRequestForHero(
        definitions,
        state,
        bridge,
        session,
        heroId,
        requestId);
    BattleResult battleResult = BuildVictoryResult(request, request.PlayerForces.Sum(force => force.Count));
    StrategicBattleResultSummary summary = bridge.BuildResultSummary(request, battleResult);
    return commands.ApplyBattleResultSummary(state, summary);
}

static StrategicMusterTemplateAvailability FindTemplate(
    IReadOnlyList<StrategicMusterTemplateAvailability> templates,
    string corpsDefinitionId)
{
    StrategicMusterTemplateAvailability? template = templates.FirstOrDefault(item => item.CorpsDefinitionId == corpsDefinitionId);
    if (template == null)
    {
        throw new InvalidOperationException($"Missing template {corpsDefinitionId}");
    }

    return template;
}

static void AssertAvailable(IReadOnlyList<StrategicMusterTemplateAvailability> templates, string corpsDefinitionId)
{
    StrategicMusterTemplateAvailability template = FindTemplate(templates, corpsDefinitionId);
    AssertTrue(template.IsAvailable, $"{corpsDefinitionId} should be available, got {string.Join(",", template.FailureReasons)}");
}

static StrategicResourceViewModel FindResource(StrategicManagementDashboardViewModel dashboard, string resourceId)
{
    StrategicResourceViewModel? resource = dashboard.Resources.FirstOrDefault(item => item.ResourceId == resourceId);
    if (resource == null)
    {
        throw new InvalidOperationException($"Missing resource {resourceId}");
    }

    return resource;
}

static StrategicFacilityOptionViewModel FindFacilityOption(StrategicManagementDashboardViewModel dashboard, string facilityDefinitionId)
{
    StrategicFacilityOptionViewModel? option = dashboard.SelectedCity.FacilityOptions.FirstOrDefault(item => item.FacilityDefinitionId == facilityDefinitionId);
    if (option == null)
    {
        throw new InvalidOperationException($"Missing facility option {facilityDefinitionId}");
    }

    return option;
}

static StrategicMusterTemplateViewModel FindMusterTemplate(StrategicManagementDashboardViewModel dashboard, string corpsDefinitionId)
{
    StrategicMusterTemplateViewModel? template = dashboard.SelectedCity.MusterTemplates.FirstOrDefault(item => item.CorpsDefinitionId == corpsDefinitionId);
    if (template == null)
    {
        throw new InvalidOperationException($"Missing dashboard muster template {corpsDefinitionId}");
    }

    return template;
}

static StrategicCorpsInstanceViewModel FindCorps(StrategicManagementDashboardViewModel dashboard, string corpsInstanceId)
{
    StrategicCorpsInstanceViewModel? corps = dashboard.SelectedCity.CorpsInstances.FirstOrDefault(item => item.CorpsInstanceId == corpsInstanceId);
    if (corps == null)
    {
        throw new InvalidOperationException($"Missing dashboard corps instance {corpsInstanceId}");
    }

    return corps;
}

static StrategicHeroAssignmentViewModel FindHero(StrategicManagementDashboardViewModel dashboard, string heroId)
{
    StrategicHeroAssignmentViewModel? hero = dashboard.Heroes.FirstOrDefault(item => item.HeroId == heroId);
    if (hero == null)
    {
        throw new InvalidOperationException($"Missing dashboard hero {heroId}");
    }

    return hero;
}

static StrategicHeroCompanyViewModel FindHeroCompany(StrategicManagementDashboardViewModel dashboard, string heroId)
{
    StrategicHeroCompanyViewModel? company = dashboard.SelectedCity.HeroCompanies.FirstOrDefault(item => item.HeroId == heroId);
    if (company == null)
    {
        throw new InvalidOperationException($"Missing dashboard hero company {heroId}");
    }

    return company;
}

static StrategicCorpsInstanceState FindAssignedCorps(StrategicManagementState state, string heroId)
{
    string corpsInstanceId = state.Heroes[heroId].AssignedCorpsInstanceId;
    return state.CorpsInstances[corpsInstanceId];
}

static void AssertForceMappedToHero(BattleStartRequest request, string heroId)
{
    AssertTrue(
        request.PlayerForces.Any(force => force.StrategicHeroId == heroId),
        $"legacy request should contain forces mapped to {heroId}");
    AssertTrue(
        request.PlayerForces
            .Where(force => force.StrategicHeroId == heroId)
            .All(force =>
                !string.IsNullOrWhiteSpace(force.StrategicParticipantId) &&
                !string.IsNullOrWhiteSpace(force.StrategicCorpsInstanceId)),
        $"all forces for {heroId} should carry strategic participant and corps identity");
}

static StrategicManagementDashboardViewModel InvokeLocationDashboard(
    StrategicManagementViewModelService viewModels,
    StrategicManagementState state,
    string factionId,
    string locationId)
{
    System.Reflection.MethodInfo? method = typeof(StrategicManagementViewModelService).GetMethod(
        "BuildLocationDashboard",
        new[] { typeof(StrategicManagementState), typeof(string), typeof(string) });
    if (method == null)
    {
        throw new InvalidOperationException("view model service should expose BuildLocationDashboard(state, factionId, locationId)");
    }

    return (StrategicManagementDashboardViewModel)method.Invoke(viewModels, new object[] { state, factionId, locationId })!;
}

static StrategicManagementDashboardViewModel InvokeRuntimeLocationDashboard(string factionId, string locationId)
{
    System.Reflection.MethodInfo? method = typeof(StrategicManagementRuntime).GetMethod(
        "BuildLocationDashboard",
        new[] { typeof(string), typeof(string) });
    if (method == null)
    {
        throw new InvalidOperationException("StrategicManagementRuntime should expose BuildLocationDashboard(factionId, locationId)");
    }

    return (StrategicManagementDashboardViewModel)method.Invoke(null, new object[] { factionId, locationId })!;
}

static IReadOnlyList<StrategicResourceAmount> InvokeLocationProduction(
    StrategicManagementRules rules,
    StrategicManagementState state,
    string locationId,
    string factionId,
    int elapsedPulses)
{
    System.Reflection.MethodInfo? method = typeof(StrategicManagementRules).GetMethod(
        "GetLocationProduction",
        new[] { typeof(StrategicManagementState), typeof(string), typeof(string), typeof(int) });
    if (method == null)
    {
        throw new InvalidOperationException("rules should expose GetLocationProduction(state, locationId, factionId, elapsedPulses)");
    }

    return (IReadOnlyList<StrategicResourceAmount>)method.Invoke(rules, new object[] { state, locationId, factionId, elapsedPulses })!;
}

static StrategicCommandResult InvokeSettleLocationProduction(
    StrategicManagementCommandService commands,
    StrategicManagementState state,
    string locationId,
    string factionId,
    int elapsedPulses)
{
    System.Reflection.MethodInfo? method = typeof(StrategicManagementCommandService).GetMethod(
        "SettleLocationProduction",
        new[] { typeof(StrategicManagementState), typeof(string), typeof(string), typeof(int) });
    if (method == null)
    {
        throw new InvalidOperationException("command service should expose SettleLocationProduction(state, locationId, factionId, elapsedPulses)");
    }

    return (StrategicCommandResult)method.Invoke(commands, new object[] { state, locationId, factionId, elapsedPulses })!;
}

static StrategicCommandResult InvokeSettleElapsedWorldTime(
    StrategicManagementCommandService commands,
    StrategicManagementState state,
    string factionId,
    int elapsedPulses)
{
    System.Reflection.MethodInfo? method = typeof(StrategicManagementCommandService).GetMethod(
        "SettleElapsedWorldTime",
        new[] { typeof(StrategicManagementState), typeof(string), typeof(int) });
    if (method == null)
    {
        throw new InvalidOperationException("command service should expose SettleElapsedWorldTime(state, factionId, elapsedPulses)");
    }

    return (StrategicCommandResult)method.Invoke(commands, new object[] { state, factionId, elapsedPulses })!;
}

static StrategicCommandResult InvokeRuntimeSettleElapsedWorldTime(int elapsedPulses)
{
    System.Reflection.MethodInfo? method = typeof(StrategicManagementRuntime).GetMethod(
        "SettleElapsedWorldTime",
        new[] { typeof(int) });
    if (method == null)
    {
        throw new InvalidOperationException("StrategicManagementRuntime should expose SettleElapsedWorldTime(elapsedPulses)");
    }

    return (StrategicCommandResult)method.Invoke(null, new object[] { elapsedPulses })!;
}

static void InvokeRuntimePauseWorldTimeForCityManagement()
{
    System.Reflection.MethodInfo? method = typeof(StrategicManagementRuntime).GetMethod(
        "PauseWorldTimeForCityManagement",
        System.Type.EmptyTypes);
    if (method == null)
    {
        throw new InvalidOperationException("StrategicManagementRuntime should expose PauseWorldTimeForCityManagement()");
    }

    method.Invoke(null, System.Array.Empty<object>());
}

static void InvokeRuntimeResumeWorldMapTime()
{
    System.Reflection.MethodInfo? method = typeof(StrategicManagementRuntime).GetMethod(
        "ResumeWorldMapTime",
        System.Type.EmptyTypes);
    if (method == null)
    {
        throw new InvalidOperationException("StrategicManagementRuntime should expose ResumeWorldMapTime()");
    }

    method.Invoke(null, System.Array.Empty<object>());
}

static int GetElapsedWorldTimePulses(StrategicManagementState state)
{
    System.Reflection.PropertyInfo? property = typeof(StrategicManagementState).GetProperty("ElapsedWorldTimePulses");
    if (property == null)
    {
        throw new InvalidOperationException("strategic state should expose ElapsedWorldTimePulses");
    }

    object? value = property.GetValue(state);
    return value is int pulses
        ? pulses
        : throw new InvalidOperationException("ElapsedWorldTimePulses should be an int");
}

static T GetRequiredProperty<T>(object instance, string propertyName)
{
    if (instance == null)
    {
        throw new InvalidOperationException($"Missing instance while reading property {propertyName}");
    }

    System.Reflection.PropertyInfo? property = instance.GetType().GetProperty(propertyName);
    if (property == null)
    {
        throw new InvalidOperationException($"type {instance.GetType().FullName} should expose property {propertyName}");
    }

    object? value = property.GetValue(instance);
    if (value is T typed)
    {
        return typed;
    }

    throw new InvalidOperationException($"property {propertyName} should be {typeof(T).FullName}, actual={value?.GetType().FullName ?? "<null>"}");
}

static int FindStrategicAmount(IReadOnlyCollection<StrategicResourceAmount> amounts, string resourceId)
{
    StrategicResourceAmount? amount = amounts.FirstOrDefault(item => item.ResourceId == resourceId);
    if (amount == null)
    {
        throw new InvalidOperationException($"Missing strategic resource amount {resourceId}");
    }

    return amount.Amount;
}

static int FindReflectedAmount(IEnumerable<object> amounts, string resourceId)
{
    foreach (object amount in amounts)
    {
        if (GetRequiredProperty<string>(amount, "ResourceId") == resourceId)
        {
            return GetRequiredProperty<int>(amount, "Amount");
        }
    }

    throw new InvalidOperationException($"Missing reflected resource amount {resourceId}");
}

static void AssertContains(IReadOnlyCollection<string> values, string expected, string message)
{
    if (!values.Contains(expected))
    {
        throw new InvalidOperationException($"{message}. Expected to contain {expected}; actual={string.Join(",", values)}");
    }
}

static void AssertNoLegacyWorldReferences(Type type)
{
    foreach (Type referenced in EnumerateReferencedTypes(type))
    {
        if (referenced.Namespace == "Rpg.Domain.World")
        {
            throw new InvalidOperationException($"{type.FullName} must not reference legacy world state type {referenced.FullName}");
        }
    }
}

static IEnumerable<Type> EnumerateReferencedTypes(Type type)
{
    foreach (System.Reflection.PropertyInfo property in type.GetProperties())
    {
        yield return UnwrapType(property.PropertyType);
    }

    foreach (System.Reflection.FieldInfo field in type.GetFields())
    {
        yield return UnwrapType(field.FieldType);
    }

    foreach (System.Reflection.MethodInfo method in type.GetMethods())
    {
        yield return UnwrapType(method.ReturnType);
        foreach (System.Reflection.ParameterInfo parameter in method.GetParameters())
        {
            yield return UnwrapType(parameter.ParameterType);
        }
    }
}

static Type UnwrapType(Type type)
{
    if (type.IsArray)
    {
        return UnwrapType(type.GetElementType() ?? type);
    }

    if (!type.IsGenericType)
    {
        return type;
    }

    Type[] arguments = type.GetGenericArguments();
    return arguments.Length == 0 ? type : UnwrapType(arguments[^1]);
}

static void Run(string name, Action test)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"FAIL {name}: {exception.Message}");
        System.Environment.ExitCode = 1;
    }
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{message}. Expected={expected} Actual={actual}");
    }
}
