using System.Collections.Generic;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle.Events;

namespace Rpg.Runtime.Battle.Effects;

internal sealed class CreateMarkSkillEffectExecutor : IBattleSkillEffectExecutor
{
    public bool CanExecute(BattleSkillEffectSnapshot payload) => payload is CreateMarkSkillEffectSnapshot;

    public IReadOnlyList<BattleEvent> Execute(BattleEffectExecutionContext context, BattleSkillEffectSnapshot payload)
    {
        return BattleEffectResolver.ApplyCreateMark(context, (CreateMarkSkillEffectSnapshot)payload);
    }
}
