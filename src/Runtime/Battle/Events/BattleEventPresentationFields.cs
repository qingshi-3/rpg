using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle.Effects;

namespace Rpg.Runtime.Battle.Events;

internal static class BattleEventPresentationFields
{
    internal static void CopyFromSkill(BattleEvent runtimeEvent, BattleSkillSnapshot skill)
    {
        CopyFromPresentation(runtimeEvent, skill?.Presentation);
    }

    internal static void CopyFromContext(BattleEvent runtimeEvent, BattleEffectExecutionContext context)
    {
        if (runtimeEvent == null || context == null)
        {
            return;
        }

        runtimeEvent.PresentationProfileId = context.PresentationProfileId ?? "";
        runtimeEvent.CastFxProfileId = context.CastFxProfileId ?? "";
        runtimeEvent.ImpactFxProfileId = context.ImpactFxProfileId ?? "";
        runtimeEvent.MarkFxProfileId = context.MarkFxProfileId ?? "";
        runtimeEvent.AreaFxProfileId = context.AreaFxProfileId ?? "";
        runtimeEvent.SuppressActorCastFx = context.SuppressActorCastFx;
        runtimeEvent.HoldCastAnimationDuringAction = context.HoldCastAnimationDuringAction;
    }

    private static void CopyFromPresentation(
        BattleEvent runtimeEvent,
        BattleSkillPresentationSnapshot presentation)
    {
        if (runtimeEvent == null)
        {
            return;
        }

        runtimeEvent.PresentationProfileId = presentation?.ProfileId ?? "";
        runtimeEvent.CastFxProfileId = presentation?.CastFxProfileId ?? "";
        runtimeEvent.ImpactFxProfileId = presentation?.ImpactFxProfileId ?? "";
        runtimeEvent.MarkFxProfileId = presentation?.MarkFxProfileId ?? "";
        runtimeEvent.AreaFxProfileId = presentation?.AreaFxProfileId ?? "";
        runtimeEvent.SuppressActorCastFx = presentation?.SuppressActorCastFx ?? false;
        runtimeEvent.HoldCastAnimationDuringAction = presentation?.HoldCastAnimationDuringAction ?? false;
    }
}
