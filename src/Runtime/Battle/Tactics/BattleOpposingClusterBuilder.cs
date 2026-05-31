using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle.Tactics;

public sealed class BattleOpposingCluster
{
    public string ClusterId { get; init; } = "";
    public IReadOnlyList<string> ActorIds { get; init; } = Array.Empty<string>();
    public int ActorCount { get; init; }
    public int TotalHitPoints { get; init; }
    public int MinCellX { get; init; }
    public int MinCellY { get; init; }
    public int MaxCellX { get; init; }
    public int MaxCellY { get; init; }
    public int CenterCellX { get; init; }
    public int CenterCellY { get; init; }
    public int CenterCellHeight { get; init; }
    public int DistanceFromOwnerAnchor { get; init; }
}

public static class BattleOpposingClusterBuilder
{
    public static IReadOnlyList<BattleOpposingCluster> BuildForGroup(
        string ownerBattleGroupId,
        IEnumerable<BattleRuntimeActor> actors,
        int mergeRange = BattleGroupTacticalPolicySettings.DefaultLocalPerceptionRange * 2)
    {
        BattleRuntimeActor[] aliveCorps = (actors ?? Enumerable.Empty<BattleRuntimeActor>())
            .Where(item => item?.Kind == BattleRuntimeActorKind.Corps && item.HitPoints > 0)
            .OrderBy(item => item.ActorId, StringComparer.Ordinal)
            .ToArray();
        BattleRuntimeActor[] ownerMembers = aliveCorps
            .Where(item => string.Equals(item.BattleGroupId ?? "", ownerBattleGroupId ?? "", StringComparison.Ordinal))
            .ToArray();
        if (ownerMembers.Length == 0)
        {
            return Array.Empty<BattleOpposingCluster>();
        }

        HashSet<string> ownerFactions = ownerMembers
            .Select(item => NormalizeFaction(item.FactionId))
            .ToHashSet(StringComparer.Ordinal);
        List<List<BattleRuntimeActor>> clusters = new();
        foreach (BattleRuntimeActor actor in aliveCorps.Where(item =>
                     !string.Equals(item.BattleGroupId ?? "", ownerBattleGroupId ?? "", StringComparison.Ordinal) &&
                     !ownerFactions.Contains(NormalizeFaction(item.FactionId))))
        {
            AddToClusters(clusters, actor, Math.Max(1, mergeRange));
        }

        BattleGridCoord ownerAnchor = ResolveMedianAnchor(ownerMembers);
        return clusters
            .Select(cluster => BuildCluster(cluster, ownerAnchor))
            .OrderBy(item => item.ClusterId, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddToClusters(List<List<BattleRuntimeActor>> clusters, BattleRuntimeActor actor, int mergeRange)
    {
        List<List<BattleRuntimeActor>> matching = clusters
            .Where(cluster => cluster.Any(member => GetSquareDistance(member, actor) <= mergeRange))
            .ToList();
        if (matching.Count == 0)
        {
            clusters.Add(new List<BattleRuntimeActor> { actor });
            return;
        }

        List<BattleRuntimeActor> target = matching[0];
        target.Add(actor);
        foreach (List<BattleRuntimeActor> extra in matching.Skip(1))
        {
            target.AddRange(extra);
            clusters.Remove(extra);
        }

        target.Sort((left, right) => string.CompareOrdinal(left.ActorId, right.ActorId));
    }

    private static BattleOpposingCluster BuildCluster(IReadOnlyList<BattleRuntimeActor> actors, BattleGridCoord ownerAnchor)
    {
        BattleRuntimeActor[] ordered = actors
            .OrderBy(item => item.ActorId, StringComparer.Ordinal)
            .ToArray();
        int[] xs = ordered.Select(item => item.GridX).OrderBy(item => item).ToArray();
        int[] ys = ordered.Select(item => item.GridY).OrderBy(item => item).ToArray();
        int[] heights = ordered.Select(item => item.GridHeight).OrderBy(item => item).ToArray();
        int centerX = xs[xs.Length / 2];
        int centerY = ys[ys.Length / 2];
        int centerHeight = heights[heights.Length / 2];
        string firstActorId = ordered.FirstOrDefault()?.ActorId ?? "cluster";
        return new BattleOpposingCluster
        {
            ClusterId = $"cluster:{Sanitize(firstActorId)}:{centerX}:{centerY}",
            ActorIds = ordered.Select(item => item.ActorId ?? "").ToArray(),
            ActorCount = ordered.Length,
            TotalHitPoints = ordered.Sum(item => Math.Max(0, item.HitPoints)),
            MinCellX = xs.First(),
            MinCellY = ys.First(),
            MaxCellX = xs.Last(),
            MaxCellY = ys.Last(),
            CenterCellX = centerX,
            CenterCellY = centerY,
            CenterCellHeight = centerHeight,
            DistanceFromOwnerAnchor = Math.Max(Math.Abs(ownerAnchor.X - centerX), Math.Abs(ownerAnchor.Y - centerY))
        };
    }

    private static BattleGridCoord ResolveMedianAnchor(IReadOnlyList<BattleRuntimeActor> actors)
    {
        int[] xs = actors.Select(item => item.GridX).OrderBy(item => item).ToArray();
        int[] ys = actors.Select(item => item.GridY).OrderBy(item => item).ToArray();
        int[] heights = actors.Select(item => item.GridHeight).OrderBy(item => item).ToArray();
        return new BattleGridCoord(xs[xs.Length / 2], ys[ys.Length / 2], heights[heights.Length / 2]);
    }

    private static int GetSquareDistance(BattleRuntimeActor first, BattleRuntimeActor second)
    {
        return Math.Max(Math.Abs((first?.GridX ?? 0) - (second?.GridX ?? 0)), Math.Abs((first?.GridY ?? 0) - (second?.GridY ?? 0)));
    }

    private static string NormalizeFaction(string factionId)
    {
        return string.IsNullOrWhiteSpace(factionId) ? "player" : factionId.Trim();
    }

    private static string Sanitize(string value)
    {
        return string.Join("_", (value ?? "").Split(new[] { ':', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
    }
}
