using System.Linq;
using Rpg.Application.Battle.Settlement;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Results;

namespace Rpg.Application.Battle.Reports;

public sealed class BattleReportBuilder
{
    public BattleReportRecord Build(
        BattleOutcomeResult result,
        BattleEventStream eventStream,
        SettlementPlan settlementPlan)
    {
        if (settlementPlan?.Accepted != true)
        {
            string reason = string.IsNullOrWhiteSpace(settlementPlan?.RejectionReason)
                ? "settlement_not_accepted"
                : settlementPlan.RejectionReason;

            return new BattleReportRecord
            {
                ReportId = "",
                SnapshotId = result?.SnapshotId ?? settlementPlan?.SnapshotId ?? "",
                BattleId = result?.BattleId ?? settlementPlan?.BattleId ?? "",
                OutcomeSummary = "SettlementRejected",
                SourceEventIds = eventStream?.EventIds.ToList() ?? new(),
                FailureCandidates = { reason },
                HeroSkillUses = BuildHeroSkillUses(eventStream),
                HeroSkillEffects = BuildHeroSkillEffects(eventStream),
                HeroSkillFailures = BuildHeroSkillFailures(eventStream)
            };
        }

        return new BattleReportRecord
        {
            ReportId = $"{result?.BattleId ?? ""}:report",
            SnapshotId = result?.SnapshotId ?? "",
            BattleId = result?.BattleId ?? "",
            OutcomeSummary = result?.TerminationReason.ToString() ?? "",
            SourceEventIds = eventStream?.EventIds.ToList() ?? new(),
            HeroSkillUses = BuildHeroSkillUses(eventStream),
            HeroSkillEffects = BuildHeroSkillEffects(eventStream),
            HeroSkillFailures = BuildHeroSkillFailures(eventStream)
        };
    }

    private static System.Collections.Generic.List<string> BuildHeroSkillUses(BattleEventStream eventStream)
    {
        return eventStream?.Events?
            .Where(item => item?.Kind == BattleEventKind.SkillUsed)
            .Select(item => $"{item.ReasonCode}:{item.ActorId}->{item.TargetId}")
            .ToList() ?? new System.Collections.Generic.List<string>();
    }

    private static System.Collections.Generic.List<BattleReportSkillEffectFact> BuildHeroSkillEffects(BattleEventStream eventStream)
    {
        return eventStream?.Events?
            .Where(item =>
                item?.Kind == BattleEventKind.EffectApplied &&
                !string.IsNullOrWhiteSpace(item.SourceDefinitionId))
            .Select(item => new BattleReportSkillEffectFact
            {
                SourceCommandId = item.SourceCommandId ?? "",
                SourceActionId = item.SourceActionId ?? "",
                SourceDefinitionId = item.SourceDefinitionId ?? "",
                EffectKind = item.EffectKind ?? "",
                ActorId = item.ActorId ?? "",
                TargetId = item.TargetId ?? "",
                ReasonCode = item.ReasonCode ?? "",
                CorpsStrengthDelta = item.CorpsStrengthDelta,
                RuntimeTick = item.RuntimeTick,
                RuntimeTimeSeconds = item.RuntimeTimeSeconds
            })
            .ToList() ?? new System.Collections.Generic.List<BattleReportSkillEffectFact>();
    }

    private static System.Collections.Generic.List<BattleReportSkillFailureFact> BuildHeroSkillFailures(BattleEventStream eventStream)
    {
        return eventStream?.Events?
            .Where(item =>
                item?.Kind == BattleEventKind.CommandFailed &&
                !string.IsNullOrWhiteSpace(item.SourceDefinitionId))
            .Select(item => new BattleReportSkillFailureFact
            {
                SourceCommandId = item.SourceCommandId ?? "",
                SourceDefinitionId = item.SourceDefinitionId ?? "",
                ActorId = item.ActorId ?? "",
                TargetId = item.TargetId ?? "",
                ReasonCode = item.ReasonCode ?? "",
                RuntimeTick = item.RuntimeTick,
                RuntimeTimeSeconds = item.RuntimeTimeSeconds
            })
            .ToList() ?? new System.Collections.Generic.List<BattleReportSkillFailureFact>();
    }
}
