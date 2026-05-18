using System.Collections.Generic;
using System.Linq;
using Rpg.Domain.Battle.Grid;

namespace Rpg.Application.Battle.Snapshots;

public static class BattleNavigationSnapshotBuilder
{
    public static void ApplyToRequest(BattleStartRequest request, BattleGridMap gridMap)
    {
        if (request == null)
        {
            return;
        }

        request.NavigationSurfaces.Clear();
        request.NavigationConnections.Clear();

        if (gridMap == null)
        {
            return;
        }

        Build(gridMap, request.NavigationSurfaces, request.NavigationConnections);
    }

    public static void CopyRequestToLocationContext(BattleStartRequest request, LocationBattleContext context)
    {
        if (context == null)
        {
            return;
        }

        context.NavigationSurfaces.Clear();
        context.NavigationConnections.Clear();

        if (request == null)
        {
            return;
        }

        foreach (BattleNavigationSurfaceSnapshot surface in request.NavigationSurfaces ?? Enumerable.Empty<BattleNavigationSurfaceSnapshot>())
        {
            if (surface == null)
            {
                continue;
            }

            context.NavigationSurfaces.Add(new BattleNavigationSurfaceSnapshot
            {
                X = surface.X,
                Y = surface.Y,
                Height = surface.Height,
                MoveCost = System.Math.Max(1, surface.MoveCost)
            });
        }

        foreach (BattleNavigationConnectionSnapshot connection in request.NavigationConnections ?? Enumerable.Empty<BattleNavigationConnectionSnapshot>())
        {
            if (connection == null)
            {
                continue;
            }

            context.NavigationConnections.Add(new BattleNavigationConnectionSnapshot
            {
                FromX = connection.FromX,
                FromY = connection.FromY,
                FromHeight = connection.FromHeight,
                ToX = connection.ToX,
                ToY = connection.ToY,
                ToHeight = connection.ToHeight,
                MoveCost = System.Math.Max(1, connection.MoveCost)
            });
        }
    }

    private static void Build(
        BattleGridMap gridMap,
        List<BattleNavigationSurfaceSnapshot> surfaces,
        List<BattleNavigationConnectionSnapshot> connections)
    {
        GridSurfacePosition[] included = gridMap.Surfaces.Values
            .Where(surface => surface.HasFoundation &&
                              surface.IsWalkable &&
                              surface.MoveCost > 0 &&
                              gridMap.IsTopSurface(surface.SurfacePosition))
            .OrderBy(surface => surface.SurfacePosition.Height)
            .ThenBy(surface => surface.SurfacePosition.Y)
            .ThenBy(surface => surface.SurfacePosition.X)
            .Select(surface =>
            {
                surfaces.Add(new BattleNavigationSurfaceSnapshot
                {
                    X = surface.SurfacePosition.X,
                    Y = surface.SurfacePosition.Y,
                    Height = surface.SurfacePosition.Height,
                    MoveCost = System.Math.Max(1, surface.MoveCost)
                });
                return surface.SurfacePosition;
            })
            .ToArray();

        var includedSet = new HashSet<GridSurfacePosition>(included);
        foreach (GridSurfacePosition from in included)
        {
            foreach (GridSurfaceConnection edge in gridMap.GetSurfaceConnections(from))
            {
                if (!includedSet.Contains(edge.Target))
                {
                    continue;
                }

                // Height changes are authored as explicit map connections. Runtime
                // receives only pure cells and never queries the Godot map scene.
                connections.Add(new BattleNavigationConnectionSnapshot
                {
                    FromX = from.X,
                    FromY = from.Y,
                    FromHeight = from.Height,
                    ToX = edge.Target.X,
                    ToY = edge.Target.Y,
                    ToHeight = edge.Target.Height,
                    MoveCost = System.Math.Max(1, edge.MoveCost)
                });
            }
        }
    }
}
