using System.Collections.Generic;
using Rpg.Application.Battle.Navigation;

namespace Rpg.Application.Battle.Snapshots;

public sealed class LocationBattleContext
{
    public string LocationId { get; set; } = "";
    public List<string> ActiveTags { get; set; } = new();
    public List<BattleNavigationSurfaceSnapshot> NavigationSurfaces { get; set; } = new();
    public List<BattleNavigationConnectionSnapshot> NavigationConnections { get; set; } = new();
    public BattleNavigationTopology NavigationTopology { get; set; } = new();
}
