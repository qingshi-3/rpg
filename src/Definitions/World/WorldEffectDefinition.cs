using Rpg.Domain.World;

namespace Rpg.Definitions.World;

public sealed class WorldEffectDefinition
{
    public WorldEffectKind Kind { get; set; } = WorldEffectKind.AddResource;
    public string SiteId { get; set; } = "";
    public string TargetId { get; set; } = "";
    public string FactionId { get; set; } = "";
    public string ResourceId { get; set; } = "";
    public string FacilityId { get; set; } = "";
    public string FacilityInstanceId { get; set; } = "";
    public string SlotId { get; set; } = "";
    public string UnitTypeId { get; set; } = "";
    public string RuleId { get; set; } = "";
    public string ThreatId { get; set; } = "";
    public string Tag { get; set; } = "";
    public string BattleKind { get; set; } = "";
    public int Amount { get; set; }
    public SiteControlState ControlState { get; set; } = SiteControlState.PlayerHeld;
    public FacilityState FacilityState { get; set; } = FacilityState.Active;
    public ThreatStage ThreatStage { get; set; } = ThreatStage.Attacking;
    public WorldArmyIntent ArmyIntent { get; set; } = WorldArmyIntent.None;
}
