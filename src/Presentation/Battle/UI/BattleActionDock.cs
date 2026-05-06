using System;
using Godot;
using Rpg.Presentation.Common;
using Rpg.Presentation.UI.ActionWheel;

namespace Rpg.Presentation.Battle.UI;

public partial class BattleActionDock : Control
{
    private static readonly Vector2 DesignWheelSize = new(1290f, 345f);

    private ActionWheel _actionWheel;

    public event Action<ActionWheelCommandViewModel> CommandHovered;
    public event Action<ActionWheelCommandViewModel> CommandSelected;
    public event Action<ActionWheelCommandViewModel> InvalidCommandSelected;
    public event Action<string> LayerChanged;

    public bool HasActiveCommand => _actionWheel?.HasActiveCommand == true;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;

        _actionWheel = GameUiSceneFactory.GetRequiredNode<ActionWheel>(
            this,
            "ActionWheel",
            nameof(BattleActionDock));
        if (_actionWheel == null)
        {
            return;
        }

        _actionWheel.CommandHovered += command => CommandHovered?.Invoke(command);
        _actionWheel.CommandSelected += command => CommandSelected?.Invoke(command);
        _actionWheel.InvalidCommandSelected += command => InvalidCommandSelected?.Invoke(command);
        _actionWheel.LayerChanged += layerId => LayerChanged?.Invoke(layerId);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            LayoutChildren();
        }
    }

    public void SetWheelViewModel(ActionWheelViewModel viewModel)
    {
        _actionWheel?.SetViewModel(viewModel);
    }

    public bool HandleCancel()
    {
        return _actionWheel?.HandleCancel() == true;
    }

    public void SetActiveCommand(string commandId)
    {
        _actionWheel?.SetActiveCommand(commandId);
    }

    private void LayoutChildren()
    {
        if (_actionWheel == null)
        {
            return;
        }

        float scale = Mathf.Min(
            Size.X / DesignWheelSize.X,
            Size.Y / DesignWheelSize.Y);
        scale = Mathf.Clamp(scale, 0.56f, 1f);

        _actionWheel.Size = DesignWheelSize;
        _actionWheel.Scale = new Vector2(scale, scale);
        _actionWheel.Position = new Vector2(0f, Size.Y - DesignWheelSize.Y * scale);
    }
}
