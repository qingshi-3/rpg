using System.Collections.Generic;
using System.Linq;

namespace Rpg.Runtime.Battle.Navigation;

internal sealed class BattleFlowField
{
    private readonly Dictionary<BattleGridCoord, int> _costs;

    public BattleFlowField(
        IEnumerable<BattleCombatSlot> goalSlots,
        Dictionary<BattleGridCoord, int> costs)
    {
        GoalSlots = (goalSlots ?? Enumerable.Empty<BattleCombatSlot>()).ToArray();
        _costs = costs ?? new Dictionary<BattleGridCoord, int>();
    }

    public IReadOnlyList<BattleCombatSlot> GoalSlots { get; }
    public bool HasCosts => _costs.Count > 0;

    public bool TryGetCost(BattleGridCoord anchor, out int cost)
    {
        return _costs.TryGetValue(anchor, out cost);
    }
}
