using System;
using System.Collections.Generic;
using Godot;
using Rpg.Definitions.Battle.Abilities;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Common;
using Rpg.Presentation.Battle.Abilities;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Rules;

namespace Rpg.Presentation.Battle.UI;

public partial class BattleHudRoot : Control
{
    public event Action<string> CommandSelected;
    public event Action CommandCancelled;

    private TopTurnBar _topTurnBar;
    private UnitStatusCard _unitStatusCard;
    private BattleActionMenu _actionMenu;
    private CommandInfoPanel _commandInfoPanel;
    private FloatingActionHint _floatingActionHint;
    private Tween _selectionUiTween;
    private bool _selectionUiVisible;
    private Vector2 _actionMenuShownPosition;
    private BattleEntity _selectedHudEntity;
    private BattleFaction _activeFaction = BattleFaction.Player;

    private const float ActionMenuWidth = 168f;
    private const float ActionMenuButtonHeight = 40f;
    private const float ActionMenuVerticalPadding = 24f;
    private const float ActionMenuVerticalSeparation = 5f;
    private const float ActionMenuEntityGap = 34f;
    private const float HudEdgePadding = 12f;
    private const float SelectionUiHiddenOffsetY = 42f;
    private const double SelectionUiShowDuration = 0.34;
    private const double SelectionUiHideDuration = 0.24;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        BuildHud();
        ConfigureDemoState();
        GameLog.Info(nameof(BattleHudRoot), "HUD ready.");
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            LayoutHud();
        }
    }

    public override void _Process(double delta)
    {
        UpdateSelectionUiFollowPosition();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsCancelEvent(@event))
        {
            return;
        }

        CommandCancelled?.Invoke();
        GetViewport().SetInputAsHandled();
    }

    private void BuildHud()
    {
        Control content = GameUiSceneFactory.Instantiate<Control>(
            GameUiSceneFactory.BattleHudContentScenePath,
            nameof(BattleHudRoot));
        if (content == null)
        {
            return;
        }

        AddChild(content);
        _topTurnBar = GameUiSceneFactory.GetRequiredNode<TopTurnBar>(
            content,
            "TopTurnBar",
            nameof(BattleHudRoot));
        _unitStatusCard = GameUiSceneFactory.GetRequiredNode<UnitStatusCard>(
            content,
            "UnitStatusCard",
            nameof(BattleHudRoot));
        _actionMenu = GameUiSceneFactory.GetRequiredNode<BattleActionMenu>(
            content,
            "BattleActionMenu",
            nameof(BattleHudRoot));
        _commandInfoPanel = GameUiSceneFactory.GetRequiredNode<CommandInfoPanel>(
            content,
            "CommandInfoPanel",
            nameof(BattleHudRoot));
        _floatingActionHint = GameUiSceneFactory.GetRequiredNode<FloatingActionHint>(
            content,
            "FloatingActionHint",
            nameof(BattleHudRoot));

        if (_actionMenu != null)
        {
            _actionMenu.CommandHovered += OnCommandHovered;
            _actionMenu.CommandSelected += OnCommandSelected;
            _actionMenu.InvalidCommandSelected += OnInvalidCommandSelected;
        }

        LayoutHud();
    }

    private void LayoutHud()
    {
        if (_topTurnBar == null ||
            _commandInfoPanel == null ||
            _unitStatusCard == null ||
            _actionMenu == null ||
            _floatingActionHint == null)
        {
            return;
        }

        float viewportWidth = Mathf.Max(Size.X, 1f);
        float viewportHeight = Mathf.Max(Size.Y, 1f);
        float sidePadding = Mathf.Clamp(viewportWidth * 0.016f, 16f, 32f);
        float availableWidth = Mathf.Max(320f, viewportWidth - sidePadding * 2f);

        _topTurnBar.Size = new Vector2(Mathf.Clamp(viewportWidth * 0.44f, 620f, 860f), 68f);
        _topTurnBar.Size = new Vector2(Mathf.Min(_topTurnBar.Size.X, availableWidth), _topTurnBar.Size.Y);
        _topTurnBar.Position = new Vector2((viewportWidth - _topTurnBar.Size.X) * 0.5f, Mathf.Clamp(viewportHeight * 0.012f, 10f, 18f));

        _actionMenu.Size = new Vector2(ActionMenuWidth, CalculateActionMenuHeight());
        _actionMenuShownPosition = CalculateActionMenuPosition(_selectedHudEntity, _actionMenu.Size);

        _unitStatusCard.Visible = false;
        _commandInfoPanel.Visible = false;

        _floatingActionHint.Size = new Vector2(Mathf.Min(460f, availableWidth), 46f);
        _floatingActionHint.Position = new Vector2((viewportWidth - _floatingActionHint.Size.X) * 0.5f, _topTurnBar.Position.Y + _topTurnBar.Size.Y + 10f);

        ApplySelectionUiPose(_selectionUiVisible);
    }

    private void ConfigureDemoState()
    {
        _topTurnBar?.SetTurnState(1, BattleFaction.Player, Array.Empty<BattleTurnQueueEntry>());
        _unitStatusCard?.SetUnit("未选择", 0, 0, 0, 0);
        _actionMenu?.SetCommands(Array.Empty<BattleActionMenuCommandViewModel>());
        SetSelectionUiVisible(false, false);
    }

    public void ShowTurnQueue(
        int roundNumber,
        BattleFaction activeFaction,
        BattleEntity activeEntity,
        IReadOnlyList<BattleEntity> queue)
    {
        _activeFaction = activeFaction;
        var entries = new List<BattleTurnQueueEntry>();
        if (queue != null)
        {
            foreach (BattleEntity entity in queue)
            {
                if (entity == null)
                {
                    continue;
                }

                HealthComponent health = entity.GetComponent<HealthComponent>();
                ActionPointComponent actionPoint = entity.GetComponent<ActionPointComponent>();
                BattleFaction faction = entity.GetComponent<FactionComponent>()?.Faction ?? BattleFaction.Player;
                entries.Add(new BattleTurnQueueEntry(
                    entity.EntityId,
                    entity.DisplayName,
                    faction,
                    health?.Hp ?? 0,
                    health?.MaxHp ?? 0,
                    actionPoint?.Ap ?? 0,
                    actionPoint?.MaxAp ?? 0,
                    activeEntity == entity,
                    BattleRuleQueries.IsDefeated(entity)));
            }
        }

        _topTurnBar?.SetTurnState(roundNumber, activeFaction, entries);
    }

    public void ShowEntity(BattleEntity entity)
    {
        if (entity == null)
        {
            _selectedHudEntity = null;
            _unitStatusCard?.SetUnit("未选择", 0, 0, 0, 0);
            _actionMenu?.SetCommands(Array.Empty<BattleActionMenuCommandViewModel>());
            SetSelectionUiVisible(false);
            GameLog.Info(nameof(BattleHudRoot), "HUD cleared selected entity.");
            return;
        }

        _selectedHudEntity = entity;
        IReadOnlyList<BattleActionMenuCommandViewModel> commands = BuildUnitMenuCommands(entity, _activeFaction);
        _actionMenu?.SetCommands(commands);

        HealthComponent health = entity.GetComponent<HealthComponent>();
        ActionPointComponent actionPoint = entity.GetComponent<ActionPointComponent>();

        _unitStatusCard?.SetUnit(
            entity.DisplayName,
            health?.Hp ?? 0,
            health?.MaxHp ?? 0,
            actionPoint?.Ap ?? 0,
            actionPoint?.MaxAp ?? 0);

        LayoutHud();
        SetSelectionUiVisible(commands.Count > 0);
        GameLog.Info(
            nameof(BattleHudRoot),
            $"HUD showing entity id={entity.EntityId} name={entity.DisplayName} commands={commands.Count} hp={health?.Hp ?? 0}/{health?.MaxHp ?? 0} ap={actionPoint?.Ap ?? 0}/{actionPoint?.MaxAp ?? 0}");
    }

    public void ClearActiveCommand()
    {
        _actionMenu?.SetActiveCommand("");
    }

    public void HideSelectedActionMenu()
    {
        _actionMenu?.SetActiveCommand("");
        SetSelectionUiVisible(false, animate: false);
        GameLog.Info(nameof(BattleHudRoot), $"Selected action menu hidden entity={_selectedHudEntity?.EntityId}");
    }

    public void ShowActionHint(string text)
    {
        _floatingActionHint?.ShowHint(text);
    }

    private void SetSelectionUiVisible(bool visible, bool animate = true)
    {
        if (_selectionUiVisible == visible && animate)
        {
            return;
        }

        _selectionUiVisible = visible;
        _selectionUiTween?.Kill();
        _selectionUiTween = null;

        if (!animate)
        {
            ApplySelectionUiPose(visible);
            SetSelectionUiNodesVisible(visible);
            GameLog.Info(nameof(BattleHudRoot), $"Selection UI visible={visible} animated=False");
            return;
        }

        if (visible)
        {
            SetSelectionUiNodesVisible(true);
            ApplySelectionUiPose(false);
        }

        _selectionUiTween = CreateTween();
        _selectionUiTween.SetParallel(true);
        _selectionUiTween.SetTrans(visible ? Tween.TransitionType.Back : Tween.TransitionType.Cubic);
        _selectionUiTween.SetEase(visible ? Tween.EaseType.Out : Tween.EaseType.In);

        double duration = visible ? SelectionUiShowDuration : SelectionUiHideDuration;
        TweenSelectionNode(_actionMenu, _actionMenuShownPosition, visible, duration);

        _selectionUiTween.Finished += () =>
        {
            if (!visible)
            {
                SetSelectionUiNodesVisible(false);
            }

            ApplySelectionUiPose(visible);
            _selectionUiTween = null;
        };

        GameLog.Info(nameof(BattleHudRoot), $"Selection UI visible={visible} animated=True");
    }

    private void TweenSelectionNode(Control node, Vector2 shownPosition, bool visible, double duration)
    {
        if (node == null)
        {
            return;
        }

        Vector2 targetPosition = visible
            ? shownPosition
            : GetHiddenPosition(shownPosition);
        var targetColor = new Color(1f, 1f, 1f, visible ? 1f : 0f);

        _selectionUiTween.TweenProperty(node, "position", targetPosition, duration);
        _selectionUiTween.TweenProperty(node, "modulate", targetColor, duration);
    }

    private void ApplySelectionUiPose(bool visible)
    {
        ApplySelectionNodePose(_actionMenu, _actionMenuShownPosition, visible);
        SetSecondarySelectionPanelsVisible(false);
    }

    private void UpdateSelectionUiFollowPosition()
    {
        if (!_selectionUiVisible || _selectedHudEntity == null || _actionMenu == null)
        {
            return;
        }

        if (!GodotObject.IsInstanceValid(_selectedHudEntity))
        {
            _selectedHudEntity = null;
            _actionMenu?.SetCommands(Array.Empty<BattleActionMenuCommandViewModel>());
            SetSelectionUiVisible(false);
            return;
        }

        Vector2 nextPosition = CalculateActionMenuPosition(_selectedHudEntity, _actionMenu.Size);
        if (_actionMenuShownPosition.DistanceSquaredTo(nextPosition) <= 0.01f)
        {
            return;
        }

        _actionMenuShownPosition = nextPosition;
        if (_selectionUiTween != null)
        {
            _selectionUiTween.Kill();
            _selectionUiTween = null;
            SetSelectionUiNodesVisible(true);
        }

        ApplySelectionNodePose(_actionMenu, _actionMenuShownPosition, true);
    }

    private static void ApplySelectionNodePose(Control node, Vector2 shownPosition, bool visible)
    {
        if (node == null)
        {
            return;
        }

        node.Position = visible ? shownPosition : GetHiddenPosition(shownPosition);
        node.Modulate = new Color(1f, 1f, 1f, visible ? 1f : 0f);
    }

    private void SetSelectionUiNodesVisible(bool visible)
    {
        if (_actionMenu != null)
        {
            _actionMenu.Visible = visible;
        }

        SetSecondarySelectionPanelsVisible(false);
    }

    private void SetSecondarySelectionPanelsVisible(bool visible)
    {
        if (_unitStatusCard != null)
        {
            _unitStatusCard.Visible = visible;
        }

        if (_commandInfoPanel != null)
        {
            _commandInfoPanel.Visible = visible;
        }
    }

    private static Vector2 GetHiddenPosition(Vector2 shownPosition)
    {
        return shownPosition + new Vector2(0f, SelectionUiHiddenOffsetY);
    }

    private Vector2 CalculateActionMenuPosition(BattleEntity entity, Vector2 menuSize)
    {
        float viewportWidth = Mathf.Max(Size.X, 1f);
        float viewportHeight = Mathf.Max(Size.Y, 1f);
        Vector2 anchor = entity == null
            ? new Vector2(viewportWidth * 0.5f, viewportHeight * 0.5f)
            : GetViewport().GetCanvasTransform() * entity.GlobalPosition;

        float rightX = anchor.X + ActionMenuEntityGap;
        float leftX = anchor.X - menuSize.X - ActionMenuEntityGap;
        float x = rightX + menuSize.X <= viewportWidth - HudEdgePadding
            ? rightX
            : leftX;
        float minY = _topTurnBar == null
            ? HudEdgePadding
            : _topTurnBar.Position.Y + _topTurnBar.Size.Y + HudEdgePadding;
        float y = anchor.Y - menuSize.Y * 0.5f;

        return new Vector2(
            Mathf.Clamp(x, HudEdgePadding, Mathf.Max(HudEdgePadding, viewportWidth - menuSize.X - HudEdgePadding)),
            Mathf.Clamp(y, minY, Mathf.Max(minY, viewportHeight - menuSize.Y - HudEdgePadding)));
    }

    private float CalculateActionMenuHeight()
    {
        int commandCount = _actionMenu?.CommandCount ?? 0;
        if (commandCount <= 0)
        {
            return 0f;
        }

        return ActionMenuVerticalPadding +
               commandCount * ActionMenuButtonHeight +
               Mathf.Max(commandCount - 1, 0) * ActionMenuVerticalSeparation;
    }

    private static IReadOnlyList<BattleActionMenuCommandViewModel> BuildUnitMenuCommands(BattleEntity entity, BattleFaction activeFaction)
    {
        var commands = new List<BattleActionMenuCommandViewModel>();
        if (!CanShowUnitCommands(entity, activeFaction))
        {
            return commands;
        }

        MovementComponent movement = entity.GetComponent<MovementComponent>();
        ActionPointComponent actionPoint = entity.GetComponent<ActionPointComponent>();

        bool moveEnabled = movement != null &&
                           movement.MoveRange > 0 &&
                           movement.CanUseMove() &&
                           (actionPoint == null || actionPoint.CanSpend(movement.ApCost));
        string moveDisabledReason = movement == null || movement.MoveRange <= 0
            ? "该单位不能移动"
            : !movement.CanUseMove()
                ? "移动次数不足"
                : "行动点不足";

        AddEnabledCommand(
            commands,
            new BattleActionMenuCommandViewModel(
                "move",
                "移动",
                movement?.ApCost,
                moveEnabled,
                moveEnabled ? "" : moveDisabledReason,
                "移"));

        foreach (AbilityDefinition ability in BattleAbilityQueries.GetAbilities(entity))
        {
            bool abilityEnabled = BattleAbilityQueries.CanUseAbility(entity, ability, out string disabledReason);
            AddEnabledCommand(
                commands,
                new BattleActionMenuCommandViewModel(
                    BattleAbilityQueries.ToCommandId(ability),
                    string.IsNullOrWhiteSpace(ability.DisplayName) ? "能力" : ability.DisplayName,
                    ability.ApCost,
                    abilityEnabled,
                    abilityEnabled ? "" : disabledReason,
                    string.IsNullOrWhiteSpace(ability.IconText) ? "技" : ability.IconText));
        }

        commands.Add(new("wait", "待机", IconText: "待"));
        commands.Add(new("end", "结束", IconText: "结"));

        return commands;
    }

    private static bool CanShowUnitCommands(BattleEntity entity, BattleFaction activeFaction)
    {
        if (entity == null || activeFaction != BattleFaction.Player || BattleRuleQueries.IsDefeated(entity))
        {
            return false;
        }

        BattleFaction faction = entity.GetComponent<FactionComponent>()?.Faction ?? BattleFaction.Neutral;
        SelectableComponent selectable = entity.GetComponent<SelectableComponent>();
        return faction == BattleFaction.Player &&
               selectable is not { IsSelectable: false };
    }

    private static void AddEnabledCommand(
        List<BattleActionMenuCommandViewModel> commands,
        BattleActionMenuCommandViewModel command)
    {
        if (command.IsEnabled)
        {
            commands.Add(command);
        }
    }

    private void OnCommandHovered(BattleActionMenuCommandViewModel command)
    {
        if (!command.IsEnabled && !string.IsNullOrWhiteSpace(command.DisabledReason))
        {
            _floatingActionHint.ShowHint(command.DisabledReason);
        }
    }

    private void OnCommandSelected(BattleActionMenuCommandViewModel command)
    {
        GameLog.Info(nameof(BattleHudRoot), $"Command selected id={command.Id} label={command.Label}");

        string hint = command.Id switch
        {
            "move" => "请选择移动目标",
            "attack" => "请选择攻击目标",
            "wait" => "正在待机",
            "end" => "结束行动",
            _ when BattleAbilityQueries.IsAbilityCommand(command.Id) => $"请选择{command.Label}目标",
            _ => command.Label
        };

        _floatingActionHint.ShowHint(hint);
        CommandSelected?.Invoke(command.Id);
    }

    private void OnInvalidCommandSelected(BattleActionMenuCommandViewModel command)
    {
        GameLog.Info(nameof(BattleHudRoot), $"Invalid command selected id={command.Id} label={command.Label} reason={command.DisabledReason}");
        _floatingActionHint.ShowHint(string.IsNullOrWhiteSpace(command.DisabledReason)
            ? "当前无法使用"
            : command.DisabledReason);
    }

    private static bool IsCancelEvent(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent)
        {
            return keyEvent.Pressed && !keyEvent.Echo && keyEvent.Keycode == Key.Escape;
        }

        return @event is InputEventMouseButton mouseButton &&
               mouseButton.Pressed &&
               mouseButton.ButtonIndex == MouseButton.Right;
    }
}
