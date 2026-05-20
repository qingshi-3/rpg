using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;
using Rpg.Infrastructure.Scenes;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World;

public partial class StrategicWorldRoot
{
    private bool TryEnterBattleForArrivedArmy(string armyId)
    {
        if (string.IsNullOrWhiteSpace(armyId) ||
            !State.ArmyStates.TryGetValue(armyId, out WorldArmyState army) ||
            army.OwnerFactionId != State.PlayerFactionId ||
            army.Intent != WorldArmyIntent.AssaultSite)
        {
            return false;
        }

        _worldClockPaused = true;
        _selectedSiteId = army.TargetSiteId;
        if (!CanBuildAssaultBattleForSite(army.TargetSiteId))
        {
            army.Status = WorldArmyStatus.Idle;
            army.Intent = WorldArmyIntent.None;
            army.ClearNavigationPath();
            _worldClockPaused = false;
            StrategicWorldDefinitionQueries queries = new(Definition);
            WorldSiteDefinition siteDefinition = queries.GetSite(army.TargetSiteId);
            StrategicWorldRuntime.LastNotice = BuildUnsupportedAssaultNotice(siteDefinition);
            GameLog.Warn(nameof(StrategicWorldRoot), $"Arrived assault army has no battle builder army={army.ArmyId} target={army.TargetSiteId}");
            RefreshAll();
            return true;
        }

        PendingBattleLaunchRollback rollback = CaptureBattleLaunchRollbackForSite(army.TargetSiteId);
        if (State.SiteStates.TryGetValue(army.TargetSiteId, out WorldSiteState site))
        {
            _siteModeTransitions.EnterWartime(site, State.WorldTick, "assault_army_arrived", army.ArmyId);
        }

        string returnScenePath = string.IsNullOrWhiteSpace(SceneFilePath)
            ? "res://scenes/world/StrategicWorldRoot.tscn"
            : SceneFilePath;
        BattleStartRequest request = _battleRequestBuilder.BuildAssaultBonefieldRequest(
            State,
            Definition,
            returnScenePath,
            SiteScenePath,
            army.ArmyId);
        StrategicWorldRuntime.LastNotice = "玩家进攻部队已抵达，进入攻占战。";
        if (!TryEnterBattle(request, rollback))
        {
            RefreshAll();
        }

        return true;
    }

    private bool TryEnterFieldInterceptBattle(WorldArmyInterceptResult intercept)
    {
        if (intercept == null ||
            string.IsNullOrWhiteSpace(intercept.PlayerArmyId) ||
            string.IsNullOrWhiteSpace(intercept.EnemyArmyId) ||
            !State.ArmyStates.ContainsKey(intercept.PlayerArmyId) ||
            !State.ArmyStates.ContainsKey(intercept.EnemyArmyId))
        {
            return false;
        }

        _worldClockPaused = true;
        string returnScenePath = string.IsNullOrWhiteSpace(SceneFilePath)
            ? "res://scenes/world/StrategicWorldRoot.tscn"
            : SceneFilePath;
        BattleStartRequest request = _battleRequestBuilder.BuildFieldInterceptRequest(
            State,
            Definition,
            intercept.PlayerArmyId,
            intercept.EnemyArmyId,
            returnScenePath,
            SiteScenePath);
        _selectedSiteId = string.IsNullOrWhiteSpace(request.TargetSiteId)
            ? _selectedSiteId
            : request.TargetSiteId;
        StrategicWorldRuntime.LastNotice = "玩家部队与敌军接触，进入野外遭遇战。";
        if (!TryEnterBattle(request))
        {
            RefreshAll();
        }

        return true;
    }

    private bool TryEnterBattle(BattleStartRequest request, IReadOnlyCollection<GameEvent> transitionEvents = null)
    {
        if (request == null)
        {
            return false;
        }

        _pendingBattleRollback = CaptureBattleLaunchRollback(request, transitionEvents);
        BeginBattleAnnouncement(request);
        return true;
    }

    private bool TryEnterBattle(BattleStartRequest request, PendingBattleLaunchRollback rollback)
    {
        if (request == null)
        {
            return false;
        }

        _pendingBattleRollback = rollback ?? CaptureBattleLaunchRollback(request, null);
        BeginBattleAnnouncement(request);
        return true;
    }

    private PendingBattleLaunchRollback CaptureBattleLaunchRollback(
        BattleStartRequest request,
        IReadOnlyCollection<GameEvent> transitionEvents)
    {
        PendingBattleLaunchRollback rollback = CaptureBattleLaunchRollbackForSite(ResolveBattleRollbackSiteId(request));
        ApplyModeTransitionRollbackEvent(rollback, transitionEvents);
        return rollback;
    }

    private PendingBattleLaunchRollback CaptureBattleLaunchRollbackForSite(string siteId)
    {
        PendingBattleLaunchRollback rollback = new()
        {
            SiteId = siteId ?? "",
            PreviousWorldClockPaused = _worldClockPaused
        };

        if (!string.IsNullOrWhiteSpace(rollback.SiteId) &&
            State.SiteStates.TryGetValue(rollback.SiteId, out WorldSiteState site))
        {
            rollback.HasPreviousSiteMode = true;
            rollback.PreviousSiteMode = site.SiteMode;
        }

        return rollback;
    }

    private static void ApplyModeTransitionRollbackEvent(
        PendingBattleLaunchRollback rollback,
        IReadOnlyCollection<GameEvent> transitionEvents)
    {
        if (rollback == null || transitionEvents == null)
        {
            return;
        }

        GameEvent modeEvent = transitionEvents.LastOrDefault(gameEvent =>
            gameEvent.Kind == "SiteModeChanged" &&
            gameEvent.TargetIds.Contains(rollback.SiteId) &&
            gameEvent.Payload.TryGetValue("to", out string toMode) &&
            toMode == WorldSiteMode.Wartime.ToString() &&
            gameEvent.Payload.TryGetValue("from", out _));
        if (modeEvent == null ||
            !modeEvent.Payload.TryGetValue("from", out string fromMode) ||
            !System.Enum.TryParse(fromMode, out WorldSiteMode previousMode))
        {
            return;
        }

        rollback.HasPreviousSiteMode = true;
        rollback.PreviousSiteMode = previousMode;
    }

    private static string ResolveBattleRollbackSiteId(BattleStartRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request?.TargetSiteId))
        {
            return request.TargetSiteId;
        }

        return request?.SourceSiteId ?? "";
    }

    private void RollbackPendingBattleLaunch(string reason)
    {
        if (_pendingBattleRollback == null)
        {
            return;
        }

        if (_pendingBattleRollback.HasPreviousSiteMode &&
            State.SiteStates.TryGetValue(_pendingBattleRollback.SiteId, out WorldSiteState site))
        {
            WorldSiteMode currentMode = site.SiteMode;
            _siteModeTransitions.RestoreMode(
                site,
                _pendingBattleRollback.PreviousSiteMode,
                State.WorldTick,
                "battle_launch_rollback");
            site.UnitPlacements.RemoveAll(placement => !WorldSiteDeploymentService.IsGarrisonPlacement(placement));
            GameLog.Warn(
                nameof(StrategicWorldRoot),
                $"Battle launch rolled back site={site.SiteId} fromMode={currentMode} toMode={site.SiteMode} reason={reason}");
        }

        _worldClockPaused = _pendingBattleRollback.PreviousWorldClockPaused;
        _worldClockAccumulator = 0.0;
        _pendingBattleRollback = null;
    }

    private void ClearPendingBattleLaunchRollback()
    {
        _pendingBattleRollback = null;
    }

    private static bool CanBuildAssaultBattleForSite(string siteId)
    {
        return siteId == StrategicWorldIds.SiteBonefield;
    }

    private static string BuildUnsupportedAssaultNotice(WorldSiteDefinition siteDefinition)
    {
        string siteName = string.IsNullOrWhiteSpace(siteDefinition?.DisplayName)
            ? "目标场域"
            : siteDefinition.DisplayName;
        return $"{siteName}暂未配置攻占战，不能进攻。";
    }

    private bool ReportUnsupportedPlayerAssaultArmies()
    {
        if (State?.ArmyStates == null)
        {
            return false;
        }

        List<string> blockedArmyIds = new();
        foreach (WorldArmyState army in State.ArmyStates.Values)
        {
            if (army.OwnerFactionId != State.PlayerFactionId ||
                army.Status != WorldArmyStatus.Attacking ||
                army.Intent != WorldArmyIntent.AssaultSite ||
                CanBuildAssaultBattleForSite(army.TargetSiteId))
            {
                continue;
            }

            blockedArmyIds.Add(army.ArmyId);
        }

        if (blockedArmyIds.Count == 0)
        {
            return false;
        }

        string joinedArmyIds = string.Join(",", blockedArmyIds);
        StrategicWorldRuntime.LastNotice = $"发现未配置攻占战的进攻状态，世界推进已暂停：{joinedArmyIds}";
        GameLog.Error(nameof(StrategicWorldRoot), $"UnsupportedPlayerAssaultArmiesBlocked armies={joinedArmyIds}");
        return true;
    }

    private void BeginBattleAnnouncement(BattleStartRequest request)
    {
        _pendingBattleRequest = request;
        _worldClockPaused = true;
        Vector2 focusPosition = ResolveBattleFocusMapPosition(request);
        FocusWorldMapOn(focusPosition);
        StrategicWorldRuntime.LastNotice = "发生了战斗。";
        RefreshAll();
        ShowPreBattleDialog();
        GameLog.Info(nameof(StrategicWorldRoot), $"BattleAnnouncement request={request.RequestId} kind={request.BattleKind} focus={focusPosition}");
    }

    private void ShowPreBattleDialog()
    {
        if (_pendingBattleRequest == null)
        {
            return;
        }

        EnsurePreBattleDialog();
        if (_preBattleDialog == null)
        {
            return;
        }

        _preBattleDialog.Title = "战前情报";
        _preBattleDialog.DialogText = BuildPreBattleText(_pendingBattleRequest);
        _activeBattleGateDialog = "prebattle";
        _preBattleDialog.PopupCentered(new Vector2I(560, 460));
    }

    private void EnsurePreBattleDialog()
    {
        if (_preBattleDialog != null)
        {
            return;
        }

        _preBattleDialog = GameUiSceneFactory.Instantiate<AcceptDialog>(
            GameUiSceneFactory.PreBattleDialogScenePath,
            nameof(StrategicWorldRoot));
        if (_preBattleDialog == null)
        {
            return;
        }

        GameUiSkin.ApplyDialog(_preBattleDialog);
        ConfigureBattleGateDialog(_preBattleDialog);
        _preBattleDialog.CloseRequested += OnBattleGateDialogCloseRequested;
        _preBattleDialog.Confirmed += LaunchPendingBattle;
        AddChild(_preBattleDialog);
    }

    private static void ConfigureBattleGateDialog(AcceptDialog dialog)
    {
        if (dialog == null)
        {
            return;
        }

        dialog.Exclusive = true;
        dialog.Unresizable = true;
        dialog.Borderless = true;
    }

    private void OnBattleGateDialogCloseRequested()
    {
        if (_pendingBattleRequest == null)
        {
            return;
        }

        CallDeferred(nameof(ReopenActiveBattleGateDialog));
    }

    private void ReopenActiveBattleGateDialog()
    {
        if (_pendingBattleRequest == null)
        {
            return;
        }

        if (_activeBattleGateDialog == "prebattle")
        {
            ShowPreBattleDialog();
            return;
        }

        ShowPreBattleDialog();
    }

    private void LaunchPendingBattle()
    {
        BattleStartRequest request = _pendingBattleRequest;
        _pendingBattleRequest = null;
        _activeBattleGateDialog = "";
        if (request == null)
        {
            return;
        }

        SceneTransitionResult transition = _sceneTransitionRouter.EnterBattlePreparation(new SceneTransitionBattleRequest
        {
            Request = request,
            OnSuccess = ClearPendingBattleLaunchRollback,
            RollbackOnFailure = RollbackPendingBattleLaunch
        });
        if (transition.Success)
        {
            return;
        }

        StrategicWorldRuntime.LastNotice = "无法进入自动战斗。";
        GameLog.Warn(
            nameof(StrategicWorldRoot),
            $"Cannot enter site path={request.SiteScenePath} error={transition.Error} reason={transition.FailureReason}");
        RefreshAll();
    }

    private string BuildPreBattleText(BattleStartRequest request)
    {
        List<string> lines = new()
        {
            GetBattleKindLabel(request.BattleKind),
            "",
            "双方部队",
            $"我方：{BuildForceSummary(request.PlayerForces, request.SourceArmyId, true)}",
            $"敌方：{BuildForceSummary(request.EnemyForces, request.TargetArmyId, false)}"
        };

        if (request.BattleKind == BattleKind.AssaultSite)
        {
            lines.Add("");
            lines.Add("城池信息");
            lines.Add(BuildSiteBattleSummary(request));
        }

        return string.Join("\n", lines);
    }

    private string BuildForceSummary(IReadOnlyCollection<BattleForceRequest> forces, string armyId, bool playerSide)
    {
        List<string> unitLines = forces?
            .Where(force => force.Count > 0)
            .Select(force => $"{GetUnitLabel(force.UnitDefinitionId)} x{force.Count}")
            .ToList() ?? new List<string>();

        return unitLines.Count == 0 ? "未配置部队详情" : string.Join("，", unitLines);
    }

    private string BuildSiteBattleSummary(BattleStartRequest request)
    {
        string siteId = !string.IsNullOrWhiteSpace(request.TargetSiteId)
            ? request.TargetSiteId
            : request.SiteStateSnapshot?.SiteId ?? "";
        StrategicWorldDefinitionQueries queries = new(Definition);
        WorldSiteDefinition definition = queries.GetSite(siteId);
        WorldSiteState site = !string.IsNullOrWhiteSpace(siteId) && State.SiteStates.TryGetValue(siteId, out WorldSiteState value)
            ? value
            : null;

        if (site == null)
        {
            return "未找到城池状态。";
        }

        string garrison = site.Garrison.Count == 0
            ? "无"
            : string.Join("，", site.Garrison.Where(item => item.Count > 0).Select(item => $"{GetUnitLabel(item.UnitTypeId)} x{item.Count}"));
        int activeFacilities = site.Facilities.Count(item => item.State == FacilityState.Active);
        int damagedFacilities = site.Facilities.Count(item => item.State == FacilityState.Damaged);

        return
            $"场域：{definition?.DisplayName ?? site.SiteId}\n" +
            $"状态：{GetControlStateLabel(site.ControlState)}\n" +
            $"归属：{StrategicWorldDisplayNames.GetFactionLabel(queries, site.OwnerFactionId)}\n" +
            $"受损：{site.DamageLevel}\n" +
            $"建筑：运行 {activeFacilities}，受损 {damagedFacilities}\n" +
            $"驻军：{garrison}";
    }

    private Vector2 ResolveBattleFocusMapPosition(BattleStartRequest request)
    {
        if (request.BattleKind == BattleKind.FieldIntercept &&
            State.ArmyStates.TryGetValue(request.SourceArmyId, out WorldArmyState playerArmy) &&
            State.ArmyStates.TryGetValue(request.TargetArmyId, out WorldArmyState enemyArmy))
        {
            return (playerArmy.WorldPosition + enemyArmy.WorldPosition) / 2.0f;
        }

        if (!string.IsNullOrWhiteSpace(request.SourceArmyId) &&
            State.ArmyStates.TryGetValue(request.SourceArmyId, out WorldArmyState sourceArmy))
        {
            return sourceArmy.WorldPosition;
        }

        if (!string.IsNullOrWhiteSpace(request.TargetArmyId) &&
            State.ArmyStates.TryGetValue(request.TargetArmyId, out WorldArmyState targetArmy))
        {
            return targetArmy.WorldPosition;
        }

        StrategicWorldDefinitionQueries queries = new(Definition);
        WorldSiteDefinition targetSite = queries.GetSite(request.TargetSiteId);
        if (targetSite != null)
        {
            return targetSite.MapPosition;
        }

        WorldSiteDefinition sourceSite = queries.GetSite(request.SourceSiteId);
        return sourceSite?.MapPosition ?? Vector2.Zero;
    }

    private void FocusWorldMapOn(Vector2 mapPosition)
    {
        if (_worldCamera == null || !float.IsFinite(mapPosition.X) || !float.IsFinite(mapPosition.Y))
        {
            return;
        }

        _worldCamera.FocusOn(mapPosition);
        UpdateWorldCameraView(true);
    }
}
