using System;
using System.Linq;

namespace Rpg.Application.Battle.Auto;

public sealed class AutoBattleReportSummaryFormatter
{
    public string Format(AutoBattleReport report)
    {
        if (report == null)
        {
            return "";
        }

        string outcome = report.Outcome switch
        {
            BattleOutcome.Victory => "自动战斗胜利",
            BattleOutcome.Defeat => "自动战斗失败",
            BattleOutcome.Withdraw => "自动战斗撤退",
            BattleOutcome.Disaster => "自动战斗惨败",
            _ => "自动战斗结束"
        };

        string summary = $"{outcome}：参战 {Math.Max(0, report.InitialUnitCount)}，生还 {Math.Max(0, report.SurvivedUnitCount)}，战损 {Math.Max(0, report.DefeatedUnitCount)}。";
        string reason = FormatFailureReason(report.TopFailureReason);
        if (!string.IsNullOrWhiteSpace(reason))
        {
            summary += $" 失败原因：{reason}。";
        }

        AutoBattleForceReport topContribution = report.ForceReports
            .OrderByDescending(item => Math.Max(0, item.DamageDealt))
            .ThenByDescending(item => Math.Max(0, item.UnitsDefeated))
            .FirstOrDefault(item => Math.Max(0, item.DamageDealt) > 0 || Math.Max(0, item.UnitsDefeated) > 0);
        if (topContribution != null)
        {
            summary += $" 主要贡献：{ResolveForceLabel(topContribution)} 造成 {Math.Max(0, topContribution.DamageDealt)} 伤害，击败 {Math.Max(0, topContribution.UnitsDefeated)}。";
        }

        return summary;
    }

    private static string FormatFailureReason(string reasonKey)
    {
        return reasonKey switch
        {
            "player_force_eliminated" => "我方战斗单位全部失去战斗力",
            _ => ""
        };
    }

    private static string ResolveForceLabel(AutoBattleForceReport report)
    {
        return string.IsNullOrWhiteSpace(report.ForceId) ? "未知部队" : report.ForceId.Trim();
    }
}
