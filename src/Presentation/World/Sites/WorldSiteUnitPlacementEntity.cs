using System;
using Godot;

namespace Rpg.Presentation.World.Sites;

public partial class WorldSiteUnitPlacementEntity : Area2D
{
    private static readonly Color InvalidMarkerColor = new(1.0f, 0.05f, 0.02f, 0.98f);
    private static readonly Color InvalidGlowColor = new(1.0f, 0.0f, 0.0f, 0.28f);

    private bool _isDragging;
    private bool _canPlace = true;

    [Export]
    public string PlacementId { get; set; } = "";

    [Export]
    public string DisplayName { get; set; } = "驻军";

    [Export]
    public Color MarkerColor { get; set; } = new(0.38f, 0.78f, 0.46f, 0.95f);

    [Export]
    public bool CanDrag { get; set; } = true;

    public event Action<string> Pressed;

    public override void _Ready()
    {
        InputPickable = true;
    }

    public override void _Draw()
    {
        Color activeColor = _isDragging && !_canPlace ? InvalidMarkerColor : MarkerColor;
        Color outlineColor = _isDragging && !_canPlace ? Colors.Red : Colors.White;
        float outlineWidth = _isDragging && !_canPlace ? 3.0f : 1.25f;

        if (_isDragging && !_canPlace)
        {
            DrawCircle(Vector2.Zero, 18f, InvalidGlowColor);
        }

        DrawCircle(Vector2.Zero, 13f, new Color(0f, 0f, 0f, 0.45f));
        DrawCircle(Vector2.Zero, 9f, activeColor);
        DrawArc(Vector2.Zero, 15f, 0f, Mathf.Tau, 32, outlineColor, outlineWidth, true);
    }

    public override void _InputEvent(Viewport viewport, InputEvent @event, int shapeIdx)
    {
        if (!CanDrag ||
            @event is not InputEventMouseButton mouseButton ||
            !mouseButton.Pressed ||
            mouseButton.ButtonIndex != MouseButton.Left)
        {
            return;
        }

        Pressed?.Invoke(PlacementId);
        GetViewport().SetInputAsHandled();
    }

    public void SetPlacementPreviewState(bool isDragging, bool canPlace)
    {
        if (_isDragging == isDragging && _canPlace == canPlace)
        {
            return;
        }

        _isDragging = isDragging;
        _canPlace = canPlace;
        QueueRedraw();
    }

    public void BindPlacement(string placementId, string displayName)
    {
        PlacementId = placementId ?? "";
        DisplayName = displayName ?? "";
        Name = string.IsNullOrWhiteSpace(PlacementId)
            ? "SiteUnitPlacement"
            : $"{PlacementId.Replace(':', '_')}Entity";
        QueueRedraw();
    }
}
