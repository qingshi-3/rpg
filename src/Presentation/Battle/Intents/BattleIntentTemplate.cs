using Rpg.Definitions.Battle.Abilities;
using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.Battle.Intents;

public sealed class BattleIntentTemplate
{
    public BattleIntentTemplate(
        string id,
        string displayName,
        string iconKey,
        bool showValue)
    {
        Id = string.IsNullOrWhiteSpace(id) ? "intent" : id;
        DisplayName = displayName ?? "";
        IconKey = BattleIntentIcons.Normalize(Id, iconKey);
        ShowValue = showValue;
    }

    public string Id { get; }
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
        "压迫最近目标",
        "压",
        true);

    public static readonly BattleIntentTemplate DirectStrike = new(
        "direct_strike",
        "攻击最近目标",
        "攻",
        true);

    public static readonly BattleIntentTemplate RangedPressure = new(
        "ranged_pressure",
        "远程压制",
        "狙",
        true);

    public static readonly BattleIntentTemplate FocusPressure = new(
        "focus_pressure",
        "推进集火",
        "集",
        true);

    public static readonly BattleIntentTemplate FocusStrike = new(
        "focus_strike",
        "集火打击",
        "集",
        true);

    public static readonly BattleIntentTemplate Hold = new(
        "hold",
        "暂不行动",
        "待",
        false);
}
