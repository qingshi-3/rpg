using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle.Navigation;

internal readonly record struct BattleCombatSlot(
    BattleGridCoord Anchor,
    BattleCombatSlotKind Kind,
    int Gap,
    int Priority,
    LocalCombatSupportSlotRole SupportRole = LocalCombatSupportSlotRole.None);
