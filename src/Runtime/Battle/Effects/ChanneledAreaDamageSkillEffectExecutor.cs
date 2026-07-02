using System.Collections.Generic;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle.Events;

namespace Rpg.Runtime.Battle.Effects;

internal sealed class ChanneledAreaDamageSkillEffectExecutor : IBattleSkillEffectExecutor
{
    public bool CanExecute(BattleSkillEffectSnapshot payload) => payload is ChanneledAreaDamageSkillEffectSnapshot;

    public IReadOnlyList<BattleEvent> Execute(BattleEffectExecutionContext context, BattleSkillEffectSnapshot payload)
    {
        ChanneledAreaDamageSkillEffectSnapshot channel = (ChanneledAreaDamageSkillEffectSnapshot)payload;
        return BattleEffectResolver.BeginChanneledArea(context, channel);
    }
}
