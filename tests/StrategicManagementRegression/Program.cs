System.Environment.SetEnvironmentVariable(
    "RPG_GAMELOG_DIR",
    Path.Combine(Path.GetTempPath(), "rpg-strategic-management-tests"));

Run("strategic management foundation building definitions load from config", StrategicManagementRegressionCases.StrategicManagementFoundationBuildingDefinitionsLoadFromConfig);
Run("strategic management foundation content uses module config authority", StrategicManagementRegressionCases.StrategicManagementFoundationContentUsesModuleConfigAuthority);
Run("strategic management loads passive reserve recovery from economy config", StrategicManagementRegressionCases.StrategicManagementLoadsPassiveReserveRecoveryFromEconomyConfig);
Run("strategic management rejects non-positive passive reserve recovery", StrategicManagementRegressionCases.StrategicManagementRejectsNonPositivePassiveReserveRecovery);
Run("strategic management config rejects invalid resource amount lists", StrategicManagementRegressionCases.StrategicManagementConfigRejectsInvalidResourceAmountLists);
Run("strategic management foundation resources replace obsolete first-loop resources", StrategicManagementRegressionCases.StrategicManagementFoundationResourcesReplaceObsoleteFirstLoopResources);
Run("strategic management state initializes without legacy world state", StrategicManagementRegressionCases.StrategicManagementStateInitializesWithoutLegacyWorldState);
Run("strategic management state saves and loads foundation city mutations", StrategicManagementRegressionCases.StrategicManagementStateSavesAndLoadsFoundationCityMutations);
Run("strategic management loads version one save with retired conscription field", StrategicManagementRegressionCases.StrategicManagementLoadsVersionOneSaveWithRetiredConscriptionField);
Run("strategic save migrates rollback station and rejects invalid documents", StrategicManagementRegressionCases.StrategicSaveMigratesRollbackStationAndRejectsInvalidDocuments);
Run("strategic save recovers only complete old or new documents", StrategicManagementRegressionCases.StrategicSaveRecoversOnlyCompleteOldOrNewDocuments);
Run("strategic management runtime repairs captured city company ownership on load", StrategicManagementRegressionCases.StrategicManagementRuntimeRepairsCapturedCityCompanyOwnershipOnLoad);
Run("first city initializes construction regions reserve and force capacity", StrategicManagementRegressionCases.FirstCityInitializesConstructionRegionsReserveAndForceCapacity);
Run("strategic management retires conscription policy contracts", StrategicManagementRegressionCases.StrategicManagementRetiresConscriptionPolicyContracts);
Run("first playable starts with three dispatchable battle groups", StrategicManagementRegressionCases.FirstPlayableStartsWithThreeDispatchableHeroCompanies);
Run("strategic management dashboard carries military preview unit ids", StrategicManagementRegressionCases.StrategicManagementDashboardCarriesMilitaryPreviewBattleUnitIds);
Run("strategic management resolves map site ids without silent city fallback", StrategicManagementRegressionCases.StrategicManagementResolvesMapSiteIdsWithoutSilentCityFallback);
Run("strategic management has no strategic battle preparation choice system", StrategicManagementRegressionCases.StrategicManagementHasNoStrategicBattlePreparationChoiceSystem);
Run("strategic management dashboard hides strategic battle preparation options", StrategicManagementRegressionCases.StrategicManagementDashboardHidesStrategicBattlePreparationOptions);
Run("Bonefield assault creates expedition without strategic preparation", StrategicManagementRegressionCases.BonefieldAssaultCreatesExpeditionWithoutStrategicPreparation);
Run("common city identity derives common muster templates", StrategicManagementRegressionCases.CommonCityIdentityDerivesCommonMusterTemplates);
Run("build city building consumes resources and records placement", StrategicManagementRegressionCases.BuildCityBuildingConsumesResourcesAndRecordsPlacement);
Run("build city building rejects invalid placement without mutation", StrategicManagementRegressionCases.BuildCityBuildingRejectsInvalidPlacementWithoutMutation);
Run("create corps consumes resources reserve and creates persistent corps instance", StrategicManagementRegressionCases.CreateCorpsConsumesResourcesReserveAndCreatesPersistentCorpsInstance);
Run("create corps failure leaves resources reserve and corps list unchanged", StrategicManagementRegressionCases.CreateCorpsFailureLeavesResourcesReserveAndCorpsListUnchanged);
Run("recruit corps for hero replaces old corps with full refund", StrategicManagementRegressionCases.RecruitCorpsForHeroReplacesOldCorpsWithFullRefund);
Run("recruit corps for hero refunds only current strength value", StrategicManagementRegressionCases.RecruitCorpsForHeroRefundsOnlyCurrentStrengthValue);
Run("replenish corps consumes resources reserve and restores strength", StrategicManagementRegressionCases.ReplenishCorpsConsumesResourcesReserveAndRestoresStrength);
Run("replenish corps failure leaves resources reserve and strength unchanged", StrategicManagementRegressionCases.ReplenishCorpsFailureLeavesResourcesReserveAndStrengthUnchanged);
Run("assign corps to hero records aptitude without random failure", StrategicManagementRegressionCases.AssignCorpsToHeroRecordsAptitudeWithoutRandomFailure);
Run("create expedition locks assigned battle group", StrategicManagementRegressionCases.CreateExpeditionLocksAssignedHeroCompany);
Run("create expedition locks selected battle groups", StrategicManagementRegressionCases.CreateExpeditionLocksSelectedHeroCompanies);
Run("expedition rollback restores exact stations without partial mutation", StrategicManagementRegressionCases.ExpeditionRollbackRestoresExactStationsWithoutPartialMutation);
Run("missing deployed result rejects without mutation", StrategicManagementRegressionCases.MissingDeployedResultRejectsWithoutMutation);
Run("final launch snapshot freezes reserve roles", StrategicManagementRegressionCases.FinalLaunchSnapshotFreezesReserveRoles);
Run("battle entry rollback clears carrier for every failure boundary", StrategicManagementRegressionCases.BattleEntryRollbackClearsCarrierForEveryFailureBoundary);
Run("reinforce arrival stations expedition at owned target city", StrategicManagementRegressionCases.ReinforceArrivalStationsExpeditionAtOwnedTargetCity);
Run("retarget moving expedition can reinforce departure city", StrategicManagementRegressionCases.RetargetMovingExpeditionCanReinforceDepartureCity);
Run("create expedition rejects hero without assigned corps", StrategicManagementRegressionCases.CreateExpeditionRejectsHeroWithoutAssignedCorps);
Run("strategic battle bridge creates assault session from expedition", StrategicManagementRegressionCases.StrategicBattleBridgeCreatesAssaultSessionFromExpedition);
Run("strategic battle bridge creates session for all expedition participants", StrategicManagementRegressionCases.StrategicBattleBridgeCreatesSessionForAllExpeditionParticipants);
Run("strategic battle bridge creates session without strategic preparation metadata", StrategicManagementRegressionCases.StrategicBattleBridgeCreatesSessionWithoutStrategicPreparationMetadata);
Run("strategic battle bridge creates active context", StrategicManagementRegressionCases.StrategicBattleBridgeCreatesActiveContext);
Run("strategic battle active context launch uses bridge snapshot authority", StrategicManagementRegressionCases.StrategicBattleActiveContextLaunchUsesBridgeSnapshotAuthority);
Run("strategic battle active context launch compiles final preparation Draft", StrategicManagementRegressionCases.StrategicBattleActiveContextLaunchCompilesFinalPreparationDraft);
Run("strategic battle active context launch rejects missing and stale Draft lineage", StrategicManagementRegressionCases.StrategicBattleActiveContextLaunchRejectsMissingAndStaleDraftLineage);
Run("strategic battle active context launch rejects mismatched draft lineage", StrategicManagementRegressionCases.StrategicBattleActiveContextLaunchRejectsMismatchedDraftLineage);
Run("strategic battle active context launch rejects unmapped Draft player force", StrategicManagementRegressionCases.StrategicBattleActiveContextLaunchRejectsUnmappedDraftPlayerForce);
Run("strategic battle active context launch rejects missing navigation topology", StrategicManagementRegressionCases.StrategicBattleActiveContextLaunchRejectsMissingNavigationTopology);
Run("strategic battle active context launch rejects missing combat stats", StrategicManagementRegressionCases.StrategicBattleActiveContextLaunchRejectsMissingCombatStats);
Run("strategic battle group cardinality covers default groups and count variations", StrategicManagementRegressionCases.StrategicBattleGroupCardinalityCoversDefaultGroupsAndCountVariations);
Run("strategic battle casualty uses frozen corps strength and runtime corps survival", StrategicManagementRegressionCases.StrategicBattleCasualtyUsesFrozenCorpsStrengthAndRuntimeCorpsSurvival);
Run("strategic battle duplicate mappings reject without mutation", StrategicManagementRegressionCases.StrategicBattleDuplicateMappingsRejectWithoutMutation);
Run("retarget moving expedition to assault updates strategic battle session authority", StrategicManagementRegressionCases.RetargetMovingExpeditionToAssaultUpdatesStrategicBattleSessionAuthority);
Run("retarget moving expedition to assault creates bridge session without preparation gate", StrategicManagementRegressionCases.RetargetMovingExpeditionToAssaultCreatesBridgeSessionWithoutPreparationGate);
Run("strategic battle result summary omits strategic preparation feedback", StrategicManagementRegressionCases.StrategicBattleResultSummaryOmitsStrategicPreparationFeedback);
Run("strategic battle bridge maps duplicate battle unit participants by participant identity", StrategicManagementRegressionCases.StrategicBattleBridgeMapsDuplicateBattleUnitParticipantsByParticipantIdentity);
Run("strategic battle bridge snapshot preserves strategic participant identity", StrategicManagementRegressionCases.StrategicBattleBridgeSnapshotPreservesStrategicParticipantIdentity);
Run("strategic battle result summary applies victory consequences", StrategicManagementRegressionCases.StrategicBattleResultSummaryAppliesVictoryConsequences);
Run("strategic battle victory stations surviving companies at captured city", StrategicManagementRegressionCases.StrategicBattleVictoryStationsSurvivingCompaniesAtCapturedCity);
Run("strategic battle result records reward hero feedback and equipment sample", StrategicManagementRegressionCases.StrategicBattleResultRecordsRewardHeroFeedbackAndEquipmentSample);
Run("strategic battle explicit summary consequences override target definition rewards", StrategicManagementRegressionCases.StrategicBattleExplicitSummaryConsequencesOverrideTargetDefinitionRewards);
Run("strategic battle result records defeat feedback and recovery reason", StrategicManagementRegressionCases.StrategicBattleResultRecordsDefeatFeedbackAndRecoveryReason);
Run("strategic management dashboard exposes latest battle feedback", StrategicManagementRegressionCases.StrategicManagementDashboardExposesLatestBattleFeedback);
Run("strategic battle result rejects duplicate reward application", StrategicManagementRegressionCases.StrategicBattleResultRejectsDuplicateRewardApplication);
Run("strategic battle result grants one time target reward across expeditions", StrategicManagementRegressionCases.StrategicBattleResultGrantsOneTimeTargetRewardAcrossExpeditions);
Run("strategic battle result rejects null participant summary without mutation", StrategicManagementRegressionCases.StrategicBattleResultRejectsNullParticipantSummaryWithoutMutation);
Run("strategic battle result summary applies defeat consequences", StrategicManagementRegressionCases.StrategicBattleResultSummaryAppliesDefeatConsequences);
Run("strategic world control projection follows victory authority", StrategicManagementRegressionCases.StrategicWorldControlProjectionFollowsVictoryAuthority);
Run("strategic world control projection follows defeat authority", StrategicManagementRegressionCases.StrategicWorldControlProjectionFollowsDefeatAuthority);
Run("strategic world control projection survives persisted reload", StrategicManagementRegressionCases.StrategicWorldControlProjectionSurvivesPersistedReload);
Run("strategic battle result summary applies all expedition participant consequences", StrategicManagementRegressionCases.StrategicBattleResultSummaryAppliesAllExpeditionParticipantConsequences);
Run("battle settlement covers every non-empty deployment subset", StrategicManagementRegressionCases.BattleSettlementCoversEveryNonEmptyDeploymentSubset);
Run("settlement commit failure leaves live state and context retryable", StrategicManagementRegressionCases.SettlementCommitFailureLeavesLiveStateAndContextRetryable);
Run("settlement publication failure leaves accepted result retryable", StrategicManagementRegressionCases.SettlementPublicationFailureLeavesAcceptedResultRetryable);
Run("settlement durable replay consumes an exact still-active result", StrategicManagementRegressionCases.SettlementDurableReplayConsumesExactStillActiveResult);
Run("settlement callbacks execute outside active context store lock", StrategicManagementRegressionCases.SettlementCallbacksExecuteOutsideActiveContextStoreLock);
Run("settlement rejects summary divergent from accepted envelope", StrategicManagementRegressionCases.SettlementRejectsSummaryDivergentFromAcceptedEnvelope);
Run("settlement exact replay is idempotent and conflict fails", StrategicManagementRegressionCases.SettlementExactReplayIsIdempotentAndConflictFails);
Run("uncommitted settlement requires matching active context", StrategicManagementRegressionCases.SettlementUncommittedResultRequiresActiveContext);
Run("settlement commit rejects mismatched identity without mutation", StrategicManagementRegressionCases.SettlementCommitRejectsMismatchedIdentityWithoutMutation);
Run("active context store rejects stale publication and consumption", StrategicManagementRegressionCases.ActiveContextStoreRejectsStalePublicationAndConsumption);
Run("active context revision lease rejects same-reference stale callbacks", StrategicManagementRegressionCases.ActiveContextRevisionLeaseRejectsSameReferenceStaleCallbacks);
Run("active context snapshot CAS rejects invalid participants atomically", StrategicManagementRegressionCases.ActiveContextSnapshotCasRejectsInvalidParticipantsAtomically);
Run("active context result revision returns exact duplicate and rejects conflict", StrategicManagementRegressionCases.ActiveContextResultRevisionReturnsExactDuplicateAndRejectsConflict);
Run("strategic battle active context publishes one result envelope once", StrategicManagementRegressionCases.StrategicBattleActiveContextPublishesOneResultEnvelopeOnce);
Run("strategic battle result envelope rejects invalid and legacy mirror authority", StrategicManagementRegressionCases.StrategicBattleResultEnvelopeRejectsInvalidAndLegacyMirrorAuthority);
Run("strategic battle result summary rejects legacy request result authority", StrategicManagementRegressionCases.StrategicBattleResultSummaryRejectsLegacyRequestResultAuthority);
Run("strategic battle result summary rejects mismatched result", StrategicManagementRegressionCases.StrategicBattleResultSummaryRejectsMismatchedResult);
Run("strategic battle result summary rejects missing participant result", StrategicManagementRegressionCases.StrategicBattleResultSummaryRejectsMissingParticipantResult);
Run("strategic battle result summary rejects missing runtime actor outcomes", StrategicManagementRegressionCases.StrategicBattleResultSummaryRejectsMissingRuntimeActorOutcomes);
Run("strategic battle bridge rejects resolved expedition", StrategicManagementRegressionCases.StrategicBattleBridgeRejectsResolvedExpedition);
Run("strategic battle bridge rejects location without battle entry metadata", StrategicManagementRegressionCases.StrategicBattleBridgeRejectsLocationWithoutBattleEntryMetadata);
Run("strategic management dashboard summarizes city resources buildings reserve corps and heroes", StrategicManagementRegressionCases.StrategicManagementDashboardSummarizesCityResourcesBuildingsReserveCorpsAndHeroes);
Run("strategic management dashboard exposes dispatchable battle groups", StrategicManagementRegressionCases.StrategicManagementDashboardExposesDispatchableHeroCompanies);
Run("strategic management dashboard reflects foundation command mutations", StrategicManagementRegressionCases.StrategicManagementDashboardReflectsFoundationCommandMutations);
Run("strategic management dashboard exposes passive reserve recovery", StrategicManagementRegressionCases.StrategicManagementDashboardExposesPassiveReserveRecovery);
Run("strategic management dashboard projects hero corps replacement cost", StrategicManagementRegressionCases.StrategicManagementDashboardProjectsHeroCorpsReplacementCost);
Run("strategic management dashboard summarizes non-city location", StrategicManagementRegressionCases.StrategicManagementDashboardSummarizesNonCityLocation);
Run("strategic management settles controlled resource site production", StrategicManagementRegressionCases.StrategicManagementSettlesControlledResourceSiteProduction);
Run("strategic management uses elapsed world time naming instead of step naming", StrategicManagementRegressionCases.StrategicManagementUsesElapsedWorldTimeNamingInsteadOfStepNaming);
Run("strategic management settles elapsed world time with city economy building production", StrategicManagementRegressionCases.StrategicManagementSettlesElapsedWorldTimeWithCityEconomyBuildingProduction);
Run("strategic management recovers reserve for one elapsed pulse without cost", StrategicManagementRegressionCases.StrategicManagementRecoversReserveForOneElapsedPulseWithoutCost);
Run("strategic management aggregates multiple pulse reserve recovery", StrategicManagementRegressionCases.StrategicManagementAggregatesMultiplePulseReserveRecovery);
Run("strategic management caps passive recovery at remaining capacity", StrategicManagementRegressionCases.StrategicManagementCapsPassiveRecoveryAtRemainingCapacity);
Run("strategic management skips passive recovery for full city", StrategicManagementRegressionCases.StrategicManagementSkipsPassiveRecoveryForFullCity);
Run("strategic management skips passive recovery for enemy held city", StrategicManagementRegressionCases.StrategicManagementSkipsPassiveRecoveryForEnemyHeldCity);
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
