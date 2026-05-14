using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rpg.Application.Battle;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Preview;
using Rpg.Presentation.Battle.Rules;

namespace Rpg.Presentation.Battle.Flow;

public partial class BattleTurnController : Node
{
    private BattleUnitRoot _unitRoot;
    private BattleCommandController _commandController;
    private BattlePreviewController _previewController;
    private System.Func<IReadOnlyList<BattleEntity>> _getEntitiesSnapshot;
    private System.Func<IReadOnlyList<BattleEntity>> _getAlliedAutoUnits;
    private System.Action _generateEnemyIntents;
    private System.Func<BattleEntity, Task> _executeEnemyAction;
    private System.Func<BattleEntity, Task> _executeAlliedAutoAction;
    private System.Action<BattleEntity> _clearIntentBookkeeping;
    private System.Action _markBattleStateChanged;
    private System.Action<string> _showHint;
    private System.Action _clearHudCommand;
    private System.Action<BattleOutcome> _battleEnded;
    private System.Action<int, BattleFaction, BattleEntity, IReadOnlyList<BattleEntity>> _showTurnQueue;
    private System.Func<BattleEntity, BattleFaction, double, Task> _showActionCue;
    private System.Func<BattleEntity, Task> _hideActionCue;

    private readonly BattleActionCueSequencer _actionCueSequencer = new();
    private readonly List<BattleEntity> _playerActionOrder = new();
    private readonly HashSet<string> _completedPlayerEntityIds = new();
    private bool _isNonPlayerPhaseRunning;
    private bool _isAlliedAutoPhaseRunning;
    private bool _isActionCueRunning;
    private int _roundNumber = 1;
    private BattleEntity _activePlayerEntity;
    private BattleFaction _activePhaseFaction = BattleFaction.Player;
    private BattleOutcome _finalOutcome = BattleOutcome.None;
    private BattleOutcome _pendingOutcome = BattleOutcome.None;
    private bool _battleOutcomeCompletionPending;

    public bool IsEnemyPhaseRunning => _isNonPlayerPhaseRunning || _isActionCueRunning;
    public int RoundNumber => _roundNumber;

    public void Initialize(
        BattleUnitRoot unitRoot,
        BattleCommandController commandController,
        BattlePreviewController previewController,
        System.Func<IReadOnlyList<BattleEntity>> getEntitiesSnapshot,
        System.Func<IReadOnlyList<BattleEntity>> getAlliedAutoUnits,
        System.Action generateEnemyIntents,
        System.Func<BattleEntity, Task> executeEnemyAction,
        System.Func<BattleEntity, Task> executeAlliedAutoAction,
        System.Action<BattleEntity> clearIntentBookkeeping,
        System.Action markBattleStateChanged,
        System.Action<string> showHint,
        System.Action clearHudCommand,
        System.Action<BattleOutcome> battleEnded,
        System.Action<int, BattleFaction, BattleEntity, IReadOnlyList<BattleEntity>> showTurnQueue,
        System.Func<BattleEntity, BattleFaction, double, Task> showActionCue = null,
        System.Func<BattleEntity, Task> hideActionCue = null)
    {
        _unitRoot = unitRoot;
        _commandController = commandController;
        _previewController = previewController;
        _getEntitiesSnapshot = getEntitiesSnapshot;
        _getAlliedAutoUnits = getAlliedAutoUnits;
        _generateEnemyIntents = generateEnemyIntents;
        _executeEnemyAction = executeEnemyAction;
        _executeAlliedAutoAction = executeAlliedAutoAction;
        _clearIntentBookkeeping = clearIntentBookkeeping;
        _markBattleStateChanged = markBattleStateChanged;
        _showHint = showHint;
        _clearHudCommand = clearHudCommand;
        _battleEnded = battleEnded;
        _showTurnQueue = showTurnQueue;
        _showActionCue = showActionCue;
        _hideActionCue = hideActionCue;

        GameLog.Info(
            nameof(BattleTurnController),
            $"Initialized unitRoot={_unitRoot != null} command={_commandController != null} preview={_previewController != null} enemyExecutor={_executeEnemyAction != null} alliedExecutor={_executeAlliedAutoAction != null}");
    }

    public void StartBattle()
    {
        if (!EvaluateBattleOutcome())
        {
            BeginPlayerPhase(advanceRound: false);
        }
    }

    public void EndPlayerTurn(string hint)
    {
        _showHint?.Invoke(hint);

        if (EvaluateBattleOutcome())
        {
            return;
        }

        CompleteActivePlayerUnitThenAdvance();
    }

    public void HandleEntityDefeated(BattleEntity entity)
    {
        _clearIntentBookkeeping?.Invoke(entity);
        _unitRoot?.MarkEntityDefeated(entity);
        _markBattleStateChanged?.Invoke();
        if (entity == _activePlayerEntity)
        {
            _activePlayerEntity = null;
        }

        MarkPlayerEntityCompleted(entity);
        RefreshTurnQueue();
        GameLog.Info(nameof(BattleTurnController), $"Entity defeated handled id={entity?.EntityId} name={entity?.DisplayName}");
    }

    public void RefreshTurnQueue(BattleEntity activeEntity = null)
    {
        BattleFaction activeFaction = _activePhaseFaction;
        activeEntity ??= activeFaction == BattleFaction.Player && !_isNonPlayerPhaseRunning
            ? _activePlayerEntity
            : null;
        IReadOnlyList<BattleEntity> queue = BuildTurnQueue(activeFaction, activeEntity);
        _showTurnQueue?.Invoke(_roundNumber, activeFaction, activeEntity, queue);
    }

    public string GetSelectionBlockReason(BattleEntity entity)
    {
        if (entity == null || _finalOutcome != BattleOutcome.None || _battleOutcomeCompletionPending)
        {
            return "";
        }

        if (_isNonPlayerPhaseRunning)
        {
            return _isAlliedAutoPhaseRunning
                ? "友军兵团自动行动中"
                : "敌方行动中";
        }

        if (_activePlayerEntity == null)
        {
            return "当前没有可行动单位";
        }

        return entity == _activePlayerEntity
            ? ""
            : $"当前应由 {_activePlayerEntity.DisplayName} 行动";
    }

    public bool EvaluateBattleOutcome()
    {
        if (_finalOutcome != BattleOutcome.None || _battleOutcomeCompletionPending)
        {
            return true;
        }

        if (_unitRoot == null)
        {
            return false;
        }

        IReadOnlyList<BattleEntity> entities = _getEntitiesSnapshot?.Invoke() ?? System.Array.Empty<BattleEntity>();
        int aliveEnemies = entities.Count(entity =>
            entity.GetComponent<FactionComponent>()?.Faction == BattleFaction.Enemy &&
            !BattleRuleQueries.IsDefeated(entity));

        int alivePlayers = entities.Count(entity =>
            entity.GetComponent<FactionComponent>()?.Faction == BattleFaction.Player &&
            !BattleRuleQueries.IsDefeated(entity));

        if (aliveEnemies == 0)
        {
            CompleteBattle(BattleOutcome.Victory, "战斗胜利");
            return true;
        }

        if (alivePlayers == 0)
        {
            CompleteBattle(BattleOutcome.Defeat, "战斗失败");
            return true;
        }

        return false;
    }

    private async Task CompleteBattleAfterDefeatedPresentations(BattleOutcome outcome, string hint)
    {
        if (_unitRoot != null)
        {
            await _unitRoot.WaitForDefeatedPresentationsAsync();
        }

        if (_finalOutcome != BattleOutcome.None ||
            !_battleOutcomeCompletionPending ||
            _pendingOutcome != outcome)
        {
            return;
        }

        _battleOutcomeCompletionPending = false;
        _pendingOutcome = BattleOutcome.None;
        CompleteBattle(outcome, hint);
    }

    private void CompleteBattle(BattleOutcome outcome, string hint)
    {
        if (_finalOutcome != BattleOutcome.None)
        {
            return;
        }

        if (!_battleOutcomeCompletionPending &&
            _unitRoot?.HasPendingDefeatedPresentations == true)
        {
            _pendingOutcome = outcome;
            _battleOutcomeCompletionPending = true;
            _commandController?.ClearSelection();
            _clearHudCommand?.Invoke();
            ClearActionPreviewHighlights();
            _showHint?.Invoke(hint);
            GameLog.Info(nameof(BattleTurnController), $"Battle outcome {outcome} pending defeated presentations.");
            _ = CompleteBattleAfterDefeatedPresentations(outcome, hint);
            return;
        }

        _battleOutcomeCompletionPending = false;
        _pendingOutcome = BattleOutcome.None;
        _finalOutcome = outcome;
        _showHint?.Invoke(hint);
        GameLog.Info(nameof(BattleTurnController), $"Battle outcome {outcome}.");
        _battleEnded?.Invoke(outcome);
    }

    private async void RunAutoPhasesThenBeginPlayerPhase()
    {
        if (_isNonPlayerPhaseRunning)
        {
            return;
        }

        _activePlayerEntity = null;
        _isNonPlayerPhaseRunning = true;
        _commandController?.ClearSelection();
        ClearActionPreviewHighlights();

        await RunAlliedAutoPhase();
        await RunEnemyPhase();

        _isNonPlayerPhaseRunning = false;
        _activePhaseFaction = BattleFaction.Player;
        _isAlliedAutoPhaseRunning = false;

        if (!EvaluateBattleOutcome())
        {
            BeginPlayerPhase();
        }
    }

    private async Task RunAlliedAutoPhase()
    {
        BattleEntity[] alliedAutoUnits = EnumerateAlliedAutoUnits().ToArray();
        if (alliedAutoUnits.Length == 0 || EvaluateBattleOutcome())
        {
            return;
        }

        _isAlliedAutoPhaseRunning = true;
        _activePhaseFaction = BattleFaction.Player;
        _showHint?.Invoke("我方兵团行动");
        RefreshTurnQueue();
        GameLog.Info(nameof(BattleTurnController), $"Allied auto phase started round={_roundNumber} units={alliedAutoUnits.Length}");

        await WaitSeconds(0.25);

        foreach (BattleEntity ally in alliedAutoUnits)
        {
            if (EvaluateBattleOutcome())
            {
                break;
            }

            if (_executeAlliedAutoAction != null)
            {
                RefreshTurnQueue(ally);
                await RunActionWithCueAsync(
                    ally,
                    BattleFaction.Player,
                    () => _executeAlliedAutoAction(ally));
                RefreshTurnQueue();
            }
        }

        _isAlliedAutoPhaseRunning = false;
    }

    private async Task RunEnemyPhase()
    {
        if (EvaluateBattleOutcome())
        {
            return;
        }

        _generateEnemyIntents?.Invoke();
        _activePhaseFaction = BattleFaction.Enemy;
        _showHint?.Invoke("敌方行动");
        RefreshTurnQueue();
        GameLog.Info(nameof(BattleTurnController), $"Enemy phase started round={_roundNumber}");

        await WaitSeconds(0.35);

        foreach (BattleEntity enemy in EnumerateAliveFaction(BattleFaction.Enemy).ToArray())
        {
            if (EvaluateBattleOutcome())
            {
                break;
            }

            if (_executeEnemyAction != null)
            {
                RefreshTurnQueue(enemy);
                await RunActionWithCueAsync(
                    enemy,
                    BattleFaction.Enemy,
                    () => _executeEnemyAction(enemy));
                RefreshTurnQueue();
            }
        }
    }

    private void BeginPlayerPhase(bool advanceRound = true)
    {
        if (advanceRound)
        {
            _roundNumber++;
        }

        _activePhaseFaction = BattleFaction.Player;
        _isAlliedAutoPhaseRunning = false;
        _isNonPlayerPhaseRunning = false;
        _activePlayerEntity = null;
        _completedPlayerEntityIds.Clear();
        _unitRoot?.RestoreTurnResourcesForFaction(BattleFaction.Player);
        _unitRoot?.RestoreTurnResourcesForFaction(BattleFaction.Enemy);
        RebuildPlayerActionOrder();
        _generateEnemyIntents?.Invoke();
        ClearActionPreviewHighlights();
        _clearHudCommand?.Invoke();
        _showHint?.Invoke("我方行动");
        if (!SelectNextPlayerUnitOrEndPhase())
        {
            if (!EvaluateBattleOutcome())
            {
                RunAutoPhasesThenBeginPlayerPhase();
            }

            return;
        }

        RefreshTurnQueue(_commandController?.SelectedEntity);
        GameLog.Info(nameof(BattleTurnController), $"Player phase started round={_roundNumber}");
    }

    private void CompleteActivePlayerUnitThenAdvance()
    {
        BattleEntity completedEntity = _commandController?.SelectedEntity ?? _activePlayerEntity;
        MarkPlayerEntityCompleted(completedEntity);
        _activePlayerEntity = null;
        _commandController?.ClearSelection();
        ClearActionPreviewHighlights();
        _clearHudCommand?.Invoke();
        RefreshTurnQueue();

        if (EvaluateBattleOutcome())
        {
            return;
        }

        if (SelectNextPlayerUnitOrEndPhase())
        {
            return;
        }

        RunAutoPhasesThenBeginPlayerPhase();
    }

    private bool SelectNextPlayerUnitOrEndPhase()
    {
        PrunePlayerActionOrder();
        BattleEntity entity = _playerActionOrder
            .FirstOrDefault(entity => !IsPlayerEntityCompleted(entity) && CanAutoSelectForPlayerTurn(entity));

        if (entity == null)
        {
            _activePlayerEntity = null;
            _commandController?.ClearSelection();
            RefreshTurnQueue();
            GameLog.Info(nameof(BattleTurnController), "Player phase has no remaining actionable player unit.");
            return false;
        }

        _activePlayerEntity = entity;
        _commandController?.SelectEntity(entity);
        RefreshTurnQueue(entity);
        PlayPlayerActionCue(entity);
        GameLog.Info(nameof(BattleTurnController), $"Selected next player unit id={entity.EntityId} name={entity.DisplayName}");
        return true;
    }

    private void RebuildPlayerActionOrder()
    {
        _playerActionOrder.Clear();
        _playerActionOrder.AddRange(EnumerateAliveFaction(BattleFaction.Player)
            .Where(CanAutoSelectForPlayerTurn));
        GameLog.Info(nameof(BattleTurnController), $"Player action order rebuilt count={_playerActionOrder.Count}");
    }

    private void PrunePlayerActionOrder()
    {
        _playerActionOrder.RemoveAll(entity =>
            entity == null ||
            BattleRuleQueries.IsDefeated(entity) ||
            entity.GetComponent<FactionComponent>()?.Faction != BattleFaction.Player);
    }

    private void MarkPlayerEntityCompleted(BattleEntity entity)
    {
        if (entity == null ||
            entity.GetComponent<FactionComponent>()?.Faction != BattleFaction.Player ||
            string.IsNullOrWhiteSpace(entity.EntityId))
        {
            return;
        }

        _completedPlayerEntityIds.Add(entity.EntityId);
        GameLog.Info(nameof(BattleTurnController), $"Player unit completed id={entity.EntityId} name={entity.DisplayName}");
    }

    private IEnumerable<BattleEntity> EnumerateAliveFaction(BattleFaction faction)
    {
        if (_unitRoot == null)
        {
            yield break;
        }

        foreach (BattleEntity entity in _unitRoot.EnumerateAliveFaction(faction))
        {
            yield return entity;
        }
    }

    private IEnumerable<BattleEntity> EnumerateAlliedAutoUnits()
    {
        IReadOnlyList<BattleEntity> explicitOrder = _getAlliedAutoUnits?.Invoke();
        if (explicitOrder != null && explicitOrder.Count > 0)
        {
            foreach (BattleEntity entity in explicitOrder)
            {
                if (CanRunAlliedAuto(entity))
                {
                    yield return entity;
                }
            }

            yield break;
        }

        foreach (BattleEntity entity in EnumerateAliveFaction(BattleFaction.Player))
        {
            if (CanRunAlliedAuto(entity))
            {
                yield return entity;
            }
        }
    }

    private IReadOnlyList<BattleEntity> BuildTurnQueue(BattleFaction activeFaction, BattleEntity activeEntity)
    {
        if (activeFaction == BattleFaction.Player)
        {
            if (_isAlliedAutoPhaseRunning)
            {
                List<BattleEntity> queue = EnumerateAlliedAutoUnits()
                    .Concat(EnumerateAliveFaction(BattleFaction.Enemy))
                    .ToList();
                return MoveActiveEntityToFront(queue, activeEntity);
            }

            PrunePlayerActionOrder();
            var playerQueue = new List<BattleEntity>();
            if (activeEntity != null &&
                !BattleRuleQueries.IsDefeated(activeEntity) &&
                activeEntity.GetComponent<FactionComponent>()?.Faction == BattleFaction.Player)
            {
                playerQueue.Add(activeEntity);
            }

            playerQueue.AddRange(_playerActionOrder.Where(entity =>
                entity != activeEntity &&
                !IsPlayerEntityCompleted(entity) &&
                CanAutoSelectForPlayerTurn(entity)));
            playerQueue.AddRange(EnumerateAliveFaction(BattleFaction.Enemy));
            return playerQueue;
        }

        BattleFaction nextFaction = activeFaction == BattleFaction.Enemy
            ? BattleFaction.Player
            : BattleFaction.Enemy;
        List<BattleEntity> combined = EnumerateAliveFaction(activeFaction)
            .Concat(EnumerateAliveFaction(nextFaction))
            .ToList();

        return MoveActiveEntityToFront(combined, activeEntity);
    }

    private static IReadOnlyList<BattleEntity> MoveActiveEntityToFront(List<BattleEntity> queue, BattleEntity activeEntity)
    {
        if (queue == null)
        {
            return System.Array.Empty<BattleEntity>();
        }

        if (activeEntity == null || queue.Count == 0)
        {
            return queue;
        }

        int activeIndex = queue.IndexOf(activeEntity);
        if (activeIndex > 0)
        {
            queue.RemoveAt(activeIndex);
            queue.Insert(0, activeEntity);
        }

        return queue;
    }

    private bool IsPlayerEntityCompleted(BattleEntity entity)
    {
        return entity != null &&
               !string.IsNullOrWhiteSpace(entity.EntityId) &&
               _completedPlayerEntityIds.Contains(entity.EntityId);
    }

    private static bool CanAutoSelectForPlayerTurn(BattleEntity entity)
    {
        if (entity == null)
        {
            return false;
        }

        SelectableComponent selectable = entity.GetComponent<SelectableComponent>();
        if (selectable is { IsSelectable: false })
        {
            return false;
        }

        ActionPointComponent actionPoint = entity.GetComponent<ActionPointComponent>();
        return actionPoint == null || actionPoint.Ap > 0;
    }

    private static bool CanRunAlliedAuto(BattleEntity entity)
    {
        if (entity == null ||
            BattleRuleQueries.IsDefeated(entity) ||
            entity.GetComponent<FactionComponent>()?.Faction != BattleFaction.Player)
        {
            return false;
        }

        SelectableComponent selectable = entity.GetComponent<SelectableComponent>();
        if (selectable is not { IsSelectable: false })
        {
            return false;
        }

        ActionPointComponent actionPoint = entity.GetComponent<ActionPointComponent>();
        return actionPoint == null || actionPoint.Ap > 0;
    }

    private void ClearActionPreviewHighlights()
    {
        _previewController?.ClearActionPreviewHighlights();
    }

    private async Task RunActionWithCueAsync(BattleEntity entity, BattleFaction faction, System.Func<Task> action)
    {
        _isActionCueRunning = true;
        try
        {
            await _actionCueSequencer.RunAsync(
                entity?.EntityId,
                faction,
                cue => ShowActionCueAsync(entity, cue.Faction, cue.DurationSeconds),
                WaitSeconds,
                action,
                _ => HideActionCueAsync(entity));
        }
        finally
        {
            _isActionCueRunning = false;
        }
    }

    private async void PlayPlayerActionCue(BattleEntity entity)
    {
        _isActionCueRunning = true;
        try
        {
            await _actionCueSequencer.RunAsync(
                entity?.EntityId,
                BattleFaction.Player,
                cue => ShowActionCueAsync(entity, cue.Faction, cue.DurationSeconds),
                WaitSeconds,
                () => Task.CompletedTask,
                _ => HideActionCueAsync(entity));
        }
        finally
        {
            _isActionCueRunning = false;
        }
    }

    private Task ShowActionCueAsync(BattleEntity entity, BattleFaction faction, double durationSeconds)
    {
        return _showActionCue?.Invoke(entity, faction, durationSeconds) ?? Task.CompletedTask;
    }

    private Task HideActionCueAsync(BattleEntity entity)
    {
        return _hideActionCue?.Invoke(entity) ?? Task.CompletedTask;
    }

    private async Task WaitSeconds(double seconds)
    {
        if (!IsInsideTree() || seconds <= 0)
        {
            return;
        }

        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    }
}
