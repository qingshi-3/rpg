#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Rpg.Application.StrategicMap;

public enum StrategicMapCampaignControl
{
    Neutral,
    Player,
    Enemy
}

public sealed record StrategicMapLocationControlView(
    string ProvinceId,
    string LocationId,
    string LayoutId,
    string OwnerFactionId,
    StrategicMapCampaignControl Control);

public sealed record StrategicMapProvinceControlView(
    string ProvinceId,
    string LayoutId,
    IReadOnlyList<StrategicMapLocationControlView> Locations);

public sealed class StrategicMapCampaignPresentationView
{
    public StrategicMapCampaignPresentationView(
        IEnumerable<StrategicMapProvinceControlView> provinces,
        IEnumerable<StrategicMapLocationControlView> locations)
    {
        Provinces = new ReadOnlyCollection<StrategicMapProvinceControlView>(new List<StrategicMapProvinceControlView>(provinces));
        Locations = new ReadOnlyCollection<StrategicMapLocationControlView>(new List<StrategicMapLocationControlView>(locations));
    }

    public IReadOnlyList<StrategicMapProvinceControlView> Provinces { get; }
    public IReadOnlyList<StrategicMapLocationControlView> Locations { get; }
}

public interface IStrategicMapCampaignPresentationPort
{
    StrategicMapCampaignPresentationView Read();
}
