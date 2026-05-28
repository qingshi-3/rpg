using System;
using System.Collections.Generic;
using Godot;
using Rpg.Infrastructure.Logging;

namespace Rpg.Presentation.Battle.Entities;

public partial class BattleEntity : Node2D
{
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
        RegisterComponents();
        GameLog.Trace(nameof(BattleEntity), $"Ready id={EntityId} name={DisplayName} components={_components.Count}");
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
