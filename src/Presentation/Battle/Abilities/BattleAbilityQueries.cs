using System.Collections.Generic;
using System.Linq;
using Rpg.Definitions.Battle.Abilities;
using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.Battle.Abilities;

public static class BattleAbilityQueries
{
    public const string CommandPrefix = "ability:";
    public const string BasicAttackId = "basic_attack";

    public static IReadOnlyList<AbilityDefinition> GetAbilities(BattleEntity entity)
    {
        if (entity == null)
        {
            return System.Array.Empty<AbilityDefinition>();
        }

        AbilityDefinition[] configuredAbilities = entity.GetComponent<AbilityComponent>()?.Abilities?
            .Where(ability => ability != null)
            .ToArray() ?? System.Array.Empty<AbilityDefinition>();

        if (configuredAbilities.Length > 0)
        {
            return configuredAbilities;
        }

        AttackComponent attack = entity.GetComponent<AttackComponent>();
        return attack == null
            ? System.Array.Empty<AbilityDefinition>()
            : new[] { CreateBasicAttackAbility(attack) };
    }

    public static AbilityDefinition GetPrimaryAbility(BattleEntity entity)
    {
        return GetAbilities(entity).FirstOrDefault();
    }

    public static string ToCommandId(AbilityDefinition ability)
    {
        return CommandPrefix + NormalizeAbilityId(ability);
    }

    public static bool IsAbilityCommand(string commandId)
    {
        return !string.IsNullOrWhiteSpace(commandId) &&
               commandId.StartsWith(CommandPrefix, System.StringComparison.Ordinal);
    }

    public static bool TryGetAbilityByCommandId(
        BattleEntity entity,
        string commandId,
        out AbilityDefinition ability)
    {
        ability = null;

        if (entity == null)
        {
            return false;
        }

        string abilityId = commandId == "attack"
            ? BasicAttackId
            : IsAbilityCommand(commandId)
                ? commandId[CommandPrefix.Length..]
                : "";

        if (string.IsNullOrWhiteSpace(abilityId))
        {
            return false;
        }

        ability = GetAbilities(entity)
            .FirstOrDefault(candidate => string.Equals(
                NormalizeAbilityId(candidate),
                abilityId,
                System.StringComparison.Ordinal));

        return ability != null;
    }

    private static AbilityDefinition CreateBasicAttackAbility(AttackComponent attack)
    {
        var ability = new AbilityDefinition
        {
            Id = BasicAttackId,
            DisplayName = "攻击",
            IconText = "攻",
            Range = attack.Range
        };

        ability.TargetRules.Add(new SingleHostileUnitTargetRule());
        ability.Effects.Add(new DamageAbilityEffect { Damage = attack.Damage });
        return ability;
    }

    private static string NormalizeAbilityId(AbilityDefinition ability)
    {
        return string.IsNullOrWhiteSpace(ability?.Id)
            ? BasicAttackId
            : ability.Id.Trim();
    }
}
