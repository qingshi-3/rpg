using Rpg.Application.Battle;
using System.Collections.Generic;

namespace Rpg.Application.Battle.Snapshots;

public sealed class BattleGroupSnapshot
{
    public string BattleGroupId { get; set; } = "";
    public string RuntimeCommanderGroupId { get; set; } = "";
    public string FactionId { get; set; } = "";
    public string SourceForceId { get; set; } = "";
    public string HeroId { get; set; } = "";
    public string HeroDefinitionId { get; set; } = "";
    public int HeroLevel { get; set; }
    public string CorpsId { get; set; } = "";
    public string CorpsDefinitionId { get; set; } = "";
    public int CorpsLevel { get; set; }
    public int CorpsEquipmentLevel { get; set; }
    public int CorpsStrength { get; set; }
    public string SourceLocationId { get; set; } = "";
    public int CellX { get; set; }
    public int CellY { get; set; }
    public int CellHeight { get; set; }
    public int FootprintWidth { get; set; } = 1;
    public int FootprintHeight { get; set; } = 1;
    public int MaxHitPoints { get; set; }
    public int AttackDamage { get; set; }
    public int AttackRange { get; set; } = 1;
    public double AttackSpeed { get; set; } = 1.0;
    public double MoveStepSeconds { get; set; } = BattleActionTimingPolicy.DefaultMoveStepSeconds;
    public double AttackActionSeconds { get; set; }
    public double AttackImpactDelaySeconds { get; set; }
    public string InitialCorpsCommandId { get; set; } = "";
    public BattleGroupPlanSnapshot Plan { get; set; } = new();
    public BattleGroupTacticalMode TacticalMode { get; set; } = BattleGroupTacticalMode.PlayerCommanded;
    public List<BattleTacticalRegionSnapshot> InitialTacticalRegions { get; set; } = new();
}
