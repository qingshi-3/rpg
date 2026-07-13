#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using Rpg.Definitions.StrategicMap;

namespace Rpg.Application.StrategicMap;

public static class StrategicMapChunkManifestLoader
{
    public static StrategicMapChunkManifest Load(string path)
    {
        using JsonDocument document = StrategicMapJson.ParseRequired(path);
        JsonElement root = document.RootElement;
        int version = StrategicMapJson.RequiredInt(root, "version", path);
        if (version is not (1 or 2))
        {
            throw new InvalidOperationException($"Unsupported strategic map chunk manifest version path={path} expected=1-or-2 actual={version}");
        }

        JsonElement world = root.GetProperty("world");
        JsonElement chunkContract = root.GetProperty("chunk");
        List<StrategicMapChunkDefinition> chunks = new();
        foreach (JsonElement element in root.GetProperty("chunks").EnumerateArray())
        {
            JsonElement coordinate = element.GetProperty("coordinate");
            StrategicMapPoint coordinateValues = StrategicMapJson.ReadPoint(coordinate, path);
            if (coordinateValues.X != Math.Truncate(coordinateValues.X) || coordinateValues.Y != Math.Truncate(coordinateValues.Y))
            {
                throw new InvalidOperationException($"Strategic map chunk coordinate must be integral path={path}");
            }
            chunks.Add(new StrategicMapChunkDefinition(
                StrategicMapJson.RequiredString(element, "id", path),
                new StrategicMapChunkCoordinate((int)coordinateValues.X, (int)coordinateValues.Y),
                StrategicMapJson.ReadPoint(element.GetProperty("worldOrigin"), path),
                StrategicMapJson.OptionalString(element, "visualTexturePath"),
                StrategicMapJson.OptionalString(element, "referenceTexturePath"),
                StrategicMapJson.OptionalString(element, "terrainMaskPath"),
                StrategicMapJson.OptionalString(element, "territoryMaskPath"),
                StrategicMapJson.OptionalString(element, "navigationScenePath")));
        }

        return new StrategicMapChunkManifest(
            version,
            StrategicMapJson.RequiredNumber(world, "width", path),
            StrategicMapJson.RequiredNumber(world, "height", path),
            StrategicMapJson.RequiredNumber(chunkContract, "width", path),
            StrategicMapJson.RequiredNumber(chunkContract, "height", path),
            chunks);
    }
}
