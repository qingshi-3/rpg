using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Reports;
using Rpg.Application.World;
using Rpg.Definitions.Battle;
using Rpg.Definitions.Maps;
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
        _selectedBattleCorpsCommand = ResolveBattleCorpsCommand(request.InitialCorpsCommandId);
        ApplyBattleRuntimeCommandToRequest(request, BuildBattleRuntimeCommandRequest(_selectedBattleCorpsCommand));
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
        if (_siteHudTopBar != null)
        {
            _siteHudTopBar.Visible = true;
        }

        if (_siteBottomCommandHost != null)
        {
            _siteBottomCommandHost.Visible = false;
        }

        if (_battleRuntimeCommandBar != null)
        {
            _battleRuntimeCommandBar.Visible = false;
        }

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
        string playerFactionId = ResolveBattlePreparationPlayerDeploymentFactionId();
        SemanticDeploymentSide playerSide = SemanticDeploymentSide.Player;
        IEnumerable<GridPosition> playerCells = BuildBattlePreparationDeploymentZoneCells(
            playerSide,
            playerFactionId,
            ResolveBattlePreparationDeploymentDirection(playerSide, playerFactionId));
        _highlightOverlay?.SetCells(BattleGridHighlightKind.FriendlyMove, playerCells);

        string enemyFactionId = ResolveBattlePreparationEnemyDeploymentFactionId();
        SemanticDeploymentSide enemySide = SemanticDeploymentSide.Enemy;
        IEnumerable<GridPosition> enemyCells = HasAuthoredBattlePreparationDeploymentZone(enemySide, enemyFactionId)
            ? BuildBattlePreparationDeploymentZoneCells(
                enemySide,
                enemyFactionId,
                ResolveBattlePreparationDeploymentDirection(enemySide, enemyFactionId))
            : System.Array.Empty<GridPosition>();
        _highlightOverlay?.SetCells(BattleGridHighlightKind.EnemyDeployment, enemyCells);
    }

    private IEnumerable<GridPosition> BuildBattlePreparationDeploymentZoneCells(
        SemanticDeploymentSide deploymentSide,
        string factionId,
        WorldSiteAttackDirection direction)
    {
        return _deploymentCache?
            .GetDeploymentZoneCandidatesForSide(deploymentSide, factionId, direction)
            .Select(candidate => new GridPosition(candidate.Cell.X, candidate.Cell.Y)) ??
            System.Array.Empty<GridPosition>();
    }

    private string ResolveBattlePreparationPlayerDeploymentFactionId()
    {
        string forceFactionId = _battlePreparationRequest?.PlayerForces?
            .FirstOrDefault(force => !string.IsNullOrWhiteSpace(force?.FactionId))
            ?.FactionId;
        if (!string.IsNullOrWhiteSpace(forceFactionId))
        {
            return forceFactionId;
        }

        return !string.IsNullOrWhiteSpace(_battlePreparationRequest?.AttackerFactionId)
            ? _battlePreparationRequest.AttackerFactionId
            : StrategicWorldRuntime.State?.PlayerFactionId ?? "";
    }

    private string ResolveBattlePreparationEnemyDeploymentFactionId()
    {
        string forceFactionId = _battlePreparationRequest?.EnemyForces?
            .FirstOrDefault(force => !string.IsNullOrWhiteSpace(force?.FactionId))
            ?.FactionId;
        if (!string.IsNullOrWhiteSpace(forceFactionId))
        {
            return forceFactionId;
        }

        string playerFactionId = ResolveBattlePreparationPlayerDeploymentFactionId();
        foreach (string factionId in new[]
                 {
                     _battlePreparationRequest?.DefenderFactionId,
                     _battlePreparationRequest?.AttackerFactionId
                 })
        {
            if (!string.IsNullOrWhiteSpace(factionId) &&
                !string.Equals(factionId, playerFactionId, System.StringComparison.Ordinal))
            {
                return factionId;
            }
        }

        return "";
    }

    private bool HasAuthoredBattlePreparationDeploymentZone(SemanticDeploymentSide deploymentSide, string factionId)
    {
        return _deploymentCache?.HasAuthoredDeploymentZoneForSide(deploymentSide, factionId) == true;
    }

    private WorldSiteAttackDirection ResolveBattlePreparationDeploymentDirection(
        SemanticDeploymentSide deploymentSide,
        string factionId)
    {
        WorldSiteAttackDirection attackDirection = _battlePreparationRequest?.AttackDirection ?? WorldSiteAttackDirection.Any;
        if (attackDirection == WorldSiteAttackDirection.Any)
        {
            return WorldSiteAttackDirection.Any;
        }

        string factionKey = factionId?.Trim() ?? "";
        if (!string.IsNullOrWhiteSpace(factionKey) &&
            string.Equals(factionKey, _battlePreparationRequest?.DefenderFactionId, System.StringComparison.Ordinal))
        {
            return GetOppositeBattlePreparationDirection(attackDirection);
        }

        if (!string.IsNullOrWhiteSpace(factionKey) &&
            string.Equals(factionKey, _battlePreparationRequest?.AttackerFactionId, System.StringComparison.Ordinal))
        {
            return attackDirection;
        }

        bool defenderSide = _battlePreparationRequest?.BattleKind switch
        {
            BattleKind.AssaultSite => deploymentSide == SemanticDeploymentSide.Enemy,
            BattleKind.DefenseRaid => deploymentSide == SemanticDeploymentSide.Player,
            BattleKind.FieldIntercept => deploymentSide == SemanticDeploymentSide.Enemy,
            _ => deploymentSide == SemanticDeploymentSide.Player
        };

        if (defenderSide)
        {
            return GetOppositeBattlePreparationDirection(attackDirection);
        }

        return attackDirection;
    }

    private SemanticDeploymentSide ResolveBattlePreparationDeploymentSide(string factionId, BattleFaction fallbackFaction)
    {
        if (fallbackFaction == BattleFaction.Player)
        {
            return SemanticDeploymentSide.Player;
        }

        if (fallbackFaction == BattleFaction.Enemy)
        {
            return SemanticDeploymentSide.Enemy;
        }

        string factionKey = factionId?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(factionKey))
        {
            return SemanticDeploymentSide.Any;
        }

        if ((_battlePreparationRequest?.PlayerForces ?? Enumerable.Empty<BattleForceRequest>())
            .Any(force => string.Equals(force?.FactionId, factionKey, System.StringComparison.Ordinal)))
        {
            return SemanticDeploymentSide.Player;
        }

        if ((_battlePreparationRequest?.EnemyForces ?? Enumerable.Empty<BattleForceRequest>())
            .Any(force => string.Equals(force?.FactionId, factionKey, System.StringComparison.Ordinal)))
        {
            return SemanticDeploymentSide.Enemy;
        }

        if (string.Equals(factionKey, ResolveBattlePreparationPlayerDeploymentFactionId(), System.StringComparison.Ordinal))
        {
            return SemanticDeploymentSide.Player;
        }

        if (string.Equals(factionKey, ResolveBattlePreparationEnemyDeploymentFactionId(), System.StringComparison.Ordinal))
        {
            return SemanticDeploymentSide.Enemy;
        }

        return SemanticDeploymentSide.Any;
    }

    private static WorldSiteAttackDirection GetOppositeBattlePreparationDirection(WorldSiteAttackDirection direction)
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
        AddBattlePreparationRosterButtons(_battlePreparationRequest?.PlayerForces, BattleFaction.Player);
        AddMutedLine(_siteBattlePreparationRosterList, "敌方部署单位");
        AddBattlePreparationRosterButtons(_battlePreparationRequest?.EnemyForces, BattleFaction.Enemy);
        AddMutedLine(
            _siteBattlePreparationRosterList,
            $"敌方：{BuildBattlePreparationForceSummary(_battlePreparationRequest?.EnemyForces)}");

        if (_siteBattlePreparationStatus != null)
        {
            _siteBattlePreparationStatus.Text = BuildBattlePreparationSelectionText();
        }
    }

    private void AddBattlePreparationRosterButtons(IEnumerable<BattleForceRequest> forces, BattleFaction fallbackFaction)
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
                BattleFaction capturedFallbackFaction = fallbackFaction;
                button.ButtonDown += () => BeginBattlePreparationRosterDrag(capturedForce, capturedIndex, capturedFallbackFaction);
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
