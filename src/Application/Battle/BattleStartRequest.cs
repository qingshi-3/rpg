using System;
using System.Collections.Generic;
using Rpg.Application.Battle.Navigation;
using Rpg.Application.Battle.Snapshots;
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
    // Migration bridge carrier only: Strategic Battle Bridge owns these ids,
    // while legacy battle preparation still consumes BattleStartRequest.
    public string StrategicBattleSessionId { get; set; } = "";
    public string StrategicExpeditionId { get; set; } = "";
    public string StrategicSourceLocationId { get; set; } = "";
    public string StrategicTargetLocationId { get; set; } = "";
    public List<string> KnownTacticalTags { get; set; } = new();
    public string AttackerFactionId { get; set; } = "";
    public string DefenderFactionId { get; set; } = "";
    public WorldSiteAttackDirection AttackDirection { get; set; } = WorldSiteAttackDirection.Any;
    public string MapDefinitionId { get; set; } = "";
    public List<string> ObjectiveIds { get; set; } = new();
    public List<BattleObjectiveZoneSnapshot> ObjectiveZones { get; set; } = new();
    public BattleGroupPlanSnapshot PlayerBattleGroupPlan { get; set; } = new();
    public Dictionary<string, BattleGroupPlanSnapshot> PlayerBattleGroupPlans { get; set; } =
        new(StringComparer.Ordinal);
    public BattleGroupPlanSnapshot EnemyBattleGroupPlan { get; set; } = new();
    public Dictionary<string, BattleGroupPlanSnapshot> EnemyBattleGroupPlans { get; set; } =
        new(StringComparer.Ordinal);
    public BattleTacticalIntentPlanSnapshot EnemyTacticalIntentPlan { get; set; } = new();
    public Dictionary<string, BattleTacticalIntentPlanSnapshot> EnemyTacticalIntentPlans { get; set; } =
        new(StringComparer.Ordinal);
    public List<BattleEntranceRequest> AvailableEntrances { get; set; } = new();
    public List<BattleForceRequest> PlayerForces { get; set; } = new();
    public List<BattleForceRequest> EnemyForces { get; set; } = new();
    public List<BattleNavigationSurfaceSnapshot> NavigationSurfaces { get; set; } = new();
    public List<BattleNavigationConnectionSnapshot> NavigationConnections { get; set; } = new();
    public BattleNavigationTopology NavigationTopology { get; set; } = new();
    public List<BattleModifier> BattleModifiers { get; set; } = new();
    public string InitialCorpsCommandId { get; set; } = "";
    public SiteStateSnapshot SiteStateSnapshot { get; set; } = new();
    public string ReturnScenePath { get; set; } = "";
    public string SiteScenePath { get; set; } = "res://scenes/world/sites/WorldSiteRoot.tscn";
}
