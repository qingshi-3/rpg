using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.StrategicBattleBridge;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.StrategicManagement;
public sealed partial class StrategicManagementCommandService
{
    public StrategicCommandResult BuildFacility(
        StrategicManagementState state,
        string cityId,
        string facilityDefinitionId)
    {
        string failureReason = _rules.GetFacilityBuildFailureReason(state, cityId, facilityDefinitionId);
        if (!string.IsNullOrWhiteSpace(failureReason))
        {
            return Reject("BuildFacility", cityId, failureReason);
        }

        StrategicCityState city = state.Cities[cityId];
        StrategicLocationState location = state.Locations[city.LocationId];
        StrategicFacilityDefinition facility = _definitions.Facilities[facilityDefinitionId];
        state.Spend(location.OwnerFactionId, facility.BuildCost);
        string instanceId = $"{city.LocationId}:facility:{city.Facilities.Count + 1:00}:{facility.FacilityDefinitionId}";
        city.Facilities.Add(new StrategicFacilityInstanceState
        {
            FacilityInstanceId = instanceId,
            FacilityDefinitionId = facility.FacilityDefinitionId
        });

        StrategicCommandResult result = StrategicCommandResult.Ok(city.LocationId, instanceId);
        result.CreatedEntityId = instanceId;
        result.Events.Add(Event("StrategicFacilityBuilt", city.LocationId, ("facility", facility.FacilityDefinitionId)));
        Accept("BuildFacility", city.LocationId, result);
        return result;
    }

    public StrategicCommandResult CreateCorps(
        StrategicManagementState state,
        string cityId,
        string corpsDefinitionId)
    {
        string failureReason = _rules.GetCorpsCreationFailureReason(state, cityId, corpsDefinitionId);
        if (!string.IsNullOrWhiteSpace(failureReason))
        {
            return Reject("CreateCorps", cityId, failureReason);
        }

        StrategicCityState city = state.Cities[cityId];
        StrategicLocationState location = state.Locations[city.LocationId];
        StrategicCorpsDefinition definition = _definitions.Corps[corpsDefinitionId];
        state.Spend(location.OwnerFactionId, definition.CreationCost);
        string corpsInstanceId = state.AllocateCorpsInstanceId();
        state.CorpsInstances[corpsInstanceId] = new StrategicCorpsInstanceState
        {
            CorpsInstanceId = corpsInstanceId,
            CorpsDefinitionId = definition.CorpsDefinitionId,
            HomeCityId = city.LocationId,
            FactionId = location.OwnerFactionId,
            Strength = 100,
            Level = 1,
            EquipmentLevel = 0,
            Experience = 0,
            Status = StrategicCorpsInstanceStatus.Garrisoned
        };

        StrategicCommandResult result = StrategicCommandResult.Ok(city.LocationId, corpsInstanceId);
        result.CreatedEntityId = corpsInstanceId;
        result.Events.Add(Event("StrategicCorpsCreated", corpsInstanceId, ("city", city.LocationId), ("corps", definition.CorpsDefinitionId)));
        Accept("CreateCorps", corpsInstanceId, result);
        return result;
    }

    public StrategicCommandResult AssignCorpsToHero(
        StrategicManagementState state,
        string heroId,
        string corpsInstanceId)
    {
        string failureReason = GetAssignmentFailureReason(state, heroId, corpsInstanceId);
        if (!string.IsNullOrWhiteSpace(failureReason))
        {
            return Reject("AssignCorpsToHero", heroId, failureReason);
        }

        StrategicHeroState hero = state.Heroes[heroId];
        StrategicCorpsInstanceState corps = state.CorpsInstances[corpsInstanceId];
        hero.AssignedCorpsInstanceId = corps.CorpsInstanceId;
        corps.AssignedHeroId = hero.HeroId;
        corps.Status = StrategicCorpsInstanceStatus.AssignedToHero;
        StrategicHeroCorpsAptitudeGrade aptitude = _rules.EvaluateHeroCorpsAptitude(state, hero.HeroId, corps.CorpsDefinitionId);

        StrategicCommandResult result = StrategicCommandResult.Ok(hero.HeroId, corps.CorpsInstanceId);
        result.AptitudeGrade = aptitude;
        result.Events.Add(Event("StrategicCorpsAssignedToHero", hero.HeroId, ("corps", corps.CorpsInstanceId), ("aptitude", aptitude.ToString())));
        Accept("AssignCorpsToHero", hero.HeroId, result);
        return result;
    }

    public StrategicCommandResult UnassignCorpsFromHero(
        StrategicManagementState state,
        string heroId)
    {
        if (state == null || !state.Heroes.TryGetValue(heroId ?? "", out StrategicHeroState hero))
        {
            return Reject("UnassignCorpsFromHero", heroId, StrategicFailureReasons.MissingHero);
        }

        if (string.IsNullOrWhiteSpace(hero.AssignedCorpsInstanceId))
        {
            return StrategicCommandResult.Ok(hero.HeroId);
        }

        string corpsInstanceId = hero.AssignedCorpsInstanceId;
        hero.AssignedCorpsInstanceId = "";
        if (state.CorpsInstances.TryGetValue(corpsInstanceId, out StrategicCorpsInstanceState corps))
        {
            corps.AssignedHeroId = "";
            corps.Status = StrategicCorpsInstanceStatus.Garrisoned;
        }

        StrategicCommandResult result = StrategicCommandResult.Ok(hero.HeroId, corpsInstanceId);
        result.Events.Add(Event("StrategicCorpsUnassignedFromHero", hero.HeroId, ("corps", corpsInstanceId)));
        Accept("UnassignCorpsFromHero", hero.HeroId, result);
        return result;
    }

    private static string GetAssignmentFailureReason(
        StrategicManagementState state,
        string heroId,
        string corpsInstanceId)
    {
        if (state == null || !state.Heroes.TryGetValue(heroId ?? "", out StrategicHeroState hero))
        {
            return StrategicFailureReasons.MissingHero;
        }

        if (!state.CorpsInstances.TryGetValue(corpsInstanceId ?? "", out StrategicCorpsInstanceState corps))
        {
            return StrategicFailureReasons.MissingCorpsInstance;
        }

        if (!string.Equals(hero.FactionId, corps.FactionId, System.StringComparison.Ordinal))
        {
            return StrategicFailureReasons.FactionMismatch;
        }

        if (!string.IsNullOrWhiteSpace(hero.AssignedCorpsInstanceId))
        {
            return StrategicFailureReasons.HeroAlreadyAssigned;
        }

        return string.IsNullOrWhiteSpace(corps.AssignedHeroId)
            ? ""
            : StrategicFailureReasons.CorpsAlreadyAssigned;
    }
}
