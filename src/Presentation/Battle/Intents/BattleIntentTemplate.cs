using Rpg.Definitions.Battle.Abilities;
using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.Battle.Intents;

public sealed class BattleIntentTemplate
{
    public BattleIntentTemplate(
        string id,
        BattleIntentType type,
        BattleIntentTargetPolicy targetPolicy,
        string displayName,
        string iconKey,
        bool showValue)
    {
        Id = string.IsNullOrWhiteSpace(id) ? "intent" : id;
        Type = type;
        TargetPolicy = targetPolicy;
        DisplayName = displayName ?? "";
        IconKey = BattleIntentIcons.Normalize(Id, iconKey);
        ShowValue = showValue;
    }

    public string Id { get; }
    public BattleIntentType Type { get; }
    public BattleIntentTargetPolicy TargetPolicy { get; }
    public string DisplayName { get; }
    public string IconKey { get; }
    public string IconText => IconKey;
    public bool ShowValue { get; }

    public BattleIntent Create(
        BattleEntity actor,
        AbilityDefinition preferredAbility,
        int power,
        string reason = "")
    {
        return new BattleIntent(actor, this, preferredAbility, power, reason);
    }
}

public static class BattleIntentTemplates
{
    public static readonly BattleIntentTemplate MeleePressure = new(
        "melee_pressure",
        BattleIntentType.Pressure,
        BattleIntentTargetPolicy.NearestHostile,
        "压迫最近目标",
        "压",
        true);

    public static readonly BattleIntentTemplate DirectStrike = new(
        "direct_strike",
        BattleIntentType.Strike,
        BattleIntentTargetPolicy.NearestHostile,
        "攻击最近目标",
        "攻",
        true);

    public static readonly BattleIntentTemplate RangedPressure = new(
        "ranged_pressure",
        BattleIntentType.Snipe,
        BattleIntentTargetPolicy.NearestHostile,
        "远程压制",
        "射",
        true);

    public static readonly BattleIntentTemplate Hold = new(
        "hold",
        BattleIntentType.Hold,
        BattleIntentTargetPolicy.None,
        "暂不行动",
        "待",
        false);
}
