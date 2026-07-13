#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;

namespace Rpg.Application.StrategicManagement;

public sealed class StrategicManagementGeographyInvariantService
{
    public void ThrowIfInvalid(
        StrategicManagementDefinitionSet definitions,
        StrategicManagementState state,
        string context = "runtime")
    {
        if (definitions == null || state == null)
        {
            throw new InvalidDataException($"Strategic management identity graph is null context={context}");
        }
        if (definitions.CanonicalGeography.Cities.Count == 0 || definitions.CanonicalGeography.Provinces.Count == 0)
        {
            throw new InvalidDataException($"Strategic management canonical geography is missing context={context}");
        }
        if (state.Locations == null || state.Cities == null || state.CorpsInstances == null ||
            state.Expeditions == null || state.BattleFeedbackRecords == null)
        {
            throw new InvalidDataException($"Strategic management identity collections are incomplete context={context}");
        }

        ValidateKeyValueIdentity(state.Locations, location => location.LocationId, "location", context);
        ValidateKeyValueIdentity(state.Cities, city => city.LocationId, "city", context);

        foreach (StrategicManagementCityReference city in definitions.CanonicalGeography.Cities.Values)
        {
            if (!state.Locations.TryGetValue(city.LocationId, out StrategicLocationState? location))
            {
                throw Failure("missing-control", city.ProvinceId, city.LocationId, city.LayoutId, context);
            }
            if (!definitions.Locations.TryGetValue(city.LocationId, out StrategicLocationDefinition? definition) ||
                definition.Kind != StrategicLocationKind.City ||
                !string.Equals(location.LocationId, city.LocationId, StringComparison.Ordinal))
            {
                throw Failure("definition-control-mismatch", city.ProvinceId, city.LocationId, city.LayoutId, context);
            }
        }

        foreach ((string locationId, StrategicLocationState location) in state.Locations)
        {
            if (!definitions.Locations.TryGetValue(locationId, out StrategicLocationDefinition? definition))
            {
                throw Failure("extra-control", "<unknown>", locationId, "<unknown>", context);
            }
            if (definition.Kind == StrategicLocationKind.City &&
                !definitions.CanonicalGeography.Cities.TryGetValue(locationId, out _))
            {
                throw Failure("city-outside-canonical-geography", "<unknown>", location.LocationId, "<unknown>", context);
            }
        }

        foreach ((string cityId, StrategicCityState city) in state.Cities)
        {
            if (!definitions.CanonicalGeography.Cities.TryGetValue(cityId, out StrategicManagementCityReference? binding))
            {
                throw Failure("managed-city-outside-canonical-geography", "<unknown>", city.LocationId, "<unknown>", context);
            }
            if (!definitions.Locations.TryGetValue(cityId, out StrategicLocationDefinition? definition) ||
                string.IsNullOrWhiteSpace(definition.CityIdentityId) ||
                !string.Equals(city.CityIdentityId, definition.CityIdentityId, StringComparison.Ordinal))
            {
                throw Failure("managed-city-content-mismatch", binding.ProvinceId, cityId, binding.LayoutId, context);
            }
        }

        foreach (StrategicCorpsInstanceState corps in state.CorpsInstances.Values)
        {
            ValidateCityReference(corps.HomeCityId, "corps-home", corps.CorpsInstanceId, definitions, state, context);
        }
        foreach (StrategicExpeditionState expedition in state.Expeditions.Values)
        {
            ValidateLocationReference(expedition.SourceLocationId, "expedition-source", expedition.ExpeditionId, state, context);
            ValidateLocationReference(expedition.TargetLocationId, "expedition-target", expedition.ExpeditionId, state, context);
            foreach (StrategicExpeditionParticipantState participant in expedition.Participants ?? new())
            {
                ValidateCityReference(
                    participant?.RollbackStationLocationId,
                    "expedition-rollback-station",
                    expedition.ExpeditionId,
                    definitions,
                    state,
                    context);
            }
        }
        foreach (StrategicBattleFeedbackRecord feedback in state.BattleFeedbackRecords.Values)
        {
            ValidateLocationReference(feedback.TargetLocationId, "battle-feedback-target", feedback.FeedbackId, state, context);
        }
    }

    private static void ValidateKeyValueIdentity<T>(
        IReadOnlyDictionary<string, T> records,
        Func<T, string> readId,
        string kind,
        string context)
        where T : class
    {
        foreach ((string key, T value) in records)
        {
            string valueId = value == null ? "<null>" : readId(value) ?? "";
            if (value == null || !string.Equals(key, valueId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Strategic management {kind} dictionary identity mismatch key={key} value={valueId} context={context}");
            }
        }
    }

    private static void ValidateLocationReference(
        string? locationId,
        string field,
        string ownerId,
        StrategicManagementState state,
        string context)
    {
        if (string.IsNullOrWhiteSpace(locationId))
        {
            return;
        }
        if (!state.Locations.ContainsKey(locationId))
        {
            throw new InvalidDataException(
                $"Strategic management location reference is incomplete field={field} owner={ownerId} LocationId={locationId} context={context}");
        }
    }

    private static void ValidateCityReference(
        string? cityId,
        string field,
        string ownerId,
        StrategicManagementDefinitionSet definitions,
        StrategicManagementState state,
        string context)
    {
        if (string.IsNullOrWhiteSpace(cityId))
        {
            return;
        }
        definitions.CanonicalGeography.Cities.TryGetValue(cityId, out StrategicManagementCityReference? binding);
        if (!state.Cities.ContainsKey(cityId) ||
            binding == null)
        {
            throw new InvalidDataException(
                $"Strategic management city reference is incomplete field={field} owner={ownerId} ProvinceId={binding?.ProvinceId ?? "<unknown>"} LocationId={cityId} LayoutId={binding?.LayoutId ?? "<unknown>"} context={context}");
        }
    }

    private static InvalidDataException Failure(
        string reason,
        string provinceId,
        string locationId,
        string layoutId,
        string context) => new(
        $"Strategic management geography invariant failed reason={reason} ProvinceId={provinceId} LocationId={locationId} LayoutId={layoutId} context={context}");
}
