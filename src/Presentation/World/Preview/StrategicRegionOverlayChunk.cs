using System;
using Godot;

namespace Rpg.Presentation.World.Preview;

/// <summary>
/// Draws one rectangular territory-mask pass for a visual chunk. Region identity stays in the
/// nearest-sampled mask; no per-region mesh or polygon is created at runtime.
/// </summary>
public partial class StrategicRegionOverlayChunk : Node2D
{
    private ColorRect _surface = null!;
    private ShaderMaterial _material = null!;

    public override void _Ready()
    {
        _surface = GetNode<ColorRect>("MaskOverlay");
        _material = (ShaderMaterial)_surface.Material.Duplicate();
        _surface.Material = _material;
    }

    public void Bind(
        StrategicRegionPreviewChunk chunk,
        Texture2D territoryMask,
        Texture2D regionMetadata,
        float maskScale,
        StrategicRegionPreviewConfig config)
    {
        if (territoryMask == null || regionMetadata == null || maskScale <= 0f)
        {
            throw new InvalidOperationException($"Invalid territory-mask overlay configuration chunk={chunk.ChunkId}");
        }

        Name = $"RegionOverlay_{SanitizeName(chunk.ChunkId)}";
        Position = chunk.WorldOrigin;
        Rect2 maskRegion = new(chunk.WorldOrigin * maskScale, chunk.Size * maskScale);
        _surface.Size = chunk.Size;
        _material.SetShaderParameter("region_mask", territoryMask);
        _material.SetShaderParameter("region_metadata", regionMetadata);
        _material.SetShaderParameter(
            "mask_pixel_size",
            new Vector2(1f / territoryMask.GetWidth(), 1f / territoryMask.GetHeight()));
        _material.SetShaderParameter(
            "mask_uv_origin",
            maskRegion.Position / new Vector2(territoryMask.GetWidth(), territoryMask.GetHeight()));
        _material.SetShaderParameter(
            "mask_uv_size",
            maskRegion.Size / new Vector2(territoryMask.GetWidth(), territoryMask.GetHeight()));
        _material.SetShaderParameter("player_color", config.PlayerColor);
        _material.SetShaderParameter("hostile_color", config.HostileColor);
        _material.SetShaderParameter("neutral_color", config.NeutralColor);
    }

    public void SetPresentation(float hoveredRegionId, float selectedRegionId, float contextCityId)
    {
        _material.SetShaderParameter("hovered_region_id", hoveredRegionId);
        _material.SetShaderParameter("selected_region_id", selectedRegionId);
        _material.SetShaderParameter("context_city_id", contextCityId);
    }

    private static string SanitizeName(string value)
    {
        return value.Replace(':', '_').Replace('/', '_');
    }
}
