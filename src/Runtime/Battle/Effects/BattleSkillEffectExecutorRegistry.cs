using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle.Events;

namespace Rpg.Runtime.Battle.Effects;

internal sealed class BattleSkillEffectExecutorRegistry
{
    private readonly IReadOnlyList<IBattleSkillEffectExecutor> _executors;

    internal BattleSkillEffectExecutorRegistry(IEnumerable<IBattleSkillEffectExecutor> executors)
    {
        _executors = (executors ?? Enumerable.Empty<IBattleSkillEffectExecutor>())
            .Where(item => item != null)
            .ToArray();
    }

    internal static BattleSkillEffectExecutorRegistry CreateDefault()
    {
        return new BattleSkillEffectExecutorRegistry(new IBattleSkillEffectExecutor[]
        {
            new DamageSkillEffectExecutor(),
            new CreateMarkSkillEffectExecutor(),
            new TeleportToMarkSkillEffectExecutor(),
            new ChanneledAreaDamageSkillEffectExecutor()
        });
    }

    internal IReadOnlyList<BattleEvent> Execute(
        BattleEffectExecutionContext context,
        BattleSkillEffectSnapshot payload)
    {
        IBattleSkillEffectExecutor executor = _executors.FirstOrDefault(item => item.CanExecute(payload));
        if (executor == null)
        {
            throw new InvalidOperationException($"battle_skill_effect_executor_missing payload={payload?.GetType().Name ?? "null"}");
        }

        return executor.Execute(context, payload);
    }
}
