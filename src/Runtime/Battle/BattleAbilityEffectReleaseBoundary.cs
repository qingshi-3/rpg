using System;
using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle.Effects;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle;

internal static class BattleAbilityEffectReleaseBoundary
{
    private static readonly BattleSkillEffectExecutorRegistry DefaultEffectExecutorRegistry =
        BattleSkillEffectExecutorRegistry.CreateDefault();

    internal static void ReleaseSkillEffects(
        BattleRuntimeState state,
        BattleEventStream stream,
        string battleId,
        int runtimeTick,
        double runtimeTimeSeconds,
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleSkillSnapshot skill,
        string sourceCommandId,
        string sourceActionId,
        BattleNavigationGraph navigationGraph,
        BattleRuntimePendingHeroSkillCommand command = null,
        BattleCommitBuffer channelStartCommitBuffer = null)
    {
        if (state == null || stream == null || actor == null || skill == null)
        {
            return;
        }

        // Ability controllers own cast timing; this boundary owns effect release
        // construction so primitive execution does not leak back into lifecycle code.
        BattleCommitBuffer effectCommitBuffer = channelStartCommitBuffer ?? new BattleCommitBuffer();
        foreach (BattleSkillEffectSnapshot effect in skill.Effects ?? Enumerable.Empty<BattleSkillEffectSnapshot>())
        {
            if (effect == null)
            {
                continue;
            }

            bool deferChannelStartDamage =
                effect is ChanneledAreaDamageSkillEffectSnapshot &&
                channelStartCommitBuffer != null;
            BattleCommitBuffer commitBuffer = deferChannelStartDamage
                ? channelStartCommitBuffer
                : effectCommitBuffer;
            foreach (BattleEvent effectEvent in DefaultEffectExecutorRegistry.Execute(
                         new BattleEffectExecutionContext
                         {
                             BattleId = battleId ?? "",
                             RuntimeTick = runtimeTick,
                             RuntimeTimeSeconds = runtimeTimeSeconds,
                             SourceCommandId = sourceCommandId ?? "",
                             SourceActionId = sourceActionId ?? "",
                             SourceDefinitionId = ResolveSkillDefinitionId(skill),
                             PresentationProfileId = skill.Presentation?.ProfileId ?? "",
                             CastFxProfileId = skill.Presentation?.CastFxProfileId ?? "",
                             ImpactFxProfileId = skill.Presentation?.ImpactFxProfileId ?? "",
                             MarkFxProfileId = skill.Presentation?.MarkFxProfileId ?? "",
                             AreaFxProfileId = skill.Presentation?.AreaFxProfileId ?? "",
                             SuppressActorCastFx = skill.Presentation?.SuppressActorCastFx ?? false,
                             HoldCastAnimationDuringAction = skill.Presentation?.HoldCastAnimationDuringAction ?? false,
                             CommitBuffer = commitBuffer,
                             DeferEffectDamageCommit = deferChannelStartDamage,
                             State = state,
                             NavigationGraph = navigationGraph,
                             Actor = actor,
                             Target = target,
                             HasTargetGrid = command?.HasTargetGrid ?? false,
                             TargetGridX = command?.TargetGridX ?? 0,
                             TargetGridY = command?.TargetGridY ?? 0,
                             TargetGridHeight = command?.TargetGridHeight ?? 0,
                             SelectedSpatialMarkId = command?.SelectedSpatialMarkId ?? ""
                         },
                         effect))
            {
                stream.Add(effectEvent);
            }
        }

        state.SkillAvailability.MarkReleased(actor.BattleGroupId, skill);
    }

    private static string ResolveSkillDefinitionId(BattleSkillSnapshot skill)
    {
        return skill?.SkillDefinitionId?.Trim() ?? "";
    }

}
