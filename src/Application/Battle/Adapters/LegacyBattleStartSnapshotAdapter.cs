using System.Collections.Generic;
using Rpg.Application.Battle.Navigation;
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

        BattleNavigationSnapshotBuilder.CopyRequestToLocationContext(request, snapshot.LocationContext);
        CopyObjectiveZones(request, snapshot);
        GameLog.Info(
            nameof(LegacyBattleStartSnapshotAdapter),
            BattleNavigationTopologyDiagnostics.DescribeSnapshotTopology(snapshot, "legacy_request_to_snapshot"));
        GameLog.Info(nameof(LegacyBattleStartSnapshotAdapter), $"Converted legacy battle request to snapshot request={request?.RequestId ?? ""} snapshot={snapshot.SnapshotId}");
        return snapshot;
    }

    public void RecompileSkillDefinitions(BattleStartSnapshot snapshot)
    {
        _snapshotBuilder.RecompileSkillDefinitions(snapshot);
    }

    private static void CopyObjectiveZones(BattleStartRequest request, BattleStartSnapshot snapshot)
    {
        snapshot?.ObjectiveZones?.Clear();
        if (request?.ObjectiveZones == null || snapshot?.ObjectiveZones == null)
        {
            return;
        }

        foreach (BattleObjectiveZoneSnapshot zone in request.ObjectiveZones)
        {
            if (zone == null || string.IsNullOrWhiteSpace(zone.ObjectiveZoneId))
            {
                continue;
            }

            // Objective zones are authored before runtime and copied once into
            // the snapshot so runtime plan resolution does not depend on UI state.
            snapshot.ObjectiveZones.Add(new BattleObjectiveZoneSnapshot
            {
                ObjectiveZoneId = zone.ObjectiveZoneId,
                DisplayName = zone.DisplayName ?? "",
                ObjectiveRole = zone.ObjectiveRole ?? "",
                DeploymentSide = zone.DeploymentSide ?? "",
                FactionId = zone.FactionId ?? "",
                Priority = zone.Priority,
                CellX = zone.CellX,
                CellY = zone.CellY,
                CellHeight = zone.CellHeight,
                Width = zone.Width,
                Height = zone.Height
            });
        }
    }
}
