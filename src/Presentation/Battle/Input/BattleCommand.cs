using Rpg.Domain.Battle.Grid;

namespace Rpg.Presentation.Battle.InputSystem;

public readonly record struct BattleCommand(
    BattleCommandKind Kind,
    GridPosition? GridPosition,
    string CommandId)
{
    public static BattleCommand GridCellClicked(GridPosition position)
    {
        return new BattleCommand(BattleCommandKind.GridCellClicked, position, "");
    }

    public static BattleCommand HudCommandSelected(string commandId)
    {
        return new BattleCommand(BattleCommandKind.HudCommandSelected, null, commandId);
    }

    public static BattleCommand HudCommandCancelled()
    {
        return new BattleCommand(BattleCommandKind.HudCommandCancelled, null, "");
    }
}
