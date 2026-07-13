using System.Text.Json;
using System.Text.Json.Nodes;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Reports;
using Rpg.Application.StrategicBattleBridge;
using Rpg.Application.StrategicManagement;
using Rpg.Application.World;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;
using Rpg.Domain.World;

internal static partial class StrategicManagementRegressionCases
{
    internal static void BattleSettlementCoversEveryNonEmptyDeploymentSubset()
    {
        string[] allHeroIds =
        {
            StrategicManagementIds.HeroOrdinaryCommander,
            StrategicManagementIds.HeroArcherCaptain,
            StrategicManagementIds.HeroCavalryCaptain
        };

        for (int carriedCount = 1; carriedCount <= allHeroIds.Length; carriedCount++)
        {
            string[] carriedHeroIds = allHeroIds.Take(carriedCount).ToArray();
            int subsetCount = 1 << carriedCount;
            for (int deployedMask = 1; deployedMask < subsetCount; deployedMask++)
            {
                StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
                StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
                StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
                StrategicCommandResult expeditionResult = commands.CreateExpedition(
                    state,
                    StrategicManagementIds.LocationQingheCore,
                    StrategicManagementIds.LocationChiyanHighBasin,
                    StrategicExpeditionIntent.AssaultLocation,
                    carriedHeroIds);
                AssertTrue(expeditionResult.Success, $"subset setup should create expedition carried={carriedCount} mask={deployedMask}");

                Dictionary<string, int> originalStrengths = carriedHeroIds.ToDictionary(
                    heroId => state.Heroes[heroId].AssignedCorpsInstanceId,
                    heroId => state.CorpsInstances[state.Heroes[heroId].AssignedCorpsInstanceId].Strength,
                    StringComparer.Ordinal);
                StrategicBattleBridgeService bridge = new(definitions);
                StrategicBattleSession session = bridge.CreateSession(
                    state,
                    expeditionResult.CreatedEntityId,
                    "res://return_to_world.tscn",
                    "res://scenes/world/sites/WorldSiteRoot.tscn").Session;
                BattleStartRequest request = new()
                {
                    RequestId = $"subset_{carriedCount}_{deployedMask}",
                    BattleKind = BattleKind.AssaultSite
                };
                for (int index = 0; index < carriedHeroIds.Length; index++)
                {
                    if ((deployedMask & (1 << index)) == 0)
                    {
                        continue;
                    }

                    AddParticipantForces(definitions, state, request, carriedHeroIds[index]);
                }

                StrategicBattleActiveContext context = BuildCompletedActiveContext(
                    bridge,
                    state,
                    session,
                    request,
                    BattleOutcome.Victory,
                    participant => Math.Max(1, ResolveParticipantInitialCount(request, participant) / 2));
                StrategicBattleResultSummary summary = bridge.BuildResultSummary(context);
                int deployedCount = CountBits(deployedMask);

                AssertEqual(deployedCount, summary.Participants.Count, "only deployed participants should have Runtime-backed result rows");
                AssertEqual(carriedCount, summary.ParticipantDispositions.Count, "summary should retain every carried participant disposition");
                AssertEqual(deployedCount, session.Participants.Count(item => item.Role == StrategicBattleParticipantRole.Deployed), "session should freeze deployed roles");
                AssertEqual(carriedCount - deployedCount, session.Participants.Count(item => item.Role == StrategicBattleParticipantRole.Reserve), "session should freeze reserve roles");
                AssertEqual(deployedCount, context.Snapshot.BattleGroups.Count, "Runtime snapshot should contain deployed strategic groups only");

                StrategicCommandResult applied = commands.ApplyBattleResultSummary(state, summary);
                AssertTrue(applied.Success, $"subset settlement should apply carried={carriedCount} mask={deployedMask}, got {applied.FailureReason}");
                for (int index = 0; index < carriedHeroIds.Length; index++)
                {
                    string heroId = carriedHeroIds[index];
                    string corpsId = state.Heroes[heroId].AssignedCorpsInstanceId;
                    StrategicCorpsInstanceState corps = state.CorpsInstances[corpsId];
                    bool deployed = (deployedMask & (1 << index)) != 0;
                    AssertEqual("", state.Heroes[heroId].CurrentExpeditionId, "settlement should unlock every carried hero");
                    AssertEqual("", corps.CurrentExpeditionId, "settlement should unlock every carried corps");
                    if (deployed)
                    {
                        AssertTrue(corps.Strength < originalStrengths[corpsId], "deployed corps should consume its Runtime casualty result");
                        AssertEqual(StrategicManagementIds.LocationChiyanHighBasin, corps.HomeCityId, "surviving deployed corps should station at captured city");
                    }
                    else
                    {
                        AssertEqual(originalStrengths[corpsId], corps.Strength, "reserve corps should take zero casualty");
                        AssertEqual(StrategicManagementIds.LocationQingheCore, corps.HomeCityId, "reserve corps should return to its exact rollback station");
                    }
                }
            }
        }
    }

    internal static void ExpeditionRollbackRestoresExactStationsWithoutPartialMutation()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
        string[] heroIds =
        {
            StrategicManagementIds.HeroOrdinaryCommander,
            StrategicManagementIds.HeroArcherCaptain
        };
        StrategicCommandResult created = commands.CreateExpedition(
            state,
            StrategicManagementIds.LocationQingheCore,
            StrategicManagementIds.LocationChiyanHighBasin,
            StrategicExpeditionIntent.AssaultLocation,
            heroIds);
        AssertTrue(created.Success, "rollback setup expedition should succeed");
        StrategicExpeditionState expedition = state.Expeditions[created.CreatedEntityId];
        AssertTrue(expedition.Participants.All(item => item.RollbackStationLocationId == StrategicManagementIds.LocationQingheCore), "dispatch should capture station before clearing corps home");

        expedition.Participants[1].RollbackStationLocationId = "missing_city";
        string beforeFailure = JsonSerializer.Serialize(state);
        StrategicCommandResult rejected = commands.CancelExpedition(state, expedition.ExpeditionId, "fault_injection");
        AssertTrue(!rejected.Success, "invalid rollback plan should fail");
        AssertEqual(beforeFailure, JsonSerializer.Serialize(state), "invalid rollback plan should not partially unlock or restore participants");

        expedition.Participants[1].RollbackStationLocationId = StrategicManagementIds.LocationQingheCore;
        StrategicCommandResult cancelled = commands.CancelExpedition(state, expedition.ExpeditionId, "retry");
        AssertTrue(cancelled.Success, $"valid rollback retry should succeed, got {cancelled.FailureReason}");
        foreach (StrategicExpeditionParticipantState participant in expedition.Participants)
        {
            AssertEqual("", state.Heroes[participant.HeroId].CurrentExpeditionId, "cancel should unlock hero");
            AssertEqual("", state.CorpsInstances[participant.CorpsInstanceId].CurrentExpeditionId, "cancel should unlock corps");
            AssertEqual(participant.RollbackStationLocationId, state.CorpsInstances[participant.CorpsInstanceId].HomeCityId, "cancel should restore exact recorded station");
        }
    }

    internal static void MissingDeployedResultRejectsWithoutMutation()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
        string[] heroIds =
        {
            StrategicManagementIds.HeroOrdinaryCommander,
            StrategicManagementIds.HeroArcherCaptain
        };
        StrategicCommandResult created = commands.CreateExpedition(
            state,
            StrategicManagementIds.LocationQingheCore,
            StrategicManagementIds.LocationChiyanHighBasin,
            StrategicExpeditionIntent.AssaultLocation,
            heroIds);
        StrategicBattleBridgeService bridge = new(definitions);
        StrategicBattleSession session = bridge.CreateSession(
            state,
            created.CreatedEntityId,
            "res://return_to_world.tscn",
            "res://scenes/world/sites/WorldSiteRoot.tscn").Session;
        BattleStartRequest request = new() { RequestId = "missing_deployed_result", BattleKind = BattleKind.AssaultSite };
        AddParticipantForces(definitions, state, request, StrategicManagementIds.HeroOrdinaryCommander);
        StrategicBattleActiveContext context = BuildCompletedActiveContext(
            bridge,
            state,
            session,
            request,
            BattleOutcome.Victory,
            participant => ResolveParticipantInitialCount(request, participant));
        StrategicBattleResultSummary summary = bridge.BuildResultSummary(context);
        summary.Participants.Clear();
        string before = JsonSerializer.Serialize(state);

        StrategicCommandResult rejected = commands.ApplyBattleResultSummary(state, summary);

        AssertTrue(!rejected.Success, "missing deployed Runtime result should fail");
        AssertEqual(StrategicFailureReasons.BattleResultMismatch, rejected.FailureReason, "missing deployed result should fail at the complete disposition contract");
        AssertEqual(before, JsonSerializer.Serialize(state), "missing deployed result must not mutate live strategic state");
    }

    internal static void FinalLaunchSnapshotFreezesReserveRoles()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
        string[] heroIds =
        {
            StrategicManagementIds.HeroOrdinaryCommander,
            StrategicManagementIds.HeroArcherCaptain
        };
        StrategicCommandResult created = commands.CreateExpedition(
            state,
            StrategicManagementIds.LocationQingheCore,
            StrategicManagementIds.LocationChiyanHighBasin,
            StrategicExpeditionIntent.AssaultLocation,
            heroIds);
        StrategicBattleBridgeService bridge = new(definitions);
        StrategicBattleSession session = bridge.CreateSession(
            state,
            created.CreatedEntityId,
            "res://return_to_world.tscn",
            "res://scenes/world/sites/WorldSiteRoot.tscn").Session;
        BattleStartRequest request = new() { RequestId = "final_role_freeze", BattleKind = BattleKind.AssaultSite };
        foreach (string heroId in heroIds)
        {
            AddParticipantForces(definitions, state, request, heroId);
        }

        StrategicBattleActiveContext context = bridge.CreateActiveContext(state, session, request).Context;
        StrategicBattlePreparationDraft draft = context.PreparationDraft;
        string reserveParticipantId = session.Participants.Single(item => item.HeroId == StrategicManagementIds.HeroArcherCaptain).ParticipantId;
        draft.PlayerForces.RemoveAll(force => string.Equals(force.StrategicParticipantId, reserveParticipantId, StringComparison.Ordinal));
        AttachStrategicLaunchFlatTopology(draft);

        StrategicBattleActiveContextToken beginToken = PublishActiveContextForTest(context);
        StrategicBattleDraftSnapshotResult synced = new StrategicBattleDraftSnapshotCompiler()
            .CompileAndCommitFinalSnapshot(context, beginToken, out _);

        AssertTrue(synced.Success, $"final Draft compilation should succeed, got {synced.FailureReason}");
        AssertEqual(StrategicBattleParticipantRole.Deployed, session.Participants.Single(item => item.HeroId == StrategicManagementIds.HeroOrdinaryCommander).Role, "remaining final participant should be deployed");
        AssertEqual(StrategicBattleParticipantRole.Reserve, session.Participants.Single(item => item.HeroId == StrategicManagementIds.HeroArcherCaptain).Role, "removed final participant should be reserve");
        AssertTrue(synced.Snapshot.BattleGroups.All(group => !string.Equals(group.SourceForceId, reserveParticipantId, StringComparison.Ordinal)), "reserve must be absent from final Runtime snapshot");

        StrategicBattleSession allReserveSession = bridge.CreateSession(
            state,
            created.CreatedEntityId,
            "res://return_to_world.tscn",
            "res://scenes/world/sites/WorldSiteRoot.tscn").Session;
        BattleStartRequest allReserveSeed = new() { RequestId = "all_reserve_final_role_freeze", BattleKind = BattleKind.AssaultSite };
        foreach (string heroId in heroIds)
        {
            AddParticipantForces(definitions, state, allReserveSeed, heroId);
        }

        StrategicBattleActiveContext allReserveContext = bridge.CreateActiveContext(state, allReserveSession, allReserveSeed).Context;
        allReserveContext.PreparationDraft.PlayerForces.Clear();
        StrategicBattleActiveContextToken allReserveToken = PublishActiveContextForTest(allReserveContext);
        StrategicBattleDraftSnapshotResult allReserve = new StrategicBattleDraftSnapshotCompiler()
            .CompileAndCommitFinalSnapshot(allReserveContext, allReserveToken, out _);
        AssertTrue(!allReserve.Success, "final launch must reject an all-reserve draft");
        AssertEqual(StrategicBattleParticipantRole.Deployed, session.Participants.Single(item => item.HeroId == StrategicManagementIds.HeroOrdinaryCommander).Role, "failed all-reserve compilation must not mutate the last valid stable role set");
        AssertTrue(allReserveSession.Participants.All(item => item.Role == StrategicBattleParticipantRole.Unknown), "failed all-reserve compilation must not mutate its session roles");
    }

    internal static void BattleEntryRollbackClearsCarrierForEveryFailureBoundary()
    {
        foreach (string reason in new[] { "bridge_session_failed", "active_context_failed", "scene_change_failed" })
        {
            var setup = CreateStrategicAssaultExpedition();
            Dictionary<string, WorldArmyState> armies = new(StringComparer.Ordinal)
            {
                ["army_under_test"] = new WorldArmyState
                {
                    ArmyId = "army_under_test",
                    StrategicExpeditionId = setup.ExpeditionId,
                    OwnerFactionId = StrategicManagementIds.FactionPlayer,
                    Status = WorldArmyStatus.Attacking,
                    Intent = WorldArmyIntent.AssaultSite
                }
            };
            StrategicBattleEntryRollbackService rollback = new(setup.Commands, new WorldArmyCommandService());

            StrategicBattleEntryRollbackResult result = rollback.Rollback(
                setup.State,
                armies,
                "army_under_test",
                setup.ExpeditionId,
                reason);

            AssertTrue(result.Success, $"{reason} rollback should succeed, got {result.FailureReason}");
            AssertTrue(!armies.ContainsKey("army_under_test"), $"{reason} rollback should clear the carrier association");
            StrategicExpeditionState expedition = setup.State.Expeditions[setup.ExpeditionId];
            foreach (StrategicExpeditionParticipantState participant in expedition.Participants)
            {
                AssertEqual("", setup.State.Heroes[participant.HeroId].CurrentExpeditionId, $"{reason} should unlock hero");
                AssertEqual(participant.RollbackStationLocationId, setup.State.CorpsInstances[participant.CorpsInstanceId].HomeCityId, $"{reason} should restore exact station");
            }
        }

        var mismatchSetup = CreateStrategicAssaultExpedition();
        Dictionary<string, WorldArmyState> mismatchedArmies = new(StringComparer.Ordinal)
        {
            ["army_mismatch"] = new WorldArmyState
            {
                ArmyId = "army_mismatch",
                StrategicExpeditionId = "different_expedition"
            }
        };
        string beforeMismatch = JsonSerializer.Serialize(mismatchSetup.State);
        StrategicBattleEntryRollbackResult mismatchResult = new StrategicBattleEntryRollbackService(
                mismatchSetup.Commands,
                new WorldArmyCommandService())
            .Rollback(
                mismatchSetup.State,
                mismatchedArmies,
                "army_mismatch",
                mismatchSetup.ExpeditionId,
                "mismatch");
        AssertTrue(!mismatchResult.Success, "mismatched carrier should reject rollback");
        AssertEqual(beforeMismatch, JsonSerializer.Serialize(mismatchSetup.State), "mismatched carrier should leave all strategic participants locked and unchanged");
        AssertTrue(mismatchedArmies.ContainsKey("army_mismatch"), "mismatched carrier should remain untouched");
    }

    internal static void SettlementCommitFailureLeavesLiveStateAndContextRetryable()
    {
        var setup = CreateCompletedSettlementSetup("commit_failure");
        StrategicManagementState liveState = setup.State;
        string before = JsonSerializer.Serialize(liveState);
        string savePath = Path.Combine(Path.GetTempPath(), $"rpg-settlement-failure-{Guid.NewGuid():N}.json");
        FailingPromotionStore store = new();
        StrategicManagementSaveService saveService = new(setup.Definitions, store);
        StrategicBattleSettlementCommitService commitService = new(setup.Definitions, saveService);
        StrategicBattleActiveContextToken resultToken = RequireActiveContextTokenForTest(setup.Context);

        StrategicBattleSettlementCommitResult result = commitService.Commit(
            liveState,
            setup.Context,
            resultToken,
            setup.Summary,
            savePath,
            candidate => liveState = candidate);

        AssertTrue(!result.Success, "promotion failure should reject commit");
        AssertEqual(before, JsonSerializer.Serialize(liveState), "promotion failure should leave live state unchanged");
        AssertTrue(StrategicBattleActiveContextStore.TryPeek(resultToken, out _), "promotion failure should leave the exact accepted result token retryable");
        AssertTrue(!File.Exists(StrategicManagementSaveService.GetStagingPath(savePath)), "handled promotion failure should remove its uncommitted staging document");

        StrategicBattleSettlementCommitResult retry = new StrategicBattleSettlementCommitService(
                setup.Definitions,
                new StrategicManagementSaveService(setup.Definitions))
            .Commit(
                liveState,
                setup.Context,
                resultToken,
                setup.Summary,
                savePath,
                candidate => liveState = candidate);
        AssertTrue(retry.Success, $"exact retry after persistence failure should succeed, got {retry.FailureReason}");
        AssertTrue(!StrategicBattleActiveContextStore.HasActiveContext, "successful retry should consume the exact result token");
        ResetActiveContextStore();
        DeleteSaveFamily(savePath);
    }

    internal static void SettlementExactReplayIsIdempotentAndConflictFails()
    {
        var setup = CreateCompletedSettlementSetup("commit_replay");
        StrategicManagementState published = setup.State;
        string savePath = Path.Combine(Path.GetTempPath(), $"rpg-settlement-replay-{Guid.NewGuid():N}.json");
        try
        {
            StrategicManagementSaveService saveService = new(setup.Definitions);
            StrategicBattleSettlementCommitService commitService = new(setup.Definitions, saveService);
            StrategicBattleActiveContextToken resultToken = RequireActiveContextTokenForTest(setup.Context);
            StrategicBattleSettlementCommitResult committed = commitService.Commit(
                published,
                setup.Context,
                resultToken,
                setup.Summary,
                savePath,
                candidate => published = candidate);
            AssertTrue(committed.Success, $"first commit should succeed, got {committed.FailureReason}");
            string committedJson = JsonSerializer.Serialize(published);
            string committedSave = File.ReadAllText(savePath);
            AssertTrue(!StrategicBattleActiveContextStore.HasActiveContext, "successful durable commit should consume matching context");

            bool replayPublished = false;
            StrategicBattleSettlementCommitResult replay = commitService.Commit(
                published,
                setup.Context,
                resultToken,
                setup.Summary,
                savePath,
                candidate =>
                {
                    replayPublished = true;
                    published = candidate;
                });
            AssertTrue(replay.Success, $"exact replay should be idempotent success, got {replay.FailureReason}");
            AssertEqual(committed.CommandResult.CreatedEntityId, replay.CommandResult.CreatedEntityId, "exact replay should return original settlement identity");
            AssertTrue(!replayPublished, "exact replay should not republish an unchanged candidate");
            AssertEqual(committedJson, JsonSerializer.Serialize(published), "exact replay should not duplicate any consequence");
            AssertEqual(committedSave, File.ReadAllText(savePath), "exact replay should not rewrite the durable save");

            foreach (string conflictingIdentity in new[] { "session", "snapshot" })
            {
                StrategicBattleResultSummary conflict = JsonSerializer.Deserialize<StrategicBattleResultSummary>(JsonSerializer.Serialize(setup.Summary))!;
                if (conflictingIdentity == "session")
                {
                    conflict.SessionId = "conflicting_session";
                }
                else
                {
                    conflict.SnapshotId = "conflicting_snapshot";
                }

                StrategicBattleSettlementCommitResult rejected = commitService.Commit(
                    published,
                    setup.Context,
                    resultToken,
                    conflict,
                    savePath,
                    candidate => published = candidate);
                AssertTrue(!rejected.Success, $"conflicting {conflictingIdentity} replay should fail explicitly");
                AssertEqual(StrategicFailureReasons.BattleResultConflict, rejected.FailureReason, $"conflicting {conflictingIdentity} replay failure reason");
                AssertEqual(committedJson, JsonSerializer.Serialize(published), $"conflicting {conflictingIdentity} replay should not mutate state");
                AssertEqual(committedSave, File.ReadAllText(savePath), $"conflicting {conflictingIdentity} replay should not rewrite the durable save");
            }

            StrategicBattleResultSummary conflictingPayload = JsonSerializer.Deserialize<StrategicBattleResultSummary>(JsonSerializer.Serialize(setup.Summary))!;
            conflictingPayload.Outcome = BattleOutcome.Defeat;
            StrategicBattleSettlementCommitResult payloadRejected = commitService.Commit(
                published,
                setup.Context,
                resultToken,
                conflictingPayload,
                savePath,
                candidate => published = candidate);
            AssertTrue(!payloadRejected.Success, "same identity with different payload should be a conflicting replay");
            AssertEqual(StrategicFailureReasons.BattleResultConflict, payloadRejected.FailureReason, "same-identity payload conflict failure reason");
            AssertEqual(committedJson, JsonSerializer.Serialize(published), "same-identity payload conflict should not mutate state");
            AssertEqual(committedSave, File.ReadAllText(savePath), "same-identity payload conflict should not rewrite the durable save");
        }
        finally
        {
            ResetActiveContextStore();
            DeleteSaveFamily(savePath);
        }
    }

    internal static void SettlementPublicationFailureLeavesAcceptedResultRetryable()
    {
        var setup = CreateCompletedSettlementSetup("publication_failure");
        StrategicManagementState liveState = setup.State;
        string before = JsonSerializer.Serialize(liveState);
        string savePath = Path.Combine(Path.GetTempPath(), $"rpg-settlement-publication-failure-{Guid.NewGuid():N}.json");
        try
        {
            StrategicBattleActiveContextToken resultToken = RequireActiveContextTokenForTest(setup.Context);
            StrategicBattleSettlementCommitService commitService = new(
                setup.Definitions,
                new StrategicManagementSaveService(setup.Definitions));
            StrategicBattleSettlementCommitResult failed = commitService.Commit(
                liveState,
                setup.Context,
                resultToken,
                setup.Summary,
                savePath,
                _ => throw new InvalidOperationException("fault_injected_live_publication"));

            AssertTrue(!failed.Success, "live publication failure should reject final consumption");
            AssertEqual(before, JsonSerializer.Serialize(liveState), "publication failure before assignment should leave live state unchanged");
            AssertTrue(File.Exists(savePath), "durable candidate should exist before live publication is attempted");
            AssertTrue(
                StrategicBattleActiveContextStore.TryPeek(resultToken, out StrategicBattleActiveContext retryable) &&
                ReferenceEquals(setup.Context, retryable),
                "publication failure should release reservation and retain the exact accepted result token");

            StrategicBattleSettlementCommitResult retry = commitService.Commit(
                liveState,
                setup.Context,
                resultToken,
                setup.Summary,
                savePath,
                candidate => liveState = candidate);
            AssertTrue(retry.Success, $"exact retry after publication failure should succeed, got {retry.FailureReason}");
            AssertTrue(!StrategicBattleActiveContextStore.HasActiveContext, "successful publication retry should consume the result token");
        }
        finally
        {
            ResetActiveContextStore();
            DeleteSaveFamily(savePath);
        }
    }

    internal static void SettlementDurableReplayConsumesExactStillActiveResult()
    {
        var setup = CreateCompletedSettlementSetup("published_then_failed");
        StrategicManagementState liveState = setup.State;
        string savePath = Path.Combine(Path.GetTempPath(), $"rpg-settlement-published-then-failed-{Guid.NewGuid():N}.json");
        try
        {
            StrategicBattleActiveContextToken resultToken = RequireActiveContextTokenForTest(setup.Context);
            StrategicBattleSettlementCommitService commitService = new(
                setup.Definitions,
                new StrategicManagementSaveService(setup.Definitions));
            StrategicBattleSettlementCommitResult failed = commitService.Commit(
                liveState,
                setup.Context,
                resultToken,
                setup.Summary,
                savePath,
                candidate =>
                {
                    liveState = candidate;
                    throw new InvalidOperationException("fault_injected_after_live_publication");
                });

            AssertTrue(!failed.Success, "a publication callback exception should report failure");
            AssertTrue(
                liveState.BattleSettlementRecordsByExpedition.ContainsKey(setup.Summary.ExpeditionId),
                "fault injection should leave the exact durable settlement visible in live state");
            AssertTrue(
                StrategicBattleActiveContextStore.TryPeek(resultToken, out _),
                "callback failure should leave the exact accepted result token retryable");

            StrategicBattleActiveContextToken wrongToken = new(
                resultToken.ContextId,
                resultToken.SessionId,
                resultToken.SnapshotId,
                resultToken.Revision + 1,
                resultToken.ResultId);
            int wrongReplayPublicationCalls = 0;
            StrategicBattleSettlementCommitResult wrongReplay = commitService.Commit(
                liveState,
                setup.Context,
                wrongToken,
                setup.Summary,
                savePath,
                _ => wrongReplayPublicationCalls++);
            AssertTrue(!wrongReplay.Success, "an exact durable record must not hide a wrong token for the same still-active context");
            AssertEqual(
                StrategicFailureReasons.ActiveBattleContextMismatch,
                wrongReplay.FailureReason,
                "wrong-token durable replay should expose the CAS mismatch contract");
            AssertEqual(0, wrongReplayPublicationCalls, "wrong-token durable replay must not invoke live publication");
            AssertTrue(
                StrategicBattleActiveContextStore.TryPeek(resultToken, out _),
                "wrong-token durable replay must leave the accepted result retryable");

            StrategicBattleSettlementCommitResult replay = commitService.Commit(
                liveState,
                setup.Context,
                resultToken,
                setup.Summary,
                savePath,
                _ => throw new InvalidOperationException("exact durable replay must not republish"));

            AssertTrue(replay.Success, $"exact durable retry should replay successfully, got {replay.FailureReason}");
            AssertTrue(
                !StrategicBattleActiveContextStore.HasActiveContext,
                "exact durable replay should CAS-consume its still-active accepted result token");
        }
        finally
        {
            ResetActiveContextStore();
            DeleteSaveFamily(savePath);
        }
    }

    internal static void SettlementRejectsSummaryDivergentFromAcceptedEnvelope()
    {
        var setup = CreateCompletedSettlementSetup("summary_envelope_conflict");
        StrategicManagementState liveState = setup.State;
        string before = JsonSerializer.Serialize(liveState);
        string savePath = Path.Combine(Path.GetTempPath(), $"rpg-settlement-summary-conflict-{Guid.NewGuid():N}.json");
        try
        {
            StrategicBattleResultSummary divergent = JsonSerializer.Deserialize<StrategicBattleResultSummary>(
                JsonSerializer.Serialize(setup.Summary))!;
            divergent.Outcome = divergent.Outcome == BattleOutcome.Victory
                ? BattleOutcome.Defeat
                : BattleOutcome.Victory;

            StrategicBattleSettlementCommitResult result = new StrategicBattleSettlementCommitService(
                    setup.Definitions,
                    new StrategicManagementSaveService(setup.Definitions))
                .Commit(
                    liveState,
                    setup.Context,
                    setup.ResultToken,
                    divergent,
                    savePath,
                    candidate => liveState = candidate);

            AssertTrue(!result.Success, "a summary divergent from the accepted P2-14 envelope must be rejected");
            AssertEqual(before, JsonSerializer.Serialize(liveState), "divergent summary must not publish strategic state");
            AssertTrue(!File.Exists(savePath), "divergent summary must not persist a candidate");
            AssertTrue(
                StrategicBattleActiveContextStore.TryPeek(setup.ResultToken, out _),
                "divergent summary must leave the exact accepted envelope retryable");
        }
        finally
        {
            ResetActiveContextStore();
            DeleteSaveFamily(savePath);
        }
    }

    internal static void SettlementCallbacksExecuteOutsideActiveContextStoreLock()
    {
        var setup = CreateCompletedSettlementSetup("callbacks_outside_lock");
        StrategicBattleActiveContextToken resultToken = RequireActiveContextTokenForTest(setup.Context);
        bool persistenceObservedUnlockedStore = false;
        bool publicationObservedUnlockedStore = false;
        List<string> phases = new();

        bool consumed = StrategicBattleActiveContextStore.TryCommitAndConsume(
            resultToken,
            setup.Context,
            () =>
            {
                persistenceObservedUnlockedStore = ProbeStoreFromAnotherThread(resultToken);
                phases.Add("persist");
            },
            () =>
            {
                publicationObservedUnlockedStore = ProbeStoreFromAnotherThread(resultToken);
                phases.Add("publish");
            },
            out StrategicBattleActiveContext consumedContext,
            out Exception callbackFailure,
            out string failureReason);

        AssertTrue(consumed, $"lock-safe reservation should commit, got {failureReason} / {callbackFailure?.Message}");
        AssertTrue(persistenceObservedUnlockedStore, "persistence callback must not execute while the global store lock is held");
        AssertTrue(publicationObservedUnlockedStore, "publication callback must not execute while the global store lock is held");
        AssertEqual("persist,publish", string.Join(",", phases), "A1 ordering must persist before live publication and final consumption");
        AssertTrue(ReferenceEquals(setup.Context, consumedContext), "commit should consume the exact reserved context");
        AssertTrue(!StrategicBattleActiveContextStore.HasActiveContext, "successful reserved commit should clear active context");
    }

    private static bool ProbeStoreFromAnotherThread(StrategicBattleActiveContextToken expectedToken)
    {
        Task<bool> probe = Task.Run(() => StrategicBattleActiveContextStore.TryPeek(expectedToken, out _));
        return probe.Wait(TimeSpan.FromSeconds(2)) && probe.Result;
    }

    internal static void SettlementUncommittedResultRequiresActiveContext()
    {
        var setup = CreateCompletedSettlementSetup("commit_requires_context");
        StrategicManagementState published = setup.State;
        string before = JsonSerializer.Serialize(published);
        string savePath = Path.Combine(Path.GetTempPath(), $"rpg-settlement-context-required-{Guid.NewGuid():N}.json");
        try
        {
            StrategicBattleActiveContextToken resultToken = RequireActiveContextTokenForTest(setup.Context);
            ResetActiveContextStore();
            StrategicBattleSettlementCommitResult result = new StrategicBattleSettlementCommitService(
                    setup.Definitions,
                    new StrategicManagementSaveService(setup.Definitions))
                .Commit(published, setup.Context, resultToken, setup.Summary, savePath, candidate => published = candidate);

            AssertTrue(!result.Success, "an uncommitted settlement without active context should fail");
            AssertEqual(StrategicFailureReasons.ActiveBattleContextMismatch, result.FailureReason, "uncommitted settlement context failure reason");
            AssertEqual(before, JsonSerializer.Serialize(published), "missing active context must not mutate live state");
            AssertTrue(!File.Exists(savePath), "missing active context must not persist a candidate");
        }
        finally
        {
            ResetActiveContextStore();
            DeleteSaveFamily(savePath);
        }
    }

    internal static void SettlementCommitRejectsMismatchedIdentityWithoutMutation()
    {
        foreach (string mismatch in new[] { "context", "session", "snapshot" })
        {
            var setup = CreateCompletedSettlementSetup($"mismatch_{mismatch}");
            StrategicBattleActiveContext suppliedContext = setup.Context;
            StrategicBattleResultSummary suppliedSummary = JsonSerializer.Deserialize<StrategicBattleResultSummary>(JsonSerializer.Serialize(setup.Summary))!;
            if (mismatch == "context")
            {
                suppliedContext = BuildIdentityContext("other_context", setup.Context.Session.SessionId, setup.Context.Snapshot.SnapshotId);
            }
            else if (mismatch == "session")
            {
                suppliedSummary.SessionId = "other_session";
            }
            else
            {
                suppliedSummary.SnapshotId = "other_snapshot";
            }

            StrategicManagementState published = setup.State;
            string before = JsonSerializer.Serialize(published);
            string savePath = Path.Combine(Path.GetTempPath(), $"rpg-settlement-mismatch-{mismatch}-{Guid.NewGuid():N}.json");
            StrategicBattleActiveContextToken resultToken = RequireActiveContextTokenForTest(setup.Context);
            StrategicBattleSettlementCommitResult result = new StrategicBattleSettlementCommitService(
                    setup.Definitions,
                    new StrategicManagementSaveService(setup.Definitions))
                .Commit(published, suppliedContext, resultToken, suppliedSummary, savePath, candidate => published = candidate);

            AssertTrue(!result.Success, $"{mismatch} mismatch should reject commit");
            AssertEqual(before, JsonSerializer.Serialize(published), $"{mismatch} mismatch must not mutate live state");
            AssertTrue(!File.Exists(savePath), $"{mismatch} mismatch must not persist a candidate");
            AssertTrue(StrategicBattleActiveContextStore.TryPeek(resultToken, out _), $"{mismatch} mismatch must not consume matching active context");
            ResetActiveContextStore();
            DeleteSaveFamily(savePath);
        }
    }

    internal static void StrategicSaveMigratesRollbackStationAndRejectsInvalidDocuments()
    {
        var setup = CreateStrategicAssaultExpedition();
        StrategicManagementSaveService saveService = new(setup.Definitions);
        string savePath = Path.Combine(Path.GetTempPath(), $"rpg-save-migration-{Guid.NewGuid():N}.json");
        try
        {
            saveService.Save(setup.State, savePath);
            JsonObject current = JsonNode.Parse(File.ReadAllText(savePath))!.AsObject();

            JsonObject versionTwoAliasesOnly = JsonNode.Parse(current.ToJsonString())!.AsObject();
            versionTwoAliasesOnly["Version"] = 2;
            JsonObject aliasOnlyExpedition = versionTwoAliasesOnly["State"]!["Expeditions"]![setup.ExpeditionId]!.AsObject();
            aliasOnlyExpedition["HeroId"] = StrategicManagementIds.HeroOrdinaryCommander;
            aliasOnlyExpedition["CorpsInstanceId"] = setup.CorpsInstanceId;
            aliasOnlyExpedition.Remove("Participants");
            File.WriteAllText(savePath, versionTwoAliasesOnly.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            StrategicManagementState aliasMigrated = saveService.Load(savePath);
            AssertEqual(1, aliasMigrated.Expeditions[setup.ExpeditionId].Participants.Count, "v2 migration should convert a provable legacy alias pair");
            AssertEqual(setup.CorpsInstanceId, aliasMigrated.Expeditions[setup.ExpeditionId].Participants[0].CorpsInstanceId, "v2 migration should preserve the proven legacy pair");

            JsonObject ambiguousVersionTwo = JsonNode.Parse(current.ToJsonString())!.AsObject();
            ambiguousVersionTwo["Version"] = 2;
            JsonObject ambiguousExpedition = ambiguousVersionTwo["State"]!["Expeditions"]![setup.ExpeditionId]!.AsObject();
            ambiguousExpedition["HeroId"] = StrategicManagementIds.HeroOrdinaryCommander;
            ambiguousExpedition["CorpsInstanceId"] = setup.State.Heroes[StrategicManagementIds.HeroArcherCaptain].AssignedCorpsInstanceId;
            File.WriteAllText(savePath, ambiguousVersionTwo.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            AssertThrowsInvalidOperation(() => saveService.Load(savePath), "v2 migration must reject aliases that contradict canonical participants");

            JsonObject versionOne = JsonNode.Parse(current.ToJsonString())!.AsObject();
            versionOne["Version"] = 1;
            JsonArray participants = versionOne["State"]!["Expeditions"]![setup.ExpeditionId]!["Participants"]!.AsArray();
            foreach (JsonNode? participant in participants)
            {
                participant!.AsObject().Remove("RollbackStationLocationId");
            }
            File.WriteAllText(savePath, versionOne.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            StrategicManagementState migrated = saveService.Load(savePath);
            AssertTrue(migrated.Expeditions[setup.ExpeditionId].Participants.All(item => item.RollbackStationLocationId == StrategicManagementIds.LocationQingheCore), "v1 migration should derive provable departure station");

            versionOne["State"]!["Expeditions"]![setup.ExpeditionId]!["SourceLocationId"] = StrategicManagementIds.LocationChiyanHighBasin;
            File.WriteAllText(savePath, versionOne.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            AssertThrowsInvalidOperation(() => saveService.Load(savePath), "v1 migration must reject an unprovable rollback station instead of guessing");

            File.WriteAllText(savePath, "{\"Version\":2,\"State\":null}");
            AssertThrowsInvalidOperation(() => saveService.Load(savePath), "null strategic state must never create a new campaign");
            File.WriteAllText(savePath, "{ malformed");
            AssertThrowsInvalidOperation(() => saveService.Load(savePath), "malformed save without recovery must fail explicitly");
            File.WriteAllText(savePath, $"{{\"Version\":{StrategicManagementSaveService.CurrentVersion + 1},\"State\":{{}}}}");
            AssertThrowsInvalidOperation(() => saveService.Load(savePath), "unsupported future save must fail explicitly");
        }
        finally
        {
            DeleteSaveFamily(savePath);
        }
    }

    internal static void StrategicSaveRecoversOnlyCompleteOldOrNewDocuments()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState oldState = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementState newState = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        newState.ElapsedWorldTimePulses = 77;
        StrategicManagementSaveService saveService = new(definitions);
        string savePath = Path.Combine(Path.GetTempPath(), $"rpg-save-recovery-{Guid.NewGuid():N}.json");
        try
        {
            saveService.Save(oldState, savePath);
            saveService.Save(newState, savePath);
            AssertEqual(77, saveService.Load(savePath).ElapsedWorldTimePulses, "normal promotion should expose complete new save");

            File.WriteAllText(savePath, "{ malformed live");
            AssertEqual(0, saveService.Load(savePath).ElapsedWorldTimePulses, "malformed live should recover complete previous save");

            saveService.Save(newState, savePath);
            File.WriteAllText(StrategicManagementSaveService.GetStagingPath(savePath), "{ malformed staging");
            AssertEqual(77, saveService.Load(savePath).ElapsedWorldTimePulses, "malformed staging must not displace complete live save");

            string completeNew = File.ReadAllText(savePath);
            File.Delete(savePath);
            if (File.Exists(StrategicManagementSaveService.GetPreviousPath(savePath)))
            {
                File.Delete(StrategicManagementSaveService.GetPreviousPath(savePath));
            }
            File.WriteAllText(StrategicManagementSaveService.GetStagingPath(savePath), completeNew);
            AssertEqual(77, saveService.Load(savePath).ElapsedWorldTimePulses, "complete staging should recover a complete new document after interrupted promotion");
        }
        finally
        {
            DeleteSaveFamily(savePath);
        }
    }

    internal static void ActiveContextStoreRejectsStalePublicationAndConsumption()
    {
        ResetActiveContextStore();
        StrategicBattleActiveContext first = BuildIdentityContext("context_first", "session_first", "snapshot_first");
        StrategicBattleActiveContext second = BuildIdentityContext("context_second", "session_second", "snapshot_second");
        AssertTrue(
            StrategicBattleActiveContextStore.TryBegin(first, out StrategicBattleActiveContextToken firstToken, out _),
            "first context should publish");
        AssertTrue(
            !StrategicBattleActiveContextStore.TryBegin(second, out _, out _),
            "different active identity must not be overwritten");
        StrategicBattleActiveContextToken staleToken = new(
            firstToken.ContextId,
            firstToken.SessionId,
            "stale_snapshot",
            firstToken.Revision);
        AssertTrue(
            !StrategicBattleActiveContextStore.TryClear(staleToken, "stale_identity", out _),
            "mismatched snapshot token must not clear context");
        AssertTrue(
            StrategicBattleActiveContextStore.TryPeek(firstToken, out StrategicBattleActiveContext active) &&
            ReferenceEquals(first, active),
            "stale mutation must leave original context active");
        AssertTrue(
            StrategicBattleActiveContextStore.TryClear(firstToken, "matching_cancel", out _),
            "matching token should cancel the active context");
        AssertTrue(
            StrategicBattleActiveContextStore.TryBegin(second, out _, out _),
            "new context may publish after matching cancellation");
        ResetActiveContextStore();
    }

    internal static void ActiveContextRevisionLeaseRejectsSameReferenceStaleCallbacks()
    {
        ResetActiveContextStore();
        StrategicBattleActiveContext context = BuildIdentityContext(
            "context_revision",
            "session_revision",
            "snapshot_revision");
        AssertTrue(
            StrategicBattleActiveContextStore.TryBegin(
                context,
                out StrategicBattleActiveContextToken beginToken,
                out string beginFailure),
            $"initial context should publish, got {beginFailure}");
        AssertTrue(
            StrategicBattleActiveContextStore.TryBegin(
                context,
                beginToken,
                out StrategicBattleActiveContextToken idempotentToken,
                out string idempotentFailure),
            $"begin should be idempotent only with the exact object and token, got {idempotentFailure}");
        AssertEqual(beginToken, idempotentToken, "idempotent begin should return the exact accepted token");

        var finalSnapshot = new Rpg.Application.Battle.Snapshots.BattleStartSnapshot
        {
            SnapshotId = beginToken.SnapshotId,
            BattleId = beginToken.SessionId
        };
        AssertTrue(
            StrategicBattleActiveContextStore.TryAdvanceSnapshot(
                beginToken,
                context,
                finalSnapshot,
                new BattleStartRequest { RequestId = "revision_projection" },
                "draft_revision",
                1,
                Array.Empty<string>(),
                out StrategicBattleActiveContextToken snapshotToken,
                out string snapshotFailure),
            $"final snapshot should advance the lease, got {snapshotFailure}");
        AssertEqual(beginToken.Revision + 1, snapshotToken.Revision, "final snapshot publication should advance revision once");
        AssertTrue(ReferenceEquals(context.Snapshot, finalSnapshot), "accepted final snapshot should become context authority");
        AssertTrue(
            !StrategicBattleActiveContextStore.TryAdvanceSnapshot(
                snapshotToken,
                context,
                new Rpg.Application.Battle.Snapshots.BattleStartSnapshot
                {
                    SnapshotId = snapshotToken.SnapshotId,
                    BattleId = snapshotToken.SessionId
                },
                new BattleStartRequest { RequestId = "duplicate_revision_projection" },
                "draft_revision_duplicate",
                2,
                Array.Empty<string>(),
                out _,
                out _),
            "the Store must reject a second authoritative final Snapshot advancement");

        AssertTrue(
            !StrategicBattleActiveContextStore.TryClear(beginToken, "stale_after_snapshot", out _),
            "a callback holding the same context reference and old lease must not clear the advanced snapshot");
        StrategicBattleActiveContextToken wrongRevision = new(
            snapshotToken.ContextId,
            snapshotToken.SessionId,
            snapshotToken.SnapshotId,
            snapshotToken.Revision + 10,
            snapshotToken.ResultId);
        AssertTrue(
            !StrategicBattleActiveContextStore.TryClear(wrongRevision, "wrong_revision", out _),
            "a fabricated wrong revision must not mutate the active context");
        AssertTrue(
            StrategicBattleActiveContextStore.TryPeek(snapshotToken, out StrategicBattleActiveContext active) &&
            ReferenceEquals(context, active) &&
            ReferenceEquals(finalSnapshot, active.Snapshot),
            "stale and wrong-revision rejection must leave the accepted state unchanged");
        ResetActiveContextStore();
    }

    internal static void ActiveContextSnapshotCasRejectsInvalidParticipantsAtomically()
    {
        ResetActiveContextStore();
        StrategicBattleActiveContext context = BuildIdentityContext(
            "context_invalid_participant",
            "session_invalid_participant",
            "snapshot_invalid_participant");
        StrategicBattleParticipantReference validParticipant = new()
        {
            ParticipantId = "participant_valid",
            Role = StrategicBattleParticipantRole.Unknown
        };
        context.Session.Participants.Add(validParticipant);
        context.Session.Participants.Add(null!);
        try
        {
            AssertTrue(
                StrategicBattleActiveContextStore.TryBegin(
                    context,
                    out StrategicBattleActiveContextToken beginToken,
                    out _),
                "invalid-participant fixture should publish its preparation state");
            bool advanced = StrategicBattleActiveContextStore.TryAdvanceSnapshot(
                beginToken,
                context,
                new Rpg.Application.Battle.Snapshots.BattleStartSnapshot
                {
                    SnapshotId = beginToken.SnapshotId,
                    BattleId = beginToken.SessionId
                },
                new BattleStartRequest { RequestId = "invalid_participant_projection" },
                "invalid_participant_draft",
                1,
                new[] { validParticipant.ParticipantId },
                out _,
                out _);

            AssertTrue(!advanced, "a null participant row must reject final Snapshot publication");
            AssertEqual(
                StrategicBattleParticipantRole.Unknown,
                validParticipant.Role,
                "rejected Snapshot CAS must not partially mutate earlier participant roles");
            AssertTrue(
                StrategicBattleActiveContextStore.TryPeek(beginToken, out _),
                "rejected Snapshot CAS must leave the predecessor token unchanged");
        }
        finally
        {
            ResetActiveContextStore();
        }
    }

    internal static void ActiveContextResultRevisionReturnsExactDuplicateAndRejectsConflict()
    {
        var setup = CreateCompletedSettlementSetup("result_revision");
        AssertTrue(
            StrategicBattleActiveContextStore.TryPeek(
                out StrategicBattleActiveContext active,
                out StrategicBattleActiveContextToken resultToken) &&
            ReferenceEquals(setup.Context, active),
            "completed context fixture should remain published with its accepted result token");
        StrategicBattleResultEnvelope accepted = active.ResultEnvelope ??
            throw new InvalidOperationException("completed context should expose the accepted result envelope");
        StrategicBattleActiveContextToken preResultToken = setup.SnapshotToken;
        AssertEqual(preResultToken.Revision + 1, resultToken.Revision, "first result publication should advance revision once");
        AssertEqual(accepted.ResultId, resultToken.ResultId, "result token should bind the accepted envelope identity");

        int persistenceCalls = 0;
        int publicationCalls = 0;
        AssertTrue(
            !StrategicBattleActiveContextStore.TryCommitAndConsume(
                preResultToken,
                active,
                () => persistenceCalls++,
                () => publicationCalls++,
                out _,
                out _,
                out _),
            "the pre-result token must not enter settlement callbacks or consume the accepted result");
        StrategicBattleActiveContextToken wrongResultToken = new(
            resultToken.ContextId,
            resultToken.SessionId,
            resultToken.SnapshotId,
            resultToken.Revision,
            $"{resultToken.ResultId}:wrong");
        AssertTrue(
            !StrategicBattleActiveContextStore.TryCommitAndConsume(
                wrongResultToken,
                active,
                () => persistenceCalls++,
                () => publicationCalls++,
                out _,
                out _,
                out _),
            "a mismatched result identity must not enter settlement callbacks or consume");
        AssertEqual(0, persistenceCalls, "stale settlement attempts must not call persistence");
        AssertEqual(0, publicationCalls, "stale settlement attempts must not call live publication");

        string acceptedReportId = accepted.Report.ReportId;
        accepted.Report.ReportId = $"{acceptedReportId}:mutated";
        bool mutableEnvelopeStillMatched = StrategicBattleActiveContextStore.TryPeek(resultToken, out _);
        accepted.Report.ReportId = acceptedReportId;
        AssertTrue(!mutableEnvelopeStillMatched, "post-publication result mutation must invalidate the accepted digest");

        AssertTrue(
            StrategicBattleActiveContextStore.TryPublishResultEnvelope(
                preResultToken,
                active,
                accepted.RuntimeResult,
                accepted.SettlementPlan,
                accepted.Report,
                out StrategicBattleResultEnvelope duplicateEnvelope,
                out StrategicBattleActiveContextToken duplicateToken,
                out string duplicateFailure),
            $"exact duplicate should return the accepted identity without republishing, got {duplicateFailure}");
        AssertTrue(ReferenceEquals(accepted, duplicateEnvelope), "exact duplicate should return the original envelope object");
        AssertEqual(resultToken, duplicateToken, "exact duplicate should return the original immutable result token");

        BattleReportRecord conflictingReport = new()
        {
            ReportId = $"{accepted.Report.ReportId}:conflict",
            SnapshotId = accepted.Report.SnapshotId,
            BattleId = accepted.Report.BattleId
        };
        AssertTrue(
            !StrategicBattleActiveContextStore.TryPublishResultEnvelope(
                preResultToken,
                active,
                accepted.RuntimeResult,
                accepted.SettlementPlan,
                conflictingReport,
                out _,
                out _,
                out string conflictFailure),
            "a different result at the same predecessor revision must be rejected");
        AssertEqual(
            StrategicBattleActiveContextStore.ResultEnvelopeConflictReason,
            conflictFailure,
            "different result publication should expose an explicit conflict");
        AssertTrue(ReferenceEquals(accepted, active.ResultEnvelope), "result conflict must not replace the accepted envelope");
        AssertTrue(
            !StrategicBattleActiveContextStore.TryClear(preResultToken, "stale_after_result", out _),
            "a callback captured before result publication must not clear the result revision");
        AssertTrue(
            StrategicBattleActiveContextStore.TryPeek(resultToken, out StrategicBattleActiveContext stillActive) &&
            ReferenceEquals(active, stillActive),
            "duplicate and conflict handling must leave the accepted result state unchanged");
        AssertTrue(
            !StrategicBattleActiveContextStore.TryClear(resultToken, "result_must_commit", out _),
            "an accepted result must not be discarded through generic cancellation");
        AssertTrue(
            StrategicBattleActiveContextStore.TryPeek(resultToken, out StrategicBattleActiveContext afterRejectedClear) &&
            ReferenceEquals(active, afterRejectedClear) &&
            ReferenceEquals(accepted, afterRejectedClear.ResultEnvelope),
            "rejected accepted-result clear must leave the exact context and envelope unchanged");
        ResetActiveContextStore();
    }

    private static void AddParticipantForces(
        StrategicManagementDefinitionSet definitions,
        StrategicManagementState state,
        BattleStartRequest request,
        string heroId)
    {
        StrategicHeroState hero = state.Heroes[heroId];
        StrategicCorpsInstanceState corps = state.CorpsInstances[hero.AssignedCorpsInstanceId];
        request.PlayerForces.Add(new BattleForceRequest
        {
            ForceId = $"{heroId}:hero",
            UnitDefinitionId = definitions.Heroes[hero.HeroDefinitionId].BattleUnitId,
            Count = 1,
            MaxHitPoints = 100,
            AttackDamage = 10,
            AttackRange = 1,
            AttackSpeed = 1.0,
            MoveStepSeconds = 0.16,
            AttackActionSeconds = 1.0,
            AttackImpactDelaySeconds = 0.4,
            PreferredPlacements =
            {
                new BattleForcePlacementRequest { CellX = 4, CellY = 4, CellHeight = 0 }
            }
        });
        request.PlayerForces.Add(new BattleForceRequest
        {
            ForceId = $"{corps.CorpsInstanceId}:corps",
            UnitDefinitionId = definitions.Corps[corps.CorpsDefinitionId].BattleUnitId,
            Count = definitions.Corps[corps.CorpsDefinitionId].BattleUnitCount,
            MaxHitPoints = 100,
            AttackDamage = 10,
            AttackRange = 1,
            AttackSpeed = 1.0,
            MoveStepSeconds = 0.16,
            AttackActionSeconds = 1.0,
            AttackImpactDelaySeconds = 0.4,
            PreferredPlacements =
            {
                new BattleForcePlacementRequest { CellX = 4, CellY = 4, CellHeight = 0 }
            }
        });
    }

    private static (
        StrategicManagementDefinitionSet Definitions,
        StrategicManagementState State,
        StrategicBattleActiveContext Context,
        StrategicBattleResultSummary Summary,
        StrategicBattleActiveContextToken SnapshotToken,
        StrategicBattleActiveContextToken ResultToken) CreateCompletedSettlementSetup(string requestId)
    {
        var setup = CreateStrategicAssaultExpedition();
        StrategicBattleBridgeService bridge = new(setup.Definitions);
        StrategicBattleSession session = bridge.CreateSession(
            setup.State,
            setup.ExpeditionId,
            "res://return_to_world.tscn",
            "res://scenes/world/sites/WorldSiteRoot.tscn").Session;
        BattleStartRequest request = new() { RequestId = requestId, BattleKind = BattleKind.AssaultSite };
        AddParticipantForces(setup.Definitions, setup.State, request, StrategicManagementIds.HeroOrdinaryCommander);
        StrategicBattleActiveContextToken? snapshotToken = null;
        StrategicBattleActiveContextToken? resultToken = null;
        StrategicBattleActiveContext context = BuildCompletedActiveContext(
            bridge,
            setup.State,
            session,
            request,
            BattleOutcome.Victory,
            participant => Math.Max(1, ResolveParticipantInitialCount(request, participant) / 2),
            (_, acceptedSnapshotToken, acceptedResultToken) =>
            {
                snapshotToken = acceptedSnapshotToken;
                resultToken = acceptedResultToken;
            });
        return (
            setup.Definitions,
            setup.State,
            context,
            bridge.BuildResultSummary(context),
            snapshotToken ?? throw new InvalidOperationException("completed fixture should capture the Snapshot token"),
            resultToken ?? throw new InvalidOperationException("completed fixture should capture the result token"));
    }

    private static StrategicBattleActiveContext BuildIdentityContext(string contextId, string sessionId, string snapshotId)
    {
        return new StrategicBattleActiveContext
        {
            ContextId = contextId,
            Session = new StrategicBattleSession { SessionId = sessionId, ExpeditionId = $"expedition:{sessionId}" },
            Snapshot = new Rpg.Application.Battle.Snapshots.BattleStartSnapshot { SnapshotId = snapshotId }
        };
    }

    private static int CountBits(int value)
    {
        int count = 0;
        while (value != 0)
        {
            count += value & 1;
            value >>= 1;
        }
        return count;
    }

    private static void ResetActiveContextStore()
    {
        if (StrategicBattleActiveContextStore.TryPeek(
                out StrategicBattleActiveContext context,
                out StrategicBattleActiveContextToken token))
        {
            if (string.IsNullOrWhiteSpace(token.ResultId))
            {
                StrategicBattleActiveContextStore.TryClear(token, "test_reset", out _);
            }
            else
            {
                StrategicBattleActiveContextStore.TryCommitAndConsume(
                    token,
                    context,
                    () => { },
                    () => { },
                    out _,
                    out _,
                    out _);
            }
        }
    }

    private static void DeleteSaveFamily(string savePath)
    {
        foreach (string path in new[]
                 {
                     savePath,
                     StrategicManagementSaveService.GetStagingPath(savePath),
                     StrategicManagementSaveService.GetPreviousPath(savePath)
                 })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private sealed class FailingPromotionStore : IStrategicManagementSaveFileStore
    {
        private readonly SystemStrategicManagementSaveFileStore _inner = new();

        public bool Exists(string path) => _inner.Exists(path);
        public string ReadAllText(string path) => _inner.ReadAllText(path);
        public void WriteStaging(string path, string contents) => _inner.WriteStaging(path, contents);
        public void Promote(string stagingPath, string livePath, string previousPath) => throw new IOException("fault_injected_before_promotion");
        public void DeleteIfExists(string path) => _inner.DeleteIfExists(path);
    }
}
