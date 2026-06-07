using Godot;
using Rpg.Presentation.Common;
using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.Battle;

public partial class BattleCameraController : MapCameraController
{
    [ExportGroup("Action Follow")]

    [Export]
    public bool AutoFollowActionEntity { get; set; } = true;

    [Export(PropertyHint.Range, "0.15,0.4,0.01")]
    public float FollowDurationMinSeconds { get; set; } = 0.18f;

    [Export(PropertyHint.Range, "0.2,0.6,0.01")]
    public float FollowDurationMaxSeconds { get; set; } = 0.35f;

    [Export(PropertyHint.Range, "180,1200,10")]
    public float FollowDistanceForMaxDuration { get; set; } = 720f;

    [Export(PropertyHint.Range, "0.45,0.95,0.01")]
    public float FollowSafeAreaViewportRatio { get; set; } = 0.68f;

    [Export(PropertyHint.Range, "8,120,1")]
    public float FollowMinimumDistancePixels { get; set; } = 18f;

    private IBattleMapBoundsSource _mapBoundsSource;
    private Tween _followTween;
    private string _lastFollowEntityId = "";
    private bool _tacticalPauseActive;

    public override void _Ready()
    {
        base._Ready();
        _mapBoundsSource = ResolveMapBoundsSource();

        if (_mapBoundsSource == null)
        {
            GD.PushWarning("BattleCameraController could not find an IBattleMapBoundsSource ancestor.");
            return;
        }

        _mapBoundsSource.BattleMapLoaded += OnBattleMapLoaded;

        if (_mapBoundsSource.ActiveBattleMap != null)
        {
            OnBattleMapLoaded(_mapBoundsSource.ActiveBattleMap);
        }
    }

    public override void _ExitTree()
    {
        if (_mapBoundsSource != null)
        {
            _mapBoundsSource.BattleMapLoaded -= OnBattleMapLoaded;
        }

        CancelFollowTween();
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        if (IsUserNavigationActive)
        {
            CancelFollowTween();
        }
    }

    public void FollowActionEntityIfNeeded(BattleEntity entity, bool force = false)
    {
        if (!AutoFollowActionEntity ||
            _tacticalPauseActive ||
            entity == null ||
            !GodotObject.IsInstanceValid(entity) ||
            !IsInsideTree() ||
            IsUserNavigationActive)
        {
            return;
        }

        string entityId = string.IsNullOrWhiteSpace(entity.EntityId) ? entity.GetInstanceId().ToString() : entity.EntityId;
        Vector2 entityWorldPosition = entity.GlobalPosition;
        Vector2 clampedTarget = ResolveClampedFocusPosition(entityWorldPosition);
        float distance = GlobalPosition.DistanceTo(clampedTarget);
        if (distance < FollowMinimumDistancePixels)
        {
            _lastFollowEntityId = entityId;
            return;
        }

        bool insideSafeArea = IsInsideFollowSafeArea(entityWorldPosition);
        if (_followTween != null && !force && entityId == _lastFollowEntityId)
        {
            return;
        }

        if (!force && entityId == _lastFollowEntityId && insideSafeArea)
        {
            return;
        }

        if (!force && insideSafeArea)
        {
            _lastFollowEntityId = entityId;
            return;
        }

        float duration = Mathf.Lerp(
            FollowDurationMinSeconds,
            FollowDurationMaxSeconds,
            Mathf.Clamp(distance / Mathf.Max(FollowDistanceForMaxDuration, 1f), 0f, 1f));

        CancelFollowTween();
        _lastFollowEntityId = entityId;
        _followTween = CreateTween();
        _followTween.SetTrans(Tween.TransitionType.Cubic);
        _followTween.SetEase(Tween.EaseType.Out);
        _followTween.TweenProperty(this, "global_position", clampedTarget, duration);
        _followTween.Finished += () =>
        {
            _followTween = null;
        };
    }

    public void SetTacticalPauseActive(bool paused)
    {
        if (_tacticalPauseActive == paused)
        {
            return;
        }

        _tacticalPauseActive = paused;
        if (_tacticalPauseActive)
        {
            // Pause transfers camera authority to player observation; runtime follow
            // resumes only after battle time advances and emits later focus events.
            CancelFollowTween();
        }
    }

    private void OnBattleMapLoaded(Node activeSiteMap)
    {
        if (activeSiteMap is BattleMapView battleMapView &&
            TryCalculateMapBounds(battleMapView, out Rect2 mapBounds))
        {
            SetMapBounds(mapBounds);
            return;
        }

        if (!ClearMapBoundsAndApplyConfiguredFallback("site_map_loaded_without_runtime_bounds"))
        {
            GD.PushWarning("BattleCameraController could not calculate battle map bounds.");
        }
    }

    private IBattleMapBoundsSource ResolveMapBoundsSource()
    {
        for (Node node = GetParent(); node != null; node = node.GetParent())
        {
            if (node is IBattleMapBoundsSource source)
            {
                return source;
            }
        }

        return GetTree()?.CurrentScene as IBattleMapBoundsSource;
    }

    private bool IsInsideFollowSafeArea(Vector2 worldPosition)
    {
        Vector2 viewportPoint = GetViewport().GetCanvasTransform() * worldPosition;
        Vector2 viewportSize = GetViewportRect().Size;
        float ratio = Mathf.Clamp(FollowSafeAreaViewportRatio, 0.1f, 1f);
        Vector2 safeSize = viewportSize * ratio;
        Vector2 safeOffset = (viewportSize - safeSize) * 0.5f;
        Rect2 safeRect = new(safeOffset, safeSize);
        return safeRect.HasPoint(viewportPoint);
    }

    private void CancelFollowTween()
    {
        if (_followTween == null)
        {
            return;
        }

        _followTween.Kill();
        _followTween = null;
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
