using System;
using Godot;

namespace Rpg.Presentation.World.Preview;

public partial class StrategicReferenceChunkVisual : Sprite2D
{
    public void Bind(StrategicRegionPreviewChunk chunk, float opacity)
    {
        Texture2D texture = GD.Load<Texture2D>(chunk.TextureResourcePath);
        if (texture == null)
        {
            throw new InvalidOperationException(
                $"Strategic region preview texture failed to load chunk={chunk.ChunkId} path={chunk.TextureResourcePath}");
        }

        Name = $"ReferenceChunk_{chunk.ChunkId}";
        Position = chunk.WorldOrigin;
        Centered = false;
        Texture = texture;
        Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(opacity, 0f, 1f));
    }
}
