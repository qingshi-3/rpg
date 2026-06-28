using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.World;
using Rpg.Domain.World;

namespace Rpg.Presentation.World;

public partial class StrategicWorldRoot
{
    private void RefreshStrategicFogOverlay()
    {
        if (!FogOfWarEnabled ||
            _fogOverlay == null ||
            State?.Fog == null ||
            !TryCalculateStrategicMapBounds(out Rect2 mapBounds))
        {
            _fogOverlay?.ClearFog();
            _strategicFogMaskReady = false;
            return;
        }

        StrategicFogOfWarSettings settings = BuildFogSettings();
        _lastStrategicFogRefreshSignature = BuildStrategicFogRefreshSignature(settings);
        Color unknownColor = new(0.015f, 0.016f, 0.018f, 0.86f);
        List<StrategicWorldFogOverlayCircle> visibleCircles = BuildStrategicFogOverlayCircles(settings);
        _fogOverlay.SetFog(
            mapBounds,
            visibleCircles,
            unknownColor);
        _strategicFogMaskReady = true;
    }

    private void RefreshStrategicFogVisibleCircles()
    {
        if (!FogOfWarEnabled || _fogOverlay == null || State?.Fog == null || Definition == null)
        {
            return;
        }

        _fogOverlay.SetVisibleCircles(BuildStrategicFogOverlayCircles(BuildFogSettings()));
    }

    private List<StrategicWorldFogOverlayCircle> BuildStrategicFogOverlayCircles(StrategicFogOfWarSettings settings)
    {
        List<StrategicWorldFogOverlayCircle> circles = new();
        foreach (StrategicFogVisionSource source in StrategicFogOfWarService.BuildVisionSources(State, Definition, settings))
        {
            Vector2 center = source.Position;
            float radius = source.Radius;
            if (radius > 0.0f)
            {
                circles.Add(new StrategicWorldFogOverlayCircle(center, radius));
            }
        }

        return circles;
    }

    private bool RefreshStrategicFog()
    {
        if (!FogOfWarEnabled || State == null || Definition == null)
        {
            return false;
        }

        StrategicFogOfWarSettings settings = BuildFogSettings();
        string refreshSignature = BuildStrategicFogRefreshSignature(settings);
        if (_strategicFogMaskReady &&
            string.Equals(refreshSignature, _lastStrategicFogRefreshSignature, StringComparison.Ordinal))
        {
            RefreshStrategicFogVisibleCircles();
            return false;
        }

        StrategicFogOfWarService.RefreshVisibility(State, Definition, settings);
        _lastStrategicFogRefreshSignature = refreshSignature;
        if (!_strategicFogMaskReady)
        {
            RefreshStrategicFogOverlay();
        }
        else
        {
            RefreshStrategicFogVisibleCircles();
        }

        return true;
    }

    private void ResetStrategicFogMaskCache()
    {
        _strategicFogMaskReady = false;
        _lastStrategicFogRefreshSignature = "";
        _fogOverlay?.ClearFog();
    }

    private StrategicFogOfWarSettings BuildFogSettings()
    {
        return new StrategicFogOfWarSettings
        {
            FogTexelWorldSize = Mathf.Max(FogTexelWorldSize, 1.0f),
            SiteVisionRadius = Mathf.Max(SiteVisionRadius, 1.0f),
            ArmyVisionRadius = Mathf.Max(ArmyVisionRadius, 1.0f)
        };
    }

    private string BuildStrategicFogRefreshSignature(StrategicFogOfWarSettings settings)
    {
        if (State == null || Definition == null)
        {
            return "";
        }

        float fogTexelWorldSize = Mathf.Max(settings?.FogTexelWorldSize ?? StrategicFogOfWarService.DefaultFogTexelWorldSize, 1.0f);
        List<string> parts = new();
        foreach (StrategicFogVisionSource source in StrategicFogOfWarService.BuildVisionSources(State, Definition, settings))
        {
            int cellX = Mathf.FloorToInt(source.Position.X / fogTexelWorldSize);
            int cellY = Mathf.FloorToInt(source.Position.Y / fogTexelWorldSize);
            parts.Add($"{cellX}:{cellY}:{Mathf.RoundToInt(source.Radius)}");
        }

        parts.Sort(StringComparer.Ordinal);
        return $"{fogTexelWorldSize:0.###}|{parts.Count}|{string.Join("|", parts)}";
    }

    private bool IsMapPositionVisible(Vector2 mapPosition)
    {
        return !FogOfWarEnabled ||
               StrategicFogOfWarService.GetPositionVisibility(State?.Fog, mapPosition, BuildFogSettings()) == StrategicFogVisibility.Visible;
    }
}
