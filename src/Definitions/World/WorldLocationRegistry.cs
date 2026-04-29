using Godot;

namespace Rpg.Definitions.World;

[GlobalClass]
public partial class WorldLocationRegistry : Resource
{
    [Export]
    public Godot.Collections.Array<WorldLocationDefinition> Locations { get; set; } = new();
}

