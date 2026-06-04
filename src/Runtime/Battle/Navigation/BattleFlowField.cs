using System.Collections.Generic;
using System.Linq;

namespace Rpg.Runtime.Battle.Navigation;

internal sealed class BattleFlowField
{
    private readonly Dictionary<BattleGridCoord, int> _costs;
    private readonly Dictionary<BattleGridCoord, BattleCombatSlot> _bestGoals;

    public BattleFlowField(
        IEnumerable<BattleCombatSlot> goalSlots,
        Dictionary<BattleGridCoord, int> costs,
        Dictionary<BattleGridCoord, BattleCombatSlot> bestGoals = null)
    {
        GoalSlots = (goalSlots ?? Enumerable.Empty<BattleCombatSlot>()).ToArray();
        _costs = costs ?? new Dictionary<BattleGridCoord, int>();
        _bestGoals = bestGoals ?? new Dictionary<BattleGridCoord, BattleCombatSlot>();
    }

    public IReadOnlyList<BattleCombatSlot> GoalSlots { get; }
    public bool HasCosts => _costs.Count > 0;

    public bool TryGetCost(BattleGridCoord anchor, out int cost)
    {
        return _costs.TryGetValue(anchor, out cost);
    }

    public bool TryGetBestGoal(BattleGridCoord anchor, out BattleCombatSlot goal)
    {
        return _bestGoals.TryGetValue(anchor, out goal);
    }
}
