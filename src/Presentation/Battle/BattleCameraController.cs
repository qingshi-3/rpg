using Godot;

namespace Rpg.Presentation.Battle;

public partial class BattleCameraController : Camera2D
{
    [ExportGroup("移动")]

    [Export]
    public bool KeyboardMoveEnabled { get; set; } = true;

    [Export]
    public float MoveSpeed { get; set; } = 560f;

    [Export]
    public float MoveSpeedZoomExponent { get; set; } = 0.55f;

    [Export]
    public float MaxMoveSpeedZoomMultiplier { get; set; } = 2f;

    [ExportGroup("缩放")]

    [Export]
    public bool MouseWheelZoomEnabled { get; set; } = true;

    [Export]
    public float MinZoom { get; set; } = 1.25f;

    [Export]
    public float MaxZoom { get; set; } = 4f;

    [Export]
    public float ZoomStep { get; set; } = 0.18f;

    private BattleRoot _battleRoot;
    private Rect2 _mapBounds;
    private bool _hasMapBounds;
    private Vector2 _lastViewportSize;
    private bool _hasViewportSize;
    private bool _moveUpPressed;
    private bool _moveDownPressed;
    private bool _moveLeftPressed;
    private bool _moveRightPressed;

    public override void _Ready()
    {
        Enabled = true;
        _battleRoot = GetParentOrNull<BattleRoot>();

        if (_battleRoot == null)
        {
            GD.PushWarning("BattleCameraController must be a child of BattleRoot.");
            return;
        }

        _battleRoot.BattleMapLoaded += OnBattleMapLoaded;

        if (_battleRoot.ActiveMap != null)
        {
            OnBattleMapLoaded(_battleRoot.ActiveMap);
        }
    }

    public override void _Process(double delta)
    {
        ApplyViewportSizeConstraintsIfChanged();

        if (!KeyboardMoveEnabled)
        {
            return;
        }

        Vector2 direction = GetMoveDirection();

        if (direction == Vector2.Zero)
        {
            return;
        }

        GlobalPosition += direction.Normalized() * GetEffectiveMoveSpeed() * (float)delta;
        ClampToMapBounds();
    }

    public override void _Input(InputEvent @event)
    {
        if (!KeyboardMoveEnabled || @event is not InputEventKey keyEvent || keyEvent.Echo)
        {
            return;
        }

        if (IsMoveKey(keyEvent, Key.W))
        {
            _moveUpPressed = keyEvent.Pressed;
        }
        else if (IsMoveKey(keyEvent, Key.S))
        {
            _moveDownPressed = keyEvent.Pressed;
        }
        else if (IsMoveKey(keyEvent, Key.A))
        {
            _moveLeftPressed = keyEvent.Pressed;
        }
        else if (IsMoveKey(keyEvent, Key.D))
        {
            _moveRightPressed = keyEvent.Pressed;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!MouseWheelZoomEnabled || @event is not InputEventMouseButton mouseButton)
        {
            return;
        }

        if (!mouseButton.Pressed)
        {
            return;
        }

        if (mouseButton.ButtonIndex == MouseButton.WheelUp)
        {
            SetZoomScalar(GetZoomScalar() + ZoomStep);
            GetViewport().SetInputAsHandled();
        }
        else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
        {
            SetZoomScalar(GetZoomScalar() - ZoomStep);
            GetViewport().SetInputAsHandled();
        }
    }

    private void ApplyViewportSizeConstraintsIfChanged()
    {
        Vector2 viewportSize = GetViewportRect().Size;

        if (_hasViewportSize && _lastViewportSize == viewportSize)
        {
            return;
        }

        _lastViewportSize = viewportSize;
        _hasViewportSize = true;
        SetZoomScalar(GetZoomScalar());
    }

    private void OnBattleMapLoaded(Node activeMap)
    {
        _hasMapBounds = activeMap is BattleMapView battleMapView && TryCalculateMapBounds(battleMapView, out _mapBounds);

        if (!_hasMapBounds)
        {
            GD.PushWarning("BattleCameraController could not calculate battle map bounds.");
            return;
        }

        SetZoomScalar(GetZoomScalar());
        ClampToMapBounds();
    }

    private Vector2 GetMoveDirection()
    {
        Vector2 direction = Vector2.Zero;

        if (_moveUpPressed || Input.IsKeyPressed(Key.W))
        {
            direction.Y -= 1f;
        }

        if (_moveDownPressed || Input.IsKeyPressed(Key.S))
        {
            direction.Y += 1f;
        }

        if (_moveLeftPressed || Input.IsKeyPressed(Key.A))
        {
            direction.X -= 1f;
        }

        if (_moveRightPressed || Input.IsKeyPressed(Key.D))
        {
            direction.X += 1f;
        }

        return direction;
    }

    private static bool IsMoveKey(InputEventKey keyEvent, Key key)
    {
        return keyEvent.Keycode == key || keyEvent.PhysicalKeycode == key;
    }

    private void SetZoomScalar(float zoomScalar)
    {
        float clampedZoom = Mathf.Clamp(
            zoomScalar,
            GetEffectiveMinZoom(),
            Mathf.Max(MaxZoom, GetEffectiveMinZoom()));

        Zoom = new Vector2(clampedZoom, clampedZoom);
        ClampToMapBounds();
    }

    private float GetZoomScalar()
    {
        return Mathf.Max(Zoom.X, 0.001f);
    }

    private float GetEffectiveMoveSpeed()
    {
        float zoomScalar = GetZoomScalar();
        float minZoom = Mathf.Max(GetEffectiveMinZoom(), 0.001f);
        float zoomRatio = Mathf.Max(zoomScalar / minZoom, 1f);
        float maxMultiplier = Mathf.Max(MaxMoveSpeedZoomMultiplier, 1f);
        float zoomMultiplier = Mathf.Clamp(Mathf.Pow(zoomRatio, MoveSpeedZoomExponent), 1f, maxMultiplier);

        return MoveSpeed * zoomMultiplier / zoomScalar;
    }

    private float GetEffectiveMinZoom()
    {
        if (!_hasMapBounds || _mapBounds.Size.X <= 0f || _mapBounds.Size.Y <= 0f)
        {
            return MinZoom;
        }

        Vector2 viewportSize = GetViewportRect().Size;
        float requiredZoomX = viewportSize.X / _mapBounds.Size.X;
        float requiredZoomY = viewportSize.Y / _mapBounds.Size.Y;

        return Mathf.Max(MinZoom, Mathf.Max(requiredZoomX, requiredZoomY));
    }

    private void ClampToMapBounds()
    {
        if (!_hasMapBounds)
        {
            return;
        }

        Vector2 halfVisibleSize = GetVisibleWorldSize() * 0.5f;
        float minX = _mapBounds.Position.X + halfVisibleSize.X;
        float maxX = _mapBounds.End.X - halfVisibleSize.X;
        float minY = _mapBounds.Position.Y + halfVisibleSize.Y;
        float maxY = _mapBounds.End.Y - halfVisibleSize.Y;

        GlobalPosition = new Vector2(
            ClampAxis(GlobalPosition.X, minX, maxX, _mapBounds.GetCenter().X),
            ClampAxis(GlobalPosition.Y, minY, maxY, _mapBounds.GetCenter().Y));
    }

    private Vector2 GetVisibleWorldSize()
    {
        Vector2 viewportSize = GetViewportRect().Size;
        Vector2 zoom = Zoom;

        return new Vector2(
            viewportSize.X / Mathf.Max(zoom.X, 0.001f),
            viewportSize.Y / Mathf.Max(zoom.Y, 0.001f));
    }

    private static float ClampAxis(float value, float min, float max, float fallback)
    {
        return min > max ? fallback : Mathf.Clamp(value, min, max);
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
