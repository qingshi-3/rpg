using System.Collections.Generic;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle;

public sealed class BattleRuntimeState
{
    public string SnapshotId { get; set; } = "";
    public string BattleId { get; set; } = "";
    public List<BattleRuntimeActor> Actors { get; set; } = new();
    internal BattleGroupTacticalStateStore TacticalStateStore { get; set; } = BattleGroupTacticalStateStore.Empty();
    internal IReadOnlyDictionary<string, BattleGroupPerceptionSummary> GroupPerceptionSummaryStore { get; set; } =
        new Dictionary<string, BattleGroupPerceptionSummary>();

    public IReadOnlyDictionary<string, BattleGroupTacticalState> TacticalStates => TacticalStateStore.CaptureSnapshots();
    public IReadOnlyDictionary<string, IReadOnlyList<BattleGroupTacticalRegionMutationResult>> TacticalInitializationResults => TacticalStateStore.CaptureInitializationResults();
    public IReadOnlyDictionary<string, BattleGroupPerceptionSummary> GroupPerceptionSummaries => GroupPerceptionSummaryStore;
}
