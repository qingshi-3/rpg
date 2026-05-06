using System.Collections.Generic;
using Godot;

namespace Rpg.Application.World;

public sealed class StrategicNavigationPath
{
    public string ProviderId { get; set; } = "";
    public List<Vector2> Points { get; set; } = new();
}
