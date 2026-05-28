using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Definitions.Maps;
using Rpg.Definitions.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.World;

public sealed class WorldSiteBattleDeploymentPreparer
{
    private readonly WorldSiteDeploymentService _deploymentService;
    private readonly WorldSiteDeploymentTerrainReconciler _terrainReconciler;

    public WorldSiteBattleDeploymentPreparer(
        WorldSiteDeploymentService deploymentService = null,
        WorldSiteDeploymentTerrainReconciler terrainReconciler = null)
    {
        _deploymentService = deploymentService ?? new WorldSiteDeploymentService();
        _terrainReconciler = terrainReconciler ?? new WorldSiteDeploymentTerrainReconciler(_deploymentService);
    }

    public bool Prepare(
        BattleStartRequest request,
        WorldSiteState site,
        WorldSiteDefinition definition,
        WorldSiteRuntimeDeploymentCache deploymentCache,
        BattleGridMap gridMap,
        Func<BattleForceRequest, bool> canForceEnterWater,
        Func<WorldSiteUnitPlacement, bool> canPlacementEnterWater,
        out string failureReason)
    {
        failureReason = "";
        if (request == null)
        {
            failureReason = "missing_battle_request";
            return false;
        }

        if (site == null || definition == null)
        {
            failureReason = "missing_site";
            return false;
        }

        if (deploymentCache == null ||
            deploymentCache.GetCandidates(WorldSiteAttackDirection.Any).Count == 0)
        {
            failureReason = "deployment_cache_empty";
            GameLog.Error(
                nameof(WorldSiteBattleDeploymentPreparer),
                $"Cannot prepare battle deployments because deployment cache is empty site={site.SiteId} request={request.RequestId}");
            return false;
        }

        _deploymentService.EnsureGarrisonPlacements(site, definition);
        WorldSiteDeploymentTerrainReconcileResult terrainResult = _terrainReconciler.Reconcile(
            gridMap,
            deploymentCache,
            site,
            definition,
            canPlacementEnterWater);

        bool success = terrainResult.Success;
        if (!terrainResult.Success)
        {
            failureReason = terrainResult.LastFailureReason;
        }

        foreach (BattleForceRequest force in request.PlayerForces ?? new List<BattleForceRequest>())
        {
            success &= EnsureResidentForceUsesDeploymentZone(
                request,
                force,
                SemanticDeploymentSide.Player,
                site,
                definition,
                deploymentCache,
                canForceEnterWater,
                ref failureReason);
            success &= EnsureForceWorldSitePlacement(
                request,
                force,
                SemanticDeploymentSide.Player,
                site,
                deploymentCache,
                canForceEnterWater,
                ref failureReason);
        }

        foreach (BattleForceRequest force in request.EnemyForces ?? new List<BattleForceRequest>())
        {
            success &= EnsureResidentForceUsesDeploymentZone(
                request,
                force,
                SemanticDeploymentSide.Enemy,
                site,
                definition,
                deploymentCache,
                canForceEnterWater,
                ref failureReason);
            success &= EnsureForceWorldSitePlacement(
                request,
                force,
                SemanticDeploymentSide.Enemy,
                site,
                deploymentCache,
                canForceEnterWater,
                ref failureReason);
        }

        success &= ApplyPreferredPlacementsFromWorldSite(
            site,
            definition,
            deploymentCache,
            gridMap,
            request.PlayerForces,
            canForceEnterWater,
            ref failureReason);
        success &= ApplyPreferredPlacementsFromWorldSite(
            site,
            definition,
            deploymentCache,
            gridMap,
            request.EnemyForces,
            canForceEnterWater,
            ref failureReason);

        GameLog.Info(
            nameof(WorldSiteBattleDeploymentPreparer),
            $"BattleDeploymentsPreparedFromWorldSite site={site.SiteId} request={request.RequestId} placements={site.UnitPlacements.Count} playerForces={request.PlayerForces.Count} enemyForces={request.EnemyForces.Count}");
        return success;
    }

    private bool EnsureResidentForceUsesDeploymentZone(
        BattleStartRequest request,
        BattleForceRequest force,
        SemanticDeploymentSide side,
        WorldSiteState site,
        WorldSiteDefinition definition,
        WorldSiteRuntimeDeploymentCache deploymentCache,
        Func<BattleForceRequest, bool> canForceEnterWater,
        ref string failureReason)
    {
        if (force == null ||
            force.Count <= 0 ||
            !IsResidentWorldSiteForceForSite(force, site) ||
            deploymentCache?.HasAuthoredDeploymentZoneForSide(side, force.FactionId) != true)
        {
            return true;
        }

        WorldSiteAttackDirection desiredDirection = ResolveForceDeploymentDirection(request, force, side);
        BattleEntranceRequest entrance = ResolveForceEntrance(request, force, desiredDirection);
        WorldSiteAttackDirection deploymentDirection = entrance?.Direction ?? desiredDirection;
        string entranceId = entrance?.EntranceId ?? force.PreferredEntranceId ?? "";
        if (!string.IsNullOrWhiteSpace(entranceId))
        {
            force.PreferredEntranceId = entranceId;
        }

        bool canEnterWater = canForceEnterWater?.Invoke(force) == true;
        WorldSiteDeploymentCell[] candidates = deploymentCache
            .GetDeploymentZoneCandidatesForSide(side, force.FactionId, deploymentDirection)
            .Where(candidate => CanUseDeploymentCell(candidate, canEnterWater))
            .ToArray();
        if (candidates.Length == 0)
        {
            failureReason = "deployment_candidates_missing";
            return false;
        }

        WorldSiteUnitPlacement[] placements = ResolveWorldSitePlacementsForForce(site, force)
            .OrderBy(placement => placement.UnitIndex)
            .ThenBy(placement => placement.PlacementId)
            .Take(force.Count)
            .ToArray();
        if (placements.Length < force.Count)
        {
            failureReason = "battle_force_world_site_placements_missing";
            return false;
        }

        foreach (WorldSiteUnitPlacement placement in placements)
        {
            if (IsPlacementInCandidates(placement, candidates) ||
                TryMovePlacementToDeploymentCandidate(site, definition, placement, candidates, out _))
            {
                // Resident defenders keep their site-local placement identity, but
                // battle preparation must align that identity with authored start zones
                // so later launch sync cannot snap them back to stale garrison cells.
                placement.EntranceId = entranceId;
                placement.AttackDirection = deploymentDirection;
                continue;
            }

            failureReason = "deployment_cell_unavailable";
            GameLog.Error(
                nameof(WorldSiteBattleDeploymentPreparer),
                $"ResidentBattleDeploymentPrepareFailed site={site?.SiteId ?? ""} request={request?.RequestId ?? ""} force={force.ForceId} unit={force.UnitDefinitionId} placement={placement.PlacementId} reason={failureReason} direction={deploymentDirection} canEnterWater={canEnterWater}");
            return false;
        }

        return true;
    }

    private bool EnsureForceWorldSitePlacement(
        BattleStartRequest request,
        BattleForceRequest force,
        SemanticDeploymentSide side,
        WorldSiteState site,
        WorldSiteRuntimeDeploymentCache deploymentCache,
        Func<BattleForceRequest, bool> canForceEnterWater,
        ref string failureReason)
    {
        if (force == null || force.Count <= 0 || IsResidentWorldSiteForceForSite(force, site))
        {
            return true;
        }

        WorldSiteAttackDirection desiredDirection = ResolveForceDeploymentDirection(request, force, side);
        BattleEntranceRequest entrance = ResolveForceEntrance(request, force, desiredDirection);
        WorldSiteAttackDirection deploymentDirection = entrance?.Direction ?? desiredDirection;
        string entranceId = entrance?.EntranceId ?? force.PreferredEntranceId ?? "";
        if (!string.IsNullOrWhiteSpace(entranceId))
        {
            force.PreferredEntranceId = entranceId;
        }

        WorldSiteUnitPlacementKind placementKind = request.BattleKind == BattleKind.FieldIntercept
            ? WorldSiteUnitPlacementKind.FieldArmy
            : IsAttackingForce(request, force, side)
                ? WorldSiteUnitPlacementKind.Attacker
                : WorldSiteUnitPlacementKind.Defender;
        bool canEnterWater = canForceEnterWater?.Invoke(force) == true;
        IReadOnlyList<WorldSiteDeploymentCell> candidates = (deploymentCache?.GetDeploymentZoneCandidatesForSide(side, force.FactionId, deploymentDirection) ??
                                                            Array.Empty<WorldSiteDeploymentCell>())
            .Where(candidate => CanUseDeploymentCell(candidate, canEnterWater))
            .ToArray();
        if (!_deploymentService.EnsureBattlePlacementsForForce(
                site,
                force,
                placementKind,
                deploymentDirection,
                candidates,
                entranceId,
                out string forceFailureReason))
        {
            failureReason = forceFailureReason;
            GameLog.Error(
                nameof(WorldSiteBattleDeploymentPreparer),
                $"BattleDeploymentPrepareFailed site={site?.SiteId ?? ""} request={request.RequestId} force={force.ForceId} unit={force.UnitDefinitionId} reason={forceFailureReason} direction={deploymentDirection} entrance={entranceId} canEnterWater={canEnterWater}");
            return false;
        }

        return true;
    }

    private bool ApplyPreferredPlacementsFromWorldSite(
        WorldSiteState site,
        WorldSiteDefinition definition,
        WorldSiteRuntimeDeploymentCache deploymentCache,
        BattleGridMap gridMap,
        IEnumerable<BattleForceRequest> forces,
        Func<BattleForceRequest, bool> canForceEnterWater,
        ref string failureReason)
    {
        if (site == null || forces == null)
        {
            failureReason = "missing_site";
            return false;
        }

        bool success = true;
        foreach (BattleForceRequest force in forces)
        {
            if (force == null || force.Count <= 0)
            {
                continue;
            }

            bool canEnterWater = canForceEnterWater?.Invoke(force) == true;
            WorldSiteUnitPlacement[] placements = ResolveWorldSitePlacementsForForce(site, force)
                .OrderBy(placement => placement.UnitIndex)
                .ThenBy(placement => placement.PlacementId)
                .Take(force.Count)
                .ToArray();
            force.PreferredPlacements.Clear();
            foreach (WorldSiteUnitPlacement placement in placements)
            {
                if (!_terrainReconciler.CanUsePlacement(gridMap, placement, canEnterWater, out _) &&
                    !_terrainReconciler.TryRelocatePlacementForTerrain(
                        gridMap,
                        deploymentCache,
                        site,
                        definition,
                        placement,
                        canEnterWater,
                        out string placementFailureReason))
                {
                    success = false;
                    failureReason = placementFailureReason;
                    GameLog.Error(
                        nameof(WorldSiteBattleDeploymentPreparer),
                        $"BattleForcePlacementInvalidTerrain site={site.SiteId} force={force.ForceId} unit={force.UnitDefinitionId} placement={placement.PlacementId} cell=({placement.CellX},{placement.CellY},h={placement.CellHeight}) terrain={_terrainReconciler.GetPlacementTerrainTag(gridMap, deploymentCache, placement)} canEnterWater={canEnterWater} reason={placementFailureReason}");
                    continue;
                }

                force.PreferredPlacements.Add(new BattleForcePlacementRequest
                {
                    PlacementId = placement.PlacementId,
                    CellX = placement.CellX,
                    CellY = placement.CellY,
                    CellHeight = placement.CellHeight
                });
            }

            if (force.PreferredPlacements.Count < force.Count)
            {
                success = false;
                failureReason = "battle_force_world_site_placements_missing";
                GameLog.Error(
                    nameof(WorldSiteBattleDeploymentPreparer),
                    $"BattleForceMissingWorldSitePlacements site={site.SiteId} force={force.ForceId} unit={force.UnitDefinitionId} expected={force.Count} actual={force.PreferredPlacements.Count}");
            }
        }

        return success;
    }

    private static IEnumerable<WorldSiteUnitPlacement> ResolveWorldSitePlacementsForForce(
        WorldSiteState site,
        BattleForceRequest force)
    {
        if (IsResidentGarrisonForceForSite(force, site.SiteId))
        {
            return site.UnitPlacements.Where(placement =>
                WorldSiteDeploymentService.IsGarrisonPlacement(placement) &&
                placement.UnitTypeId == force.UnitDefinitionId);
        }

        if (IsResidentSitePlacementForce(force, site))
        {
            return site.UnitPlacements.Where(placement =>
                placement.PlacementId == force.SourceId &&
                placement.UnitTypeId == force.UnitDefinitionId);
        }

        if (IsResidentPlayerArmySiteForce(force, site))
        {
            return site.UnitPlacements.Where(placement =>
                IsPlayerArmySitePlacement(placement) &&
                placement.SourceId == force.SourceId &&
                placement.UnitTypeId == force.UnitDefinitionId);
        }

        string sourceKind = ResolveForceSourceKind(force);
        string sourceId = ResolveForceSourceId(force);
        return site.UnitPlacements.Where(placement =>
            !WorldSiteDeploymentService.IsGarrisonPlacement(placement) &&
            placement.UnitTypeId == force.UnitDefinitionId &&
            placement.SourceKind == sourceKind &&
            placement.SourceId == sourceId);
    }

    private static bool IsResidentGarrisonForceForSite(BattleForceRequest force, string siteId)
    {
        if (force == null || string.IsNullOrWhiteSpace(siteId))
        {
            return false;
        }

        return (force.SourceKind == "Garrison" || force.SourceKind == "DefenderSite") &&
               force.SourceId == siteId;
    }

    private static bool IsResidentWorldSiteForceForSite(BattleForceRequest force, WorldSiteState site)
    {
        return site != null &&
               (IsResidentGarrisonForceForSite(force, site.SiteId) ||
                IsResidentSitePlacementForce(force, site) ||
                IsResidentPlayerArmySiteForce(force, site));
    }

    private static bool IsResidentSitePlacementForce(BattleForceRequest force, WorldSiteState site)
    {
        return force != null &&
               site != null &&
               force.SourceKind == "SitePlacement" &&
               !string.IsNullOrWhiteSpace(force.SourceId) &&
               site.UnitPlacements.Any(placement =>
                   placement.PlacementId == force.SourceId &&
                   placement.UnitTypeId == force.UnitDefinitionId);
    }

    private static bool IsResidentPlayerArmySiteForce(BattleForceRequest force, WorldSiteState site)
    {
        return force != null &&
               site != null &&
               force.SourceKind == "PlayerArmy" &&
               !string.IsNullOrWhiteSpace(force.SourceId) &&
               site.UnitPlacements.Any(placement =>
                   IsPlayerArmySitePlacement(placement) &&
                   placement.SourceId == force.SourceId &&
                   placement.UnitTypeId == force.UnitDefinitionId);
    }

    private static bool IsPlayerArmySitePlacement(WorldSiteUnitPlacement placement)
    {
        return placement != null &&
               placement.SourceKind == "PlayerArmy" &&
               !string.IsNullOrWhiteSpace(placement.ArmyId) &&
               placement.PlacementKind is WorldSiteUnitPlacementKind.VisitingArmy or WorldSiteUnitPlacementKind.Attacker;
    }

    private static WorldSiteAttackDirection ResolveForceDeploymentDirection(
        BattleStartRequest request,
        BattleForceRequest force,
        SemanticDeploymentSide side)
    {
        WorldSiteAttackDirection attackDirection = request?.AttackDirection ?? WorldSiteAttackDirection.Any;
        if (attackDirection == WorldSiteAttackDirection.Any)
        {
            return WorldSiteAttackDirection.Any;
        }

        return IsAttackingForce(request, force, side)
            ? attackDirection
            : GetOppositeDirection(attackDirection);
    }

    private static bool IsAttackingForce(
        BattleStartRequest request,
        BattleForceRequest force,
        SemanticDeploymentSide side)
    {
        if (request != null &&
            !string.IsNullOrWhiteSpace(force?.FactionId) &&
            !string.IsNullOrWhiteSpace(request.AttackerFactionId))
        {
            return force.FactionId == request.AttackerFactionId;
        }

        return request?.BattleKind switch
        {
            BattleKind.AssaultSite => side == SemanticDeploymentSide.Player,
            BattleKind.FieldIntercept => side == SemanticDeploymentSide.Player,
            _ => side == SemanticDeploymentSide.Enemy
        };
    }

    private static WorldSiteAttackDirection GetOppositeDirection(WorldSiteAttackDirection direction)
    {
        return direction switch
        {
            WorldSiteAttackDirection.North => WorldSiteAttackDirection.South,
            WorldSiteAttackDirection.South => WorldSiteAttackDirection.North,
            WorldSiteAttackDirection.West => WorldSiteAttackDirection.East,
            WorldSiteAttackDirection.East => WorldSiteAttackDirection.West,
            _ => WorldSiteAttackDirection.Any
        };
    }

    private static BattleEntranceRequest ResolveForceEntrance(
        BattleStartRequest request,
        BattleForceRequest force,
        WorldSiteAttackDirection desiredDirection)
    {
        if (request?.AvailableEntrances == null || request.AvailableEntrances.Count == 0)
        {
            return null;
        }

        BattleEntranceRequest[] candidates = request.AvailableEntrances
            .Where(entrance => IsEntranceForForce(entrance, force))
            .ToArray();
        if (candidates.Length == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(force?.PreferredEntranceId))
        {
            BattleEntranceRequest preferred = candidates.FirstOrDefault(entrance => entrance.EntranceId == force.PreferredEntranceId);
            if (preferred != null)
            {
                return preferred;
            }
        }

        if (desiredDirection != WorldSiteAttackDirection.Any)
        {
            BattleEntranceRequest exact = candidates.FirstOrDefault(entrance => entrance.Direction == desiredDirection);
            if (exact != null)
            {
                return exact;
            }
        }

        BattleEntranceRequest anyEntrance = candidates.FirstOrDefault(entrance => entrance.Direction == WorldSiteAttackDirection.Any);
        if (anyEntrance != null)
        {
            return anyEntrance;
        }

        return candidates.FirstOrDefault();
    }

    private static bool IsEntranceForForce(BattleEntranceRequest entrance, BattleForceRequest force)
    {
        if (entrance == null)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(entrance.FactionId) ||
               string.IsNullOrWhiteSpace(force?.FactionId) ||
               entrance.FactionId == force.FactionId;
    }

    private static bool CanUseDeploymentCell(WorldSiteDeploymentCell candidate, bool canEnterWater)
    {
        return canEnterWater || !candidate.IsWater;
    }

    private static bool IsPlacementInCandidates(
        WorldSiteUnitPlacement placement,
        IEnumerable<WorldSiteDeploymentCell> candidates)
    {
        return placement != null &&
               (candidates ?? Enumerable.Empty<WorldSiteDeploymentCell>()).Any(candidate =>
                   candidate.Cell.X == placement.CellX &&
                   candidate.Cell.Y == placement.CellY &&
                   candidate.Height == placement.CellHeight);
    }

    private bool TryMovePlacementToDeploymentCandidate(
        WorldSiteState site,
        WorldSiteDefinition definition,
        WorldSiteUnitPlacement placement,
        IEnumerable<WorldSiteDeploymentCell> candidates,
        out string failureReason)
    {
        failureReason = "";
        foreach (WorldSiteDeploymentCell candidate in candidates ?? Enumerable.Empty<WorldSiteDeploymentCell>())
        {
            if (_deploymentService.TryMovePlacementToSurface(
                    site,
                    definition,
                    placement.PlacementId,
                    new GridSurfacePosition(candidate.Cell.X, candidate.Cell.Y, candidate.Height),
                    out failureReason))
            {
                return true;
            }
        }

        return false;
    }

    private static string ResolveForceSourceKind(BattleForceRequest force)
    {
        return string.IsNullOrWhiteSpace(force?.SourceKind) ? "BattleForce" : force.SourceKind;
    }

    private static string ResolveForceSourceId(BattleForceRequest force)
    {
        if (force == null)
        {
            return "";
        }

        if (!string.IsNullOrWhiteSpace(force.SourceId))
        {
            return force.SourceId;
        }

        if (!string.IsNullOrWhiteSpace(force.ForceId))
        {
            return force.ForceId;
        }

        return force.UnitDefinitionId ?? "";
    }
}
