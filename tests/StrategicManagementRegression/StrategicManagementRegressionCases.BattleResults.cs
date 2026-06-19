using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Application.StrategicBattleBridge;
using Rpg.Application.StrategicManagement;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;
internal static partial class StrategicManagementRegressionCases
{
    internal static void StrategicBattleResultSummaryAppliesVictoryConsequences()
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

    internal static void StrategicBattleResultRecordsRewardHeroFeedbackAndEquipmentSample()
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

    internal static void StrategicBattleResultRecordsDefeatFeedbackAndRecoveryReason()
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

    internal static void StrategicManagementDashboardExposesLatestBattleFeedback()
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

    internal static void StrategicBattleResultRejectsDuplicateRewardApplication()
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

    internal static void StrategicBattleResultGrantsOneTimeTargetRewardAcrossExpeditions()
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

    internal static void StrategicBattleResultRejectsNullParticipantSummaryWithoutMutation()
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

    internal static void StrategicBattleResultSummaryAppliesDefeatConsequences()
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

    internal static void StrategicBattleResultSummaryAppliesAllExpeditionParticipantConsequences()
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

        StrategicBattleActiveContext context = BuildCompletedActiveContext(
            bridge,
            state,
            session,
            request,
            BattleOutcome.Victory,
            participant =>
            {
                return participant.HeroId switch
                {
                    StrategicManagementIds.HeroOrdinaryCommander => ResolveParticipantInitialCount(request, participant),
                    StrategicManagementIds.HeroArcherCaptain => 2,
                    StrategicManagementIds.HeroBeastTamer => 0,
                    _ => 0
                };
            });
        StrategicBattleResultSummary summary = bridge.BuildResultSummary(context);
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

    internal static void StrategicBattleResultSummaryRejectsLegacyRequestResultAuthority()
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
            "request_legacy_result_authority");
        BattleResult battleResult = BuildVictoryResult(request, request.PlayerForces.Sum(force => force.Count));

        StrategicBattleResultSummary summary = bridge.BuildResultSummary(request, battleResult);
        StrategicCommandResult result = setup.Commands.ApplyBattleResultSummary(setup.State, summary);

        AssertEqual(0, summary.Participants.Count, "legacy request/result must not produce strategic participant consequences");
        AssertTrue(!result.Success, "legacy request/result-only summary should not apply Strategic Management consequences");
        AssertEqual(StrategicFailureReasons.MissingBattleParticipantResult, result.FailureReason, "legacy strategic result rejection should be explicit");
        AssertEqual(StrategicExpeditionStatus.Moving, setup.State.Expeditions[setup.ExpeditionId].Status, "legacy strategic result must not resolve the expedition");
        AssertEqual(100, setup.State.CorpsInstances[setup.CorpsInstanceId].Strength, "legacy strategic result must not mutate strategic corps strength");
    }

    internal static void StrategicBattleResultSummaryRejectsMismatchedResult()
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

    internal static void StrategicBattleResultSummaryRejectsMissingParticipantResult()
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

    internal static void StrategicBattleBridgeRejectsResolvedExpedition()
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
            TargetLocationId = StrategicManagementIds.LocationBeastDen,
            Outcome = BattleOutcome.Defeat,
            ObjectiveSucceeded = false
        };
        summary.Participants.Add(new StrategicBattleParticipantResult
        {
            HeroId = StrategicManagementIds.HeroOrdinaryCommander,
            CorpsInstanceId = setup.CorpsInstanceId,
            RemainingCorpsStrength = 0
        });
        StrategicCommandResult applied = setup.Commands.ApplyBattleResultSummary(setup.State, summary);
        AssertTrue(applied.Success, $"setup battle result should apply, got {applied.FailureReason}");

        StrategicBattleSessionResult retry = bridge.CreateSession(
            setup.State,
            setup.ExpeditionId,
            "res://return_to_world.tscn",
            "res://scenes/world/sites/WorldSiteRoot.tscn");

        AssertTrue(!retry.Success, "resolved expedition must not create another strategic battle session");
        AssertEqual(StrategicFailureReasons.ExpeditionNotCommandable, retry.FailureReason, "resolved expedition rejection reason");
    }

    internal static void StrategicBattleBridgeRejectsLocationWithoutBattleEntryMetadata()
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
}
