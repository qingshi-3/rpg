#nullable enable

using System;
using System.Collections.Generic;

namespace Rpg.Definitions.StrategicMap;

public enum StrategicLocationType
{
    MainCity,
    AuxiliaryCity,
    Gate,
    Bridge,
    Ferry,
    Port,
    Ruin,
    ResourceSite
}

public readonly record struct StrategicMapPoint(double X, double Y)
{
    public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y);
}

public sealed record ProvinceDefinition(
    string ProvinceId,
    string Name,
    string LayoutId);

public sealed record StrategicLocationDefinition(
    string LocationId,
    string Name,
    StrategicLocationType LocationType,
    string? ProvinceId,
    StrategicMapPoint WorldPosition);

public sealed record StrategicMapPolygon(IReadOnlyList<IReadOnlyList<StrategicMapPoint>> Rings);

public sealed record StrategicMapGeometry(IReadOnlyList<StrategicMapPolygon> Polygons);

public sealed record LocationGeometryDefinition(
    string LocationId,
    string ProvinceId,
    string Direction,
    StrategicMapGeometry Geometry);

public sealed record StrategicMapGeographyDefinition(
    int Version,
    IReadOnlyList<ProvinceDefinition> Provinces,
    IReadOnlyList<StrategicLocationDefinition> Locations,
    IReadOnlyList<LocationGeometryDefinition> LocationGeometries);
