using System;
using System.Linq;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.World;

public sealed class StrategicWorldService
{
    private readonly WorldSiteDeploymentService _deploymentService = new();

    public StrategicWorldState CreateInitialState(StrategicWorldDefinition definition, int seed = 0)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        StrategicWorldState state = new()
        {
            RunId = Guid.NewGuid().ToString("N"),
            DefinitionId = definition.Id,
            Seed = seed,
            WorldTick = 0,
            PlayerFactionId = definition.PlayerFactionId
        };

        foreach (ResourceAmountDefinition resource in definition.InitialResources)
        {
            state.PlayerResources.Set(resource.ResourceId, resource.Amount);
        }

        foreach (WorldSiteDefinition siteDefinition in definition.SiteDefinitions)
        {
            WorldSiteState siteState = new()
            {
                SiteId = siteDefinition.Id,
                OwnerFactionId = siteDefinition.InitialOwnerFactionId,
                ControlState = siteDefinition.InitialControlState,
                SiteMode = WorldSiteMode.Peacetime,
                ActiveTags = siteDefinition.Tags.ToList()
            };

            foreach (FacilitySlotDefinition slot in siteDefinition.FacilitySlots)
            {
                if (string.IsNullOrWhiteSpace(slot.InitialFacilityId))
                {
                    continue;
                }

                siteState.Facilities.Add(new FacilityInstance
                {
                    InstanceId = BuildFacilityInstanceId(siteDefinition.Id, slot.SlotId, slot.InitialFacilityId),
                    FacilityId = slot.InitialFacilityId,
                    SiteId = siteDefinition.Id,
                    SlotId = slot.SlotId,
                    State = FacilityState.Active,
                    Level = 1
                });
            }

            foreach (GarrisonDefinition garrison in siteDefinition.InitialGarrison)
            {
                siteState.Garrison.Add(new GarrisonState
                {
                    UnitTypeId = garrison.UnitTypeId,
                    Count = garrison.Count,
                    Morale = garrison.Morale
                });
            }

            _deploymentService.EnsureGarrisonPlacements(siteState, siteDefinition);
            state.SiteStates[siteDefinition.Id] = siteState;
        }

        GameLog.Info(nameof(StrategicWorldService), $"StrategicWorldInitialized definition={definition.Id} run={state.RunId}");
        return state;
    }

    public static string BuildFacilityInstanceId(string siteId, string slotId, string facilityId)
    {
        return $"{siteId}:{slotId}:{facilityId}";
    }
}
