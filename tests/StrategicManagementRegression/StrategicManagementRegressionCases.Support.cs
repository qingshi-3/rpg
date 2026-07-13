using Rpg.Application.Battle;
using Rpg.Application.Battle.Navigation;
using Rpg.Application.Battle.Reports;
using Rpg.Application.Battle.Settlement;
using Rpg.Application.Battle.Snapshots;
using Rpg.Application.StrategicBattleBridge;
using Rpg.Application.StrategicManagement;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Results;
internal static partial class StrategicManagementRegressionCases
{
    private static StrategicConstructionRegionDefinition FindRegion(
        StrategicManagementDefinitionSet definitions,
        string regionId)
    {
        StrategicLocationDefinition city = definitions.Locations[StrategicManagementIds.LocationPlainsCity];
        StrategicConstructionRegionDefinition? region = city.ConstructionRegions
            .FirstOrDefault(item => item.RegionId == regionId);
        AssertTrue(region != null, $"expected construction region id={regionId}");
        return region ?? throw new InvalidOperationException($"expected construction region id={regionId}");
    }

    private static (
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
            StrategicManagementIds.LocationBonefieldOutpost,
            StrategicExpeditionIntent.AssaultLocation,
            StrategicManagementIds.HeroOrdinaryCommander);
        AssertTrue(expedition.Success, $"expedition creation should succeed, got {expedition.FailureReason}");

        return (definitions, state, commands, expedition.CreatedEntityId, corpsInstanceId);
    }

    private static BattleStartRequest BuildStrategicBattleRequestForHero(
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
            Count = 1,
            MaxHitPoints = 30,
            AttackDamage = 6,
            AttackRange = 1,
            AttackSpeed = 1.0,
            MoveStepSeconds = 0.16,
            AttackActionSeconds = 1.0,
            AttackImpactDelaySeconds = 0.45,
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
            MaxHitPoints = 24,
            AttackDamage = 5,
            AttackRange = 1,
            AttackSpeed = 1.0,
            MoveStepSeconds = 0.16,
            AttackActionSeconds = 1.0,
            AttackImpactDelaySeconds = 0.45,
            PreferredPlacements =
            {
                new BattleForcePlacementRequest { CellX = 4, CellY = 4, CellHeight = 0 }
            }
        });
        bridge.AttachSessionToLegacyRequest(session, request);
        return request;
    }

    private static BattleResult BuildVictoryResult(BattleStartRequest request, int survivedCount)
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

    private static StrategicCommandResult ApplyVictoryForSingleHeroExpedition(
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
        StrategicBattleActiveContext context = BuildCompletedActiveContext(
            bridge,
            state,
            session,
            request,
            BattleOutcome.Victory,
            participant => ResolveParticipantInitialCount(request, participant));
        StrategicBattleResultSummary summary = bridge.BuildResultSummary(context);
        return commands.ApplyBattleResultSummary(state, summary);
    }

    private static StrategicBattleActiveContext BuildCompletedActiveContext(
        StrategicBattleBridgeService bridge,
        StrategicManagementState state,
        StrategicBattleSession session,
        BattleStartRequest request,
        BattleOutcome outcome,
        Func<StrategicBattleParticipantReference, int> survivingCorpsActors,
        Action<StrategicBattleActiveContextToken, StrategicBattleActiveContextToken, StrategicBattleActiveContextToken>? captureTokens = null,
        Action<StrategicBattlePreparationDraft>? configureDraft = null,
        Action<BattleEventStream>? configureEvents = null,
        Action<SettlementPlan, BattleReportRecord>? configureSettlementAndReport = null)
    {
        StrategicBattleActiveContextResult contextResult = bridge.CreateActiveContext(state, session, request);
        AssertTrue(contextResult.Success, $"active context should be created, got {contextResult.FailureReason}");
        StrategicBattleActiveContext context = contextResult.Context;
        StrategicBattleActiveContextToken beginToken = PublishActiveContextForTest(context);
        foreach (BattleForceRequest force in context.PreparationDraft.PlayerForces)
        {
            // This helper fabricates completed Runtime outcomes without starting
            // Runtime; supply the same valid combat contract production launch requires.
            force.MaxHitPoints = force.MaxHitPoints > 0 ? force.MaxHitPoints : 24;
            force.AttackDamage = force.AttackDamage > 0 ? force.AttackDamage : 5;
            force.AttackRange = force.AttackRange > 0 ? force.AttackRange : 1;
            force.AttackSpeed = double.IsFinite(force.AttackSpeed) && force.AttackSpeed > 0 ? force.AttackSpeed : 1.0;
            force.MoveStepSeconds = double.IsFinite(force.MoveStepSeconds) && force.MoveStepSeconds > 0 ? force.MoveStepSeconds : 0.16;
            force.AttackActionSeconds = double.IsFinite(force.AttackActionSeconds) && force.AttackActionSeconds > 0 ? force.AttackActionSeconds : 1.0;
            force.AttackImpactDelaySeconds = double.IsFinite(force.AttackImpactDelaySeconds) && force.AttackImpactDelaySeconds >= 0
                ? force.AttackImpactDelaySeconds
                : 0.45;
            if (force.PreferredPlacements.Count == 0)
            {
                force.PreferredPlacements.Add(new BattleForcePlacementRequest { CellX = 4, CellY = 4, CellHeight = 0 });
            }
        }
        configureDraft?.Invoke(context.PreparationDraft);
        StrategicBattleDraftSnapshotResult finalSnapshot = new StrategicBattleDraftSnapshotCompiler()
            .CompileAndCommitFinalSnapshot(context, beginToken, out StrategicBattleActiveContextToken snapshotToken);
        AssertTrue(finalSnapshot.Success, $"completed context should compile the final Draft snapshot, got {finalSnapshot.FailureReason}");
        BattleStartRequest draft = context.PreparationDraft;
        BattleOutcomeResult runtimeOutcome = BattleOutcomeResult.Completed(
            context.Snapshot.SnapshotId,
            session.SessionId,
            ToTerminationReason(outcome));
        foreach (StrategicBattleParticipantReference participant in session.Participants)
        {
            if (participant.Role != StrategicBattleParticipantRole.Deployed)
            {
                continue;
            }

            int survivorCount = Math.Max(0, survivingCorpsActors?.Invoke(participant) ?? 0);
            int initialCount = ResolveParticipantInitialCount(draft, participant);
            BattleGroupSnapshot participantGroup = context.Snapshot.BattleGroups.Single(group =>
                string.Equals(group.SourceForceId, participant.ParticipantId, StringComparison.Ordinal));
            participantGroup.MaxHitPoints = 100;
            runtimeOutcome.ActorOutcomes.Add(new BattleActorOutcome
            {
                ActorId = $"{participant.ParticipantId}:hero",
                BattleGroupId = participant.ParticipantId,
                FactionId = participant.FactionId,
                SourceForceId = participant.ParticipantId,
                SourceStateId = participant.HeroId,
                Kind = BattleRuntimeActorKind.Hero,
                Survived = true,
                RemainingHitPoints = 1
            });
            // Older transaction fixtures express survival as visible-row counts.
            // Convert that fixture input once into the single authoritative corps outcome.
            int remainingHitPoints = initialCount <= 0
                ? 0
                : (int)Math.Round(100 * Math.Clamp(survivorCount / (double)initialCount, 0.0, 1.0));
            runtimeOutcome.ActorOutcomes.Add(new BattleActorOutcome
            {
                ActorId = $"{participant.ParticipantId}:corps",
                BattleGroupId = participant.ParticipantId,
                FactionId = participant.FactionId,
                SourceForceId = participant.ParticipantId,
                SourceStateId = participant.CorpsInstanceId,
                Kind = BattleRuntimeActorKind.Corps,
                Survived = remainingHitPoints > 0,
                RemainingHitPoints = remainingHitPoints
            });
        }

        BattleEventStream eventStream = new();
        eventStream.Add(new BattleEvent
        {
            EventId = $"{session.SessionId}:started",
            BattleId = session.SessionId,
            Kind = BattleEventKind.BattleStarted
        });
        configureEvents?.Invoke(eventStream);
        eventStream.Add(new BattleEvent
        {
            EventId = $"{session.SessionId}:ended",
            BattleId = session.SessionId,
            Kind = BattleEventKind.BattleEnded
        });
        SettlementPlan settlement = new BattleSettlementService().BuildPlan(
            context.Snapshot.SnapshotId,
            runtimeOutcome,
            eventStream);
        BattleRuntimeSessionResult runtimeResult = new()
        {
            Outcome = runtimeOutcome,
            EventStream = eventStream
        };
        BattleReportRecord report = new BattleReportBuilder().Build(runtimeOutcome, eventStream, settlement);
        configureSettlementAndReport?.Invoke(settlement, report);
        AssertTrue(
            StrategicBattleActiveContextStore.TryPublishResultEnvelope(
                snapshotToken,
                context,
                runtimeResult,
                settlement,
                report,
                out _,
                out StrategicBattleActiveContextToken resultToken,
                out string envelopeFailureReason),
            $"completed fixture should publish one result envelope, got {envelopeFailureReason}");
        captureTokens?.Invoke(beginToken, snapshotToken, resultToken);
        return context;
    }

    private static StrategicBattleActiveContextToken PublishActiveContextForTest(
        StrategicBattleActiveContext context)
    {
        ResetActiveContextStore();
        AssertTrue(
            StrategicBattleActiveContextStore.TryBegin(
                context,
                out StrategicBattleActiveContextToken token,
                out string failureReason),
            $"test active context should publish, got {failureReason}");
        return token;
    }

    private static StrategicBattleActiveContextToken RequireActiveContextTokenForTest(
        StrategicBattleActiveContext context)
    {
        AssertTrue(
            StrategicBattleActiveContextStore.TryPeek(
                out StrategicBattleActiveContext storedContext,
                out StrategicBattleActiveContextToken token) &&
            ReferenceEquals(context, storedContext),
            "test should retain the expected published active context");
        return token;
    }

    private static int ResolveParticipantInitialCount(
        BattleStartRequest request,
        StrategicBattleParticipantReference participant)
    {
        return (request?.PlayerForces ?? new List<BattleForceRequest>())
            .Where(force =>
                string.Equals(force.StrategicParticipantId ?? "", participant.ParticipantId ?? "", StringComparison.Ordinal) ||
                string.Equals(force.StrategicCorpsInstanceId ?? "", participant.CorpsInstanceId ?? "", StringComparison.Ordinal))
            .Sum(force => Math.Max(0, force.Count));
    }

    private static void PopulateExplicitBattleConsequencesForTest(
        StrategicBattleResultSummary summary,
        StrategicManagementDefinitionSet definitions)
    {
        summary.HasConsequenceFacts = true;
        summary.OutcomeText = summary.Outcome == BattleOutcome.Victory ? "胜利" : "失败";
        definitions.Locations.TryGetValue(summary.TargetLocationId ?? "", out StrategicLocationDefinition? target);
        summary.TargetDisplayName = target?.DisplayName ?? summary.TargetLocationId ?? "";
        StrategicBattleRewardDefinition? reward = definitions.BattleRewards.Values.SingleOrDefault(item =>
            string.Equals(item.TargetLocationId, summary.TargetLocationId, StringComparison.Ordinal));
        if (reward == null)
        {
            return;
        }

        summary.EquipmentSampleIds.AddRange(reward.EquipmentSampleIds);
        bool victory = summary.Outcome == BattleOutcome.Victory && summary.ObjectiveSucceeded;
        summary.WorldChangeText = victory ? reward.VictorySummaryText : reward.DefeatSummaryText;
        summary.ProgressionText = victory ? reward.VictoryProgressionText : reward.DefeatProgressionText;
        if (!victory)
        {
            summary.FailureReasonText = "阵线失利";
            summary.RewardLines.Add($"未获得：{reward.DisplayName}");
            return;
        }

        summary.RewardClaimId = reward.RewardId;
        summary.ResourceRewards.AddRange(reward.VictoryResourceRewards.Select(item =>
            new StrategicResourceAmount(item.ResourceId, item.Amount)));
        if (!string.IsNullOrWhiteSpace(reward.RewardEquipmentSampleId))
        {
            summary.RewardEquipmentSampleIds.Add(reward.RewardEquipmentSampleId);
        }

        if (!string.IsNullOrWhiteSpace(reward.UnlockText))
        {
            summary.RewardLines.Add(reward.UnlockText);
        }
        foreach (StrategicResourceAmount amount in reward.VictoryResourceRewards)
        {
            definitions.Resources.TryGetValue(amount.ResourceId, out StrategicResourceDefinition? resource);
            summary.RewardLines.Add($"获得：{resource?.DisplayName ?? amount.ResourceId} +{amount.Amount}");
        }
        if (definitions.EquipmentSamples.TryGetValue(reward.RewardEquipmentSampleId, out StrategicEquipmentSampleDefinition? equipment))
        {
            summary.RewardLines.Add($"获得装备：{equipment.DisplayName}");
        }
    }

    private static void CompleteDirectBattleResultSummaryForTest(
        StrategicBattleResultSummary summary,
        StrategicBattleSession session)
    {
        summary.SnapshotId = $"{session.SessionId}:direct-summary-snapshot";
        summary.TerminationReason = summary.Outcome == BattleOutcome.Victory
            ? BattleTerminationReason.NormalVictory
            : BattleTerminationReason.NormalDefeat;
        summary.ObjectiveId = session.BattleObjectiveId;
        summary.ReportId = $"{session.SessionId}:direct-summary-report";
        summary.HasConsequenceFacts = true;

        foreach (StrategicBattleParticipantReference participant in session.Participants)
        {
            StrategicBattleParticipantRole role = participant.Role == StrategicBattleParticipantRole.Unknown
                ? StrategicBattleParticipantRole.Deployed
                : participant.Role;
            summary.ParticipantDispositions.Add(new StrategicBattleParticipantDisposition
            {
                ParticipantId = participant.ParticipantId,
                HeroId = participant.HeroId,
                CorpsInstanceId = participant.CorpsInstanceId,
                RollbackStationLocationId = participant.RollbackStationLocationId,
                Role = role
            });

            StrategicBattleParticipantResult? result = summary.Participants.SingleOrDefault(item =>
                string.Equals(item.HeroId, participant.HeroId, StringComparison.Ordinal) &&
                string.Equals(item.CorpsInstanceId, participant.CorpsInstanceId, StringComparison.Ordinal));
            if (role != StrategicBattleParticipantRole.Deployed || result == null)
            {
                continue;
            }

            result.ParticipantId = participant.ParticipantId;
            result.HeroState = result.RemainingCorpsStrength > 0
                ? StrategicHeroBattleState.Survived
                : StrategicHeroBattleState.Defeated;
            result.PreBattleCorpsStrength = participant.PreBattleCorpsStrength;
            result.StrengthLoss = participant.PreBattleCorpsStrength - result.RemainingCorpsStrength;
            result.CorpsEquipmentLevel = participant.CorpsEquipmentLevel;
            result.Routed = result.RemainingCorpsStrength == 0;
        }
    }

    private static BattleTerminationReason ToTerminationReason(BattleOutcome outcome)
    {
        return outcome switch
        {
            BattleOutcome.Victory => BattleTerminationReason.NormalVictory,
            BattleOutcome.Defeat => BattleTerminationReason.NormalDefeat,
            BattleOutcome.Withdraw => BattleTerminationReason.PlayerRetreat,
            _ => BattleTerminationReason.RuntimeException
        };
    }

    private static StrategicMusterTemplateAvailability FindTemplate(
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

    private static void AssertAvailable(IReadOnlyList<StrategicMusterTemplateAvailability> templates, string corpsDefinitionId)
    {
        StrategicMusterTemplateAvailability template = FindTemplate(templates, corpsDefinitionId);
        AssertTrue(template.IsAvailable, $"{corpsDefinitionId} should be available, got {string.Join(",", template.FailureReasons)}");
    }

    private static StrategicResourceViewModel FindResource(StrategicManagementDashboardViewModel dashboard, string resourceId)
    {
        StrategicResourceViewModel? resource = dashboard.Resources.FirstOrDefault(item => item.ResourceId == resourceId);
        if (resource == null)
        {
            throw new InvalidOperationException($"Missing resource {resourceId}");
        }

        return resource;
    }

    private static StrategicBuildingOptionViewModel FindBuildingOption(StrategicManagementDashboardViewModel dashboard, string buildingDefinitionId)
    {
        StrategicBuildingOptionViewModel? option = dashboard.SelectedCity.BuildingOptions.FirstOrDefault(item => item.BuildingDefinitionId == buildingDefinitionId);
        if (option == null)
        {
            throw new InvalidOperationException($"Missing building option {buildingDefinitionId}");
        }

        return option;
    }

    private static StrategicMusterTemplateViewModel FindMusterTemplate(StrategicManagementDashboardViewModel dashboard, string corpsDefinitionId)
    {
        StrategicMusterTemplateViewModel? template = dashboard.SelectedCity.MusterTemplates.FirstOrDefault(item => item.CorpsDefinitionId == corpsDefinitionId);
        if (template == null)
        {
            throw new InvalidOperationException($"Missing dashboard muster template {corpsDefinitionId}");
        }

        return template;
    }

    private static StrategicCorpsInstanceViewModel FindCorps(StrategicManagementDashboardViewModel dashboard, string corpsInstanceId)
    {
        StrategicCorpsInstanceViewModel? corps = dashboard.SelectedCity.CorpsInstances.FirstOrDefault(item => item.CorpsInstanceId == corpsInstanceId);
        if (corps == null)
        {
            throw new InvalidOperationException($"Missing dashboard corps instance {corpsInstanceId}");
        }

        return corps;
    }

    private static StrategicHeroAssignmentViewModel FindHero(StrategicManagementDashboardViewModel dashboard, string heroId)
    {
        StrategicHeroAssignmentViewModel? hero = dashboard.Heroes.FirstOrDefault(item => item.HeroId == heroId);
        if (hero == null)
        {
            throw new InvalidOperationException($"Missing dashboard hero {heroId}");
        }

        return hero;
    }

    private static StrategicHeroCompanyViewModel FindHeroCompany(StrategicManagementDashboardViewModel dashboard, string heroId)
    {
        StrategicHeroCompanyViewModel? company = dashboard.SelectedCity.HeroCompanies.FirstOrDefault(item => item.HeroId == heroId);
        if (company == null)
        {
            throw new InvalidOperationException($"Missing dashboard battle group {heroId}");
        }

        return company;
    }

    private static StrategicCorpsInstanceState FindAssignedCorps(StrategicManagementState state, string heroId)
    {
        string corpsInstanceId = state.Heroes[heroId].AssignedCorpsInstanceId;
        return state.CorpsInstances[corpsInstanceId];
    }

    private static void AssertForceMappedToHero(BattleStartRequest request, string heroId)
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

    private static void AttachStrategicLaunchFlatTopology(
        BattleStartRequest request,
        int minX = -2,
        int minY = -2,
        int maxX = 32,
        int maxY = 24)
    {
        if (request == null)
        {
            return;
        }

        request.NavigationSurfaces.Clear();
        request.NavigationConnections.Clear();
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                request.NavigationSurfaces.Add(new BattleNavigationSurfaceSnapshot
                {
                    X = x,
                    Y = y,
                    Height = 0,
                    MoveCost = 1
                });
            }
        }

        request.NavigationTopology = BattleNavigationTopologyCompiler.Compile(
            request.NavigationSurfaces,
            request.NavigationConnections);
    }

    private static StrategicManagementDashboardViewModel InvokeLocationDashboard(
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

    private static StrategicManagementDashboardViewModel InvokeHeroCorpsWorkbenchDashboard(
        StrategicManagementViewModelService viewModels,
        StrategicManagementState state,
        string factionId,
        string cityId,
        string heroId)
    {
        System.Reflection.MethodInfo? method = typeof(StrategicManagementViewModelService).GetMethod(
            "BuildHeroCorpsWorkbenchDashboard",
            new[] { typeof(StrategicManagementState), typeof(string), typeof(string), typeof(string) });
        if (method == null)
        {
            throw new InvalidOperationException("view model service should expose BuildHeroCorpsWorkbenchDashboard(state, factionId, cityId, heroId)");
        }

        return (StrategicManagementDashboardViewModel)method.Invoke(viewModels, new object[] { state, factionId, cityId, heroId })!;
    }

    private static StrategicManagementDashboardViewModel InvokeRuntimeLocationDashboard(string factionId, string locationId)
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

    private static IReadOnlyList<StrategicResourceAmount> InvokeLocationProduction(
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

    private static StrategicCommandResult InvokeSettleLocationProduction(
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

    private static StrategicCommandResult InvokeSettleElapsedWorldTime(
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

    private static StrategicCommandResult InvokeRecruitCorpsForHero(
        StrategicManagementCommandService commands,
        StrategicManagementState state,
        string cityId,
        string heroId,
        string corpsDefinitionId)
    {
        System.Reflection.MethodInfo? method = typeof(StrategicManagementCommandService).GetMethod(
            "RecruitCorpsForHero",
            new[] { typeof(StrategicManagementState), typeof(string), typeof(string), typeof(string) });
        if (method == null)
        {
            throw new InvalidOperationException("command service should expose RecruitCorpsForHero(state, cityId, heroId, corpsDefinitionId)");
        }

        return (StrategicCommandResult)method.Invoke(commands, new object[] { state, cityId, heroId, corpsDefinitionId })!;
    }

    private static StrategicCommandResult InvokeRuntimeSettleElapsedWorldTime(int elapsedPulses)
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

    private static void InvokeRuntimePauseWorldTimeForCityManagement()
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

    private static void InvokeRuntimeResumeWorldMapTime()
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

    private static int GetElapsedWorldTimePulses(StrategicManagementState state)
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

    private static T GetRequiredProperty<T>(object instance, string propertyName)
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

    private static string ProjectRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "rpg.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate project root from test output directory.");
    }

    private static int FindStrategicAmount(IReadOnlyCollection<StrategicResourceAmount> amounts, string resourceId)
    {
        StrategicResourceAmount? amount = amounts.FirstOrDefault(item => item.ResourceId == resourceId);
        if (amount == null)
        {
            throw new InvalidOperationException($"Missing strategic resource amount {resourceId}");
        }

        return amount.Amount;
    }

    private static int FindReflectedAmount(IEnumerable<object> amounts, string resourceId)
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

    private static void AssertContains(IReadOnlyCollection<string> values, string expected, string message)
    {
        if (!values.Contains(expected))
        {
            throw new InvalidOperationException($"{message}. Expected to contain {expected}; actual={string.Join(",", values)}");
        }
    }

    private static void AssertResPathExists(string projectRoot, string resPath, string message)
    {
        AssertTrue(resPath.StartsWith("res://", StringComparison.Ordinal), $"{message}. Expected a res:// path, actual={resPath}");
        string localPath = Path.Combine(projectRoot, resPath["res://".Length..].Replace('/', Path.DirectorySeparatorChar));
        AssertTrue(File.Exists(localPath), $"{message}. Missing file path={localPath}");
    }

    private static void AssertPreviewUnitResourceExists(
        string projectRoot,
        IReadOnlyDictionary<string, string> unitDefinitionIndex,
        string battleUnitId,
        string message)
    {
        AssertTrue(!battleUnitId.StartsWith("res://", StringComparison.Ordinal), $"{message}. Expected a battle unit id, actual={battleUnitId}");
        AssertTrue(!battleUnitId.EndsWith(".png", StringComparison.OrdinalIgnoreCase), $"{message}. Expected a battle unit id, not a raw PNG path");
        AssertTrue(unitDefinitionIndex.TryGetValue(battleUnitId, out string? unitResourcePath), $"{message}. Missing battle unit index entry id={battleUnitId}");
        unitResourcePath ??= "";
        AssertTrue(
            unitResourcePath.StartsWith("res://resource/battle/units/", StringComparison.Ordinal) &&
            unitResourcePath.EndsWith("/unit.tres", StringComparison.Ordinal),
            $"{message}. Expected authored battle unit resource path, actual={unitResourcePath}");
        AssertResPathExists(projectRoot, unitResourcePath, message);
    }

    private static void AssertNoLegacyWorldReferences(Type type)
    {
        foreach (Type referenced in EnumerateReferencedTypes(type))
        {
            if (referenced.Namespace == "Rpg.Domain.World")
            {
                throw new InvalidOperationException($"{type.FullName} must not reference legacy world state type {referenced.FullName}");
            }
        }
    }

    private static IEnumerable<Type> EnumerateReferencedTypes(Type type)
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

    private static Type UnwrapType(Type type)
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

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message}. Expected={expected} Actual={actual}");
        }
    }

    private static void AssertThrowsInvalidOperation(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }
}
