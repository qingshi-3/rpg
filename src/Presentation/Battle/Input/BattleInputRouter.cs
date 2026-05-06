using Godot;
using Rpg.Domain.Battle.Grid;
using Rpg.Infrastructure.Logging;

namespace Rpg.Presentation.Battle.InputSystem;

public delegate bool TryResolveBattleGridPosition(out GridPosition position);

public delegate bool BattleCommandRequestedHandler(BattleCommand command);

public partial class BattleInputRouter : Node
{
    private TryResolveBattleGridPosition _tryResolvePointerGridPosition;

    public event BattleCommandRequestedHandler CommandRequested;

    public void Initialize(TryResolveBattleGridPosition tryResolvePointerGridPosition)
    {
        _tryResolvePointerGridPosition = tryResolvePointerGridPosition;
        GameLog.Info(nameof(BattleInputRouter), $"Initialized pointerGridResolver={_tryResolvePointerGridPosition != null}");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            return;
        }

        if (_tryResolvePointerGridPosition == null ||
            !_tryResolvePointerGridPosition(out GridPosition position))
        {
            return;
        }

        if (RequestCommand(BattleCommand.GridCellClicked(position)))
        {
            GetViewport()?.SetInputAsHandled();
        }
    }

    private bool RequestCommand(BattleCommand command)
    {
        if (CommandRequested == null)
        {
            return false;
        }

        bool handled = false;
        foreach (BattleCommandRequestedHandler handler in CommandRequested.GetInvocationList())
        {
            handled |= handler(command);
        }

        return handled;
    }
}
