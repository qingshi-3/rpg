#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rpg.Definitions.StrategicMap;

namespace Rpg.Application.StrategicMap;

public static class StrategicMapPresentationContract
{
    public static void ThrowIfVisualBindingsInvalid(StrategicMapChunkManifest manifest, string projectRoot)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        string textureRoot = Path.GetFullPath(Path.Combine(projectRoot, "assets", "textures", "world"));
        List<string> failures = new();
        HashSet<string> resourcePaths = new(StringComparer.Ordinal);
        foreach (StrategicMapChunkDefinition chunk in manifest.Chunks)
        {
            if (string.IsNullOrWhiteSpace(chunk.VisualTexturePath))
            {
                failures.Add($"chunkId={chunk.ChunkId} path=<empty>");
                continue;
            }

            string resourcePath;
            try
            {
                resourcePath = ResolveVisualTextureResourcePath(chunk);
            }
            catch (InvalidOperationException exception)
            {
                failures.Add(exception.Message);
                continue;
            }
            if (!resourcePaths.Add(resourcePath))
            {
                failures.Add($"chunkId={chunk.ChunkId} path={resourcePath} reason=duplicate-resource-path");
                continue;
            }

            string normalized = resourcePath["res://".Length..].Replace('/', Path.DirectorySeparatorChar);
            string fullPath = Path.GetFullPath(Path.Combine(projectRoot, normalized));
            string relative = Path.GetRelativePath(textureRoot, fullPath);
            if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            {
                failures.Add($"chunkId={chunk.ChunkId} path={chunk.VisualTexturePath} reason=outside-world-texture-root");
                continue;
            }
            if (resourcePath.Contains("/reference/", StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"chunkId={chunk.ChunkId} path={chunk.VisualTexturePath} reason=reference-path-forbidden");
                continue;
            }
            if (!File.Exists(fullPath))
            {
                failures.Add($"chunkId={chunk.ChunkId} path={chunk.VisualTexturePath} reason=file-missing");
            }
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException($"Strategic map production visual bindings are invalid: {string.Join("; ", failures)}");
        }
    }

    public static string ResolveVisualTextureResourcePath(StrategicMapChunkDefinition chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        if (string.IsNullOrWhiteSpace(chunk.VisualTexturePath))
        {
            throw new InvalidOperationException($"Strategic map visual texture binding is missing chunkId={chunk.ChunkId} path=<empty>");
        }

        string normalized = chunk.VisualTexturePath.Replace('\\', '/');
        if (normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Contains("..", StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"Strategic map visual texture path is invalid chunkId={chunk.ChunkId} path={chunk.VisualTexturePath}");
        }
        if (normalized.StartsWith("res://", StringComparison.Ordinal))
        {
            return normalized;
        }
        return $"res://assets/textures/world/{normalized.TrimStart('/')}";
    }
}
