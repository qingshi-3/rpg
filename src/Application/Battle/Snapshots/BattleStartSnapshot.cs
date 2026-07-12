using System.Collections.Generic;

namespace Rpg.Application.Battle.Snapshots;

public sealed class BattleStartSnapshot
{
    public string SnapshotId { get; set; } = "";
    public string BattleId { get; set; } = "";
    public string StrategicBattleSessionId { get; set; } = "";
    public string StrategicBattleDraftId { get; set; } = "";
    public long StrategicBattleDraftRevision { get; set; }
    public string TargetLocationId { get; set; } = "";
    public LocationBattleContext LocationContext { get; set; } = new();
    public List<BattleGroupSnapshot> BattleGroups { get; set; } = new();
    public List<BattleSkillSnapshot> SkillDefinitions { get; set; } = new();
    public List<BattleObjectiveZoneSnapshot> ObjectiveZones { get; set; } = new();
}
