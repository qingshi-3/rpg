using System.Collections.Generic;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle.Entities;
using Rpg.Runtime.Battle.Events;

namespace Rpg.Presentation.World.Sites;

internal static class BattleRuntimeSkillProfilePresentationObserver
{
    private const string MarkProjectileProfileId = "skill_mark_projectile";
    private const string ChanneledAreaProfileId = "skill_channeled_area";

    internal static bool IsOffhandSkillReleaseEvent(BattleEvent runtimeEvent)
    {
        return string.Equals(
            runtimeEvent?.PresentationProfileId ?? "",
            MarkProjectileProfileId,
            System.StringComparison.Ordinal);
    }

    internal static bool IsChanneledAreaSkillUsedEvent(BattleEvent runtimeEvent)
    {
        return runtimeEvent?.HoldCastAnimationDuringAction == true ||
               string.Equals(
                   runtimeEvent?.PresentationProfileId ?? "",
                   ChanneledAreaProfileId,
                   System.StringComparison.Ordinal);
    }

    internal static double ObserveRuntimeMarkCreatedEvent(
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
        return unitRoot.PlayMarkProjectilePresentation(
            actor,
            target,
            new GridSurfacePosition(runtimeEvent.TargetGridX, runtimeEvent.TargetGridY, runtimeEvent.TargetGridHeight),
            runtimeEvent.BattleGroupId,
            attachedToTarget);
    }

    internal static double ObserveRuntimeChanneledAreaSkillUsedEvent(
        BattleEvent runtimeEvent,
        BattleEntity actor,
        BattleUnitRoot unitRoot)
    {
        if (unitRoot == null ||
            actor == null ||
            runtimeEvent == null ||
            !runtimeEvent.HasTargetCells)
        {
            return 0;
        }

        return unitRoot.PlayChanneledAreaPresentation(
            actor,
            new GridSurfacePosition(runtimeEvent.TargetGridX, runtimeEvent.TargetGridY, runtimeEvent.TargetGridHeight),
            runtimeEvent.ActionDurationSeconds);
    }
}
