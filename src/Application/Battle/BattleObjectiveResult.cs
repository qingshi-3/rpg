using System.Collections.Generic;

namespace Rpg.Application.Battle;

public sealed class BattleObjectiveResult
{
    public string ObjectiveId { get; set; } = "";
    public BattleObjectiveState State { get; set; } = BattleObjectiveState.Skipped;
    public int? Score { get; set; }
    public List<string> Tags { get; set; } = new();
}
