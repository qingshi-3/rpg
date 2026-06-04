using Rpg.Runtime.Battle.AI.BehaviorTree;

namespace Rpg.Runtime.Battle.AI;

public sealed class DefaultBattleRuntimeAiExecutor : IBattleRuntimeAiExecutor
{
    private readonly IBattleRuntimeAiExecutor _executor = BattleRuntimeBehaviorTreeExecutor.CreateDefault();

    public BattleRuntimeAiActionRequest ChooseAction(BattleRuntimeAiDecisionFacts facts)
    {
        return _executor.ChooseAction(facts);
    }
}
