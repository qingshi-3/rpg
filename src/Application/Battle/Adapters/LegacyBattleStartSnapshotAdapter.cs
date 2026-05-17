using System.Collections.Generic;
using Rpg.Application.Battle.Snapshots;
using Rpg.Domain.BattleGroups;
using Rpg.Domain.Corps;
using Rpg.Domain.Heroes;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.Battle.Adapters;

public sealed class LegacyBattleStartSnapshotAdapter
{
    private readonly BattleSnapshotBuilder _snapshotBuilder = new();

    public BattleStartSnapshot ToSnapshot(
        BattleStartRequest request,
        IEnumerable<BattleGroupState> groups,
        IReadOnlyDictionary<string, HeroState> heroes,
        IReadOnlyDictionary<string, CorpsState> corps)
    {
        string snapshotId = string.IsNullOrWhiteSpace(request?.RequestId)
            ? $"snapshot:{request?.ContextId ?? ""}"
            : $"snapshot:{request.RequestId}";

        BattleStartSnapshot snapshot = _snapshotBuilder.Build(
            snapshotId,
            request?.ContextId ?? "",
            request?.TargetSiteId ?? "",
            groups,
            heroes,
            corps);

        GameLog.Info(nameof(LegacyBattleStartSnapshotAdapter), $"Converted legacy battle request to snapshot request={request?.RequestId ?? ""} snapshot={snapshot.SnapshotId}");
        return snapshot;
    }
}
