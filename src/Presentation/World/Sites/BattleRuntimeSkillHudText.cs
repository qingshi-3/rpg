namespace Rpg.Presentation.World.Sites;

internal static class BattleRuntimeSkillHudText
{
    internal static string BuildStatusText(
        BattleRuntimeCommandGroupView selected,
        bool hasRuntime,
        WorldSiteRoot.BattleRuntimeSkillUsageState usageState)
    {
        if (selected == null)
        {
            return "未选择";
        }

        if (!hasRuntime)
        {
            return "未就绪";
        }

        return usageState switch
        {
            WorldSiteRoot.BattleRuntimeSkillUsageState.Unavailable => "需雷印",
            WorldSiteRoot.BattleRuntimeSkillUsageState.Pending => "等待",
            WorldSiteRoot.BattleRuntimeSkillUsageState.Used => "已用",
            _ => ""
        };
    }

    internal static string BuildUnavailableText(string reasonCode)
    {
        return reasonCode switch
        {
            "hero_skill_already_pending" => "技能指令正在等待结算",
            "hero_skill_already_used" => "本场战斗已经使用过",
            "hero_actor_unavailable" => "当前英雄无法行动",
            "skill_caster_not_allowed" => "当前英雄不能使用该技能",
            "hero_skill_target_missing" => "没有可影响的敌方目标",
            "thunder_mark_missing" => "没有可用雷印",
            "battle_already_complete" => "战斗已经结束",
            "battle_id_mismatch" => "战斗上下文不匹配",
            _ => "战斗运行时尚未准备好"
        };
    }
}
