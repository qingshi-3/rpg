using Godot;

namespace Rpg.Presentation.Battle.Entities;

public partial class ActionPointComponent : BattleEntityComponent
{
    [Export]
    public int MaxAp { get; set; } = 0;

    [Export]
    public int Ap { get; set; } = 0;

    public bool CanSpend(int cost)
    {
        return cost <= 0 || Ap >= cost;
    }

    public bool TrySpend(int cost)
    {
        if (!CanSpend(cost))
        {
            return false;
        }

        Ap = System.Math.Max(0, Ap - System.Math.Max(0, cost));
        return true;
    }

    public void RestoreToMax()
    {
        Ap = MaxAp;
    }
}
