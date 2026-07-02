using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.Battle.Intents;

public sealed class BattleIntent
{
    public BattleIntent(
        BattleEntity actor,
        BattleIntentTemplate template,
        int power,
        string reason = "")
    {
        Actor = actor;
        Template = template ?? BattleIntentTemplates.Hold;
        Power = System.Math.Max(0, power);
        Reason = reason ?? "";
    }

    public BattleEntity Actor { get; }
    public BattleIntentTemplate Template { get; }
    public int Power { get; }
    public string Reason { get; }
    public string TemplateId => Template.Id;
    public string DisplayName => Template.DisplayName;
    public string IconKey => Template.IconKey;
    public string IconText => Template.IconText;
    public string ValueText => Template.ShowValue && Power > 0
        ? Power.ToString(System.Globalization.CultureInfo.InvariantCulture)
        : "";
    public string Summary => Actor == null
        ? DisplayName
        : $"{Actor.DisplayName}: {DisplayName}";

    public static BattleIntent Hold(BattleEntity actor, string reason)
    {
        return BattleIntentTemplates.Hold.Create(actor, 0, reason);
    }
}
