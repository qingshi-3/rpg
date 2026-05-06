namespace Rpg.Domain.Battle.Grid;

public readonly record struct GridSurfaceConnection(
    GridSurfacePosition Target,
    int MoveCost,
    string ConnectionId);
