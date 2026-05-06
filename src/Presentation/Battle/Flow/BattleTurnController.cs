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
    private System.Action _generateEnemyIntents;
    private System.Func<BattleEntity, Task> _executeEnemyAction;
    private System.Action<BattleEntity> _clearIntentBookkeeping;
    private System.Action _markBattleStateChanged;
    private System.Action<string> _showHint;
    private System.Action _clearHudCommand;
    private System.Action<BattleOutcome> _battleEnded;

    private bool _isEnemyPhaseRunning;
    private int _roundNumber = 1;
    private BattleOutcome _finalOutcome = BattleOutcome.None;

    public bool IsEnemyPhaseRunning => _isEnemyPhaseRunning;
    public int RoundNumber => _roundNumber;

    public void Initialize(
        BattleUnitRoot unitRoot,
        BattleCommandController commandController,
        BattlePreviewController previewController,
        System.Func<IReadOnlyList<BattleEntity>> getEntitiesSnapshot,
        System.Action generateEnemyIntents,
        System.Func<BattleEntity, Task> executeEnemyAction,
        System.Action<BattleEntity> clearIntentBookkeeping,
        System.Action markBattleStateChanged,
        System.Action<string> showHint,
        System.Action clearHudCommand,
        System.Action<BattleOutcome> battleEnded)
    {
        _unitRoot = unitRoot;
        _commandController = commandController;
        _previewController = previewController;
        _getEntitiesSnapshot = getEntitiesSnapshot;
        _generateEnemyIntents = generateEnemyIntents;
        _executeEnemyAction = executeEnemyAction;
        _clearIntentBookkeeping = clearIntentBookkeeping;
        _markBattleStateChanged = markBattleStateChanged;
        _showHint = showHint;
        _clearHudCommand = clearHudCommand;
        _battleEnded = battleEnded;

        GameLog.Info(
            nameof(BattleTurnController),
            $"Initialized unitRoot={_unitRoot != null} command={_commandController != null} preview={_previewController != null} executor={_executeEnemyAction != null}");
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

        RunEnemyPhaseThenBeginPlayerPhase();
    }

    public void HandleEntityDefeated(BattleEntity entity)
    {
        _clearIntentBookkeeping?.Invoke(entity);
        _unitRoot?.MarkEntityDefeated(entity);
        _markBattleStateChanged?.Invoke();
        GameLog.Info(nameof(BattleTurnController), $"Entity defeated handled id={entity?.EntityId} name={entity?.DisplayName}");
    }

    public bool EvaluateBattleOutcome()
    {
        if (_finalOutcome != BattleOutcome.None)
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

    private void CompleteBattle(BattleOutcome outcome, string hint)
    {
        if (_finalOutcome != BattleOutcome.None)
        {
            return;
        }

        _finalOutcome = outcome;
        _showHint?.Invoke(hint);
        GameLog.Info(nameof(BattleTurnController), $"Battle outcome {outcome}.");
        _battleEnded?.Invoke(outcome);
    }

    private async void RunEnemyPhaseThenBeginPlayerPhase()
    {
        if (_isEnemyPhaseRunning)
        {
            return;
        }

        _isEnemyPhaseRunning = true;
        ClearActionPreviewHighlights();
        _showHint?.Invoke("敌方行动");
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
                await _executeEnemyAction(enemy);
            }
        }

        _isEnemyPhaseRunning = false;

        if (!EvaluateBattleOutcome())
        {
            BeginPlayerPhase();
        }
    }

    private void BeginPlayerPhase(bool advanceRound = true)
    {
        if (advanceRound)
        {
            _roundNumber++;
        }

        _unitRoot?.RestoreTurnResourcesForFaction(BattleFaction.Player);
        _unitRoot?.RestoreTurnResourcesForFaction(BattleFaction.Enemy);
        _generateEnemyIntents?.Invoke();
        ClearActionPreviewHighlights();
        _clearHudCommand?.Invoke();
        _showHint?.Invoke("我方行动");
        AutoSelectPlayerUnitForTurn();
        GameLog.Info(nameof(BattleTurnController), $"Player phase started round={_roundNumber}");
    }

    private void AutoSelectPlayerUnitForTurn()
    {
        BattleEntity entity = EnumerateAliveFaction(BattleFaction.Player)
            .FirstOrDefault(CanAutoSelectForPlayerTurn);

        if (entity == null)
        {
            _commandController?.ClearSelection();
            GameLog.Warn(nameof(BattleTurnController), "Player phase has no selectable player unit.");
            return;
        }

        _commandController?.SelectEntity(entity);
        GameLog.Info(nameof(BattleTurnController), $"Auto selected player unit for turn id={entity.EntityId} name={entity.DisplayName}");
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

    private void ClearActionPreviewHighlights()
    {
        _previewController?.ClearActionPreviewHighlights();
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
