using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Reports;
using Rpg.Application.World;
using Rpg.Definitions.Battle;
using Rpg.Definitions.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle;
using Rpg.Presentation.Battle.Actions;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Rules;
using Rpg.Runtime.Battle.Events;

namespace Rpg.Presentation.World.Sites;

public partial class WorldSiteRoot
{
    private void ApplyBattleStartRequest()
    {
        _battleStartBlockedReason = "";
        if (_unitRoot == null || !BattleSessionHandoff.TryPeekActiveRequest(out BattleStartRequest request))
        {
            return;
        }

        _battlePreparationRequest = request;
        if (request.BattleKind is BattleKind.AssaultSite or BattleKind.DefenseRaid or BattleKind.FieldIntercept)
        {
            if (!EnsureBattleRequestSiteDeployments(request))
            {
                _battleStartBlockedReason = "场域部署数据缺失，无法进入战斗。";
                ClearBattleEntities();
                return;
            }

            ClearBattleEntities();
            var reservedDeploymentSurfaces = new HashSet<GridSurfacePosition>();
            AddRequestedForces(request.PlayerForces, BattleFaction.Player, request, reservedDeploymentSurfaces);
            AddRequestedForces(request.EnemyForces, BattleFaction.Enemy, request, reservedDeploymentSurfaces);
        }

        if (request.BattleKind == BattleKind.DefenseRaid)
        {
            ApplyBattleModifiers(request);
        }

        GameLog.Info(
            nameof(WorldSiteRoot),
            $"Battle request consumed kind={request.BattleKind} target={request.TargetSiteId} playerForces={request.PlayerForces.Count} enemyForces={request.EnemyForces.Count} modifiers={request.BattleModifiers.Count}");
    }

    private bool ActivateBattleRuntime()
    {
        _isBattlePreparationActive = false;
        _battlePreparationRequest = null;
        _highlightOverlay?.ClearCells(BattleGridHighlightKind.FriendlyMove);
        if (!string.IsNullOrWhiteSpace(_battleStartBlockedReason))
        {
            GameLog.Warn(nameof(WorldSiteRoot), $"Battle runtime activation blocked reason={_battleStartBlockedReason}");
            SetBattleRuntimeEnabled(false);
            return false;
        }

        // Preparation can start runtime directly after the player confirms deployment,
        // so this boundary must own the UI transition instead of relying on launch callbacks.
        SetBattleRuntimeEnabled(true);
        BindBattleRuntimeHud();
        return ActivateBattleGroupRuntime();
    }

    private void BindBattleRuntimeHud()
    {
        // Battle runtime UI is currently presentation-only playback. Future command
        // controls belong behind this binder and must submit CommandRequest.
        SetBattlePreparationContentVisible(false);
        UpdateSitePeacetimePanelVisibility("battle_runtime");
    }

    private bool ActivateBattleGroupRuntime()
    {
        if (!_battleGroupRuntimeAdapter.TryResolveActiveBattle(out WorldSiteBattleGroupRuntimeResolveResult resolution))
        {
            _battleStartBlockedReason = string.IsNullOrWhiteSpace(resolution?.FailureReason)
                ? "battle_group_runtime_activation_failed"
                : resolution.FailureReason;
            GameLog.Warn(nameof(WorldSiteRoot), $"Battle group runtime activation blocked reason={_battleStartBlockedReason}");
            SetBattleRuntimeEnabled(false);
            return false;
        }

        _ = PlayBattleGroupRuntimeAndApplyResultAsync(resolution);
        return true;
    }

    private async Task PlayBattleGroupRuntimeAndApplyResultAsync(WorldSiteBattleGroupRuntimeResolveResult resolution)
    {
        WorldActionResult applyResult = null;
        try
        {
            await PlayBattleGroupRuntimeEventsAsync(resolution);
        }
        catch (System.Exception ex)
        {
            GameLog.Warn(nameof(WorldSiteRoot), $"Battle runtime presentation failed request={resolution?.Request?.RequestId ?? ""} error={ex.Message}");
        }

        applyResult = ApplyBattleResultToWorld(resolution.Request, resolution.BattleResult);
        string battleNotice = BuildBattleGroupRuntimeReturnNotice(applyResult, resolution.Report);
        if (!string.IsNullOrWhiteSpace(battleNotice))
        {
            applyResult ??= new WorldActionResult
            {
                Success = true,
                ActionId = "battle_result"
            };
            applyResult.Message = battleNotice;
            StrategicWorldRuntime.LastNotice = battleNotice;
        }

        ReconcileWorldSitePlacementsAfterBattle(
            resolution.Request,
            System.Array.Empty<WorldSiteLivePlacementSnapshot>(),
            resolution.BattleResult.Outcome);
        ClearBattleEntities();
        SetBattleRuntimeEnabled(false);
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"Battle group runtime resolved request={resolution.Request?.RequestId ?? ""} outcome={resolution.BattleResult?.Outcome} reportEvents={resolution.Report?.SourceEventIds.Count ?? 0} failure={string.Join(",", resolution.Report?.FailureCandidates ?? new List<string>())}");
        SwitchToNonBattleUi(
            resolution.BattleResult.Outcome,
            resolution.Request,
            applyResult,
            resolution.Request?.ReturnScenePath ?? "");
    }

    private async Task PlayBattleGroupRuntimeEventsAsync(WorldSiteBattleGroupRuntimeResolveResult resolution)
    {
        IReadOnlyList<BattleEvent> events = resolution?.FlowResult?.RuntimeResult?.EventStream?.Events;
        if (_unitRoot == null || events == null || events.Count == 0)
        {
            return;
        }

        Dictionary<string, BattleEntity> entitiesByRuntimeActor = _unitRoot.GetEntitiesSnapshot()
            .Where(entity => entity != null && GodotObject.IsInstanceValid(entity))
            .GroupBy(entity => entity.EntityId)
            .ToDictionary(group => group.Key, group => group.First());

        // Presentation consumes runtime events only as playback instructions. The
        // authoritative result still comes from runtime outcome and settlement.
        foreach (BattleEvent runtimeEvent in events)
        {
            if (runtimeEvent == null)
            {
                continue;
            }

            switch (runtimeEvent.Kind)
            {
                case BattleEventKind.MovementCompleted:
                    await PlayRuntimeMovementEventAsync(runtimeEvent, entitiesByRuntimeActor);
                    break;
                case BattleEventKind.DamageApplied:
                    await PlayRuntimeDamageEventAsync(runtimeEvent, entitiesByRuntimeActor);
                    break;
            }
        }

        if (_unitRoot.HasPendingDefeatedPresentations)
        {
            await _unitRoot.WaitForDefeatedPresentationsAsync();
        }
    }

    private async Task WaitSiteBattlePresentationSeconds(double seconds)
    {
        if (!IsInsideTree() || seconds <= 0)
        {
            return;
        }

        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    }

    private static string BuildBattleGroupRuntimeReturnNotice(WorldActionResult applyResult, BattleReportRecord report)
    {
        string worldMessage = applyResult?.Message?.Trim() ?? "";
        string reportSummary = BuildBattleGroupRuntimeReportSummary(report).Trim();
        if (string.IsNullOrWhiteSpace(reportSummary))
        {
            return worldMessage;
        }

        if (string.IsNullOrWhiteSpace(worldMessage))
        {
            return reportSummary;
        }

        return $"{worldMessage}\n{reportSummary}";
    }

    private static string BuildBattleGroupRuntimeReportSummary(BattleReportRecord report)
    {
        if (report == null)
        {
            return "";
        }

        if (report.FailureCandidates.Count > 0)
        {
            return $"战斗结算未完成：{string.Join("，", report.FailureCandidates)}";
        }

        return report.OutcomeSummary switch
        {
            "NormalVictory" => "战斗胜利。战报已由运行时事件生成。",
            "NormalDefeat" => "战斗失败。战报已由运行时事件生成。",
            "PlayerRetreat" => "部队已撤退。战报已由运行时事件生成。",
            _ => string.IsNullOrWhiteSpace(report.OutcomeSummary)
                ? ""
                : $"战斗结束：{report.OutcomeSummary}"
        };
    }

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
            // preview and runtime start positions cannot drift apart. Only player
            // units expose drag behavior.
            SetDeploymentDragComponent(entity, placementId, fallbackFaction == BattleFaction.Player);
            _sitePlacementEntities[placementId] = entity;
        }
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

    private void ApplyBattleModifiers(BattleStartRequest request)
    {
        int towerSupportCount = request.BattleModifiers.Count(modifier => modifier.Type == "tower_support" && modifier.Uses > 0);
        if (towerSupportCount > 0)
        {
            int damage = towerSupportCount * 2;
            BattleEntity target = _unitRoot.GetEntitiesSnapshot()
                .FirstOrDefault(entity =>
                    entity.GetComponent<FactionComponent>()?.Faction == BattleFaction.Enemy &&
                    !BattleRuleQueries.IsDefeated(entity));
            if (target != null)
            {
                int applied = target.GetComponent<HealthComponent>()?.ApplyDamage(damage) ?? 0;
                if (BattleRuleQueries.IsDefeated(target))
                {
                    _unitRoot.MarkEntityDefeated(target);
                }

                GameLog.Info(nameof(WorldSiteRoot), $"Tower support applied target={target.EntityId} damage={applied} supports={towerSupportCount}");
            }
        }

        ApplyWorldBattlePhaseModifiers(request);
    }

    private void ApplyWorldBattlePhaseModifiers(BattleStartRequest request)
    {
        foreach (BattleModifier modifier in request.BattleModifiers.Where(modifier =>
                     modifier.Type == "world_battle_phase" && modifier.Uses > 0))
        {
            int playerDamage = modifier.Values.TryGetValue("player_damage", out int playerValue) ? playerValue : 0;
            int enemyDamage = modifier.Values.TryGetValue("enemy_damage", out int enemyValue) ? enemyValue : 0;
            if (playerDamage > 0)
            {
                ApplyWorldBattlePhaseDamage(BattleFaction.Player, playerDamage, modifier.SourceId);
            }

            if (enemyDamage > 0)
            {
                ApplyWorldBattlePhaseDamage(BattleFaction.Enemy, enemyDamage, modifier.SourceId);
            }
        }
    }

    private void ApplyWorldBattlePhaseDamage(BattleFaction faction, int damage, string sourceId)
    {
        if (damage <= 0)
        {
            return;
        }

        BattleEntity target = _unitRoot.GetEntitiesSnapshot()
            .FirstOrDefault(entity =>
                entity.GetComponent<FactionComponent>()?.Faction == faction &&
                !BattleRuleQueries.IsDefeated(entity));
        if (target == null)
        {
            return;
        }

        int applied = target.GetComponent<HealthComponent>()?.ApplyDamage(damage) ?? 0;
        if (BattleRuleQueries.IsDefeated(target))
        {
            _unitRoot.MarkEntityDefeated(target);
        }

        GameLog.Info(nameof(WorldSiteRoot), $"WorldBattlePhaseModifierApplied source={sourceId} target={target.EntityId} faction={faction} damage={applied}");
    }

    private void EnsureBattleRenderSortDomain()
    {
        YSortEnabled = true;

        if (_mapRoot is CanvasItem mapRootItem)
        {
            mapRootItem.YSortEnabled = true;
        }

        if (_unitRoot != null)
        {
            _unitRoot.YSortEnabled = true;
        }
    }

    private void PlaceBattleEntitiesOnGrid()
    {
        if (_activeSiteMap is not BattleMapView || _unitRoot == null)
        {
            GameLog.Warn(nameof(WorldSiteRoot), $"Skip entity placement activeSiteMapIsBattleMap={_activeSiteMap is BattleMapView} unitRoot={_unitRoot != null}");
            return;
        }

        if (_coordinateLayer == null)
        {
            GameLog.Warn(nameof(WorldSiteRoot), "Skip entity placement because coordinate layer is missing.");
            return;
        }

        int placedCount = 0;
        foreach (BattleEntity entity in _unitRoot.GetEntitiesSnapshot())
        {
            GridOccupantComponent gridOccupant = entity.GetComponent<GridOccupantComponent>();
            if (gridOccupant == null)
            {
                GameLog.Info(nameof(WorldSiteRoot), $"Entity has no grid occupant entity={entity.EntityId} name={entity.DisplayName}");
                continue;
            }

            ResolveEntitySurfaceHeight(gridOccupant);
            var cell = new Vector2I(gridOccupant.GridX, gridOccupant.GridY);
            entity.GlobalPosition = _coordinateLayer.ToGlobal(_coordinateLayer.MapToLocal(cell));
            ApplyEntityRenderSort(entity, gridOccupant.SurfacePosition);
            placedCount++;
            GameLog.Info(
                nameof(WorldSiteRoot),
                $"Placed entity id={entity.EntityId} name={entity.DisplayName} surface={gridOccupant.SurfacePosition} global={entity.GlobalPosition} {DescribeGridCell(gridOccupant.Position)} {DescribeGridSurface(gridOccupant.SurfacePosition)}");

            WarnIfEntityStartsOnInvalidSurface(entity, gridOccupant.SurfacePosition);
        }

        GameLog.Info(nameof(WorldSiteRoot), $"Entity placement complete count={placedCount}");
    }

    private void ResolveEntitySurfaceHeight(GridOccupantComponent gridOccupant)
    {
        if (gridOccupant == null || gridOccupant.UseExplicitHeight || _activeGridMap == null)
        {
            return;
        }

        if (_activeGridMap.TryGetTopSurfacePosition(gridOccupant.Position, out GridSurfacePosition topSurface))
        {
            gridOccupant.GridHeight = topSurface.Height;
            return;
        }

        if (_activeGridMap.TryGetCell(gridOccupant.Position, out GridCell cell))
        {
            gridOccupant.GridHeight = cell.Height;
        }
    }

    private void ApplyEntityRenderSort(BattleEntity entity, GridSurfacePosition surfacePosition)
    {
        if (entity == null)
        {
            return;
        }

        int zIndex = BattleRenderSortPolicy.GetUnitZIndex(surfacePosition.Height);
        bool suppressPresentationRaise = false;
        if (_activeSiteMap is BattleMapView battleMapView &&
            battleMapView.RenderSortCache?.TryGetYSortOriginUnitZIndex(surfacePosition, out int ySortOriginZIndex) == true)
        {
            zIndex = ySortOriginZIndex;
            suppressPresentationRaise = true;
        }

        BattleUnitPresentationComponent presentation = entity.GetComponent<BattleUnitPresentationComponent>();
        if (presentation != null)
        {
            presentation.SetMapSortZIndex(zIndex, suppressPresentationRaise);
            return;
        }

        entity.ZIndex = zIndex;
    }

    private string DescribeGridCell(GridPosition position)
    {
        if (_activeGridMap == null)
        {
            return "grid=missing";
        }

        if (!_activeGridMap.TryGetCell(position, out GridCell cell))
        {
            return "cell=missing";
        }

        string terrain = string.IsNullOrWhiteSpace(cell.TerrainTag) ? "-" : cell.TerrainTag;
        return $"height={cell.Height} terrain={terrain} walkable={cell.IsWalkable} moveCost={cell.MoveCost} foundation={cell.HasFoundation} obstacle={cell.IsObstacle}";
    }

    private string DescribeGridSurface(GridSurfacePosition position)
    {
        if (_activeGridMap == null)
        {
            return "surfaceGrid=missing";
        }

        if (!_activeGridMap.TryGetSurface(position, out GridCellSurface surface))
        {
            return "surface=missing";
        }

        string terrain = string.IsNullOrWhiteSpace(surface.TerrainTag) ? "-" : surface.TerrainTag;
        return $"surfaceTerrain={terrain} surfaceWalkable={surface.IsWalkable} surfaceMoveCost={surface.MoveCost} surfaceFoundation={surface.HasFoundation}";
    }

    private void WarnIfEntityStartsOnInvalidSurface(BattleEntity entity, GridSurfacePosition position)
    {
        if (_activeGridMap == null ||
            !_activeGridMap.TryGetSurface(position, out GridCellSurface surface) ||
            IsValidMovementDestination(entity, surface))
        {
            return;
        }

        GameLog.Warn(
            nameof(WorldSiteRoot),
            $"Entity starts on invalid movement surface id={entity.EntityId} name={entity.DisplayName} surface={position} {DescribeGridSurface(position)} nearest={DescribeNearestValidMovementSurfaces(entity, position, 5)}");
    }

    private string DescribeNearestValidMovementSurfaces(BattleEntity entity, GridSurfacePosition origin, int count)
    {
        ISet<GridSurfacePosition> blockedSurfaces = BuildBlockedMovementSurfaces(entity);
        GridSurfacePosition[] candidates = _activeGridMap.Surfaces.Values
            .Where(surface => !blockedSurfaces.Contains(surface.SurfacePosition) && IsValidMovementDestination(entity, surface))
            .OrderBy(surface => BattleRuleQueries.GetManhattanDistance(origin.Position, surface.Position))
            .ThenBy(surface => surface.Height)
            .ThenBy(surface => surface.Position.Y)
            .ThenBy(surface => surface.Position.X)
            .Take(count)
            .Select(surface => surface.SurfacePosition)
            .ToArray();

        return candidates.Length == 0
            ? "none"
            : string.Join(", ", candidates.Select(position => $"{position} {DescribeGridSurface(position)}"));
    }

    private bool IsValidMovementDestination(BattleEntity entity, GridCellSurface surface)
    {
        return surface is { IsWalkable: true, MoveCost: > 0 } &&
               _activeGridMap?.IsTopSurface(surface.SurfacePosition) == true &&
               BattleRuleQueries.CanEnterSurface(entity, surface);
    }

    private void ReconcileWorldSitePlacementsAfterBattle(
        BattleStartRequest request,
        IReadOnlyList<WorldSiteLivePlacementSnapshot> snapshots,
        BattleOutcome outcome)
    {
        if (request == null)
        {
            return;
        }

        StrategicWorldRuntime.EnsureInitialized();
        string siteId = ResolveRequestSiteId(request);
        if (string.IsNullOrWhiteSpace(siteId) ||
            StrategicWorldRuntime.State.SiteStates.TryGetValue(siteId, out WorldSiteState site) != true)
        {
            return;
        }

        WorldSiteDefinition definition = ResolveSiteDefinition(site.SiteId);
        if (definition == null)
        {
            return;
        }

        _deploymentService.EnsureGarrisonPlacements(site, definition);
        var usedSnapshotPlacementIds = new HashSet<string>();
        int matched = 0;
        int converted = 0;
        matched = ApplyLiveSnapshotsToMatchingPlacements(site, snapshots, usedSnapshotPlacementIds);
        converted = ApplyLiveSnapshotsToOwnerGarrisons(site, snapshots, usedSnapshotPlacementIds);
        EnsureSitePlacementsRespectTerrain(site, definition);
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"WorldSitePlacementsReconciledAfterBattle site={site.SiteId} request={request.RequestId} snapshots={snapshots?.Count ?? 0} matched={matched} converted={converted} remaining={site.UnitPlacements.Count}");
    }

    private static int ApplyLiveSnapshotsToMatchingPlacements(
        WorldSiteState site,
        IReadOnlyList<WorldSiteLivePlacementSnapshot> snapshots,
        ISet<string> usedSnapshotPlacementIds)
    {
        if (site == null || snapshots == null || snapshots.Count == 0)
        {
            return 0;
        }

        int updated = 0;
        foreach (WorldSiteLivePlacementSnapshot snapshot in snapshots)
        {
            WorldSiteUnitPlacement placement = site.UnitPlacements
                .FirstOrDefault(item => item.PlacementId == snapshot.PlacementId);
            if (placement == null)
            {
                continue;
            }

            ApplySnapshotToPlacement(placement, snapshot);
            if (WorldSiteDeploymentService.IsGarrisonPlacement(placement))
            {
                usedSnapshotPlacementIds?.Add(snapshot.PlacementId);
            }

            updated++;
        }

        return updated;
    }

    private int ApplyLiveSnapshotsToOwnerGarrisons(
        WorldSiteState site,
        IReadOnlyList<WorldSiteLivePlacementSnapshot> snapshots,
        ISet<string> usedSnapshotPlacementIds)
    {
        if (site == null || snapshots == null || snapshots.Count == 0)
        {
            return 0;
        }

        BattleFaction ownerFaction = ResolveBattleFaction(site.OwnerFactionId);
        int updated = 0;
        foreach (WorldSiteUnitPlacement placement in site.UnitPlacements
                     .Where(WorldSiteDeploymentService.IsGarrisonPlacement)
                     .OrderBy(item => item.UnitTypeId)
                     .ThenBy(item => item.UnitIndex))
        {
            WorldSiteLivePlacementSnapshot snapshot = snapshots
                .Where(item =>
                    item.Faction == ownerFaction &&
                    item.UnitTypeId == placement.UnitTypeId &&
                    usedSnapshotPlacementIds?.Contains(item.PlacementId) != true)
                .OrderBy(item => item.UnitIndex)
                .ThenBy(item => item.PlacementId)
                .FirstOrDefault();
            if (snapshot == null)
            {
                continue;
            }

            ApplySnapshotToPlacement(placement, snapshot);
            usedSnapshotPlacementIds?.Add(snapshot.PlacementId);
            updated++;
        }

        return updated;
    }

    private static void ApplySnapshotToPlacement(
        WorldSiteUnitPlacement placement,
        WorldSiteLivePlacementSnapshot snapshot)
    {
        placement.CellX = snapshot.CellX;
        placement.CellY = snapshot.CellY;
        placement.CellHeight = snapshot.CellHeight;
    }

    private ISet<GridSurfacePosition> BuildBlockedMovementSurfaces(BattleEntity movingEntity)
    {
        return _unitRoot?.BuildBlockedMovementSurfaces(movingEntity) ?? new HashSet<GridSurfacePosition>();
    }

    private bool TryGetMouseGridPosition(out GridPosition position)
    {
        position = default;

        if (_coordinateLayer == null || _activeGridMap == null)
        {
            return false;
        }

        Vector2I tilePosition = _coordinateLayer.LocalToMap(_coordinateLayer.ToLocal(GetGlobalMousePosition()));
        position = new GridPosition(tilePosition.X, tilePosition.Y);
        return _activeGridMap.TryGetCell(position, out _);
    }

    private bool TryGetCellGlobalPosition(GridPosition position, out Vector2 globalPosition)
    {
        globalPosition = default;

        if (_coordinateLayer == null)
        {
            return false;
        }

        var cell = new Vector2I(position.X, position.Y);
        globalPosition = _coordinateLayer.ToGlobal(_coordinateLayer.MapToLocal(cell));
        return true;
    }

    public BattleEntity FindEntityAt(GridPosition position)
    {
        return _unitRoot?.FindEntityAt(position);
    }

    private WorldActionResult ApplyBattleResultToWorld(BattleStartRequest request, BattleResult battleResult)
    {
        if (request == null || battleResult == null || battleResult.BattleKind == BattleKind.Unknown)
        {
            return new WorldActionResult
            {
                Success = true,
                ActionId = "battle_result",
                Message = "战斗已结束，当前战斗没有绑定战略世界结算。"
            };
        }

        StrategicWorldRuntime.EnsureInitialized();
        WorldActionResult applyResult = _worldBattleResultApplier.Apply(
            StrategicWorldRuntime.State,
            StrategicWorldRuntime.Definition,
            request,
            battleResult);
        StrategicWorldRuntime.LastNotice = applyResult.Message;
        return applyResult;
    }
}
