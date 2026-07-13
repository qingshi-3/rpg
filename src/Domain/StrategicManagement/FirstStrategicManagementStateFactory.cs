using System.Linq;
using Rpg.Definitions.StrategicManagement;

namespace Rpg.Domain.StrategicManagement;

public static class FirstStrategicManagementStateFactory
{
    public static StrategicManagementState CreatePlayerStart(StrategicManagementDefinitionSet definitions)
    {
        StrategicManagementState state = new()
        {
            ElapsedWorldTimePulses = 0
        };

        foreach (StrategicScenarioResourceStart resource in definitions.Scenario.Resources)
        {
            state.SetResourceAmount(resource.FactionId, resource.ResourceId, resource.Amount);
        }

        foreach (StrategicManagementCityReference city in definitions.CanonicalGeography.Cities.Values
                     .OrderBy(city => city.LocationId, System.StringComparer.Ordinal))
        {
            StrategicScenarioProvinceStart start = definitions.Scenario.Provinces
                .Single(province => province.ProvinceId == city.ProvinceId);
            AddLocation(state, city.LocationId, start.OwnerFactionId, ToControlState(start.Control));
        }

        foreach (StrategicScenarioLocationStart location in definitions.Scenario.Locations)
        {
            AddLocation(state, location.LocationId, location.OwnerFactionId, ToControlState(location.Control));
        }

        foreach (StrategicLocationDefinition cityDefinition in definitions.Locations.Values
            .Where(location =>
                location.Kind == StrategicLocationKind.City &&
                !string.IsNullOrWhiteSpace(location.CityIdentityId)))
        {
            // Every implemented managed location owns isolated city state from the start;
            // ownership/control decide whether the player may open its management UI.
            state.Cities[cityDefinition.LocationId] = new StrategicCityState
            {
                LocationId = cityDefinition.LocationId,
                CityIdentityId = cityDefinition.CityIdentityId,
                CityForceCapacity = definitions.Scenario.DefaultCityForceCapacity,
                ReserveForces = definitions.Scenario.DefaultCityReserveForces,
                ConstructionRegionIds = cityDefinition.ConstructionRegions
                    .Select(region => region.RegionId)
                    .ToList()
            };
        }

        state.Heroes[StrategicManagementIds.HeroOrdinaryCommander] = new StrategicHeroState
        {
            HeroId = StrategicManagementIds.HeroOrdinaryCommander,
            HeroDefinitionId = StrategicManagementIds.HeroOrdinaryCommander,
            FactionId = StrategicManagementIds.FactionPlayer
        };
        state.Heroes[StrategicManagementIds.HeroArcherCaptain] = new StrategicHeroState
        {
            HeroId = StrategicManagementIds.HeroArcherCaptain,
            HeroDefinitionId = StrategicManagementIds.HeroArcherCaptain,
            FactionId = StrategicManagementIds.FactionPlayer
        };
        state.Heroes[StrategicManagementIds.HeroCavalryCaptain] = new StrategicHeroState
        {
            HeroId = StrategicManagementIds.HeroCavalryCaptain,
            HeroDefinitionId = StrategicManagementIds.HeroCavalryCaptain,
            FactionId = StrategicManagementIds.FactionPlayer
        };

        SeedAssignedCorps(state, StrategicManagementIds.HeroOrdinaryCommander, StrategicManagementIds.CorpsShieldLine);
        SeedAssignedCorps(state, StrategicManagementIds.HeroArcherCaptain, StrategicManagementIds.CorpsArcherLine);
        SeedAssignedCorps(state, StrategicManagementIds.HeroCavalryCaptain, StrategicManagementIds.CorpsCavalryLine);

        return state;
    }

    private static StrategicLocationControlState ToControlState(StrategicScenarioControl control) => control switch
    {
        StrategicScenarioControl.PlayerHeld => StrategicLocationControlState.PlayerHeld,
        StrategicScenarioControl.EnemyHeld => StrategicLocationControlState.EnemyHeld,
        _ => StrategicLocationControlState.Neutral
    };

    private static void SeedAssignedCorps(
        StrategicManagementState state,
        string heroId,
        string corpsDefinitionId)
    {
        string corpsInstanceId = state.AllocateCorpsInstanceId();
        state.CorpsInstances[corpsInstanceId] = new StrategicCorpsInstanceState
        {
            CorpsInstanceId = corpsInstanceId,
            CorpsDefinitionId = corpsDefinitionId,
            HomeCityId = StrategicManagementIds.LocationQingheCore,
            FactionId = StrategicManagementIds.FactionPlayer,
            Strength = 100,
            Level = 1,
            EquipmentLevel = 0,
            Experience = 0,
            Status = StrategicCorpsInstanceStatus.AssignedToHero,
            AssignedHeroId = heroId
        };

        state.Heroes[heroId].AssignedCorpsInstanceId = corpsInstanceId;
    }

    private static void AddLocation(
        StrategicManagementState state,
        string locationId,
        string ownerFactionId,
        StrategicLocationControlState controlState)
    {
        state.Locations[locationId] = new StrategicLocationState
        {
            LocationId = locationId,
            OwnerFactionId = ownerFactionId,
            ControlState = controlState
        };
    }
}
