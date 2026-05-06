using Rpg.Definitions.Battle.Abilities;
using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.Battle.Intents;

public sealed class BattleIntent
{
    public BattleIntent(
        BattleEntity actor,
        BattleIntentTemplate template,
        AbilityDefinition preferredAbility,
        int power,
        string reason = "")
    {
        Actor = actor;
        Template = template ?? BattleIntentTemplates.Hold;
        PreferredAbility = preferredAbility;
        Power = System.Math.Max(0, power);
        Reason = reason ?? "";
    }

    public BattleEntity Actor { get; }
    public BattleIntentTemplate Template { get; }
    public AbilityDefinition PreferredAbility { get; }
    public int Power { get; }
    public string Reason { get; }
    public string TemplateId => Template.Id;
    public BattleIntentType Type => Template.Type;
    public BattleIntentTargetPolicy TargetPolicy => Template.TargetPolicy;
    public string DisplayName => Template.DisplayName;
    public string IconKey => Template.IconKey;
    public string IconText => Template.IconText;
    public string ValueText => Template.ShowValue && Power > 0
        ? Power.ToString(System.Globalization.CultureInfo.InvariantCulture)
        : "";
    public string Summary => Actor == null
        ? DisplayName
        : $"{Actor.DisplayName}：{DisplayName}";
    public bool CanResolveAction => Actor != null &&
                                    Type != BattleIntentType.None &&
                                    Type != BattleIntentType.Hold;

    public static BattleIntent Hold(BattleEntity actor, string reason)
    {
        return BattleIntentTemplates.Hold.Create(actor, null, 0, reason);
    }
}
