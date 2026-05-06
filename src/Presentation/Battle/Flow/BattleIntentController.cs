using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle.Actions;
using Rpg.Presentation.Battle.AI;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Intents;
using Rpg.Presentation.Battle.Preview;

namespace Rpg.Presentation.Battle.Flow;

public partial class BattleIntentController : Node
{
    private BattleUnitRoot _unitRoot;
    private BattlePreviewController _previewController;
    private Func<BattleAiContext> _createAiContext;
    private Func<BattleActionRequest, BattleActionResult> _executeActionRequest;
    private Func<bool> _evaluateBattleOutcome;
    private Action<string> _showHint;
    private Action _markBattleStateChanged;
    private Func<double> _getUnitMoveDuration;

    private readonly IEnemyIntentPlanner _enemyIntentPlanner = new GreedyEnemyIntentPlanner();
    private readonly BattleIntentResolver _intentResolver = new();
    private readonly Dictionary<BattleEntity, BattleIntent> _enemyIntents = new();

    public void Initialize(
        BattleUnitRoot unitRoot,
        BattlePreviewController previewController,
        Func<BattleAiContext> createAiContext,
        Func<BattleActionRequest, BattleActionResult> executeActionRequest,
        Func<bool> evaluateBattleOutcome,
        Action<string> showHint,
        Action markBattleStateChanged,
        Func<double> getUnitMoveDuration)
    {
        _unitRoot = unitRoot;
        _previewController = previewController;
        _createAiContext = createAiContext;
        _executeActionRequest = executeActionRequest;
        _evaluateBattleOutcome = evaluateBattleOutcome;
        _showHint = showHint;
        _markBattleStateChanged = markBattleStateChanged;
        _getUnitMoveDuration = getUnitMoveDuration;

        GameLog.Info(
            nameof(BattleIntentController),
            $"Initialized unitRoot={_unitRoot != null} preview={_previewController != null} executor={_executeActionRequest != null}");
    }

    public void GenerateEnemyIntents()
    {
        _unitRoot?.ClearIntentMarkers();
        _enemyIntents.Clear();

        foreach (BattleEntity enemy in EnumerateAliveEnemies().ToArray())
        {
            BattleIntent intent = BuildIntentForEnemy(enemy);
            _enemyIntents[enemy] = intent;
            _unitRoot?.SetIntentMarker(enemy, intent);
            GameLog.Info(
                nameof(BattleIntentController),
                $"Intent generated actor={enemy.EntityId} template={intent.TemplateId} type={intent.Type} policy={intent.TargetPolicy} power={intent.Power} summary={intent.Summary}");
        }

        _markBattleStateChanged?.Invoke();
    }

    public BattleIntent GetEnemyIntent(BattleEntity enemy)
    {
        return enemy != null && _enemyIntents.TryGetValue(enemy, out BattleIntent intent)
            ? intent
            : null;
    }

    public async Task ExecuteEnemyAction(BattleEntity enemy)
    {
        if (!_enemyIntents.TryGetValue(enemy, out BattleIntent intent))
        {
            intent = BuildIntentForEnemy(enemy);
            _enemyIntents[enemy] = intent;
            GameLog.Warn(nameof(BattleIntentController), $"Enemy intent was missing and generated late enemy={enemy.EntityId}");
        }

        BattleIntentPreview preview = _intentResolver.Preview(_createAiContext?.Invoke(), intent);
        if (intent == null || !preview.HasAction)
        {
            _unitRoot?.SetIntentMarker(enemy, null);
            _showHint?.Invoke(preview?.DetailText ?? $"{enemy.DisplayName} 无法行动");
            GameLog.Info(nameof(BattleIntentController), $"Enemy intent skipped enemy={enemy.EntityId} template={intent?.TemplateId} reason={preview?.Request?.Reason}");
            await WaitSeconds(0.25);
            return;
        }

        _previewController?.ApplyIntentHighlights(preview);
        _showHint?.Invoke(BattlePreviewController.DescribeIntentForCurrentState(preview));
        await WaitSeconds(0.25);

        BattleActionResult result = _executeActionRequest?.Invoke(preview.Request) ??
            BattleActionResult.Failed(
                preview.Request.Kind,
                preview.Request.Actor,
                preview.Request.Target,
                preview.Request.Destination,
                "敌方意图执行入口未初始化");
        _enemyIntents.Remove(enemy);
        _unitRoot?.SetIntentMarker(enemy, null);
        if (!result.Success)
        {
            _showHint?.Invoke($"{enemy.DisplayName} 的意图未能执行：{result.Message}");
            GameLog.Info(nameof(BattleIntentController), $"Enemy intent failed enemy={enemy.EntityId} template={intent.TemplateId} resolvedKind={preview.Kind} reason={result.Message}");
            await WaitSeconds(0.35);
            ClearActionPreviewHighlights();
            return;
        }

        if (result.Kind == BattleActionKind.Move)
        {
            _previewController?.ShowTargetCells(new[] { result.Destination });
            _showHint?.Invoke(result.Message);
            GameLog.Info(nameof(BattleIntentController), $"Enemy intent move resolved id={enemy.EntityId} to={result.Destination}");
            await WaitSeconds(GetUnitMoveDuration() * Math.Max(1, result.MovementStepCount) + 0.12);
            ClearActionPreviewHighlights();
            return;
        }

        if (result.Kind == BattleActionKind.Ability || result.Kind == BattleActionKind.Attack)
        {
            GridOccupantComponent targetGrid = result.Target?.GetComponent<GridOccupantComponent>();
            if (targetGrid != null)
            {
                _previewController?.ShowTargetCells(new[] { targetGrid.Position });
            }

            _showHint?.Invoke(result.Message);
            _evaluateBattleOutcome?.Invoke();
            GameLog.Info(
                nameof(BattleIntentController),
                $"Enemy intent ability resolved attacker={enemy.EntityId} template={intent.TemplateId} target={result.Target?.EntityId} ability={result.Ability?.Id} damage={result.DamageApplied} defeated={result.TargetDefeated}");
            await WaitSeconds(0.5);
            ClearActionPreviewHighlights();
        }
    }

    public void ClearEnemyIntentBookkeeping(BattleEntity entity)
    {
        _enemyIntents.Remove(entity);
    }

    private BattleIntent BuildIntentForEnemy(BattleEntity enemy)
    {
        return _enemyIntentPlanner.ChooseIntent(_createAiContext?.Invoke(), enemy);
    }

    private IEnumerable<BattleEntity> EnumerateAliveEnemies()
    {
        if (_unitRoot == null)
        {
            yield break;
        }

        foreach (BattleEntity entity in _unitRoot.EnumerateAliveFaction(BattleFaction.Enemy))
        {
            yield return entity;
        }
    }

    private void ClearActionPreviewHighlights()
    {
        _previewController?.ClearActionPreviewHighlights();
    }

    private double GetUnitMoveDuration()
    {
        return _getUnitMoveDuration?.Invoke() ?? 0.28;
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
