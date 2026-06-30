using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Application.StrategicBattleBridge;
using Rpg.Application.StrategicManagement;
using Rpg.Application.World;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;
internal static partial class StrategicManagementRegressionCases
{
    internal static void CreateExpeditionLocksAssignedHeroCompany()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
        string corpsInstanceId = state.Heroes[StrategicManagementIds.HeroOrdinaryCommander].AssignedCorpsInstanceId;
        StrategicCommandResult expedition = commands.CreateExpedition(
            state,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.LocationBonefieldOutpost,
            StrategicExpeditionIntent.AssaultLocation,
            StrategicManagementIds.HeroOrdinaryCommander);

        AssertTrue(expedition.Success, $"expedition creation should succeed, got {expedition.FailureReason}");
        AssertTrue(state.Expeditions.ContainsKey(expedition.CreatedEntityId), "created expedition should be durable strategic state");
        StrategicExpeditionState expeditionState = state.Expeditions[expedition.CreatedEntityId];
        AssertEqual(StrategicManagementIds.HeroOrdinaryCommander, expeditionState.HeroId, "expedition should reference the hero");
        AssertEqual(corpsInstanceId, expeditionState.CorpsInstanceId, "expedition should reference the assigned corps instance");
        AssertEqual(1, expeditionState.Participants.Count, "single-company expedition should retain one participant");
        AssertEqual(StrategicManagementIds.LocationPlainsCity, expeditionState.SourceLocationId, "expedition source should be the source city");
        AssertEqual(StrategicManagementIds.LocationBonefieldOutpost, expeditionState.TargetLocationId, "expedition target should be the selected strategic location");
        AssertEqual(StrategicExpeditionStatus.Moving, expeditionState.Status, "new expedition should start as moving");
        AssertEqual(expedition.CreatedEntityId, state.Heroes[StrategicManagementIds.HeroOrdinaryCommander].CurrentExpeditionId, "hero should be locked to the expedition");
        AssertEqual(expedition.CreatedEntityId, state.CorpsInstances[corpsInstanceId].CurrentExpeditionId, "corps should be locked to the expedition");
        AssertEqual(StrategicCorpsInstanceStatus.Expedition, state.CorpsInstances[corpsInstanceId].Status, "corps status should move to expedition");
        AssertEqual("", state.CorpsInstances[corpsInstanceId].HomeCityId, "corps on expedition should not remain stationed at the source city");
    }

    internal static void CreateExpeditionLocksSelectedHeroCompanies()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
        string[] heroIds =
        {
            StrategicManagementIds.HeroOrdinaryCommander,
            StrategicManagementIds.HeroArcherCaptain,
            StrategicManagementIds.HeroCavalryCaptain
        };

        StrategicCommandResult expedition = commands.CreateExpedition(
            state,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.LocationBonefieldOutpost,
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
            AssertEqual("", state.CorpsInstances[corpsInstanceId].HomeCityId, $"{corpsInstanceId} should leave the source city while on expedition");
        }
    }

    internal static void ReinforceArrivalStationsExpeditionAtOwnedTargetCity()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementRules rules = new(definitions);
        StrategicManagementCommandService commands = new(definitions, rules);
        state.Locations[StrategicManagementIds.LocationBonefieldOutpost].OwnerFactionId = StrategicManagementIds.FactionPlayer;
        state.Locations[StrategicManagementIds.LocationBonefieldOutpost].ControlState = StrategicLocationControlState.PlayerHeld;
        string corpsInstanceId = state.Heroes[StrategicManagementIds.HeroOrdinaryCommander].AssignedCorpsInstanceId;
        state.CorpsInstances[corpsInstanceId].HomeCityId = StrategicManagementIds.LocationBonefieldOutpost;

        StrategicCommandResult expedition = commands.CreateExpedition(
            state,
            StrategicManagementIds.LocationBonefieldOutpost,
            StrategicManagementIds.LocationPlainsCity,
            StrategicExpeditionIntent.ReinforceLocation,
            StrategicManagementIds.HeroOrdinaryCommander);
        AssertTrue(expedition.Success, $"reinforce expedition should be created, got {expedition.FailureReason}");

        StrategicCommandResult arrival = commands.CompleteExpeditionArrival(state, expedition.CreatedEntityId);

        AssertTrue(arrival.Success, $"reinforce arrival should settle, got {arrival.FailureReason}");
        AssertEqual(StrategicExpeditionStatus.Resolved, state.Expeditions[expedition.CreatedEntityId].Status, "reinforce arrival should resolve the expedition");
        AssertEqual("", state.Heroes[StrategicManagementIds.HeroOrdinaryCommander].CurrentExpeditionId, "arrival should unlock the hero");
        AssertEqual("", state.CorpsInstances[corpsInstanceId].CurrentExpeditionId, "arrival should unlock the corps");
        AssertEqual(StrategicManagementIds.LocationPlainsCity, state.CorpsInstances[corpsInstanceId].HomeCityId, "arrival should station the corps at the target city");
        AssertEqual(StrategicCorpsInstanceStatus.AssignedToHero, state.CorpsInstances[corpsInstanceId].Status, "surviving arrival corps should return to assigned hero status");
    }

    internal static void RetargetMovingExpeditionCanReinforceDepartureCity()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
        string corpsInstanceId = state.Heroes[StrategicManagementIds.HeroOrdinaryCommander].AssignedCorpsInstanceId;

        StrategicCommandResult expedition = commands.CreateExpedition(
            state,
            StrategicManagementIds.LocationPlainsCity,
            "",
            StrategicExpeditionIntent.MoveToPosition,
            StrategicManagementIds.HeroOrdinaryCommander);
        AssertTrue(expedition.Success, $"move expedition should be created, got {expedition.FailureReason}");
        AssertEqual("", state.CorpsInstances[corpsInstanceId].HomeCityId, "dispatch should detach the corps from its departure city");

        StrategicCommandResult retarget = commands.RetargetExpedition(
            state,
            expedition.CreatedEntityId,
            StrategicManagementIds.LocationPlainsCity,
            StrategicExpeditionIntent.ReinforceLocation);

        AssertTrue(retarget.Success, $"moving expedition should be able to reinforce its departure record city, got {retarget.FailureReason}");
        AssertEqual(StrategicManagementIds.LocationPlainsCity, state.Expeditions[expedition.CreatedEntityId].SourceLocationId, "source remains a departure record");
        AssertEqual(StrategicManagementIds.LocationPlainsCity, state.Expeditions[expedition.CreatedEntityId].TargetLocationId, "retarget should allow the departure city as the new target");
        AssertEqual("", state.CorpsInstances[corpsInstanceId].HomeCityId, "retarget should not station the corps before arrival");

        StrategicCommandResult arrival = commands.CompleteExpeditionArrival(state, expedition.CreatedEntityId);

        AssertTrue(arrival.Success, $"arrival at departure city should settle through Strategic Management, got {arrival.FailureReason}");
        AssertEqual(StrategicManagementIds.LocationPlainsCity, state.CorpsInstances[corpsInstanceId].HomeCityId, "arrival should station the corps at the chosen target city");
        AssertEqual("", state.CorpsInstances[corpsInstanceId].CurrentExpeditionId, "arrival should unlock the corps expedition id");
    }

    internal static void CreateExpeditionRejectsHeroWithoutAssignedCorps()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
        StrategicCommandResult unassign = commands.UnassignCorpsFromHero(state, StrategicManagementIds.HeroOrdinaryCommander);
        AssertTrue(unassign.Success, $"test setup unassignment should succeed, got {unassign.FailureReason}");

        StrategicCommandResult result = commands.CreateExpedition(
            state,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.LocationBonefieldOutpost,
            StrategicExpeditionIntent.AssaultLocation,
            StrategicManagementIds.HeroOrdinaryCommander);

        AssertTrue(!result.Success, "hero without assigned corps should not create an expedition");
        AssertEqual(StrategicFailureReasons.HeroHasNoAssignedCorps, result.FailureReason, "failure reason should explain the missing assigned corps");
        AssertEqual(0, state.Expeditions.Count, "failed expedition creation must not mutate expedition state");
        AssertEqual("", state.Heroes[StrategicManagementIds.HeroOrdinaryCommander].CurrentExpeditionId, "failed expedition creation must not lock the hero");
    }

    internal static void StrategicBattleBridgeCreatesAssaultSessionFromExpedition()
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
        AssertEqual(StrategicManagementIds.LocationBonefieldOutpost, session.TargetLocationId, "bridge session should retain target strategic location");
        AssertEqual("bonefield_assault_v1", session.MapDefinitionId, "bridge session should use target location battle metadata");
        AssertEqual("assault_bonefield", session.EncounterId, "bridge session should expose encounter metadata");
        AssertEqual(1, session.Participants.Count, "first bridge slice should expose the selected hero company as one participant");
        AssertEqual(StrategicManagementIds.HeroOrdinaryCommander, session.Participants[0].HeroId, "participant should retain strategic hero id");
        AssertEqual(corpsId, session.Participants[0].CorpsInstanceId, "participant should retain strategic corps instance id");
        AssertEqual(100, session.Participants[0].PreBattleCorpsStrength, "participant should snapshot pre-battle corps strength");
    }

    internal static void StrategicBattleBridgeCreatesSessionForAllExpeditionParticipants()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
        string[] heroIds =
        {
            StrategicManagementIds.HeroOrdinaryCommander,
            StrategicManagementIds.HeroArcherCaptain,
            StrategicManagementIds.HeroCavalryCaptain
        };
        StrategicCommandResult expedition = commands.CreateExpedition(
            state,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.LocationBonefieldOutpost,
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

    internal static void StrategicBattleBridgeCreatesSessionWithoutStrategicPreparationMetadata()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
        StrategicCommandResult expedition = commands.CreateExpedition(
            state,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.LocationBonefieldOutpost,
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

    internal static void StrategicBattleBridgeCreatesActiveContext()
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
        AssertTrue(
            context.CompatibilityRequest.PlayerForces.All(force =>
                GetRequiredProperty<string>(force, "StrategicHeroBattleUnitId") ==
                definitions.Heroes[StrategicManagementIds.HeroOrdinaryCommander].BattleUnitId),
            "active context compatibility projection should preserve the hero battle unit id on every participant force");
        AssertTrue(
            context.CompatibilityRequest.PlayerForces.All(force =>
                GetRequiredProperty<string>(force, "StrategicCorpsBattleUnitId") ==
                definitions.Corps[StrategicManagementIds.CorpsShieldLine].BattleUnitId),
            "active context compatibility projection should preserve the corps battle unit id on every participant force");
    }

    internal static void StrategicBattleActiveContextLaunchUsesBridgeSnapshotAuthority()
    {
        var setup = CreateStrategicAssaultExpedition();
        StrategicBattleBridgeService bridge = new(setup.Definitions);
        StrategicBattleSession session = bridge.CreateSession(
            setup.State,
            setup.ExpeditionId,
            "res://return_to_world.tscn",
            "res://scenes/world/sites/WorldSiteRoot.tscn").Session;
        BattleStartRequest request = BuildStrategicBattleRequestForHero(
            setup.Definitions,
            setup.State,
            bridge,
            session,
            StrategicManagementIds.HeroOrdinaryCommander,
            "request_active_context_snapshot_authority");
        StrategicBattleActiveContext context = bridge.CreateActiveContext(setup.State, session, request).Context;
        string bridgeSnapshotId = context.Snapshot.SnapshotId;
        string bridgeBattleId = context.Snapshot.BattleId;
        AttachStrategicLaunchFlatTopology(request);

        bool started = new WorldSiteBattleGroupRuntimeAdapter().TryStartActiveBattle(
            context,
            out WorldSiteBattleGroupRuntimeResolveResult result);

        AssertTrue(started, $"active context launch should start Runtime, got {result.FailureReason}");
        AssertEqual(bridgeSnapshotId, result.Snapshot.SnapshotId, "Runtime start should consume the bridge-compiled snapshot");
        AssertEqual(bridgeSnapshotId, context.Snapshot.SnapshotId, "active context snapshot must not be replaced by legacy request preparation");
        AssertEqual(bridgeBattleId, result.Snapshot.BattleId, "Runtime snapshot battle id should remain the bridge session id");
        AssertTrue(
            result.Snapshot.BattleGroups.All(group => !group.HeroId.StartsWith("probe_", StringComparison.Ordinal)),
            "strategic launch must not use legacy probe-generated hero identities");
    }

    internal static void StrategicBattleActiveContextLaunchSyncsFinalPreparationSnapshot()
    {
        var setup = CreateStrategicAssaultExpedition();
        StrategicBattleBridgeService bridge = new(setup.Definitions);
        StrategicBattleSession session = bridge.CreateSession(
            setup.State,
            setup.ExpeditionId,
            "res://return_to_world.tscn",
            "res://scenes/world/sites/WorldSiteRoot.tscn").Session;
        BattleStartRequest request = BuildStrategicBattleRequestForHero(
            setup.Definitions,
            setup.State,
            bridge,
            session,
            StrategicManagementIds.HeroOrdinaryCommander,
            "request_active_context_final_preparation");
        request.SourceSiteId = StrategicManagementIds.LocationPlainsCity;
        request.TargetSiteId = StrategicManagementIds.MapSiteBonefield;
        request.AttackerFactionId = "player";
        request.DefenderFactionId = "enemy";
        StrategicBattleActiveContext context = bridge.CreateActiveContext(setup.State, session, request).Context;
        string bridgeSnapshotId = context.Snapshot.SnapshotId;
        string bridgeBattleId = context.Snapshot.BattleId;

        request.ObjectiveZones.Add(new BattleObjectiveZoneSnapshot
        {
            ObjectiveZoneId = "enemy_deployment_zone_under_test",
            DisplayName = "Enemy Deployment",
            ObjectiveRole = "enemy_deployment",
            DeploymentSide = "Enemy",
            FactionId = "enemy",
            CellX = 24,
            CellY = 8,
            CellHeight = 0,
            Width = 4,
            Height = 3
        });
        foreach (BattleForceRequest playerForce in request.PlayerForces)
        {
            playerForce.FactionId = "player";
            playerForce.PreferredPlacements.Clear();
            playerForce.PreferredPlacements.Add(new BattleForcePlacementRequest
            {
                CellX = 11,
                CellY = 20,
                CellHeight = 0
            });
        }

        request.EnemyForces.Add(new BattleForceRequest
        {
            ForceId = "enemy_runtime_force",
            SourceKind = "Garrison",
            SourceId = "bonefield",
            UnitDefinitionId = "enemy_runtime_unit",
            Count = 2,
            FactionId = "enemy",
            MaxHitPoints = 20,
            AttackDamage = 4,
            AttackRange = 1,
            AttackSpeed = 1.0,
            MoveStepSeconds = 0.16,
            AttackActionSeconds = 1.0,
            AttackImpactDelaySeconds = 0.45,
            PreferredPlacements =
            {
                new BattleForcePlacementRequest { CellX = 28, CellY = 20, CellHeight = 0 },
                new BattleForcePlacementRequest { CellX = 29, CellY = 20, CellHeight = 0 }
            }
        });
        request.PlayerBattleGroupPlan = new BattleGroupPlanSnapshot
        {
            ObjectiveZoneId = "enemy_deployment_zone_under_test",
            EngagementRule = BattleEngagementRule.AttackFirst
        };
        AttachStrategicLaunchFlatTopology(request);

        bool started = new WorldSiteBattleGroupRuntimeAdapter().TryStartActiveBattle(
            context,
            out WorldSiteBattleGroupRuntimeResolveResult result);

        int expectedGroupCount = request.PlayerForces.Sum(force => Math.Max(0, force.Count)) +
                                request.EnemyForces.Sum(force => Math.Max(0, force.Count));
        AssertTrue(started, $"active context launch should start Runtime, got {result.FailureReason}");
        AssertEqual(bridgeSnapshotId, result.Snapshot.SnapshotId, "launch snapshot should keep the active bridge snapshot id");
        AssertEqual(bridgeBattleId, result.Snapshot.BattleId, "launch snapshot should keep the active bridge battle id");
        AssertEqual(expectedGroupCount, result.Snapshot.BattleGroups.Count, "Runtime snapshot should include final deployed player and enemy forces");
        AssertTrue(
            result.Snapshot.BattleGroups.Any(group => group.FactionId == "enemy"),
            "Runtime snapshot should include final enemy battle groups");
        AssertTrue(
            result.Snapshot.BattleGroups.Any(group => group.FactionId == "player" && group.CellX == 11 && group.CellY == 20),
            "Runtime snapshot should include final player deployment placement");
        AssertTrue(
            context.Snapshot.BattleGroups.Count == expectedGroupCount,
            "active context snapshot should be synchronized to the final launch snapshot");
    }

    internal static void StrategicBattleActiveContextLaunchRejectsUnmappedCompatibilityPlayerForce()
    {
        var setup = CreateStrategicAssaultExpedition();
        StrategicBattleBridgeService bridge = new(setup.Definitions);
        StrategicBattleSession session = bridge.CreateSession(
            setup.State,
            setup.ExpeditionId,
            "res://return_to_world.tscn",
            "res://scenes/world/sites/WorldSiteRoot.tscn").Session;
        BattleStartRequest request = BuildStrategicBattleRequestForHero(
            setup.Definitions,
            setup.State,
            bridge,
            session,
            StrategicManagementIds.HeroOrdinaryCommander,
            "request_active_context_unmapped_force");
        StrategicBattleActiveContext context = bridge.CreateActiveContext(setup.State, session, request).Context;
        request.PlayerForces.Add(new BattleForceRequest
        {
            ForceId = "rogue_compatibility_force",
            UnitDefinitionId = "rogue_unit_should_not_be_probed",
            Count = 1,
            FactionId = StrategicManagementIds.FactionPlayer,
            MaxHitPoints = 20,
            AttackDamage = 4,
            AttackRange = 1,
            AttackSpeed = 1.0,
            MoveStepSeconds = 0.16,
            AttackActionSeconds = 1.0,
            AttackImpactDelaySeconds = 0.45,
            PreferredPlacements =
            {
                new BattleForcePlacementRequest { CellX = 12, CellY = 20, CellHeight = 0 }
            }
        });
        AttachStrategicLaunchFlatTopology(request);

        bool started = new WorldSiteBattleGroupRuntimeAdapter().TryStartActiveBattle(
            context,
            out WorldSiteBattleGroupRuntimeResolveResult result);

        AssertTrue(!started, "unmapped compatibility player force must not launch through generated probe identities");
        AssertEqual(
            "strategic_battle_launch_participant_mapping_missing",
            result.FailureReason,
            "launch failure should expose the missing strategic participant mapping");
    }

    internal static void StrategicBattleActiveContextLaunchRejectsMissingNavigationTopology()
    {
        var setup = CreateStrategicAssaultExpedition();
        StrategicBattleBridgeService bridge = new(setup.Definitions);
        StrategicBattleSession session = bridge.CreateSession(
            setup.State,
            setup.ExpeditionId,
            "res://return_to_world.tscn",
            "res://scenes/world/sites/WorldSiteRoot.tscn").Session;
        BattleStartRequest request = BuildStrategicBattleRequestForHero(
            setup.Definitions,
            setup.State,
            bridge,
            session,
            StrategicManagementIds.HeroOrdinaryCommander,
            "request_active_context_missing_topology");
        StrategicBattleActiveContext context = bridge.CreateActiveContext(setup.State, session, request).Context;

        bool started = new WorldSiteBattleGroupRuntimeAdapter().TryStartActiveBattle(
            context,
            out WorldSiteBattleGroupRuntimeResolveResult result);

        AssertTrue(!started, "active context launch without compiled topology should not report Runtime start success");
        AssertEqual(
            "battle_group_runtime_start_failed",
            result.FailureReason,
            "missing topology should be exposed as a runtime start failure at the launch adapter boundary");
    }

    internal static void StrategicBattleActiveContextLaunchRejectsMissingCombatStats()
    {
        var setup = CreateStrategicAssaultExpedition();
        StrategicBattleBridgeService bridge = new(setup.Definitions);
        StrategicBattleSession session = bridge.CreateSession(
            setup.State,
            setup.ExpeditionId,
            "res://return_to_world.tscn",
            "res://scenes/world/sites/WorldSiteRoot.tscn").Session;
        BattleStartRequest request = BuildStrategicBattleRequestForHero(
            setup.Definitions,
            setup.State,
            bridge,
            session,
            StrategicManagementIds.HeroOrdinaryCommander,
            "request_active_context_missing_combat_stats");
        request.PlayerForces[0].AttackDamage = 0;
        AttachStrategicLaunchFlatTopology(request);
        StrategicBattleActiveContext context = bridge.CreateActiveContext(setup.State, session, request).Context;

        bool started = new WorldSiteBattleGroupRuntimeAdapter().TryStartActiveBattle(
            context,
            out WorldSiteBattleGroupRuntimeResolveResult result);

        AssertTrue(!started, "active context launch should reject missing production combat stats before Runtime defaults apply");
        AssertEqual(
            "strategic_battle_launch_combat_stats_missing",
            result.FailureReason,
            "combat stat validation should expose a named launch failure");
    }

    internal static void RetargetMovingExpeditionToAssaultUpdatesStrategicBattleSessionAuthority()
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
            StrategicManagementIds.LocationBonefieldOutpost,
            StrategicExpeditionIntent.AssaultLocation);

        AssertTrue(retarget.Success, $"retarget to Bonefield assault should succeed, got {retarget.FailureReason}");
        StrategicExpeditionState retargeted = state.Expeditions[expedition.CreatedEntityId];
        AssertEqual(StrategicManagementIds.LocationBonefieldOutpost, retargeted.TargetLocationId, "retarget should update strategic target location");
        AssertEqual(StrategicExpeditionIntent.AssaultLocation, retargeted.Intent, "retarget should update strategic expedition intent");

        StrategicBattleSessionResult session = new StrategicBattleBridgeService(definitions).CreateSession(
            state,
            expedition.CreatedEntityId,
            "res://return_to_world.tscn",
            "res://scenes/world/sites/WorldSiteRoot.tscn");

        AssertTrue(session.Success, $"retargeted assault expedition should be accepted by bridge, got {session.FailureReason}");
        AssertEqual(StrategicManagementIds.LocationBonefieldOutpost, session.Session.TargetLocationId, "bridge session should read the retargeted strategic target");
    }

    internal static void RetargetMovingExpeditionToAssaultCreatesBridgeSessionWithoutPreparationGate()
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
            StrategicManagementIds.LocationBonefieldOutpost,
            StrategicExpeditionIntent.AssaultLocation);

        AssertTrue(retarget.Success, $"retarget to Bonefield assault should allow travel, got {retarget.FailureReason}");
        StrategicExpeditionState retargeted = state.Expeditions[expedition.CreatedEntityId];
        AssertEqual(StrategicManagementIds.LocationBonefieldOutpost, retargeted.TargetLocationId, "retarget should keep the assault target for travel");
        AssertEqual(StrategicExpeditionIntent.AssaultLocation, retargeted.Intent, "retarget should record the intended assault for later bridge entry");

        StrategicBattleSessionResult session = new StrategicBattleBridgeService(definitions).CreateSession(
            state,
            expedition.CreatedEntityId,
            "res://return_to_world.tscn",
            "res://scenes/world/sites/WorldSiteRoot.tscn");
        AssertTrue(session.Success, $"retargeted assault should enter bridge without strategic preparation, got {session.FailureReason}");
    }

    internal static void StrategicBattleResultSummaryOmitsStrategicPreparationFeedback()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
        StrategicCommandResult expedition = commands.CreateExpedition(
            state,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.LocationBonefieldOutpost,
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
        StrategicBattleActiveContext context = BuildCompletedActiveContext(
            bridge,
            state,
            session,
            request,
            BattleOutcome.Victory,
            participant => ResolveParticipantInitialCount(request, participant));
        StrategicBattleResultSummary summary = bridge.BuildResultSummary(context);
        StrategicCommandResult result = commands.ApplyBattleResultSummary(state, summary);

        AssertTrue(result.Success, $"battle summary should apply, got {result.FailureReason}");
        AssertTrue(summary.GetType().GetProperty("StrategicPreparationId") == null, "result summary should not carry strategic preparation id");
        StrategicBattleFeedbackRecord feedback = state.BattleFeedbackRecords[result.CreatedEntityId];
        AssertTrue(feedback.GetType().GetProperty("PreparationId") == null, "battle feedback should not store strategic preparation id");
        AssertTrue(feedback.GetType().GetProperty("PreparationText") == null, "battle feedback should not store strategic preparation text");
    }

    internal static void StrategicBattleBridgeMapsDuplicateBattleUnitParticipantsByParticipantIdentity()
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
            StrategicManagementIds.LocationBonefieldOutpost,
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

        StrategicBattleActiveContext context = BuildCompletedActiveContext(
            bridge,
            state,
            session,
            request,
            BattleOutcome.Victory,
            participant => string.Equals(participant.ParticipantId, ordinary.ParticipantId, StringComparison.Ordinal)
                ? 4
                : 0);
        StrategicBattleResultSummary summary = bridge.BuildResultSummary(context);
        StrategicCommandResult result = commands.ApplyBattleResultSummary(state, summary);

        AssertTrue(result.Success, $"duplicate-unit participant results should apply, got {result.FailureReason}");
        AssertEqual(100, state.CorpsInstances[ordinary.CorpsInstanceId].Strength, "first duplicate participant should keep its own full survival result");
        AssertEqual(0, state.CorpsInstances[archer.CorpsInstanceId].Strength, "second duplicate participant should keep its own routed result");
    }

    internal static void StrategicBattleBridgeSnapshotPreservesStrategicParticipantIdentity()
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
        AssertEqual(StrategicManagementIds.LocationBonefieldOutpost, snapshot.TargetLocationId, "snapshot target should be the strategic target location");
        BattleGroupSnapshot? playerGroup = snapshot.BattleGroups.FirstOrDefault(group =>
            string.Equals(group.FactionId, StrategicManagementIds.FactionPlayer, StringComparison.Ordinal));
        if (playerGroup == null)
        {
            throw new InvalidOperationException("snapshot should contain a player battle group");
        }

        AssertEqual(StrategicManagementIds.HeroOrdinaryCommander, playerGroup.HeroId, "snapshot hero id should come from Strategic Management");
        AssertEqual(corpsId, playerGroup.CorpsId, "snapshot corps id should come from Strategic Management corps instance");
        AssertEqual(StrategicManagementIds.CorpsShieldLine, playerGroup.CorpsDefinitionId, "snapshot corps definition should come from Strategic Management");
        AssertEqual(
            definitions.Heroes[StrategicManagementIds.HeroOrdinaryCommander].BattleUnitId,
            GetRequiredProperty<string>(playerGroup, "HeroBattleUnitId"),
            "snapshot hero battle unit should come from Strategic Management definitions");
        AssertEqual(
            definitions.Corps[StrategicManagementIds.CorpsShieldLine].BattleUnitId,
            GetRequiredProperty<string>(playerGroup, "CorpsBattleUnitId"),
            "snapshot corps battle unit should come from Strategic Management definitions");
        AssertEqual(StrategicManagementIds.LocationPlainsCity, playerGroup.SourceLocationId, "snapshot source location should come from the expedition source");
        AssertEqual(100, playerGroup.CorpsStrength, "snapshot should preserve pre-battle strategic corps strength");
        AssertTrue(
            !playerGroup.HeroId.StartsWith("probe_", StringComparison.Ordinal) &&
            !playerGroup.CorpsId.StartsWith("probe_", StringComparison.Ordinal),
            "bridge snapshots must not use legacy probe hero/corps identities for strategic participants");
    }
}
