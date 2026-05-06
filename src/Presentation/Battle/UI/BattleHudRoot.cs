using System;
using System.Collections.Generic;
using Godot;
using Rpg.Definitions.Battle.Abilities;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Common;
using Rpg.Presentation.Battle.Abilities;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.UI.ActionWheel;

namespace Rpg.Presentation.Battle.UI;

public partial class BattleHudRoot : Control
{
    public event Action<string> CommandSelected;
    public event Action CommandCancelled;

    private TopTurnBar _topTurnBar;
    private UnitStatusCard _unitStatusCard;
    private BattleActionDock _actionDock;
    private CommandInfoPanel _commandInfoPanel;
    private FloatingActionHint _floatingActionHint;
    private Tween _selectionUiTween;
    private bool _selectionUiVisible;
    private Vector2 _unitStatusShownPosition;
    private Vector2 _actionDockShownPosition;
    private Vector2 _commandInfoShownPosition;

    private const float SelectionUiHiddenOffsetY = 132f;
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

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsCancelEvent(@event))
        {
            return;
        }

        bool hadActiveCommand = _actionDock?.HasActiveCommand == true;
        if (_actionDock?.HandleCancel() == true)
        {
            _floatingActionHint?.ShowHint("请选择行动");

            if (hadActiveCommand)
            {
                CommandCancelled?.Invoke();
            }

            GetViewport().SetInputAsHandled();
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
        _actionDock = GameUiSceneFactory.GetRequiredNode<BattleActionDock>(
            content,
            "BattleActionDock",
            nameof(BattleHudRoot));
        _commandInfoPanel = GameUiSceneFactory.GetRequiredNode<CommandInfoPanel>(
            content,
            "CommandInfoPanel",
            nameof(BattleHudRoot));
        _floatingActionHint = GameUiSceneFactory.GetRequiredNode<FloatingActionHint>(
            content,
            "FloatingActionHint",
            nameof(BattleHudRoot));

        if (_actionDock != null)
        {
            _actionDock.CommandHovered += OnCommandHovered;
            _actionDock.CommandSelected += OnCommandSelected;
            _actionDock.InvalidCommandSelected += OnInvalidCommandSelected;
            _actionDock.LayerChanged += OnLayerChanged;
        }

        LayoutHud();
    }

    private void LayoutHud()
    {
        if (_topTurnBar == null ||
            _commandInfoPanel == null ||
            _unitStatusCard == null ||
            _actionDock == null ||
            _floatingActionHint == null)
        {
            return;
        }

        float viewportWidth = Mathf.Max(Size.X, 1f);
        float viewportHeight = Mathf.Max(Size.Y, 1f);
        float sidePadding = Mathf.Clamp(viewportWidth * 0.018f, 18f, 34f);
        float bottomPadding = Mathf.Clamp(viewportHeight * 0.02f, 12f, 28f);
        float infoGap = Mathf.Clamp(viewportWidth * 0.008f, 8f, 16f);

        _topTurnBar.Size = new Vector2(Mathf.Clamp(viewportWidth * 0.34f, 460f, 680f), 40f);
        _topTurnBar.Size = new Vector2(Mathf.Min(_topTurnBar.Size.X, viewportWidth - sidePadding * 2f), _topTurnBar.Size.Y);
        _topTurnBar.Position = new Vector2((viewportWidth - _topTurnBar.Size.X) * 0.5f, 16f);

        float panelHeight = Mathf.Clamp(viewportHeight * 0.18f, 112f, 168f);
        float actionDockHeight = Mathf.Clamp(viewportHeight * 0.31f, 260f, 360f);
        float availableWidth = Mathf.Max(320f, viewportWidth - sidePadding * 2f);
        bool stackedLayout = viewportWidth < 1040f;

        if (stackedLayout)
        {
            float fullWidth = availableWidth;
            float commandY = viewportHeight - panelHeight - bottomPadding;
            float actionY = commandY - actionDockHeight - infoGap;
            float unitY = Mathf.Max(_topTurnBar.Position.Y + _topTurnBar.Size.Y + infoGap, actionY - panelHeight - infoGap);

            _commandInfoPanel.Size = new Vector2(fullWidth, panelHeight);
            _commandInfoPanel.Position = new Vector2(sidePadding, commandY);

            _unitStatusCard.Size = new Vector2(fullWidth, panelHeight);
            _unitStatusCard.Position = new Vector2(sidePadding, unitY);

            _actionDock.Size = new Vector2(fullWidth, actionDockHeight);
            _actionDock.Position = new Vector2(sidePadding, actionY);
        }
        else
        {
            float commandInfoWidth = Mathf.Clamp(viewportWidth * 0.23f, 300f, 440f);
            float commandX = viewportWidth - commandInfoWidth - sidePadding;
            float actionDockWidth = Mathf.Clamp(commandX - sidePadding - infoGap, 720f, 1040f);
            float actionDockX = sidePadding;
            float actionDockY = viewportHeight - actionDockHeight - bottomPadding;
            float panelY = viewportHeight - panelHeight - bottomPadding;

            _commandInfoPanel.Size = new Vector2(commandInfoWidth, panelHeight);
            _commandInfoPanel.Position = new Vector2(commandX, panelY);

            float actionDockRight = actionDockX + actionDockWidth;
            float unitInfoRight = _commandInfoPanel.Position.X - infoGap;
            bool compactBottomBand = unitInfoRight - actionDockRight < 368f || viewportWidth < 1500f;

            if (compactBottomBand)
            {
                float unitInfoWidth = Mathf.Clamp(actionDockWidth * 0.52f, 340f, 480f);
                float unitInfoX = Mathf.Max(sidePadding, actionDockRight - unitInfoWidth);
                float unitInfoY = Mathf.Max(_topTurnBar.Position.Y + _topTurnBar.Size.Y + infoGap, actionDockY - panelHeight - infoGap);

                _unitStatusCard.Size = new Vector2(unitInfoWidth, panelHeight);
                _unitStatusCard.Position = new Vector2(unitInfoX, unitInfoY);
            }
            else
            {
                float unitInfoLeft = actionDockRight + infoGap;
                float unitInfoWidth = Mathf.Max(360f, unitInfoRight - unitInfoLeft);

                _unitStatusCard.Size = new Vector2(unitInfoWidth, panelHeight);
                _unitStatusCard.Position = new Vector2(unitInfoRight - _unitStatusCard.Size.X, panelY);
            }

            _actionDock.Size = new Vector2(actionDockWidth, actionDockHeight);
            _actionDock.Position = new Vector2(actionDockX, actionDockY);
        }

        _unitStatusShownPosition = _unitStatusCard.Position;
        _actionDockShownPosition = _actionDock.Position;

        _floatingActionHint.Size = new Vector2(Mathf.Min(420f, viewportWidth - sidePadding * 2f), 52f);
        _floatingActionHint.Position = new Vector2((viewportWidth - _floatingActionHint.Size.X) * 0.5f, 86f);

        _commandInfoShownPosition = _commandInfoPanel.Position;

        ApplySelectionUiPose(_selectionUiVisible);
    }

    private void ConfigureDemoState()
    {
        _unitStatusCard?.SetUnit("未选择", 0, 0, 0, 0);
        _actionDock?.SetWheelViewModel(BuildDemoWheelViewModel());
        SetSelectionUiVisible(false, false);
    }

    public void ShowEntity(BattleEntity entity)
    {
        if (entity == null)
        {
            _unitStatusCard?.SetUnit("未选择", 0, 0, 0, 0);
            _actionDock?.SetWheelViewModel(BuildDemoWheelViewModel());
            SetSelectionUiVisible(false);
            GameLog.Info(nameof(BattleHudRoot), "HUD cleared selected entity.");
            return;
        }

        _actionDock?.SetWheelViewModel(BuildUnitWheelViewModel(entity));
        SetSelectionUiVisible(true);

        HealthComponent health = entity.GetComponent<HealthComponent>();
        ActionPointComponent actionPoint = entity.GetComponent<ActionPointComponent>();

        _unitStatusCard?.SetUnit(
            entity.DisplayName,
            health?.Hp ?? 0,
            health?.MaxHp ?? 0,
            actionPoint?.Ap ?? 0,
            actionPoint?.MaxAp ?? 0);

        _floatingActionHint.ShowHint($"已选择 {entity.DisplayName}");
        GameLog.Info(
            nameof(BattleHudRoot),
            $"HUD showing entity id={entity.EntityId} name={entity.DisplayName} hp={health?.Hp ?? 0}/{health?.MaxHp ?? 0} ap={actionPoint?.Ap ?? 0}/{actionPoint?.MaxAp ?? 0}");
    }

    public void ClearActiveCommand()
    {
        _actionDock?.SetActiveCommand("");
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
        TweenSelectionNode(_actionDock, _actionDockShownPosition, visible, duration);
        TweenSelectionNode(_unitStatusCard, _unitStatusShownPosition, visible, duration);
        TweenSelectionNode(_commandInfoPanel, _commandInfoShownPosition, visible, duration);

        _floatingActionHint.Visible = visible;
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
        ApplySelectionNodePose(_actionDock, _actionDockShownPosition, visible);
        ApplySelectionNodePose(_unitStatusCard, _unitStatusShownPosition, visible);
        ApplySelectionNodePose(_commandInfoPanel, _commandInfoShownPosition, visible);

        if (_floatingActionHint != null)
        {
            _floatingActionHint.Visible = visible;
        }
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
        if (_unitStatusCard != null)
        {
            _unitStatusCard.Visible = visible;
        }

        if (_actionDock != null)
        {
            _actionDock.Visible = visible;
        }

        if (_commandInfoPanel != null)
        {
            _commandInfoPanel.Visible = visible;
        }

        if (_floatingActionHint != null)
        {
            _floatingActionHint.Visible = visible;
        }
    }

    private static Vector2 GetHiddenPosition(Vector2 shownPosition)
    {
        return shownPosition + new Vector2(0f, SelectionUiHiddenOffsetY);
    }

    private static ActionWheelViewModel BuildDemoWheelViewModel()
    {
        var primaryCommands = new List<ActionWheelCommandViewModel>
        {
            new("move", "移动", 1, IconText: "移"),
            new("attack", "攻击", 1, IconText: "攻"),
            new("skill-menu", "技能", 2, IconText: "技", TargetLayerId: ActionWheelLayerIds.Skills),
            new("cards", "卡牌", 1, false, "卡牌指令尚未接入", "卡"),
            new("corps", "兵团", 2, false, "兵团指令尚未接入", "令"),
            new("wait", "待机", IconText: "待"),
            new("end", "结束", IconText: "终")
        };

        var skillCommands = new List<ActionWheelCommandViewModel>
        {
            new("skill_push", "推击", 2, IconText: "推"),
            new("skill_guard", "守护", 1, false, "行动点不足", "守"),
            new("skill_mark", "标记", 1, IconText: "标"),
            new("skill_back", "返回", IconText: "返", IsBackCommand: true)
        };

        var layers = new Dictionary<string, ActionWheelLayerViewModel>
        {
            [ActionWheelLayerIds.Primary] = new(ActionWheelLayerIds.Primary, "", primaryCommands),
            [ActionWheelLayerIds.Skills] = new(ActionWheelLayerIds.Skills, ActionWheelLayerIds.Primary, skillCommands)
        };

        return new ActionWheelViewModel(ActionWheelLayerIds.Primary, "", layers);
    }

    private static ActionWheelViewModel BuildUnitWheelViewModel(BattleEntity entity)
    {
        var primaryCommands = new List<ActionWheelCommandViewModel>();
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

        primaryCommands.Add(new ActionWheelCommandViewModel(
            "move",
            "移动",
            movement?.ApCost,
            moveEnabled,
            moveEnabled ? "" : moveDisabledReason,
            "移"));

        foreach (AbilityDefinition ability in BattleAbilityQueries.GetAbilities(entity))
        {
            bool abilityEnabled = BattleAbilityQueries.CanUseAbility(entity, ability, out string disabledReason);
            primaryCommands.Add(new ActionWheelCommandViewModel(
                BattleAbilityQueries.ToCommandId(ability),
                string.IsNullOrWhiteSpace(ability.DisplayName) ? "能力" : ability.DisplayName,
                ability.ApCost,
                abilityEnabled,
                abilityEnabled ? "" : disabledReason,
                string.IsNullOrWhiteSpace(ability.IconText) ? "技" : ability.IconText));
        }

        primaryCommands.Add(new("cards", "卡牌", 1, false, "卡牌指令尚未接入", "卡"));
        primaryCommands.Add(new("corps", "兵团", 2, false, "兵团指令尚未接入", "令"));
        primaryCommands.Add(new("wait", "待机", IconText: "待"));
        primaryCommands.Add(new("end", "结束", IconText: "结"));

        var layers = new Dictionary<string, ActionWheelLayerViewModel>
        {
            [ActionWheelLayerIds.Primary] = new(ActionWheelLayerIds.Primary, "", primaryCommands),
            [ActionWheelLayerIds.Skills] = new(ActionWheelLayerIds.Skills, ActionWheelLayerIds.Primary, new List<ActionWheelCommandViewModel>
            {
                new("skill_back", "返回", IconText: "返", IsBackCommand: true)
            })
        };

        return new ActionWheelViewModel(ActionWheelLayerIds.Primary, "", layers);
    }

    private void OnCommandHovered(ActionWheelCommandViewModel command)
    {
        _commandInfoPanel.ShowCommand(command, false);

        if (!command.IsEnabled && !string.IsNullOrWhiteSpace(command.DisabledReason))
        {
            _floatingActionHint.ShowHint(command.DisabledReason);
        }
    }

    private void OnCommandSelected(ActionWheelCommandViewModel command)
    {
        _commandInfoPanel.ShowCommand(command, true);
        GameLog.Info(nameof(BattleHudRoot), $"Command selected id={command.Id} label={command.Label}");

        string hint = command.Id switch
        {
            "move" => "请选择移动目标",
            "attack" => "请选择攻击目标",
            "wait" => "正在待机",
            "end" => "结束行动",
            _ when BattleAbilityQueries.IsAbilityCommand(command.Id) => $"请选择{command.Label}目标",
            _ when command.Id.StartsWith("skill_") => $"请选择{command.Label}目标",
            _ => command.Label
        };

        _floatingActionHint.ShowHint(hint);
        CommandSelected?.Invoke(command.Id);
    }

    private void OnInvalidCommandSelected(ActionWheelCommandViewModel command)
    {
        _commandInfoPanel.ShowCommand(command, false);
        GameLog.Info(nameof(BattleHudRoot), $"Invalid command selected id={command.Id} label={command.Label} reason={command.DisabledReason}");
        _floatingActionHint.ShowHint(string.IsNullOrWhiteSpace(command.DisabledReason)
            ? "当前无法使用"
            : command.DisabledReason);
    }

    private void OnLayerChanged(string layerId)
    {
        _commandInfoPanel.ShowLayer(layerId);
        GameLog.Info(nameof(BattleHudRoot), $"Action wheel layer changed layer={layerId}");
        _floatingActionHint.ShowHint(layerId == ActionWheelLayerIds.Skills
            ? "请选择技能"
            : "请选择行动");
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
