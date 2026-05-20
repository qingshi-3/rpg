using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Navigation;
using Rpg.Domain.Battle.Grid;
using Rpg.Infrastructure.Logging;

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
        request.NavigationTopology = new BattleNavigationTopology();

        if (gridMap == null)
        {
            return;
        }

        Build(gridMap, request.NavigationSurfaces, request.NavigationConnections);
        request.NavigationTopology = BattleNavigationTopologyCompiler.Compile(
            request.NavigationSurfaces,
            request.NavigationConnections);
        GameLog.Info(
            nameof(BattleNavigationSnapshotBuilder),
            BattleNavigationTopologyDiagnostics.DescribeRequestTopology(request, "apply_to_request"));
    }

    public static void CopyRequestToLocationContext(BattleStartRequest request, LocationBattleContext context)
    {
        if (context == null)
        {
            return;
        }

        context.NavigationSurfaces.Clear();
        context.NavigationConnections.Clear();
        context.NavigationTopology = new BattleNavigationTopology();

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
                MoveCost = 1
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
                MoveCost = 1
            });
        }

        context.NavigationTopology = request.NavigationTopology?.HasNodes == true
            ? request.NavigationTopology.Clone()
            : BattleNavigationTopologyCompiler.Compile(context.NavigationSurfaces, context.NavigationConnections);
        GameLog.Info(
            nameof(BattleNavigationSnapshotBuilder),
            BattleNavigationTopologyDiagnostics.DescribeRequestTopology(request, "copy_request_to_context"));
    }

    public static int SyncPreferredPlacementHeightsToCurrentNavigationSurfaces(
        BattleStartRequest request,
        BattleGridMap gridMap,
        Func<BattleForceRequest, bool> canForceEnterWater = null)
    {
        if (request == null || gridMap == null)
        {
            return 0;
        }

        int synced = 0;
        foreach (BattleForceRequest force in EnumerateForces(request))
        {
            bool canEnterWater = canForceEnterWater?.Invoke(force) == true;
            foreach (BattleForcePlacementRequest placement in force?.PreferredPlacements ?? Enumerable.Empty<BattleForcePlacementRequest>())
            {
                if (placement == null)
                {
                    continue;
                }

                var position = new GridPosition(placement.CellX, placement.CellY);
                if (!TryGetCurrentNavigationSurface(gridMap, position, canEnterWater, out GridCellSurface surface))
                {
                    continue;
                }

                // Runtime navigation exports only current top walkable surfaces.
                // A stale bridge height in the launch request would otherwise
                // spawn an actor outside the graph and leave playback running in place.
                if (placement.CellHeight != surface.Height)
                {
                    placement.CellHeight = surface.Height;
                    synced++;
                }
            }
        }

        return synced;
    }

    private static void Build(
        BattleGridMap gridMap,
        List<BattleNavigationSurfaceSnapshot> surfaces,
        List<BattleNavigationConnectionSnapshot> connections)
    {
        GridSurfacePosition[] included = gridMap.Surfaces.Values
            .Where(surface => IsRuntimeNavigationExportSurface(gridMap, surface))
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
                    // Runtime battle traversal is walkable-only. Keep MoveCost on the
                    // snapshot contract for compatibility, but normalize every legal
                    // surface to 1 so static/runtime navigation shares one authority.
                    MoveCost = 1
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
                    MoveCost = 1
                });
            }
        }
    }

    private static IEnumerable<BattleForceRequest> EnumerateForces(BattleStartRequest request)
    {
        foreach (BattleForceRequest force in request?.PlayerForces ?? Enumerable.Empty<BattleForceRequest>())
        {
            yield return force;
        }

        foreach (BattleForceRequest force in request?.EnemyForces ?? Enumerable.Empty<BattleForceRequest>())
        {
            yield return force;
        }
    }

    private static bool TryGetCurrentNavigationSurface(
        BattleGridMap gridMap,
        GridPosition position,
        bool canEnterWater,
        out GridCellSurface surface)
    {
        surface = null;
        if (gridMap == null ||
            !gridMap.TryGetTopSurface(position, out GridCellSurface topSurface) ||
            !IsRuntimeNavigationSurface(gridMap, topSurface) ||
            (!canEnterWater && BattleGridTerrainQueries.IsWater(topSurface)))
        {
            return false;
        }

        surface = topSurface;
        return true;
    }

    private static bool IsRuntimeNavigationSurface(BattleGridMap gridMap, GridCellSurface surface)
    {
        // Runtime topology is the current land battle slice. Negative-height
        // foundations are lower authored layers and must not become fallback
        // graph nodes when the visible land layer is absent or untagged.
        return surface is { HasFoundation: true, IsWalkable: true, MoveCost: > 0 } &&
               surface.Height >= 0 &&
               gridMap?.IsTopSurface(surface.SurfacePosition) == true;
    }

    private static bool IsRuntimeNavigationExportSurface(BattleGridMap gridMap, GridCellSurface surface)
    {
        // The topology data layer is land navigation for this battle slice. Water
        // may sit under authored land as a visual/lower surface, but it must not
        // become a fallback route when the land layer is absent.
        return IsRuntimeNavigationSurface(gridMap, surface) &&
               !BattleGridTerrainQueries.IsWater(surface);
    }
}
