using System;
using System.Collections.Generic;

namespace Rpg.Application.Battle;

public sealed class BattleStartRequest
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString("N");
    public string ContextId { get; set; } = "";
    public BattleKind BattleKind { get; set; } = BattleKind.Unknown;
    public string EncounterId { get; set; } = "";
    public string SourceArmyId { get; set; } = "";
    public string TargetArmyId { get; set; } = "";
    public string SourceSiteId { get; set; } = "";
    public string TargetSiteId { get; set; } = "";
    public string ThreatId { get; set; } = "";
    public string AttackerFactionId { get; set; } = "";
    public string DefenderFactionId { get; set; } = "";
    public string MapDefinitionId { get; set; } = "";
    public List<string> ObjectiveIds { get; set; } = new();
    public List<BattleEntranceRequest> AvailableEntrances { get; set; } = new();
    public List<BattleForceRequest> PlayerForces { get; set; } = new();
    public List<BattleForceRequest> EnemyForces { get; set; } = new();
    public List<BattleModifier> BattleModifiers { get; set; } = new();
    public SiteStateSnapshot SiteStateSnapshot { get; set; } = new();
    public string ReturnScenePath { get; set; } = "";
    public string SiteScenePath { get; set; } = "res://scenes/world/sites/WorldSiteRoot.tscn";
}
