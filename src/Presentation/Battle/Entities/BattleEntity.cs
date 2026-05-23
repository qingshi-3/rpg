using System;
using System.Collections.Generic;
using Godot;
using Rpg.Infrastructure.Logging;

namespace Rpg.Presentation.Battle.Entities;

public partial class BattleEntity : Area2D
{
    [Signal]
    public delegate void ClickedEventHandler(BattleEntity entity);

    [Export]
    public string EntityId { get; set; } = "";

    [Export]
    public string DisplayName { get; set; } = "战斗实体";

    [Export]
    public bool DrawDebugMarker { get; set; } = true;

    [Export]
    public Color DebugMarkerColor { get; set; } = new(0.35f, 0.9f, 0.75f, 0.9f);

    private readonly Dictionary<Type, BattleEntityComponent> _components = new();

    public override void _Ready()
    {
        InputPickable = false;
        RegisterComponents();
        GameLog.Trace(nameof(BattleEntity), $"Ready id={EntityId} name={DisplayName} components={_components.Count} inputPickable={InputPickable}");
    }

    public override void _Draw()
    {
        if (!DrawDebugMarker)
        {
            return;
        }

        DrawCircle(Vector2.Zero, 10f, new Color(0f, 0f, 0f, 0.42f));
        DrawCircle(Vector2.Zero, 7f, DebugMarkerColor);
        DrawArc(Vector2.Zero, 12f, 0f, Mathf.Tau, 32, new Color(1f, 1f, 1f, 0.72f), 1.4f, true);
    }

    public override void _InputEvent(Viewport viewport, InputEvent @event, int shapeIdx)
    {
        if (@event is not InputEventMouseButton mouseButton ||
            !mouseButton.Pressed ||
            mouseButton.ButtonIndex != MouseButton.Left)
        {
            return;
        }

        GameLog.Info(nameof(BattleEntity), $"Clicked id={EntityId} name={DisplayName} shape={shapeIdx} global={GlobalPosition}");
        EmitSignal(SignalName.Clicked, this);
        GetViewport().SetInputAsHandled();
    }

    public T GetComponent<T>() where T : BattleEntityComponent
    {
        return _components.TryGetValue(typeof(T), out BattleEntityComponent component)
            ? component as T
            : null;
    }

    public bool HasComponent<T>() where T : BattleEntityComponent
    {
        return _components.ContainsKey(typeof(T));
    }

    public IEnumerable<BattleEntityComponent> GetComponents()
    {
        return _components.Values;
    }

    private void RegisterComponents()
    {
        _components.Clear();

        foreach (Node child in GetChildren())
        {
            if (child is not BattleEntityComponent component)
            {
                continue;
            }

            _components[component.GetType()] = component;
            component.AttachTo(this);
            GameLog.Trace(nameof(BattleEntity), $"Registered component entity={EntityId} component={component.GetType().Name}");
        }
    }
}
