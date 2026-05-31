using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle;

internal sealed class BattleRuntimeTickContext
{
    public BattleRuntimeActionProposal Proposal { get; set; }
    public BattleRuntimeAiActionRequest Request { get; set; }
    public BattleRuntimeTickStartActorFact ActorFact { get; init; }
    public BattleRuntimeTickStartActorFact? TargetFact { get; set; }
    public LocalCombatSituation LocalCombatSituation { get; init; }
    public BattleRuntimeAiActionResult Result { get; set; }
}
