using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle.Tactics;

internal static class BattleCombatZoneBuilder
{
    private const int HotAreaPadding = 3;
    private const string RebuildReason = "combat_zone_rebuilt";

    internal static IReadOnlyDictionary<string, BattleCombatZoneSnapshot> Build(
        IEnumerable<BattleRuntimeActor> livingCorps,
        int runtimeTick)
    {
        BattleRuntimeActor[] actors = (livingCorps ?? Enumerable.Empty<BattleRuntimeActor>())
            .Where(item => item?.Kind == BattleRuntimeActorKind.Corps && item.HitPoints > 0)
            .OrderBy(item => item.ActorId, StringComparer.Ordinal)
            .ToArray();
        if (actors.Length < 2)
        {
            return new ReadOnlyDictionary<string, BattleCombatZoneSnapshot>(
                new Dictionary<string, BattleCombatZoneSnapshot>(StringComparer.Ordinal));
        }

        DisjointSet components = new(actors.Length);
        bool[,] hostileLinks = new bool[actors.Length, actors.Length];
        for (int i = 0; i < actors.Length; i++)
        {
            for (int j = i + 1; j < actors.Length; j++)
            {
                BattleRuntimeActor first = actors[i];
                BattleRuntimeActor second = actors[j];
                if (first.GridHeight != second.GridHeight)
                {
                    continue;
                }

                bool sameGroup = string.Equals(first.BattleGroupId ?? "", second.BattleGroupId ?? "", StringComparison.Ordinal);
                bool sameFaction = BattleRuntimeTickResolver.SameFaction(first, second);
                bool targetLinked = IsTargetLinked(first, second);
                int gap = BattleActorFootprint.GetGap(first, second);
                bool closeEnough = gap <= BattleGroupTacticalPolicySettings.DefaultLocalPerceptionRange;
                if (!sameFaction && (closeEnough || targetLinked))
                {
                    components.Union(i, j);
                    hostileLinks[i, j] = true;
                    hostileLinks[j, i] = true;
                    continue;
                }

                if (sameGroup && closeEnough)
                {
                    components.Union(i, j);
                }
            }
        }

        List<BattleCombatZoneSnapshot> zones = components.Groups()
            .Select(group => group.Select(index => actors[index]).ToArray())
            .Where(group => group.Select(item => NormalizeFaction(item.FactionId)).Distinct(StringComparer.Ordinal).Count() > 1)
            .Where(group => HasHostileLink(group, actors, hostileLinks))
            .Select((group, index) => BuildZone(group, index + 1, runtimeTick))
            .OrderBy(item => item.MinCellX)
            .ThenBy(item => item.MinCellY)
            .ThenBy(item => item.CombatZoneId, StringComparer.Ordinal)
            .ToList();

        return new ReadOnlyDictionary<string, BattleCombatZoneSnapshot>(
            zones.ToDictionary(item => item.CombatZoneId, item => item, StringComparer.Ordinal));
    }

    private static BattleCombatZoneSnapshot BuildZone(
        IReadOnlyList<BattleRuntimeActor> actors,
        int index,
        int runtimeTick)
    {
        // Combat-zone bounds are battlefield facts: they must preserve every
        // participant footprint plus join space. Slot/path search owns its own
        // performance budget and must not clip these fact bounds.
        int minX = actors.Min(item => item.GridX) - HotAreaPadding;
        int minY = actors.Min(item => item.GridY) - HotAreaPadding;
        int maxX = actors.Max(item => item.GridX + BattleActorFootprint.NormalizeSize(item.FootprintWidth) - 1) + HotAreaPadding;
        int maxY = actors.Max(item => item.GridY + BattleActorFootprint.NormalizeSize(item.FootprintHeight) - 1) + HotAreaPadding;
        int centerX = minX + (maxX - minX) / 2;
        int centerY = minY + (maxY - minY) / 2;
        int[] heights = actors.Select(item => item.GridHeight).OrderBy(item => item).ToArray();
        return new BattleCombatZoneSnapshot
        {
            CombatZoneId = $"combat_zone_{index}",
            OwnerBattleGroupId = "",
            ReasonCode = RebuildReason,
            Version = runtimeTick + 1,
            LastBuiltRuntimeTick = runtimeTick,
            MinCellX = minX,
            MinCellY = minY,
            MaxCellX = maxX,
            MaxCellY = maxY,
            CenterCellX = centerX,
            CenterCellY = centerY,
            CenterCellHeight = heights[heights.Length / 2],
            ActorIds = actors
                .Select(item => item.ActorId ?? "")
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
            BattleGroupIds = actors
                .Select(item => item.BattleGroupId ?? "")
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
            FactionIds = actors
                .Select(item => NormalizeFaction(item.FactionId))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray()
        };
    }

    private static bool HasHostileLink(
        IReadOnlyList<BattleRuntimeActor> group,
        IReadOnlyList<BattleRuntimeActor> allActors,
        bool[,] hostileLinks)
    {
        HashSet<string> actorIds = group.Select(item => item.ActorId ?? "").ToHashSet(StringComparer.Ordinal);
        for (int i = 0; i < allActors.Count; i++)
        {
            if (!actorIds.Contains(allActors[i].ActorId ?? ""))
            {
                continue;
            }

            for (int j = 0; j < allActors.Count; j++)
            {
                if (hostileLinks[i, j] && actorIds.Contains(allActors[j].ActorId ?? ""))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsTargetLinked(BattleRuntimeActor first, BattleRuntimeActor second)
    {
        return string.Equals(first?.TargetActorId ?? "", second?.ActorId ?? "", StringComparison.Ordinal) ||
               string.Equals(second?.TargetActorId ?? "", first?.ActorId ?? "", StringComparison.Ordinal);
    }

    private static string NormalizeFaction(string factionId)
    {
        return string.IsNullOrWhiteSpace(factionId) ? "player" : factionId.Trim();
    }

    private sealed class DisjointSet
    {
        private readonly int[] _parents;

        internal DisjointSet(int count)
        {
            _parents = Enumerable.Range(0, count).ToArray();
        }

        internal void Union(int first, int second)
        {
            int firstRoot = Find(first);
            int secondRoot = Find(second);
            if (firstRoot != secondRoot)
            {
                _parents[secondRoot] = firstRoot;
            }
        }

        internal IReadOnlyList<IReadOnlyList<int>> Groups()
        {
            return Enumerable.Range(0, _parents.Length)
                .GroupBy(Find)
                .Select(group => (IReadOnlyList<int>)group.OrderBy(item => item).ToArray())
                .ToArray();
        }

        private int Find(int value)
        {
            if (_parents[value] != value)
            {
                _parents[value] = Find(_parents[value]);
            }

            return _parents[value];
        }
    }
}
