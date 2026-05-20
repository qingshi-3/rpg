namespace Rpg.Runtime.Battle.Navigation;

internal readonly record struct BattleCombatSlot(
    BattleGridCoord Anchor,
    BattleCombatSlotKind Kind,
    int Gap,
    int Priority);
