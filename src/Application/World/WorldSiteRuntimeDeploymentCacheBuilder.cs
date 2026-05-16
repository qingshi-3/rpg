using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;

namespace Rpg.Application.World;

public sealed class WorldSiteRuntimeDeploymentCacheBuilder
{
    public static IReadOnlyList<WorldSiteAttackDirection> SupportedDirections { get; } = new[]
    {
        WorldSiteAttackDirection.Any,
        WorldSiteAttackDirection.North,
        WorldSiteAttackDirection.South,
        WorldSiteAttackDirection.West,
        WorldSiteAttackDirection.East
    };

    public WorldSiteRuntimeDeploymentCache Build(string siteId, BattleGridMap gridMap)
    {
        var candidatesByDirection = new Dictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>>();
        foreach (WorldSiteAttackDirection direction in SupportedDirections)
        {
            candidatesByDirection[direction] = System.Array.Empty<WorldSiteDeploymentCell>();
        }

        if (gridMap == null)
        {
            return new WorldSiteRuntimeDeploymentCache(siteId, candidateSurfaceCount: 0, candidatesByDirection);
        }

        GridCellSurface[] surfaces = gridMap.Surfaces.Values
            .Where(surface => IsDeploymentCandidateSurface(gridMap, surface))
            .ToArray();

        foreach (WorldSiteAttackDirection direction in SupportedDirections)
        {
            candidatesByDirection[direction] = surfaces.Length == 0
                ? System.Array.Empty<WorldSiteDeploymentCell>()
                : OrderDeploymentSurfaceCandidates(surfaces, direction)
                    .Select(surface => new WorldSiteDeploymentCell(
                        new Vector2I(surface.Position.X, surface.Position.Y),
                        surface.Height,
                        surface.TerrainTag ?? "",
                        BattleGridTerrainQueries.IsWater(surface)))
                    .ToArray();
        }

        return new WorldSiteRuntimeDeploymentCache(siteId, surfaces.Length, candidatesByDirection);
    }

    public static bool IsDeploymentCandidateSurface(BattleGridMap gridMap, GridCellSurface surface)
    {
        return gridMap != null &&
               surface is { IsWalkable: true, MoveCost: > 0 } &&
               gridMap.IsTopSurface(surface.SurfacePosition);
    }

    private static IEnumerable<GridCellSurface> OrderDeploymentSurfaceCandidates(
        IReadOnlyCollection<GridCellSurface> candidates,
        WorldSiteAttackDirection direction)
    {
        int minX = candidates.Min(surface => surface.Position.X);
        int maxX = candidates.Max(surface => surface.Position.X);
        int minY = candidates.Min(surface => surface.Position.Y);
        int maxY = candidates.Max(surface => surface.Position.Y);
        float centerX = (minX + maxX) / 2f;
        float centerY = (minY + maxY) / 2f;

        return direction switch
        {
            WorldSiteAttackDirection.North => candidates
                .OrderBy(surface => surface.Position.Y)
                .ThenBy(surface => System.Math.Abs(surface.Position.X - centerX))
                .ThenBy(surface => surface.Position.X)
                .ThenBy(surface => surface.Height),
            WorldSiteAttackDirection.South => candidates
                .OrderByDescending(surface => surface.Position.Y)
                .ThenBy(surface => System.Math.Abs(surface.Position.X - centerX))
                .ThenBy(surface => surface.Position.X)
                .ThenBy(surface => surface.Height),
            WorldSiteAttackDirection.West => candidates
                .OrderBy(surface => surface.Position.X)
                .ThenBy(surface => System.Math.Abs(surface.Position.Y - centerY))
                .ThenBy(surface => surface.Position.Y)
                .ThenBy(surface => surface.Height),
            WorldSiteAttackDirection.East => candidates
                .OrderByDescending(surface => surface.Position.X)
                .ThenBy(surface => System.Math.Abs(surface.Position.Y - centerY))
                .ThenBy(surface => surface.Position.Y)
                .ThenBy(surface => surface.Height),
            _ => candidates
                .OrderBy(surface => System.Math.Abs(surface.Position.X - centerX) + System.Math.Abs(surface.Position.Y - centerY))
                .ThenBy(surface => surface.Position.Y)
                .ThenBy(surface => surface.Position.X)
                .ThenBy(surface => surface.Height)
        };
    }
}
