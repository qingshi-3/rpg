using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Application.World;
using Rpg.Definitions.Battle;
using Rpg.Definitions.Maps;
using Rpg.Definitions.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Flow;
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
        _selectedBattlePreparationPlanGroupKey = "";
        _explicitBattlePreparationRuleGroups.Clear();
        ClearPlayerBattlePreparationPlacements(request);
        EnsureBattlePreparationPlanDefaults(request);

        if (_returnMapButton != null)
        {
            _returnMapButton.Disabled = true;
            _returnMapButton.TooltipText = "战前部署中不能返回大地图。";
        }

        if (_siteHudRoot != null)
        {
            _siteHudRoot.Visible = true;
            ApplySiteHudFullRect("battle_preparation");
        }

        RefreshBattlePreparationUi("拖动部队头像到部署区，选择目标和策略后开战。");
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
        EnsureBattlePreparationPlanDefaults(_battlePreparationRequest);

        if (_siteHudTopBar != null)
        {
            _siteHudTopBar.Visible = true;
        }

        if (_sitePeacetimePanel != null)
        {
            _sitePeacetimePanel.Visible = false;
        }

        if (_siteBottomCommandHost != null)
        {
            _siteBottomCommandHost.Visible = false;
        }

        if (_battleRuntimeCommandBar != null)
        {
            _battleRuntimeCommandBar.Visible = false;
        }

        RefreshBattleRuntimeHeroFrame();
        SetBattlePreparationHudVisible(true);

        if (_siteHudTitle != null)
        {
            _siteHudTitle.Text = $"{ResolveSiteName(_siteHudSiteId)} - 战前部署";
        }

        if (_siteResourceLabel != null)
        {
            _siteResourceLabel.Text = string.IsNullOrWhiteSpace(notice)
                ? "拖动部队部署，点击缩略图选择目标。"
                : notice.Trim();
        }

        BindBattlePreparationCompanyRoster();
        BindBattlePreparationCompactPlanControls();
        BindBattlePreparationObjectiveThumbnail();
        ShowBattlePreparationDeploymentZone();
        RefreshBattlePreparationMapEntities();
        UpdateSitePeacetimePanelVisibility("battle_preparation_refresh");
        UpdateMainWorldViewportLayout("battle_preparation_refresh");
    }

    private void BindBattlePreparationCompanyRoster()
    {
        if (_battlePreparationRosterList == null)
        {
            return;
        }

        ClearChildren(_battlePreparationRosterList);
        EnsureSelectedBattlePreparationPlanGroup(_battlePreparationRequest);
        foreach (BattleRuntimeCommandGroupView group in BuildBattlePreparationPlayerGroups())
        {
            BattlePreparationRosterRow row = GameUiSceneFactory.CreateBattlePreparationRosterRow(nameof(WorldSiteRoot));
            if (row == null)
            {
                continue;
            }

            bool selected = string.Equals(group.GroupKey, _selectedBattlePreparationPlanGroupKey, System.StringComparison.Ordinal);
            row.Bind(group.GroupKey, group.DisplayName, ResolveBattlePreparationCompanyPlanStatus(group), selected);
            row.Selected += OnBattlePreparationCompanySelected;
            row.DragStarted += BeginBattlePreparationCompanyDrag;
            _battlePreparationRosterList.AddChild(row);
        }
    }

    private void OnBattlePreparationCompanySelected(string groupKey)
    {
        _selectedBattlePreparationPlanGroupKey = groupKey ?? "";
        ResolveBattlePreparationGroupPlan(_battlePreparationRequest, _selectedBattlePreparationPlanGroupKey, create: true);
        SyncSelectedBattlePreparationPlanFallback(_battlePreparationRequest);
        BindBattlePreparationCompanyRoster();
        BindBattlePreparationCompactPlanControls();
        BindBattlePreparationObjectiveThumbnail();
        GameLog.Info(nameof(WorldSiteRoot), $"BattlePreparationCompanySelected group={_selectedBattlePreparationPlanGroupKey}");
    }

    private void BindBattlePreparationCompactPlanControls()
    {
        BattleRuntimeCommandGroupView selectedGroup = ResolveSelectedBattlePreparationGroup();
        BattleGroupPlanSnapshot plan = ResolveBattlePreparationGroupPlan(
            _battlePreparationRequest,
            _selectedBattlePreparationPlanGroupKey,
            create: false);
        if (_battlePreparationCompanyLabel != null)
        {
            _battlePreparationCompanyLabel.Text = selectedGroup?.DisplayName ?? "未选择部队";
        }

        if (_battlePreparationObjectiveLabel != null)
        {
            _battlePreparationObjectiveLabel.Text = $"目标：{BattlePreparationPlanUiModel.ResolveObjectiveText(plan, _battlePreparationRequest?.ObjectiveZones)}";
        }

        BindBattlePreparationRuleButton(_battlePreparationMoveFirstButton, BattleEngagementRule.MoveFirst, plan);
        BindBattlePreparationRuleButton(_battlePreparationAttackFirstButton, BattleEngagementRule.AttackFirst, plan);
        BindBattlePreparationRuleButton(_battlePreparationHoldButton, BattleEngagementRule.Hold, plan);

        if (_battlePreparationStartButton != null)
        {
            bool canLaunch = CanLaunchPreparedBattle(_battlePreparationRequest, out string failureReason);
            _battlePreparationStartButton.Disabled = !canLaunch;
            _battlePreparationStartButton.TooltipText = canLaunch ? "开始实时战斗" : failureReason;
        }
    }

    private void BindBattlePreparationRuleButton(
        Button button,
        BattleEngagementRule rule,
        BattleGroupPlanSnapshot plan)
    {
        if (button == null)
        {
            return;
        }

        bool explicitSelected = plan != null &&
                                plan.EngagementRule == rule &&
                                _explicitBattlePreparationRuleGroups.Contains(_selectedBattlePreparationPlanGroupKey);
        button.Text = explicitSelected ? $"✓{BattlePreparationPlanUiModel.BuildRuleLabel(rule)}" : BattlePreparationPlanUiModel.BuildRuleLabel(rule);
        button.Disabled = string.IsNullOrWhiteSpace(_selectedBattlePreparationPlanGroupKey);
        button.TooltipText = BattlePreparationPlanUiModel.BuildRuleTooltip(rule);
    }

    private void BindBattlePreparationObjectiveThumbnail()
    {
        if (_battlePreparationObjectiveThumbnail == null)
        {
            return;
        }

        BattleRuntimeCommandGroupView selectedGroup = ResolveSelectedBattlePreparationGroup();
        BattleGroupPlanSnapshot plan = ResolveBattlePreparationGroupPlan(
            _battlePreparationRequest,
            _selectedBattlePreparationPlanGroupKey,
            create: false);
        _battlePreparationObjectiveThumbnail.Bind(
            selectedGroup?.DisplayName ?? "当前部队",
            BuildBattleObjectiveMapCells(),
            (_battlePreparationRequest?.ObjectiveZones ?? new List<BattleObjectiveZoneSnapshot>())
                .OrderBy(zone => zone?.Priority ?? int.MaxValue)
                .ToArray(),
            plan?.ObjectiveZoneId ?? "",
            BuildBattleObjectiveMapRegions());
    }

    private BattleRuntimeCommandGroupView ResolveSelectedBattlePreparationGroup()
    {
        return BuildBattlePreparationPlayerGroups()
            .FirstOrDefault(group => string.Equals(
                group.GroupKey,
                _selectedBattlePreparationPlanGroupKey,
                System.StringComparison.Ordinal));
    }

    private BattlePreparationCompanyPlanStatus ResolveBattlePreparationCompanyPlanStatus(
        BattleRuntimeCommandGroupView group)
    {
        return BattlePreparationPlanUiModel.ResolveCompanyPlanStatus(
            group,
            ResolveBattlePreparationGroupPlan(_battlePreparationRequest, group?.GroupKey, create: false),
            _battlePreparationRequest?.ObjectiveZones,
            _explicitBattlePreparationRuleGroups);
    }

    private bool IsBattlePreparationCompanyPlaced(BattleRuntimeCommandGroupView group)
        => BattlePreparationPlanUiModel.IsCompanyPlaced(group);

    private void ShowBattlePreparationDeploymentZone()
    {
        string playerFactionId = ResolveBattlePreparationPlayerDeploymentFactionId();
        SemanticDeploymentSide playerSide = SemanticDeploymentSide.Player;
        IEnumerable<GridPosition> playerCells = BuildBattlePreparationDeploymentZoneCells(
            playerSide,
            playerFactionId,
            ResolveBattlePreparationDeploymentDirection(playerSide, playerFactionId));

        string enemyFactionId = ResolveBattlePreparationEnemyDeploymentFactionId();
        SemanticDeploymentSide enemySide = SemanticDeploymentSide.Enemy;
        IEnumerable<GridPosition> enemyCells = HasAuthoredBattlePreparationDeploymentZone(enemySide, enemyFactionId)
            ? BuildBattlePreparationDeploymentZoneCells(
                enemySide,
                enemyFactionId,
                ResolveBattlePreparationDeploymentDirection(enemySide, enemyFactionId))
            : System.Array.Empty<GridPosition>();
        // Deployment zones are semantic preparation regions, not generic movement or attack range highlights.
        _deploymentZoneOverlay?.SetZones(playerCells, enemyCells);
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
        return BattlePreparationDeploymentRouting.ResolvePlayerFactionId(
            _battlePreparationRequest,
            StrategicWorldRuntime.State?.PlayerFactionId ?? "");
    }

    private string ResolveBattlePreparationEnemyDeploymentFactionId()
    {
        return BattlePreparationDeploymentRouting.ResolveEnemyFactionId(
            _battlePreparationRequest,
            ResolveBattlePreparationPlayerDeploymentFactionId());
    }

    private bool HasAuthoredBattlePreparationDeploymentZone(SemanticDeploymentSide deploymentSide, string factionId)
        => _deploymentCache?.HasAuthoredDeploymentZoneForSide(deploymentSide, factionId) == true;

    private WorldSiteAttackDirection ResolveBattlePreparationDeploymentDirection(
        SemanticDeploymentSide deploymentSide,
        string factionId)
    {
        return BattlePreparationDeploymentRouting.ResolveDirection(
            _battlePreparationRequest,
            deploymentSide,
            factionId);
    }

    private SemanticDeploymentSide ResolveBattlePreparationDeploymentSide(string factionId, BattleFaction fallbackFaction)
    {
        return BattlePreparationDeploymentRouting.ResolveSide(
            _battlePreparationRequest,
            factionId,
            fallbackFaction,
            ResolveBattlePreparationPlayerDeploymentFactionId(),
            ResolveBattlePreparationEnemyDeploymentFactionId());
    }

    private void EnsureBattlePreparationPlanDefaults(BattleStartRequest request)
    {
        if (request == null)
        {
            return;
        }
        request.ObjectiveZones ??= new List<BattleObjectiveZoneSnapshot>();
        IReadOnlyList<BattleObjectiveZoneSnapshot> markerZones = BuildMarkerBackedBattlePreparationObjectiveZones(request);
        if (markerZones.Count > 0 || ContainsGeneratedBattlePreparationObjectiveZones(request.ObjectiveZones))
        {
            request.ObjectiveZones.Clear();
            request.ObjectiveZones.AddRange(markerZones);
        }
        request.PlayerBattleGroupPlan ??= new BattleGroupPlanSnapshot();
        request.PlayerBattleGroupPlans ??= new Dictionary<string, BattleGroupPlanSnapshot>(System.StringComparer.Ordinal);
        EnsureSelectedBattlePreparationPlanGroup(request);
        foreach (BattleRuntimeCommandGroupView group in BuildBattlePreparationPlayerGroups())
        {
            BattleGroupPlanSnapshot plan = ResolveBattlePreparationGroupPlan(request, group.GroupKey, create: true);
            plan.InitialFormationId = BattlePreparationPlanUiModel.ResolveFormationId(plan.InitialFormationId, group.DefaultFormationId);
            bool explicitRuleSelected = _explicitBattlePreparationRuleGroups.Contains(group.GroupKey);
            if (BattlePreparationPlanUiModel.ShouldDefaultEngagementRule(plan, explicitRuleSelected))
            {
                plan.EngagementRule = _selectedBattleCorpsCommand == BattleCorpsCommand.HoldLine
                    ? BattleEngagementRule.Hold
                    : BattleEngagementRule.MoveFirst;
            }
        }
        EnsureBattlePreparationEnemyPlanDefaults(request);
        SyncSelectedBattlePreparationPlanFallback(request);
    }

    private void SelectBattlePreparationObjectiveZone(string objectiveZoneId)
    {
        EnsureBattlePreparationPlanDefaults(_battlePreparationRequest);
        BattleObjectiveZoneSnapshot zone = _battlePreparationRequest?.ObjectiveZones?
            .FirstOrDefault(item => string.Equals(item?.ObjectiveZoneId, objectiveZoneId, System.StringComparison.Ordinal));
        if (zone == null)
        {
            RefreshBattlePreparationUi("目标区域不存在。");
            return;
        }

        ApplyBattlePreparationObjectiveZoneToPlan(_battlePreparationRequest, zone);
        string label = BattlePreparationPlanUiModel.BuildObjectiveLabel(zone);
        RefreshBattlePreparationUi($"目标区域已设为：{label}。");
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"BattlePreparationObjectiveSelected request={_battlePreparationRequest?.RequestId ?? ""} group={_selectedBattlePreparationPlanGroupKey} objective={zone.ObjectiveZoneId}");
    }

    private void SelectBattlePreparationEngagementRule(BattleEngagementRule rule)
    {
        EnsureBattlePreparationPlanDefaults(_battlePreparationRequest);
        BattleGroupPlanSnapshot plan = ResolveBattlePreparationGroupPlan(
            _battlePreparationRequest,
            _selectedBattlePreparationPlanGroupKey,
            create: true);
        if (plan == null)
        {
            return;
        }

        plan.EngagementRule = rule;
        _explicitBattlePreparationRuleGroups.Add(_selectedBattlePreparationPlanGroupKey ?? "");
        SyncSelectedBattlePreparationPlanFallback(_battlePreparationRequest);
        _selectedBattleCorpsCommand = rule == BattleEngagementRule.Hold
            ? BattleCorpsCommand.HoldLine
            : BattleCorpsCommand.Assault;
        ApplyBattleRuntimeCommandToRequest(_battlePreparationRequest, BuildBattleRuntimeCommandRequest(_selectedBattleCorpsCommand));

        string label = BattlePreparationPlanUiModel.BuildRuleLabel(rule);
        RefreshBattlePreparationUi($"推进策略已设为：{label}。");
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"BattlePreparationEngagementRuleSelected request={_battlePreparationRequest.RequestId} group={_selectedBattlePreparationPlanGroupKey} rule={rule}");
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
        SyncBattlePreparationPlanToRequest(request);
        SetAllDeploymentDragEnabled(false);
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"BattlePreparationCommitted request={request?.RequestId ?? ""} site={_siteHudSiteId} groups={request?.PlayerBattleGroupPlans?.Count ?? 0} selectedGroup={_selectedBattlePreparationPlanGroupKey} objective={request?.PlayerBattleGroupPlan?.ObjectiveZoneId ?? ""} rule={request?.PlayerBattleGroupPlan?.EngagementRule.ToString() ?? ""}");
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

    private void SyncBattlePreparationPlanToRequest(BattleStartRequest request)
    {
        EnsureBattlePreparationPlanDefaults(request);
        if (request?.PlayerBattleGroupPlans == null)
        {
            return;
        }

        int synced = 0;
        foreach (BattleRuntimeCommandGroupView group in BuildBattlePreparationPlayerGroups())
        {
            BattleGroupPlanSnapshot plan = ResolveBattlePreparationGroupPlan(request, group.GroupKey, create: true);
            BattleObjectiveZoneSnapshot zone = request.ObjectiveZones?
                .FirstOrDefault(item => string.Equals(item?.ObjectiveZoneId, plan.ObjectiveZoneId, System.StringComparison.Ordinal));
            if (zone == null)
            {
                continue;
            }

            ApplyBattlePreparationObjectiveZoneToPlan(plan, zone);
            synced++;
        }

        SyncSelectedBattlePreparationPlanFallback(request);
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"BattlePreparationPlanSynced request={request.RequestId} groups={synced} selectedGroup={_selectedBattlePreparationPlanGroupKey} objective={request.PlayerBattleGroupPlan?.ObjectiveZoneId ?? ""} rule={request.PlayerBattleGroupPlan?.EngagementRule.ToString() ?? ""}");
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

        EnsureBattlePreparationPlanDefaults(request);
        if ((request.ObjectiveZones?.Count ?? 0) == 0)
        {
            failureReason = "当前地图没有可选目标区域 marker，不能开战。";
            return false;
        }

        if (!BattlePreparationPlanUiModel.ArePlayerRequestSlotsPlaced(request))
        {
            failureReason = "还有我方单位未部署，不能开战。";
            return false;
        }

        foreach (BattleRuntimeCommandGroupView group in BuildBattlePreparationPlayerGroups())
        {
            if (!IsBattlePreparationCompanyPlaced(group))
            {
                failureReason = $"还有部队未部署：{group.DisplayName}。";
                return false;
            }

            BattleGroupPlanSnapshot plan = ResolveBattlePreparationGroupPlan(request, group.GroupKey, create: false);
            if (plan == null || string.IsNullOrWhiteSpace(plan.ObjectiveZoneId))
            {
                failureReason = $"还有部队未选择进攻目标：{group.DisplayName}。";
                return false;
            }

            bool objectiveExists = request.ObjectiveZones.Any(zone =>
                string.Equals(zone?.ObjectiveZoneId, plan.ObjectiveZoneId, System.StringComparison.Ordinal));
            if (!objectiveExists)
            {
                failureReason = $"部队目标区域已失效：{group.DisplayName}。";
                return false;
            }

            if (!_explicitBattlePreparationRuleGroups.Contains(group.GroupKey) ||
                !BattlePreparationPlanUiModel.IsEngagementRuleDefined(plan.EngagementRule))
            {
                failureReason = $"还有部队未选择推进策略：{group.DisplayName}。";
                return false;
            }
        }

        return true;
    }
}
