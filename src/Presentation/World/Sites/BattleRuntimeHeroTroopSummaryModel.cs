using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Presentation.Battle.Entities;
using Rpg.Runtime.Battle;

namespace Rpg.Presentation.World.Sites;

internal sealed class BattleRuntimeHeroTroopSummaryView
{
    public string GroupKey { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public int HeroHpCurrent { get; init; }
    public int HeroHpMax { get; init; } = 1;
    public int RemainingTroopCount { get; init; }
    public int TotalTroopCount { get; init; }
    public int TroopHpCurrent { get; init; }
    public int TroopHpMax { get; init; } = 1;
    public string SoldierCountText => $"{RemainingTroopCount}/{TotalTroopCount}";
}

internal static class BattleRuntimeHeroTroopSummaryModel
{
    internal static IReadOnlyList<BattleRuntimeHeroTroopSummaryView> Build(
        IReadOnlyList<BattleRuntimeCommandGroupView> groups,
        BattleRuntimeState state,
        Func<BattleRuntimeCommandGroupView, BattleEntity> resolveHeroEntity)
    {
        return (groups ?? Array.Empty<BattleRuntimeCommandGroupView>())
            .Where(group => group != null && !string.IsNullOrWhiteSpace(group.GroupKey))
            .Select(group => BuildGroupSummary(group, state, resolveHeroEntity?.Invoke(group)))
            .ToArray();
    }

    private static BattleRuntimeHeroTroopSummaryView BuildGroupSummary(
        BattleRuntimeCommandGroupView group,
        BattleRuntimeState state,
        BattleEntity heroEntity)
    {
        HealthComponent heroHealth = heroEntity?.GetComponent<HealthComponent>();
        int heroHpMax = System.Math.Max(1, heroHealth?.MaxHp ?? 1);
        int heroHpCurrent = System.Math.Clamp(heroHealth?.Hp ?? heroHpMax, 0, heroHpMax);

        IReadOnlyList<BattleRuntimeActor> corpsActors = ResolveCorpsActors(group, state);
        int totalTroopCount = corpsActors.Count > 0
            ? corpsActors.Count
            : ResolveConfiguredTroopCount(group);
        int remainingTroopCount = corpsActors.Count > 0
            ? corpsActors.Count(actor => System.Math.Max(0, actor.HitPoints) > 0)
            : totalTroopCount;
        int troopHpCurrent = corpsActors.Count > 0
            ? corpsActors.Sum(actor => System.Math.Max(0, actor.HitPoints))
            : ResolveConfiguredTroopHp(group);
        int troopHpMax = corpsActors.Count > 0
            ? corpsActors.Sum(actor => ResolveMaxHitPoints(actor, group))
            : System.Math.Max(1, ResolveConfiguredTroopHp(group));

        return new BattleRuntimeHeroTroopSummaryView
        {
            GroupKey = group.GroupKey ?? "",
            DisplayName = string.IsNullOrWhiteSpace(group.DisplayName) ? group.GroupKey ?? "" : group.DisplayName.Trim(),
            HeroHpCurrent = heroHpCurrent,
            HeroHpMax = heroHpMax,
            RemainingTroopCount = System.Math.Clamp(remainingTroopCount, 0, System.Math.Max(0, totalTroopCount)),
            TotalTroopCount = System.Math.Max(0, totalTroopCount),
            TroopHpCurrent = System.Math.Clamp(troopHpCurrent, 0, System.Math.Max(1, troopHpMax)),
            TroopHpMax = System.Math.Max(1, troopHpMax)
        };
    }

    private static IReadOnlyList<BattleRuntimeActor> ResolveCorpsActors(
        BattleRuntimeCommandGroupView group,
        BattleRuntimeState state)
    {
        if (state?.Actors == null)
        {
            return Array.Empty<BattleRuntimeActor>();
        }

        return state.Actors
            .Where(actor =>
                actor != null &&
                actor.Kind == BattleRuntimeActorKind.Corps &&
                BelongsToGroup(actor, group))
            .ToArray();
    }

    private static bool BelongsToGroup(BattleRuntimeActor actor, BattleRuntimeCommandGroupView group)
    {
        string groupKey = group?.GroupKey ?? "";
        if (string.Equals(actor?.BattleGroupId ?? "", groupKey, StringComparison.Ordinal))
        {
            return true;
        }

        return (group?.Forces ?? Array.Empty<BattleForceRequest>())
            .Any(force => MatchesForce(actor, force));
    }

    private static int ResolveMaxHitPoints(BattleRuntimeActor actor, BattleRuntimeCommandGroupView group)
    {
        BattleForceRequest matchedForce = (group?.Forces ?? Array.Empty<BattleForceRequest>())
            .Where(force => force != null && !BattleRuntimeCommandHudModel.IsLikelyHeroForce(force))
            .FirstOrDefault(force => MatchesForceByUnit(actor, force))
            ?? (group?.Forces ?? Array.Empty<BattleForceRequest>())
                .Where(force => force != null && !BattleRuntimeCommandHudModel.IsLikelyHeroForce(force))
                .FirstOrDefault(force => MatchesForceBySource(actor, force));

        int configured = matchedForce?.MaxHitPoints ?? 0;
        if (configured > 0)
        {
            return configured;
        }

        return System.Math.Max(1, actor?.HitPoints ?? 1);
    }

    private static bool MatchesForce(BattleRuntimeActor actor, BattleForceRequest force) =>
        MatchesForceByUnit(actor, force) || MatchesForceBySource(actor, force);

    private static bool MatchesForceByUnit(BattleRuntimeActor actor, BattleForceRequest force)
    {
        string actorUnitId = actor?.UnitDefinitionId ?? "";
        if (string.IsNullOrWhiteSpace(actorUnitId))
        {
            return false;
        }

        string[] candidateUnitIds =
        {
            force?.StrategicCorpsBattleUnitId,
            force?.StrategicCorpsDefinitionId,
            force?.UnitDefinitionId
        };
        return candidateUnitIds.Any(id => string.Equals(actorUnitId, id ?? "", StringComparison.Ordinal));
    }

    private static bool MatchesForceBySource(BattleRuntimeActor actor, BattleForceRequest force)
    {
        string actorSourceForceId = actor?.SourceForceId ?? "";
        if (string.IsNullOrWhiteSpace(actorSourceForceId))
        {
            return false;
        }

        return string.Equals(actorSourceForceId, force?.ForceId ?? "", StringComparison.Ordinal) ||
               string.Equals(actorSourceForceId, force?.StrategicParticipantId ?? "", StringComparison.Ordinal) ||
               string.Equals(actorSourceForceId, force?.UnitDefinitionId ?? "", StringComparison.Ordinal);
    }

    private static int ResolveConfiguredTroopCount(BattleRuntimeCommandGroupView group)
    {
        return (group?.Forces ?? Array.Empty<BattleForceRequest>())
            .Where(force => force != null && !BattleRuntimeCommandHudModel.IsLikelyHeroForce(force))
            .Sum(force => System.Math.Max(0, force.Count));
    }

    private static int ResolveConfiguredTroopHp(BattleRuntimeCommandGroupView group)
    {
        return (group?.Forces ?? Array.Empty<BattleForceRequest>())
            .Where(force => force != null && !BattleRuntimeCommandHudModel.IsLikelyHeroForce(force))
            .Sum(force => System.Math.Max(0, force.Count) * System.Math.Max(1, force.MaxHitPoints));
    }
}
