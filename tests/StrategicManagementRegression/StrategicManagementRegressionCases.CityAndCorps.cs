using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Application.StrategicBattleBridge;
using Rpg.Application.StrategicManagement;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;
internal static partial class StrategicManagementRegressionCases
{
    internal static void CommonCityIdentityDerivesCommonMusterTemplates()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementRules rules = new(definitions);

        IReadOnlyList<StrategicMusterTemplateAvailability> templates =
            rules.GetMusterTemplates(state, StrategicManagementIds.LocationPlainsCity);

        AssertAvailable(templates, StrategicManagementIds.CorpsShieldLine);
        AssertAvailable(templates, StrategicManagementIds.CorpsArcherLine);
        AssertAvailable(templates, StrategicManagementIds.CorpsCavalryLine);
    }

    internal static void BeastMusterRequiresControlledBeastSourceAndBeastPen()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementRules rules = new(definitions);
        StrategicManagementCommandService commands = new(definitions, rules);

        StrategicMusterTemplateAvailability initial =
            FindTemplate(rules.GetMusterTemplates(state, StrategicManagementIds.LocationPlainsCity), StrategicManagementIds.CorpsWolfPack);
        AssertTrue(!initial.IsAvailable, "wolf pack should start unavailable");
        AssertContains(initial.FailureReasons, StrategicFailureReasons.MissingSourcePermission, "wolf pack should require beast source permission");
        AssertContains(initial.FailureReasons, StrategicFailureReasons.MissingFacility, "wolf pack should require beast pen");

        StrategicCommandResult occupy = commands.OccupyLocation(
            state,
            StrategicManagementIds.LocationBeastDen,
            StrategicManagementIds.FactionPlayer);
        AssertTrue(occupy.Success, "occupying beast den should succeed");

        StrategicCommandResult build = commands.BuildFacility(
            state,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.FacilityBeastPen);
        AssertTrue(build.Success, $"building beast pen should succeed, got {build.FailureReason}");

        StrategicMusterTemplateAvailability afterUnlock =
            FindTemplate(rules.GetMusterTemplates(state, StrategicManagementIds.LocationPlainsCity), StrategicManagementIds.CorpsWolfPack);
        AssertTrue(afterUnlock.IsAvailable, $"wolf pack should become available, got {string.Join(",", afterUnlock.FailureReasons)}");
    }

    internal static void LosingBeastSourceKeepsExistingCorpsButBlocksNewBeastCreation()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementRules rules = new(definitions);
        StrategicManagementCommandService commands = new(definitions, rules);

        commands.OccupyLocation(state, StrategicManagementIds.LocationBeastDen, StrategicManagementIds.FactionPlayer);
        commands.BuildFacility(state, StrategicManagementIds.LocationPlainsCity, StrategicManagementIds.FacilityBeastPen);
        StrategicCommandResult unassign = commands.UnassignCorpsFromHero(state, StrategicManagementIds.HeroBeastTamer);
        AssertTrue(unassign.Success, $"test setup unassignment should succeed, got {unassign.FailureReason}");
        StrategicCommandResult create = commands.CreateCorps(
            state,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.CorpsWolfPack);
        AssertTrue(create.Success, $"creating wolf pack should succeed, got {create.FailureReason}");
        string existingCorpsId = create.CreatedEntityId;

        StrategicCommandResult lose = commands.LoseLocation(
            state,
            StrategicManagementIds.LocationBeastDen,
            StrategicManagementIds.FactionEnemy);
        AssertTrue(lose.Success, "losing beast source should succeed");

        AssertTrue(state.CorpsInstances.ContainsKey(existingCorpsId), "existing beast corps should remain after source loss");
        StrategicCommandResult secondCreate = commands.CreateCorps(
            state,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.CorpsWolfPack);
        AssertTrue(!secondCreate.Success, "new wolf pack creation should be blocked after source loss");
        AssertEqual(StrategicFailureReasons.MissingSourcePermission, secondCreate.FailureReason, "source loss should be the rejection reason");
    }

    internal static void BuildFacilityConsumesResourcesAndFacilitySlot()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
        int beforeMaterials = state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceBuildingMaterials);

        StrategicCommandResult result = commands.BuildFacility(
            state,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.FacilityTrainingGround);

        AssertTrue(result.Success, $"training ground build should succeed, got {result.FailureReason}");
        StrategicCityState city = state.Cities[StrategicManagementIds.LocationPlainsCity];
        AssertEqual(1, city.Facilities.Count, "city should consume one facility slot");
        AssertEqual(
            beforeMaterials - 40,
            state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceBuildingMaterials),
            "building materials should be spent");
    }

    internal static void BuildFacilityFailureLeavesResourcesAndSlotsUnchanged()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        state.SetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceBuildingMaterials, 0);
        StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
        int beforeFacilities = state.Cities[StrategicManagementIds.LocationPlainsCity].Facilities.Count;
        int beforeMaterials = state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceBuildingMaterials);

        StrategicCommandResult result = commands.BuildFacility(
            state,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.FacilityTrainingGround);

        AssertTrue(!result.Success, "facility build should fail without resources");
        AssertEqual(StrategicFailureReasons.InsufficientResources, result.FailureReason, "failure reason should be resources");
        AssertEqual(beforeFacilities, state.Cities[StrategicManagementIds.LocationPlainsCity].Facilities.Count, "facility list should not change");
        AssertEqual(beforeMaterials, state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceBuildingMaterials), "resources should not change");
    }

    internal static void CreateCorpsConsumesResourcesAndCreatesPersistentCorpsInstance()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
        int beforeMoney = state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceMoney);

        StrategicCommandResult result = commands.CreateCorps(
            state,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.CorpsShieldLine);

        AssertTrue(result.Success, $"shield corps creation should succeed, got {result.FailureReason}");
        AssertTrue(state.CorpsInstances.ContainsKey(result.CreatedEntityId), "created corps instance should be durable state");
        StrategicCorpsInstanceState corps = state.CorpsInstances[result.CreatedEntityId];
        AssertEqual(100, corps.Strength, "new corps should start at full strength");
        AssertEqual(StrategicCorpsInstanceStatus.Garrisoned, corps.Status, "new corps should start in city garrison");
        AssertEqual(beforeMoney - 30, state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceMoney), "money should be spent");
    }

    internal static void CreateCorpsFailureLeavesResourcesAndCorpsListUnchanged()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
        int beforeMoney = state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceMoney);
        int beforeCorps = state.CorpsInstances.Count;

        StrategicCommandResult result = commands.CreateCorps(
            state,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.CorpsGreatBeast);

        AssertTrue(!result.Success, "great beast should be unavailable without beast chain");
        AssertEqual(StrategicFailureReasons.MissingSourcePermission, result.FailureReason, "missing source should be reported first");
        AssertEqual(beforeCorps, state.CorpsInstances.Count, "corps list should not change");
        AssertEqual(beforeMoney, state.GetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceMoney), "resources should not change");
    }

    internal static void AssignCorpsToHeroRecordsAptitudeWithoutRandomFailure()
    {
        StrategicManagementDefinitionSet definitions = FirstStrategicManagementDefinitions.Create();
        StrategicManagementState state = FirstStrategicManagementStateFactory.CreatePlayerStart(definitions);
        StrategicManagementCommandService commands = new(definitions, new StrategicManagementRules(definitions));
        commands.OccupyLocation(state, StrategicManagementIds.LocationBeastDen, StrategicManagementIds.FactionPlayer);
        commands.BuildFacility(state, StrategicManagementIds.LocationPlainsCity, StrategicManagementIds.FacilityBeastPen);
        StrategicCommandResult unassign = commands.UnassignCorpsFromHero(state, StrategicManagementIds.HeroBeastTamer);
        AssertTrue(unassign.Success, $"test setup unassignment should succeed, got {unassign.FailureReason}");
        StrategicCommandResult create = commands.CreateCorps(
            state,
            StrategicManagementIds.LocationPlainsCity,
            StrategicManagementIds.CorpsWolfPack);

        StrategicCommandResult assign = commands.AssignCorpsToHero(
            state,
            StrategicManagementIds.HeroBeastTamer,
            create.CreatedEntityId);

        AssertTrue(assign.Success, $"assignment should succeed, got {assign.FailureReason}");
        AssertEqual(create.CreatedEntityId, state.Heroes[StrategicManagementIds.HeroBeastTamer].AssignedCorpsInstanceId, "hero should reference assigned corps");
        AssertEqual(StrategicManagementIds.HeroBeastTamer, state.CorpsInstances[create.CreatedEntityId].AssignedHeroId, "corps should reference assigned hero");
        AssertEqual(StrategicHeroCorpsAptitudeGrade.A, assign.AptitudeGrade, "beast tamer should record beast aptitude");
        AssertTrue(!assign.Events.Any(item => item.Kind == "RandomBeastControlFailure"), "assignment must not create random beast failure");
    }
}
