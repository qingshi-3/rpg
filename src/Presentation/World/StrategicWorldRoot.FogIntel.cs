using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World;

public partial class StrategicWorldRoot
{
    private void RefreshStrategicFogOverlay()
    {
        if (!FogOfWarEnabled ||
            _fogOverlay == null ||
            State?.Intel == null ||
            !TryCalculateStrategicMapBounds(out Rect2 mapBounds))
        {
            _fogOverlay?.ClearFog();
            _strategicFogMaskReady = false;
            return;
        }

        StrategicFogOfWarSettings settings = BuildFogSettings();
        HashSet<string> explored = State.Intel.ExploredCells.ToHashSet();
        Color unknownColor = new(0.015f, 0.016f, 0.018f, 0.86f);
        Color revealedColor = new(0.025f, 0.03f, 0.035f, 0.42f);
        List<StrategicWorldFogOverlayRect> revealedRects = new();

        foreach (string cellKey in StrategicFogOfWarService.EnumerateCellKeysForBounds(mapBounds, settings))
        {
            if (!explored.Contains(cellKey))
            {
                continue;
            }

            Rect2 mapRect = StrategicFogOfWarService.CellKeyToWorldRect(cellKey, settings);
            Rect2 screenRect = MapRectToViewportLocal(mapRect);
            revealedRects.Add(new StrategicWorldFogOverlayRect(
                screenRect,
                revealedColor));
        }

        List<StrategicWorldFogOverlayCircle> visibleCircles = BuildStrategicFogOverlayCircles(settings);
        _fogOverlay.SetFog(
            MapRectToViewportLocal(mapBounds),
            revealedRects,
            visibleCircles,
            EstimateFogTexelScreenSize(settings.FogTexelWorldSize),
            unknownColor);
        _strategicFogMaskReady = true;
    }

    private void RefreshStrategicFogVisibleCircles()
    {
        if (!FogOfWarEnabled || _fogOverlay == null || State?.Intel == null || Definition == null)
        {
            return;
        }

        // 移动帧只更新动态视野圈；已探索 mask 由 RefreshStrategicFogOverlay 在探索格增长或相机变换时重建。
        _fogOverlay.SetVisibleCircles(BuildStrategicFogOverlayCircles(BuildFogSettings()));
    }

    private List<StrategicWorldFogOverlayCircle> BuildStrategicFogOverlayCircles(StrategicFogOfWarSettings settings)
    {
        List<StrategicWorldFogOverlayCircle> circles = new();
        foreach (StrategicFogVisionSource source in StrategicFogOfWarService.BuildVisionSources(State, Definition, settings))
        {
            Vector2 center = MapToViewportLocal(source.Position);
            float radiusX = center.DistanceTo(MapToViewportLocal(source.Position + new Vector2(source.Radius, 0.0f)));
            float radiusY = center.DistanceTo(MapToViewportLocal(source.Position + new Vector2(0.0f, source.Radius)));
            float radius = Mathf.Max(radiusX, radiusY);
            if (radius > 0.0f)
            {
                circles.Add(new StrategicWorldFogOverlayCircle(center, radius));
            }
        }

        return circles;
    }

    private bool RefreshWorldIntel()
    {
        if (!FogOfWarEnabled || State == null || Definition == null)
        {
            return false;
        }

        int exploredCountBefore = State.Intel?.ExploredCells?.Count ?? 0;
        bool changed = StrategicFogOfWarService.RefreshVisibility(State, Definition, BuildFogSettings());
        int exploredCountAfter = State.Intel?.ExploredCells?.Count ?? 0;
        if (!_strategicFogMaskReady || exploredCountAfter != exploredCountBefore)
        {
            RefreshStrategicFogOverlay();
        }
        else
        {
            RefreshStrategicFogVisibleCircles();
        }

        if (changed)
        {
            QueueStrategicOverlayRedraw();
        }

        return changed;
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

    private float EstimateFogTexelScreenSize(float fogTexelWorldSize)
    {
        Vector2 mapOriginScreen = MapToViewportLocal(Vector2.Zero);
        Vector2 mapOffsetXScreen = MapToViewportLocal(new Vector2(1.0f, 0.0f));
        Vector2 mapOffsetYScreen = MapToViewportLocal(new Vector2(0.0f, 1.0f));

        float scaleX = (mapOffsetXScreen - mapOriginScreen).Length();
        float scaleY = (mapOffsetYScreen - mapOriginScreen).Length();
        return fogTexelWorldSize * Mathf.Max(1.0f, Mathf.Max(scaleX, scaleY));
    }

    private WorldIntelVisibility GetSiteIntelVisibility(WorldSiteDefinition definition)
    {
        return !FogOfWarEnabled
            ? WorldIntelVisibility.Visible
            : StrategicFogOfWarService.GetSiteVisibility(State?.Intel, definition, BuildFogSettings());
    }

    private bool IsMapPositionVisible(Vector2 mapPosition)
    {
        return !FogOfWarEnabled ||
               StrategicFogOfWarService.GetPositionVisibility(State?.Intel, mapPosition, BuildFogSettings()) == WorldIntelVisibility.Visible;
    }

    private bool IsScreenPositionVisible(Vector2 screenPosition)
    {
        return IsMapPositionVisible(ScreenToMap(screenPosition));
    }

    private bool IsViewportPositionVisible(Vector2 viewportLocalPosition)
    {
        return IsMapPositionVisible(ViewportLocalToMap(viewportLocalPosition));
    }
}
