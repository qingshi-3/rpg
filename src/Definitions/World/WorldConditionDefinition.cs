using Rpg.Domain.World;
using System.Collections.Generic;

namespace Rpg.Definitions.World;

public sealed class WorldConditionDefinition
{
    public WorldConditionKind Kind { get; set; } = WorldConditionKind.Always;
    public string SiteId { get; set; } = "";
    public string TargetId { get; set; } = "";
    public string FactionId { get; set; } = "";
    public string ResourceId { get; set; } = "";
    public string UnitTypeId { get; set; } = "";
    public string RuleId { get; set; } = "";
    public SiteControlState ControlState { get; set; } = SiteControlState.PlayerHeld;
    public List<SiteControlState> ControlStates { get; set; } = new();
    public int Amount { get; set; }
    public string FailureReasonKey { get; set; } = "";
}
