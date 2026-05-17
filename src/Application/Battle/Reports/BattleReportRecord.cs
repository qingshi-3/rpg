using System.Collections.Generic;

namespace Rpg.Application.Battle.Reports;

public sealed class BattleReportRecord
{
    public string ReportId { get; set; } = "";
    public string SnapshotId { get; set; } = "";
    public string BattleId { get; set; } = "";
    public string OutcomeSummary { get; set; } = "";
    public List<string> SourceEventIds { get; set; } = new();
    public List<string> FailureCandidates { get; set; } = new();
}
