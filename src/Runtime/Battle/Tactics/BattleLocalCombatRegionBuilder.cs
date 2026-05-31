using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Snapshots;

namespace Rpg.Runtime.Battle.Tactics;

public sealed class BattleLocalCombatRegionBuildResult
{
    public BattleTacticalRegionSnapshot Region { get; init; }
    public int PerceptionCoverageScore { get; init; }
    public int PerceivedHostileCount { get; init; }
}

public static class BattleLocalCombatRegionBuilder
{
    private readonly record struct Candidate(int CenterX, int CenterY, int Height, int Score, int Hostiles, int DistanceToCentroid);

    public static BattleLocalCombatRegionBuildResult BuildForGroup(
        string ownerBattleGroupId,
        IEnumerable<BattleRuntimeActor> livingCorps,
        int runtimeTick)
    {
        BattleRuntimeActor[] alive = (livingCorps ?? Enumerable.Empty<BattleRuntimeActor>())
            .Where(item => item?.Kind == BattleRuntimeActorKind.Corps && item.HitPoints > 0)
            .OrderBy(item => item.ActorId, StringComparer.Ordinal)
            .ToArray();
        BattleRuntimeActor[] members = alive
            .Where(item => string.Equals(item.BattleGroupId ?? "", ownerBattleGroupId ?? "", StringComparison.Ordinal))
            .ToArray();
        if (members.Length == 0)
        {
            return null;
        }

        string[] ownerFactions = members
            .Select(item => NormalizeFaction(item.FactionId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        BattleRuntimeActor[] perceivedHostiles = alive
            .Where(actor => !ownerFactions.Contains(NormalizeFaction(actor.FactionId)) &&
                            members.Any(member => IsPerceived(member, actor)))
            .OrderBy(actor => actor.ActorId, StringComparer.Ordinal)
            .ToArray();
        if (perceivedHostiles.Length == 0)
        {
            return null;
        }

        Dictionary<string, (int X, int Y, int Height, int Weight)> coverage = BuildCoverage(members);
        int width = Math.Min(
            BattleGroupTacticalPolicySettings.DefaultLocalPerceptionRange * 2 + 1,
            (int)Math.Floor(Math.Sqrt(BattleGroupTacticalPolicySettings.DefaultLocalCombatMaxCells)));
        int height = Math.Max(1, BattleGroupTacticalPolicySettings.DefaultLocalCombatMaxCells / Math.Max(1, width));
        Candidate selected = BuildCandidates(members, perceivedHostiles, coverage, width, height)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.DistanceToCentroid)
            .ThenBy(item => item.CenterX)
            .ThenBy(item => item.CenterY)
            .First();

        return new BattleLocalCombatRegionBuildResult
        {
            Region = new BattleTacticalRegionSnapshot
            {
                RegionId = $"{ownerBattleGroupId}:local:{selected.CenterX}:{selected.CenterY}:tick_{runtimeTick}",
                OwnerBattleGroupId = ownerBattleGroupId ?? "",
                Kind = BattleTacticalRegionKind.LocalCombat,
                SourceRegionId = string.Join(",", perceivedHostiles.Select(item => item.ActorId ?? "")),
                ReasonCode = HasOverlapInside(coverage.Values, selected.CenterX, selected.CenterY, width, height)
                    ? BattleGroupTacticalReasonCode.LocalRegionBuiltPerceptionOverlap
                    : "local_region_built_perception_single",
                CenterCellX = selected.CenterX,
                CenterCellY = selected.CenterY,
                CenterCellHeight = selected.Height,
                Width = width,
                Height = height
            },
            PerceptionCoverageScore = selected.Score,
            PerceivedHostileCount = selected.Hostiles
        };
    }

    private static IEnumerable<Candidate> BuildCandidates(
        IReadOnlyList<BattleRuntimeActor> members,
        IReadOnlyList<BattleRuntimeActor> perceivedHostiles,
        IReadOnlyDictionary<string, (int X, int Y, int Height, int Weight)> coverage,
        int width,
        int height)
    {
        double centroidX = members.Average(item => item.GridX);
        double centroidY = members.Average(item => item.GridY);
        (int X, int Y, int Height)[] centers = members
            .Select(item => (item.GridX, item.GridY, item.GridHeight))
            .Concat(perceivedHostiles.Select(item => (item.GridX, item.GridY, item.GridHeight)))
            .Concat(coverage.Values
                .OrderByDescending(item => item.Weight)
                .ThenBy(item => item.X)
                .ThenBy(item => item.Y)
                .Take(16)
                .Select(item => (item.X, item.Y, item.Height)))
            .Distinct()
            .ToArray();

        foreach ((int x, int y, int cellHeight) in centers)
        {
            int coverageScore = coverage.Values
                .Where(item => IsInside(item.X, item.Y, x, y, width, height))
                .Sum(item => item.Weight);
            int hostileCount = perceivedHostiles.Count(item => IsInside(item.GridX, item.GridY, x, y, width, height));
            int overlapScore = coverage.Values.Count(item => item.Weight > 1 && IsInside(item.X, item.Y, x, y, width, height));
            int distanceToCentroid = (int)Math.Round(Math.Abs(x - centroidX) + Math.Abs(y - centroidY));
            yield return new Candidate(
                x,
                y,
                cellHeight,
                coverageScore + hostileCount * 100 + overlapScore * 10,
                hostileCount,
                distanceToCentroid);
        }
    }

    private static Dictionary<string, (int X, int Y, int Height, int Weight)> BuildCoverage(IReadOnlyList<BattleRuntimeActor> members)
    {
        Dictionary<string, (int X, int Y, int Height, int Weight)> coverage = new(StringComparer.Ordinal);
        int range = BattleGroupTacticalPolicySettings.DefaultLocalPerceptionRange;
        foreach (BattleRuntimeActor member in members)
        {
            for (int y = member.GridY - range; y <= member.GridY + range; y++)
            {
                for (int x = member.GridX - range; x <= member.GridX + range; x++)
                {
                    if (Math.Max(Math.Abs(x - member.GridX), Math.Abs(y - member.GridY)) > range)
                    {
                        continue;
                    }

                    string key = $"{x}:{y}:{member.GridHeight}";
                    coverage.TryGetValue(key, out (int X, int Y, int Height, int Weight) current);
                    coverage[key] = (x, y, member.GridHeight, current.Weight + 1);
                }
            }
        }

        return coverage;
    }

    private static bool HasOverlapInside(
        IEnumerable<(int X, int Y, int Height, int Weight)> coverage,
        int centerX,
        int centerY,
        int width,
        int height)
    {
        return coverage.Any(item => item.Weight > 1 && IsInside(item.X, item.Y, centerX, centerY, width, height));
    }

    private static bool IsPerceived(BattleRuntimeActor member, BattleRuntimeActor hostile)
    {
        int range = BattleGroupTacticalPolicySettings.DefaultLocalPerceptionRange;
        int gridGap = Math.Max(Math.Abs((member?.GridX ?? 0) - (hostile?.GridX ?? 0)), Math.Abs((member?.GridY ?? 0) - (hostile?.GridY ?? 0)));
        int heightGap = Math.Abs((member?.GridHeight ?? 0) - (hostile?.GridHeight ?? 0));
        return gridGap + heightGap <= range;
    }

    private static bool IsInside(int x, int y, int centerX, int centerY, int width, int height)
    {
        int minX = centerX - (width - 1) / 2;
        int minY = centerY - (height - 1) / 2;
        return x >= minX && x < minX + width && y >= minY && y < minY + height;
    }

    private static string NormalizeFaction(string factionId)
    {
        return string.IsNullOrWhiteSpace(factionId) ? "player" : factionId.Trim();
    }
}
