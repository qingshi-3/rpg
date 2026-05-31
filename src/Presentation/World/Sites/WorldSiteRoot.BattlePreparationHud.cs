using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Reports;
using Rpg.Application.Battle.Snapshots;
using Rpg.Application.World;
using Rpg.Definitions.Battle;
using Rpg.Definitions.Maps;
using Rpg.Definitions.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Flow;
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
        _selectedBattlePreparationPlanGroupKey = "";
        _explicitBattlePreparationRuleGroups.Clear();
        ClearPlayerBattlePreparationPlacements(request);
        EnsureBattlePreparationPlanDefaults(request);

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
        EnsureBattlePreparationPlanDefaults(_battlePreparationRequest);

        _siteHudTitle.Text = $"{ResolveSiteName(_siteHudSiteId)} · 战前部署";
        _siteResourceLabel.Text = BuildResourceLine();
        _siteHudBody.Text = BuildBattlePreparationOverview(site, definition);
        _siteSelectionLabel.Text = BuildBattlePreparationSelectionText();
        _siteNoticeLabel.Text = string.IsNullOrWhiteSpace(notice) ? StrategicWorldRuntime.LastNotice : notice.Trim();

        SetBattlePreparationContentVisible(true);

        ClearChildren(_siteFacilityList);
        AddMutedLine(_siteFacilityList, "战前部署中不能建造或调整建筑。");
        RefreshBattlePreparationForceList();
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
            BuildBattlePreparationPlanSummary(_battlePreparationRequest),
            $"我方部署：{playerPlacementCount}    敌方部署：{enemyPlacementCount}"
        });
    }

    private string BuildBattlePreparationSelectionText()
    {
        string planSummary = BuildBattlePreparationPlanSummary(_battlePreparationRequest);
        return string.IsNullOrWhiteSpace(_selectedPlacementId)
            ? $"{planSummary}\n当前未选中部署单位。"
            : $"{planSummary}\n当前选中：{BuildPlacementDisplayName(_selectedPlacementId)}";
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
            bool explicitRuleSelected = _explicitBattlePreparationRuleGroups.Contains(group.GroupKey);
            if (ShouldDefaultBattlePreparationEngagementRule(plan, explicitRuleSelected))
            {
                plan.EngagementRule = _selectedBattleCorpsCommand == BattleCorpsCommand.HoldLine
                    ? BattleEngagementRule.Hold
                    : BattleEngagementRule.MoveFirst;
            }
        }

        EnsureBattlePreparationEnemyPlanDefaults(request);
        SyncSelectedBattlePreparationPlanFallback(request);
    }

    private static bool IsBattleEngagementRuleDefined(BattleEngagementRule rule)
    {
        return System.Enum.IsDefined(typeof(BattleEngagementRule), rule);
    }

    private static bool ShouldDefaultBattlePreparationEngagementRule(BattleGroupPlanSnapshot plan, bool explicitRuleSelected)
    {
        if (plan == null || !IsBattleEngagementRuleDefined(plan.EngagementRule))
        {
            return true;
        }

        // AttackFirst is the blank snapshot default, so only normalize it before
        // the player has explicitly clicked an engagement-rule button for this group.
        return plan.EngagementRule == BattleEngagementRule.AttackFirst &&
               !explicitRuleSelected &&
               string.IsNullOrWhiteSpace(plan.ObjectiveZoneId);
    }

    private string BuildBattlePreparationPlanSummary(BattleStartRequest request)
    {
        BattleRuntimeCommandGroupView selectedGroup = BuildBattlePreparationPlayerGroups()
            .FirstOrDefault(group => string.Equals(group.GroupKey, _selectedBattlePreparationPlanGroupKey, System.StringComparison.Ordinal));
        BattleGroupPlanSnapshot plan = ResolveBattlePreparationGroupPlan(
            request,
            _selectedBattlePreparationPlanGroupKey,
            create: false);
        string objectiveName = TryResolveSelectedBattleObjectiveZone(request, out BattleObjectiveZoneSnapshot selectedZone)
            ? BuildBattlePreparationObjectiveLabel(selectedZone)
            : "未选择";
        string ruleName = plan == null
            ? "未选择"
            : BuildBattlePreparationRuleLabel(plan.EngagementRule);
        string groupName = selectedGroup?.DisplayName ?? "未选择兵团";
        return $"兵团：{groupName}    目标区域：{objectiveName}    推进规则：{ruleName}";
    }

    private static string BuildBattlePreparationObjectiveLabel(BattleObjectiveZoneSnapshot zone)
    {
        if (!string.IsNullOrWhiteSpace(zone?.DisplayName))
        {
            return zone.DisplayName.Trim();
        }

        return string.IsNullOrWhiteSpace(zone?.ObjectiveZoneId) ? "目标区域" : zone.ObjectiveZoneId;
    }

    private void RefreshBattlePreparationActions()
    {
        if (_siteBattlePreparationActionList == null)
        {
            return;
        }

        ClearChildren(_siteBattlePreparationActionList);
        EnsureBattlePreparationPlanDefaults(_battlePreparationRequest);
        AddBattlePreparationStartButton(_siteBattlePreparationActionList);
        AddMutedLine(_siteBattlePreparationActionList, "目标区域");
        AddBattlePreparationObjectiveMapButton(_siteBattlePreparationActionList);

        AddMutedLine(_siteBattlePreparationActionList, "推进规则");
        AddBattlePreparationEngagementRuleButton(_siteBattlePreparationActionList, BattleEngagementRule.MoveFirst);
        AddBattlePreparationEngagementRuleButton(_siteBattlePreparationActionList, BattleEngagementRule.AttackFirst);
        AddBattlePreparationEngagementRuleButton(_siteBattlePreparationActionList, BattleEngagementRule.Hold);
    }

    private void AddBattlePreparationStartButton(Container targetList)
    {
        if (targetList == null)
        {
            return;
        }

        // Keep the launch action visible near the top of the planning controls;
        // the objective map can grow as marker counts increase.
        Button startButton = GameUiSceneFactory.CreateWorldPrimaryActionButton(nameof(WorldSiteRoot));
        if (startButton == null)
        {
            return;
        }

        startButton.Text = "开战\n确认部署并进入实时战斗";
        startButton.Pressed += LaunchPreparedBattle;
        targetList.AddChild(startButton);
    }

    private void AddBattlePreparationObjectiveMapButton(Container targetList)
    {
        if (targetList == null)
        {
            return;
        }

        Button button = GameUiSceneFactory.CreateWorldSecondaryActionButton(nameof(WorldSiteRoot));
        if (button == null)
        {
            return;
        }

        int objectiveCount = _battlePreparationRequest?.ObjectiveZones?.Count ?? 0;
        button.Text = objectiveCount == 0
            ? "选择进攻目标\n当前地图没有可用目标 marker"
            : $"打开战术缩略图\n{BuildBattlePreparationPlanSummary(_battlePreparationRequest)}";
        button.TooltipText = "在地图缩略图中先选兵团，再点击敌方目标区域 marker。";
        button.Pressed += OpenBattleObjectiveMapDialog;
        targetList.AddChild(button);
    }

    private void AddBattlePreparationEngagementRuleButton(
        Container targetList,
        BattleEngagementRule rule)
    {
        if (targetList == null)
        {
            return;
        }

        Button button = GameUiSceneFactory.CreateWorldSecondaryActionButton(nameof(WorldSiteRoot));
        if (button == null)
        {
            return;
        }

        BattleGroupPlanSnapshot selectedPlan = ResolveBattlePreparationGroupPlan(
            _battlePreparationRequest,
            _selectedBattlePreparationPlanGroupKey,
            create: false);
        bool selected = selectedPlan?.EngagementRule == rule;
        button.Text = selected
            ? $"已选 {BuildBattlePreparationRuleLabel(rule)}\n{BuildBattlePreparationRuleDetail(rule)}"
            : $"{BuildBattlePreparationRuleLabel(rule)}\n{BuildBattlePreparationRuleDetail(rule)}";
        button.TooltipText = BuildBattlePreparationRuleTooltip(rule);
        button.Pressed += () => SelectBattlePreparationEngagementRule(rule);
        targetList.AddChild(button);
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
        string label = BuildBattlePreparationObjectiveLabel(zone);
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

        string label = BuildBattlePreparationRuleLabel(rule);
        RefreshBattlePreparationUi($"推进规则已设为：{label}。");
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"BattlePreparationEngagementRuleSelected request={_battlePreparationRequest.RequestId} group={_selectedBattlePreparationPlanGroupKey} rule={rule}");
    }

    private static string BuildBattlePreparationObjectiveDetail(BattleObjectiveZoneSnapshot zone)
    {
        return zone?.ObjectiveRole switch
        {
            "enemy_core" => "直指敌方核心",
            "flank" => "侧翼推进路线",
            _ => "正面推进路线"
        };
    }

    private static string BuildBattlePreparationRuleLabel(BattleEngagementRule rule)
    {
        return rule switch
        {
            BattleEngagementRule.MoveFirst => "移动优先",
            BattleEngagementRule.AttackFirst => "攻击优先",
            BattleEngagementRule.Hold => "原地坚守",
            BattleEngagementRule.FireOnTheMove => "边走边打",
            BattleEngagementRule.RetreatFirst => "撤退优先",
            BattleEngagementRule.ProtectHero => "保护英雄",
            _ => "移动优先"
        };
    }

    private static string BuildBattlePreparationRuleDetail(BattleEngagementRule rule)
    {
        return rule switch
        {
            BattleEngagementRule.AttackFirst => "发现敌人优先接战",
            BattleEngagementRule.Hold => "等待敌人进入射程",
            _ => "先推进到目标区"
        };
    }

    private static string BuildBattlePreparationRuleTooltip(BattleEngagementRule rule)
    {
        return rule switch
        {
            BattleEngagementRule.AttackFirst => "推进途中感知到敌人时，优先完成接战。",
            BattleEngagementRule.Hold => "不主动追击远处目标，适合守点或等待敌人进入射程。",
            _ => "按目标区域推进，只处理近身或局部感知到的敌人。"
        };
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

        EnsureBattlePreparationPlanDefaults(request);
        if ((request.ObjectiveZones?.Count ?? 0) == 0)
        {
            failureReason = "当前地图没有可选目标区域 marker，不能开战。";
            return false;
        }

        foreach (BattleRuntimeCommandGroupView group in BuildBattlePreparationPlayerGroups())
        {
            BattleGroupPlanSnapshot plan = ResolveBattlePreparationGroupPlan(request, group.GroupKey, create: false);
            if (plan == null || string.IsNullOrWhiteSpace(plan.ObjectiveZoneId))
            {
                failureReason = $"还有兵团未选择进攻目标：{group.DisplayName}。";
                return false;
            }

            bool objectiveExists = request.ObjectiveZones.Any(zone =>
                string.Equals(zone?.ObjectiveZoneId, plan.ObjectiveZoneId, System.StringComparison.Ordinal));
            if (!objectiveExists)
            {
                failureReason = $"兵团目标区域已失效：{group.DisplayName}。";
                return false;
            }

            if (!IsBattleEngagementRuleDefined(plan.EngagementRule))
            {
                failureReason = $"还有兵团未选择推进规则：{group.DisplayName}。";
                return false;
            }
        }

        return true;
    }
}
