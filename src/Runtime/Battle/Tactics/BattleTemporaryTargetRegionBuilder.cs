using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;

namespace Rpg.Runtime.Battle.Tactics;

public static class BattleTemporaryTargetRegionBuilder
{
    public static BattleTacticalRegionSnapshot BuildForGroup(
        string ownerBattleGroupId,
        IEnumerable<BattleRuntimeActor> livingCorps,
        int runtimeTick)
    {
        BattleOpposingCluster selected = BattleOpposingClusterBuilder
            .BuildForGroup(ownerBattleGroupId, livingCorps)
            .OrderByDescending(item => item.ActorCount)
            .ThenByDescending(item => item.TotalHitPoints)
            .ThenBy(item => item.DistanceFromOwnerAnchor)
            .ThenBy(item => item.ClusterId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (selected == null)
        {
            return null;
        }

        ResolveRegionExtent(selected, out int width, out int height);
        return new BattleTacticalRegionSnapshot
        {
            RegionId = BuildRegionId(ownerBattleGroupId, selected),
            OwnerBattleGroupId = ownerBattleGroupId ?? "",
            Kind = BattleTacticalRegionKind.TemporaryTarget,
            SourceRegionId = selected.ClusterId ?? "",
            ReasonCode = BattleGroupTacticalReasonCode.TemporaryRegionCreatedCluster,
            CenterCellX = selected.CenterCellX,
            CenterCellY = selected.CenterCellY,
            CenterCellHeight = selected.CenterCellHeight,
            Width = width,
            Height = height
        };
    }

    private static void ResolveRegionExtent(BattleOpposingCluster cluster, out int width, out int height)
    {
        width = Math.Max(
            BattleGroupTacticalPolicySettings.MinimumRegionWidth,
            cluster.MaxCellX - cluster.MinCellX + 1);
        height = Math.Max(
            BattleGroupTacticalPolicySettings.MinimumRegionHeight,
            cluster.MaxCellY - cluster.MinCellY + 1);
        while (width * height > BattleGroupTacticalPolicySettings.DefaultLocalCombatMaxCells)
        {
            if (width >= height && width > BattleGroupTacticalPolicySettings.MinimumRegionWidth)
            {
                width--;
                continue;
            }

            if (height > BattleGroupTacticalPolicySettings.MinimumRegionHeight)
            {
                height--;
                continue;
            }

            break;
        }
    }

    private static string BuildRegionId(string ownerBattleGroupId, BattleOpposingCluster cluster)
    {
        string firstActorId = cluster.ActorIds?.FirstOrDefault() ?? "cluster";
        string normalizedOwner = Sanitize(ownerBattleGroupId);
        string normalizedActor = Sanitize(firstActorId);
        return $"{normalizedOwner}:temporary:{cluster.CenterCellX}:{cluster.CenterCellY}:{normalizedActor}";
    }

    private static string Sanitize(string value)
    {
        string sanitized = string.Join("_", (value ?? "").Split(new[] { ':', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(sanitized) ? "group" : sanitized;
    }
}
