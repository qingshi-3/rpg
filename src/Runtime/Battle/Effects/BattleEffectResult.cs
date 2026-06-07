using System.Collections.Generic;
using Rpg.Runtime.Battle.Events;

namespace Rpg.Runtime.Battle.Effects;

internal sealed class BattleEffectResult
{
    public List<BattleEvent> Events { get; set; } = new();
    public int AppliedAmount { get; set; }
    public bool TargetDefeated { get; set; }
}
