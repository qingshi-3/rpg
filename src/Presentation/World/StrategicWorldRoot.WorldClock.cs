using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.StrategicManagement;
using Rpg.Application.World;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Presentation.World;

public partial class StrategicWorldRoot
{
    private static readonly Texture2D WorldClockPauseNormalTexture = GD.Load<Texture2D>("res://assets/textures/ui/basic-ui/1/button_pause.png");
    private static readonly Texture2D WorldClockPauseHoverTexture = GD.Load<Texture2D>("res://assets/textures/ui/basic-ui/1/button_pause_hover.png");
    private static readonly Texture2D WorldClockPausePressedTexture = GD.Load<Texture2D>("res://assets/textures/ui/basic-ui/1/button_pause_pressed.png");
    private static readonly Texture2D WorldClockPlayNormalTexture = GD.Load<Texture2D>("res://assets/textures/ui/basic-ui/1/button_play.png");
    private static readonly Texture2D WorldClockPlayHoverTexture = GD.Load<Texture2D>("res://assets/textures/ui/basic-ui/1/button_play_hover.png");
    private static readonly Texture2D WorldClockPlayPressedTexture = GD.Load<Texture2D>("res://assets/textures/ui/basic-ui/1/button_play_pressed.png");
    private static readonly Texture2D WorldClockQuickNormalTexture = GD.Load<Texture2D>("res://assets/textures/ui/basic-ui/1/button_quick.png");
    private static readonly Texture2D WorldClockQuickHoverTexture = GD.Load<Texture2D>("res://assets/textures/ui/basic-ui/1/button_quick_hover.png");
    private static readonly Texture2D WorldClockQuickPressedTexture = GD.Load<Texture2D>("res://assets/textures/ui/basic-ui/1/button_quick_pressed.png");

    private bool EnsureStrategicRuntimeReady()
    {
        if (_runtimeStage == StrategicRuntimeStage.Active)
        {
            return true;
        }

        if (_runtimeStage == StrategicRuntimeStage.Bootstrapping)
        {
            return false;
        }

        if (Definition == null || State == null || _worldMapRoot == null)
        {
            return false;
        }

        string navigationSyncFailureReason = "";
        if (_strategicNavigationContext?.IsSynchronized(out navigationSyncFailureReason) != true)
        {
            if (!_reportedStrategicNavigationNotSynchronized)
            {
                _reportedStrategicNavigationNotSynchronized = true;
                GameLog.Warn(nameof(StrategicWorldRoot), $"StrategicRuntimeActivationDelayed reason={navigationSyncFailureReason}");
            }

            return false;
        }

        _runtimeStage = StrategicRuntimeStage.Active;
        _reportedStrategicNavigationNotSynchronized = false;
        _worldClockPaused = HasNavigationBlockedArmy() || _worldClockPaused;
        RestoreWorldClockAfterSiteReturn();
        _worldClockAccumulator = 0.0;
        RefreshAll();
        GameLog.Info(nameof(StrategicWorldRoot), $"StrategicRuntimeActivated tick={State.WorldTick} paused={_worldClockPaused} navigationVersion={_strategicNavigationContext.Version}");
        return true;
    }

    private void UpdateWorldClock(double delta)
    {
        if (!AutoWorldClockEnabled || Definition == null || State == null)
        {
            return;
        }

        if (HasNavigationBlockedArmy())
        {
            _worldClockPaused = true;
            _worldClockAccumulator = 0.0;
            RefreshWorldClockLabel();
            return;
        }

        if (_worldClockPaused)
        {
            RefreshWorldClockLabel();
            return;
        }

        double interval = System.Math.Max(1.0, WorldTickIntervalSeconds);
        _worldClockAccumulator += delta * WorldClockSpeedMultipliers[_worldClockSpeedIndex];
        if (_worldClockAccumulator < interval)
        {
            RefreshWorldClockLabel();
            return;
        }

        _worldClockAccumulator %= interval;
        AdvanceWorldClockTick();
    }

    private void AdvanceWorldClockTick()
    {
        WorldTickResult tickResult = _worldTickService.AdvanceWorldTick(State, Definition);
        List<string> messages = new() { $"大地图结算完成：{tickResult.WorldTick}。" };
        messages.AddRange(tickResult.Messages);
        // The legacy world clock still drives map cadence, but elapsed strategic
        // effects mutate launch-session memory only here. Save policy belongs to
        // a save coordinator/autosave boundary, not this high-frequency tick loop.
        StrategicCommandResult strategicSettlement = StrategicManagementRuntime.SettleElapsedWorldTime(1);
        if (strategicSettlement.Success)
        {
            ShowStrategicProductionFeedback(strategicSettlement);
            if (strategicSettlement.Events.Any(item =>
                    item.Kind is "StrategicLocationProductionSettled" or "StrategicCityBuildingProductionSettled"))
            {
                messages.Add("战略经营产出已结算。");
            }
        }
        else
        {
            GameLog.Warn(
                nameof(StrategicWorldRoot),
                $"StrategicManagementSettlementSkipped reason={strategicSettlement.FailureReason}");
        }

        StrategicWorldRuntime.LastNotice = string.Join("\n", messages);
        GameLog.Info(nameof(StrategicWorldRoot), $"WorldClockTick tick={State.WorldTick} paused={_worldClockPaused}");
        RefreshAll();
    }

    private bool UpdateWorldArmyMovement(double delta)
    {
        if (!AutoWorldClockEnabled || Definition == null || State == null || _worldClockPaused)
        {
            return false;
        }

        string navigationSyncFailureReason = "";
        if (_strategicNavigationContext?.IsSynchronized(out navigationSyncFailureReason) != true)
        {
            if (!_reportedStrategicNavigationNotSynchronized)
            {
                _reportedStrategicNavigationNotSynchronized = true;
                GameLog.Warn(nameof(StrategicWorldRoot), $"WorldArmyMovementDelayed reason={navigationSyncFailureReason}");
            }

            return false;
        }

        if (!State.ArmyStates.Values.Any(army => army.Status == WorldArmyStatus.Moving))
        {
            return false;
        }

        ResolveMovingArmySiteNavigationPoints();
        WorldArmyMovementResult result = _armyMovementService.AdvanceArmies(
            State,
            Definition,
            delta * WorldClockSpeedMultipliers[_worldClockSpeedIndex],
            _strategicNavigationContext,
            delta);

        if (result.NavigationBlockedArmyIds.Count > 0)
        {
            _worldClockPaused = true;
            string blockedArmyId = result.NavigationBlockedArmyIds[0];
            WorldArmyState blockedArmy = State.ArmyStates.TryGetValue(blockedArmyId, out WorldArmyState army) ? army : null;
            _selectedSiteId = blockedArmy?.TargetSiteId ?? "";
            StrategicWorldRuntime.LastNotice = result.Messages.Count > 0 ? string.Join("\n", result.Messages) : $"战略导航失败，大地图时间已暂停：{blockedArmyId}";
            GameLog.Error(nameof(StrategicWorldRoot), $"StrategicWorldPausedByNavigationBlocked armies={string.Join(",", result.NavigationBlockedArmyIds)}");
            RefreshAll();
            return true;
        }

        if (result.BattleReadyArmyIds.Count > 0 && TryEnterBattleForArrivedArmy(result.BattleReadyArmyIds[0]))
        {
            return false;
        }

        if (result.FieldIntercepts.Count > 0 && TryEnterFieldInterceptBattle(result.FieldIntercepts[0]))
        {
            return false;
        }

        ApplyArrivedStrategicReinforcements(result);

        if (result.ArrivedArmyIds.Count > 0 || result.Messages.Count > 0)
        {
            StrategicWorldRuntime.LastNotice = result.Messages.Count > 0 ? string.Join("\n", result.Messages) : $"部队已抵达目标：{string.Join("，", result.ArrivedArmyIds)}。";
            RefreshAll();
            return true;
        }

        RefreshStrategicFog();
        QueueStrategicOverlayRedraw();
        return true;
    }

    private void ApplyArrivedStrategicReinforcements(WorldArmyMovementResult movementResult)
    {
        if (movementResult?.ArrivedArmyIds == null || movementResult.ArrivedArmyIds.Count == 0)
        {
            return;
        }

        foreach (string armyId in movementResult.ArrivedArmyIds.ToArray())
        {
            if (!State.ArmyStates.TryGetValue(armyId, out WorldArmyState army) ||
                army.Intent != WorldArmyIntent.ReinforceSite ||
                string.IsNullOrWhiteSpace(army.StrategicExpeditionId))
            {
                continue;
            }

            StrategicCommandResult strategicResult = StrategicManagementRuntime.Commands.CompleteExpeditionArrival(
                StrategicManagementRuntime.State,
                army.StrategicExpeditionId);
            if (!strategicResult.Success)
            {
                string reason = FormatStrategicExpeditionFailureReason(strategicResult.FailureReason);
                movementResult.Messages.Add(reason);
                GameLog.Warn(
                    nameof(StrategicWorldRoot),
                    $"StrategicExpeditionArrivalRejected army={army.ArmyId} expedition={army.StrategicExpeditionId} reason={strategicResult.FailureReason}");
                continue;
            }

            StrategicManagementRuntime.SaveCurrentState();
            WorldArmyCommandResult carrierResult = _armyCommandService.RemoveResolvedStrategicExpeditionCarrier(
                State.ArmyStates,
                army.ArmyId,
                army.StrategicExpeditionId,
                "reinforce_arrived");
            if (!carrierResult.Success)
            {
                movementResult.Messages.Add(WorldActionResolver.FormatFailureReason(carrierResult.FailureReason));
                GameLog.Warn(
                    nameof(StrategicWorldRoot),
                    $"StrategicExpeditionArrivalCarrierCleanupRejected army={army.ArmyId} expedition={army.StrategicExpeditionId} reason={carrierResult.FailureReason}");
            }
        }
    }

    private void RestoreWorldClockAfterSiteReturn()
    {
        if (!StrategicWorldRuntime.TryConsumeWorldResumeAfterSiteReturn())
        {
            return;
        }

        if (HasNavigationBlockedArmy())
        {
            GameLog.Info(nameof(StrategicWorldRoot), "World clock resume requested after site return, but navigation is blocked.");
            return;
        }

        _worldClockPaused = false;
        _worldClockAccumulator = 0.0;
        GameLog.Info(nameof(StrategicWorldRoot), "World clock resumed after site return.");
    }

    private void ToggleWorldClock()
    {
        if (HasNavigationBlockedArmy())
        {
            _worldClockPaused = true;
            StrategicWorldRuntime.LastNotice = "战略导航阻塞，必须先修复行军路径。";
        }
        else
        {
            _worldClockPaused = !_worldClockPaused;
            StrategicWorldRuntime.LastNotice = _worldClockPaused ? "大地图时间已暂停。" : "大地图时间继续运行。";
        }

        RefreshAll();
    }

    private void CycleWorldClockSpeed()
    {
        _worldClockSpeedIndex = (_worldClockSpeedIndex + 1) % WorldClockSpeedMultipliers.Length;
        StrategicWorldRuntime.LastNotice = $"大地图时间速度 {WorldClockSpeedMultipliers[_worldClockSpeedIndex]:0}x。";
        RefreshAll();
    }

    private bool HasNavigationBlockedArmy()
    {
        return State?.ArmyStates.Values.Any(army => army.Status == WorldArmyStatus.NavigationBlocked) == true;
    }

    private void RefreshWorldClockLabel()
    {
        if (_worldClockLabel == null)
        {
            return;
        }

        double interval = System.Math.Max(1.0, WorldTickIntervalSeconds);
        double remaining = _worldClockPaused ? interval : System.Math.Max(0.0, interval - _worldClockAccumulator);
        string status = !AutoWorldClockEnabled
            ? "关闭"
            : HasNavigationBlockedArmy()
                ? "导航阻塞"
                : _worldClockPaused
                    ? "暂停"
                    : $"运行 {WorldClockSpeedMultipliers[_worldClockSpeedIndex]:0}x";
        _worldClockLabel.Text = $"大地图时间：{status}\n下次大地图结算：{System.Math.Ceiling(remaining):0}s";

        if (_worldClockToggleButton != null)
        {
            if (_worldClockPaused)
            {
                ApplyTextureButtonStates(
                    _worldClockToggleButton,
                    WorldClockPlayNormalTexture,
                    WorldClockPlayHoverTexture,
                    WorldClockPlayPressedTexture);
            }
            else
            {
                ApplyTextureButtonStates(
                    _worldClockToggleButton,
                    WorldClockPauseNormalTexture,
                    WorldClockPauseHoverTexture,
                    WorldClockPausePressedTexture);
            }

            _worldClockToggleButton.TooltipText = _worldClockPaused ? "继续大地图时间" : "暂停大地图时间";
        }

        if (_worldClockSpeedButton != null)
        {
            ApplyTextureButtonStates(
                _worldClockSpeedButton,
                WorldClockQuickNormalTexture,
                WorldClockQuickHoverTexture,
                WorldClockQuickPressedTexture);
            _worldClockSpeedButton.TooltipText = $"快进速度：{WorldClockSpeedMultipliers[_worldClockSpeedIndex]:0}x";
        }
    }

    private static void ApplyTextureButtonStates(
        TextureButton button,
        Texture2D normal,
        Texture2D hover,
        Texture2D pressed)
    {
        // Top-right controls use authored TextureButton states so hover/click
        // feedback stays resource-driven instead of mouse-event styling logic.
        button.TextureNormal = normal;
        button.TextureHover = hover;
        button.TexturePressed = pressed;
    }
}
