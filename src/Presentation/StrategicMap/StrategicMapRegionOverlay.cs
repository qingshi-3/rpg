#nullable enable

using System;
using Godot;

namespace Rpg.Presentation.StrategicMap;

public partial class StrategicMapRegionOverlay : Node2D
{
    private ColorRect _surface = null!;
    private ShaderMaterial _material = null!;

    public override void _Ready()
    {
        _surface = GetNode<ColorRect>("Surface");
        _material = (ShaderMaterial)_surface.Material.Duplicate();
        _surface.Material = _material;
    }

    public void Bind(
        Vector2 worldOrigin,
        Vector2 worldSize,
        Texture2D territoryMask,
        Texture2D regionMetadata,
        StrategicMapPresentationConfig config)
    {
        if (worldSize.X <= 0f || worldSize.Y <= 0f || territoryMask == null || regionMetadata == null || config == null)
        {
            throw new InvalidOperationException("Strategic map static region overlay configuration is invalid.");
        }

        Position = worldOrigin;
        _surface.Size = worldSize;
        _material.SetShaderParameter("region_mask", territoryMask);
        _material.SetShaderParameter("region_metadata", regionMetadata);
        _material.SetShaderParameter("metadata_width", regionMetadata.GetWidth());
        _material.SetShaderParameter("mask_pixel_size", new Vector2(
            1f / territoryMask.GetWidth(),
            1f / territoryMask.GetHeight()));
        _material.SetShaderParameter("player_control_color", config.PlayerControlColor);
        _material.SetShaderParameter("enemy_control_color", config.EnemyControlColor);
        _material.SetShaderParameter("neutral_color", config.NeutralProvinceColor);
        _material.SetShaderParameter("fill_alpha", config.RegionFillAlpha);
        _material.SetShaderParameter("city_boundary_alpha", config.CityBoundaryAlpha);
        _material.SetShaderParameter("province_boundary_alpha", config.ProvinceBoundaryAlpha);
    }
}
