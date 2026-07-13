#nullable enable

using System;
using Godot;
using Rpg.Definitions.StrategicMap;

namespace Rpg.Presentation.StrategicMap;

public partial class StrategicMapChunkVisual : Node2D
{
    private Sprite2D _surface = null!;

    public override void _Ready()
    {
        _surface = GetNode<Sprite2D>("Surface");
    }

    public void Bind(
        StrategicMapChunkDefinition chunk,
        Vector2 chunkSize,
        Texture2D texture,
        string resourcePath)
    {
        Name = $"Chunk_{SanitizeName(chunk.ChunkId)}";
        Position = new Vector2((float)chunk.WorldOrigin.X, (float)chunk.WorldOrigin.Y);
        _surface.Texture = texture;
        _surface.Centered = false;
        Vector2 textureSize = texture.GetSize();
        if (textureSize.X <= 0f || textureSize.Y <= 0f)
        {
            throw new InvalidOperationException($"Strategic map visual texture is empty chunkId={chunk.ChunkId} path={resourcePath}");
        }
        _surface.Scale = chunkSize / textureSize;
    }

    private static string SanitizeName(string value) => value.Replace(':', '_').Replace('/', '_');
}
