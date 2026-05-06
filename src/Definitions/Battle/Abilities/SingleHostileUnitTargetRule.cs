using Godot;
using Rpg.Presentation.Battle.Abilities;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Rules;

namespace Rpg.Definitions.Battle.Abilities;

[GlobalClass]
public partial class SingleHostileUnitTargetRule : AbilityTargetRule
{
    public override bool IsValidTarget(AbilityUseContext context, out string reason)
    {
        reason = "";

        if (context?.Actor == null || context.Target == null)
        {
            reason = "缺少目标";
            return false;
        }

        if (context.Actor == context.Target)
        {
            reason = "不能选择自己";
            return false;
        }

        if (BattleRuleQueries.IsDefeated(context.Actor))
        {
            reason = "倒下的单位不能行动";
            return false;
        }

        if (BattleRuleQueries.IsDefeated(context.Target))
        {
            reason = "目标已经倒下";
            return false;
        }

        if (!BattleRuleQueries.AreHostile(context.Actor, context.Target))
        {
            reason = "只能选择敌方目标";
            return false;
        }

        if (context.Target.GetComponent<TargetableComponent>() is { IsTargetable: false })
        {
            reason = "目标不可被选中";
            return false;
        }

        if (context.Target.GetComponent<HealthComponent>() == null)
        {
            reason = "目标没有生命数据";
            return false;
        }

        return true;
    }
}
