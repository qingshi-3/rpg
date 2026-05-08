using Godot;

namespace Rpg.Definitions.World;

[GlobalClass]
public partial class WorldSiteInitialStateResource : Resource
{
    [Export]
    public string SiteId { get; set; } = "";

    [Export]
    public Godot.Collections.Array<WorldInitialGarrisonEntryResource> InitialGarrison { get; set; } = new();
}
