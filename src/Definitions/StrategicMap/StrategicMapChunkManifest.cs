#nullable enable

using System.Collections.Generic;

namespace Rpg.Definitions.StrategicMap;

public readonly record struct StrategicMapChunkCoordinate(int X, int Y);

public sealed record StrategicMapChunkDefinition(
    string ChunkId,
    StrategicMapChunkCoordinate Coordinate,
    StrategicMapPoint WorldOrigin,
    string? VisualTexturePath,
    string? ReferenceTexturePath,
    string? TerrainMaskPath,
    string? TerritoryMaskPath,
    string? NavigationScenePath);

public sealed record StrategicMapChunkManifest(
    int Version,
    double WorldWidth,
    double WorldHeight,
    double ChunkWidth,
    double ChunkHeight,
    IReadOnlyList<StrategicMapChunkDefinition> Chunks);

public sealed record StrategicMapCanonicalDefinition(
    StrategicMapChunkManifest ChunkManifest,
    StrategicMapGeographyDefinition Geography);

public sealed record StrategicMapRegionArtifactChunk(
    string ChunkId,
    StrategicMapPoint WorldOrigin,
    double WorldWidth,
    double WorldHeight,
    string MaskTexturePath);

public sealed record StrategicMapArtifactHash(
    string Kind,
    string ArtifactId,
    string Sha256);

public sealed record StrategicMapPackageManifest(
    int SchemaVersion,
    string MapId,
    string Revision,
    int CompatibilityRevision,
    string ContentHash,
    string PublishProfile,
    IReadOnlyList<string> Capabilities,
    string ChunkManifestPath,
    string GeographyPath,
    string RegionLookupPath,
    string RegionOutlinesPath,
    string RegionEncoding,
    IReadOnlyList<StrategicMapRegionArtifactChunk> RegionChunks,
    IReadOnlyList<StrategicMapArtifactHash> ArtifactHashes);

public sealed record StrategicMapPackageSelection(
    int Version,
    string PackageManifestPath,
    string ScenarioPath);

public sealed record StrategicMapLoadedContext(
    StrategicMapPackageManifest Package,
    StrategicMapCanonicalDefinition Canonical,
    StrategicMapRegionLookupDefinition RegionLookup);
