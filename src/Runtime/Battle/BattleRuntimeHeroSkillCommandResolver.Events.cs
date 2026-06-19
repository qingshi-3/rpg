using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle.Events;

namespace Rpg.Runtime.Battle;

internal static partial class BattleRuntimeHeroSkillCommandResolver
{
    private static void AddSkillUsed(
        BattleEventStream stream,
        string battleId,
        int runtimeTick,
        double runtimeTimeSeconds,
        BattleRuntimeActor hero,
        BattleRuntimeActor target,
        BattleSkillSnapshot skill,
        BattleRuntimePendingHeroSkillCommand command,
        string actionId)
    {
        stream.Add(new BattleEvent
        {
            EventId = $"{battleId}:tick_{runtimeTick}:{hero.ActorId}:skill:{target?.ActorId ?? "cell"}",
            BattleId = battleId ?? "",
            BattleGroupId = hero.BattleGroupId ?? "",
            ActorId = hero.ActorId ?? "",
            TargetId = target?.ActorId ?? "",
            SourceCommandId = command.CommandId ?? "",
            SourceActionId = actionId,
            SourceDefinitionId = skill.SkillId ?? "",
            Kind = BattleEventKind.SkillUsed,
            ReasonCode = skill.SkillId ?? "",
            ActionDurationSeconds = ResolveSkillUsedActionDurationSeconds(skill),
            RuntimeTick = runtimeTick,
            RuntimeTimeSeconds = runtimeTimeSeconds,
            HasActorCells = true,
            ActorGridX = hero.GridX,
            ActorGridY = hero.GridY,
            ActorGridHeight = hero.GridHeight,
            HasTargetCells = target != null || command.HasTargetGrid,
            TargetGridX = target?.GridX ?? command.TargetGridX,
            TargetGridY = target?.GridY ?? command.TargetGridY,
            TargetGridHeight = target?.GridHeight ?? command.TargetGridHeight
        });
    }

    private static double ResolveSkillUsedActionDurationSeconds(BattleSkillSnapshot skill)
    {
        if (skill?.Effects == null || skill.Effects.Count == 0)
        {
            return 0;
        }

        double durationSeconds = 0;
        foreach (BattleSkillEffectSnapshot effect in skill.Effects)
        {
            if (effect?.Kind == BattleSkillEffectKind.StartChanneledAreaDamage)
            {
                durationSeconds = System.Math.Max(durationSeconds, System.Math.Max(0, effect.DurationSeconds));
            }
        }

        return durationSeconds;
    }
}
