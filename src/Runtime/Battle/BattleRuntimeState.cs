using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle.Navigation;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle;

public sealed class BattleRuntimeState
{
    public string SnapshotId { get; set; } = "";
    public string BattleId { get; set; } = "";
    public List<BattleRuntimeActor> Actors { get; set; } = new();
    public List<BattleObjectiveZoneSnapshot> ObjectiveZones { get; set; } = new();
    public List<BattleSkillSnapshot> SkillDefinitions { get; set; } = new();
    // Shared beacon objects form a query/presentation catalog. Each commander
    // state's active beacon reference remains the command-selection authority.
    public List<BattleRuntimeDestinationBeacon> DestinationBeacons { get; } = new();
    public List<BattleRuntimeSpatialMark> SpatialMarks { get; } = new();
    internal BattleBeaconFlowFieldCache BeaconFlowFields { get; } = new();
    internal long NextAbilityOrderSequence { get; set; }
    internal BattleSkillAvailabilityState SkillAvailability { get; } = new();
    internal BattleGroupTacticalStateStore TacticalStateStore { get; set; } = BattleGroupTacticalStateStore.Empty();
    internal IReadOnlyDictionary<string, BattleGroupPerceptionSummary> GroupPerceptionSummaryStore { get; set; } =
        new Dictionary<string, BattleGroupPerceptionSummary>();
    internal IReadOnlyDictionary<string, BattleCombatZoneSnapshot> CombatZoneStore { get; set; } =
        new ReadOnlyDictionary<string, BattleCombatZoneSnapshot>(new Dictionary<string, BattleCombatZoneSnapshot>());
    internal IReadOnlyDictionary<string, BattleGroupActionZoneSnapshot> GroupActionZoneStore { get; set; } =
        new ReadOnlyDictionary<string, BattleGroupActionZoneSnapshot>(new Dictionary<string, BattleGroupActionZoneSnapshot>());

    public IReadOnlyDictionary<string, BattleGroupTacticalState> TacticalStates => TacticalStateStore.CaptureSnapshots();
    public IReadOnlyDictionary<string, IReadOnlyList<BattleGroupTacticalRegionMutationResult>> TacticalInitializationResults => TacticalStateStore.CaptureInitializationResults();
    public IReadOnlyDictionary<string, BattleGroupPerceptionSummary> GroupPerceptionSummaries => GroupPerceptionSummaryStore;
    public IReadOnlyDictionary<string, BattleCombatZoneSnapshot> CombatZones => CombatZoneStore;
    public IReadOnlyDictionary<string, BattleGroupActionZoneSnapshot> GroupActionZones => GroupActionZoneStore;
}
