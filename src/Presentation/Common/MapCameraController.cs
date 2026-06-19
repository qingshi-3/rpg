using Godot;
using Rpg.Infrastructure.Logging;

namespace Rpg.Presentation.Common;

public partial class MapCameraController : Camera2D
{
    private const string CameraMoveLeftAction = "camera_move_left";
    private const string CameraMoveRightAction = "camera_move_right";
    private const string CameraMoveUpAction = "camera_move_up";
    private const string CameraMoveDownAction = "camera_move_down";

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

    [ExportGroup("Map Bounds")]

    [Export]
    public bool UseConfiguredMapBoundsFallback { get; set; }

    [Export]
    public Vector2 ConfiguredMapBoundsPosition { get; set; } = Vector2.Zero;

    [Export]
    public Vector2 ConfiguredMapBoundsSize { get; set; } = Vector2.Zero;

    [ExportGroup("Mouse Drag")]

    [Export]
    public bool MiddleMouseDragPanEnabled { get; set; } = true;

    [ExportGroup("Rendering")]

    [Export]
    public bool UseViewportCamera { get; set; } = true;

    private Rect2 _mapBounds;
    private bool _hasMapBounds;
    private bool _usingConfiguredMapBoundsFallback;
    private Vector2 _lastViewportSize;
    private bool _hasViewportSize;
    private Vector2 _viewportSizeOverride;
    private bool _hasViewportSizeOverride;
    private bool _moveUpPressed;
    private bool _moveDownPressed;
    private bool _moveLeftPressed;
    private bool _moveRightPressed;
    private bool _isMiddleMouseDragging;
    private bool _suppressPolledKeyboardUntilRelease;

    public bool HasMapBounds => _hasMapBounds;
    public bool IsPointerNavigationActive => _isMiddleMouseDragging;
    public bool IsUserNavigationActive => IsPointerNavigationActive || GetMoveDirection() != Vector2.Zero;

    public override void _Ready()
    {
        ResetNavigationInputState("ready");
        Enabled = UseViewportCamera;
        ApplyConfiguredMapBoundsFallback("ready");
    }

    public override void _ExitTree()
    {
        ResetNavigationInputState("exit_tree");
    }

    public override void _Process(double delta)
    {
        if (!CanProcessViewportCameraInput())
        {
            return;
        }

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

    public void ResetNavigationInputState(string reason)
    {
        bool hadInputState = _moveUpPressed ||
                             _moveDownPressed ||
                             _moveLeftPressed ||
                             _moveRightPressed ||
                             _isMiddleMouseDragging ||
                             AnyPolledMoveActionPressed();

        // Scene transitions can miss action or mouse release events. The next scene must
        // start with no camera intent, then re-enable global action polling after release.
        _moveUpPressed = false;
        _moveDownPressed = false;
        _moveLeftPressed = false;
        _moveRightPressed = false;
        _isMiddleMouseDragging = false;
        _suppressPolledKeyboardUntilRelease = true;

        if (hadInputState)
        {
            GameLog.Info("Camera", $"NavigationInputStateReset reason={reason ?? ""}");
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (!CanProcessViewportCameraInput() ||
            !KeyboardMoveEnabled ||
            IsEchoKeyEvent(@event))
        {
            return;
        }

        if (ShouldIgnoreSuppressedMoveActionEvent(@event))
        {
            return;
        }

        if (IsAnyMoveActionEvent(@event))
        {
            _moveUpPressed = IsMoveActionPressed(@event, CameraMoveUpAction, _moveUpPressed);
            _moveDownPressed = IsMoveActionPressed(@event, CameraMoveDownAction, _moveDownPressed);
            _moveLeftPressed = IsMoveActionPressed(@event, CameraMoveLeftAction, _moveLeftPressed);
            _moveRightPressed = IsMoveActionPressed(@event, CameraMoveRightAction, _moveRightPressed);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!CanProcessViewportCameraInput())
        {
            return;
        }

        TryHandlePointerNavigationAndZoomInput(@event);
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

    public bool TryHandlePointerNavigationAndZoomInput(InputEvent @event)
    {
        if (TryHandlePointerNavigationInput(@event))
        {
            return true;
        }

        return @event is InputEventMouseButton mouseButton && HandleMouseWheelInput(mouseButton);
    }

    public void SetMapBounds(Rect2 mapBounds)
    {
        _mapBounds = mapBounds;
        _hasMapBounds = mapBounds.Size.X > 0f && mapBounds.Size.Y > 0f;
        _usingConfiguredMapBoundsFallback = false;
        SetZoomScalar(GetZoomScalar());
        ClampToMapBounds();
    }

    public void ClearMapBounds()
    {
        ClearRuntimeMapBounds();
    }

    public void ClearRuntimeMapBounds()
    {
        _mapBounds = default;
        _hasMapBounds = false;
        _usingConfiguredMapBoundsFallback = false;
        SetZoomScalar(GetZoomScalar());
    }

    public bool ClearMapBoundsAndApplyConfiguredFallback(string reason)
    {
        ClearRuntimeMapBounds();
        return ApplyConfiguredMapBoundsFallback(reason);
    }

    public bool ApplyConfiguredMapBoundsFallback(string reason)
    {
        if (_hasMapBounds && !_usingConfiguredMapBoundsFallback)
        {
            return false;
        }

        if (!UseConfiguredMapBoundsFallback ||
            ConfiguredMapBoundsSize.X <= 0f ||
            ConfiguredMapBoundsSize.Y <= 0f)
        {
            return false;
        }

        // Scene-authored bounds keep camera zoom from revealing unpainted space
        // when runtime map-bound discovery is not available for a viewport slice.
        _mapBounds = new Rect2(ConfiguredMapBoundsPosition, ConfiguredMapBoundsSize);
        _hasMapBounds = true;
        _usingConfiguredMapBoundsFallback = true;
        SetZoomScalar(GetZoomScalar());
        ClampToMapBounds();
        GameLog.Info("Camera", $"ConfiguredMapBoundsFallbackApplied reason={reason} bounds={_mapBounds}");
        return true;
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

    private bool HandleMouseWheelInput(InputEventMouseButton mouseButton)
    {
        if (!MouseWheelZoomEnabled || !mouseButton.Pressed)
        {
            return false;
        }

        if (mouseButton.ButtonIndex == MouseButton.WheelUp)
        {
            SetZoomScalar(GetZoomScalar() + ZoomStep);
            GetViewport().SetInputAsHandled();
            return true;
        }

        if (mouseButton.ButtonIndex == MouseButton.WheelDown)
        {
            SetZoomScalar(GetZoomScalar() - ZoomStep);
            GetViewport().SetInputAsHandled();
            return true;
        }

        return false;
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
        bool allowPolledKeyboard = !ShouldSuppressPolledKeyboard();

        if (_moveUpPressed)
        {
            direction.Y -= 1f;
        }

        if (_moveDownPressed)
        {
            direction.Y += 1f;
        }

        if (_moveLeftPressed)
        {
            direction.X -= 1f;
        }

        if (_moveRightPressed)
        {
            direction.X += 1f;
        }

        if (allowPolledKeyboard)
        {
            direction += Input.GetVector(CameraMoveLeftAction, CameraMoveRightAction, CameraMoveUpAction, CameraMoveDownAction);
        }

        return direction;
    }

    private bool ShouldSuppressPolledKeyboard()
    {
        if (!_suppressPolledKeyboardUntilRelease)
        {
            return false;
        }

        if (AnyPolledMoveActionPressed())
        {
            return true;
        }

        _suppressPolledKeyboardUntilRelease = false;
        return false;
    }

    private bool ShouldIgnoreSuppressedMoveActionEvent(InputEvent @event)
    {
        if (!_suppressPolledKeyboardUntilRelease || !IsAnyMoveActionEvent(@event))
        {
            return false;
        }

        if (IsMoveActionPressed(@event))
        {
            // Scene swaps may deliver a queued press after reset; accepting it could
            // recreate a stuck event-backed camera pan before the matching release.
            GameLog.Info("Camera", "SuppressedMoveActionPressAfterReset");
            return true;
        }

        if (!AnyPolledMoveActionPressed())
        {
            _suppressPolledKeyboardUntilRelease = false;
        }

        return false;
    }

    private bool CanProcessViewportCameraInput()
    {
        // Strategic world uses this node as an explicit camera-state object while
        // rendering through a SubViewport transform; it must not consume global UI input.
        return UseViewportCamera && Enabled;
    }

    private static bool AnyPolledMoveActionPressed()
    {
        return Input.IsActionPressed(CameraMoveUpAction) ||
               Input.IsActionPressed(CameraMoveDownAction) ||
               Input.IsActionPressed(CameraMoveLeftAction) ||
               Input.IsActionPressed(CameraMoveRightAction);
    }

    private static bool IsAnyMoveActionEvent(InputEvent @event)
    {
        return @event.IsActionPressed(CameraMoveUpAction) ||
               @event.IsActionReleased(CameraMoveUpAction) ||
               @event.IsActionPressed(CameraMoveDownAction) ||
               @event.IsActionReleased(CameraMoveDownAction) ||
               @event.IsActionPressed(CameraMoveLeftAction) ||
               @event.IsActionReleased(CameraMoveLeftAction) ||
               @event.IsActionPressed(CameraMoveRightAction) ||
               @event.IsActionReleased(CameraMoveRightAction);
    }

    private static bool IsMoveActionPressed(InputEvent @event)
    {
        return @event.IsActionPressed(CameraMoveUpAction) ||
               @event.IsActionPressed(CameraMoveDownAction) ||
               @event.IsActionPressed(CameraMoveLeftAction) ||
               @event.IsActionPressed(CameraMoveRightAction);
    }

    private static bool IsMoveActionPressed(InputEvent @event, string action, bool currentValue)
    {
        if (@event.IsActionPressed(action))
        {
            return true;
        }

        return @event.IsActionReleased(action) ? false : currentValue;
    }

    private static bool IsEchoKeyEvent(InputEvent @event)
    {
        return @event is InputEventKey { Echo: true };
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
