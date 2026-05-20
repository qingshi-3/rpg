namespace Rpg.Runtime.Battle.Navigation;

internal static class BattlePathCostPolicy
{
    public const int StepCost = 10;
    public const int DiagonalStepCost = 14;
    private const int FutureOccupiedCellSoftCost = StepCost * 2;

    public static int GetTraversalCost(
        BattleRuntimeActor actor,
        BattleGridCoord start,
        BattleGridCoord current,
        BattleGridCoord neighbor,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy)
    {
        // Terrain remains uniform in this slice; diagonal geometry is still
        // longer than an orthogonal cell step so A* does not prefer side dips
        // when a straight corridor step reaches the same combat state.
        int stepCost = IsDiagonalStep(current, neighbor)
            ? DiagonalStepCost
            : StepCost;
        return graph.GetStepCost(current, neighbor, stepCost) +
               GetProjectedDynamicCost(actor, start, current, neighbor, occupancy);
    }

    public static int EstimateToAttackRange(
        BattleRuntimeActor actor,
        BattleGridCoord anchor,
        BattleRuntimeActor target,
        int attackRange)
    {
        int gap = BattleActorFootprint.GetGap(actor, anchor, target, new BattleGridCoord(target.GridX, target.GridY, target.GridHeight));
        return System.Math.Max(0, gap - attackRange) * StepCost;
    }

    private static int GetProjectedDynamicCost(
        BattleRuntimeActor actor,
        BattleGridCoord start,
        BattleGridCoord current,
        BattleGridCoord neighbor,
        BattleDynamicOccupancy occupancy)
    {
        if (current == start)
        {
            return 0;
        }

        return System.Math.Max(0, occupancy?.CountOtherOccupiedCells(actor, neighbor) ?? 0) *
               FutureOccupiedCellSoftCost;
    }

    private static bool IsDiagonalStep(BattleGridCoord from, BattleGridCoord to)
    {
        return from.X != to.X && from.Y != to.Y;
    }
}
