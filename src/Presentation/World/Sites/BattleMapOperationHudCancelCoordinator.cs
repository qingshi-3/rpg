using System;
using Godot;

namespace Rpg.Presentation.World.Sites;

internal static class BattleMapOperationHudCancelCoordinator
{
    internal static bool TryHandle(
        InputEvent inputEvent,
        BattleMapOperationHudSuppressionReason activeReason,
        string cancelAction,
        Action cancelRuntimeSkillTarget,
        Action cancelPreparationPlacement,
        Action cancelPreparationDestinationBeacon,
        Action cancelRuntimeDestinationBeacon,
        Action markInputHandled)
    {
        if (activeReason == BattleMapOperationHudSuppressionReason.None ||
            !inputEvent.IsActionPressed(cancelAction))
        {
            return false;
        }

        switch (activeReason)
        {
            case BattleMapOperationHudSuppressionReason.RuntimeSkillTarget:
                cancelRuntimeSkillTarget?.Invoke();
                break;
            case BattleMapOperationHudSuppressionReason.PreparationPlacement:
                cancelPreparationPlacement?.Invoke();
                break;
            case BattleMapOperationHudSuppressionReason.PreparationDestinationBeacon:
                cancelPreparationDestinationBeacon?.Invoke();
                break;
            case BattleMapOperationHudSuppressionReason.RuntimeDestinationBeacon:
                cancelRuntimeDestinationBeacon?.Invoke();
                break;
        }

        markInputHandled?.Invoke();
        return true;
    }
}
