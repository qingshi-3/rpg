using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.World;
using Rpg.Definitions.Battle;
using Rpg.Definitions.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle;
using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.World.Sites;

public partial class WorldSiteRoot
{
    private bool EnsureBattleRequestSiteDeployments(BattleStartRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.TargetSiteId))
        {
            GameLog.Error(nameof(WorldSiteRoot), "Cannot prepare battle deployments because request target site is missing.");
            return false;
        }

        if (StrategicWorldRuntime.State?.SiteStates.TryGetValue(request.TargetSiteId, out WorldSiteState site) != true)
        {
            GameLog.Error(nameof(WorldSiteRoot), $"Cannot prepare battle deployments because WorldSiteState is missing site={request.TargetSiteId}");
            return false;
        }

        if (StrategicWorldRuntime.Definition == null)
        {
            GameLog.Error(nameof(WorldSiteRoot), $"Cannot prepare battle deployments because StrategicWorldDefinition is missing site={site.SiteId}");
            return false;
        }

        WorldSiteDefinition siteDefinition = new StrategicWorldDefinitionQueries(StrategicWorldRuntime.Definition).GetSite(site.SiteId);
        if (siteDefinition == null)
        {
            GameLog.Error(nameof(WorldSiteRoot), $"Cannot prepare battle deployments because WorldSiteDefinition is missing site={site.SiteId}");
            return false;
        }

        if (_deploymentCache == null || _deploymentCache.SiteId != site.SiteId)
        {
            RebuildSiteDeploymentRuntimeCache(site.SiteId);
        }

        if (_deploymentCache == null ||
            _deploymentCache.GetCandidates(WorldSiteAttackDirection.Any).Count == 0)
        {
            GameLog.Error(nameof(WorldSiteRoot), $"Cannot prepare battle deployments because deployment cache is empty site={site.SiteId}");
            return false;
        }

        return _battleDeploymentPreparer.Prepare(
            request,
            site,
            siteDefinition,
            _deploymentCache,
            _activeGridMap,
            ResolveForceCanEnterWater,
            ResolvePlacementCanEnterWater,
            out _);
    }

    private bool ResolveForceCanEnterWater(BattleForceRequest force)
    {
        if (!_battleUnitFactory.TryGetUnitDefinition(force?.UnitDefinitionId, out BattleUnitDefinition definition))
        {
            return false;
        }

        return definition.CanEnterWater;
    }

    private void ApplyBattleRequestForceFootprints(BattleStartRequest request)
    {
        IEnumerable<BattleForceRequest> playerForces = request?.PlayerForces ?? Enumerable.Empty<BattleForceRequest>();
        IEnumerable<BattleForceRequest> enemyForces = request?.EnemyForces ?? Enumerable.Empty<BattleForceRequest>();
        foreach (BattleForceRequest force in playerForces.Concat(enemyForces))
        {
            if (!_battleUnitFactory.TryGetUnitDefinition(force?.UnitDefinitionId, out BattleUnitDefinition definition))
            {
                continue;
            }

            force.FootprintWidth = definition.FootprintWidth;
            force.FootprintHeight = definition.FootprintHeight;
            force.MaxHitPoints = definition.MaxHp;
            force.AttackDamage = definition.AttackDamage;
            force.AttackRange = definition.AttackRange;
            force.AttackSpeed = definition.AttackSpeed;
            force.MoveStepSeconds = BattleActionTimingPolicy.NormalizeActionSeconds(
                _unitRoot?.UnitMoveDuration ?? BattleActionTimingPolicy.DefaultMoveStepSeconds,
                BattleActionTimingPolicy.DefaultMoveStepSeconds);
            force.AttackActionSeconds = BattleActionTimingPolicy.ResolveAttackActionSeconds(
                definition.Visual?.AnimationSet?.TargetAttackSeconds ?? BattleActionTimingPolicy.DefaultAttackActionSeconds,
                definition.AttackSpeed);
            force.AttackImpactDelaySeconds = BattleActionTimingPolicy.ResolveAttackImpactDelaySeconds(
                force.AttackActionSeconds,
                ResolveAttackImpactNormalizedTime(definition));
        }
    }

    private static double ResolveAttackImpactNormalizedTime(BattleUnitDefinition definition)
    {
        if (definition == null)
        {
            return BattleActionTimingPolicy.DefaultAttackImpactNormalizedTime;
        }

        return definition.AttackImpactNormalizedTimeOverride >= 0
            ? System.Math.Clamp(definition.AttackImpactNormalizedTimeOverride, 0, 1)
            : definition.Visual?.AnimationSet?.AttackImpactNormalizedTime ??
              BattleActionTimingPolicy.DefaultAttackImpactNormalizedTime;
    }

    private static bool CanUseDeploymentCell(WorldSiteDeploymentCell candidate, bool canEnterWater)
    {
        return canEnterWater || !candidate.IsWater;
    }

    private bool EnsureSitePlacementsRespectTerrain(WorldSiteState site, WorldSiteDefinition definition)
    {
        if (site == null || definition == null)
        {
            return false;
        }

        if (_deploymentCache == null || _deploymentCache.SiteId != site.SiteId)
        {
            RebuildSiteDeploymentRuntimeCache(site.SiteId);
        }

        WorldSiteDeploymentTerrainReconcileResult result = _deploymentTerrainReconciler.Reconcile(
            _activeGridMap,
            _deploymentCache,
            site,
            definition,
            ResolvePlacementCanEnterWater);
        return result.Success;
    }

    private bool ResolvePlacementCanEnterWater(WorldSiteUnitPlacement placement)
    {
        if (!_battleUnitFactory.TryGetUnitDefinition(placement?.UnitTypeId, out BattleUnitDefinition definition))
        {
            return false;
        }

        return definition.CanEnterWater;
    }

    private void AddRequestedForces(
        IEnumerable<BattleForceRequest> forces,
        BattleFaction fallbackFaction,
        BattleStartRequest request,
        ISet<GridSurfacePosition> reservedDeploymentSurfaces,
        bool requireAllPlacements = true)
    {
        foreach (BattleForceRequest force in forces ?? System.Array.Empty<BattleForceRequest>())
        {
            if (force.Count <= 0)
            {
                continue;
            }

            for (int i = 0; i < force.Count; i++)
            {
                BattleForcePlacementRequest placement = i < force.PreferredPlacements.Count
                    ? force.PreferredPlacements[i]
                    : null;
                if (placement == null)
                {
                    if (requireAllPlacements)
                    {
                        GameLog.Error(
                            nameof(WorldSiteRoot),
                            $"Skip battle unit without prepared placement force={force.ForceId} unit={force.UnitDefinitionId} index={i}");
                    }

                    continue;
                }

                GridPosition fallbackPosition = new(placement.CellX, placement.CellY);
                BattleEntity entity = _battleUnitFactory.Create(force, i, fallbackFaction, fallbackPosition);
                if (entity == null)
                {
                    GameLog.Warn(nameof(WorldSiteRoot), $"Skip battle unit force={force.ForceId} unit={force.UnitDefinitionId} index={i}");
                    continue;
                }

                ApplyBattleRequestDeployment(entity, force, i, fallbackFaction, request, reservedDeploymentSurfaces);
                RegisterBattlePreparationPlacement(entity, force, i, fallbackFaction);
                _unitRoot.AddChild(entity);
            }
        }
    }

    private void RefreshBattleRequestMapEntitiesForDirectRuntime(BattleStartRequest request)
    {
        ClearBattleEntities();
        var reservedDeploymentSurfaces = new HashSet<GridSurfacePosition>();
        AddRequestedForces(request.PlayerForces, BattleFaction.Player, request, reservedDeploymentSurfaces);
        AddRequestedForces(request.EnemyForces, BattleFaction.Enemy, request, reservedDeploymentSurfaces);
        PlaceBattleEntitiesOnGrid();
    }

    private void RegisterBattlePreparationPlacement(
        BattleEntity entity,
        BattleForceRequest force,
        int forceIndex,
        BattleFaction fallbackFaction)
    {
        if (!_isBattlePreparationActive ||
            entity == null ||
            forceIndex >= (force?.PreferredPlacements?.Count ?? 0))
        {
            return;
        }

        string placementId = force.PreferredPlacements[forceIndex]?.PlacementId ?? "";
        if (!string.IsNullOrWhiteSpace(placementId))
        {
            // Both sides are indexed from the same prepared placement data so
            // preview and runtime start positions cannot drift apart. Battle
            // preparation can tune both sides, but runtime disables all drag before start.
            SetDeploymentDragComponent(entity, placementId, IsBattlePreparationPlacementDragEnabled(fallbackFaction));
            _sitePlacementEntities[placementId] = entity;
        }
    }

    private static bool IsBattlePreparationPlacementDragEnabled(BattleFaction fallbackFaction)
    {
        return fallbackFaction is BattleFaction.Player or BattleFaction.Enemy;
    }

    private void ApplyBattleRequestDeployment(
        BattleEntity entity,
        BattleForceRequest force,
        int forceIndex,
        BattleFaction fallbackFaction,
        BattleStartRequest request,
        ISet<GridSurfacePosition> reservedDeploymentSurfaces)
    {
        GridOccupantComponent gridOccupant = entity?.GetComponent<GridOccupantComponent>();
        if (gridOccupant == null)
        {
            return;
        }

        BattleForcePlacementRequest placement = forceIndex < (force?.PreferredPlacements?.Count ?? 0)
            ? force.PreferredPlacements[forceIndex]
            : null;
        if (placement != null)
        {
            gridOccupant.GridX = placement.CellX;
            gridOccupant.GridY = placement.CellY;
            gridOccupant.FootprintWidth = BattleFootprintCells.NormalizeSize(force?.FootprintWidth ?? gridOccupant.FootprintWidth);
            gridOccupant.FootprintHeight = BattleFootprintCells.NormalizeSize(force?.FootprintHeight ?? gridOccupant.FootprintHeight);
            if (placement.CellHeight > 0)
            {
                gridOccupant.GridHeight = placement.CellHeight;
                gridOccupant.UseExplicitHeight = true;
            }

            ResolveEntitySurfaceHeight(gridOccupant);
            reservedDeploymentSurfaces?.Add(gridOccupant.SurfacePosition);
            GameLog.Info(
                nameof(WorldSiteRoot),
                $"Battle unit placed from WorldSiteState entity={entity.EntityId} force={force?.ForceId} placement={placement.PlacementId} surface={gridOccupant.SurfacePosition}");
            return;
        }

        ResolveEntitySurfaceHeight(gridOccupant);
        reservedDeploymentSurfaces?.Add(gridOccupant.SurfacePosition);
        GameLog.Error(
            nameof(WorldSiteRoot),
            $"Battle unit missing WorldSiteState placement entity={entity.EntityId} force={force?.ForceId} faction={fallbackFaction} fallbackSurface={gridOccupant.SurfacePosition}");
    }

    private void ClearBattleEntities()
    {
        if (_unitRoot == null)
        {
            return;
        }

        foreach (BattleEntity entity in _unitRoot.GetEntitiesSnapshot())
        {
            entity.GetParent()?.RemoveChild(entity);
            entity.QueueFree();
        }
    }
}
