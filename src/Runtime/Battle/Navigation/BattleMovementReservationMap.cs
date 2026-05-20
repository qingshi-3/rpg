using System.Collections.Generic;

namespace Rpg.Runtime.Battle.Navigation;

internal sealed class BattleMovementReservationMap
{
    private static readonly IReadOnlySet<BattleGridCoord> EmptyReleasedCells = new HashSet<BattleGridCoord>();
    private readonly HashSet<BattleGridCoord> _reservedCells = new();
    private readonly HashSet<BattleMoveEdge> _reservedEdges = new();

    public bool CanReserveFootprint(BattleRuntimeActor actor, BattleGridCoord anchor, BattleDynamicOccupancy occupancy)
    {
        return CanReserveFootprint(actor, anchor, occupancy, EmptyReleasedCells);
    }

    public bool CanReserveFootprint(
        BattleRuntimeActor actor,
        BattleGridCoord anchor,
        BattleDynamicOccupancy occupancy,
        IReadOnlySet<BattleGridCoord> releasedCells)
    {
        foreach (BattleGridCoord cell in BattleActorFootprint.Enumerate(actor, anchor))
        {
            if (_reservedCells.Contains(cell) ||
                occupancy?.IsOccupiedByOther(actor, cell) == true && releasedCells?.Contains(cell) != true)
            {
                return false;
            }
        }

        return true;
    }

    public bool CanReserveMove(
        BattleRuntimeActor actor,
        BattleGridCoord from,
        BattleGridCoord to,
        BattleDynamicOccupancy occupancy)
    {
        if (actor == null || from == to)
        {
            return false;
        }

        if (_reservedEdges.Contains(new BattleMoveEdge(to, from)))
        {
            return false;
        }

        return CanReserveFootprint(actor, to, occupancy);
    }

    public bool CanReserveMoveAllowingReleasedCells(
        BattleRuntimeActor actor,
        BattleGridCoord from,
        BattleGridCoord to,
        BattleDynamicOccupancy occupancy,
        IReadOnlySet<BattleGridCoord> releasedCells)
    {
        if (actor == null || from == to)
        {
            return false;
        }

        if (_reservedEdges.Contains(new BattleMoveEdge(to, from)))
        {
            return false;
        }

        return CanReserveFootprint(actor, to, occupancy, releasedCells);
    }

    public bool TryReserveMove(
        BattleRuntimeActor actor,
        BattleGridCoord from,
        BattleGridCoord to,
        BattleDynamicOccupancy occupancy)
    {
        if (!CanReserveMove(actor, from, to, occupancy))
        {
            return false;
        }

        foreach (BattleGridCoord cell in BattleActorFootprint.Enumerate(actor, to))
        {
            _reservedCells.Add(cell);
        }

        _reservedEdges.Add(new BattleMoveEdge(from, to));
        return true;
    }

    public bool TryReserveMoveAllowingReleasedCells(
        BattleRuntimeActor actor,
        BattleGridCoord from,
        BattleGridCoord to,
        BattleDynamicOccupancy occupancy,
        IReadOnlySet<BattleGridCoord> releasedCells)
    {
        if (!CanReserveMoveAllowingReleasedCells(actor, from, to, occupancy, releasedCells))
        {
            return false;
        }

        foreach (BattleGridCoord cell in BattleActorFootprint.Enumerate(actor, to))
        {
            _reservedCells.Add(cell);
        }

        _reservedEdges.Add(new BattleMoveEdge(from, to));
        return true;
    }

    private readonly record struct BattleMoveEdge(BattleGridCoord From, BattleGridCoord To);
}
