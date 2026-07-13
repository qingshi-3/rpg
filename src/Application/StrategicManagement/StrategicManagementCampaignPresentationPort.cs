#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Rpg.Application.StrategicMap;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;

namespace Rpg.Application.StrategicManagement;

public sealed class StrategicManagementCampaignPresentationPort : IStrategicMapCampaignPresentationPort
{
    private readonly StrategicManagementDefinitionSet _definitions;
    private readonly StrategicManagementState _state;
    private readonly StrategicManagementGeographyInvariantService _invariants = new();

    public StrategicManagementCampaignPresentationPort(
        StrategicManagementDefinitionSet definitions,
        StrategicManagementState state)
    {
        _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public StrategicMapCampaignPresentationView Read()
    {
        _invariants.ThrowIfInvalid(_definitions, _state, "strategic-map-presentation-port");

        List<StrategicMapLocationControlView> locations = _definitions.CanonicalGeography.Cities.Values
            .OrderBy(city => city.ProvinceId, StringComparer.Ordinal)
            .ThenBy(city => city.LocationId, StringComparer.Ordinal)
            .Select(BuildLocationView)
            .ToList();
        List<StrategicMapProvinceControlView> provinces = _definitions.CanonicalGeography.Provinces.Values
            .OrderBy(province => province.ProvinceId, StringComparer.Ordinal)
            .Select(province => new StrategicMapProvinceControlView(
                province.ProvinceId,
                province.LayoutId,
                new ReadOnlyCollection<StrategicMapLocationControlView>(locations
                    .Where(location => string.Equals(location.ProvinceId, province.ProvinceId, StringComparison.Ordinal))
                    .ToList())))
            .ToList();
        return new StrategicMapCampaignPresentationView(provinces, locations);
    }

    private StrategicMapLocationControlView BuildLocationView(StrategicManagementCityReference city)
    {
        StrategicLocationState state = _state.Locations[city.LocationId];
        StrategicMapCampaignControl control = state.ControlState switch
        {
            StrategicLocationControlState.PlayerHeld
                when string.Equals(state.OwnerFactionId, StrategicManagementIds.FactionPlayer, StringComparison.Ordinal) =>
                StrategicMapCampaignControl.Player,
            StrategicLocationControlState.EnemyHeld
                when string.Equals(state.OwnerFactionId, StrategicManagementIds.FactionEnemy, StringComparison.Ordinal) =>
                StrategicMapCampaignControl.Enemy,
            StrategicLocationControlState.Neutral when string.IsNullOrWhiteSpace(state.OwnerFactionId) =>
                StrategicMapCampaignControl.Neutral,
            _ => throw new InvalidOperationException(
                $"Strategic map control is inconsistent ProvinceId={city.ProvinceId} LocationId={city.LocationId} LayoutId={city.LayoutId} owner={state.OwnerFactionId} control={state.ControlState}")
        };

        return new StrategicMapLocationControlView(
            city.ProvinceId,
            city.LocationId,
            city.LayoutId,
            state.OwnerFactionId,
            control);
    }
}
