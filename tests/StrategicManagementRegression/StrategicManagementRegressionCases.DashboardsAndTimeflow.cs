using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Application.StrategicBattleBridge;
using Rpg.Application.StrategicManagement;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;

internal static partial class StrategicManagementRegressionCases
{
    internal static void StrategicManagementDashboardSummarizesCityResourcesBuildingsReserveCorpsAndHeroes()
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
        AssertEqual(0, dashboard.SelectedCity.Buildings.Count, "new city should have no placed buildings");
        AssertTrue(dashboard.SelectedCity.ConstructionRegions.Count >= 3, "dashboard should expose construction regions");
        AssertEqual(220, dashboard.SelectedCity.CityForceCapacity, "dashboard should expose city force capacity");
        AssertEqual(80, dashboard.SelectedCity.ReserveForces, "dashboard should expose reserve soldiers");
        AssertEqual(100, dashboard.SelectedCity.ActiveForces, "dashboard should expose derived active forces");
        AssertEqual(40, dashboard.SelectedCity.RemainingForceCapacity, "dashboard should expose remaining capacity");
        AssertEqual(500, FindResource(dashboard, StrategicManagementIds.ResourceMoney).Amount, "money should be summarized");
        AssertEqual(240, FindResource(dashboard, StrategicManagementIds.ResourceWood).Amount, "wood should be summarized");
        AssertTrue(FindBuildingOption(dashboard, StrategicManagementIds.BuildingTrainingGround).CanBuild, "training ground should be buildable initially");
        AssertTrue(FindMusterTemplate(dashboard, StrategicManagementIds.CorpsShieldLine).CanCreate, "shield line should be creatable initially");
        AssertEqual(3, dashboard.Heroes.Count, "dashboard should expose the first playable strategic heroes");
        AssertTrue(
            !string.IsNullOrWhiteSpace(FindHero(dashboard, StrategicManagementIds.HeroCavalryCaptain).AssignedCorpsInstanceId),
            "cavalry captain should start with an assigned cavalry corps company");
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
        AssertTrue(company.CanCreateExpedition, $"assigned battle group should be dispatchable, got {company.DisabledReason}");
        AssertEqual(corpsInstanceId, company.CorpsInstanceId, "battle group should expose the assigned corps instance");
        AssertEqual(StrategicManagementIds.CorpsShieldLine, company.CorpsDefinitionId, "battle group should expose the corps definition");
        AssertEqual(3, beforeDispatch.SelectedCity.HeroCompanies.Count, "first city should start with three in-city expedition candidates");
        AssertEqual(100, beforeDispatch.SelectedCity.ActiveForces, "derived active forces should include in-city assigned corps before dispatch");

        commands.CreateExpedition(
            state,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.LocationBonefieldOutpost,
            StrategicExpeditionIntent.AssaultLocation,
            StrategicManagementIds.HeroOrdinaryCommander);
        StrategicManagementDashboardViewModel afterDispatch = viewModels.BuildDashboard(
            state,
            StrategicManagementIds.FactionPlayer,
            StrategicManagementIds.LocationPlainsCity);
        AssertEqual(2, afterDispatch.SelectedCity.HeroCompanies.Count, "dispatched battle group should leave the source city's expedition roster");
        AssertTrue(
            afterDispatch.SelectedCity.HeroCompanies.All(item => item.HeroId != StrategicManagementIds.HeroOrdinaryCommander),
            "source city expedition roster must not keep the battle group already on expedition");
        AssertTrue(
            afterDispatch.SelectedCity.CorpsInstances.All(item => item.CorpsInstanceId != corpsInstanceId),
            "source city corps list must not keep the company after it leaves on expedition");
        int dispatchedForceCost = definitions.Corps[StrategicManagementIds.CorpsShieldLine].SoldierCapacityCost;
        AssertEqual(
            100 - dispatchedForceCost,
            afterDispatch.SelectedCity.ActiveForces,
            "active forces should exclude expedition corps after it leaves the source city");
    }

    internal static void StrategicManagementDashboardReflectsFoundationCommandMutations()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementRules rules = new(definitions);
        StrategicManagementCommandService commands = new(definitions, rules);
        StrategicManagementViewModelService viewModels = new(definitions, rules);
        StrategicConstructionRegionDefinition military = FindRegion(definitions, StrategicManagementIds.RegionPlainsMilitary);

        StrategicCommandResult build = commands.BuildCityBuilding(
            state,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.BuildingTrainingGround,
            StrategicManagementIds.RegionPlainsMilitary,
            military.OriginX,
            military.OriginY);
        AssertTrue(build.Success, $"building training ground should succeed, got {build.FailureReason}");
        StrategicCommandResult unassign = commands.UnassignCorpsFromHero(state, StrategicManagementIds.HeroCavalryCaptain);
        AssertTrue(unassign.Success, $"test setup unassignment should succeed, got {unassign.FailureReason}");
        StrategicCommandResult create = commands.CreateCorps(
            state,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.CorpsCavalryLine);
        AssertTrue(create.Success, $"creating cavalry corps should succeed, got {create.FailureReason}");
        commands.AssignCorpsToHero(state, StrategicManagementIds.HeroCavalryCaptain, create.CreatedEntityId);

        StrategicManagementDashboardViewModel dashboard = viewModels.BuildDashboard(
            state,
            StrategicManagementIds.FactionPlayer,
            StrategicManagementIds.LocationPlainsCity);

        AssertEqual(1, dashboard.SelectedCity.Buildings.Count, "built training ground should be listed");
        AssertEqual(StrategicManagementIds.BuildingTrainingGround, dashboard.SelectedCity.Buildings[0].BuildingDefinitionId, "built building should expose definition id");
        AssertEqual(220, dashboard.SelectedCity.CityForceCapacity, "building placement should not apply retired direct force-capacity scalar effects");
        StrategicCorpsInstanceViewModel corps = FindCorps(dashboard, create.CreatedEntityId);
        AssertEqual("辉光龙骑", corps.DisplayName, "created corps should use definition display name");
        AssertEqual(StrategicCorpsInstanceStatus.AssignedToHero, corps.Status, "assigned corps status should be reflected");
        StrategicHeroAssignmentViewModel hero = FindHero(dashboard, StrategicManagementIds.HeroCavalryCaptain);
        AssertEqual(create.CreatedEntityId, hero.AssignedCorpsInstanceId, "hero row should show assigned corps");
        AssertEqual("辉光龙骑", hero.AssignedCorpsDisplayName, "hero row should show assigned corps display name");
        AssertEqual(StrategicHeroCorpsAptitudeGrade.B, hero.AptitudeGrade, "hero row should show derived aptitude");
    }

    internal static void StrategicManagementDashboardSeparatesConscriptionFromCorpsRecruitment()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementViewModelService viewModels = new(definitions, new StrategicManagementRules(definitions));

        StrategicManagementDashboardViewModel dashboard = viewModels.BuildDashboard(
            state,
            StrategicManagementIds.FactionPlayer,
            StrategicManagementIds.LocationPlainsCity);
        object conscription = GetRequiredProperty<object>(dashboard.SelectedCity, "Conscription");
        object manualOption = GetRequiredProperty<object>(conscription, "ManualOption");
        IEnumerable<object> intensityOptions = GetRequiredProperty<IEnumerable<object>>(conscription, "IntensityOptions");

        AssertEqual("conscription_off", GetRequiredProperty<string>(conscription, "CurrentIntensityId"), "dashboard should expose the current auto-conscription intensity");
        AssertEqual(80, GetRequiredProperty<int>(conscription, "ReserveForces"), "conscription page should expose reserve pool state");
        AssertEqual(40, GetRequiredProperty<int>(conscription, "RemainingForceCapacity"), "conscription page should expose remaining reserve capacity");
        AssertEqual(10, GetRequiredProperty<int>(manualOption, "ReserveGain"), "manual conscription option should expose the accepted batch size");
        AssertEqual(true, GetRequiredProperty<bool>(manualOption, "CanConscript"), "manual conscription should initially be available");
        AssertEqual(4, intensityOptions.Count(), "conscription page should expose off/low/standard/high automatic intensity options");
        AssertTrue(
            intensityOptions.Any(option =>
                GetRequiredProperty<string>(option, "IntensityId") == "conscription_standard" &&
                GetRequiredProperty<string>(option, "DisabledReason") == StrategicFailureReasons.MissingBuilding),
            "standard auto conscription should be present but disabled before the training ground is built");
        AssertTrue(
            dashboard.SelectedCity.MusterTemplates.Count > 0 &&
            dashboard.SelectedCity.MusterTemplates.All(template => !string.IsNullOrWhiteSpace(template.CorpsDefinitionId)),
            "corps recruitment should remain a separate muster-template list from conscription");
    }

    internal static void StrategicManagementDashboardProjectsHeroCorpsReplacementCost()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementViewModelService viewModels = new(definitions, new StrategicManagementRules(definitions));

        StrategicManagementDashboardViewModel dashboard = InvokeHeroCorpsWorkbenchDashboard(
            viewModels,
            state,
            StrategicManagementIds.FactionPlayer,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.HeroOrdinaryCommander);
        StrategicMusterTemplateViewModel cavalry = FindMusterTemplate(dashboard, StrategicManagementIds.CorpsCavalryLine);

        AssertEqual(40, cavalry.ReserveForceCost, "cavalry option should expose direct reserve consumption");
        AssertEqual(30, GetRequiredProperty<int>(cavalry, "ReserveForceRefund"), "workbench option should project full old-corps reserve refund");
        AssertEqual(10, GetRequiredProperty<int>(cavalry, "NetReserveForceCost"), "workbench option should project final reserve cost after refund");
        IEnumerable<object> refundCost = GetRequiredProperty<IEnumerable<object>>(cavalry, "RefundCost");
        IEnumerable<object> netCost = GetRequiredProperty<IEnumerable<object>>(cavalry, "NetCost");
        AssertEqual(30, FindReflectedAmount(refundCost, StrategicManagementIds.ResourceMoney), "workbench option should project old-corps money refund");
        AssertEqual(20, FindReflectedAmount(refundCost, StrategicManagementIds.ResourceFood), "workbench option should project old-corps food refund");
        AssertEqual(15, FindReflectedAmount(netCost, StrategicManagementIds.ResourceMoney), "workbench option should project net money cost");
        AssertEqual(10, FindReflectedAmount(netCost, StrategicManagementIds.ResourceFood), "workbench option should project net food cost");

        string oldCorpsId = state.Heroes[StrategicManagementIds.HeroOrdinaryCommander].AssignedCorpsInstanceId;
        state.CorpsInstances[oldCorpsId].Strength = 50;
        StrategicManagementDashboardViewModel damagedDashboard = InvokeHeroCorpsWorkbenchDashboard(
            viewModels,
            state,
            StrategicManagementIds.FactionPlayer,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.HeroOrdinaryCommander);
        StrategicMusterTemplateViewModel archer = FindMusterTemplate(damagedDashboard, StrategicManagementIds.CorpsArcherLine);

        AssertEqual(15, GetRequiredProperty<int>(archer, "ReserveForceRefund"), "damaged old corps should refund only current-strength reserve value");
        AssertEqual(15, GetRequiredProperty<int>(archer, "NetReserveForceCost"), "damaged old corps should leave the correct net reserve cost");
        refundCost = GetRequiredProperty<IEnumerable<object>>(archer, "RefundCost");
        netCost = GetRequiredProperty<IEnumerable<object>>(archer, "NetCost");
        AssertEqual(15, FindReflectedAmount(refundCost, StrategicManagementIds.ResourceMoney), "damaged old corps should refund only current-strength money value");
        AssertEqual(10, FindReflectedAmount(refundCost, StrategicManagementIds.ResourceFood), "damaged old corps should refund only current-strength food value");
        AssertEqual(20, FindReflectedAmount(netCost, StrategicManagementIds.ResourceMoney), "damaged old corps should leave correct net money cost");
        AssertEqual(10, FindReflectedAmount(netCost, StrategicManagementIds.ResourceFood), "damaged old corps should leave correct net food cost");
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

        StrategicManagementDashboardViewModel targetDashboard = InvokeLocationDashboard(
            viewModels,
            state,
            StrategicManagementIds.FactionPlayer,
            StrategicManagementIds.LocationBonefieldOutpost);
        object targetLocation = GetRequiredProperty<object>(targetDashboard, "SelectedLocation");
        AssertEqual(StrategicManagementIds.MapSiteBonefield, GetRequiredProperty<string>(targetLocation, "MapSiteId"), "foundation target should expose the Bonefield map-site id");
        AssertEqual(StrategicLocationKind.City, GetRequiredProperty<StrategicLocationKind>(targetLocation, "Kind"), "foundation target should be an enemy-held managed stronghold/city");
        AssertEqual("", GetRequiredProperty<string>(targetLocation, "SourcePermissionDisplayText"), "foundation target should not expose beast source permission text");
        AssertEqual(StrategicManagementIds.FactionEnemy, GetRequiredProperty<string>(targetLocation, "OwnerFactionId"), "foundation target should start enemy-held");
        AssertEqual(StrategicLocationControlState.EnemyHeld, GetRequiredProperty<StrategicLocationControlState>(targetLocation, "ControlState"), "foundation target should start enemy-held");
        AssertEqual(false, GetRequiredProperty<bool>(targetLocation, "CanManageCity"), "enemy-held Bonefield should not expose player city management before victory");
        AssertEqual("", targetDashboard.SelectedCity.LocationId, "enemy-held Bonefield should not bind city-management detail before victory");

        StrategicCommandResult occupy = commands.OccupyLocation(
            state,
            StrategicManagementIds.LocationBonefieldOutpost,
            StrategicManagementIds.FactionPlayer);
        AssertTrue(occupy.Success, "occupying foundation target should succeed before dashboard refresh");

        StrategicManagementDashboardViewModel occupiedTargetDashboard = InvokeLocationDashboard(
            viewModels,
            state,
            StrategicManagementIds.FactionPlayer,
            StrategicManagementIds.LocationBonefieldOutpost);
        object occupiedTargetLocation = GetRequiredProperty<object>(occupiedTargetDashboard, "SelectedLocation");
        AssertEqual(StrategicManagementIds.FactionPlayer, GetRequiredProperty<string>(occupiedTargetLocation, "OwnerFactionId"), "location dashboard should reflect command-mutated owner");
        AssertEqual(StrategicLocationControlState.PlayerHeld, GetRequiredProperty<StrategicLocationControlState>(occupiedTargetLocation, "ControlState"), "location dashboard should reflect command-mutated control");
        AssertEqual(true, GetRequiredProperty<bool>(occupiedTargetLocation, "CanManageCity"), "player-held Bonefield should expose city management after victory writeback");
        AssertEqual(StrategicManagementIds.LocationBonefieldOutpost, occupiedTargetDashboard.SelectedCity.LocationId, "occupied Bonefield should bind its own isolated city-management state");

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
            FindStrategicAmount(productionPerWorldTimePulse, StrategicManagementIds.ResourceWood),
            "timber site should define foundation wood production");

        IReadOnlyList<StrategicResourceAmount> projected = InvokeLocationProduction(
            rules,
            state,
            StrategicManagementIds.LocationTimberSite,
            StrategicManagementIds.FactionPlayer,
            2);
        AssertEqual(
            24,
            FindStrategicAmount(projected, StrategicManagementIds.ResourceWood),
            "rules should project timber production by requested elapsed world-map pulses");

        StrategicManagementDashboardViewModel dashboard = viewModels.BuildLocationDashboard(
            state,
            StrategicManagementIds.FactionPlayer,
            StrategicManagementIds.LocationTimberSite);
        object location = GetRequiredProperty<object>(dashboard, "SelectedLocation");
        AssertEqual(
            "木材 +12 / 大地图时间",
            GetRequiredProperty<string>(location, "ProductionDisplayText"),
            "location dashboard should expose production summary");
        IEnumerable<object> productionView = GetRequiredProperty<IEnumerable<object>>(location, "ProductionPerWorldTimePulse");
        AssertEqual(
            12,
            FindReflectedAmount(productionView, StrategicManagementIds.ResourceWood),
            "location dashboard should expose production amounts as view models");

        int beforeWood = state.GetResourceAmount(
            StrategicManagementIds.FactionPlayer,
            StrategicManagementIds.ResourceWood);
        StrategicCommandResult settle = InvokeSettleLocationProduction(
            commands,
            state,
            StrategicManagementIds.LocationTimberSite,
            StrategicManagementIds.FactionPlayer,
            2);
        AssertTrue(settle.Success, $"settling player-held timber production should succeed, got {settle.FailureReason}");
        AssertEqual(
            beforeWood + 24,
            state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceWood),
            "settlement should add production into faction-shared resources");
        AssertTrue(
            settle.Events.Any(item => item.Kind == "StrategicLocationProductionSettled"),
            "settlement command should emit a low-noise production event");

        StrategicCommandResult lose = commands.LoseLocation(
            state,
            StrategicManagementIds.LocationTimberSite,
            StrategicManagementIds.FactionEnemy);
        AssertTrue(lose.Success, "losing timber site should succeed before rejection check");

        int beforeRejectedWood = state.GetResourceAmount(
            StrategicManagementIds.FactionPlayer,
            StrategicManagementIds.ResourceWood);
        StrategicCommandResult rejected = InvokeSettleLocationProduction(
            commands,
            state,
            StrategicManagementIds.LocationTimberSite,
            StrategicManagementIds.FactionPlayer,
            1);
        AssertTrue(!rejected.Success, "player production settlement should fail after the resource site is enemy-held");
        AssertEqual(StrategicFailureReasons.FactionMismatch, rejected.FailureReason, "enemy-held production rejection should report faction mismatch");
        AssertEqual(
            beforeRejectedWood,
            state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceWood),
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

    internal static void StrategicManagementSettlesElapsedWorldTimeWithCityEconomyBuildingProduction()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
        StrategicCityState city = state.Cities[StrategicManagementIds.LocationPlainsCity];
        StrategicConstructionRegionDefinition economy = FindRegion(definitions, StrategicManagementIds.RegionPlainsEconomy);
        StrategicConstructionRegionDefinition military = FindRegion(definitions, StrategicManagementIds.RegionPlainsMilitary);
        StrategicCommandResult buildFarm = commands.BuildCityBuilding(
            state,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.BuildingFarm,
            StrategicManagementIds.RegionPlainsEconomy,
            economy.OriginX,
            economy.OriginY);
        AssertTrue(buildFarm.Success, $"farm setup should succeed, got {buildFarm.FailureReason}");
        StrategicCommandResult buildTraining = commands.BuildCityBuilding(
            state,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.BuildingTrainingGround,
            StrategicManagementIds.RegionPlainsMilitary,
            military.OriginX,
            military.OriginY);
        AssertTrue(buildTraining.Success, $"training setup should succeed, got {buildTraining.FailureReason}");
        city.ReserveForces = 10;
        int beforeFood = state.GetResourceAmount(
            StrategicManagementIds.FactionPlayer,
            StrategicManagementIds.ResourceFood);
        int beforeWood = state.GetResourceAmount(
            StrategicManagementIds.FactionPlayer,
            StrategicManagementIds.ResourceWood);

        AssertEqual(0, GetElapsedWorldTimePulses(state), "player start should begin before elapsed world-map time has been settled");

        StrategicCommandResult result = InvokeSettleElapsedWorldTime(
            commands,
            state,
            StrategicManagementIds.FactionPlayer,
            2);

        AssertTrue(result.Success, $"settling elapsed world time should succeed, got {result.FailureReason}");
        AssertEqual(2, GetElapsedWorldTimePulses(state), "settlement should add requested pulses to durable world-map time");
        AssertEqual(
            beforeFood + 16,
            state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceFood),
            "constructed farm should produce food during elapsed world-map settlement");
        AssertEqual(
            beforeWood + 24,
            state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceWood),
            "controlled resource-site production should still be settled for every elapsed pulse");
        AssertEqual(10, city.ReserveForces, "built training ground should not recover reserves through retired per-building scalar effects");
        AssertTrue(
            result.Events.Any(item => item.Kind == "StrategicWorldTimeSettled"),
            "elapsed-time settlement should emit a command-level time event");
        AssertTrue(
            result.Events.Any(item => item.Kind == "StrategicCityBuildingProductionSettled"),
            "global advancement should emit a city-building production event for constructed economy buildings");
        AssertTrue(
            !result.Events.Any(item => item.Kind == "StrategicCityReserveRecovered"),
            "global advancement should not emit retired city reserve recovery events");
    }

    internal static void StrategicManagementSettlesLowAutoConscriptionDuringElapsedWorldTime()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
        StrategicCityState city = state.Cities[StrategicManagementIds.LocationPlainsCity];
        city.ReserveForces = 0;
        int beforeMoney = state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceMoney);
        int beforeFood = state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceFood);

        StrategicCommandResult policy = InvokeSetAutoConscriptionIntensity(
            commands,
            state,
            StrategicManagementIds.LocationPlainsCity,
            "conscription_low");
        AssertTrue(policy.Success, $"low auto conscription should be selectable, got {policy.FailureReason}");

        StrategicCommandResult result = InvokeSettleElapsedWorldTime(
            commands,
            state,
            StrategicManagementIds.FactionPlayer,
            1);

        AssertTrue(result.Success, $"elapsed world time should settle with low auto conscription, got {result.FailureReason}");
        AssertEqual(2, city.ReserveForces, "low auto conscription should add reserve soldiers during world-map time");
        AssertEqual(beforeMoney - 2, state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceMoney), "low auto conscription should spend money");
        AssertEqual(beforeFood - 3, state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceFood), "low auto conscription should spend food");
        AssertTrue(
            result.Events.Any(item => item.Kind == "StrategicCityReserveForcesAutoConscripted"),
            "auto conscription settlement should emit a reserve-soldier event");
    }

    internal static void StrategicManagementStandardAutoConscriptionRequiresTrainingGround()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
        StrategicConstructionRegionDefinition military = FindRegion(definitions, StrategicManagementIds.RegionPlainsMilitary);
        StrategicCityState city = state.Cities[StrategicManagementIds.LocationPlainsCity];

        StrategicCommandResult missingTrainingGround = InvokeSetAutoConscriptionIntensity(
            commands,
            state,
            StrategicManagementIds.LocationPlainsCity,
            "conscription_standard");
        AssertTrue(!missingTrainingGround.Success, "standard auto conscription should fail before the training ground exists");
        AssertEqual(StrategicFailureReasons.MissingBuilding, missingTrainingGround.FailureReason, "missing training ground should be explicit");
        AssertEqual("conscription_off", GetRequiredProperty<string>(city, "AutoConscriptionIntensityId"), "failed policy selection must not mutate the city intensity");

        StrategicCommandResult buildTraining = commands.BuildCityBuilding(
            state,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.BuildingTrainingGround,
            StrategicManagementIds.RegionPlainsMilitary,
            military.OriginX,
            military.OriginY);
        AssertTrue(buildTraining.Success, $"training ground setup should succeed, got {buildTraining.FailureReason}");

        StrategicCommandResult policy = InvokeSetAutoConscriptionIntensity(
            commands,
            state,
            StrategicManagementIds.LocationPlainsCity,
            "conscription_standard");
        AssertTrue(policy.Success, $"standard auto conscription should be selectable after training ground, got {policy.FailureReason}");
        city.ReserveForces = 0;
        int beforeMoney = state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceMoney);
        int beforeFood = state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceFood);

        StrategicCommandResult result = InvokeSettleElapsedWorldTime(
            commands,
            state,
            StrategicManagementIds.FactionPlayer,
            1);

        AssertTrue(result.Success, $"elapsed world time should settle with standard auto conscription, got {result.FailureReason}");
        AssertEqual(6, city.ReserveForces, "standard auto conscription should add the accepted reserve batch");
        AssertEqual(beforeMoney - 5, state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceMoney), "standard auto conscription should spend money");
        AssertEqual(beforeFood - 8, state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceFood), "standard auto conscription should spend food");
    }

    internal static void StrategicManagementAutoConscriptionSkipsWithoutResourcesOrCapacity()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementRules rules = new(definitions);
        StrategicManagementCommandService commands = new(definitions, rules);
        StrategicCityState city = state.Cities[StrategicManagementIds.LocationPlainsCity];

        StrategicCommandResult policy = InvokeSetAutoConscriptionIntensity(
            commands,
            state,
            StrategicManagementIds.LocationPlainsCity,
            "conscription_low");
        AssertTrue(policy.Success, $"low auto conscription should be selectable, got {policy.FailureReason}");

        city.ReserveForces = 0;
        state.SetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceMoney, 0);
        state.SetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceFood, 0);
        StrategicCommandResult noResources = InvokeSettleElapsedWorldTime(
            commands,
            state,
            StrategicManagementIds.FactionPlayer,
            1);
        AssertTrue(noResources.Success, $"world time should still settle when auto conscription skips resources, got {noResources.FailureReason}");
        AssertEqual(0, city.ReserveForces, "resource shortage should skip reserve gain");
        AssertEqual(0, state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceMoney), "resource shortage should not mutate money");
        AssertEqual(0, state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceFood), "resource shortage should not mutate food");

        state.SetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceMoney, 500);
        state.SetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceFood, 300);
        city.ReserveForces = city.CityForceCapacity - rules.GetActiveForces(state, city.LocationId) - 1;
        int beforeReserve = city.ReserveForces;
        int beforeMoney = state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceMoney);
        int beforeFood = state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceFood);

        StrategicCommandResult noCapacity = InvokeSettleElapsedWorldTime(
            commands,
            state,
            StrategicManagementIds.FactionPlayer,
            1);

        AssertTrue(noCapacity.Success, $"world time should still settle when auto conscription skips capacity, got {noCapacity.FailureReason}");
        AssertEqual(beforeReserve, city.ReserveForces, "insufficient full-batch capacity should skip reserve gain");
        AssertEqual(beforeMoney, state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceMoney), "capacity skip should not spend money");
        AssertEqual(beforeFood, state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceFood), "capacity skip should not spend food");
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
        int beforeWood = state.GetResourceAmount(
            StrategicManagementIds.FactionPlayer,
            StrategicManagementIds.ResourceWood);

        StrategicCommandResult result = InvokeSettleElapsedWorldTime(
            commands,
            state,
            StrategicManagementIds.FactionPlayer,
            1);

        AssertTrue(result.Success, $"settling time without controlled production should still succeed, got {result.FailureReason}");
        AssertEqual(1, GetElapsedWorldTimePulses(state), "world-map time should advance even when no controlled production is available");
        AssertEqual(
            beforeWood,
            state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceWood),
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
        int beforeWood = state.GetResourceAmount(
            StrategicManagementIds.FactionPlayer,
            StrategicManagementIds.ResourceWood);

        StrategicCommandResult result = InvokeSettleElapsedWorldTime(
            commands,
            state,
            StrategicManagementIds.FactionPlayer,
            0);

        AssertTrue(!result.Success, "settling zero elapsed pulses should fail");
        AssertEqual("invalid_elapsed_world_time_pulses", result.FailureReason, "invalid elapsed pulse count should be explicit");
        AssertEqual(beforePulses, GetElapsedWorldTimePulses(state), "failed settlement must not mutate strategic time");
        AssertEqual(
            beforeWood,
            state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceWood),
            "failed settlement must not mutate resources");
    }

    internal static void StrategicManagementRuntimeBlocksElapsedTimeWhileCityManagementPaused()
    {
        StrategicManagementRuntime.Reset();
        InvokeRuntimePauseWorldTimeForCityManagement();
        int beforePulses = GetElapsedWorldTimePulses(StrategicManagementRuntime.State);
        int beforeWood = StrategicManagementRuntime.State.GetResourceAmount(
            StrategicManagementIds.FactionPlayer,
            StrategicManagementIds.ResourceWood);

        StrategicCommandResult result = InvokeRuntimeSettleElapsedWorldTime(1);

        AssertTrue(!result.Success, "runtime elapsed-time settlement should fail while city management pauses world time");
        AssertEqual("world_time_paused", result.FailureReason, "paused settlement should report the pause boundary");
        AssertEqual(beforePulses, GetElapsedWorldTimePulses(StrategicManagementRuntime.State), "paused settlement must not mutate retained strategic time");
        AssertEqual(
            beforeWood,
            StrategicManagementRuntime.State.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceWood),
            "paused settlement must not mutate retained resource production");
    }

    internal static void StrategicManagementRuntimeSettlesElapsedTimeAfterWorldMapResumes()
    {
        StrategicManagementRuntime.Reset();
        InvokeRuntimePauseWorldTimeForCityManagement();
        InvokeRuntimeResumeWorldMapTime();
        int beforePulses = GetElapsedWorldTimePulses(StrategicManagementRuntime.State);
        int beforeWood = StrategicManagementRuntime.State.GetResourceAmount(
            StrategicManagementIds.FactionPlayer,
            StrategicManagementIds.ResourceWood);

        StrategicCommandResult result = InvokeRuntimeSettleElapsedWorldTime(1);

        AssertTrue(result.Success, $"runtime elapsed-time settlement should succeed after world map resumes, got {result.FailureReason}");
        AssertEqual(beforePulses + 1, GetElapsedWorldTimePulses(StrategicManagementRuntime.State), "runtime helper should mutate retained world-map time");
        AssertEqual(
            beforeWood + 12,
            StrategicManagementRuntime.State.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceWood),
            "runtime helper should settle retained resource-site production while world time runs");
    }

    internal static void StrategicManagementRuntimeBuildsDashboardFromRetainedCommandState()
    {
        StrategicManagementRuntime.Reset();
        StrategicConstructionRegionDefinition military = FindRegion(
            StrategicManagementRuntime.Definitions,
            StrategicManagementIds.RegionPlainsMilitary);

        StrategicCommandResult build = StrategicManagementRuntime.Commands.BuildCityBuilding(
            StrategicManagementRuntime.State,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.BuildingTrainingGround,
            StrategicManagementIds.RegionPlainsMilitary,
            military.OriginX,
            military.OriginY);
        AssertTrue(build.Success, $"runtime command should build training ground, got {build.FailureReason}");

        StrategicManagementDashboardViewModel dashboard = StrategicManagementRuntime.BuildDashboard(
            StrategicManagementIds.FactionPlayer,
            StrategicManagementIds.LocationPlainsCity);

        AssertEqual(1, dashboard.SelectedCity.Buildings.Count, "runtime dashboard should reflect command-mutated state");
        AssertEqual(StrategicManagementIds.BuildingTrainingGround, dashboard.SelectedCity.Buildings[0].BuildingDefinitionId, "runtime dashboard should show built training ground");
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
