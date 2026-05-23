using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Reports;
using Rpg.Application.Battle.Snapshots;
using Rpg.Application.World;
using Rpg.Definitions.Battle;
using Rpg.Definitions.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle;
using Rpg.Presentation.Battle.Actions;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Flow;
using Rpg.Presentation.Battle.Rules;
using Rpg.Runtime.Battle;
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
        ApplyBattleRequestForceFootprints(request);
        if (request.BattleKind is BattleKind.AssaultSite or BattleKind.FieldIntercept)
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

        GameLog.Info(
            nameof(WorldSiteRoot),
            $"Battle request consumed kind={request.BattleKind} target={request.TargetSiteId} playerForces={request.PlayerForces.Count} enemyForces={request.EnemyForces.Count} modifiers={request.BattleModifiers.Count}");
    }

    private bool ActivateBattleRuntime()
    {
        _deploymentZoneOverlay?.ClearZones();
        if (!string.IsNullOrWhiteSpace(_battleStartBlockedReason))
        {
            GameLog.Warn(nameof(WorldSiteRoot), $"Battle runtime activation blocked reason={_battleStartBlockedReason}");
            SetBattleRuntimeEnabled(false);
            return false;
        }

        if (BattleSessionHandoff.TryPeekActiveRequest(out BattleStartRequest request))
        {
            _battleRuntimeRequest = request;
            ApplyBattleNavigationSnapshot(request);
        }

        _isBattlePreparationActive = false;
        _battlePreparationRequest = null;
        _battleRuntimeCommandPauseActive = false;
        _selectedBattleRuntimeGroupKey = "";
        _battlePerformanceCounters.Reset();
        // Preparation can start runtime directly after the player confirms deployment,
        // so this boundary must own the UI transition instead of relying on launch callbacks.
        SetBattleRuntimeEnabled(true);
        BindBattleRuntimeHud();
        return ActivateBattleGroupRuntime();
    }

    private void ApplyBattleNavigationSnapshot(BattleStartRequest request)
    {
        int syncedPlacementHeights = BattleNavigationSnapshotBuilder.SyncPreferredPlacementHeightsToCurrentNavigationSurfaces(
            request,
            _activeGridMap,
            ResolveForceCanEnterWater);
        BattleNavigationSnapshotBuilder.ApplyToRequest(request, _activeGridMap);
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"Battle navigation snapshot applied request={request?.RequestId ?? ""} surfaces={request?.NavigationSurfaces.Count ?? 0} connections={request?.NavigationConnections.Count ?? 0} syncedPlacementHeights={syncedPlacementHeights}");
    }

    private void BindBattleRuntimeHud()
    {
        // Battle commands live in the post-start HUD so deployment keeps Start
        // Battle as the primary action and the bottom bar can reserve viewport space.
        SetBattlePreparationContentVisible(false);
        _selectedFacilitySlotId = "";
        if (_siteHudRoot != null)
        {
            _siteHudRoot.Visible = true;
            ApplySiteHudFullRect("battle_runtime_hud");
        }

        if (_siteHudTopBar != null)
        {
            _siteHudTopBar.Visible = false;
        }

        if (_sitePeacetimePanel != null)
        {
            _sitePeacetimePanel.Visible = false;
        }

        UpdateSitePeacetimePanelVisibility("battle_runtime");
        ShowBattleRuntimeCommandHud(runtimeLocked: true);
        UpdateMainWorldViewportLayout("battle_runtime_hud");
    }

    private void ShowBattleRuntimeCommandHud(bool runtimeLocked)
    {
        if (_siteBottomCommandHost != null)
        {
            _siteBottomCommandHost.Visible = true;
        }

        if (_battleRuntimeCommandBar != null)
        {
            _battleRuntimeCommandBar.Visible = true;
        }

        RefreshBattleRuntimeCommandControls(runtimeLocked);
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"BattleRuntimeCommandHudShown locked={runtimeLocked} bottomVisible={_siteBottomCommandHost?.Visible == true} commandVisible={_battleRuntimeCommandBar?.Visible == true}");
    }

    private bool ActivateBattleGroupRuntime()
    {
        if (!_battleGroupRuntimeAdapter.TryStartActiveBattle(out WorldSiteBattleGroupRuntimeResolveResult resolution))
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
            await AdvanceBattleGroupRuntimeOnLiveClockAsync(resolution);
        }
        catch (System.Exception ex)
        {
            GameLog.Warn(nameof(WorldSiteRoot), $"Battle runtime presentation failed request={resolution?.Request?.RequestId ?? ""} error={ex.Message}");
        }

        resolution = _battleGroupRuntimeAdapter.CompleteResolvedBattle(resolution);
        if (resolution?.Success != true)
        {
            _battleStartBlockedReason = string.IsNullOrWhiteSpace(resolution?.FailureReason)
                ? "battle_group_runtime_completion_failed"
                : resolution.FailureReason;
            GameLog.Warn(nameof(WorldSiteRoot), $"Battle group runtime completion blocked reason={_battleStartBlockedReason}");
            ClearBattleEntities();
            SetBattleRuntimeEnabled(false);
            return;
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

    private double ResolveRuntimePlaybackTickSeconds()
    {
        return BattleActionTimingPolicy.DefaultSimulationTickSeconds;
    }

    private async Task WaitSiteBattlePresentationSeconds(double seconds)
    {
        if (!IsInsideTree() || seconds <= 0)
        {
            return;
        }

        double remainingSeconds = seconds;
        while (IsInsideTree() && remainingSeconds > 0)
        {
            bool loggedPauseWait = false;
            while (_battleRuntimeCommandPauseActive && IsInsideTree())
            {
                if (!loggedPauseWait)
                {
                    GameLog.Info(nameof(WorldSiteRoot), "BattleRuntimePresentationWaitPaused");
                    loggedPauseWait = true;
                }

                await ToSignal(GetTree().CreateTimer(0.05), SceneTreeTimer.SignalName.Timeout);
            }

            double stepSeconds = System.Math.Min(0.05, remainingSeconds);
            await ToSignal(GetTree().CreateTimer(stepSeconds), SceneTreeTimer.SignalName.Timeout);
            if (!_battleRuntimeCommandPauseActive)
            {
                remainingSeconds -= stepSeconds;
            }
        }
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
            if (TryGetFootprintCenterGlobalPosition(
                    gridOccupant.Position,
                    new Vector2I(gridOccupant.FootprintWidth, gridOccupant.FootprintHeight),
                    out Vector2 globalPosition))
            {
                entity.GlobalPosition = globalPosition;
            }
            else
            {
                entity.GlobalPosition = _coordinateLayer.ToGlobal(_coordinateLayer.MapToLocal(cell));
            }

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

        Vector2I tilePosition = _coordinateLayer.LocalToMap(_coordinateLayer.ToLocal(GetWorldViewportMousePosition()));
        position = new GridPosition(tilePosition.X, tilePosition.Y);
        return _activeGridMap.TryGetCell(position, out _);
    }

    private bool TryResolveMouseFootprintAnchor(Vector2I footprintSize, out GridPosition position)
    {
        return TryResolveFootprintAnchorAtWorldPosition(
            GetWorldViewportMousePosition(),
            footprintSize,
            out position);
    }

    private bool TryResolveFootprintAnchorAtWorldPosition(
        Vector2 globalPosition,
        Vector2I footprintSize,
        out GridPosition position)
    {
        position = default;
        if (_coordinateLayer == null ||
            _activeGridMap == null ||
            !TryResolveCellCenterCoordinates(globalPosition, out float centerX, out float centerY))
        {
            return false;
        }

        position = BattleFootprintCells.ResolveAnchorFromCenter(
            centerX,
            centerY,
            footprintSize.X,
            footprintSize.Y);
        return _activeGridMap.TryGetCell(position, out _);
    }

    private bool TryResolveCellCenterCoordinates(Vector2 globalPosition, out float cellX, out float cellY)
    {
        cellX = 0f;
        cellY = 0f;
        if (_coordinateLayer == null)
        {
            return false;
        }

        Vector2 localPosition = _coordinateLayer.ToLocal(globalPosition);
        Vector2 origin = _coordinateLayer.MapToLocal(Vector2I.Zero);
        Vector2 stepX = _coordinateLayer.MapToLocal(new Vector2I(1, 0)) - origin;
        Vector2 stepY = _coordinateLayer.MapToLocal(new Vector2I(0, 1)) - origin;
        float determinant = (stepX.X * stepY.Y) - (stepX.Y * stepY.X);
        if (Mathf.Abs(determinant) <= 0.001f)
        {
            return false;
        }

        Vector2 delta = localPosition - origin;
        cellX = ((delta.X * stepY.Y) - (delta.Y * stepY.X)) / determinant;
        cellY = ((stepX.X * delta.Y) - (stepX.Y * delta.X)) / determinant;
        return true;
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

    private bool TryGetFootprintCenterGlobalPosition(
        GridPosition anchor,
        Vector2I footprintSize,
        out Vector2 globalPosition)
    {
        globalPosition = default;
        if (_coordinateLayer == null)
        {
            return false;
        }

        int width = BattleFootprintCells.NormalizeSize(footprintSize.X);
        int height = BattleFootprintCells.NormalizeSize(footprintSize.Y);
        var anchorCell = new Vector2I(anchor.X, anchor.Y);
        Vector2 anchorLocal = _coordinateLayer.MapToLocal(anchorCell);
        Vector2 stepX = _coordinateLayer.MapToLocal(new Vector2I(anchor.X + 1, anchor.Y)) - anchorLocal;
        Vector2 stepY = _coordinateLayer.MapToLocal(new Vector2I(anchor.X, anchor.Y + 1)) - anchorLocal;
        Vector2 centerLocal = anchorLocal +
                              stepX * ((width - 1) * 0.5f) +
                              stepY * ((height - 1) * 0.5f);
        globalPosition = _coordinateLayer.ToGlobal(centerLocal);
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
