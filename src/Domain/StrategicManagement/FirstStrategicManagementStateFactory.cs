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

        state.SetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceMoney, 500);
        state.SetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceFood, 300);
        state.SetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceWood, 240);
        state.SetResourceAmount(StrategicManagementIds.FactionPlayer, StrategicManagementIds.ResourceOre, 120);

        AddLocation(state, StrategicManagementIds.LocationPlainsCity, StrategicManagementIds.FactionPlayer, StrategicLocationControlState.PlayerHeld);
        AddLocation(state, StrategicManagementIds.LocationTimberSite, StrategicManagementIds.FactionPlayer, StrategicLocationControlState.PlayerHeld);
        AddLocation(state, StrategicManagementIds.LocationBonefieldOutpost, StrategicManagementIds.FactionEnemy, StrategicLocationControlState.EnemyHeld);

        foreach (StrategicLocationDefinition cityDefinition in definitions.Locations.Values
            .Where(location => location.Kind == StrategicLocationKind.City))
        {
            // Every implemented managed location owns isolated city state from the start;
            // ownership/control decide whether the player may open its management UI.
            state.Cities[cityDefinition.LocationId] = new StrategicCityState
            {
                LocationId = cityDefinition.LocationId,
                CityIdentityId = cityDefinition.CityIdentityId,
                CityForceCapacity = 220,
                ReserveForces = 80,
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
            HomeCityId = StrategicManagementIds.LocationPlainsCity,
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
