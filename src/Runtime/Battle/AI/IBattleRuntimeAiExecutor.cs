namespace Rpg.Runtime.Battle.AI;

public interface IBattleRuntimeAiExecutor
{
    BattleRuntimeAiActionRequest ChooseAction(BattleRuntimeAiDecisionFacts facts);
}
