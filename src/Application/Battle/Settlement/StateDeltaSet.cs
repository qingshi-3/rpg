using System.Collections.Generic;

namespace Rpg.Application.Battle.Settlement;

public sealed class StateDeltaSet
{
    public List<string> ChangedHeroIds { get; set; } = new();
    public List<string> ChangedCorpsIds { get; set; } = new();
    public List<string> ChangedBattleGroupIds { get; set; } = new();
    public List<string> ChangedLocationIds { get; set; } = new();
}
