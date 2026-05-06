using Godot;

namespace Rpg.Presentation.Battle.Entities;

public partial class MovementComponent : BattleEntityComponent
{
    [Export]
    public int MoveRange { get; set; } = 4;

    [Export]
    public int ApCost { get; set; } = 1;

    [ExportGroup("移动次数")]

    [Export]
    public int MaxMoveUsesPerTurn { get; set; } = 1;

    [Export]
    public int MoveUsesRemaining { get; set; } = 1;

    [ExportGroup("地形")]

    [Export]
    public bool CanEnterWater { get; set; }

    public bool CanUseMove()
    {
        return MoveUsesRemaining > 0;
    }

    public bool TryUseMove()
    {
        if (!CanUseMove())
        {
            return false;
        }

        MoveUsesRemaining = System.Math.Max(0, MoveUsesRemaining - 1);
        return true;
    }

    public void RestoreMoveUses()
    {
        MoveUsesRemaining = System.Math.Max(0, MaxMoveUsesPerTurn);
    }

    public void AddTemporaryMoveUses(int amount)
    {
        MoveUsesRemaining = System.Math.Max(0, MoveUsesRemaining + System.Math.Max(0, amount));
    }
}
