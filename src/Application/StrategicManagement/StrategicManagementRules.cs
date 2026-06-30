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

    public int GetManualConscriptionReserveGain()
    {
        return System.Math.Max(0, _definitions.Conscription?.Manual?.ReserveGain ?? 0);
    }

    public IReadOnlyList<StrategicResourceAmount> GetManualConscriptionCost()
    {
        return NormalizeCost(_definitions.Conscription?.Manual?.Cost);
    }

    public IReadOnlyList<StrategicConscriptionIntensityRule> GetAutoConscriptionIntensityRules()
    {
        return (_definitions.Conscription?.AutoIntensities ?? new List<StrategicConscriptionIntensityDefinition>())
            .Where(item => !string.IsNullOrWhiteSpace(item.IntensityId))
            .Select(ToConscriptionRule)
            .ToList();
    }

    private static StrategicConscriptionIntensityRule ToConscriptionRule(StrategicConscriptionIntensityDefinition definition)
    {
        return new(
            definition.IntensityId,
            definition.DisplayName,
            definition.ReserveGain,
            NormalizeCost(definition.Cost),
            definition.RequiresTrainingGround);
    }

    private static List<StrategicResourceAmount> NormalizeCost(IReadOnlyCollection<StrategicResourceAmount> cost)
    {
        return (cost ?? System.Array.Empty<StrategicResourceAmount>())
            .Where(item => item.Amount > 0 && !string.IsNullOrWhiteSpace(item.ResourceId))
            .Select(item => new StrategicResourceAmount(item.ResourceId, item.Amount))
            .OrderBy(item => item.ResourceId)
            .ToList();
    }

    public bool TryGetAutoConscriptionIntensityRule(
        string intensityId,
        out StrategicConscriptionIntensityRule rule)
    {
        rule = GetAutoConscriptionIntensityRules()
            .FirstOrDefault(item => string.Equals(item.IntensityId, intensityId ?? "", System.StringComparison.Ordinal));
        return rule != null;
    }

    public string GetManualConscriptionFailureReason(
        StrategicManagementState state,
        string cityId)
    {
        if (!TryGetCityContext(state, cityId, out StrategicCityState city, out StrategicLocationState cityLocation))
        {
            return StrategicFailureReasons.MissingCity;
        }

        if (GetRemainingCityForceCapacity(state, city.LocationId) < GetManualConscriptionReserveGain())
        {
            return StrategicFailureReasons.CityForceCapacityFull;
        }

        return state.CanSpend(cityLocation.OwnerFactionId, GetManualConscriptionCost())
            ? ""
            : StrategicFailureReasons.InsufficientResources;
    }

    public string GetAutoConscriptionIntensityFailureReason(
        StrategicManagementState state,
        string cityId,
        string intensityId)
    {
        if (!TryGetAutoConscriptionIntensityRule(intensityId, out StrategicConscriptionIntensityRule rule))
        {
            return StrategicFailureReasons.InvalidConscriptionIntensity;
        }

        if (!TryGetCityContext(state, cityId, out StrategicCityState city, out _))
        {
            return StrategicFailureReasons.MissingCity;
        }

        return rule.RequiresTrainingGround && !CityHasConstructedBuilding(city, StrategicManagementIds.BuildingTrainingGround)
            ? StrategicFailureReasons.MissingBuilding
            : "";
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

        foreach (string requiredCategoryId in corps.RequiredBuildingCategoryIds)
        {
            if (!CityHasBuildingCategory(city, requiredCategoryId))
            {
                AddUnique(result.FailureReasons, StrategicFailureReasons.MissingBuilding);
            }
        }

        if (city.ReserveForces < System.Math.Max(0, corps.SoldierCapacityCost))
        {
            AddUnique(result.FailureReasons, StrategicFailureReasons.InsufficientReserveForces);
        }

        if (GetRemainingCityForceCapacity(state, city.LocationId) < 0)
        {
            AddUnique(result.FailureReasons, StrategicFailureReasons.CityForceCapacityFull);
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

    public string GetBuildingPlacementFailureReason(
        StrategicManagementState state,
        string cityId,
        string buildingDefinitionId,
        string constructionRegionId,
        int gridX,
        int gridY)
    {
        if (state == null ||
            !_definitions.Buildings.TryGetValue(buildingDefinitionId ?? "", out StrategicBuildingDefinition building))
        {
            return StrategicFailureReasons.MissingBuilding;
        }

        if (!TryGetCityContext(state, cityId, out StrategicCityState city, out StrategicLocationState cityLocation) ||
            !_definitions.Locations.TryGetValue(city.LocationId, out StrategicLocationDefinition locationDefinition))
        {
            return StrategicFailureReasons.MissingCity;
        }

        StrategicConstructionRegionDefinition region = locationDefinition.ConstructionRegions.FirstOrDefault(item =>
            string.Equals(item.RegionId, constructionRegionId ?? "", System.StringComparison.Ordinal));
        if (region == null || !city.ConstructionRegionIds.Contains(region.RegionId))
        {
            return StrategicFailureReasons.MissingConstructionRegion;
        }

        IReadOnlyList<(int X, int Y)> candidateCells = BuildFootprintCells(gridX, gridY, building.FootprintWidth, building.FootprintHeight);
        if (candidateCells.Count == 0 || !candidateCells.All(cell => IsCellInsideRegion(cell.X, cell.Y, region)))
        {
            return StrategicFailureReasons.BuildingPlacementOutOfBounds;
        }

        if (city.Buildings.Any(existing => FootprintsOverlap(existing, candidateCells)))
        {
            return StrategicFailureReasons.BuildingPlacementOccupied;
        }

        return state.CanSpend(cityLocation.OwnerFactionId, building.BuildCost)
            ? ""
            : StrategicFailureReasons.InsufficientResources;
    }

    public int GetActiveForces(StrategicManagementState state, string cityId)
    {
        if (state == null || string.IsNullOrWhiteSpace(cityId))
        {
            return 0;
        }

        int activeForces = 0;
        foreach (StrategicCorpsInstanceState corps in state.CorpsInstances.Values)
        {
            if (!string.IsNullOrWhiteSpace(corps.CurrentExpeditionId) ||
                corps.Status == StrategicCorpsInstanceStatus.Expedition ||
                !string.Equals(corps.HomeCityId, cityId, System.StringComparison.Ordinal) ||
                !_definitions.Corps.TryGetValue(corps.CorpsDefinitionId, out StrategicCorpsDefinition definition))
            {
                continue;
            }

            int strength = System.Math.Clamp(corps.Strength, 0, 100);
            if (strength <= 0)
            {
                continue;
            }

            activeForces += (int)System.Math.Ceiling(definition.SoldierCapacityCost * (strength / 100.0));
        }

        return activeForces;
    }

    public int GetRemainingCityForceCapacity(StrategicManagementState state, string cityId)
    {
        if (state == null || !state.Cities.TryGetValue(cityId ?? "", out StrategicCityState city))
        {
            return 0;
        }

        return System.Math.Max(0, city.CityForceCapacity - GetActiveForces(state, cityId) - city.ReserveForces);
    }

    public int GetCorpsReplenishmentReserveCost(
        StrategicManagementState state,
        string corpsInstanceId,
        int targetStrength)
    {
        if (state == null ||
            !state.CorpsInstances.TryGetValue(corpsInstanceId ?? "", out StrategicCorpsInstanceState corps) ||
            !_definitions.Corps.TryGetValue(corps.CorpsDefinitionId, out StrategicCorpsDefinition definition))
        {
            return 0;
        }

        int boundedTarget = System.Math.Clamp(targetStrength, 0, 100);
        int missingStrength = System.Math.Max(0, boundedTarget - System.Math.Clamp(corps.Strength, 0, 100));
        return (int)System.Math.Ceiling(definition.SoldierCapacityCost * (missingStrength / 100.0));
    }

    public IReadOnlyList<StrategicResourceAmount> GetCorpsReplenishmentResourceCost(
        StrategicManagementState state,
        string corpsInstanceId,
        int targetStrength)
    {
        if (state == null ||
            !state.CorpsInstances.TryGetValue(corpsInstanceId ?? "", out StrategicCorpsInstanceState corps) ||
            !_definitions.Corps.TryGetValue(corps.CorpsDefinitionId, out StrategicCorpsDefinition definition))
        {
            return System.Array.Empty<StrategicResourceAmount>();
        }

        int boundedTarget = System.Math.Clamp(targetStrength, 0, 100);
        int missingStrength = System.Math.Max(0, boundedTarget - System.Math.Clamp(corps.Strength, 0, 100));
        if (missingStrength <= 0)
        {
            return System.Array.Empty<StrategicResourceAmount>();
        }

        return definition.ReplenishFullCost
            .Where(cost => cost.Amount > 0 && !string.IsNullOrWhiteSpace(cost.ResourceId))
            .Select(cost => new StrategicResourceAmount(
                cost.ResourceId,
                (int)System.Math.Ceiling(cost.Amount * (missingStrength / 100.0))))
            .Where(cost => cost.Amount > 0)
            .OrderBy(cost => cost.ResourceId)
            .ToList();
    }

    public string GetCorpsReplenishmentFailureReason(
        StrategicManagementState state,
        string cityId,
        string corpsInstanceId,
        int targetStrength)
    {
        if (state == null || !state.Cities.TryGetValue(cityId ?? "", out StrategicCityState city))
        {
            return StrategicFailureReasons.MissingCity;
        }

        if (!state.CorpsInstances.TryGetValue(corpsInstanceId ?? "", out StrategicCorpsInstanceState corps) ||
            !_definitions.Corps.ContainsKey(corps.CorpsDefinitionId))
        {
            return StrategicFailureReasons.MissingCorpsInstance;
        }

        if (!string.Equals(corps.HomeCityId, city.LocationId, System.StringComparison.Ordinal))
        {
            return StrategicFailureReasons.MissingCity;
        }

        int boundedTarget = System.Math.Clamp(targetStrength, 0, 100);
        if (boundedTarget <= System.Math.Clamp(corps.Strength, 0, 100))
        {
            return corps.Strength >= 100
                ? StrategicFailureReasons.CorpsAlreadyFullStrength
                : StrategicFailureReasons.InvalidReplenishmentTarget;
        }

        int reserveCost = GetCorpsReplenishmentReserveCost(state, corps.CorpsInstanceId, boundedTarget);
        if (city.ReserveForces < reserveCost)
        {
            return StrategicFailureReasons.InsufficientReserveForces;
        }

        if (!state.Locations.TryGetValue(city.LocationId, out StrategicLocationState cityLocation) ||
            !state.CanSpend(cityLocation.OwnerFactionId, GetCorpsReplenishmentResourceCost(state, corps.CorpsInstanceId, boundedTarget)))
        {
            return StrategicFailureReasons.InsufficientResources;
        }

        return "";
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
        IReadOnlyCollection<string> heroIds)
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
            StrategicExpeditionIntent.ReinforceLocation => GetTargetReinforcementFailureReason(state, sourceLocation, targetLocationId, expedition.FactionId, rejectSameAsSource: false),
            StrategicExpeditionIntent.AssaultLocation => GetTargetAssaultFailureReason(state, sourceLocation, targetLocationId, expedition.FactionId, rejectSameAsSource: false),
            _ => StrategicFailureReasons.UnsupportedExpeditionIntent
        };
    }

    public string GetExpeditionArrivalFailureReason(
        StrategicManagementState state,
        string expeditionId)
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

        if (expedition.Intent != StrategicExpeditionIntent.ReinforceLocation)
        {
            return StrategicFailureReasons.UnsupportedExpeditionIntent;
        }

        if (!state.Locations.TryGetValue(expedition.TargetLocationId ?? "", out StrategicLocationState targetLocation))
        {
            return StrategicFailureReasons.MissingLocation;
        }

        if (!string.Equals(targetLocation.OwnerFactionId, expedition.FactionId, System.StringComparison.Ordinal) ||
            targetLocation.ControlState != StrategicLocationControlState.PlayerHeld)
        {
            return StrategicFailureReasons.TargetLocationNotOwned;
        }

        return state.Cities.ContainsKey(expedition.TargetLocationId ?? "")
            ? ""
            : StrategicFailureReasons.MissingCity;
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

    public IReadOnlyList<StrategicResourceAmount> GetCityBuildingProduction(
        StrategicManagementState state,
        string cityId,
        string factionId,
        int elapsedPulses)
    {
        if (elapsedPulses <= 0 ||
            !TryGetCityContext(state, cityId, out StrategicCityState city, out StrategicLocationState cityLocation) ||
            !string.Equals(cityLocation.OwnerFactionId, factionId ?? "", System.StringComparison.Ordinal) ||
            cityLocation.ControlState != StrategicLocationControlState.PlayerHeld)
        {
            return System.Array.Empty<StrategicResourceAmount>();
        }

        Dictionary<string, int> totals = new(System.StringComparer.Ordinal);
        foreach (StrategicBuildingInstanceState instance in city.Buildings)
        {
            if (instance?.IsConstructed != true ||
                !_definitions.Buildings.TryGetValue(instance.BuildingDefinitionId ?? "", out StrategicBuildingDefinition building))
            {
                continue;
            }

            foreach (StrategicResourceAmount amount in building.ProvidedCapabilities?.ResourceProductionPerWorldTimePulse ??
                                                     Enumerable.Empty<StrategicResourceAmount>())
            {
                if (amount.Amount <= 0 || string.IsNullOrWhiteSpace(amount.ResourceId))
                {
                    continue;
                }

                totals.TryGetValue(amount.ResourceId, out int current);
                totals[amount.ResourceId] = current + amount.Amount * elapsedPulses;
            }
        }

        return totals
            .Where(item => item.Value > 0)
            .OrderBy(item => item.Key)
            .Select(item => new StrategicResourceAmount(item.Key, item.Value))
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
        string factionId,
        bool rejectSameAsSource = true)
    {
        if (string.IsNullOrWhiteSpace(targetLocationId) ||
            !state.Locations.TryGetValue(targetLocationId, out StrategicLocationState targetLocation))
        {
            return StrategicFailureReasons.MissingLocation;
        }

        // Creation rejects same-location targets. Retargeting does not: after dispatch,
        // SourceLocationId is only a departure record and the expedition may return
        // to that city through the normal owned-target reinforce path.
        if (rejectSameAsSource &&
            string.Equals(targetLocation.LocationId, sourceLocation.LocationId, System.StringComparison.Ordinal))
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

    private static string[] NormalizeHeroIds(IReadOnlyCollection<string> heroIds)
    {
        return (heroIds ?? System.Array.Empty<string>())
            .Where(heroId => !string.IsNullOrWhiteSpace(heroId))
            .Distinct(System.StringComparer.Ordinal)
            .ToArray();
    }

    private static int CountProvidedHeroIds(IReadOnlyCollection<string> heroIds)
    {
        return (heroIds ?? System.Array.Empty<string>())
            .Count(heroId => !string.IsNullOrWhiteSpace(heroId));
    }

    private string GetTargetAssaultFailureReason(
        StrategicManagementState state,
        StrategicLocationState sourceLocation,
        string targetLocationId,
        string factionId,
        bool rejectSameAsSource = true)
    {
        if (string.IsNullOrWhiteSpace(targetLocationId) ||
            !state.Locations.TryGetValue(targetLocationId, out StrategicLocationState targetLocation))
        {
            return StrategicFailureReasons.MissingLocation;
        }

        if (rejectSameAsSource &&
            string.Equals(targetLocation.LocationId, sourceLocation.LocationId, System.StringComparison.Ordinal))
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

        return corps.RequiredBuildingCategoryIds.Count > 0;
    }

    private bool CityHasBuildingCategory(StrategicCityState city, string requiredCategoryId)
    {
        if (city == null || string.IsNullOrWhiteSpace(requiredCategoryId))
        {
            return true;
        }

        return city.Buildings.Any(instance =>
            _definitions.Buildings.TryGetValue(instance.BuildingDefinitionId, out StrategicBuildingDefinition building) &&
            string.Equals(building.CategoryId, requiredCategoryId, System.StringComparison.Ordinal));
    }

    private bool CityHasConstructedBuilding(StrategicCityState city, string buildingDefinitionId)
    {
        if (city == null || string.IsNullOrWhiteSpace(buildingDefinitionId))
        {
            return false;
        }

        return city.Buildings.Any(instance =>
            instance?.IsConstructed == true &&
            string.Equals(instance.BuildingDefinitionId, buildingDefinitionId, System.StringComparison.Ordinal));
    }

    private bool FootprintsOverlap(
        StrategicBuildingInstanceState existing,
        IReadOnlyCollection<(int X, int Y)> candidateCells)
    {
        if (existing == null ||
            !_definitions.Buildings.TryGetValue(existing.BuildingDefinitionId, out StrategicBuildingDefinition definition))
        {
            return false;
        }

        HashSet<(int X, int Y)> existingCells = new(BuildFootprintCells(existing.GridX, existing.GridY, definition.FootprintWidth, definition.FootprintHeight));
        return candidateCells.Any(existingCells.Contains);
    }

    private static IReadOnlyList<(int X, int Y)> BuildFootprintCells(int gridX, int gridY, int width, int height)
    {
        List<(int X, int Y)> cells = new();
        if (width <= 0 || height <= 0)
        {
            return cells;
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                cells.Add((gridX + x, gridY + y));
            }
        }

        return cells;
    }

    private static bool IsCellInsideRegion(int x, int y, StrategicConstructionRegionDefinition region)
    {
        return region != null &&
               x >= region.OriginX &&
               y >= region.OriginY &&
               x < region.OriginX + region.Width &&
               y < region.OriginY + region.Height;
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
            StrategicFailureReasons.MissingBuilding,
            StrategicFailureReasons.MissingCityIdentity,
            StrategicFailureReasons.InsufficientReserveForces,
            StrategicFailureReasons.CityForceCapacityFull,
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

public sealed class StrategicConscriptionIntensityRule
{
    public StrategicConscriptionIntensityRule(
        string intensityId,
        string displayName,
        int reserveGain,
        IReadOnlyCollection<StrategicResourceAmount> cost,
        bool requiresTrainingGround)
    {
        IntensityId = intensityId ?? "";
        DisplayName = displayName ?? "";
        ReserveGain = System.Math.Max(0, reserveGain);
        Cost = (cost ?? System.Array.Empty<StrategicResourceAmount>())
            .Where(item => item.Amount > 0 && !string.IsNullOrWhiteSpace(item.ResourceId))
            .Select(item => new StrategicResourceAmount(item.ResourceId, item.Amount))
            .ToList();
        RequiresTrainingGround = requiresTrainingGround;
    }

    public string IntensityId { get; }
    public string DisplayName { get; }
    public int ReserveGain { get; }
    public List<StrategicResourceAmount> Cost { get; }
    public bool RequiresTrainingGround { get; }
}
