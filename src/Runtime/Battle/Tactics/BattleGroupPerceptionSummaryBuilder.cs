using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle.Tactics;

public static class BattleGroupPerceptionSummaryBuilder
{
    public static IReadOnlyDictionary<string, BattleGroupPerceptionSummary> BuildForGroups(
        IEnumerable<BattleRuntimeActor> actors,
        int runtimeTick,
        int perceptionRange = BattleGroupTacticalPolicySettings.DefaultLocalPerceptionRange)
    {
        BattleRuntimeActor[] aliveActors = (actors ?? Enumerable.Empty<BattleRuntimeActor>())
            .Where(actor => actor != null &&
                            actor.HitPoints > 0 &&
                            !string.IsNullOrWhiteSpace(actor.BattleGroupId))
            .OrderBy(actor => actor.BattleGroupId, StringComparer.Ordinal)
            .ThenBy(actor => actor.ActorId, StringComparer.Ordinal)
            .ToArray();

        Dictionary<string, BattleGroupPerceptionSummary> summaries = aliveActors
            .GroupBy(actor => actor.BattleGroupId ?? "", StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => BuildSummary(group.Key, group.ToArray(), aliveActors, runtimeTick, perceptionRange),
                StringComparer.Ordinal);

        return new ReadOnlyDictionary<string, BattleGroupPerceptionSummary>(summaries);
    }

    private static BattleGroupPerceptionSummary BuildSummary(
        string battleGroupId,
        BattleRuntimeActor[] members,
        IReadOnlyCollection<BattleRuntimeActor> aliveActors,
        int runtimeTick,
        int perceptionRange)
    {
        BattleGroupPerceptionMemberCoverage[] coverages = members
            .OrderBy(member => member.ActorId, StringComparer.Ordinal)
            .Select(member => BuildCoverage(member, aliveActors, perceptionRange))
            .ToArray();
        string[] perceivedHostiles = coverages
            .SelectMany(coverage => coverage.PerceivedHostileActorIds)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(actorId => actorId, StringComparer.Ordinal)
            .ToArray();

        return new BattleGroupPerceptionSummary
        {
            BattleGroupId = battleGroupId ?? "",
            FactionId = members.FirstOrDefault()?.FactionId ?? "",
            LastBuiltRuntimeTick = runtimeTick,
            MinAnchorCellX = members.Min(member => member.GridX),
            MaxAnchorCellX = members.Max(member => member.GridX),
            MinAnchorCellY = members.Min(member => member.GridY),
            MaxAnchorCellY = members.Max(member => member.GridY),
            MinAnchorCellHeight = members.Min(member => member.GridHeight),
            MaxAnchorCellHeight = members.Max(member => member.GridHeight),
            PerceivedHostileActorIds = perceivedHostiles,
            MemberCoverages = coverages
        };
    }

    private static BattleGroupPerceptionMemberCoverage BuildCoverage(
        BattleRuntimeActor member,
        IEnumerable<BattleRuntimeActor> aliveActors,
        int perceptionRange)
    {
        string[] hostileIds = (aliveActors ?? Enumerable.Empty<BattleRuntimeActor>())
            .Where(actor => actor != null &&
                            !string.Equals(actor.ActorId ?? "", member.ActorId ?? "", StringComparison.Ordinal) &&
                            !SameFaction(actor.FactionId, member.FactionId) &&
                            IsPerceived(member, actor, perceptionRange))
            .Select(actor => actor.ActorId ?? "")
            .Where(actorId => !string.IsNullOrWhiteSpace(actorId))
            .OrderBy(actorId => actorId, StringComparer.Ordinal)
            .ToArray();

        return new BattleGroupPerceptionMemberCoverage
        {
            ActorId = member.ActorId ?? "",
            AnchorCellX = member.GridX,
            AnchorCellY = member.GridY,
            AnchorCellHeight = member.GridHeight,
            PerceivedHostileActorIds = hostileIds
        };
    }

    private static bool IsPerceived(BattleRuntimeActor member, BattleRuntimeActor hostile, int perceptionRange)
    {
        int normalizedRange = Math.Max(0, perceptionRange);
        int gridGap = BattleActorFootprint.GetGap(
            member,
            new BattleGridCoord(member.GridX, member.GridY, member.GridHeight),
            hostile,
            new BattleGridCoord(hostile.GridX, hostile.GridY, hostile.GridHeight));
        int heightGap = Math.Abs(member.GridHeight - hostile.GridHeight);
        return gridGap + heightGap <= normalizedRange;
    }

    private static bool SameFaction(string first, string second)
    {
        return string.Equals(NormalizeFaction(first), NormalizeFaction(second), StringComparison.Ordinal);
    }

    private static string NormalizeFaction(string factionId)
    {
        return string.IsNullOrWhiteSpace(factionId) ? "player" : factionId.Trim();
    }
}
