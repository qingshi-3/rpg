using System;
using System.Collections.Generic;
using Godot;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.Battle.UI;

public partial class BattleActionMenu : PanelContainer
{
    private readonly List<BattleActionMenuButton> _buttons = new();
    private IReadOnlyList<BattleActionMenuCommandViewModel> _commands = Array.Empty<BattleActionMenuCommandViewModel>();
    private GridContainer _commandList;
    private string _activeCommandId = "";

    public event Action<BattleActionMenuCommandViewModel> CommandHovered;
    public event Action<BattleActionMenuCommandViewModel> CommandSelected;
    public event Action<BattleActionMenuCommandViewModel> InvalidCommandSelected;

    public bool HasActiveCommand => !string.IsNullOrWhiteSpace(_activeCommandId);
    public int CommandCount => _commands.Count;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        _commandList = GameUiSceneFactory.GetRequiredNode<GridContainer>(this, "Margin/Root/CommandList", nameof(BattleActionMenu));
        RenderCommands();
    }

    public void SetCommands(IReadOnlyList<BattleActionMenuCommandViewModel> commands)
    {
        _commands = commands ?? Array.Empty<BattleActionMenuCommandViewModel>();
        RenderCommands();
    }

    public void SetActiveCommand(string commandId)
    {
        _activeCommandId = commandId ?? "";

        foreach (BattleActionMenuButton button in _buttons)
        {
            button.SetActive(button.Command?.Id == _activeCommandId);
        }
    }

    private void RenderCommands()
    {
        if (_commandList == null)
        {
            return;
        }

        ClearCommandButtons();

        _commandList.Columns = 1;

        foreach (BattleActionMenuCommandViewModel command in _commands)
        {
            if (command == null)
            {
                continue;
            }

            BattleActionMenuButton button = GameUiSceneFactory.CreateBattleActionMenuButton(nameof(BattleActionMenu));
            if (button == null)
            {
                GameLog.Warn(nameof(BattleActionMenu), $"Cannot create command button id={command.Id}");
                continue;
            }

            button.SetCommand(command);
            button.SetActive(command.Id == _activeCommandId);
            button.CommandHovered += hovered => CommandHovered?.Invoke(hovered);
            button.CommandSelected += OnCommandSelected;
            button.InvalidCommandSelected += invalid => InvalidCommandSelected?.Invoke(invalid);
            _commandList.AddChild(button);
            _buttons.Add(button);
        }
    }

    private void OnCommandSelected(BattleActionMenuCommandViewModel command)
    {
        SetActiveCommand(command.Id);
        CommandSelected?.Invoke(command);
    }

    private void ClearCommandButtons()
    {
        foreach (BattleActionMenuButton button in _buttons)
        {
            _commandList.RemoveChild(button);
            button.QueueFree();
        }

        _buttons.Clear();
    }
}
