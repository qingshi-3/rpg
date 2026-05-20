using System.Collections.Generic;
using System.Linq;
using Rpg.Application.World;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Presentation.World;

public partial class StrategicWorldRoot
{
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
        List<string> messages = new() { $"世界推进到 {tickResult.WorldTick}。" };
        messages.AddRange(tickResult.Messages);
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
            StrategicWorldRuntime.LastNotice = result.Messages.Count > 0 ? string.Join("\n", result.Messages) : $"战略导航失败，世界推进已暂停：{blockedArmyId}";
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
            StrategicWorldRuntime.LastNotice = _worldClockPaused ? "世界时钟已暂停。" : "世界时钟继续推进。";
        }

        RefreshAll();
    }

    private void CycleWorldClockSpeed()
    {
        _worldClockSpeedIndex = (_worldClockSpeedIndex + 1) % WorldClockSpeedMultipliers.Length;
        StrategicWorldRuntime.LastNotice = $"世界时钟速度 {WorldClockSpeedMultipliers[_worldClockSpeedIndex]:0}x。";
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
        _worldClockLabel.Text = $"世界推进：{status}\n下一世界步：{System.Math.Ceiling(remaining):0}s";

        if (_worldClockToggleButton != null)
        {
            _worldClockToggleButton.Text = _worldClockPaused ? "继续" : "暂停";
            _worldClockToggleButton.TooltipText = _worldClockPaused ? "继续世界推进" : "暂停世界推进";
        }

        if (_worldClockSpeedButton != null)
        {
            _worldClockSpeedButton.Text = $"{WorldClockSpeedMultipliers[_worldClockSpeedIndex]:0}x";
            _worldClockSpeedButton.TooltipText = $"快进速度：{WorldClockSpeedMultipliers[_worldClockSpeedIndex]:0}x";
        }
    }
}
