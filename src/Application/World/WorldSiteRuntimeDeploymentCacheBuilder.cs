using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Maps;
using Rpg.Definitions.Maps;
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
        return Build(siteId, gridMap, null);
    }

    public WorldSiteRuntimeDeploymentCache Build(
        string siteId,
        BattleGridMap gridMap,
        IEnumerable<SemanticMapMarkerData> semanticMarkers)
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
                    .Select(BuildDeploymentCell)
                    .ToArray();
        }

        DeploymentZoneCandidateMaps deploymentZones = BuildDeploymentZoneCandidateMaps(gridMap, semanticMarkers);
        return new WorldSiteRuntimeDeploymentCache(
            siteId,
            surfaces.Length,
            candidatesByDirection,
            deploymentZones.ByFaction,
            deploymentZones.BySide);
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

    private static DeploymentZoneCandidateMaps BuildDeploymentZoneCandidateMaps(
        BattleGridMap gridMap,
        IEnumerable<SemanticMapMarkerData> semanticMarkers)
    {
        if (gridMap == null || semanticMarkers == null)
        {
            return new DeploymentZoneCandidateMaps();
        }

        var factionBuckets = new Dictionary<string, Dictionary<GridSurfacePosition, GridCellSurface>>(System.StringComparer.Ordinal);
        var sideBuckets = new Dictionary<SemanticDeploymentSide, Dictionary<GridSurfacePosition, GridCellSurface>>();
        foreach (SemanticMapMarkerData marker in semanticMarkers
                     .Where(marker => marker?.MarkerType == SemanticMapMarkerType.DeploymentZone)
                     .OrderBy(marker => marker.Priority)
                     .ThenBy(marker => marker.MarkerId, System.StringComparer.Ordinal))
        {
            GridCellSurface[] markerSurfaces = marker.CoveredCells
                .Select(cell => TryResolveDeploymentZoneSurface(gridMap, cell, out GridCellSurface surface) ? surface : null)
                .Where(surface => surface != null)
                .ToArray();
            if (markerSurfaces.Length == 0)
            {
                continue;
            }

            SemanticDeploymentSide deploymentSide = NormalizeDeploymentSide(marker.DeploymentSide);
            if (deploymentSide != SemanticDeploymentSide.Any)
            {
                AddDeploymentZoneSurfacesBySide(sideBuckets, deploymentSide, markerSurfaces);
            }
            else if (string.IsNullOrWhiteSpace(marker.FactionId))
            {
                AddDeploymentZoneSurfacesBySide(sideBuckets, SemanticDeploymentSide.Any, markerSurfaces);
            }

            if (!string.IsNullOrWhiteSpace(marker.FactionId))
            {
                AddDeploymentZoneSurfacesByFaction(factionBuckets, marker.FactionId, markerSurfaces);
            }
            else if (deploymentSide == SemanticDeploymentSide.Any)
            {
                AddDeploymentZoneSurfacesByFaction(factionBuckets, "", markerSurfaces);
            }
        }

        return new DeploymentZoneCandidateMaps
        {
            ByFaction = BuildFactionDirectionMaps(factionBuckets),
            BySide = BuildSideDirectionMaps(sideBuckets)
        };
    }

    private static bool TryResolveDeploymentZoneSurface(
        BattleGridMap gridMap,
        Vector2I cell,
        out GridCellSurface surface)
    {
        surface = null;
        return gridMap != null &&
               gridMap.TryGetTopSurface(new GridPosition(cell.X, cell.Y), out surface) &&
               IsDeploymentCandidateSurface(gridMap, surface);
    }

    private static void AddDeploymentZoneSurfacesByFaction(
        Dictionary<string, Dictionary<GridSurfacePosition, GridCellSurface>> buckets,
        string factionId,
        IEnumerable<GridCellSurface> surfaces)
    {
        string factionKey = factionId?.Trim() ?? "";
        if (!buckets.TryGetValue(factionKey, out Dictionary<GridSurfacePosition, GridCellSurface> bucket))
        {
            bucket = new Dictionary<GridSurfacePosition, GridCellSurface>();
            buckets[factionKey] = bucket;
        }

        foreach (GridCellSurface surface in surfaces)
        {
            bucket[surface.SurfacePosition] = surface;
        }
    }

    private static void AddDeploymentZoneSurfacesBySide(
        Dictionary<SemanticDeploymentSide, Dictionary<GridSurfacePosition, GridCellSurface>> buckets,
        SemanticDeploymentSide deploymentSide,
        IEnumerable<GridCellSurface> surfaces)
    {
        SemanticDeploymentSide sideKey = NormalizeDeploymentSide(deploymentSide);
        if (!buckets.TryGetValue(sideKey, out Dictionary<GridSurfacePosition, GridCellSurface> bucket))
        {
            bucket = new Dictionary<GridSurfacePosition, GridCellSurface>();
            buckets[sideKey] = bucket;
        }

        foreach (GridCellSurface surface in surfaces)
        {
            bucket[surface.SurfacePosition] = surface;
        }
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>>> BuildFactionDirectionMaps(
        Dictionary<string, Dictionary<GridSurfacePosition, GridCellSurface>> buckets)
    {
        return buckets.ToDictionary(
            pair => pair.Key,
            pair => BuildDirectionMap(pair.Value.Values),
            System.StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<SemanticDeploymentSide, IReadOnlyDictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>>> BuildSideDirectionMaps(
        Dictionary<SemanticDeploymentSide, Dictionary<GridSurfacePosition, GridCellSurface>> buckets)
    {
        return buckets.ToDictionary(
            pair => pair.Key,
            pair => BuildDirectionMap(pair.Value.Values));
    }

    private static IReadOnlyDictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>> BuildDirectionMap(
        IEnumerable<GridCellSurface> surfaces)
    {
        GridCellSurface[] surfaceArray = surfaces?.ToArray() ?? System.Array.Empty<GridCellSurface>();
        return SupportedDirections.ToDictionary(
            direction => direction,
            direction => (IReadOnlyList<WorldSiteDeploymentCell>)OrderDeploymentSurfaceCandidates(surfaceArray, direction)
                .Select(BuildDeploymentCell)
                .ToArray());
    }

    private static SemanticDeploymentSide NormalizeDeploymentSide(SemanticDeploymentSide deploymentSide)
    {
        return deploymentSide switch
        {
            SemanticDeploymentSide.Player => SemanticDeploymentSide.Player,
            SemanticDeploymentSide.Enemy => SemanticDeploymentSide.Enemy,
            _ => SemanticDeploymentSide.Any
        };
    }

    private static WorldSiteDeploymentCell BuildDeploymentCell(GridCellSurface surface)
    {
        return new WorldSiteDeploymentCell(
            new Vector2I(surface.Position.X, surface.Position.Y),
            surface.Height,
            surface.TerrainTag ?? "",
            BattleGridTerrainQueries.IsWater(surface));
    }

    private sealed class DeploymentZoneCandidateMaps
    {
        public IReadOnlyDictionary<string, IReadOnlyDictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>>> ByFaction { get; init; } =
            new Dictionary<string, IReadOnlyDictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>>>();

        public IReadOnlyDictionary<SemanticDeploymentSide, IReadOnlyDictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>>> BySide { get; init; } =
            new Dictionary<SemanticDeploymentSide, IReadOnlyDictionary<WorldSiteAttackDirection, IReadOnlyList<WorldSiteDeploymentCell>>>();
    }
}
