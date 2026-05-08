using Godot;

namespace Rpg.Definitions.World;

[GlobalClass]
public partial class StrategicWorldInitialStateResource : Resource
{
    [Export]
    public Godot.Collections.Array<WorldSiteInitialStateResource> Sites { get; set; } = new();
}
