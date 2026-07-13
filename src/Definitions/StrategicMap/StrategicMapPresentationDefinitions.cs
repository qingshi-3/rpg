#nullable enable

using System.Collections.Generic;

namespace Rpg.Definitions.StrategicMap;

public readonly record struct StrategicMapWorldRect(double X, double Y, double Width, double Height)
{
    public double EndX => X + Width;
    public double EndY => Y + Height;
    public bool IsValid => Width >= 0d && Height >= 0d;

    public bool Contains(StrategicMapPoint point) =>
        point.X >= X && point.X < EndX && point.Y >= Y && point.Y < EndY;

    public bool Intersects(StrategicMapWorldRect other) =>
        X < other.EndX && EndX > other.X && Y < other.EndY && EndY > other.Y;

    public StrategicMapWorldRect Expanded(double margin) => new(
        X - margin,
        Y - margin,
        Width + margin * 2d,
        Height + margin * 2d);
}

public sealed record StrategicMapRegionLookupEntry(
    int MaskId,
    string LocationId,
    string ProvinceId);

public sealed record StrategicMapRegionLookupDefinition(
    int Version,
    IReadOnlyList<StrategicMapRegionLookupEntry> Entries);

public sealed record StrategicMapChunkLoadRequest(
    StrategicMapChunkDefinition Chunk,
    string ResourcePath);

public enum StrategicMapChunkLoadCompletion
{
    Resident,
    Stale,
    Failed
}
