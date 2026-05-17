using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World;

public partial class StrategicWorldRoot
{
    private bool EnsureWorldBattlesForAttackingThreats()
    {
        if (State == null || Definition == null)
        {
            return false;
        }

        WorldTickResult result = new() { WorldTick = State.WorldTick };
        _worldBattleProgressionService.EnsureBattlesForAttackingThreats(State, Definition, result);
        if (result.StartedWorldBattleIds.Count == 0)
        {
            return false;
        }

        StrategicWorldRuntime.LastNotice = string.Join("\n", result.Messages);
        GameLog.Info(
            nameof(StrategicWorldRoot),
            $"WorldBattlesEnsured started={string.Join(",", result.StartedWorldBattleIds)} tick={State.WorldTick}");
        return true;
    }

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
                GameLog.Warn(
                    nameof(StrategicWorldRoot),
                    $"StrategicRuntimeActivationDelayed reason={navigationSyncFailureReason}");
            }

            return false;
        }

        _runtimeStage = StrategicRuntimeStage.Active;
        _reportedStrategicNavigationNotSynchronized = false;
        EnsureWorldBattlesForAttackingThreats();
        _worldClockPaused = HasAttackingThreat() || HasNavigationBlockedArmy();
        RestoreWorldClockAfterSiteReturn();
        _worldClockPaused = HasAttackingThreat() || HasNavigationBlockedArmy() || _worldClockPaused;
        _worldClockAccumulator = 0.0;
        RefreshAll();
        GameLog.Info(
            nameof(StrategicWorldRoot),
            $"StrategicRuntimeActivated tick={State.WorldTick} paused={_worldClockPaused} navigationVersion={_strategicNavigationContext.Version}");

        if (_pendingBattleRequest == null)
        {
            TryEnterFirstDefenseRaidBattle();
        }

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

        if (HasAttackingThreat())
        {
            if (_pendingBattleRequest == null && TryEnterFirstDefenseRaidBattle())
            {
                return;
            }

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

        if (tickResult.AttackingThreatIds.Count > 0)
        {
            _selectedThreatId = tickResult.AttackingThreatIds[0];
            if (State.ThreatPlans.TryGetValue(_selectedThreatId, out EnemyThreatPlan threat))
            {
                _selectedSiteId = threat.TargetSiteId;
                messages.Add("敌方已抵达，世界战斗开始推演。");
            }

            string playerThreatId = tickResult.AttackingThreatIds
                .FirstOrDefault(threatId => WorldBattleProgressionService.IsPlayerInvolvedThreat(State, Definition, threatId)) ?? "";
            if (!string.IsNullOrWhiteSpace(playerThreatId))
            {
                StrategicWorldRuntime.LastNotice = string.Join("\n", messages);
                if (TryEnterDefenseRaidBattle(playerThreatId))
                {
                    GameLog.Info(nameof(StrategicWorldRoot), $"WorldClockTick tick={State.WorldTick} immediatePlayerBattle={playerThreatId}");
                    return;
                }
            }

            if (HasAttackingThreat())
            {
                _worldClockPaused = true;
                messages.Add("敌方已抵达，但世界战斗未能创建，世界时钟已暂停。");
            }
        }

        StrategicWorldRuntime.LastNotice = string.Join("\n", messages);
        GameLog.Info(nameof(StrategicWorldRoot), $"WorldClockTick tick={State.WorldTick} paused={_worldClockPaused}");
        RefreshAll();
    }

    private bool UpdateWorldArmyMovement(double delta)
    {
        if (!AutoWorldClockEnabled || Definition == null || State == null || _worldClockPaused || HasAttackingThreat())
        {
            return false;
        }

        string navigationSyncFailureReason = "";
        if (_strategicNavigationContext?.IsSynchronized(out navigationSyncFailureReason) != true)
        {
            if (!_reportedStrategicNavigationNotSynchronized)
            {
                _reportedStrategicNavigationNotSynchronized = true;
                GameLog.Warn(
                    nameof(StrategicWorldRoot),
                    $"WorldArmyMovementDelayed reason={navigationSyncFailureReason}");
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
            // 导航阻塞表示地图制作或状态契约不成立；暂停世界并暴露第一支问题部队，不自动改道或修复。
            _worldClockPaused = true;
            string blockedArmyId = result.NavigationBlockedArmyIds[0];
            WorldArmyState blockedArmy = State.ArmyStates.TryGetValue(blockedArmyId, out WorldArmyState army)
                ? army
                : null;
            _selectedThreatId = State.ThreatPlans.Values
                .FirstOrDefault(threat => threat.WorldArmyId == blockedArmyId)?.Id ?? "";
            _selectedSiteId = blockedArmy?.TargetSiteId ?? "";
            StrategicWorldRuntime.LastNotice = result.Messages.Count > 0
                ? string.Join("\n", result.Messages)
                : $"战略导航失败，世界推进已暂停：{blockedArmyId}";
            GameLog.Error(
                nameof(StrategicWorldRoot),
                $"StrategicWorldPausedByNavigationBlocked armies={string.Join(",", result.NavigationBlockedArmyIds)}");
            RefreshAll();
            return true;
        }

        if (result.ArrivedArmyIds.Count > 0 || result.Messages.Count > 0)
        {
            if (result.AttackingThreatIds.Count > 0)
            {
                _selectedThreatId = result.AttackingThreatIds[0];
                if (State.ThreatPlans.TryGetValue(_selectedThreatId, out EnemyThreatPlan threat))
                {
                    _selectedSiteId = threat.TargetSiteId;
                }

                string playerThreatId = result.AttackingThreatIds
                    .FirstOrDefault(threatId => WorldBattleProgressionService.IsPlayerInvolvedThreat(State, Definition, threatId)) ?? "";
                if (!string.IsNullOrWhiteSpace(playerThreatId) &&
                    TryEnterDefenseRaidBattle(playerThreatId))
                {
                    return false;
                }

                WorldTickResult battleStartResult = new() { WorldTick = State.WorldTick };
                _worldBattleProgressionService.EnsureBattlesForAttackingThreats(State, Definition, battleStartResult);
                result.Events.AddRange(battleStartResult.Events);
                result.Messages.AddRange(battleStartResult.Messages);
                if (HasAttackingThreat())
                {
                    _worldClockPaused = true;
                    result.Messages.Add("敌方已抵达，但世界战斗未能创建，世界时钟已暂停。");
                }
            }

            if (result.BattleReadyArmyIds.Count > 0)
            {
                WorldArmyState readyArmy = State.ArmyStates.TryGetValue(result.BattleReadyArmyIds[0], out WorldArmyState value)
                    ? value
                    : null;
                if (readyArmy != null)
                {
                    _selectedSiteId = readyArmy.TargetSiteId;
                    _selectedThreatId = "";
                    _worldClockPaused = true;
                    StrategicWorldRuntime.LastNotice = $"部队已抵达{ResolveSiteDisplayName(readyArmy.TargetSiteId)}，请选择进攻或潜入。";
                    RefreshAll();
                }

                return true;
            }

            if (result.FieldIntercepts.Count > 0 &&
                TryEnterFieldInterceptBattle(result.FieldIntercepts[0]))
            {
                return false;
            }

            StrategicWorldRuntime.LastNotice = result.Messages.Count > 0
                ? string.Join("\n", result.Messages)
                : $"部队已抵达目标：{string.Join("，", result.ArrivedArmyIds)}。";
            RefreshAll();
            return true;
        }

        QueueStrategicOverlayRedraw();
        return true;
    }

    private void RestoreWorldClockAfterSiteReturn()
    {
        if (!StrategicWorldRuntime.TryConsumeWorldResumeAfterSiteReturn())
        {
            return;
        }

        if (HasAttackingThreat())
        {
            GameLog.Info(nameof(StrategicWorldRoot), "World clock resume requested after site return, but an attacking threat is still pending.");
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
        if (HasAttackingThreat())
        {
            _worldClockPaused = true;
            StrategicWorldRuntime.LastNotice = "敌方正在进攻，必须先处理威胁。";
        }
        else if (HasNavigationBlockedArmy())
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

    private bool HasAttackingThreat()
    {
        return State?.ThreatPlans.Values.Any(threat =>
            threat.Stage == ThreatStage.Attacking &&
            WorldBattleProgressionService.IsPlayerInvolvedThreat(State, Definition, threat) &&
            !WorldBattleProgressionService.HasActiveBattleForThreat(State, threat.Id)) == true;
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
