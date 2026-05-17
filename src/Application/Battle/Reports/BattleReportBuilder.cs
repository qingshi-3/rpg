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
                FailureCandidates = { reason }
            };
        }

        return new BattleReportRecord
        {
            ReportId = $"{result?.BattleId ?? ""}:report",
            SnapshotId = result?.SnapshotId ?? "",
            BattleId = result?.BattleId ?? "",
            OutcomeSummary = result?.TerminationReason.ToString() ?? "",
            SourceEventIds = eventStream?.EventIds.ToList() ?? new()
        };
    }
}
