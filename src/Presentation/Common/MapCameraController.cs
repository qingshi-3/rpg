using Godot;
using Rpg.Infrastructure.Logging;

namespace Rpg.Presentation.Common;

public partial class MapCameraController : Camera2D
{
    [ExportGroup("Movement")]

    [Export]
    public bool KeyboardMoveEnabled { get; set; } = true;

    [Export]
    public float MoveSpeed { get; set; } = 560f;

    [Export]
    public float MoveSpeedZoomExponent { get; set; } = 0.55f;

    [Export]
    public float MaxMoveSpeedZoomMultiplier { get; set; } = 2f;

    [ExportGroup("Zoom")]

    [Export]
    public bool MouseWheelZoomEnabled { get; set; } = true;

    [Export]
    public float MinZoom { get; set; } = 1.25f;

    [Export]
    public float MaxZoom { get; set; } = 4f;

    [Export]
    public float ZoomStep { get; set; } = 0.18f;

    [ExportGroup("Mouse Drag")]

    [Export]
    public bool MiddleMouseDragPanEnabled { get; set; } = true;

    [ExportGroup("Rendering")]

    [Export]
    public bool UseViewportCamera { get; set; } = true;

    private Rect2 _mapBounds;
    private bool _hasMapBounds;
    private Vector2 _lastViewportSize;
    private bool _hasViewportSize;
    private Vector2 _viewportSizeOverride;
    private bool _hasViewportSizeOverride;
    private bool _moveUpPressed;
    private bool _moveDownPressed;
    private bool _moveLeftPressed;
    private bool _moveRightPressed;
    private bool _isMiddleMouseDragging;

    public bool HasMapBounds => _hasMapBounds;
    public bool IsUserNavigationActive => _isMiddleMouseDragging || GetMoveDirection() != Vector2.Zero;

    public override void _Ready()
    {
        Enabled = UseViewportCamera;
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
        if (TryHandlePointerNavigationInput(@event))
        {
            return;
        }

        if (@event is InputEventMouseButton mouseButton)
        {
            HandleMouseWheelInput(mouseButton);
        }
    }

    public bool TryHandlePointerNavigationInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Middle } mouseButton)
        {
            return HandleMiddleMouseDragButton(mouseButton);
        }

        if (@event is InputEventMouseMotion mouseMotion)
        {
            return HandleMouseMotionInput(mouseMotion);
        }

        return false;
    }

    public void SetMapBounds(Rect2 mapBounds)
    {
        _mapBounds = mapBounds;
        _hasMapBounds = mapBounds.Size.X > 0f && mapBounds.Size.Y > 0f;
        SetZoomScalar(GetZoomScalar());
        ClampToMapBounds();
    }

    public void ClearMapBounds()
    {
        _mapBounds = default;
        _hasMapBounds = false;
    }

    public void SetViewportSizeOverride(Vector2 viewportSize)
    {
        _viewportSizeOverride = viewportSize;
        _hasViewportSizeOverride = viewportSize.X > 0f && viewportSize.Y > 0f;
        SetZoomScalar(GetZoomScalar());
    }

    public void ClearViewportSizeOverride()
    {
        _viewportSizeOverride = default;
        _hasViewportSizeOverride = false;
        SetZoomScalar(GetZoomScalar());
    }

    public void FocusOn(Vector2 worldPosition)
    {
        GlobalPosition = ResolveClampedFocusPosition(worldPosition);
    }

    public Vector2 ResolveClampedFocusPosition(Vector2 worldPosition)
    {
        if (!float.IsFinite(worldPosition.X) || !float.IsFinite(worldPosition.Y))
        {
            return GlobalPosition;
        }

        return ClampPositionToMapBounds(worldPosition);
    }

    public void SetZoomScalar(float zoomScalar)
    {
        float clampedZoom = Mathf.Clamp(
            zoomScalar,
            GetEffectiveMinZoom(),
            Mathf.Max(MaxZoom, GetEffectiveMinZoom()));

        Zoom = new Vector2(clampedZoom, clampedZoom);
        ClampToMapBounds();
    }

    public float GetZoomScalar()
    {
        return Mathf.Max(Zoom.X, 0.001f);
    }

    private void HandleMouseWheelInput(InputEventMouseButton mouseButton)
    {
        if (!MouseWheelZoomEnabled || !mouseButton.Pressed)
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

    private bool HandleMiddleMouseDragButton(InputEventMouseButton mouseButton)
    {
        if (!MiddleMouseDragPanEnabled)
        {
            return false;
        }

        if (mouseButton.Pressed && !_isMiddleMouseDragging)
        {
            _isMiddleMouseDragging = true;
            GameLog.Info("Camera", $"Middle mouse drag pan started position={GlobalPosition} zoom={GetZoomScalar():0.###}");
        }
        else if (!mouseButton.Pressed && _isMiddleMouseDragging)
        {
            _isMiddleMouseDragging = false;
            GameLog.Info("Camera", $"Middle mouse drag pan ended position={GlobalPosition} zoom={GetZoomScalar():0.###}");
        }

        GetViewport().SetInputAsHandled();
        return true;
    }

    private bool HandleMouseMotionInput(InputEventMouseMotion mouseMotion)
    {
        if (!MiddleMouseDragPanEnabled || !_isMiddleMouseDragging)
        {
            return false;
        }

        GlobalPosition = CalculateMiddleMouseDragPanPosition(GlobalPosition, mouseMotion.Relative, GetZoomScalar());
        ClampToMapBounds();
        GetViewport().SetInputAsHandled();
        return true;
    }

    public static Vector2 CalculateMiddleMouseDragPanPosition(Vector2 currentPosition, Vector2 mouseRelative, float zoomScalar)
    {
        return currentPosition - mouseRelative / Mathf.Max(zoomScalar, 0.001f);
    }

    private void ApplyViewportSizeConstraintsIfChanged()
    {
        Vector2 viewportSize = GetCameraViewportSize();

        if (_hasViewportSize && _lastViewportSize == viewportSize)
        {
            return;
        }

        _lastViewportSize = viewportSize;
        _hasViewportSize = true;
        SetZoomScalar(GetZoomScalar());
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

        Vector2 viewportSize = GetCameraViewportSize();
        float requiredZoomX = viewportSize.X / _mapBounds.Size.X;
        float requiredZoomY = viewportSize.Y / _mapBounds.Size.Y;

        return Mathf.Max(MinZoom, Mathf.Max(requiredZoomX, requiredZoomY));
    }

    private Vector2 GetCameraViewportSize()
    {
        return _hasViewportSizeOverride ? _viewportSizeOverride : GetViewportRect().Size;
    }

    private void ClampToMapBounds()
    {
        GlobalPosition = ClampPositionToMapBounds(GlobalPosition);
    }

    private Vector2 GetVisibleWorldSize()
    {
        Vector2 viewportSize = GetCameraViewportSize();
        Vector2 zoom = Zoom;

        return new Vector2(
            viewportSize.X / Mathf.Max(zoom.X, 0.001f),
            viewportSize.Y / Mathf.Max(zoom.Y, 0.001f));
    }

    private Vector2 ClampPositionToMapBounds(Vector2 position)
    {
        if (!_hasMapBounds)
        {
            return position;
        }

        Vector2 halfVisibleSize = GetVisibleWorldSize() * 0.5f;
        float minX = _mapBounds.Position.X + halfVisibleSize.X;
        float maxX = _mapBounds.End.X - halfVisibleSize.X;
        float minY = _mapBounds.Position.Y + halfVisibleSize.Y;
        float maxY = _mapBounds.End.Y - halfVisibleSize.Y;

        return new Vector2(
            ClampAxis(position.X, minX, maxX, _mapBounds.GetCenter().X),
            ClampAxis(position.Y, minY, maxY, _mapBounds.GetCenter().Y));
    }

    private static float ClampAxis(float value, float min, float max, float fallback)
    {
        return min > max ? fallback : Mathf.Clamp(value, min, max);
    }
}
