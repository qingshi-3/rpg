using Rpg.Definitions.Battle.Abilities;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Intents;

namespace Rpg.Presentation.Battle.AI;

public sealed class BattleAiDecisionResult
{
    public BattleAiDecisionResult(BattleIntentTemplate template, int power, string reason = "")
    {
        Template = template ?? BattleIntentTemplates.Hold;
        Power = System.Math.Max(0, power);
        Reason = reason ?? "";
    }

    public BattleIntentTemplate Template { get; }
    public string TemplateId => Template.Id;
    public int Power { get; }
    public string Reason { get; }

    public BattleIntent ToIntent(BattleEntity actor, AbilityDefinition preferredAbility)
    {
        AbilityDefinition ability = Template == BattleIntentTemplates.Hold ? null : preferredAbility;
        return Template.Create(actor, ability, Power, Reason);
    }
}
