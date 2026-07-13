#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Rpg.Definitions.StrategicMap;

namespace Rpg.Application.StrategicMap;

public static class StrategicMapPackageLoader
{
    public const int SelectionVersion = 1;
    public const int PackageSchemaVersion = 2;
    public const string RegionEncoding = "rgb24-location-code-v1";

    public static StrategicMapPackageSelection LoadSelection(string projectRoot, string selectionResourcePath)
    {
        string path = ResolveProjectPath(projectRoot, selectionResourcePath);
        using JsonDocument document = StrategicMapJson.ParseRequired(path);
        JsonElement root = document.RootElement;
        int version = StrategicMapJson.RequiredInt(root, "version", path);
        if (version != SelectionVersion)
        {
            throw new InvalidOperationException($"Unsupported strategic map selection version path={path} expected={SelectionVersion} actual={version}");
        }
        return new StrategicMapPackageSelection(
            version,
            StrategicMapJson.RequiredString(root, "packageManifestPath", path),
            StrategicMapJson.RequiredString(root, "scenarioPath", path));
    }

    public static StrategicMapLoadedContext LoadSelected(string projectRoot, StrategicMapPackageSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        string manifestPath = ResolveProjectPath(projectRoot, selection.PackageManifestPath);
        StrategicMapPackageManifest package = LoadManifest(projectRoot, manifestPath);
        ValidatePackageRoots(package, selection.PackageManifestPath, projectRoot);
        StrategicMapChunkManifest chunks = StrategicMapChunkManifestLoader.Load(
            ResolveProjectPath(projectRoot, package.ChunkManifestPath));
        StrategicMapGeographyDefinition geography = StrategicMapGeographyLoader.Load(
            ResolveProjectPath(projectRoot, package.GeographyPath));
        StrategicMapRegionLookupDefinition lookup = StrategicMapRegionLookupLoader.Load(
            ResolveProjectPath(projectRoot, package.RegionLookupPath));
        StrategicMapCanonicalDefinition canonical = new(chunks, geography);
        StrategicMapValidator.ThrowIfInvalid(canonical);
        StrategicMapRegionLookupLoader.ValidateAgainstCanonical(lookup, geography);
        ValidatePackage(package, canonical, projectRoot);
        ValidateArtifactIntegrity(package, canonical, projectRoot);
        return new StrategicMapLoadedContext(package, canonical, lookup);
    }

    private static StrategicMapPackageManifest LoadManifest(string projectRoot, string path)
    {
        using JsonDocument document = StrategicMapJson.ParseRequired(path);
        JsonElement root = document.RootElement;
        int schemaVersion = StrategicMapJson.RequiredInt(root, "schemaVersion", path);
        if (schemaVersion != PackageSchemaVersion)
        {
            throw new InvalidOperationException($"Unsupported strategic map package schema path={path} expected={PackageSchemaVersion} actual={schemaVersion}");
        }

        List<string> capabilities = root.GetProperty("capabilities").EnumerateArray()
            .Select(value => value.GetString() ?? "")
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
        List<StrategicMapRegionArtifactChunk> regionChunks = new();
        foreach (JsonElement element in root.GetProperty("regionArtifacts").GetProperty("chunks").EnumerateArray())
        {
            regionChunks.Add(new StrategicMapRegionArtifactChunk(
                StrategicMapJson.RequiredString(element, "chunkId", path),
                StrategicMapJson.ReadPoint(element.GetProperty("worldOrigin"), path),
                StrategicMapJson.RequiredNumber(element, "worldWidth", path),
                StrategicMapJson.RequiredNumber(element, "worldHeight", path),
                StrategicMapJson.RequiredString(element, "maskTexturePath", path)));
        }

        List<StrategicMapArtifactHash> artifactHashes = new();
        foreach (JsonElement element in root.GetProperty("artifactHashes").EnumerateArray())
        {
            artifactHashes.Add(new StrategicMapArtifactHash(
                StrategicMapJson.RequiredString(element, "kind", path),
                StrategicMapJson.RequiredString(element, "artifactId", path),
                StrategicMapJson.RequiredString(element, "sha256", path)));
        }

        JsonElement region = root.GetProperty("regionArtifacts");
        return new StrategicMapPackageManifest(
            schemaVersion,
            StrategicMapJson.RequiredString(root, "mapId", path),
            StrategicMapJson.RequiredString(root, "revision", path),
            StrategicMapJson.RequiredInt(root, "compatibilityRevision", path),
            StrategicMapJson.RequiredString(root, "contentHash", path),
            StrategicMapJson.RequiredString(root, "publishProfile", path),
            capabilities,
            StrategicMapJson.RequiredString(root, "chunkManifestPath", path),
            StrategicMapJson.RequiredString(root, "geographyPath", path),
            StrategicMapJson.RequiredString(region, "lookupPath", path),
            StrategicMapJson.RequiredString(region, "outlinesPath", path),
            StrategicMapJson.RequiredString(region, "encoding", path),
            regionChunks,
            artifactHashes);
    }

    private static void ValidatePackageRoots(
        StrategicMapPackageManifest package,
        string manifestResourcePath,
        string projectRoot)
    {
        string configPrefix = $"res://config/world/published/{package.MapId}/{package.Revision}/";
        string assetPrefix = $"res://assets/textures/world/maps/{package.MapId}/{package.Revision}/";
        RequireRevisionPath(manifestResourcePath, configPrefix, "package.json", package);
        RequireRevisionPath(package.ChunkManifestPath, configPrefix, "chunk-manifest.json", package);
        RequireRevisionPath(package.GeographyPath, configPrefix, "geography.json", package);
        RequireRevisionPath(package.RegionLookupPath, assetPrefix, "regions/region_lookup.json", package);
        RequireRevisionPath(package.RegionOutlinesPath, assetPrefix, "regions/region_outlines.json", package);
        _ = ResolveProjectPath(projectRoot, package.ChunkManifestPath);
        _ = ResolveProjectPath(projectRoot, package.GeographyPath);
        _ = ResolveProjectPath(projectRoot, package.RegionLookupPath);
        _ = ResolveProjectPath(projectRoot, package.RegionOutlinesPath);
    }

    private static void RequireRevisionPath(
        string resourcePath,
        string expectedPrefix,
        string expectedSuffix,
        StrategicMapPackageManifest package)
    {
        if (resourcePath.Contains("..", StringComparison.Ordinal) ||
            !resourcePath.StartsWith(expectedPrefix, StringComparison.Ordinal) ||
            !string.Equals(resourcePath, expectedPrefix + expectedSuffix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Strategic map package path is outside immutable revision MapId={package.MapId} revision={package.Revision} path={resourcePath}");
        }
    }

    private static void RequireRevisionChildPath(
        string resourcePath,
        string expectedPrefix,
        StrategicMapPackageManifest package)
    {
        if (resourcePath.Contains("..", StringComparison.Ordinal) ||
            !resourcePath.StartsWith(expectedPrefix, StringComparison.Ordinal) ||
            resourcePath.Length <= expectedPrefix.Length)
        {
            throw new InvalidOperationException(
                $"Strategic map package child path is outside immutable revision MapId={package.MapId} revision={package.Revision} path={resourcePath}");
        }
    }

    private static void ValidatePackage(
        StrategicMapPackageManifest package,
        StrategicMapCanonicalDefinition canonical,
        string projectRoot)
    {
        if (package.CompatibilityRevision <= 0 ||
            !string.Equals(package.RegionEncoding, RegionEncoding, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Strategic map package contract is invalid MapId={package.MapId} revision={package.Revision}");
        }
        if (!package.Capabilities.Contains("visual", StringComparer.Ordinal) ||
            !package.Capabilities.Contains("regions", StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"Strategic map package lacks production capabilities MapId={package.MapId} revision={package.Revision}");
        }

        Dictionary<string, StrategicMapChunkDefinition> chunks = canonical.ChunkManifest.Chunks
            .ToDictionary(chunk => chunk.ChunkId, StringComparer.Ordinal);
        string assetPrefix = $"res://assets/textures/world/maps/{package.MapId}/{package.Revision}/";
        string configPrefix = $"res://config/world/published/{package.MapId}/{package.Revision}/";
        HashSet<string> regionChunkIds = new(StringComparer.Ordinal);
        foreach (StrategicMapRegionArtifactChunk region in package.RegionChunks)
        {
            if (!regionChunkIds.Add(region.ChunkId) || !chunks.TryGetValue(region.ChunkId, out StrategicMapChunkDefinition? chunk) ||
                region.WorldOrigin != chunk.WorldOrigin || region.WorldWidth != canonical.ChunkManifest.ChunkWidth ||
                region.WorldHeight != canonical.ChunkManifest.ChunkHeight)
            {
                throw new InvalidOperationException($"Strategic map region chunk mismatch MapId={package.MapId} chunkId={region.ChunkId}");
            }
            RequireRevisionChildPath(region.MaskTexturePath, assetPrefix, package);
            _ = ResolveProjectPath(projectRoot, region.MaskTexturePath);
        }
        if (!regionChunkIds.SetEquals(chunks.Keys))
        {
            throw new InvalidOperationException($"Strategic map package region chunk coverage is incomplete MapId={package.MapId}");
        }
        foreach (StrategicMapChunkDefinition chunk in chunks.Values)
        {
            if (string.IsNullOrWhiteSpace(chunk.VisualTexturePath))
            {
                throw new InvalidOperationException($"Strategic map package visual binding is missing MapId={package.MapId} chunkId={chunk.ChunkId}");
            }
            RequireRevisionChildPath(chunk.VisualTexturePath, assetPrefix, package);
            _ = ResolveProjectPath(projectRoot, chunk.VisualTexturePath);
            if (!string.IsNullOrWhiteSpace(chunk.ReferenceTexturePath))
            {
                throw new InvalidOperationException($"Strategic map published chunk retains reference media MapId={package.MapId} chunkId={chunk.ChunkId}");
            }
            if (!string.IsNullOrWhiteSpace(chunk.TerritoryMaskPath))
            {
                RequireRevisionChildPath(chunk.TerritoryMaskPath, assetPrefix, package);
                _ = ResolveProjectPath(projectRoot, chunk.TerritoryMaskPath);
            }
            if (!string.IsNullOrWhiteSpace(chunk.TerrainMaskPath))
            {
                RequireRevisionChildPath(chunk.TerrainMaskPath, assetPrefix, package);
                _ = ResolveProjectPath(projectRoot, chunk.TerrainMaskPath);
            }
            if (!string.IsNullOrWhiteSpace(chunk.NavigationScenePath))
            {
                RequireRevisionChildPath(chunk.NavigationScenePath, configPrefix, package);
                _ = ResolveProjectPath(projectRoot, chunk.NavigationScenePath);
            }
        }
    }

    private static void ValidateArtifactIntegrity(
        StrategicMapPackageManifest package,
        StrategicMapCanonicalDefinition canonical,
        string projectRoot)
    {
        Dictionary<(string Kind, string ArtifactId), string> expectedPaths = new()
        {
            [("chunk-manifest", "manifest")] = package.ChunkManifestPath,
            [("geography", "geography")] = package.GeographyPath,
            [("region-lookup", "lookup")] = package.RegionLookupPath,
            [("region-outlines", "outlines")] = package.RegionOutlinesPath
        };
        Dictionary<string, StrategicMapRegionArtifactChunk> regionChunks = package.RegionChunks
            .ToDictionary(chunk => chunk.ChunkId, StringComparer.Ordinal);
        foreach (StrategicMapChunkDefinition chunk in canonical.ChunkManifest.Chunks)
        {
            expectedPaths.Add(("visual", chunk.ChunkId), chunk.VisualTexturePath!);
            expectedPaths.Add(("region-mask", chunk.ChunkId), regionChunks[chunk.ChunkId].MaskTexturePath);
            if (!string.IsNullOrWhiteSpace(chunk.TerrainMaskPath)) expectedPaths.Add(("terrain", chunk.ChunkId), chunk.TerrainMaskPath);
            if (!string.IsNullOrWhiteSpace(chunk.NavigationScenePath)) expectedPaths.Add(("navigation", chunk.ChunkId), chunk.NavigationScenePath);
        }

        Dictionary<(string Kind, string ArtifactId), StrategicMapArtifactHash> declared = new();
        foreach (StrategicMapArtifactHash artifact in package.ArtifactHashes)
        {
            if (artifact.Sha256.Length != 64 || artifact.Sha256.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')) ||
                !declared.TryAdd((artifact.Kind, artifact.ArtifactId), artifact))
            {
                throw new InvalidOperationException($"Strategic map package artifact hash declaration is invalid MapId={package.MapId} kind={artifact.Kind} artifactId={artifact.ArtifactId}");
            }
        }
        if (!declared.Keys.ToHashSet().SetEquals(expectedPaths.Keys))
        {
            throw new InvalidOperationException($"Strategic map package artifact hash coverage mismatch MapId={package.MapId} revision={package.Revision}");
        }
        string aggregate = ComputeAggregateHash(package.ArtifactHashes);
        if (!string.Equals(aggregate, package.ContentHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Strategic map package content hash mismatch MapId={package.MapId} revision={package.Revision}");
        }
        foreach (((string kind, string artifactId), string resourcePath) in expectedPaths)
        {
            string actual = ComputeFileHash(ResolveProjectPath(projectRoot, resourcePath));
            if (!string.Equals(actual, declared[(kind, artifactId)].Sha256, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Strategic map package artifact hash mismatch MapId={package.MapId} revision={package.Revision} kind={kind} artifactId={artifactId} path={resourcePath}");
            }
        }
    }

    private static string ComputeAggregateHash(IEnumerable<StrategicMapArtifactHash> artifacts)
    {
        StringBuilder canonical = new();
        foreach (StrategicMapArtifactHash artifact in artifacts
                     .OrderBy(item => item.Kind, StringComparer.Ordinal)
                     .ThenBy(item => item.ArtifactId, StringComparer.Ordinal))
        {
            canonical.Append(artifact.Kind).Append('\0')
                .Append(artifact.ArtifactId).Append('\0')
                .Append(artifact.Sha256).Append('\n');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
    }

    private static string ComputeFileHash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public static string ResolveProjectPath(string projectRoot, string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(projectRoot) || string.IsNullOrWhiteSpace(resourcePath) ||
            !resourcePath.StartsWith("res://", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Strategic map package path must use res:// path={resourcePath}");
        }
        string root = Path.GetFullPath(projectRoot);
        string resolved = Path.GetFullPath(Path.Combine(root, resourcePath[6..].Replace('/', Path.DirectorySeparatorChar)));
        string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(resolved))
        {
            throw new FileNotFoundException($"Strategic map package path is missing or escapes project root path={resourcePath}", resolved);
        }
        return resolved;
    }
}
