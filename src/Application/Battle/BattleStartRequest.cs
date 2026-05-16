using System;
using System.Collections.Generic;
using Rpg.Domain.World;

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
    public string SiteIntelPolicyId { get; set; } = "";
    public List<string> RevealedEntranceIds { get; set; } = new();
    public List<string> KnownTacticalTags { get; set; } = new();
    public List<string> ActiveObscurationSourceIds { get; set; } = new();
    public List<string> ExplorationAdvantageTags { get; set; } = new();
    public string ThreatId { get; set; } = "";
    public string WorldBattleId { get; set; } = "";
    public string WorldBattlePhase { get; set; } = "";
    public string AttackerFactionId { get; set; } = "";
    public string DefenderFactionId { get; set; } = "";
    public WorldSiteAttackDirection AttackDirection { get; set; } = WorldSiteAttackDirection.Any;
    public string MapDefinitionId { get; set; } = "";
    public string ExplorationPointId { get; set; } = "";
    public string ExplorationTriggerPatrolId { get; set; } = "";
    public int ExplorationEntryCellX { get; set; }
    public int ExplorationEntryCellY { get; set; }
    public int ExplorationEntryCellHeight { get; set; }
    public int ExplorationAlertLevel { get; set; }
    public List<string> ObjectiveIds { get; set; } = new();
    public List<BattleEntranceRequest> AvailableEntrances { get; set; } = new();
    public List<BattleForceRequest> PlayerForces { get; set; } = new();
    public List<BattleForceRequest> EnemyForces { get; set; } = new();
    public List<BattleModifier> BattleModifiers { get; set; } = new();
    public SiteStateSnapshot SiteStateSnapshot { get; set; } = new();
    public string ReturnScenePath { get; set; } = "";
    public string SiteScenePath { get; set; } = "res://scenes/world/sites/WorldSiteRoot.tscn";
}
