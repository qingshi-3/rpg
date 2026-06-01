using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle;

internal static class BattleTacticalObservationUpdater
{
    // Owns the Runtime tactical-observation mutation order while preserving
    // the existing stream append points.
    internal static BattleRuntimeActor[] RefreshAtTickStart(
        BattleRuntimeState state,
        BattleEventStream stream,
        string battleId,
        int tick,
        double currentTimeSeconds)
    {
        BattleRuntimeActor[] livingCorps = state.Actors
            .Where(item => item.Kind == BattleRuntimeActorKind.Corps && item.HitPoints > 0)
            .OrderBy(item => item.ActorId, System.StringComparer.Ordinal)
            .ToArray();

        // Perception summaries are observation facts; engagement mutation remains
        // centralized in the battle-group state machine below.
        state.GroupPerceptionSummaryStore = BattleGroupPerceptionSummaryBuilder.BuildForGroups(livingCorps, tick);
        IReadOnlyList<BattleEvent> engagementEvents = BattleGroupEngagementStateMachine.ApplyPerceptionTransitions(
            state.TacticalStateStore,
            state.GroupPerceptionSummaryStore,
            battleId,
            tick,
            currentTimeSeconds);
        BattleTargetLockLifecycle.ClearForEngagementExits(livingCorps, engagementEvents);
        foreach (BattleEvent engagementEvent in engagementEvents)
        {
            stream.Add(engagementEvent);
        }

        RefreshEngagedLocalCombatRegions(state, livingCorps, stream, tick);
        RefreshEnemyTemporaryTargetRegions(state, livingCorps, stream, tick);
        return livingCorps;
    }

    internal static void ApplyPostAttackEngagementTriggers(
        BattleRuntimeState state,
        IReadOnlyList<BattleEvent> attackEvents,
        BattleEventStream stream,
        string battleId,
        int tick,
        double currentTimeSeconds)
    {
        if (attackEvents.Count == 0 || state?.Actors == null)
        {
            return;
        }

        Dictionary<string, string> actorGroupIds = state.Actors
            .Where(item => !string.IsNullOrWhiteSpace(item.ActorId) &&
                           !string.IsNullOrWhiteSpace(item.BattleGroupId))
            .ToDictionary(
                item => item.ActorId,
                item => item.BattleGroupId,
                System.StringComparer.Ordinal);
        IReadOnlyList<BattleEvent> engagementEvents = BattleGroupEngagementStateMachine.ApplyMemberActionTransitions(
            state.TacticalStateStore,
            attackEvents,
            actorGroupIds,
            battleId,
            tick,
            currentTimeSeconds);
        foreach (BattleEvent engagementEvent in engagementEvents)
        {
            stream.Add(engagementEvent);
        }
    }

    private static void RefreshEngagedLocalCombatRegions(
        BattleRuntimeState state,
        BattleRuntimeActor[] livingCorps,
        BattleEventStream stream,
        int tick)
    {
        if (state?.TacticalStateStore == null || livingCorps == null || stream == null)
        {
            return;
        }

        foreach (BattleGroupTacticalState tacticalState in state.TacticalStates.Values
                     .Where(item => item.EngagementState == BattleGroupEngagementState.Engaged)
                     .OrderBy(item => item.BattleGroupId, System.StringComparer.Ordinal))
        {
            // Local combat regions are owned by group tactical state. AI and
            // navigation consume the stored snapshot rather than rebuilding
            // their own local truth at action-selection time.
            BattleLocalCombatRegionBuildResult build = BattleLocalCombatRegionBuilder.BuildForGroup(
                tacticalState.BattleGroupId,
                livingCorps,
                tick);
            if (build?.Region == null)
            {
                continue;
            }

            BattleGroupTacticalRegionMutationResult result = state.TacticalStateStore.TrySetLocalCombatRegion(
                tacticalState.BattleGroupId,
                build.Region);
            if (result.Event != null)
            {
                stream.Add(result.Event);
            }
        }
    }

    private static void RefreshEnemyTemporaryTargetRegions(
        BattleRuntimeState state,
        BattleRuntimeActor[] livingCorps,
        BattleEventStream stream,
        int tick)
    {
        if (state?.TacticalStateStore == null || livingCorps == null || stream == null)
        {
            return;
        }

        foreach (BattleGroupTacticalState tacticalState in state.TacticalStates.Values
                     .OrderBy(item => item.BattleGroupId, System.StringComparer.Ordinal))
        {
            if (!ShouldUseEnemyTemporaryRegion(tacticalState, livingCorps, tick))
            {
                continue;
            }

            BattleTacticalRegionSnapshot temporaryRegion = BattleTemporaryTargetRegionBuilder.BuildForGroup(
                tacticalState.BattleGroupId,
                livingCorps,
                tick);
            if (temporaryRegion == null)
            {
                continue;
            }

            BattleGroupTacticalRegionMutationResult result = state.TacticalStateStore.TrySetTemporaryRegion(
                tacticalState.BattleGroupId,
                temporaryRegion,
                tick);
            if (result.Event != null)
            {
                stream.Add(result.Event);
            }
        }
    }

    private static bool ShouldUseEnemyTemporaryRegion(
        BattleGroupTacticalState tacticalState,
        BattleRuntimeActor[] livingCorps,
        int tick)
    {
        if (tacticalState == null ||
            tacticalState.EngagementState != BattleGroupEngagementState.NotEngaged ||
            tacticalState.TacticalMode is not (BattleGroupTacticalMode.EnemyOffense or BattleGroupTacticalMode.EnemyActiveDefense))
        {
            return false;
        }

        BattleTacticalRegionSnapshot selected = tacticalState.SelectedRegion;
        if (selected == null)
        {
            return true;
        }

        if (selected.Kind == BattleTacticalRegionKind.FixedTarget)
        {
            return !RegionContainsOpposingActor(tacticalState.BattleGroupId, selected, livingCorps);
        }

        return selected.Kind == BattleTacticalRegionKind.TemporaryTarget &&
               tick - tacticalState.LastTemporaryRegionRefreshTick >= BattleGroupTacticalPolicySettings.DefaultTemporaryRegionRefreshTicks;
    }

    private static bool RegionContainsOpposingActor(
        string ownerBattleGroupId,
        BattleTacticalRegionSnapshot region,
        IEnumerable<BattleRuntimeActor> livingCorps)
    {
        string[] ownerFactions = (livingCorps ?? System.Array.Empty<BattleRuntimeActor>())
            .Where(item => string.Equals(item.BattleGroupId ?? "", ownerBattleGroupId ?? "", System.StringComparison.Ordinal))
            .Select(item => BattleRuntimeTickResolver.NormalizeFaction(item.FactionId))
            .Distinct(System.StringComparer.Ordinal)
            .ToArray();
        if (ownerFactions.Length == 0)
        {
            return false;
        }

        int width = System.Math.Max(1, region?.Width ?? 1);
        int height = System.Math.Max(1, region?.Height ?? 1);
        int minX = region?.CenterCellX ?? 0;
        int minY = region?.CenterCellY ?? 0;
        int maxXExclusive = minX + width;
        int maxYExclusive = minY + height;
        return (livingCorps ?? System.Array.Empty<BattleRuntimeActor>())
            .Any(actor => actor != null &&
                          actor.HitPoints > 0 &&
                          !ownerFactions.Contains(BattleRuntimeTickResolver.NormalizeFaction(actor.FactionId)) &&
                          actor.GridHeight == (region?.CenterCellHeight ?? 0) &&
                          actor.GridX >= minX &&
                          actor.GridX < maxXExclusive &&
                          actor.GridY >= minY &&
                          actor.GridY < maxYExclusive);
    }
}
