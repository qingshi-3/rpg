using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.Battle.Preview;

public readonly record struct BattleHoverPreviewStyle(
    BattleGridHighlightKind MoveKind,
    BattleGridHighlightKind AttackKind,
    // Hover previews keep post-move attack semantics for both factions; performance is handled by projection/overlay batching.
    bool ProjectAttackFromReachableMoveOrigins)
{
    public static BattleHoverPreviewStyle ForFaction(BattleFaction faction)
    {
        return faction == BattleFaction.Player
            ? new BattleHoverPreviewStyle(BattleGridHighlightKind.FriendlyMove, BattleGridHighlightKind.FriendlyAttack, true)
            : new BattleHoverPreviewStyle(BattleGridHighlightKind.Move, BattleGridHighlightKind.Threat, true);
    }
}
