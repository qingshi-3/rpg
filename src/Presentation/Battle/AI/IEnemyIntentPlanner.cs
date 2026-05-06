using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Intents;

namespace Rpg.Presentation.Battle.AI;

public interface IEnemyIntentPlanner
{
    BattleIntent ChooseIntent(BattleAiContext context, BattleEntity actor);
}
