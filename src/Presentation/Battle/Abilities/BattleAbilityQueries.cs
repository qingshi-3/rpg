using System.Collections.Generic;
using System.Linq;
using Rpg.Definitions.Battle.Abilities;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Rules;

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

    public static bool CanUseAbility(BattleEntity actor, AbilityDefinition ability, out string reason)
    {
        reason = "";

        if (actor == null || ability == null)
        {
            reason = "能力数据不完整";
            return false;
        }

        if (BattleRuleQueries.IsDefeated(actor))
        {
            reason = "倒下的单位不能行动";
            return false;
        }

        return true;
    }

    public static bool IsValidTarget(
        BattleGridMap gridMap,
        IReadOnlyList<BattleEntity> entities,
        BattleEntity actor,
        BattleEntity target,
        GridPosition destination,
        AbilityDefinition ability,
        System.Action<BattleEntity> markEntityDefeated,
        out string reason)
    {
        reason = "";

        if (!CanUseAbility(actor, ability, out reason))
        {
            return false;
        }

        GridOccupantComponent actorGrid = actor.GetComponent<GridOccupantComponent>();
        GridOccupantComponent targetGrid = target?.GetComponent<GridOccupantComponent>();
        if (actorGrid == null || targetGrid == null)
        {
            reason = "目标不在地图中";
            return false;
        }

        int range = System.Math.Max(ability.Range, 0);
        if (BattleRuleQueries.GetManhattanDistance(actorGrid.Position, targetGrid.Position) > range)
        {
            reason = "目标超出范围";
            return false;
        }

        var context = new AbilityUseContext(
            gridMap,
            entities ?? System.Array.Empty<BattleEntity>(),
            actor,
            target,
            destination,
            ability,
            markEntityDefeated);

        AbilityTargetRule[] rules = ability.TargetRules?
            .Where(rule => rule != null)
            .ToArray() ?? System.Array.Empty<AbilityTargetRule>();

        if (rules.Length == 0)
        {
            reason = "能力缺少目标规则";
            return false;
        }

        foreach (AbilityTargetRule rule in rules)
        {
            if (!rule.IsValidTarget(context, out reason))
            {
                return false;
            }
        }

        return true;
    }

    public static AbilityEffectResult ApplyEffects(AbilityUseContext context)
    {
        if (context?.Ability?.Effects == null)
        {
            return AbilityEffectResult.None;
        }

        int totalDamage = 0;
        bool defeated = false;

        foreach (AbilityEffect effect in context.Ability.Effects.Where(effect => effect != null))
        {
            AbilityEffectResult result = effect.Apply(context);
            totalDamage += result.DamageApplied;
            defeated |= result.TargetDefeated;
        }

        return new AbilityEffectResult(totalDamage, defeated);
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
