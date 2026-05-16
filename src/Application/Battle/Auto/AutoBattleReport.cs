using System.Collections.Generic;

namespace Rpg.Application.Battle.Auto;

public sealed class AutoBattleReport
{
    public BattleOutcome Outcome { get; set; } = BattleOutcome.None;
    public int InitialUnitCount { get; set; }
    public int SurvivedUnitCount { get; set; }
    public int DefeatedUnitCount { get; set; }
    public string TopFailureReason { get; set; } = "";
    public List<AutoBattleForceReport> ForceReports { get; set; } = new();
    public List<AutoBattleReportEvent> EventFeed { get; set; } = new();
}
