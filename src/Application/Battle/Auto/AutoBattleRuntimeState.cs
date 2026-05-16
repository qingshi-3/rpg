using System.Collections.Generic;

namespace Rpg.Application.Battle.Auto;

public sealed class AutoBattleRuntimeState
{
    private readonly List<AutoBattleCombatant> _combatants = new();

    public IReadOnlyList<AutoBattleCombatant> Combatants => _combatants;
    public int CurrentTick { get; internal set; }
    public BattleOutcome Outcome { get; internal set; } = BattleOutcome.None;

    internal List<AutoBattleCombatant> MutableCombatants => _combatants;
}
