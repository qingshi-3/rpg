using System.Collections.Generic;

namespace Rpg.Runtime.Battle;

public sealed class BattleRuntimeState
{
    public string SnapshotId { get; set; } = "";
    public string BattleId { get; set; } = "";
    public List<BattleRuntimeActor> Actors { get; set; } = new();
}
