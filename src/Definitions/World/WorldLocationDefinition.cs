using Godot;

namespace Rpg.Definitions.World;

[GlobalClass]
public partial class WorldLocationDefinition : Resource
{
    [Export]
    public string Id { get; set; } = string.Empty;

    [Export]
    public WorldLocationType LocationType { get; set; } = WorldLocationType.Town;

    [Export]
    public PackedScene LocationScene { get; set; }

    [Export]
    public string DefaultSpawnId { get; set; } = "default";
}

