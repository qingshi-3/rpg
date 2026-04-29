using System;
using Godot;
using Rpg.Presentation.UI.ActionWheel;

namespace Rpg.Presentation.Battle.UI;

public partial class BattleActionDock : Control
{
    private readonly ActionWheel _actionWheel = new();

    public event Action<ActionWheelCommandViewModel> CommandHovered;
    public event Action<ActionWheelCommandViewModel> CommandSelected;
    public event Action<ActionWheelCommandViewModel> InvalidCommandSelected;
    public event Action<string> LayerChanged;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        CustomMinimumSize = new Vector2(900, 330);

        AddChild(_actionWheel);

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
        _actionWheel.SetViewModel(viewModel);
    }

    public bool HandleCancel()
    {
        return _actionWheel.HandleCancel();
    }

    public void SetActiveCommand(string commandId)
    {
        _actionWheel.SetActiveCommand(commandId);
    }

    private void LayoutChildren()
    {
        float wheelWidth = Mathf.Min(Mathf.Max(Size.X, 720f), 1290f);

        _actionWheel.Size = new Vector2(wheelWidth, Size.Y);
        _actionWheel.Position = Vector2.Zero;
    }
}
