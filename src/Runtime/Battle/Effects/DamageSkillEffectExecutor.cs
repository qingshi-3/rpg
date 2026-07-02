using System.Collections.Generic;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle.Events;

namespace Rpg.Runtime.Battle.Effects;

internal sealed class DamageSkillEffectExecutor : IBattleSkillEffectExecutor
{
    public bool CanExecute(BattleSkillEffectSnapshot payload) => payload is DamageSkillEffectSnapshot;

    public IReadOnlyList<BattleEvent> Execute(BattleEffectExecutionContext context, BattleSkillEffectSnapshot payload)
    {
        DamageSkillEffectSnapshot damage = (DamageSkillEffectSnapshot)payload;
        return BattleEffectResolver.ApplyDamage(context, damage);
    }
}
