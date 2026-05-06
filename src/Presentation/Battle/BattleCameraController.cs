using Godot;
using Rpg.Presentation.Common;
using Rpg.Presentation.World.Sites;

namespace Rpg.Presentation.Battle;

public partial class BattleCameraController : MapCameraController
{
    private WorldSiteRoot _siteRoot;

    public override void _Ready()
    {
        base._Ready();
        _siteRoot = GetParentOrNull<WorldSiteRoot>();

        if (_siteRoot == null)
        {
            GD.PushWarning("BattleCameraController must be a child of WorldSiteRoot.");
            return;
        }

        _siteRoot.SiteMapLoaded += OnSiteMapLoaded;

        if (_siteRoot.ActiveSiteMap != null)
        {
            OnSiteMapLoaded(_siteRoot.ActiveSiteMap);
        }
    }

    private void OnSiteMapLoaded(Node activeSiteMap)
    {
        if (activeSiteMap is not BattleMapView battleMapView ||
            !TryCalculateMapBounds(battleMapView, out Rect2 mapBounds))
        {
            GD.PushWarning("BattleCameraController could not calculate battle map bounds.");
            return;
        }

        SetMapBounds(mapBounds);
    }

    private static bool TryCalculateMapBounds(BattleMapView battleMapView, out Rect2 bounds)
    {
        bounds = default;
        BattleMapLayer groundLayer = BattleMapLayerQueries.FindLowestFoundationLayer(battleMapView);

        if (groundLayer == null)
        {
            return false;
        }

        bool hasPoint = false;

        foreach (Vector2I cell in groundLayer.GetUsedCells())
        {
            foreach (Vector2 point in BuildCellGlobalPolygon(groundLayer, cell))
            {
                if (!hasPoint)
                {
                    bounds = new Rect2(point, Vector2.Zero);
                    hasPoint = true;
                    continue;
                }

                bounds = bounds.Expand(point);
            }
        }

        return hasPoint;
    }

    private static Vector2[] BuildCellGlobalPolygon(BattleMapLayer layer, Vector2I cell)
    {
        Vector2 center = layer.MapToLocal(cell);
        Vector2 stepX = layer.MapToLocal(new Vector2I(cell.X + 1, cell.Y)) - center;
        Vector2 stepY = layer.MapToLocal(new Vector2I(cell.X, cell.Y + 1)) - center;

        Vector2[] localPoints =
        {
            center - (stepX + stepY) * 0.5f,
            center + (stepX - stepY) * 0.5f,
            center + (stepX + stepY) * 0.5f,
            center + (-stepX + stepY) * 0.5f
        };

        return new[]
        {
            layer.ToGlobal(localPoints[0]),
            layer.ToGlobal(localPoints[1]),
            layer.ToGlobal(localPoints[2]),
            layer.ToGlobal(localPoints[3])
        };
    }
}
