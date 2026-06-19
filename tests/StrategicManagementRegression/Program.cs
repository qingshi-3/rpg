System.Environment.SetEnvironmentVariable(
    "RPG_GAMELOG_DIR",
    Path.Combine(Path.GetTempPath(), "rpg-strategic-management-tests"));

Run("strategic management state initializes without legacy world state", StrategicManagementRegressionCases.StrategicManagementStateInitializesWithoutLegacyWorldState);
Run("first playable starts with three dispatchable hero companies", StrategicManagementRegressionCases.FirstPlayableStartsWithThreeDispatchableHeroCompanies);
Run("strategic management resolves map site ids without silent city fallback", StrategicManagementRegressionCases.StrategicManagementResolvesMapSiteIdsWithoutSilentCityFallback);
Run("strategic management has no strategic battle preparation choice system", StrategicManagementRegressionCases.StrategicManagementHasNoStrategicBattlePreparationChoiceSystem);
Run("strategic management dashboard hides strategic battle preparation options", StrategicManagementRegressionCases.StrategicManagementDashboardHidesStrategicBattlePreparationOptions);
Run("strategic management facility definitions load from config", StrategicManagementRegressionCases.StrategicManagementFacilityDefinitionsLoadFromConfig);
Run("Bonefield assault creates expedition without strategic preparation", StrategicManagementRegressionCases.BonefieldAssaultCreatesExpeditionWithoutStrategicPreparation);
Run("common city identity derives common muster templates", StrategicManagementRegressionCases.CommonCityIdentityDerivesCommonMusterTemplates);
Run("beast muster requires controlled beast source and beast pen", StrategicManagementRegressionCases.BeastMusterRequiresControlledBeastSourceAndBeastPen);
Run("losing beast source keeps existing corps but blocks new beast creation", StrategicManagementRegressionCases.LosingBeastSourceKeepsExistingCorpsButBlocksNewBeastCreation);
Run("build facility consumes resources and facility slot", StrategicManagementRegressionCases.BuildFacilityConsumesResourcesAndFacilitySlot);
Run("build facility failure leaves resources and slots unchanged", StrategicManagementRegressionCases.BuildFacilityFailureLeavesResourcesAndSlotsUnchanged);
Run("create corps consumes resources and creates persistent corps instance", StrategicManagementRegressionCases.CreateCorpsConsumesResourcesAndCreatesPersistentCorpsInstance);
Run("create corps failure leaves resources and corps list unchanged", StrategicManagementRegressionCases.CreateCorpsFailureLeavesResourcesAndCorpsListUnchanged);
Run("assign corps to hero records aptitude without random failure", StrategicManagementRegressionCases.AssignCorpsToHeroRecordsAptitudeWithoutRandomFailure);
Run("create expedition locks assigned hero company", StrategicManagementRegressionCases.CreateExpeditionLocksAssignedHeroCompany);
Run("create expedition locks selected hero companies", StrategicManagementRegressionCases.CreateExpeditionLocksSelectedHeroCompanies);
Run("create expedition rejects hero without assigned corps", StrategicManagementRegressionCases.CreateExpeditionRejectsHeroWithoutAssignedCorps);
Run("strategic battle bridge creates assault session from expedition", StrategicManagementRegressionCases.StrategicBattleBridgeCreatesAssaultSessionFromExpedition);
Run("strategic battle bridge creates session for all expedition participants", StrategicManagementRegressionCases.StrategicBattleBridgeCreatesSessionForAllExpeditionParticipants);
Run("strategic battle bridge creates session without strategic preparation metadata", StrategicManagementRegressionCases.StrategicBattleBridgeCreatesSessionWithoutStrategicPreparationMetadata);
Run("strategic battle bridge creates active context", StrategicManagementRegressionCases.StrategicBattleBridgeCreatesActiveContext);
Run("strategic battle active context launch uses bridge snapshot authority", StrategicManagementRegressionCases.StrategicBattleActiveContextLaunchUsesBridgeSnapshotAuthority);
Run("strategic battle active context launch syncs final preparation snapshot", StrategicManagementRegressionCases.StrategicBattleActiveContextLaunchSyncsFinalPreparationSnapshot);
Run("retarget moving expedition to assault updates strategic battle session authority", StrategicManagementRegressionCases.RetargetMovingExpeditionToAssaultUpdatesStrategicBattleSessionAuthority);
Run("retarget moving expedition to assault creates bridge session without preparation gate", StrategicManagementRegressionCases.RetargetMovingExpeditionToAssaultCreatesBridgeSessionWithoutPreparationGate);
Run("strategic battle result summary omits strategic preparation feedback", StrategicManagementRegressionCases.StrategicBattleResultSummaryOmitsStrategicPreparationFeedback);
Run("strategic battle bridge maps duplicate battle unit participants by participant identity", StrategicManagementRegressionCases.StrategicBattleBridgeMapsDuplicateBattleUnitParticipantsByParticipantIdentity);
Run("strategic battle bridge snapshot preserves strategic participant identity", StrategicManagementRegressionCases.StrategicBattleBridgeSnapshotPreservesStrategicParticipantIdentity);
Run("strategic battle result summary applies victory consequences", StrategicManagementRegressionCases.StrategicBattleResultSummaryAppliesVictoryConsequences);
Run("strategic battle result records reward hero feedback and equipment sample", StrategicManagementRegressionCases.StrategicBattleResultRecordsRewardHeroFeedbackAndEquipmentSample);
Run("strategic battle result records defeat feedback and recovery reason", StrategicManagementRegressionCases.StrategicBattleResultRecordsDefeatFeedbackAndRecoveryReason);
Run("strategic management dashboard exposes latest battle feedback", StrategicManagementRegressionCases.StrategicManagementDashboardExposesLatestBattleFeedback);
Run("strategic battle result rejects duplicate reward application", StrategicManagementRegressionCases.StrategicBattleResultRejectsDuplicateRewardApplication);
Run("strategic battle result grants one time target reward across expeditions", StrategicManagementRegressionCases.StrategicBattleResultGrantsOneTimeTargetRewardAcrossExpeditions);
Run("strategic battle result rejects null participant summary without mutation", StrategicManagementRegressionCases.StrategicBattleResultRejectsNullParticipantSummaryWithoutMutation);
Run("strategic battle result summary applies defeat consequences", StrategicManagementRegressionCases.StrategicBattleResultSummaryAppliesDefeatConsequences);
Run("strategic battle result summary applies all expedition participant consequences", StrategicManagementRegressionCases.StrategicBattleResultSummaryAppliesAllExpeditionParticipantConsequences);
Run("strategic battle result summary rejects legacy request result authority", StrategicManagementRegressionCases.StrategicBattleResultSummaryRejectsLegacyRequestResultAuthority);
Run("strategic battle result summary rejects mismatched result", StrategicManagementRegressionCases.StrategicBattleResultSummaryRejectsMismatchedResult);
Run("strategic battle result summary rejects missing participant result", StrategicManagementRegressionCases.StrategicBattleResultSummaryRejectsMissingParticipantResult);
Run("strategic battle bridge rejects resolved expedition", StrategicManagementRegressionCases.StrategicBattleBridgeRejectsResolvedExpedition);
Run("strategic battle bridge rejects location without battle entry metadata", StrategicManagementRegressionCases.StrategicBattleBridgeRejectsLocationWithoutBattleEntryMetadata);
Run("strategic management dashboard summarizes city resources facilities corps and heroes", StrategicManagementRegressionCases.StrategicManagementDashboardSummarizesCityResourcesFacilitiesCorpsAndHeroes);
Run("strategic management dashboard exposes dispatchable hero companies", StrategicManagementRegressionCases.StrategicManagementDashboardExposesDispatchableHeroCompanies);
Run("strategic management dashboard explains unavailable beast muster reasons", StrategicManagementRegressionCases.StrategicManagementDashboardExplainsUnavailableBeastMusterReasons);
Run("strategic management dashboard reflects command mutations", StrategicManagementRegressionCases.StrategicManagementDashboardReflectsCommandMutations);
Run("strategic management dashboard summarizes non-city location", StrategicManagementRegressionCases.StrategicManagementDashboardSummarizesNonCityLocation);
Run("strategic management settles controlled resource site production", StrategicManagementRegressionCases.StrategicManagementSettlesControlledResourceSiteProduction);
Run("strategic management uses elapsed world time naming instead of step naming", StrategicManagementRegressionCases.StrategicManagementUsesElapsedWorldTimeNamingInsteadOfStepNaming);
Run("strategic management settles elapsed world time and controlled production", StrategicManagementRegressionCases.StrategicManagementSettlesElapsedWorldTimeAndControlledProduction);
Run("strategic management elapsed world time skips enemy held production", StrategicManagementRegressionCases.StrategicManagementElapsedWorldTimeSkipsEnemyHeldProduction);
Run("strategic management elapsed world time rejects invalid pulse count without mutation", StrategicManagementRegressionCases.StrategicManagementElapsedWorldTimeRejectsInvalidPulseCountWithoutMutation);
Run("strategic management runtime blocks elapsed time while city management paused", StrategicManagementRegressionCases.StrategicManagementRuntimeBlocksElapsedTimeWhileCityManagementPaused);
Run("strategic management runtime settles elapsed time after world map resumes", StrategicManagementRegressionCases.StrategicManagementRuntimeSettlesElapsedTimeAfterWorldMapResumes);
Run("strategic management runtime builds dashboard from retained command state", StrategicManagementRegressionCases.StrategicManagementRuntimeBuildsDashboardFromRetainedCommandState);
Run("strategic management application has no legacy world state dependency", StrategicManagementRegressionCases.StrategicManagementApplicationHasNoLegacyWorldStateDependency);

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
