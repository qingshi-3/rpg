using System.Collections.Generic;
using Rpg.Application.Battle.Commands;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle.Entities;
using Rpg.Runtime.Battle.Events;

namespace Rpg.Presentation.World.Sites;

internal static class BattleRuntimeThunderTagPresentationObserver
{
    internal static bool IsOffhandSkillReleaseEvent(BattleEvent runtimeEvent)
    {
        return string.Equals(
            runtimeEvent?.SourceDefinitionId ?? "",
            HeroSkillCommandIds.ThunderTagThrowSkillId,
            System.StringComparison.Ordinal);
    }

    internal static double ObserveRuntimeThunderMarkCreatedEvent(
        BattleEvent runtimeEvent,
        IReadOnlyDictionary<string, BattleEntity> entitiesByRuntimeActor,
        BattleUnitRoot unitRoot)
    {
        if (unitRoot == null ||
            runtimeEvent == null ||
            entitiesByRuntimeActor == null ||
            !entitiesByRuntimeActor.TryGetValue(runtimeEvent.ActorId ?? "", out BattleEntity actor) ||
            !runtimeEvent.HasTargetCells)
        {
            return 0;
        }

        entitiesByRuntimeActor.TryGetValue(runtimeEvent.TargetId ?? "", out BattleEntity target);
        bool attachedToTarget = target != null &&
                                !string.IsNullOrWhiteSpace(runtimeEvent.TargetId);
        return unitRoot.PlayThunderTagPresentation(
            actor,
            target,
            new GridSurfacePosition(runtimeEvent.TargetGridX, runtimeEvent.TargetGridY, runtimeEvent.TargetGridHeight),
            runtimeEvent.BattleGroupId,
            attachedToTarget);
    }
}
