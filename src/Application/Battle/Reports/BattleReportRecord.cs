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
    public List<string> HeroSkillUses { get; set; } = new();
    public List<BattleReportSkillEffectFact> HeroSkillEffects { get; set; } = new();
    public List<BattleReportSkillFailureFact> HeroSkillFailures { get; set; } = new();
}

public sealed class BattleReportSkillEffectFact
{
    public string SourceCommandId { get; set; } = "";
    public string SourceActionId { get; set; } = "";
    public string SourceDefinitionId { get; set; } = "";
    public string EffectKind { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string TargetId { get; set; } = "";
    public string ReasonCode { get; set; } = "";
    public int CorpsStrengthDelta { get; set; }
    public int RuntimeTick { get; set; }
    public double RuntimeTimeSeconds { get; set; }
}

public sealed class BattleReportSkillFailureFact
{
    public string SourceCommandId { get; set; } = "";
    public string SourceDefinitionId { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string TargetId { get; set; } = "";
    public string ReasonCode { get; set; } = "";
    public int RuntimeTick { get; set; }
    public double RuntimeTimeSeconds { get; set; }
}
