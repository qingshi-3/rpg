namespace Rpg.Runtime.Battle.Navigation;

internal static class BattlePathStepRules
{
    public static bool CanUseAnchor(
        BattleRuntimeActor actor,
        BattleGridCoord start,
        BattleGridCoord current,
        BattleGridCoord neighbor,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattleMovementReservationMap reservations)
    {
        if (!CanUseStaticStep(actor, current, neighbor, graph))
        {
            return false;
        }

        // Corner cutting is a static topology rule, so every diagonal in the
        // projected route must keep both orthogonal side anchors legal. Only
        // the immediate committed step should also pay the dynamic reservation
        // check, because future occupancy is allowed to change before arrival.
        return current == start
            ? reservations.CanReserveMove(actor, start, neighbor, occupancy)
            : true;
    }

    public static bool CanUseStaticStep(
        BattleRuntimeActor actor,
        BattleGridCoord current,
        BattleGridCoord neighbor,
        BattleNavigationGraph graph)
    {
        if (graph == null || !graph.CanTraverseStep(actor, current, neighbor))
        {
            return false;
        }

        return !IsDiagonalStep(current, neighbor) ||
               HasLegalDiagonalSides(actor, current, neighbor, graph);
    }

    private static bool IsDiagonalStep(BattleGridCoord from, BattleGridCoord to)
    {
        return from.X != to.X && from.Y != to.Y;
    }

    private static bool HasLegalDiagonalSides(
        BattleRuntimeActor actor,
        BattleGridCoord from,
        BattleGridCoord to,
        BattleNavigationGraph graph)
    {
        BattleGridCoord horizontalSide = new(to.X, from.Y, from.Height);
        BattleGridCoord verticalSide = new(from.X, to.Y, from.Height);
        return graph.CanPlaceFootprint(actor, horizontalSide) &&
               graph.CanPlaceFootprint(actor, verticalSide);
    }
}
