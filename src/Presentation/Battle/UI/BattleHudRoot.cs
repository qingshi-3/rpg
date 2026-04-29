using System.Collections.Generic;
using Godot;
using Rpg.Presentation.UI.ActionWheel;

namespace Rpg.Presentation.Battle.UI;

public partial class BattleHudRoot : Control
{
    private TopTurnBar _topTurnBar;
    private UnitStatusCard _unitStatusCard;
    private BattleActionDock _actionDock;
    private CommandInfoPanel _commandInfoPanel;
    private FloatingActionHint _floatingActionHint;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        BuildHud();
        ConfigureDemoState();
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
        if (IsCancelEvent(@event) && _actionDock.HandleCancel())
        {
            _floatingActionHint.ShowHint("请选择行动");
            GetViewport().SetInputAsHandled();
        }
    }

    private void BuildHud()
    {
        _topTurnBar = new TopTurnBar();
        _unitStatusCard = new UnitStatusCard();
        _actionDock = new BattleActionDock();
        _commandInfoPanel = new CommandInfoPanel();
        _floatingActionHint = new FloatingActionHint();

        AddChild(_topTurnBar);
        AddChild(_actionDock);
        AddChild(_unitStatusCard);
        AddChild(_commandInfoPanel);
        AddChild(_floatingActionHint);

        _actionDock.CommandHovered += OnCommandHovered;
        _actionDock.CommandSelected += OnCommandSelected;
        _actionDock.InvalidCommandSelected += OnInvalidCommandSelected;
        _actionDock.LayerChanged += OnLayerChanged;

        LayoutHud();
    }

    private void LayoutHud()
    {
        if (_topTurnBar == null)
        {
            return;
        }

        _topTurnBar.Size = new Vector2(Mathf.Min(620f, Size.X - 48f), 36f);
        _topTurnBar.Position = new Vector2((Size.X - _topTurnBar.Size.X) * 0.5f, 16f);

        float actionDockHeight = Mathf.Clamp(Size.Y * 0.33f, 300f, 360f);
        float panelHeight = Mathf.Max(120f, Size.Y * 0.2f);
        float commandInfoWidth = Mathf.Clamp(Size.X * 0.24f, 320f, 460f);
        float sidePadding = 24f;
        float infoGap = 10f;
        float actionDockWidth = Mathf.Clamp(Size.X * 0.52f, 820f, 1040f);
        float actionDockX = sidePadding;
        float actionDockY = Size.Y - actionDockHeight - 8f;
        float panelY = Size.Y - panelHeight - 24f;

        _commandInfoPanel.Size = new Vector2(commandInfoWidth, panelHeight);

        _commandInfoPanel.Position = new Vector2(
            Size.X - _commandInfoPanel.Size.X - sidePadding,
            panelY);

        float actionDockRight = actionDockX + actionDockWidth;
        float unitInfoRight = _commandInfoPanel.Position.X - infoGap;
        float topEdgeReach = Mathf.Clamp(Size.X * 0.08f, 120f, 190f);
        float unitInfoLeft = actionDockRight - topEdgeReach;
        float unitInfoWidth = Mathf.Max(360f, unitInfoRight - unitInfoLeft);

        _unitStatusCard.Size = new Vector2(unitInfoWidth, panelHeight);
        _unitStatusCard.Position = new Vector2(unitInfoRight - _unitStatusCard.Size.X, panelY);

        _actionDock.Size = new Vector2(actionDockWidth, actionDockHeight);
        _actionDock.Position = new Vector2(actionDockX, actionDockY);

        _floatingActionHint.Size = new Vector2(360f, 48f);
        _floatingActionHint.Position = new Vector2((Size.X - _floatingActionHint.Size.X) * 0.5f, 86f);
    }

    private void ConfigureDemoState()
    {
        _unitStatusCard.SetUnit("骑士", 24, 24, 3, 3);
        _actionDock.SetWheelViewModel(BuildDemoWheelViewModel());
    }

    private static ActionWheelViewModel BuildDemoWheelViewModel()
    {
        var primaryCommands = new List<ActionWheelCommandViewModel>
        {
            new("move", "移动", 1, IconText: "↗"),
            new("attack", "攻击", 1, IconText: "⚔"),
            new("skill-menu", "技能", 2, IconText: "✦", TargetLayerId: ActionWheelLayerIds.Skills),
            new("cards", "卡牌", 1, false, "卡牌指令尚未接入", "卡"),
            new("corps", "兵团", 2, false, "兵团指令尚未接入", "令"),
            new("wait", "待机", IconText: "…"),
            new("end", "结束", IconText: "✓")
        };

        var skillCommands = new List<ActionWheelCommandViewModel>
        {
            new("skill_push", "推击", 2, IconText: "推"),
            new("skill_guard", "守护", 1, false, "行动点不足", "盾"),
            new("skill_mark", "标记", 1, IconText: "标"),
            new("skill_back", "返回", IconText: "↩", IsBackCommand: true)
        };

        var layers = new Dictionary<string, ActionWheelLayerViewModel>
        {
            [ActionWheelLayerIds.Primary] = new(ActionWheelLayerIds.Primary, "", primaryCommands),
            [ActionWheelLayerIds.Skills] = new(ActionWheelLayerIds.Skills, ActionWheelLayerIds.Primary, skillCommands)
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

        string hint = command.Id switch
        {
            "move" => "请选择移动目标",
            "attack" => "请选择攻击目标",
            "wait" => "正在待机",
            "end" => "结束行动",
            _ when command.Id.StartsWith("skill_") => $"请选择{command.Label}目标",
            _ => command.Label
        };

        _floatingActionHint.ShowHint(hint);
    }

    private void OnInvalidCommandSelected(ActionWheelCommandViewModel command)
    {
        _commandInfoPanel.ShowCommand(command, false);
        _floatingActionHint.ShowHint(string.IsNullOrWhiteSpace(command.DisabledReason)
            ? "当前无法使用"
            : command.DisabledReason);
    }

    private void OnLayerChanged(string layerId)
    {
        _commandInfoPanel.ShowLayer(layerId);
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
