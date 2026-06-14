using System;
using System.Collections.Generic;
using Rpg.Domain.Battle.Grid;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle.Entities;
using Rpg.Runtime.Battle.Events;

namespace Rpg.Presentation.World.Sites;

internal static class BattleRuntimeTeleportPresentationObserver
{
    internal static double ObserveRuntimeTeleportEvent(
        BattleEvent runtimeEvent,
        IReadOnlyDictionary<string, BattleEntity> entitiesByRuntimeActor,
        BattleUnitRoot unitRoot,
        Action queueOverlayRefresh)
    {
        if (unitRoot == null ||
            runtimeEvent == null ||
            entitiesByRuntimeActor == null ||
            !entitiesByRuntimeActor.TryGetValue(runtimeEvent.ActorId ?? "", out BattleEntity actor) ||
            !runtimeEvent.HasMovementCells)
        {
            return 0;
        }

        GridOccupantComponent gridOccupant = actor.GetComponent<GridOccupantComponent>();
        GridSurfacePosition previousSurface = gridOccupant?.SurfacePosition ?? default;
        GridSurfacePosition destinationSurface = new(runtimeEvent.ToGridX, runtimeEvent.ToGridY, runtimeEvent.ToGridHeight);
        GameLog.Info(
            nameof(WorldSiteRoot),
            $"BattleRuntimeTeleportPresentation actor={runtimeEvent.ActorId ?? ""} fromSurface={previousSurface} toSurface={destinationSurface} activeMovementTweens={unitRoot.ActiveMovementTweenCount}");

        // Teleport is not a movement lane: Runtime already committed the final
        // anchor, so presentation must drop any queued interpolation first.
        unitRoot.SnapEntityToSurface(actor, destinationSurface);
        queueOverlayRefresh?.Invoke();
        return 0;
    }
}
