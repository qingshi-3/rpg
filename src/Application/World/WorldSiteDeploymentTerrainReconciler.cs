using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Definitions.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.World;

public sealed class WorldSiteDeploymentTerrainReconcileResult
{
    public bool Success { get; init; } = true;
    public int Relocated { get; init; }
    public int HeightSynced { get; init; }
    public int Invalid { get; init; }
    public string LastFailureReason { get; init; } = "";
}

public sealed class WorldSiteDeploymentTerrainReconciler
{
    private readonly WorldSiteDeploymentService _deploymentService;

    public WorldSiteDeploymentTerrainReconciler(WorldSiteDeploymentService deploymentService = null)
    {
        _deploymentService = deploymentService ?? new WorldSiteDeploymentService();
    }

    public WorldSiteDeploymentTerrainReconcileResult Reconcile(
        BattleGridMap gridMap,
        WorldSiteRuntimeDeploymentCache deploymentCache,
        WorldSiteState site,
        WorldSiteDefinition definition,
        Func<WorldSiteUnitPlacement, bool> canEnterWater)
    {
        if (site == null || definition == null)
        {
            return new WorldSiteDeploymentTerrainReconcileResult
            {
                Success = false,
                Invalid = 1,
                LastFailureReason = "missing_site"
            };
        }

        if (gridMap == null)
        {
            return new WorldSiteDeploymentTerrainReconcileResult();
        }

        bool success = true;
        int relocated = 0;
        int heightSynced = 0;
        int invalid = 0;
        string lastFailureReason = "";

        foreach (WorldSiteUnitPlacement placement in site.UnitPlacements.ToArray())
        {
            bool canUseWater = canEnterWater?.Invoke(placement) == true;
            if (CanUsePlacement(gridMap, placement, canUseWater, out string failureReason))
            {
                if (TrySyncPlacementSurfaceHeight(gridMap, placement))
                {
                    heightSynced++;
                }

                continue;
            }

            if (TryRelocatePlacementForTerrain(
                    gridMap,
                    deploymentCache,
                    site,
                    definition,
                    placement,
                    canUseWater,
                    out failureReason))
            {
                relocated++;
                continue;
            }

            success = false;
            invalid++;
            lastFailureReason = failureReason;
            GameLog.Error(
                nameof(WorldSiteDeploymentTerrainReconciler),
                $"WorldSitePlacementInvalidTerrain site={site.SiteId} placement={placement.PlacementId} unit={placement.UnitTypeId} cell=({placement.CellX},{placement.CellY},h={placement.CellHeight}) terrain={GetPlacementTerrainTag(gridMap, deploymentCache, placement)} canEnterWater={canUseWater} reason={failureReason}");
        }

        if (relocated > 0 || heightSynced > 0)
        {
            GameLog.Info(
                nameof(WorldSiteDeploymentTerrainReconciler),
                $"WorldSitePlacementsTerrainReconciled site={site.SiteId} relocated={relocated} heightSynced={heightSynced}");
        }

        return new WorldSiteDeploymentTerrainReconcileResult
        {
            Success = success,
            Relocated = relocated,
            HeightSynced = heightSynced,
            Invalid = invalid,
            LastFailureReason = lastFailureReason
        };
    }

    public bool CanUsePlacement(
        BattleGridMap gridMap,
        WorldSiteUnitPlacement placement,
        bool canEnterWater,
        out string failureReason)
    {
        failureReason = "";
        if (placement == null)
        {
            failureReason = "missing_placement";
            return false;
        }

        if (gridMap == null)
        {
            return true;
        }

        if (!TryGetPlacementSurface(gridMap, placement, out GridCellSurface surface))
        {
            failureReason = "placement_surface_missing";
            return false;
        }

        if (!WorldSiteRuntimeDeploymentCacheBuilder.IsDeploymentCandidateSurface(gridMap, surface))
        {
            failureReason = "placement_surface_blocked";
            return false;
        }

        if (!canEnterWater && BattleGridTerrainQueries.IsWater(surface))
        {
            failureReason = "placement_surface_water";
            return false;
        }

        return true;
    }

    public bool TryRelocatePlacementForTerrain(
        BattleGridMap gridMap,
        WorldSiteRuntimeDeploymentCache deploymentCache,
        WorldSiteState site,
        WorldSiteDefinition definition,
        WorldSiteUnitPlacement placement,
        bool canEnterWater,
        out string failureReason)
    {
        failureReason = "";
        if (site == null || placement == null || deploymentCache == null)
        {
            failureReason = "deployment_cache_missing";
            return false;
        }

        IReadOnlyList<WorldSiteDeploymentCell> candidates = BuildRelocationCandidates(
            gridMap,
            deploymentCache,
            definition,
            placement,
            canEnterWater);
        foreach (WorldSiteDeploymentCell candidate in candidates)
        {
            if (IsDeploymentCandidateOccupied(site, candidate, placement.PlacementId))
            {
                continue;
            }

            Vector2I oldCell = new(placement.CellX, placement.CellY);
            int oldHeight = placement.CellHeight;
            placement.CellX = candidate.Cell.X;
            placement.CellY = candidate.Cell.Y;
            placement.CellHeight = candidate.Height;
            GameLog.Warn(
                nameof(WorldSiteDeploymentTerrainReconciler),
                $"WorldSitePlacementRelocatedForTerrain site={site.SiteId} placement={placement.PlacementId} unit={placement.UnitTypeId} from=({oldCell.X},{oldCell.Y},h={oldHeight}) to=({placement.CellX},{placement.CellY},h={placement.CellHeight}) terrain={candidate.TerrainTag} isWater={candidate.IsWater}");
            return true;
        }

        failureReason = "non_water_deployment_cell_unavailable";
        return false;
    }

    public string GetPlacementTerrainTag(
        BattleGridMap gridMap,
        WorldSiteRuntimeDeploymentCache deploymentCache,
        WorldSiteUnitPlacement placement)
    {
        if (TryGetPlacementSurface(gridMap, placement, out GridCellSurface surface))
        {
            return surface.TerrainTag ?? "";
        }

        return TryGetDeploymentCellForPlacement(deploymentCache, placement, out WorldSiteDeploymentCell cell)
            ? cell.TerrainTag ?? ""
            : "";
    }

    private static bool TrySyncPlacementSurfaceHeight(BattleGridMap gridMap, WorldSiteUnitPlacement placement)
    {
        if (placement == null || !TryGetPlacementSurface(gridMap, placement, out GridCellSurface surface))
        {
            return false;
        }

        if (placement.CellHeight == surface.Height)
        {
            return false;
        }

        placement.CellHeight = surface.Height;
        return true;
    }

    private IReadOnlyList<WorldSiteDeploymentCell> BuildRelocationCandidates(
        BattleGridMap gridMap,
        WorldSiteRuntimeDeploymentCache deploymentCache,
        WorldSiteDefinition definition,
        WorldSiteUnitPlacement placement,
        bool canEnterWater)
    {
        var result = new List<WorldSiteDeploymentCell>();
        var seen = new HashSet<GridSurfacePosition>();
        AddZoneRelocationCandidates(gridMap, definition, placement, canEnterWater, result, seen);

        WorldSiteAttackDirection direction = placement.AttackDirection == WorldSiteAttackDirection.Any
            ? WorldSiteAttackDirection.Any
            : placement.AttackDirection;
        foreach (WorldSiteDeploymentCell candidate in deploymentCache.GetCandidates(direction))
        {
            if (!CanUseDeploymentCell(candidate, canEnterWater))
            {
                continue;
            }

            var surfacePosition = new GridSurfacePosition(new GridPosition(candidate.Cell.X, candidate.Cell.Y), candidate.Height);
            if (seen.Add(surfacePosition))
            {
                result.Add(candidate);
            }
        }

        return result;
    }

    private void AddZoneRelocationCandidates(
        BattleGridMap gridMap,
        WorldSiteDefinition definition,
        WorldSiteUnitPlacement placement,
        bool canEnterWater,
        List<WorldSiteDeploymentCell> result,
        HashSet<GridSurfacePosition> seen)
    {
        if (definition == null || placement == null || gridMap == null)
        {
            return;
        }

        SiteDeploymentZoneDefinition zone = definition.DeploymentZones.FirstOrDefault(item => item.ZoneId == placement.ZoneId) ??
                                            _deploymentService.GetDefaultGarrisonZone(definition);
        if (zone?.Cells == null || zone.Cells.Count == 0)
        {
            return;
        }

        foreach (Vector2I cell in zone.Cells)
        {
            var position = new GridPosition(cell.X, cell.Y);
            if (!gridMap.TryGetTopSurface(position, out GridCellSurface surface) ||
                !WorldSiteRuntimeDeploymentCacheBuilder.IsDeploymentCandidateSurface(gridMap, surface))
            {
                continue;
            }

            var candidate = new WorldSiteDeploymentCell(
                new Vector2I(surface.Position.X, surface.Position.Y),
                surface.Height,
                surface.TerrainTag ?? "",
                BattleGridTerrainQueries.IsWater(surface));
            if (!CanUseDeploymentCell(candidate, canEnterWater))
            {
                continue;
            }

            if (seen.Add(surface.SurfacePosition))
            {
                result.Add(candidate);
            }
        }
    }

    private static bool TryGetPlacementSurface(
        BattleGridMap gridMap,
        WorldSiteUnitPlacement placement,
        out GridCellSurface surface)
    {
        surface = null;
        if (placement == null || gridMap == null)
        {
            return false;
        }

        var position = new GridPosition(placement.CellX, placement.CellY);
        if (placement.CellHeight == 0 && gridMap.TryGetTopSurface(position, out surface))
        {
            return true;
        }

        if (gridMap.TryGetSurface(new GridSurfacePosition(position, placement.CellHeight), out surface))
        {
            return true;
        }

        return gridMap.TryGetTopSurface(position, out surface);
    }

    private static bool TryGetDeploymentCellForPlacement(
        WorldSiteRuntimeDeploymentCache deploymentCache,
        WorldSiteUnitPlacement placement,
        out WorldSiteDeploymentCell cell)
    {
        cell = default;
        if (placement == null || deploymentCache == null)
        {
            return false;
        }

        foreach (WorldSiteDeploymentCell candidate in deploymentCache.CandidatesByDirection.Values.SelectMany(candidates => candidates))
        {
            if (candidate.Cell.X == placement.CellX &&
                candidate.Cell.Y == placement.CellY &&
                candidate.Height == placement.CellHeight)
            {
                cell = candidate;
                return true;
            }
        }

        foreach (WorldSiteDeploymentCell candidate in deploymentCache.CandidatesByDirection.Values.SelectMany(candidates => candidates))
        {
            if (candidate.Cell.X == placement.CellX &&
                candidate.Cell.Y == placement.CellY)
            {
                cell = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool IsDeploymentCandidateOccupied(
        WorldSiteState site,
        WorldSiteDeploymentCell candidate,
        string ignorePlacementId)
    {
        return site.UnitPlacements.Any(placement =>
            placement.PlacementId != ignorePlacementId &&
            placement.CellX == candidate.Cell.X &&
            placement.CellY == candidate.Cell.Y);
    }

    private static bool CanUseDeploymentCell(WorldSiteDeploymentCell candidate, bool canEnterWater)
    {
        return canEnterWater || !candidate.IsWater;
    }
}
