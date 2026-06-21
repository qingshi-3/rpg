using System.Collections.Generic;
using Rpg.Application.Battle.Snapshots;

namespace Rpg.Application.Battle;

public sealed class BattleForceRequest
{
    public string ForceId { get; set; } = "";
    public string CommandGroupId { get; set; } = "";
    public string SourceKind { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string UnitDefinitionId { get; set; } = "";
    // Migration bridge carrier only. Runtime snapshots consume copied values;
    // this legacy request remains a removable scene-preparation adapter.
    public string StrategicParticipantId { get; set; } = "";
    public string StrategicHeroId { get; set; } = "";
    public string StrategicHeroDefinitionId { get; set; } = "";
    public string StrategicHeroBattleUnitId { get; set; } = "";
    public string StrategicCorpsInstanceId { get; set; } = "";
    public string StrategicCorpsDefinitionId { get; set; } = "";
    public string StrategicCorpsBattleUnitId { get; set; } = "";
    public string StrategicSourceLocationId { get; set; } = "";
    public int StrategicPreBattleCorpsStrength { get; set; }
    public int Count { get; set; }
    public int FootprintWidth { get; set; } = 1;
    public int FootprintHeight { get; set; } = 1;
    public int MaxHitPoints { get; set; }
    public int AttackDamage { get; set; }
    public int AttackRange { get; set; } = 1;
    public double AttackSpeed { get; set; } = BattleAttackSpeedPolicy.DefaultAttackSpeed;
    public double MoveStepSeconds { get; set; } = BattleActionTimingPolicy.DefaultMoveStepSeconds;
    public double AttackActionSeconds { get; set; }
    // NaN keeps this bridge unset so Runtime can derive the default impact point;
    // authored zero is reserved for explicit instant-impact behavior.
    public double AttackImpactDelaySeconds { get; set; } = double.NaN;
    public string FactionId { get; set; } = "";
    public string PreferredEntranceId { get; set; } = "";
    public string DefaultFormationId { get; set; } = "";
    public BattleTacticalIntentPlanSnapshot TacticalIntentPlan { get; set; } = new();
    public List<BattleForcePlacementRequest> PreferredPlacements { get; set; } = new();
}
