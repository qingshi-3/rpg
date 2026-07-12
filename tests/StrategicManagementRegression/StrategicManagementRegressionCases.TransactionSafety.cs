using System.Text.Json;
using System.Text.Json.Nodes;
using Rpg.Application.Battle;
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
                    StrategicManagementIds.LocationPlainsCity,
                    StrategicManagementIds.LocationBonefieldOutpost,
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
                        AssertEqual(StrategicManagementIds.LocationBonefieldOutpost, corps.HomeCityId, "surviving deployed corps should station at captured city");
                    }
                    else
                    {
                        AssertEqual(originalStrengths[corpsId], corps.Strength, "reserve corps should take zero casualty");
                        AssertEqual(StrategicManagementIds.LocationPlainsCity, corps.HomeCityId, "reserve corps should return to its exact rollback station");
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
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.LocationBonefieldOutpost,
            StrategicExpeditionIntent.AssaultLocation,
            heroIds);
        AssertTrue(created.Success, "rollback setup expedition should succeed");
        StrategicExpeditionState expedition = state.Expeditions[created.CreatedEntityId];
        AssertTrue(expedition.Participants.All(item => item.RollbackStationLocationId == StrategicManagementIds.LocationPlainsCity), "dispatch should capture station before clearing corps home");

        expedition.Participants[1].RollbackStationLocationId = "missing_city";
        string beforeFailure = JsonSerializer.Serialize(state);
        StrategicCommandResult rejected = commands.CancelExpedition(state, expedition.ExpeditionId, "fault_injection");
        AssertTrue(!rejected.Success, "invalid rollback plan should fail");
        AssertEqual(beforeFailure, JsonSerializer.Serialize(state), "invalid rollback plan should not partially unlock or restore participants");

        expedition.Participants[1].RollbackStationLocationId = StrategicManagementIds.LocationPlainsCity;
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
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.LocationBonefieldOutpost,
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
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.LocationBonefieldOutpost,
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

        StrategicBattleDraftSnapshotResult synced = new StrategicBattleDraftSnapshotCompiler().CompileAndCommitFinalSnapshot(context);

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
        StrategicBattleDraftSnapshotResult allReserve = new StrategicBattleDraftSnapshotCompiler()
            .CompileAndCommitFinalSnapshot(allReserveContext);
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
        ResetActiveContextStore();
        AssertTrue(StrategicBattleActiveContextStore.TryBegin(setup.Context, out _), "setup context should publish");

        StrategicBattleSettlementCommitResult result = commitService.Commit(
            liveState,
            setup.Context,
            setup.Summary,
            savePath,
            candidate => liveState = candidate);

        AssertTrue(!result.Success, "promotion failure should reject commit");
        AssertEqual(before, JsonSerializer.Serialize(liveState), "promotion failure should leave live state unchanged");
        AssertTrue(StrategicBattleActiveContextStore.TryPeek(setup.Context.ContextId, setup.Context.Session.SessionId, setup.Context.Snapshot.SnapshotId, out _), "promotion failure should leave matching context retryable");
        AssertTrue(!File.Exists(StrategicManagementSaveService.GetStagingPath(savePath)), "handled promotion failure should remove its uncommitted staging document");
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
            ResetActiveContextStore();
            AssertTrue(StrategicBattleActiveContextStore.TryBegin(setup.Context, out _), "setup context should publish");
            StrategicBattleSettlementCommitResult committed = commitService.Commit(
                published,
                setup.Context,
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

    internal static void SettlementUncommittedResultRequiresActiveContext()
    {
        var setup = CreateCompletedSettlementSetup("commit_requires_context");
        StrategicManagementState published = setup.State;
        string before = JsonSerializer.Serialize(published);
        string savePath = Path.Combine(Path.GetTempPath(), $"rpg-settlement-context-required-{Guid.NewGuid():N}.json");
        try
        {
            ResetActiveContextStore();
            StrategicBattleSettlementCommitResult result = new StrategicBattleSettlementCommitService(
                    setup.Definitions,
                    new StrategicManagementSaveService(setup.Definitions))
                .Commit(published, setup.Context, setup.Summary, savePath, candidate => published = candidate);

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
        ResetActiveContextStore();
            AssertTrue(StrategicBattleActiveContextStore.TryBegin(setup.Context, out _), "setup context should publish");
            StrategicBattleSettlementCommitResult result = new StrategicBattleSettlementCommitService(
                    setup.Definitions,
                    new StrategicManagementSaveService(setup.Definitions))
                .Commit(published, suppliedContext, suppliedSummary, savePath, candidate => published = candidate);

            AssertTrue(!result.Success, $"{mismatch} mismatch should reject commit");
            AssertEqual(before, JsonSerializer.Serialize(published), $"{mismatch} mismatch must not mutate live state");
            AssertTrue(!File.Exists(savePath), $"{mismatch} mismatch must not persist a candidate");
            AssertTrue(StrategicBattleActiveContextStore.TryPeek(setup.Context.ContextId, setup.Context.Session.SessionId, setup.Context.Snapshot.SnapshotId, out _), $"{mismatch} mismatch must not consume matching active context");
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
            JsonObject versionOne = JsonNode.Parse(File.ReadAllText(savePath))!.AsObject();
            versionOne["Version"] = 1;
            JsonArray participants = versionOne["State"]!["Expeditions"]![setup.ExpeditionId]!["Participants"]!.AsArray();
            foreach (JsonNode? participant in participants)
            {
                participant!.AsObject().Remove("RollbackStationLocationId");
            }
            File.WriteAllText(savePath, versionOne.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            StrategicManagementState migrated = saveService.Load(savePath);
            AssertTrue(migrated.Expeditions[setup.ExpeditionId].Participants.All(item => item.RollbackStationLocationId == StrategicManagementIds.LocationPlainsCity), "v1 migration should derive provable departure station");

            versionOne["State"]!["Expeditions"]![setup.ExpeditionId]!["SourceLocationId"] = StrategicManagementIds.LocationBonefieldOutpost;
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
        AssertTrue(StrategicBattleActiveContextStore.TryBegin(first, out _), "first context should publish");
        AssertTrue(!StrategicBattleActiveContextStore.TryBegin(second, out _), "different active identity must not be overwritten");
        AssertTrue(!StrategicBattleActiveContextStore.TryCommitAndConsume(first.ContextId, first.Session.SessionId, "stale_snapshot", () => { }, () => { }, out _, out _), "mismatched snapshot must not consume context");
        AssertTrue(StrategicBattleActiveContextStore.TryPeek(first.ContextId, first.Session.SessionId, first.Snapshot.SnapshotId, out StrategicBattleActiveContext active) && ReferenceEquals(first, active), "stale consume must leave original context active");
        AssertTrue(StrategicBattleActiveContextStore.TryCommitAndConsume(first.ContextId, first.Session.SessionId, first.Snapshot.SnapshotId, () => { }, () => { }, out _, out _), "matching identity should consume context after commit callbacks");
        AssertTrue(StrategicBattleActiveContextStore.TryBegin(second, out _), "new context may publish after matching consumption");
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

    private static (StrategicManagementDefinitionSet Definitions, StrategicManagementState State, StrategicBattleActiveContext Context, StrategicBattleResultSummary Summary) CreateCompletedSettlementSetup(string requestId)
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
        StrategicBattleActiveContext context = BuildCompletedActiveContext(
            bridge,
            setup.State,
            session,
            request,
            BattleOutcome.Victory,
            participant => Math.Max(1, ResolveParticipantInitialCount(request, participant) / 2));
        return (setup.Definitions, setup.State, context, bridge.BuildResultSummary(context));
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
        if (StrategicBattleActiveContextStore.TryPeek(out StrategicBattleActiveContext context))
        {
            StrategicBattleActiveContextStore.TryClear(
                context.ContextId,
                context.Session?.SessionId,
                context.Snapshot?.SnapshotId,
                "test_reset");
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
