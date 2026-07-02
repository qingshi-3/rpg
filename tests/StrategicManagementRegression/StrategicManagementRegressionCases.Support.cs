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
            AttackImpactDelaySeconds = 0.45
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
            AttackImpactDelaySeconds = 0.45
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
        Func<StrategicBattleParticipantReference, int> survivingCorpsActors)
    {
        StrategicBattleActiveContextResult contextResult = bridge.CreateActiveContext(state, session, request);
        AssertTrue(contextResult.Success, $"active context should be created, got {contextResult.FailureReason}");
        StrategicBattleActiveContext context = contextResult.Context;
        BattleOutcomeResult runtimeOutcome = BattleOutcomeResult.Completed(
            context.Snapshot.SnapshotId,
            session.SessionId,
            ToTerminationReason(outcome));
        foreach (StrategicBattleParticipantReference participant in session.Participants)
        {
            int survivorCount = Math.Max(0, survivingCorpsActors?.Invoke(participant) ?? 0);
            int initialCount = ResolveParticipantInitialCount(request, participant);
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
            for (int index = 0; index < survivorCount; index++)
            {
                runtimeOutcome.ActorOutcomes.Add(new BattleActorOutcome
                {
                    ActorId = $"{participant.ParticipantId}:corps:{index}",
                    BattleGroupId = participant.ParticipantId,
                    FactionId = participant.FactionId,
                    SourceForceId = participant.ParticipantId,
                    SourceStateId = participant.CorpsInstanceId,
                    Kind = BattleRuntimeActorKind.Corps,
                    Survived = true,
                    RemainingHitPoints = 1
                });
            }

            // Bridge summaries must consume explicit runtime actor outcomes.
            // Defeated corps actors are recorded here instead of being inferred from outcome.
            for (int index = survivorCount; index < initialCount; index++)
            {
                runtimeOutcome.ActorOutcomes.Add(new BattleActorOutcome
                {
                    ActorId = $"{participant.ParticipantId}:corps:{index}",
                    BattleGroupId = participant.ParticipantId,
                    FactionId = participant.FactionId,
                    SourceForceId = participant.ParticipantId,
                    SourceStateId = participant.CorpsInstanceId,
                    Kind = BattleRuntimeActorKind.Corps,
                    Survived = false,
                    RemainingHitPoints = 0
                });
            }
        }

        BattleEventStream eventStream = BuildEndedStream(session.SessionId);
        SettlementPlan settlement = new BattleSettlementService().BuildPlan(
            context.Snapshot.SnapshotId,
            runtimeOutcome,
            eventStream);
        context.RuntimeResult = new BattleRuntimeSessionResult
        {
            Outcome = runtimeOutcome,
            EventStream = eventStream
        };
        context.SettlementPlan = settlement;
        context.Report = new BattleReportBuilder().Build(runtimeOutcome, eventStream, settlement);
        return context;
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

    private static StrategicCommandResult InvokeManualConscriptReserveForces(
        StrategicManagementCommandService commands,
        StrategicManagementState state,
        string cityId)
    {
        System.Reflection.MethodInfo? method = typeof(StrategicManagementCommandService).GetMethod(
            "ManualConscriptReserveForces",
            new[] { typeof(StrategicManagementState), typeof(string) });
        if (method == null)
        {
            throw new InvalidOperationException("command service should expose ManualConscriptReserveForces(state, cityId)");
        }

        return (StrategicCommandResult)method.Invoke(commands, new object[] { state, cityId })!;
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

    private static StrategicCommandResult InvokeSetAutoConscriptionIntensity(
        StrategicManagementCommandService commands,
        StrategicManagementState state,
        string cityId,
        string intensityId)
    {
        System.Reflection.MethodInfo? method = typeof(StrategicManagementCommandService).GetMethod(
            "SetAutoConscriptionIntensity",
            new[] { typeof(StrategicManagementState), typeof(string), typeof(string) });
        if (method == null)
        {
            throw new InvalidOperationException("command service should expose SetAutoConscriptionIntensity(state, cityId, intensityId)");
        }

        return (StrategicCommandResult)method.Invoke(commands, new object[] { state, cityId, intensityId })!;
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
