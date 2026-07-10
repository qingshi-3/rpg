using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;

namespace Rpg.Application.StrategicManagement;

public sealed partial class StrategicManagementCommandService
{
    public StrategicCommandResult BuildCityBuilding(
        StrategicManagementState state,
        string cityId,
        string buildingDefinitionId,
        string constructionRegionId,
        int gridX,
        int gridY)
    {
        string failureReason = _rules.GetBuildingPlacementFailureReason(
            state,
            cityId,
            buildingDefinitionId,
            constructionRegionId,
            gridX,
            gridY);
        if (!string.IsNullOrWhiteSpace(failureReason))
        {
            return Reject("BuildCityBuilding", cityId, failureReason);
        }

        StrategicCityState city = state.Cities[cityId];
        StrategicLocationState location = state.Locations[city.LocationId];
        StrategicBuildingDefinition building = _definitions.Buildings[buildingDefinitionId];
        state.Spend(location.OwnerFactionId, building.BuildCost);
        string instanceId = state.AllocateBuildingInstanceId();
        city.Buildings.Add(new StrategicBuildingInstanceState
        {
            BuildingInstanceId = instanceId,
            BuildingDefinitionId = building.BuildingDefinitionId,
            ConstructionRegionId = constructionRegionId ?? "",
            GridX = gridX,
            GridY = gridY,
            Level = 1,
            IsConstructed = true
        });

        StrategicCommandResult result = StrategicCommandResult.Ok(city.LocationId, instanceId);
        result.CreatedEntityId = instanceId;
        result.Events.Add(Event(
            "StrategicCityBuildingPlaced",
            city.LocationId,
            ("building", building.BuildingDefinitionId),
            ("region", constructionRegionId ?? ""),
            ("grid", $"{gridX},{gridY}")));
        Accept("BuildCityBuilding", city.LocationId, result);
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
        city.ReserveForces = System.Math.Max(0, city.ReserveForces - System.Math.Max(0, definition.SoldierCapacityCost));

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
        result.Events.Add(Event(
            "StrategicCorpsCreated",
            corpsInstanceId,
            ("city", city.LocationId),
            ("corps", definition.CorpsDefinitionId),
            ("reserveSpent", definition.SoldierCapacityCost.ToString())));
        Accept("CreateCorps", corpsInstanceId, result);
        return result;
    }

    public StrategicCommandResult RecruitCorpsForHero(
        StrategicManagementState state,
        string cityId,
        string heroId,
        string corpsDefinitionId)
    {
        string failureReason = GetRecruitCorpsForHeroFailureReason(state, cityId, heroId, corpsDefinitionId);
        if (!string.IsNullOrWhiteSpace(failureReason))
        {
            return Reject("RecruitCorpsForHero", heroId, failureReason);
        }

        StrategicCityState city = state.Cities[cityId ?? ""];
        StrategicLocationState location = state.Locations[city.LocationId];
        StrategicHeroState hero = state.Heroes[heroId ?? ""];
        StrategicCorpsDefinition definition = _definitions.Corps[corpsDefinitionId ?? ""];
        string previousCorpsInstanceId = hero.AssignedCorpsInstanceId ?? "";
        StrategicCorpsInstanceState previousCorps = null;
        if (!string.IsNullOrWhiteSpace(previousCorpsInstanceId))
        {
            state.CorpsInstances.TryGetValue(previousCorpsInstanceId, out previousCorps);
        }

        int reserveConsumed = System.Math.Max(0, definition.SoldierCapacityCost);
        int reserveRefunded = previousCorps == null
            ? 0
            : _rules.GetCorpsCurrentReserveValue(state, previousCorps.CorpsInstanceId);
        System.Collections.Generic.IReadOnlyList<StrategicResourceAmount> resourcesConsumed = NormalizeResourceAmounts(definition.CreationCost);
        System.Collections.Generic.IReadOnlyList<StrategicResourceAmount> resourcesRefunded = previousCorps == null
            ? System.Array.Empty<StrategicResourceAmount>()
            : _rules.GetCorpsCurrentResourceValue(state, previousCorps.CorpsInstanceId);
        System.Collections.Generic.IReadOnlyList<StrategicResourceAmount> netResources =
            CombineResourceAmounts(resourcesRefunded, resourcesConsumed, -1);

        // Replacement settlement dissolves the old main corps into city reserves
        // at current strength before paying the new corps cost. Battle losses stay lost.
        city.ReserveForces = System.Math.Max(0, city.ReserveForces + reserveRefunded - reserveConsumed);
        foreach (StrategicResourceAmount refund in resourcesRefunded)
        {
            state.AddResourceAmount(location.OwnerFactionId, refund.ResourceId, refund.Amount);
        }

        state.Spend(location.OwnerFactionId, resourcesConsumed);
        if (previousCorps != null)
        {
            state.CorpsInstances.Remove(previousCorps.CorpsInstanceId);
        }

        string corpsInstanceId = state.AllocateCorpsInstanceId();
        StrategicCorpsInstanceState recruitedCorps = new()
        {
            CorpsInstanceId = corpsInstanceId,
            CorpsDefinitionId = definition.CorpsDefinitionId,
            HomeCityId = city.LocationId,
            FactionId = location.OwnerFactionId,
            Strength = 100,
            Level = 1,
            EquipmentLevel = 0,
            Experience = 0,
            Status = StrategicCorpsInstanceStatus.AssignedToHero,
            AssignedHeroId = hero.HeroId
        };
        state.CorpsInstances[corpsInstanceId] = recruitedCorps;
        hero.AssignedCorpsInstanceId = recruitedCorps.CorpsInstanceId;
        StrategicHeroCorpsAptitudeGrade aptitude = _rules.EvaluateHeroCorpsAptitude(state, hero.HeroId, definition.CorpsDefinitionId);

        StrategicCommandResult result = StrategicCommandResult.Ok(city.LocationId, hero.HeroId, corpsInstanceId, previousCorpsInstanceId);
        result.CreatedEntityId = corpsInstanceId;
        result.AptitudeGrade = aptitude;
        result.Events.Add(Event(
            "StrategicHeroMainCorpsReplaced",
            corpsInstanceId,
            ("city", city.LocationId),
            ("hero", hero.HeroId),
            ("corps", definition.CorpsDefinitionId),
            ("previousCorps", previousCorpsInstanceId),
            ("reserveConsumed", reserveConsumed.ToString()),
            ("reserveRefunded", reserveRefunded.ToString()),
            ("netReserveDelta", (reserveRefunded - reserveConsumed).ToString()),
            ("resourcesConsumed", FormatResourceAmounts(resourcesConsumed)),
            ("resourcesRefunded", FormatResourceAmounts(resourcesRefunded)),
            ("netResources", FormatResourceAmounts(netResources)),
            ("aptitude", aptitude.ToString())));
        Accept("RecruitCorpsForHero", corpsInstanceId, result);
        return result;
    }

    public StrategicCommandResult ReplenishCorps(
        StrategicManagementState state,
        string cityId,
        string corpsInstanceId,
        int targetStrength)
    {
        string failureReason = _rules.GetCorpsReplenishmentFailureReason(state, cityId, corpsInstanceId, targetStrength);
        if (!string.IsNullOrWhiteSpace(failureReason))
        {
            return Reject("ReplenishCorps", corpsInstanceId, failureReason);
        }

        StrategicCityState city = state.Cities[cityId];
        StrategicLocationState location = state.Locations[city.LocationId];
        StrategicCorpsInstanceState corps = state.CorpsInstances[corpsInstanceId];
        int boundedTarget = System.Math.Clamp(targetStrength, 0, 100);
        int reserveCost = _rules.GetCorpsReplenishmentReserveCost(state, corps.CorpsInstanceId, boundedTarget);
        System.Collections.Generic.IReadOnlyList<StrategicResourceAmount> resourceCost =
            _rules.GetCorpsReplenishmentResourceCost(state, corps.CorpsInstanceId, boundedTarget);

        state.Spend(location.OwnerFactionId, resourceCost);
        city.ReserveForces = System.Math.Max(0, city.ReserveForces - reserveCost);
        corps.Strength = boundedTarget;
        if (corps.Status is StrategicCorpsInstanceStatus.Routed or StrategicCorpsInstanceStatus.Scattered or StrategicCorpsInstanceStatus.Rebuilding)
        {
            corps.Status = string.IsNullOrWhiteSpace(corps.AssignedHeroId)
                ? StrategicCorpsInstanceStatus.Garrisoned
                : StrategicCorpsInstanceStatus.AssignedToHero;
        }

        StrategicCommandResult result = StrategicCommandResult.Ok(city.LocationId, corps.CorpsInstanceId);
        result.Events.Add(Event(
            "StrategicCorpsReplenished",
            corps.CorpsInstanceId,
            ("city", city.LocationId),
            ("targetStrength", boundedTarget.ToString()),
            ("reserveSpent", reserveCost.ToString()),
            ("resources", FormatResourceAmounts(resourceCost))));
        Accept("ReplenishCorps", corps.CorpsInstanceId, result);
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

    private string GetRecruitCorpsForHeroFailureReason(
        StrategicManagementState state,
        string cityId,
        string heroId,
        string corpsDefinitionId)
    {
        if (state == null || !state.Cities.TryGetValue(cityId ?? "", out StrategicCityState city))
        {
            return StrategicFailureReasons.MissingCity;
        }

        if (!state.Locations.TryGetValue(city.LocationId, out StrategicLocationState cityLocation))
        {
            return StrategicFailureReasons.MissingLocation;
        }

        if (!state.Heroes.TryGetValue(heroId ?? "", out StrategicHeroState hero))
        {
            return StrategicFailureReasons.MissingHero;
        }

        if (!string.Equals(hero.FactionId, cityLocation.OwnerFactionId, System.StringComparison.Ordinal))
        {
            return StrategicFailureReasons.FactionMismatch;
        }

        if (!string.IsNullOrWhiteSpace(hero.CurrentExpeditionId))
        {
            return StrategicFailureReasons.HeroAlreadyOnExpedition;
        }

        if (!string.IsNullOrWhiteSpace(hero.AssignedCorpsInstanceId))
        {
            if (!state.CorpsInstances.TryGetValue(hero.AssignedCorpsInstanceId, out StrategicCorpsInstanceState currentCorps))
            {
                return StrategicFailureReasons.MissingCorpsInstance;
            }

            if (!string.IsNullOrWhiteSpace(currentCorps.CurrentExpeditionId) ||
                currentCorps.Status == StrategicCorpsInstanceStatus.Expedition)
            {
                return StrategicFailureReasons.CorpsAlreadyOnExpedition;
            }
        }

        return _rules.GetHeroCorpsReplacementFailureReason(state, city.LocationId, hero.HeroId, corpsDefinitionId);
    }
}
