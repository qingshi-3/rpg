using System;
using System.Collections.Generic;
using System.Linq;

namespace Rpg.Application.Battle.Auto;

public sealed class AutoBattleReportBuilder
{
    public AutoBattleReport Build(AutoBattleSimulationResult simulationResult)
    {
        if (simulationResult == null)
        {
            throw new ArgumentNullException(nameof(simulationResult));
        }

        BattleResult battleResult = simulationResult.BattleResult ?? new BattleResult();
        List<AutoBattleEvent> events = simulationResult.Events?.ToList() ?? new List<AutoBattleEvent>();
        Dictionary<string, int> attackCounts = events
            .Where(item => item.Kind == AutoBattleEventKind.AttackResolved)
            .GroupBy(item => item.ForceId ?? "", StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        Dictionary<string, int> damageDealt = events
            .Where(item => item.Kind == AutoBattleEventKind.AttackResolved)
            .GroupBy(item => item.ForceId ?? "", StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Sum(item => Math.Max(0, item.Damage)), StringComparer.Ordinal);
        Dictionary<string, int> unitsDefeated = events
            .Where(item => item.Kind == AutoBattleEventKind.UnitDefeated)
            .GroupBy(item => item.ForceId ?? "", StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        AutoBattleReport report = new()
        {
            Outcome = battleResult.Outcome
        };

        foreach (BattleForceResult forceResult in battleResult.ForceResults ?? new List<BattleForceResult>())
        {
            string forceId = forceResult.ForceId ?? "";
            AutoBattleForceReport forceReport = new()
            {
                ForceId = forceId,
                SourceKind = forceResult.SourceKind ?? "",
                SourceId = forceResult.SourceId ?? "",
                UnitDefinitionId = forceResult.UnitDefinitionId ?? "",
                InitialCount = Math.Max(0, forceResult.InitialCount),
                SurvivedCount = Math.Max(0, forceResult.SurvivedCount),
                DefeatedCount = Math.Max(0, forceResult.DefeatedCount),
                AttackCount = attackCounts.TryGetValue(forceId, out int attacks) ? attacks : 0,
                DamageDealt = damageDealt.TryGetValue(forceId, out int damage) ? damage : 0,
                UnitsDefeated = unitsDefeated.TryGetValue(forceId, out int defeated) ? defeated : 0
            };

            report.ForceReports.Add(forceReport);
        }

        report.InitialUnitCount = report.ForceReports.Sum(item => item.InitialCount);
        report.SurvivedUnitCount = report.ForceReports.Sum(item => item.SurvivedCount);
        report.DefeatedUnitCount = report.ForceReports.Sum(item => item.DefeatedCount);
        report.TopFailureReason = ResolveTopFailureReason(report);
        report.EventFeed = BuildEventFeed(events);
        return report;
    }

    private static List<AutoBattleReportEvent> BuildEventFeed(IReadOnlyList<AutoBattleEvent> events)
    {
        List<AutoBattleReportEvent> feed = new();
        foreach (AutoBattleEvent runtimeEvent in events)
        {
            string summaryKey = ToSummaryKey(runtimeEvent.Kind);
            if (string.IsNullOrWhiteSpace(summaryKey))
            {
                continue;
            }

            feed.Add(new AutoBattleReportEvent
            {
                Tick = runtimeEvent.Tick,
                Kind = runtimeEvent.Kind,
                SummaryKey = summaryKey,
                ActorId = runtimeEvent.ActorId ?? "",
                TargetId = runtimeEvent.TargetId ?? "",
                ForceId = runtimeEvent.ForceId ?? "",
                UnitDefinitionId = runtimeEvent.UnitDefinitionId ?? "",
                Damage = runtimeEvent.Damage,
                RemainingHealth = runtimeEvent.RemainingHealth,
                Outcome = runtimeEvent.Outcome
            });
        }

        return feed;
    }

    private static string ResolveTopFailureReason(AutoBattleReport report)
    {
        if (report.Outcome != BattleOutcome.Defeat)
        {
            return "";
        }

        int playerSurvivors = report.ForceReports
            .Where(IsPlayerForce)
            .Sum(item => item.SurvivedCount);
        return playerSurvivors <= 0 ? "player_force_eliminated" : "";
    }

    private static bool IsPlayerForce(AutoBattleForceReport report)
    {
        return string.Equals(report.SourceKind, "PlayerArmy", StringComparison.Ordinal) ||
               report.ForceId.StartsWith("player", StringComparison.OrdinalIgnoreCase);
    }

    private static string ToSummaryKey(AutoBattleEventKind kind)
    {
        return kind switch
        {
            AutoBattleEventKind.BattleStarted => "battle_started",
            AutoBattleEventKind.UnitSpawned => "unit_deployed",
            AutoBattleEventKind.AttackResolved => "attack_resolved",
            AutoBattleEventKind.UnitDefeated => "unit_defeated",
            AutoBattleEventKind.BattleEnded => "battle_ended",
            _ => ""
        };
    }
}
