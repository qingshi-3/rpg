using System.Collections.Generic;

namespace Rpg.Application.Battle;

public sealed class BattleResult
{
    public string RequestId { get; set; } = "";
    public string ContextId { get; set; } = "";
    public BattleKind BattleKind { get; set; } = BattleKind.Unknown;
    public BattleOutcome Outcome { get; set; } = BattleOutcome.None;
    public List<BattleObjectiveResult> ObjectiveResults { get; set; } = new();
    public List<BattleForceResult> ForceResults { get; set; } = new();
    public List<BattleResourceChange> ResourceChanges { get; set; } = new();
    public List<SiteStateChange> SiteStateChanges { get; set; } = new();
    public List<string> TagsAdded { get; set; } = new();
    public List<string> TagsRemoved { get; set; } = new();
}
