using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.StrategicBattleBridge;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.StrategicManagement;
public sealed partial class StrategicManagementCommandService
{
    public StrategicCommandResult AddResource(
        StrategicManagementState state,
        string factionId,
        string resourceId,
        int amount)
    {
        if (state == null || string.IsNullOrWhiteSpace(factionId) || string.IsNullOrWhiteSpace(resourceId))
        {
            return Reject("AddResource", factionId, StrategicFailureReasons.MissingDefinitions);
        }

        state.AddResourceAmount(factionId, resourceId, amount);
        StrategicCommandResult result = StrategicCommandResult.Ok($"{factionId}:{resourceId}");
        result.Events.Add(Event("StrategicResourceChanged", factionId, ("resource", resourceId), ("amount", amount.ToString())));
        Accept("AddResource", factionId, result);
        return result;
    }

    public StrategicCommandResult OccupyLocation(
        StrategicManagementState state,
        string locationId,
        string factionId)
    {
        if (state == null || !state.Locations.TryGetValue(locationId ?? "", out StrategicLocationState location))
        {
            return Reject("OccupyLocation", locationId, StrategicFailureReasons.MissingLocation);
        }

        location.OwnerFactionId = factionId ?? "";
        location.ControlState = string.Equals(factionId, StrategicManagementIds.FactionPlayer, System.StringComparison.Ordinal)
            ? StrategicLocationControlState.PlayerHeld
            : StrategicLocationControlState.EnemyHeld;
        StrategicCommandResult result = StrategicCommandResult.Ok(location.LocationId);
        result.Events.Add(Event("StrategicLocationOccupied", location.LocationId, ("owner", location.OwnerFactionId)));
        Accept("OccupyLocation", location.LocationId, result);
        return result;
    }

    public StrategicCommandResult LoseLocation(
        StrategicManagementState state,
        string locationId,
        string newOwnerFactionId)
    {
        return OccupyLocation(state, locationId, newOwnerFactionId);
    }

    public StrategicCommandResult SettleLocationProduction(
        StrategicManagementState state,
        string locationId,
        string factionId,
        int elapsedPulses)
    {
        string failureReason = _rules.GetLocationProductionFailureReason(state, locationId, factionId, elapsedPulses);
        if (!string.IsNullOrWhiteSpace(failureReason))
        {
            return Reject("SettleLocationProduction", locationId, failureReason);
        }

        StrategicLocationState location = state.Locations[locationId];
        System.Collections.Generic.IReadOnlyList<StrategicResourceAmount> production =
            _rules.GetLocationProduction(state, location.LocationId, factionId, elapsedPulses);
        foreach (StrategicResourceAmount amount in production)
        {
            state.AddResourceAmount(factionId, amount.ResourceId, amount.Amount);
        }

        StrategicCommandResult result = StrategicCommandResult.Ok(location.LocationId);
        foreach (StrategicResourceAmount amount in production)
        {
            result.ChangedFactIds.Add($"{factionId}:{amount.ResourceId}");
        }

        result.Events.Add(Event(
            "StrategicLocationProductionSettled",
            location.LocationId,
            ("faction", factionId ?? ""),
            ("elapsedPulses", elapsedPulses.ToString()),
            ("resources", FormatResourceAmounts(production))));
        Accept("SettleLocationProduction", location.LocationId, result);
        return result;
    }

    public StrategicCommandResult SettleElapsedWorldTime(
        StrategicManagementState state,
        string factionId,
        int elapsedPulses)
    {
        if (state == null || string.IsNullOrWhiteSpace(factionId))
        {
            return Reject("SettleElapsedWorldTime", factionId, StrategicFailureReasons.MissingDefinitions);
        }

        if (elapsedPulses <= 0)
        {
            return Reject("SettleElapsedWorldTime", factionId, StrategicFailureReasons.InvalidElapsedWorldTimePulses);
        }

        int previousPulses = state.ElapsedWorldTimePulses;
        StrategicCommandResult result = StrategicCommandResult.Ok("world_time");
        result.Events.Add(Event(
            "StrategicWorldTimeSettled",
            factionId,
            ("faction", factionId),
            ("previousPulses", previousPulses.ToString()),
            ("newPulses", (previousPulses + elapsedPulses).ToString()),
            ("elapsedPulses", elapsedPulses.ToString())));

        // Elapsed world-map time is command-driven: Godot process callbacks,
        // management UI refreshes, and legacy world ticks must not mutate this state.
        foreach (StrategicCityState city in GetControlledCities(state, factionId))
        {
            System.Collections.Generic.IReadOnlyList<StrategicResourceAmount> cityProduction =
                _rules.GetCityBuildingProduction(state, city.LocationId, factionId, elapsedPulses);
            foreach (StrategicResourceAmount amount in cityProduction)
            {
                state.AddResourceAmount(factionId, amount.ResourceId, amount.Amount);
                AddUnique(result.ChangedFactIds, $"{factionId}:{amount.ResourceId}");
            }

            if (cityProduction.Count > 0)
            {
                result.Events.Add(Event(
                    "StrategicCityProductionSettled",
                    city.LocationId,
                    ("faction", factionId),
                    ("elapsedPulses", elapsedPulses.ToString()),
                    ("resources", FormatResourceAmounts(cityProduction))));
            }

            int reserveRecovery = _rules.GetRecoverableReserveForces(state, city.LocationId, elapsedPulses);
            if (reserveRecovery > 0)
            {
                city.ReserveForces += reserveRecovery;
                AddUnique(result.ChangedFactIds, $"{city.LocationId}:reserve");
                result.Events.Add(Event(
                    "StrategicCityReserveRecovered",
                    city.LocationId,
                    ("faction", factionId),
                    ("elapsedPulses", elapsedPulses.ToString()),
                    ("reserve", reserveRecovery.ToString())));
            }
        }

        foreach (StrategicLocationState location in GetControlledProducingLocations(state, factionId))
        {
            System.Collections.Generic.IReadOnlyList<StrategicResourceAmount> production =
                _rules.GetLocationProduction(state, location.LocationId, factionId, elapsedPulses);
            if (production.Count == 0)
            {
                continue;
            }

            foreach (StrategicResourceAmount amount in production)
            {
                state.AddResourceAmount(factionId, amount.ResourceId, amount.Amount);
                AddUnique(result.ChangedFactIds, $"{factionId}:{amount.ResourceId}");
            }

            result.Events.Add(Event(
                "StrategicLocationProductionSettled",
                location.LocationId,
                ("faction", factionId),
                ("elapsedPulses", elapsedPulses.ToString()),
                ("resources", FormatResourceAmounts(production))));
        }

        state.ElapsedWorldTimePulses = previousPulses + elapsedPulses;
        Accept("SettleElapsedWorldTime", factionId, result);
        return result;
    }

    private System.Collections.Generic.IReadOnlyList<StrategicLocationState> GetControlledProducingLocations(
        StrategicManagementState state,
        string factionId)
    {
        if (state == null || string.IsNullOrWhiteSpace(factionId))
        {
            return System.Array.Empty<StrategicLocationState>();
        }

        return state.Locations.Values
            .Where(location =>
                string.Equals(location.OwnerFactionId, factionId, System.StringComparison.Ordinal) &&
                location.ControlState == StrategicLocationControlState.PlayerHeld &&
                _definitions.Locations.TryGetValue(location.LocationId, out StrategicLocationDefinition definition) &&
                definition.ProductionPerWorldTimePulse.Any(item => item.Amount > 0 && !string.IsNullOrWhiteSpace(item.ResourceId)))
            .OrderBy(location => location.LocationId)
            .ToList();
    }

    private System.Collections.Generic.IReadOnlyList<StrategicCityState> GetControlledCities(
        StrategicManagementState state,
        string factionId)
    {
        if (state == null || string.IsNullOrWhiteSpace(factionId))
        {
            return System.Array.Empty<StrategicCityState>();
        }

        return state.Cities.Values
            .Where(city =>
                state.Locations.TryGetValue(city.LocationId, out StrategicLocationState location) &&
                string.Equals(location.OwnerFactionId, factionId, System.StringComparison.Ordinal) &&
                location.ControlState == StrategicLocationControlState.PlayerHeld)
            .OrderBy(city => city.LocationId)
            .ToList();
    }
}
