using System.Collections.Generic;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle.Events;

namespace Rpg.Runtime.Battle.Effects;

internal sealed class TeleportToMarkSkillEffectExecutor : IBattleSkillEffectExecutor
{
    public bool CanExecute(BattleSkillEffectSnapshot payload) => payload is TeleportToMarkSkillEffectSnapshot;

    public IReadOnlyList<BattleEvent> Execute(BattleEffectExecutionContext context, BattleSkillEffectSnapshot payload)
    {
        TeleportToMarkSkillEffectSnapshot teleport = (TeleportToMarkSkillEffectSnapshot)payload;
        return BattleDisplacementCommitBoundary.CommitMarkTeleport(context, teleport);
    }
}
