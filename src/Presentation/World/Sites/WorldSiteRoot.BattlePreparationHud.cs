using System.Collections.Generic;
using System.Linq;
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
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Preview;
using Rpg.Presentation.Battle.Rules;
using Rpg.Presentation.Common;
using Rpg.Presentation.World;

namespace Rpg.Presentation.World.Sites;

public partial class WorldSiteRoot
{
    private void EnterBattlePreparation()
    {
        if (!BattleSessionHandoff.TryPeekActiveRequest(out BattleStartRequest request))
        {
            return;
        }

        // V0.1 keeps the prepared battle request active, renders its units, and lets the
        // player adjust player-side placements before committing to the auto runtime.
        _isBattlePreparationActive = true;
        _battlePreparationRequest = request;
        SetBattleRuntimeEnabled(false, keepBattlePresentation: true);
        StrategicWorldRuntime.EnsureInitialized();
        _siteHudSiteId = ResolveRequestSiteId(request);
        _siteHudReturnScenePath = string.IsNullOrWhiteSpace(request.ReturnScenePath)
            ? "res://scenes/world/StrategicWorldRoot.tscn"
            : request.ReturnScenePath;
        _selectedPlacementId = "";
        _selectedFacilitySlotId = "";
        ClearPlayerBattlePreparationPlacements(request);

        if (_returnMapButton != null)
        {
            _returnMapButton.Disabled = true;
            _returnMapButton.TooltipText = "战前部署中不能返回大地图，请先开战完成本次战斗。";
        }

        if (_siteHudRoot != null)
        {
            _siteHudRoot.Visible = true;
            ApplySiteHudFullRect("battle_preparation");
        }

        RefreshBattlePreparationUi("拖动我方单位调整部署，确认后点击开战。");
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"BattlePreparationEntered request={request.RequestId} site={_siteHudSiteId} playerForces={request.PlayerForces.Count} enemyForces={request.EnemyForces.Count}");
    }

    private void RefreshBattlePreparationUi(string notice = "")
    {
        BindBattlePreparationPanel(notice);
    }

    private void BindBattlePreparationPanel(string notice = "")
    {
        if (!_isBattlePreparationActive)
        {
            return;
        }

        StrategicWorldRuntime.EnsureInitialized();
        WorldSiteState site = ResolveSiteState(_siteHudSiteId);
        WorldSiteDefinition definition = ResolveSiteDefinition(_siteHudSiteId);
        EnsureSitePlacementsRespectTerrain(site, definition);

        _siteHudTitle.Text = $"{ResolveSiteName(_siteHudSiteId)} · 战前部署";
        _siteResourceLabel.Text = BuildResourceLine();
        _siteHudBody.Text = BuildBattlePreparationOverview(site, definition);
        _siteSelectionLabel.Text = BuildBattlePreparationSelectionText();
        _siteNoticeLabel.Text = string.IsNullOrWhiteSpace(notice) ? StrategicWorldRuntime.LastNotice : notice.Trim();

        SetBattlePreparationContentVisible(true);

        ClearChildren(_siteFacilityList);
        AddMutedLine(_siteFacilityList, "战前部署中不能建造或调整建筑。");
        RefreshBattlePreparationForceList();
        ClearChildren(_siteThreatList);
        AddMutedLine(_siteThreatList, "部署完成后将进入实时战斗。");
        RefreshBattlePreparationActions();
        ShowBattlePreparationDeploymentZone();
        RefreshBattlePreparationMapEntities();
        UpdateSitePeacetimePanelVisibility("battle_preparation_refresh");
    }

    private void ShowBattlePreparationDeploymentZone()
    {
        IEnumerable<GridPosition> cells = _deploymentCache?
            .GetCandidates(_battlePreparationRequest?.AttackDirection ?? WorldSiteAttackDirection.Any)
            .Select(candidate => new GridPosition(candidate.Cell.X, candidate.Cell.Y)) ??
            System.Array.Empty<GridPosition>();
        _highlightOverlay?.SetCells(BattleGridHighlightKind.FriendlyMove, cells);
    }

    private string BuildBattlePreparationOverview(WorldSiteState site, WorldSiteDefinition definition)
    {
        string siteName = definition?.DisplayName ?? site?.SiteId ?? "目标地点";
        int playerPlacementCount = CountPreparedPlacements(_battlePreparationRequest?.PlayerForces);
        int enemyPlacementCount = CountPreparedPlacements(_battlePreparationRequest?.EnemyForces);
        return string.Join("\n", new[]
        {
            $"{siteName}即将发生战斗。",
            "左键拖动我方单位可调整部署位置。",
            $"我方部署：{playerPlacementCount}    敌方部署：{enemyPlacementCount}"
        });
    }

    private string BuildBattlePreparationSelectionText()
    {
        return string.IsNullOrWhiteSpace(_selectedPlacementId)
            ? "当前未选中部署单位。"
            : $"当前选中：{BuildPlacementDisplayName(_selectedPlacementId)}";
    }

    private void RefreshBattlePreparationForceList()
    {
        if (_siteBattlePreparationRosterList == null)
        {
            return;
        }

        ClearChildren(_siteBattlePreparationRosterList);
        AddMutedLine(_siteBattlePreparationRosterList, "我方出征单位");
        AddBattlePreparationRosterButtons(_battlePreparationRequest?.PlayerForces);
        AddMutedLine(
            _siteBattlePreparationRosterList,
            $"敌方：{BuildBattlePreparationForceSummary(_battlePreparationRequest?.EnemyForces)}");

        if (_siteBattlePreparationStatus != null)
        {
            _siteBattlePreparationStatus.Text = BuildBattlePreparationSelectionText();
        }
    }

    private void AddBattlePreparationRosterButtons(IEnumerable<BattleForceRequest> forces)
    {
        foreach (BattleForceRequest force in forces ?? System.Array.Empty<BattleForceRequest>())
        {
            int count = System.Math.Max(0, force?.Count ?? 0);
            for (int index = 0; index < count; index++)
            {
                Button button = GameUiSceneFactory.CreateWorldSecondaryActionButton(nameof(WorldSiteRoot));
                if (button == null)
                {
                    continue;
                }

                bool deployed = IsBattlePreparationUnitDeployed(force, index);
                button.Text = $"{GetUnitLabel(force.UnitDefinitionId)} #{index + 1}\n{(deployed ? "已部署，可重新拖出" : "拖到绿色区域部署")}";
                button.Disabled = false;
                int capturedIndex = index;
                BattleForceRequest capturedForce = force;
                button.ButtonDown += () => BeginBattlePreparationRosterDrag(capturedForce, capturedIndex);
                if (_siteBattlePreparationRosterList == null)
                {
                    continue;
                }

                _siteBattlePreparationRosterList.AddChild(button);
            }
        }
    }

    private string BuildBattlePreparationForceSummary(IEnumerable<BattleForceRequest> forces)
    {
        List<string> lines = forces?
            .Where(force => force.Count > 0)
            .Select(force => $"{GetUnitLabel(force.UnitDefinitionId)} x{force.Count}")
            .ToList() ?? new List<string>();
        return lines.Count == 0 ? "未配置" : string.Join("，", lines);
    }

    private static int CountPreparedPlacements(IEnumerable<BattleForceRequest> forces)
    {
        return forces?.Sum(force => force?.PreferredPlacements?.Count ?? 0) ?? 0;
    }

    private void RefreshBattlePreparationActions()
    {
        if (_siteBattlePreparationActionList == null)
        {
            return;
        }

        ClearChildren(_siteBattlePreparationActionList);
        Button startButton = GameUiSceneFactory.CreateWorldPrimaryActionButton(nameof(WorldSiteRoot));
        if (startButton == null)
        {
            return;
        }

        startButton.Text = "开战\n确认部署并进入实时战斗";
        startButton.Pressed += LaunchPreparedBattle;
        _siteBattlePreparationActionList.AddChild(startButton);
    }

    private void LaunchPreparedBattle()
    {
        if (!_isBattlePreparationActive)
        {
            return;
        }

        BattleStartRequest request = _battlePreparationRequest;
        if (!CanLaunchPreparedBattle(request, out string failureReason))
        {
            RefreshBattlePreparationUi(failureReason);
            return;
        }

        SyncBattlePreparationRequestPlacements(request);
        SetAllDeploymentDragEnabled(false);
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"BattlePreparationCommitted request={request?.RequestId ?? ""} site={_siteHudSiteId}");
        ActivateBattleRuntime();
    }

    private void ClearPlayerBattlePreparationPlacements(BattleStartRequest request)
    {
        if (request?.PlayerForces == null)
        {
            return;
        }

        foreach (BattleForceRequest force in request.PlayerForces)
        {
            force?.PreferredPlacements?.Clear();
        }

        RefreshBattlePreparationMapEntities();
        GameLog.Info(nameof(WorldSiteRoot), $"BattlePreparationPlayerPlacementsCleared request={request.RequestId}");
    }

    private void SyncBattlePreparationRequestPlacements(BattleStartRequest request)
    {
        WorldSiteState site = ResolveSiteState(_siteHudSiteId);
        if (request == null || site?.UnitPlacements == null)
        {
            return;
        }

        int updated = 0;
        foreach (BattleForcePlacementRequest placementRequest in request.PlayerForces
                     .Concat(request.EnemyForces)
                     .SelectMany(force => force.PreferredPlacements)
                     .Where(placement => placement != null))
        {
            WorldSiteUnitPlacement placement = site.UnitPlacements
                .FirstOrDefault(item => item.PlacementId == placementRequest.PlacementId);
            if (placement == null)
            {
                continue;
            }

            placementRequest.CellX = placement.CellX;
            placementRequest.CellY = placement.CellY;
            placementRequest.CellHeight = placement.CellHeight;
            updated++;
        }

        GameLog.Info(
            nameof(WorldSiteRoot),
            $"BattlePreparationPlacementsSynced request={request.RequestId} site={site.SiteId} placements={updated}");
    }

    private bool IsBattlePreparationUnitDeployed(BattleForceRequest force, int forceIndex)
    {
        return force != null &&
               forceIndex >= 0 &&
               forceIndex < (force.PreferredPlacements?.Count ?? 0) &&
               force.PreferredPlacements[forceIndex] != null;
    }

    private void RefreshBattlePreparationMapEntities()
    {
        if (_battlePreparationRequest == null || _unitRoot == null)
        {
            return;
        }

        ClearBattleEntities();
        _sitePlacementEntities.Clear();
        var reservedDeploymentSurfaces = new HashSet<GridSurfacePosition>();
        AddRequestedForces(_battlePreparationRequest.PlayerForces, BattleFaction.Player, _battlePreparationRequest, reservedDeploymentSurfaces, requireAllPlacements: false);
        AddRequestedForces(_battlePreparationRequest.EnemyForces, BattleFaction.Enemy, _battlePreparationRequest, reservedDeploymentSurfaces, requireAllPlacements: true);
        PlaceBattleEntitiesOnGrid();
    }

    private bool CanLaunchPreparedBattle(BattleStartRequest request, out string failureReason)
    {
        failureReason = "";
        if (request == null)
        {
            failureReason = "战斗请求已失效。";
            return false;
        }

        foreach (BattleForceRequest force in request.PlayerForces ?? Enumerable.Empty<BattleForceRequest>())
        {
            if (force == null)
            {
                continue;
            }

            if ((force.PreferredPlacements?.Count(placement => placement != null) ?? 0) < force.Count)
            {
                failureReason = "还有我方单位未部署，不能开战。";
                return false;
            }
        }

        return true;
    }
}
