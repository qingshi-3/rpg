using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Application.StrategicBattleBridge;
using Rpg.Application.StrategicManagement;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;
internal static partial class StrategicManagementRegressionCases
{
    internal static void StrategicManagementDashboardSummarizesCityResourcesFacilitiesCorpsAndHeroes()
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

    internal static void StrategicManagementDashboardExposesDispatchableHeroCompanies()
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
        AssertEqual(3, beforeDispatch.SelectedCity.HeroCompanies.Count, "first city should start with three in-city expedition candidates");

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
        AssertEqual(2, afterDispatch.SelectedCity.HeroCompanies.Count, "dispatched hero company should leave the source city's expedition roster");
        AssertTrue(
            afterDispatch.SelectedCity.HeroCompanies.All(item => item.HeroId != StrategicManagementIds.HeroOrdinaryCommander),
            "source city expedition roster must not keep the hero company already on expedition");
        AssertEqual(
            StrategicCorpsInstanceStatus.Expedition,
            FindCorps(afterDispatch, corpsInstanceId).Status,
            "dispatched corps should remain durable strategic state with expedition status");
    }

    internal static void StrategicManagementDashboardExplainsUnavailableBeastMusterReasons()
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

    internal static void StrategicManagementDashboardReflectsCommandMutations()
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

    internal static void StrategicManagementDashboardSummarizesNonCityLocation()
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

    internal static void StrategicManagementSettlesControlledResourceSiteProduction()
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

    internal static void StrategicManagementUsesElapsedWorldTimeNamingInsteadOfStepNaming()
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

    internal static void StrategicManagementSettlesElapsedWorldTimeAndControlledProduction()
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

    internal static void StrategicManagementElapsedWorldTimeSkipsEnemyHeldProduction()
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

    internal static void StrategicManagementElapsedWorldTimeRejectsInvalidPulseCountWithoutMutation()
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

    internal static void StrategicManagementRuntimeBlocksElapsedTimeWhileCityManagementPaused()
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

    internal static void StrategicManagementRuntimeSettlesElapsedTimeAfterWorldMapResumes()
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

    internal static void StrategicManagementRuntimeBuildsDashboardFromRetainedCommandState()
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

    internal static void StrategicManagementApplicationHasNoLegacyWorldStateDependency()
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
}
