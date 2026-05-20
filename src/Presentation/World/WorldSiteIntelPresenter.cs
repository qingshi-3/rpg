using System.Collections.Generic;
using System.Linq;
using Rpg.Application.World;
using Rpg.Definitions.World;

namespace Rpg.Presentation.World;

public static class WorldSiteIntelPresenter
{
    public static string GetPolicyLabel(WorldSiteIntelPolicy policy)
    {
        return policy switch
        {
            WorldSiteIntelPolicy.Transparent => "情报透明",
            WorldSiteIntelPolicy.Partial => "战术细节未知",
            WorldSiteIntelPolicy.Obscured => "布阵被遮蔽",
            _ => "情报未知"
        };
    }

    public static IEnumerable<string> BuildSummaryLines(WorldSiteIntelViewModel view)
    {
        if (view == null)
        {
            yield return "场域情报缺失。";
            yield break;
        }

        yield return $"情报状态：{GetPolicyLabel(view.Policy)}";
        if (view.IsStale)
        {
            yield return $"旧情报：世界步 {view.LastSeenWorldTick}";
        }

        if (!string.IsNullOrWhiteSpace(view.StrategicSummary))
        {
            yield return view.StrategicSummary;
        }

        if (!string.IsNullOrWhiteSpace(view.TacticalSummary))
        {
            yield return view.TacticalSummary;
        }

        foreach (string reason in view.UnknownIntelReasons.Where(reason => !string.IsNullOrWhiteSpace(reason)))
        {
            yield return $"未知：{reason}";
        }
    }

    public static string BuildSummaryText(WorldSiteIntelViewModel view)
    {
        return string.Join("\n", BuildSummaryLines(view));
    }

    public static string GetSiteEntryLabel(WorldSiteIntelViewModel view)
    {
        return "查看场域";
    }

    public static string GetDirectAssaultLabel()
    {
        return "直接强攻";
    }
}
