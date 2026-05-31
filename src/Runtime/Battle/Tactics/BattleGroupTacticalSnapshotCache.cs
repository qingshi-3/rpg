using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;

namespace Rpg.Runtime.Battle.Tactics;

public sealed class BattleGroupTacticalSnapshotCache
{
    private readonly Dictionary<string, BattleGroupTacticalState> _snapshots;

    private BattleGroupTacticalSnapshotCache(IReadOnlyDictionary<string, BattleGroupTacticalState> snapshots)
    {
        _snapshots = snapshots?.ToDictionary(
            item => item.Key,
            item => BattleGroupTacticalStateStore.CloneState(item.Value),
            StringComparer.Ordinal) ?? new Dictionary<string, BattleGroupTacticalState>(StringComparer.Ordinal);
    }

    public static BattleGroupTacticalSnapshotCache Capture(BattleGroupTacticalStateStore store)
    {
        return new BattleGroupTacticalSnapshotCache(store?.CaptureSnapshots());
    }

    public BattleGroupTacticalState GetRequiredSnapshot(string battleGroupId)
    {
        return _snapshots.TryGetValue(battleGroupId ?? "", out BattleGroupTacticalState state)
            ? BattleGroupTacticalStateStore.CloneState(state)
            : throw new KeyNotFoundException($"battle group tactical snapshot not found: {battleGroupId}");
    }

    public IReadOnlyDictionary<string, BattleGroupTacticalState> GetAllSnapshots()
    {
        return new ReadOnlyDictionary<string, BattleGroupTacticalState>(_snapshots.ToDictionary(
            item => item.Key,
            item => BattleGroupTacticalStateStore.CloneState(item.Value),
            StringComparer.Ordinal));
    }
}
