using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Reports;
using Rpg.Application.Battle.Snapshots;
using Rpg.Application.StrategicBattleBridge;
using Rpg.Application.StrategicManagement;
using Rpg.Application.World;
using Rpg.Definitions.Battle;
using Rpg.Definitions.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.StrategicManagement;
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
        if (_unitRoot == null || !TryResolveActiveBattleRequest(out BattleStartRequest request))
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

            if (!_isBattlePreparationActive)
            {
                RefreshBattleRequestMapEntitiesForDirectRuntime(request);
            }
            else
            {
                ClearBattleEntities();
            }
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

        if (TryResolveActiveBattleRequest(out BattleStartRequest request))
        {
            _battleRuntimeRequest = request;
            ApplyBattleNavigationSnapshot(request);
            AlignBattlePresentationEntityIdsToRuntime(request);
        }

        _isBattlePreparationActive = false;
        _battlePreparationRequest = null;
        SetBattleRuntimeCommandPauseActive(false, "runtime_activated");
        _selectedBattleRuntimeGroupKey = "";
        _selectedBattleRuntimeGroupKeys.Clear();
        _battleRuntimeDestinationBeaconCommandSequence = 0;
        _battlePerformanceCounters.Reset();
        // Preparation can start runtime directly after the player confirms deployment,
        // so this boundary must own the UI transition instead of relying on launch callbacks.
        SetBattleRuntimeEnabled(true);
        BindBattleRuntimeHud();
        bool runtimeActivated = ActivateBattleGroupRuntime();
        if (runtimeActivated)
        {
            if (ShouldPlayBattleMovementTweenProbe())
            {
                PlayBattleMovementTweenProbe();
            }
        }

        return runtimeActivated;
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
        // Battle runtime owns a fullscreen battlefield. Persistent commands stay in
        // the authored bottom HUD frame and must not re-open the management panel.
        SetBattlePreparationHudVisible(false);
        if (_siteHudRoot != null)
        {
            _siteHudRoot.Visible = true;
            ApplySiteHudFullRect("battle_runtime_hud");
        }

        if (_sitePeacetimePanel != null)
        {
            _sitePeacetimePanel.Visible = false;
        }

        UpdateSitePeacetimePanelVisibility("battle_runtime");
        ShowBattleRuntimeCommandHud(runtimeLocked: true);
        RefreshBattleRuntimeDestinationBeaconOverlays();
        UpdateMainWorldViewportLayout("battle_runtime_hud");
    }

    private void ShowBattleRuntimeCommandHud(bool runtimeLocked)
    {
        if (_siteBottomCommandHost != null)
        {
            _siteBottomCommandHost.Visible = true;
        }

        if (_battleRuntimeSummaryBar != null)
        {
            _battleRuntimeSummaryBar.Visible = true;
        }

        if (_battleRuntimeCommandBar != null)
        {
            _battleRuntimeCommandBar.Visible = _battleRuntimeCommandPauseActive;
        }

        RefreshBattleRuntimeCommandControls(runtimeLocked);
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"BattleRuntimeCommandHudShown locked={runtimeLocked} bottomVisible={_siteBottomCommandHost?.Visible == true} summaryVisible={_battleRuntimeSummaryBar?.Visible == true} commandVisible={_battleRuntimeCommandBar?.Visible == true}");
    }

    private bool ActivateBattleGroupRuntime()
    {
        if (!TryResolveActiveBattleContext(out StrategicBattleActiveContext activeContext))
        {
            _battleStartBlockedReason = "strategic_battle_active_context_required";
            GameLog.Warn(nameof(WorldSiteRoot), $"Battle group runtime activation blocked reason={_battleStartBlockedReason}");
            SetBattleRuntimeEnabled(false);
            return false;
        }

        bool started = _battleGroupRuntimeAdapter.TryStartActiveBattle(
            activeContext,
            out WorldSiteBattleGroupRuntimeResolveResult resolution);
        if (!started)
        {
            _battleStartBlockedReason = string.IsNullOrWhiteSpace(resolution?.FailureReason)
                ? "battle_group_runtime_activation_failed"
                : resolution.FailureReason;
            GameLog.Warn(nameof(WorldSiteRoot), $"Battle group runtime activation blocked reason={_battleStartBlockedReason}");
            SetBattleRuntimeEnabled(false);
            return false;
        }

        _activeBattleGroupRuntimeResolution = resolution;
        AlignBattlePresentationEntityIdsToRuntime(resolution.Request, resolution.Snapshot);
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
            // Failed live-clock handoff has no accepted settlement facts; do not write it back.
            _battleStartBlockedReason = "battle_group_runtime_presentation_failed";
            GameLog.Warn(nameof(WorldSiteRoot), $"Battle runtime presentation failed request={resolution?.Request?.RequestId ?? ""} reason={_battleStartBlockedReason} error={ex.Message}");
            _activeBattleGroupRuntimeResolution = null; ClearBattleEntities(); SetBattleRuntimeEnabled(false);
            CancelActiveBattleLaunch(_battleStartBlockedReason);
            return;
        }
        resolution = resolution?.ActiveContext != null
            ? _battleGroupRuntimeAdapter.CompleteResolvedBattle(resolution, resolution.ActiveContext)
            : _battleGroupRuntimeAdapter.CompleteResolvedBattle(resolution);
        if (resolution?.Success != true)
        {
            _battleStartBlockedReason = string.IsNullOrWhiteSpace(resolution?.FailureReason)
                ? "battle_group_runtime_completion_failed"
                : resolution.FailureReason;
            GameLog.Warn(nameof(WorldSiteRoot), $"Battle group runtime completion blocked reason={_battleStartBlockedReason}");
            _activeBattleGroupRuntimeResolution = null;
            ClearBattleEntities();
            SetBattleRuntimeEnabled(false);
            return;
        }

        _activeBattleGroupRuntimeResolution = null;
        BattleOutcome outcome = resolution.BattleResult?.Outcome ?? BattleOutcome.None;
        applyResult = resolution.ActiveContext != null
            ? ApplyStrategicBattleResultToWorld(resolution.ActiveContext, resolution.BattleResult)
            : ApplyBattleResultToWorld(resolution.Request, resolution.BattleResult);
        string battleNotice = BuildBattleGroupRuntimeReturnNotice(applyResult, resolution.Report, resolution.Request);
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
            outcome);
        ClearBattleEntities();
        SetBattleRuntimeEnabled(false);
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"Battle group runtime resolved request={resolution.Request?.RequestId ?? ""} outcome={resolution.BattleResult?.Outcome} reportEvents={resolution.Report?.SourceEventIds.Count ?? 0} failure={string.Join(",", resolution.Report?.FailureCandidates ?? new List<string>())}");

        string returnScenePath = ResolveBattleResultReturnScenePath(resolution.Request?.ReturnScenePath ?? "");
        ShowPostBattleSettlementDialog(
            outcome,
            resolution.Request,
            applyResult,
            returnScenePath);
    }

    private static string ResolveBattleResultReturnScenePath(string returnScenePath)
    {
        return string.IsNullOrWhiteSpace(returnScenePath)
            ? "res://scenes/world/StrategicWorldRoot.tscn"
            : returnScenePath;
    }

    private double ResolveRuntimePlaybackTickSeconds() => BattleActionTimingPolicy.DefaultSimulationTickSeconds;
    private async Task WaitSiteBattlePresentationSeconds(double seconds)
    {
        await BattlePresentationClockWaiter.WaitSecondsAsync(
            this,
            seconds,
            () => _battleRuntimeCommandPauseActive,
            "BattleRuntimePresentationWaitPaused");
    }

    private async Task WaitForBattleRuntimeAdvanceGateAsync()
    {
        bool loggedPauseWait = false;
        while (_battleRuntimeCommandPauseActive && IsInsideTree())
        {
            if (!loggedPauseWait)
            {
                GameLog.Info(nameof(WorldSiteRoot), "BattleRuntimePresentationWaitPaused");
                loggedPauseWait = true;
            }

            await ToSignal(GetTree().CreateTimer(0.05, processAlways: true), SceneTreeTimer.SignalName.Timeout);
        }
    }

    private static string BuildBattleGroupRuntimeReturnNotice(
        WorldActionResult applyResult,
        BattleReportRecord report,
        BattleStartRequest request)
    {
        string worldMessage = applyResult?.Message?.Trim() ?? "";
        string reportSummary = BuildBattleGroupRuntimeReportSummary(report).Trim();
        List<string> lines = new();
        if (!string.IsNullOrWhiteSpace(worldMessage))
        {
            lines.Add(worldMessage);
        }

        if (!string.IsNullOrWhiteSpace(reportSummary))
        {
            lines.Add(reportSummary);
        }

        return string.Join("\n", lines);
    }

    private static string BuildStrategicBattleFeedbackReturnNotice(StrategicBattleFeedbackRecord feedback)
    {
        if (feedback == null || string.IsNullOrWhiteSpace(feedback.FeedbackId))
        {
            return "";
        }

        List<string> lines = new()
        {
            "战略反馈",
            string.IsNullOrWhiteSpace(feedback.WorldChangeText)
                ? $"战斗结果：{feedback.OutcomeText}"
                : feedback.WorldChangeText
        };

        if (!string.IsNullOrWhiteSpace(feedback.FailureReasonText))
        {
            lines.Add($"失利原因：{feedback.FailureReasonText}");
        }

        if (feedback.RewardLines.Count > 0)
        {
            lines.Add($"奖励：{string.Join("；", feedback.RewardLines)}");
        }

        if (feedback.ParticipantFeedback.Count > 0)
        {
            lines.Add($"编制损失：{string.Join("；", feedback.ParticipantFeedback.Select(item => item.ResultText))}");
        }

        if (feedback.HeroFeedback.Count > 0)
        {
            lines.Add($"英雄反馈：{string.Join("；", feedback.HeroFeedback.Select(item => item.ReactionText))}");
        }

        IReadOnlyList<StrategicEquipmentSampleFeedbackRecord> visibleEquipment = feedback.EquipmentSamples
            .Where(item => item.IsReward || !string.IsNullOrWhiteSpace(item.RoleText))
            .ToList();
        if (visibleEquipment.Count > 0)
        {
            lines.Add($"装备：{string.Join("；", visibleEquipment.Select(FormatEquipmentFeedback))}");
        }

        if (!string.IsNullOrWhiteSpace(feedback.ProgressionText))
        {
            lines.Add(feedback.ProgressionText);
        }

        return string.Join("\n", lines);
    }

    private static string FormatEquipmentFeedback(StrategicEquipmentSampleFeedbackRecord equipment)
    {
        string prefix = equipment.IsReward ? "获得" : "样本";
        string slot = equipment.SlotKind switch
        {
            "weapon" => "武器",
            "armor" => "护甲",
            "token" => "号令道具",
            _ => equipment.SlotKind ?? ""
        };
        return $"{prefix}{slot}：{equipment.DisplayName}。{equipment.RoleText}";
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
            if (PlaceBattleEntityOnGridResolved(entity))
            {
                placedCount++;
            }
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

    public void SetHoveredBattleRuntimeEntity(string entityId)
    {
        string normalizedEntityId = entityId?.Trim() ?? "";
        if (string.Equals(_hoveredBattleRuntimeEntityId, normalizedEntityId, System.StringComparison.Ordinal))
        {
            return;
        }

        _hoveredBattleRuntimeEntityId = normalizedEntityId;
        if (string.IsNullOrWhiteSpace(normalizedEntityId))
        {
            _unitRoot?.ClearHoverPreview();
            return;
        }

        _unitRoot?.SetHoverPreviewByEntityId(normalizedEntityId);
    }

    private WorldActionResult ApplyStrategicBattleResultToWorld(StrategicBattleActiveContext context, BattleResult compatibilityResult)
    {
        StrategicWorldRuntime.EnsureInitialized();
        string bridgeFailureReason = StrategicBattleBridgeService.GetActiveContextFailureReason(context);
        if (!string.IsNullOrWhiteSpace(bridgeFailureReason))
        {
            GameLog.Warn(
                nameof(WorldSiteRoot),
                $"Strategic battle active context result rejected context={context?.ContextId ?? ""} reason={bridgeFailureReason}");
            return new WorldActionResult
            {
                Success = false,
                ActionId = "battle_result",
                Message = "战斗结果与战略战斗上下文不匹配，已阻止回写。"
            };
        }

        StrategicManagementRuntime.EnsureInitialized();
        StrategicBattleBridgeService bridge = new(StrategicManagementRuntime.Definitions);
        StrategicBattleResultSummary summary = bridge.BuildResultSummary(context);
        StrategicBattleSettlementCommitResult commitResult = StrategicManagementRuntime.CommitBattleResult(context, summary);
        StrategicCommandResult strategicResult = commitResult.CommandResult;
        if (!commitResult.Success || strategicResult?.Success != true)
        {
            GameLog.Warn(
                nameof(WorldSiteRoot),
                $"Strategic battle active context result rejected context={context.ContextId} expedition={context.Session?.ExpeditionId ?? ""} reason={commitResult.FailureReason}");
            return new WorldActionResult
            {
                Success = false,
                ActionId = "battle_result",
                Message = "战斗结果无法写回战略经营。"
            };
        }

        StrategicBattleFeedbackRecord strategicFeedback = null;
        if (!string.IsNullOrWhiteSpace(strategicResult.CreatedEntityId))
        {
            StrategicManagementRuntime.State.BattleFeedbackRecords.TryGetValue(
                strategicResult.CreatedEntityId,
                out strategicFeedback);
        }

        string strategicNotice = BuildStrategicBattleFeedbackReturnNotice(strategicFeedback);
        WorldActionResult applyResult = new()
        {
            Success = true,
            ActionId = "battle_result",
            Message = string.IsNullOrWhiteSpace(strategicNotice)
                ? "战斗结果已写回战略经营。"
                : strategicNotice
        };
        ApplyStrategicBattleResultPresentationCleanup(context.CompatibilityRequest, applyResult, summary);
        ApplyStrategicBattleResultWorldArmyCarrierCleanup(context.CompatibilityRequest, applyResult);
        StrategicWorldRuntime.LastNotice = applyResult.Message;
        context.CompatibilityResult = compatibilityResult;
        ClearLegacyStrategicBattleHandoff("result_consumed");
        _activeStrategicBattleContext = null;
        return applyResult;
    }

    private WorldActionResult ApplyBattleResultToWorld(BattleStartRequest request, BattleResult battleResult)
    {
        if (!string.IsNullOrWhiteSpace(request?.StrategicExpeditionId))
        {
            GameLog.Warn(
                nameof(WorldSiteRoot),
                $"Strategic battle reached legacy result applier expedition={request.StrategicExpeditionId} request={request.RequestId}");
            return new WorldActionResult
            {
                Success = false,
                ActionId = "battle_result",
                Message = "战斗结果缺少战略战斗上下文，已阻止回写。"
            };
        }

        return ApplyLegacyBattleResultToWorld(request, battleResult);
    }

    private WorldActionResult ApplyLegacyBattleResultToWorld(BattleStartRequest request, BattleResult battleResult)
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
        // Legacy compatibility is now scoped to non-Strategic battle requests. The
        // public wrapper rejects Strategic Management requests before reaching here.
        WorldActionResult applyResult = _worldBattleResultApplier.Apply(
            StrategicWorldRuntime.State,
            StrategicWorldRuntime.Definition,
            request,
            battleResult);

        StrategicWorldRuntime.LastNotice = applyResult.Message;
        return applyResult;
    }

    private void ApplyStrategicBattleResultPresentationCleanup(BattleStartRequest request, WorldActionResult result, StrategicBattleResultSummary summary)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.SourceArmyId))
        {
            return;
        }

        string siteId = ResolveRequestSiteId(request);
        if (string.IsNullOrWhiteSpace(siteId) ||
            StrategicWorldRuntime.State.SiteStates.TryGetValue(siteId, out WorldSiteState site) != true)
        {
            return;
        }

        // Strategic Management owns the battle result facts. This legacy-site cleanup is
        // limited to presentation leftovers created by the current map/battle handoff.
        int removedPlacements = site.UnitPlacements.RemoveAll(placement =>
            placement != null &&
            string.Equals(placement.SourceKind, "PlayerArmy", System.StringComparison.Ordinal) &&
            (string.Equals(placement.SourceId, request.SourceArmyId, System.StringComparison.Ordinal) ||
             string.Equals(placement.ArmyId, request.SourceArmyId, System.StringComparison.Ordinal)) &&
            placement.PlacementKind is WorldSiteUnitPlacementKind.VisitingArmy or WorldSiteUnitPlacementKind.Attacker);
        int removedLegacyGarrisonUnits = new WorldSiteBattleUnitPoolService()
            .RemoveImportedArmyForSiteBattle(site, request.SourceArmyId);
        int removedDefenderPlacements = 0;
        if (summary != null &&
            summary.Outcome == BattleOutcome.Victory &&
            summary.ObjectiveSucceeded)
        {
            // Strategic Management has already applied ownership. This only prevents
            // stale legacy defender placements from being redrawn after victory.
            removedDefenderPlacements = site.UnitPlacements.RemoveAll(placement =>
                placement != null &&
                placement.PlacementKind is WorldSiteUnitPlacementKind.Defender or WorldSiteUnitPlacementKind.Garrison &&
                !string.Equals(placement.FactionId, StrategicWorldIds.FactionPlayer, System.StringComparison.Ordinal) &&
                !string.Equals(placement.SourceKind, "PlayerArmy", System.StringComparison.Ordinal));
        }

        if (site.SiteMode == WorldSiteMode.Wartime)
        {
            WorldSiteModeTransitionService.AddEvent(
                result,
                _siteModeTransitions.EnterAftermath(
                    site,
                    StrategicWorldRuntime.State.WorldTick,
                    "strategic_battle_result_cleanup",
                    request.RequestId));
        }

        if (removedPlacements > 0 || removedLegacyGarrisonUnits > 0 || removedDefenderPlacements > 0)
        {
            GameLog.Info(
                nameof(WorldSiteRoot),
                $"StrategicBattlePresentationCleanup site={site.SiteId} army={request.SourceArmyId} removedPlacements={removedPlacements} removedLegacyGarrisonUnits={removedLegacyGarrisonUnits} removedDefenderPlacements={removedDefenderPlacements}");
        }
    }

    private void ApplyStrategicBattleResultWorldArmyCarrierCleanup(BattleStartRequest request, WorldActionResult result)
    {
        if (request == null ||
            string.IsNullOrWhiteSpace(request.SourceArmyId) ||
            string.IsNullOrWhiteSpace(request.StrategicExpeditionId))
        {
            return;
        }

        // Strategic Management has already resolved the expedition. The matching world
        // army is only the temporary map movement/battle-entry carrier and must not
        // stay in Attacking state where it can reopen the same resolved battle.
        WorldArmyCommandResult carrierResult = _armyCommandService.RemoveResolvedStrategicExpeditionCarrier(
            StrategicWorldRuntime.State?.ArmyStates,
            request.SourceArmyId,
            request.StrategicExpeditionId,
            "strategic_battle_result_cleanup");

        if (!carrierResult.Success)
        {
            GameLog.Warn(
                nameof(WorldSiteRoot),
                $"StrategicBattleWorldArmyCarrierCleanupRejected army={request.SourceArmyId} expedition={request.StrategicExpeditionId} reason={carrierResult.FailureReason}");
            return;
        }

        if (result != null && carrierResult.Events.Count > 0)
        {
            result.Events.AddRange(carrierResult.Events);
        }
    }
}
