using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Rpg.Definitions.Battle.Abilities;
using Rpg.Domain.Battle.Grid;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle.Abilities;
using Rpg.Presentation.Battle.Actions;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.InputSystem;
using Rpg.Presentation.Battle.Preview;
using Rpg.Presentation.Battle.Rules;

namespace Rpg.Presentation.Battle.Flow;

public partial class BattleCommandController : Node
{
    private BattlePreviewController _previewController;
    private System.Func<bool> _isEnemyPhaseRunning;
    private System.Func<BattleGridMap> _getGridMap;
    private System.Func<GridPosition, BattleEntity> _findEntityAt;
    private System.Func<BattleActionRequest, BattleActionResult> _executeActionRequest;
    private System.Action<string> _endPlayerTurn;
    private System.Func<bool> _evaluateOutcome;
    private System.Action<BattleEntity> _showEntity;
    private System.Action<string> _showHint;
    private System.Action _clearHudCommand;
    private System.Action _hideHudCommand;
    private System.Func<BattleEntity, string> _getSelectionBlockReason;
    private System.Func<bool> _hasActiveMovementTweens;
    private int _restoreActionUiVersion;

    private BattleEntity _selectedEntity;
    private readonly List<BattleInteractionFrame> _interactionStack = new();

    public BattleEntity SelectedEntity => _selectedEntity;

    public bool AllowsDebugHoverInfo => !IsEnemyPhaseRunning() && _interactionStack.Count == 0;

    public bool IsMoveTargeting =>
        !IsEnemyPhaseRunning() &&
        _interactionStack.Count > 0 &&
        PeekInteractionFrame().Kind == BattleInteractionFrameKind.MoveTargeting;

    public bool CanShowHoverIntentPreview
    {
        get
        {
            if (_previewController == null || _getGridMap?.Invoke() == null)
            {
                return false;
            }

            return !IsEnemyPhaseRunning() &&
                   (_interactionStack.Count == 0 ||
                    PeekInteractionFrame().Kind == BattleInteractionFrameKind.UnitSelected);
        }
    }

    public void Initialize(
        BattlePreviewController previewController,
        System.Func<bool> isEnemyPhaseRunning,
        System.Func<BattleGridMap> getGridMap,
        System.Func<GridPosition, BattleEntity> findEntityAt,
        System.Func<BattleActionRequest, BattleActionResult> executeActionRequest,
        System.Action<string> endPlayerTurn,
        System.Func<bool> evaluateOutcome,
        System.Action<BattleEntity> showEntity,
        System.Action<string> showHint,
        System.Action clearHudCommand,
        System.Action hideHudCommand,
        System.Func<BattleEntity, string> getSelectionBlockReason,
        System.Func<bool> hasActiveMovementTweens)
    {
        _previewController = previewController;
        _isEnemyPhaseRunning = isEnemyPhaseRunning;
        _getGridMap = getGridMap;
        _findEntityAt = findEntityAt;
        _executeActionRequest = executeActionRequest;
        _endPlayerTurn = endPlayerTurn;
        _evaluateOutcome = evaluateOutcome;
        _showEntity = showEntity;
        _showHint = showHint;
        _clearHudCommand = clearHudCommand;
        _hideHudCommand = hideHudCommand;
        _getSelectionBlockReason = getSelectionBlockReason;
        _hasActiveMovementTweens = hasActiveMovementTweens;

        GameLog.Info(
            nameof(BattleCommandController),
            $"Initialized preview={_previewController != null} finder={_findEntityAt != null} executor={_executeActionRequest != null}");
    }

    public bool OnBattleCommandRequested(BattleCommand command)
    {
        switch (command.Kind)
        {
            case BattleCommandKind.GridCellClicked:
                if (IsEnemyPhaseRunning() || command.GridPosition is not { } position)
                {
                    return false;
                }

                return TryHandleGridCellClick(position) ||
                       TryHandleGridEntityClick(position);

            case BattleCommandKind.HudCommandSelected:
                OnHudCommandSelected(command.CommandId);
                return true;

            case BattleCommandKind.HudCommandCancelled:
                OnHudCommandCancelled();
                return true;

            default:
                GameLog.Warn(nameof(BattleCommandController), $"Unknown battle command kind={command.Kind}");
                return false;
        }
    }

    public void SelectEntity(BattleEntity entity)
    {
        if (entity == null)
        {
            ClearSelection();
            return;
        }

        SelectableComponent selectable = entity.GetComponent<SelectableComponent>();
        if (selectable is { IsSelectable: false })
        {
            GameLog.Info(nameof(BattleCommandController), $"Selection ignored because entity is not selectable id={entity.EntityId} name={entity.DisplayName}");
            return;
        }

        string blockReason = _getSelectionBlockReason?.Invoke(entity) ?? "";
        if (!string.IsNullOrWhiteSpace(blockReason))
        {
            _showHint?.Invoke(blockReason);
            GameLog.Info(nameof(BattleCommandController), $"Selection blocked id={entity.EntityId} name={entity.DisplayName} reason={blockReason}");
            return;
        }

        _restoreActionUiVersion++;
        _selectedEntity = entity;
        GameLog.Info(nameof(BattleCommandController), $"Selected entity id={entity.EntityId} name={entity.DisplayName}");
        ResetInteractionStack(entity);
        _showEntity?.Invoke(entity);
        _previewController?.UpdateSelectedHighlight(entity);
        ClearActionPreviewHighlights();
    }

    public void ClearSelection()
    {
        BattleEntity previous = _selectedEntity;
        _restoreActionUiVersion++;
        _selectedEntity = null;
        _interactionStack.Clear();
        _showEntity?.Invoke(null);
        _clearHudCommand?.Invoke();
        _previewController?.ClearSelectedHighlight();
        ClearActionPreviewHighlights();
        GameLog.Info(nameof(BattleCommandController), $"Selection cleared previous={previous?.EntityId}");
    }

    private void OnBattleEntityClicked(BattleEntity entity)
    {
        GameLog.Info(nameof(BattleCommandController), $"Entity click received id={entity?.EntityId} name={entity?.DisplayName}");

        if (IsEnemyPhaseRunning())
        {
            GameLog.Info(nameof(BattleCommandController), $"Entity click ignored during enemy phase id={entity?.EntityId}");
            return;
        }

        if (TryHandleTargetEntityClick(entity))
        {
            return;
        }

        SelectEntity(entity);
    }

    private bool TryHandleGridEntityClick(GridPosition position)
    {
        BattleEntity entity = _findEntityAt?.Invoke(position);
        if (entity == null)
        {
            return false;
        }

        OnBattleEntityClicked(entity);
        return true;
    }

    private void OnHudCommandSelected(string commandId)
    {
        if (IsEnemyPhaseRunning())
        {
            GameLog.Info(nameof(BattleCommandController), $"Command ignored during enemy phase command={commandId}");
            _clearHudCommand?.Invoke();
            return;
        }

        if (_selectedEntity == null)
        {
            GameLog.Info(nameof(BattleCommandController), $"Command ignored because no entity is selected command={commandId}");
            _clearHudCommand?.Invoke();
            return;
        }

        switch (commandId)
        {
            case "move":
                EnterMoveTargeting(commandId);
                break;

            case "attack":
                EnterAbilityTargeting(commandId);
                break;

            case "wait":
                ResolveWaitCommand();
                break;

            case "end":
                ResolveEndCommand();
                break;

            default:
                if (BattleAbilityQueries.IsAbilityCommand(commandId))
                {
                    EnterAbilityTargeting(commandId);
                }
                else
                {
                    PushActionFrame(BattleInteractionFrameKind.CommandSelected, commandId);
                    ClearActionPreviewHighlights();
                    GameLog.Info(nameof(BattleCommandController), $"Command selected id={_selectedEntity.EntityId} command={commandId}; resolution is not implemented yet.");
                }

                break;
        }
    }

    private void OnHudCommandCancelled()
    {
        if (IsEnemyPhaseRunning())
        {
            GameLog.Info(nameof(BattleCommandController), "Cancel ignored during enemy phase.");
            return;
        }

        if (_interactionStack.Count == 0)
        {
            return;
        }

        BattleInteractionFrame top = PeekInteractionFrame();
        if (top.Kind == BattleInteractionFrameKind.UnitSelected)
        {
            ClearSelection();
            return;
        }

        _interactionStack.RemoveAt(_interactionStack.Count - 1);
        ClearActionPreviewHighlights();
        _clearHudCommand?.Invoke();
        RestoreSelectedActionMenu();
        GameLog.Info(nameof(BattleCommandController), $"Interaction pop kind={top.Kind} command={top.CommandId} stackDepth={_interactionStack.Count}");
    }

    private void ResetInteractionStack(BattleEntity entity)
    {
        _interactionStack.Clear();
        _interactionStack.Add(new BattleInteractionFrame(BattleInteractionFrameKind.UnitSelected, entity, ""));
        _clearHudCommand?.Invoke();
        GameLog.Info(nameof(BattleCommandController), $"Interaction stack reset selectedEntity={entity.EntityId} depth={_interactionStack.Count}");
    }

    private void PushActionFrame(BattleInteractionFrameKind kind, string commandId)
    {
        while (_interactionStack.Count > 0 && PeekInteractionFrame().Kind != BattleInteractionFrameKind.UnitSelected)
        {
            _interactionStack.RemoveAt(_interactionStack.Count - 1);
        }

        _interactionStack.Add(new BattleInteractionFrame(kind, _selectedEntity, commandId));
        GameLog.Info(nameof(BattleCommandController), $"Interaction push kind={kind} command={commandId} stackDepth={_interactionStack.Count}");
    }

    private BattleInteractionFrame PeekInteractionFrame()
    {
        return _interactionStack[^1];
    }

    private void ReturnToUnitSelectedFrame()
    {
        while (_interactionStack.Count > 0 && PeekInteractionFrame().Kind != BattleInteractionFrameKind.UnitSelected)
        {
            BattleInteractionFrame removed = PeekInteractionFrame();
            _interactionStack.RemoveAt(_interactionStack.Count - 1);
            GameLog.Info(nameof(BattleCommandController), $"Interaction pop kind={removed.Kind} command={removed.CommandId} stackDepth={_interactionStack.Count}");
        }
    }

    private void EnterMoveTargeting(string commandId)
    {
        MovementComponent movement = _selectedEntity.GetComponent<MovementComponent>();
        if (movement == null || movement.MoveRange <= 0)
        {
            ClearActionPreviewHighlights();
            _clearHudCommand?.Invoke();
            _showHint?.Invoke("该单位不能移动");
            GameLog.Info(nameof(BattleCommandController), $"Move command rejected id={_selectedEntity.EntityId} hasMovement={movement != null}");
            return;
        }

        if (!movement.CanUseMove())
        {
            ClearActionPreviewHighlights();
            _clearHudCommand?.Invoke();
            _showHint?.Invoke("移动次数不足");
            GameLog.Info(nameof(BattleCommandController), $"Move command rejected by move uses id={_selectedEntity.EntityId} remaining={movement.MoveUsesRemaining}");
            return;
        }

        if (!BattleRuleQueries.CanSpendActionPoints(_selectedEntity, movement.ApCost))
        {
            ClearActionPreviewHighlights();
            _clearHudCommand?.Invoke();
            _showHint?.Invoke("行动点不足");
            GameLog.Info(nameof(BattleCommandController), $"Move command rejected by AP id={_selectedEntity.EntityId} cost={movement.ApCost}");
            return;
        }

        PushActionFrame(BattleInteractionFrameKind.MoveTargeting, commandId);
        HideSelectedActionMenuForTargeting(commandId);
        _previewController?.ShowMovementRange(_selectedEntity);
    }

    private void EnterAbilityTargeting(string commandId)
    {
        if (!BattleAbilityQueries.TryGetAbilityByCommandId(_selectedEntity, commandId, out AbilityDefinition ability))
        {
            ClearActionPreviewHighlights();
            _clearHudCommand?.Invoke();
            _showHint?.Invoke("该单位没有可用能力");
            GameLog.Info(nameof(BattleCommandController), $"Ability command rejected id={_selectedEntity.EntityId} command={commandId} reason=missing_ability");
            return;
        }

        if (!BattleAbilityQueries.CanUseAbility(_selectedEntity, ability, out string reason))
        {
            ClearActionPreviewHighlights();
            _clearHudCommand?.Invoke();
            _showHint?.Invoke(reason);
            GameLog.Info(nameof(BattleCommandController), $"Ability command rejected id={_selectedEntity.EntityId} ability={ability.Id} reason={reason}");
            return;
        }

        PushActionFrame(BattleInteractionFrameKind.AbilityTargeting, commandId);
        HideSelectedActionMenuForTargeting(commandId);
        _previewController?.ShowAbilityTargetHighlight(_selectedEntity, ability);
    }

    private void ResolveWaitCommand()
    {
        BattleEntity entity = _selectedEntity;
        ActionPointComponent actionPoint = _selectedEntity.GetComponent<ActionPointComponent>();
        if (actionPoint != null)
        {
            actionPoint.Ap = 0;
        }

        GameLog.Info(nameof(BattleCommandController), $"Wait resolved id={entity.EntityId}");
        ClearActionPreviewHighlights();
        _clearHudCommand?.Invoke();
        _endPlayerTurn?.Invoke($"{entity.DisplayName} 已待机");
    }

    private void ResolveEndCommand()
    {
        BattleEntity entity = _selectedEntity;
        ActionPointComponent actionPoint = _selectedEntity.GetComponent<ActionPointComponent>();
        if (actionPoint != null)
        {
            actionPoint.Ap = 0;
        }

        GameLog.Info(nameof(BattleCommandController), $"End command resolved id={entity.EntityId}");
        ClearActionPreviewHighlights();
        _clearHudCommand?.Invoke();
        _endPlayerTurn?.Invoke($"{entity.DisplayName} 行动结束");
    }

    private bool TryHandleGridCellClick(GridPosition position)
    {
        if (_interactionStack.Count == 0)
        {
            return false;
        }

        BattleInteractionFrame top = PeekInteractionFrame();
        if (top.Kind == BattleInteractionFrameKind.MoveTargeting)
        {
            return TryResolveMovement(position);
        }

        if (top.Kind == BattleInteractionFrameKind.AbilityTargeting)
        {
            BattleEntity target = _findEntityAt?.Invoke(position);
            if (target != null)
            {
                return TryResolveAbility(_selectedEntity, target, top.CommandId);
            }

            _previewController?.ShowInvalidCell(position);
            _showHint?.Invoke("这里没有可选目标");
            GameLog.Info(nameof(BattleCommandController), $"Ability target rejected emptyCell={position} command={top.CommandId}");
            return true;
        }

        return false;
    }

    private bool TryHandleTargetEntityClick(BattleEntity entity)
    {
        if (entity == null || _interactionStack.Count == 0)
        {
            return false;
        }

        BattleInteractionFrame top = PeekInteractionFrame();
        return top.Kind switch
        {
            BattleInteractionFrameKind.AbilityTargeting => TryResolveAbility(_selectedEntity, entity, top.CommandId),
            BattleInteractionFrameKind.MoveTargeting => TryResolveMovement(entity.GetComponent<GridOccupantComponent>()?.Position ?? default),
            _ => false
        };
    }

    private bool TryResolveMovement(GridPosition targetPosition)
    {
        if (_selectedEntity == null || _previewController?.HasActiveMovementRange != true)
        {
            return false;
        }

        GridOccupantComponent gridOccupant = _selectedEntity.GetComponent<GridOccupantComponent>();
        GridPosition previousPosition = gridOccupant?.Position ?? default;
        BattleActionResult result = _executeActionRequest?.Invoke(BattleActionRequest.Move(_selectedEntity, targetPosition)) ??
                                    BattleActionResult.Failed(BattleActionKind.Move, _selectedEntity, null, targetPosition, "行动执行器未初始化");
        if (!result.Success)
        {
            _previewController?.ShowInvalidCell(targetPosition);
            _showHint?.Invoke(result.Message);
            GameLog.Info(nameof(BattleCommandController), $"Move rejected id={_selectedEntity.EntityId} target={targetPosition} reason={result.Message}");
            return true;
        }

        ReturnToUnitSelectedFrame();
        _clearHudCommand?.Invoke();
        _showHint?.Invoke(result.Message);
        ClearActionPreviewHighlights();
        _previewController?.UpdateSelectedHighlight(_selectedEntity);
        RestoreSelectedActionMenuAfterPresentation(_selectedEntity, minimumDelaySeconds: 0, waitForMovement: true);

        GameLog.Info(
            nameof(BattleCommandController),
            $"Move resolved id={_selectedEntity.EntityId} from={previousPosition} to={targetPosition}");
        return true;
    }

    private bool TryResolveAbility(BattleEntity attacker, BattleEntity target, string commandId)
    {
        if (attacker == null || target == null)
        {
            return false;
        }

        if (!BattleAbilityQueries.TryGetAbilityByCommandId(attacker, commandId, out AbilityDefinition ability))
        {
            _showHint?.Invoke("该单位没有可用能力");
            GameLog.Info(nameof(BattleCommandController), $"Ability rejected attacker={attacker.EntityId} target={target.EntityId} command={commandId} reason=missing_ability");
            return true;
        }

        BattleActionResult result = _executeActionRequest?.Invoke(BattleActionRequest.UseAbility(attacker, target, ability)) ??
                                    BattleActionResult.Failed(BattleActionKind.Ability, attacker, target, default, "行动执行器未初始化");
        if (!result.Success)
        {
            GridOccupantComponent targetGrid = target.GetComponent<GridOccupantComponent>();
            if (targetGrid != null)
            {
                _previewController?.ShowInvalidCell(targetGrid.Position);
            }

            _showHint?.Invoke(result.Message);
            GameLog.Info(nameof(BattleCommandController), $"Ability rejected attacker={attacker.EntityId} target={target.EntityId} ability={ability.Id} reason={result.Message}");
            return true;
        }

        double presentationDelaySeconds = ResolveActionPresentationSeconds(attacker);
        _showHint?.Invoke(result.Message);

        ReturnToUnitSelectedFrame();
        _clearHudCommand?.Invoke();
        ClearActionPreviewHighlights();
        _previewController?.UpdateSelectedHighlight(attacker);
        bool battleEnded = _evaluateOutcome?.Invoke() == true;
        if (!battleEnded)
        {
            RestoreSelectedActionMenuAfterPresentation(attacker, presentationDelaySeconds, waitForMovement: false);
        }

        GameLog.Info(
            nameof(BattleCommandController),
            $"Ability resolved attacker={attacker.EntityId} target={target.EntityId} ability={ability.Id} damage={result.DamageApplied} defeated={result.TargetDefeated}");
        return true;
    }

    private void ClearActionPreviewHighlights()
    {
        _previewController?.ClearActionPreviewHighlights();
    }

    private void HideSelectedActionMenuForTargeting(string commandId)
    {
        _hideHudCommand?.Invoke();
        GameLog.Info(nameof(BattleCommandController), $"Action menu hidden for targeting id={_selectedEntity?.EntityId} command={commandId}");
    }

    private void RestoreSelectedActionMenu()
    {
        if (!CanRestoreSelectedActionMenu(_selectedEntity))
        {
            return;
        }

        _showEntity?.Invoke(_selectedEntity);
    }

    private async void RestoreSelectedActionMenuAfterPresentation(
        BattleEntity entity,
        double minimumDelaySeconds,
        bool waitForMovement)
    {
        int restoreVersion = ++_restoreActionUiVersion;
        await WaitForActionPresentationAsync(minimumDelaySeconds, waitForMovement);

        if (restoreVersion != _restoreActionUiVersion ||
            !CanRestoreSelectedActionMenu(entity))
        {
            return;
        }

        _showEntity?.Invoke(entity);
        GameLog.Info(nameof(BattleCommandController), $"Action menu restored after presentation id={entity.EntityId}");
    }

    private bool CanRestoreSelectedActionMenu(BattleEntity entity)
    {
        return entity != null &&
               entity == _selectedEntity &&
               GodotObject.IsInstanceValid(entity) &&
               !IsEnemyPhaseRunning() &&
               !BattleRuleQueries.IsDefeated(entity) &&
               _interactionStack.Count > 0 &&
               PeekInteractionFrame().Kind == BattleInteractionFrameKind.UnitSelected;
    }

    private async Task WaitForActionPresentationAsync(double minimumDelaySeconds, bool waitForMovement)
    {
        if (minimumDelaySeconds > 0 && IsInsideTree())
        {
            await ToSignal(GetTree().CreateTimer(minimumDelaySeconds), SceneTreeTimer.SignalName.Timeout);
        }

        while (waitForMovement &&
               IsInsideTree() &&
               _hasActiveMovementTweens?.Invoke() == true)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private static double ResolveActionPresentationSeconds(BattleEntity actor)
    {
        return actor?.GetComponent<UnitAnimationComponent>()?.ResolveAttackDurationSeconds() ?? 0;
    }

    private bool IsEnemyPhaseRunning()
    {
        return _isEnemyPhaseRunning?.Invoke() == true;
    }

    private enum BattleInteractionFrameKind
    {
        UnitSelected,
        MoveTargeting,
        AbilityTargeting,
        CommandSelected
    }

    private readonly record struct BattleInteractionFrame(
        BattleInteractionFrameKind Kind,
        BattleEntity Entity,
        string CommandId);
}
