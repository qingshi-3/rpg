using System.Collections.Generic;
using System.Linq;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;

namespace Rpg.Application.StrategicManagement;

public sealed class StrategicManagementRules
{
    private readonly StrategicManagementDefinitionSet _definitions;
    public const int FirstSliceMaxActivePlayerExpeditions = 3;
    public const int FirstSliceMaxHeroCompaniesPerExpedition = 3;

    public StrategicManagementRules(StrategicManagementDefinitionSet definitions)
    {
        _definitions = definitions ?? new StrategicManagementDefinitionSet();
    }

    public IReadOnlyList<StrategicMusterTemplateAvailability> GetMusterTemplates(
        StrategicManagementState state,
        string cityId)
    {
        List<StrategicMusterTemplateAvailability> results = new();
        foreach (StrategicCorpsDefinition corps in _definitions.Corps.Values.OrderBy(item => item.CorpsDefinitionId))
        {
            results.Add(EvaluateMusterTemplate(state, cityId, corps.CorpsDefinitionId));
        }

        return results;
    }

    public StrategicMusterTemplateAvailability EvaluateMusterTemplate(
        StrategicManagementState state,
        string cityId,
        string corpsDefinitionId)
    {
        StrategicMusterTemplateAvailability result = new()
        {
            CorpsDefinitionId = corpsDefinitionId ?? ""
        };

        if (state == null ||
            !_definitions.Corps.TryGetValue(corpsDefinitionId ?? "", out StrategicCorpsDefinition corps) ||
            !TryGetCityContext(state, cityId, out StrategicCityState city, out StrategicLocationState cityLocation))
        {
            AddUnique(result.FailureReasons, StrategicFailureReasons.MissingDefinitions);
            return result;
        }

        if (!CityIdentityAllows(city, corps))
        {
            AddUnique(result.FailureReasons, StrategicFailureReasons.MissingCityIdentity);
        }

        foreach (string requiredFacilityTag in corps.RequiredFacilityTags)
        {
            if (!CityHasFacilityTag(city, requiredFacilityTag))
            {
                AddUnique(result.FailureReasons, StrategicFailureReasons.MissingFacility);
            }
        }

        foreach (string sourceTag in corps.RequiredSourcePermissionTags)
        {
            if (!FactionControlsSourceTag(state, cityLocation.OwnerFactionId, sourceTag))
            {
                AddUnique(result.FailureReasons, StrategicFailureReasons.MissingSourcePermission);
            }
        }

        if (!state.CanSpend(cityLocation.OwnerFactionId, corps.CreationCost))
        {
            AddUnique(result.FailureReasons, StrategicFailureReasons.InsufficientResources);
        }

        return result;
    }

    public string GetCorpsCreationFailureReason(
        StrategicManagementState state,
        string cityId,
        string corpsDefinitionId)
    {
        StrategicMusterTemplateAvailability availability = EvaluateMusterTemplate(state, cityId, corpsDefinitionId);
        return availability.IsAvailable ? "" : SelectPrimaryFailure(availability.FailureReasons);
    }

    public string GetFacilityBuildFailureReason(
        StrategicManagementState state,
        string cityId,
        string facilityDefinitionId)
    {
        if (state == null ||
            !_definitions.Facilities.TryGetValue(facilityDefinitionId ?? "", out StrategicFacilityDefinition facility))
        {
            return StrategicFailureReasons.MissingFacility;
        }

        if (!TryGetCityContext(state, cityId, out StrategicCityState city, out StrategicLocationState cityLocation))
        {
            return StrategicFailureReasons.MissingCity;
        }

        if (city.Facilities.Count + System.Math.Max(1, facility.SlotCost) > city.FacilitySlotCount)
        {
            return StrategicFailureReasons.FacilitySlotsFull;
        }

        return state.CanSpend(cityLocation.OwnerFactionId, facility.BuildCost)
            ? ""
            : StrategicFailureReasons.InsufficientResources;
    }

    public string GetExpeditionCreationFailureReason(
        StrategicManagementState state,
        string sourceLocationId,
        string targetLocationId,
        StrategicExpeditionIntent intent,
        string heroId)
    {
        return GetExpeditionCreationFailureReason(
            state,
            sourceLocationId,
            targetLocationId,
            intent,
            string.IsNullOrWhiteSpace(heroId) ? System.Array.Empty<string>() : new[] { heroId });
    }

    public string GetExpeditionCreationFailureReason(
        StrategicManagementState state,
        string sourceLocationId,
        string targetLocationId,
        StrategicExpeditionIntent intent,
        System.Collections.Generic.IReadOnlyCollection<string> heroIds)
    {
        if (state == null)
        {
            return StrategicFailureReasons.MissingDefinitions;
        }

        string[] selectedHeroIds = NormalizeHeroIds(heroIds);
        if (selectedHeroIds.Length != CountProvidedHeroIds(heroIds))
        {
            return StrategicFailureReasons.InvalidExpeditionParticipants;
        }

        if (selectedHeroIds.Length == 0 ||
            selectedHeroIds.Length > FirstSliceMaxHeroCompaniesPerExpedition)
        {
            return StrategicFailureReasons.InvalidExpeditionParticipants;
        }

        if (!state.Heroes.TryGetValue(selectedHeroIds[0] ?? "", out StrategicHeroState leadHero))
        {
            return StrategicFailureReasons.MissingHero;
        }

        if (!state.Locations.TryGetValue(sourceLocationId ?? "", out StrategicLocationState sourceLocation))
        {
            return StrategicFailureReasons.MissingLocation;
        }

        if (!string.Equals(sourceLocation.OwnerFactionId, leadHero.FactionId, System.StringComparison.Ordinal) ||
            sourceLocation.ControlState != StrategicLocationControlState.PlayerHeld)
        {
            return StrategicFailureReasons.SourceLocationNotOwned;
        }

        foreach (string selectedHeroId in selectedHeroIds)
        {
            string participantFailure = GetExpeditionParticipantFailureReason(
                state,
                sourceLocation,
                leadHero.FactionId,
                selectedHeroId);
            if (!string.IsNullOrWhiteSpace(participantFailure))
            {
                return participantFailure;
            }
        }

        if (CountActiveExpeditions(state, leadHero.FactionId) >= FirstSliceMaxActivePlayerExpeditions)
        {
            return StrategicFailureReasons.ExpeditionCapacityFull;
        }

        return intent switch
        {
            StrategicExpeditionIntent.MoveToPosition => "",
            StrategicExpeditionIntent.ReinforceLocation => GetTargetReinforcementFailureReason(state, sourceLocation, targetLocationId, leadHero.FactionId),
            StrategicExpeditionIntent.AssaultLocation => GetTargetAssaultFailureReason(state, sourceLocation, targetLocationId, leadHero.FactionId),
            _ => StrategicFailureReasons.UnsupportedExpeditionIntent
        };
    }

    public string GetExpeditionRetargetFailureReason(
        StrategicManagementState state,
        string expeditionId,
        string targetLocationId,
        StrategicExpeditionIntent intent)
    {
        if (state == null)
        {
            return StrategicFailureReasons.MissingDefinitions;
        }

        if (!state.Expeditions.TryGetValue(expeditionId ?? "", out StrategicExpeditionState expedition))
        {
            return StrategicFailureReasons.MissingExpedition;
        }

        if (expedition.Status != StrategicExpeditionStatus.Moving)
        {
            return StrategicFailureReasons.ExpeditionNotCommandable;
        }

        if (intent == StrategicExpeditionIntent.MoveToPosition)
        {
            return "";
        }

        if (!state.Locations.TryGetValue(expedition.SourceLocationId ?? "", out StrategicLocationState sourceLocation))
        {
            return StrategicFailureReasons.MissingLocation;
        }

        return intent switch
        {
            StrategicExpeditionIntent.ReinforceLocation => GetTargetReinforcementFailureReason(state, sourceLocation, targetLocationId, expedition.FactionId),
            StrategicExpeditionIntent.AssaultLocation => GetTargetAssaultFailureReason(state, sourceLocation, targetLocationId, expedition.FactionId),
            _ => StrategicFailureReasons.UnsupportedExpeditionIntent
        };
    }

    public IReadOnlyList<StrategicResourceAmount> GetLocationProduction(
        StrategicManagementState state,
        string locationId,
        string factionId,
        int elapsedPulses)
    {
        string failureReason = GetLocationProductionFailureReason(state, locationId, factionId, elapsedPulses);
        if (!string.IsNullOrWhiteSpace(failureReason) ||
            !_definitions.Locations.TryGetValue(locationId ?? "", out StrategicLocationDefinition definition))
        {
            return System.Array.Empty<StrategicResourceAmount>();
        }

        return definition.ProductionPerWorldTimePulse
            .Where(item => item.Amount > 0 && !string.IsNullOrWhiteSpace(item.ResourceId))
            .OrderBy(item => item.ResourceId)
            .Select(item => new StrategicResourceAmount(item.ResourceId, item.Amount * elapsedPulses))
            .ToList();
    }

    public string GetLocationProductionFailureReason(
        StrategicManagementState state,
        string locationId,
        string factionId,
        int elapsedPulses)
    {
        if (elapsedPulses <= 0)
        {
            return StrategicFailureReasons.InvalidElapsedWorldTimePulses;
        }

        if (state == null ||
            !_definitions.Locations.TryGetValue(locationId ?? "", out StrategicLocationDefinition definition) ||
            !state.Locations.TryGetValue(locationId ?? "", out StrategicLocationState location))
        {
            return StrategicFailureReasons.MissingLocation;
        }

        // First-slice production is player-controlled settlement, not an automated
        // economy tick for every faction on the map.
        if (!string.Equals(location.OwnerFactionId, factionId ?? "", System.StringComparison.Ordinal) ||
            location.ControlState != StrategicLocationControlState.PlayerHeld)
        {
            return StrategicFailureReasons.FactionMismatch;
        }

        return definition.ProductionPerWorldTimePulse.Any(item => item.Amount > 0 && !string.IsNullOrWhiteSpace(item.ResourceId))
            ? ""
            : StrategicFailureReasons.NoProduction;
    }

    public StrategicHeroCorpsAptitudeGrade EvaluateHeroCorpsAptitude(
        StrategicManagementState state,
        string heroId,
        string corpsDefinitionId)
    {
        if (state == null ||
            !state.Heroes.TryGetValue(heroId ?? "", out StrategicHeroState hero) ||
            !_definitions.Heroes.TryGetValue(hero.HeroDefinitionId, out StrategicHeroDefinition heroDefinition) ||
            !_definitions.Corps.TryGetValue(corpsDefinitionId ?? "", out StrategicCorpsDefinition corps))
        {
            return StrategicHeroCorpsAptitudeGrade.D;
        }

        if (!string.IsNullOrWhiteSpace(corps.AptitudeTag) &&
            heroDefinition.AptitudeTags.Contains(corps.AptitudeTag))
        {
            return StrategicHeroCorpsAptitudeGrade.A;
        }

        return string.IsNullOrWhiteSpace(corps.AptitudeTag)
            ? StrategicHeroCorpsAptitudeGrade.B
            : StrategicHeroCorpsAptitudeGrade.C;
    }

    public static int CountActiveExpeditions(StrategicManagementState state, string factionId)
    {
        if (state?.Expeditions == null)
        {
            return 0;
        }

        return state.Expeditions.Values.Count(expedition =>
            expedition != null &&
            string.Equals(expedition.FactionId, factionId ?? "", System.StringComparison.Ordinal) &&
            expedition.Status is not (StrategicExpeditionStatus.Resolved or StrategicExpeditionStatus.Cancelled));
    }

    private static string GetTargetReinforcementFailureReason(
        StrategicManagementState state,
        StrategicLocationState sourceLocation,
        string targetLocationId,
        string factionId)
    {
        if (string.IsNullOrWhiteSpace(targetLocationId) ||
            !state.Locations.TryGetValue(targetLocationId, out StrategicLocationState targetLocation))
        {
            return StrategicFailureReasons.MissingLocation;
        }

        if (string.Equals(targetLocation.LocationId, sourceLocation.LocationId, System.StringComparison.Ordinal))
        {
            return StrategicFailureReasons.SameLocationTarget;
        }

        return string.Equals(targetLocation.OwnerFactionId, factionId ?? "", System.StringComparison.Ordinal)
            ? ""
            : StrategicFailureReasons.TargetLocationNotOwned;
    }

    private static string GetExpeditionParticipantFailureReason(
        StrategicManagementState state,
        StrategicLocationState sourceLocation,
        string factionId,
        string heroId)
    {
        if (!state.Heroes.TryGetValue(heroId ?? "", out StrategicHeroState hero))
        {
            return StrategicFailureReasons.MissingHero;
        }

        if (!string.Equals(hero.FactionId, factionId, System.StringComparison.Ordinal))
        {
            return StrategicFailureReasons.FactionMismatch;
        }

        if (!string.IsNullOrWhiteSpace(hero.CurrentExpeditionId))
        {
            return StrategicFailureReasons.HeroAlreadyOnExpedition;
        }

        if (string.IsNullOrWhiteSpace(hero.AssignedCorpsInstanceId))
        {
            return StrategicFailureReasons.HeroHasNoAssignedCorps;
        }

        if (!state.CorpsInstances.TryGetValue(hero.AssignedCorpsInstanceId, out StrategicCorpsInstanceState corps))
        {
            return StrategicFailureReasons.MissingCorpsInstance;
        }

        if (!string.Equals(corps.FactionId, factionId, System.StringComparison.Ordinal))
        {
            return StrategicFailureReasons.FactionMismatch;
        }

        if (!string.Equals(corps.AssignedHeroId, hero.HeroId, System.StringComparison.Ordinal) ||
            corps.Status != StrategicCorpsInstanceStatus.AssignedToHero)
        {
            return StrategicFailureReasons.CorpsNotAssignedToHero;
        }

        if (!string.Equals(corps.HomeCityId, sourceLocation.LocationId, System.StringComparison.Ordinal))
        {
            return StrategicFailureReasons.MissingCity;
        }

        return string.IsNullOrWhiteSpace(corps.CurrentExpeditionId)
            ? ""
            : StrategicFailureReasons.CorpsAlreadyOnExpedition;
    }

    private static string[] NormalizeHeroIds(System.Collections.Generic.IReadOnlyCollection<string> heroIds)
    {
        return (heroIds ?? System.Array.Empty<string>())
            .Where(heroId => !string.IsNullOrWhiteSpace(heroId))
            .Distinct(System.StringComparer.Ordinal)
            .ToArray();
    }

    private static int CountProvidedHeroIds(System.Collections.Generic.IReadOnlyCollection<string> heroIds)
    {
        return (heroIds ?? System.Array.Empty<string>())
            .Count(heroId => !string.IsNullOrWhiteSpace(heroId));
    }

    private string GetTargetAssaultFailureReason(
        StrategicManagementState state,
        StrategicLocationState sourceLocation,
        string targetLocationId,
        string factionId)
    {
        if (string.IsNullOrWhiteSpace(targetLocationId) ||
            !state.Locations.TryGetValue(targetLocationId, out StrategicLocationState targetLocation))
        {
            return StrategicFailureReasons.MissingLocation;
        }

        if (string.Equals(targetLocation.LocationId, sourceLocation.LocationId, System.StringComparison.Ordinal))
        {
            return StrategicFailureReasons.SameLocationTarget;
        }

        if (string.Equals(targetLocation.OwnerFactionId, factionId ?? "", System.StringComparison.Ordinal))
        {
            return StrategicFailureReasons.TargetLocationNotAttackable;
        }

        // Strategic travel only validates whether the target can be attacked.
        // Battle entry is confirmed later by the world-map trigger dialog.
        return "";
    }

    private bool TryGetCityContext(
        StrategicManagementState state,
        string cityId,
        out StrategicCityState city,
        out StrategicLocationState cityLocation)
    {
        city = null;
        cityLocation = null;
        if (state == null ||
            !state.Cities.TryGetValue(cityId ?? "", out city) ||
            !state.Locations.TryGetValue(city.LocationId, out cityLocation))
        {
            return false;
        }

        return true;
    }

    private bool CityIdentityAllows(StrategicCityState city, StrategicCorpsDefinition corps)
    {
        if (corps.RequiredCityIdentityIds.Count > 0)
        {
            return corps.RequiredCityIdentityIds.Contains(city.CityIdentityId);
        }

        if (_definitions.CityIdentities.TryGetValue(city.CityIdentityId, out StrategicCityIdentityDefinition identity) &&
            identity.NaturalCorpsDefinitionIds.Contains(corps.CorpsDefinitionId))
        {
            return true;
        }

        return corps.RequiredFacilityTags.Count > 0 || corps.RequiredSourcePermissionTags.Count > 0;
    }

    private bool CityHasFacilityTag(StrategicCityState city, string requiredFacilityTag)
    {
        if (city == null || string.IsNullOrWhiteSpace(requiredFacilityTag))
        {
            return true;
        }

        return city.Facilities.Any(instance =>
            _definitions.Facilities.TryGetValue(instance.FacilityDefinitionId, out StrategicFacilityDefinition facility) &&
            facility.ProvidedTags.Contains(requiredFacilityTag));
    }

    private bool FactionControlsSourceTag(StrategicManagementState state, string factionId, string sourceTag)
    {
        if (state == null || string.IsNullOrWhiteSpace(sourceTag))
        {
            return true;
        }

        foreach (StrategicLocationState location in state.Locations.Values)
        {
            if (!string.Equals(location.OwnerFactionId, factionId, System.StringComparison.Ordinal) ||
                location.ControlState != StrategicLocationControlState.PlayerHeld &&
                !string.Equals(factionId, StrategicManagementIds.FactionEnemy, System.StringComparison.Ordinal))
            {
                continue;
            }

            if (_definitions.Locations.TryGetValue(location.LocationId, out StrategicLocationDefinition definition) &&
                definition.SourcePermissionTags.Contains(sourceTag))
            {
                return true;
            }
        }

        return false;
    }

    private static string SelectPrimaryFailure(IReadOnlyList<string> reasons)
    {
        string[] priority =
        {
            StrategicFailureReasons.MissingDefinitions,
            StrategicFailureReasons.MissingCity,
            StrategicFailureReasons.MissingHero,
            StrategicFailureReasons.HeroHasNoAssignedCorps,
            StrategicFailureReasons.CorpsNotAssignedToHero,
            StrategicFailureReasons.HeroAlreadyOnExpedition,
            StrategicFailureReasons.CorpsAlreadyOnExpedition,
            StrategicFailureReasons.MissingSourcePermission,
            StrategicFailureReasons.MissingFacility,
            StrategicFailureReasons.MissingCityIdentity,
            StrategicFailureReasons.InsufficientResources
        };

        foreach (string item in priority)
        {
            if (reasons.Contains(item))
            {
                return item;
            }
        }

        return reasons.Count == 0 ? "" : reasons[0];
    }

    private static void AddUnique(List<string> reasons, string reason)
    {
        if (!string.IsNullOrWhiteSpace(reason) && !reasons.Contains(reason))
        {
            reasons.Add(reason);
        }
    }
}
