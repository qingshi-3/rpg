using Godot;

namespace Rpg.Definitions.World;

public sealed class OpportunitySpawnPointDefinition
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public Vector2 MapPosition { get; set; }
    public float Radius { get; set; } = 64.0f;
}
