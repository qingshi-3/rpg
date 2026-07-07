using System.Collections.Generic;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle;

public sealed class BattleRuntimeDestinationBeacon
{
    public string BeaconId { get; set; } = "";
    public string CommandId { get; set; } = "";
    public List<string> OwnerBattleGroupIds { get; } = new();
    public BattleGridCoord Anchor { get; set; }
    public int Revision { get; set; } = 1;
    public bool IsValid { get; set; } = true;
}
