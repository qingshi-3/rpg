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
using Rpg.Presentation.World;

namespace Rpg.Presentation.World.Sites;

public partial class WorldSiteRoot
{
    private void EnterBattlePreparation()
    {
        if (!TryResolveActiveBattleRequest(out BattleStartRequest request))
        {
            return;
        }

        _isBattlePreparationActive = true;
        _battlePreparationRequest = request;
        _selectedBattleCorpsCommand = BattleCorpsCommand.Assault;
        BattleRuntimeCommandHudModel.ApplyRuntimeCommandToRequest(request, BuildBattleRuntimeCommandRequest(_selectedBattleCorpsCommand));
        SetBattleRuntimeEnabled(false, keepBattlePresentation: true);
        StrategicWorldRuntime.EnsureInitialized();
        _siteHudSiteId = ResolveRequestSiteId(request);
        _siteHudReturnScenePath = string.IsNullOrWhiteSpace(request.ReturnScenePath)
            ? "res://scenes/world/StrategicWorldRoot.tscn"
            : request.ReturnScenePath;
        _selectedPlacementId = "";
        _selectedBattlePreparationPlanGroupKey = "";
        _selectedBattlePreparationPlanGroupKeys.Clear();
        _explicitBattlePreparationRuleGroups.Clear();
        ClearPlayerBattlePreparationPlacements(request, refreshMapEntities: false);
        EnsureBattlePreparationPlanDefaults(request);
        SetBattlePreparationTopPrompt("");

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

        RefreshBattlePreparationUi("点击部队后移动鼠标预览阵型，再点击合法部署位置放下。");
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

        if (_sitePeacetimePanel != null)
        {
            _sitePeacetimePanel.Visible = false;
        }

        if (_siteBottomCommandHost != null)
        {
            _siteBottomCommandHost.Visible = false;
        }

        if (_battleRuntimeSummaryBar != null)
        {
            _battleRuntimeSummaryBar.Visible = false;
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
                ? "点击部队后放置阵型，放下后右键选择目的地。"
                : notice.Trim();
        }

        BindBattlePreparationCompanyRoster();
        BindBattlePreparationCompactPlanControls();
        ShowBattlePreparationDeploymentZone();
        RefreshBattlePreparationMapEntities();
        RefreshBattlePreparationDestinationBeaconOverlays();
        UpdateSitePeacetimePanelVisibility("battle_preparation_refresh");
        UpdateMainWorldViewportLayout("battle_preparation_refresh");
    }

    private void BindBattlePreparationCompanyRoster()
    {
        EnsureSelectedBattlePreparationPlanGroup(_battlePreparationRequest);
        _battlePreparationHudBinder.BindCompanyRoster(
            _battlePreparationRosterList,
            BuildBattlePreparationPlayerGroups(),
            _selectedBattlePreparationPlanGroupKey,
            (groupKey, create) => ResolveBattlePreparationGroupPlan(_battlePreparationRequest, groupKey, create),
            _battlePreparationRequest?.ObjectiveZones,
            _explicitBattlePreparationRuleGroups,
            OnBattlePreparationCompanySelected,
            BeginBattlePreparationCompanyDrag);
    }

    private void OnBattlePreparationCompanySelected(string groupKey)
    {
        _selectedBattlePreparationPlanGroupKey = groupKey ?? "";
        bool additiveSelection = Input.IsKeyPressed(Key.Shift);
        if (!string.IsNullOrWhiteSpace(_selectedBattlePreparationPlanGroupKey) && additiveSelection)
        {
            if (!_selectedBattlePreparationPlanGroupKeys.Add(_selectedBattlePreparationPlanGroupKey) &&
                _selectedBattlePreparationPlanGroupKeys.Count > 1)
            {
                _selectedBattlePreparationPlanGroupKeys.Remove(_selectedBattlePreparationPlanGroupKey);
            }
        }
        else if (!string.IsNullOrWhiteSpace(_selectedBattlePreparationPlanGroupKey))
        {
            _selectedBattlePreparationPlanGroupKeys.Clear();
            _selectedBattlePreparationPlanGroupKeys.Add(_selectedBattlePreparationPlanGroupKey);
        }

        ResolveBattlePreparationGroupPlan(_battlePreparationRequest, _selectedBattlePreparationPlanGroupKey, create: true);
        SyncSelectedBattlePreparationPlanFallback(_battlePreparationRequest);
        BindBattlePreparationCompanyRoster(); BindBattlePreparationCompactPlanControls();
        if (!additiveSelection)
        {
            BeginBattlePreparationCompanyPlacementFollow(groupKey);
        }
        RefreshBattlePreparationDestinationBeaconOverlays(); BattlePreparationCommandSelectionPresenter.Apply(_unitRoot, ResolveSelectedBattlePreparationGroup(), _selectedBattlePreparationPlanGroupKey);
        GameLog.Info(nameof(WorldSiteRoot), $"BattlePreparationCompanySelected group={_selectedBattlePreparationPlanGroupKey}");
    }

    private void BindBattlePreparationCompactPlanControls()
    {
        BattleRuntimeCommandGroupView selectedGroup = ResolveSelectedBattlePreparationGroup();
        BattleGroupPlanSnapshot plan = ResolveBattlePreparationGroupPlan(
            _battlePreparationRequest,
            _selectedBattlePreparationPlanGroupKey,
            create: false);
        _battlePreparationHudBinder.BindCompactPlanControls(
            _battlePreparationCompanyLabel,
            _battlePreparationObjectiveLabel,
            _battlePreparationMoveFirstButton,
            _battlePreparationAttackFirstButton,
            _battlePreparationHoldButton,
            _battlePreparationStartButton,
            selectedGroup,
            _selectedBattlePreparationPlanGroupKey,
            plan,
            _battlePreparationRequest,
            _explicitBattlePreparationRuleGroups,
            CanLaunchPreparedBattle);
    }

    private void BindBattlePreparationObjectiveThumbnail()
    {
        _battleObjectivePlanningHudBinder.BindThumbnail(
            _battlePreparationObjectiveThumbnail,
            _battlePreparationRequest,
            ResolveSelectedBattlePreparationGroup()?.DisplayName ?? "当前部队",
            _selectedBattlePreparationPlanGroupKey,
            (groupKey, create) => ResolveBattlePreparationGroupPlan(_battlePreparationRequest, groupKey, create),
            _activeGridMap,
            _deploymentCache,
            _semanticMapMarkers?.Markers,
            BuildBattlePreparationObjectiveZoneFromMarker);
    }

    private BattleRuntimeCommandGroupView ResolveSelectedBattlePreparationGroup()
    {
        return BuildBattlePreparationPlayerGroups()
            .FirstOrDefault(group => string.Equals(
                group.GroupKey,
                _selectedBattlePreparationPlanGroupKey,
                System.StringComparison.Ordinal));
    }

    private bool IsBattlePreparationCompanyPlaced(BattleRuntimeCommandGroupView group)
        => BattlePreparationPlanUiModel.IsCompanyPlaced(group);

    private IReadOnlyList<BattleRuntimeCommandGroupView> BuildDeployedBattlePreparationPlayerGroups()
    {
        return BuildBattlePreparationPlayerGroups()
            .Where(IsBattlePreparationCompanyPlaced)
            .ToArray();
    }

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
            if (BattlePreparationPlanUiModel.ShouldDefaultEngagementRule(plan, explicitRuleSelected: false))
            {
                // Destination-beacon combat starts from the default attack posture; movement goals come from live commands.
                plan.EngagementRule = BattleEngagementRule.AttackFirst;
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
            RefreshBattlePreparationPlanUi("目标区域不存在。", "battle_preparation_objective_missing");
            return;
        }

        ApplyBattlePreparationObjectiveZoneToPlan(_battlePreparationRequest, zone);
        string label = BattlePreparationPlanUiModel.BuildObjectiveLabel(zone);
        RefreshBattlePreparationPlanUi($"目标区域已设为：{label}。", "battle_preparation_objective_selected");
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
        BattleRuntimeCommandHudModel.ApplyRuntimeCommandToRequest(_battlePreparationRequest, BuildBattleRuntimeCommandRequest(_selectedBattleCorpsCommand));

        string label = BattlePreparationPlanUiModel.BuildRuleLabel(rule);
        RefreshBattlePreparationPlanUi($"推进策略已设为：{label}。", "battle_preparation_rule_selected");
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
            RefreshBattlePreparationPlanUi(failureReason, "battle_preparation_launch_rejected");
            return;
        }

        SyncBattlePreparationRequestPlacements(request);
        SyncBattlePreparationPlanToRequest(request);
        ExcludeUndeployedBattlePreparationReserveGroups(request);
        SetAllDeploymentDragEnabled(false);
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"BattlePreparationCommitted request={request?.RequestId ?? ""} site={_siteHudSiteId} groups={request?.PlayerBattleGroupPlans?.Count ?? 0} selectedGroup={_selectedBattlePreparationPlanGroupKey} objective={request?.PlayerBattleGroupPlan?.ObjectiveZoneId ?? ""} rule={request?.PlayerBattleGroupPlan?.EngagementRule.ToString() ?? ""}");
        string siteId = ResolveRequestSiteId(request);
        WorldSiteState site = ResolveSiteState(siteId);
        WorldSiteBattleLaunchRollback rollback = _battleLauncher.CaptureRollback(site);
        WorldSiteBattleLaunchResult result = _battleLauncher.BeginAndActivate(
            StrategicWorldRuntime.State,
            request,
            rollback,
            // The preparation UI has already consumed and rendered the request; reapplying it here would clear confirmed placement entities.
            () => { },
            ActivateBattleRuntime,
            () => _battleStartBlockedReason,
            ClearBattleEntities,
            () => { },
            enabled => SetBattleRuntimeEnabled(enabled));
        if (!result.Success)
        {
            _isBattlePreparationActive = true;
            _battlePreparationRequest = request;
            SetAllDeploymentDragEnabled(true);
            RefreshBattlePreparationUi(string.IsNullOrWhiteSpace(result.FailureReason)
                ? "battle_activation_failed"
                : result.FailureReason);
        }
    }

    private void ClearPlayerBattlePreparationPlacements(BattleStartRequest request, bool refreshMapEntities = true)
    {
        if (request?.PlayerForces == null)
        {
            return;
        }

        foreach (BattleForceRequest force in request.PlayerForces)
        {
            force?.PreferredPlacements?.Clear();
        }

        if (refreshMapEntities)
        {
            RefreshBattlePreparationMapEntities();
        }

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

    private void ExcludeUndeployedBattlePreparationReserveGroups(BattleStartRequest request)
    {
        if (request?.PlayerForces == null)
        {
            return;
        }

        IReadOnlyList<BattleRuntimeCommandGroupView> allGroups = BuildBattlePreparationPlayerGroups();
        HashSet<string> deployedGroupKeys = allGroups
            .Where(IsBattlePreparationCompanyPlaced)
            .Select(group => group.GroupKey)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(System.StringComparer.Ordinal);
        if (deployedGroupKeys.Count == 0)
        {
            return;
        }

        int forceCountBefore = request.PlayerForces.Count;
        int planCountBefore = request.PlayerBattleGroupPlans?.Count ?? 0;
        request.PlayerForces = request.PlayerForces
            .Where(force => deployedGroupKeys.Contains(BattleRuntimeCommandHudModel.ResolveGroupKey(force)))
            .ToList();
        if (request.PlayerBattleGroupPlans != null)
        {
            foreach (string key in request.PlayerBattleGroupPlans.Keys
                         .Where(key => !deployedGroupKeys.Contains(key))
                         .ToArray())
            {
                request.PlayerBattleGroupPlans.Remove(key);
            }
        }

        if (!deployedGroupKeys.Contains(_selectedBattlePreparationPlanGroupKey ?? ""))
        {
            _selectedBattlePreparationPlanGroupKey = deployedGroupKeys.OrderBy(key => key, System.StringComparer.Ordinal).FirstOrDefault() ?? "";
        }

        _selectedBattlePreparationPlanGroupKeys.RemoveWhere(key => !deployedGroupKeys.Contains(key));
        if (!string.IsNullOrWhiteSpace(_selectedBattlePreparationPlanGroupKey))
        {
            _selectedBattlePreparationPlanGroupKeys.Add(_selectedBattlePreparationPlanGroupKey);
        }

        SyncSelectedBattlePreparationPlanFallback(request);
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"BattlePreparationReserveGroupsExcluded request={request.RequestId} deployed={deployedGroupKeys.Count} reserve={allGroups.Count - deployedGroupKeys.Count} playerForces={forceCountBefore}->{request.PlayerForces.Count} plans={planCountBefore}->{request.PlayerBattleGroupPlans?.Count ?? 0}");
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
        PlaceBattleEntitiesOnGrid(); BattlePreparationCommandSelectionPresenter.Apply(_unitRoot, ResolveSelectedBattlePreparationGroup(), _selectedBattlePreparationPlanGroupKey);
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

        IReadOnlyList<BattleRuntimeCommandGroupView> deployedGroups = BuildDeployedBattlePreparationPlayerGroups();
        if (deployedGroups.Count == 0)
        {
            failureReason = "至少部署一支我方英雄部队后才能开战。";
            return false;
        }

        foreach (BattleRuntimeCommandGroupView group in deployedGroups)
        {
            if (!IsBattlePreparationCompanyPlaced(group))
            {
                failureReason = $"还有部队未部署：{group.DisplayName}。";
                return false;
            }
        }

        foreach (BattleRuntimeCommandGroupView group in deployedGroups)
        {
            if (!HasBattlePreparationInitialDestinationBeacon(request, group.GroupKey))
            {
                failureReason = $"请右键选择部队目的地：{group.DisplayName}。";
                return false;
            }
        }

        return true;
    }

    private bool HasBattlePreparationInitialDestinationBeacon(BattleStartRequest request, string groupKey)
    {
        BattleGroupPlanSnapshot plan = ResolveBattlePreparationGroupPlan(request, groupKey, create: false);
        return plan?.HasInitialDestinationBeacon == true;
    }
}
