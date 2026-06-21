using System.Collections.Generic;
using System.Linq;
using System.Text;
using Rpg.Infrastructure.Logging;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Navigation;
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

        string previousCombatSignature = BuildCombatZoneSignature(state.CombatZoneStore);
        state.CombatZoneStore = BattleCombatZoneBuilder.Build(livingCorps, tick);
        bool combatChanged = !string.Equals(
            previousCombatSignature,
            BuildCombatZoneSignature(state.CombatZoneStore),
            System.StringComparison.Ordinal);

        // Perception summaries and combat zones are observation facts; engagement
        // mutation remains centralized in the battle-group state machine below.
        state.GroupPerceptionSummaryStore = BattleGroupPerceptionSummaryBuilder.BuildForGroups(livingCorps, tick);
        IReadOnlySet<string> groupsWithActiveCombatActions = BuildGroupsWithActiveCombatActions(livingCorps);
        IReadOnlyList<BattleEvent> engagementEvents = BattleGroupEngagementStateMachine.ApplyPerceptionTransitions(
            state.TacticalStateStore,
            state.GroupPerceptionSummaryStore,
            state.CombatZones,
            groupsWithActiveCombatActions,
            battleId,
            tick,
            currentTimeSeconds);
        BattleTargetLockLifecycle.ClearForEngagementExits(livingCorps, engagementEvents);
        foreach (BattleEvent engagementEvent in engagementEvents)
        {
            LogEngagementTransition(engagementEvent);
            stream.Add(engagementEvent);
        }

        RefreshSelectedRegionLifecycles(state, livingCorps);
        RefreshGroupActionZonesAndLog(state, livingCorps, tick, combatChanged);
        RefreshEngagedLocalCombatRegions(state, livingCorps, stream, tick);
        RefreshTemporaryTargetRegions(state, livingCorps, stream, tick);
        return livingCorps;
    }

    private static IReadOnlySet<string> BuildGroupsWithActiveCombatActions(IEnumerable<BattleRuntimeActor> livingCorps)
    {
        return (livingCorps ?? System.Array.Empty<BattleRuntimeActor>())
            .Where(item => item != null &&
                           item.HitPoints > 0 &&
                           !string.IsNullOrWhiteSpace(item.BattleGroupId) &&
                           HasActiveCombatAction(item))
            .Select(item => item.BattleGroupId ?? "")
            .ToHashSet(System.StringComparer.Ordinal);
    }

    private static bool HasActiveCombatAction(BattleRuntimeActor actor)
    {
        if (actor == null)
        {
            return false;
        }

        bool hasCombatTarget = !string.IsNullOrWhiteSpace(actor.TargetActorId) ||
                               !string.IsNullOrWhiteSpace(actor.MovementIntentTargetActorId);
        if (!hasCombatTarget)
        {
            return actor.Phase is BattleRuntimeActorPhase.SkillCasting or BattleRuntimeActorPhase.SkillRecovery;
        }

        return actor.Phase is BattleRuntimeActorPhase.Moving
            or BattleRuntimeActorPhase.AttackWindup
            or BattleRuntimeActorPhase.AttackRecovery
            or BattleRuntimeActorPhase.SkillCasting
            or BattleRuntimeActorPhase.SkillRecovery;
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

        Dictionary<string, string> actorGroupIds = BuildActorGroupIdMap(state.Actors);
        IReadOnlyList<BattleEvent> engagementEvents = BattleGroupEngagementStateMachine.ApplyMemberActionTransitions(
            state.TacticalStateStore,
            attackEvents,
            actorGroupIds,
            battleId,
            tick,
            currentTimeSeconds);
        foreach (BattleEvent engagementEvent in engagementEvents)
        {
            LogEngagementTransition(engagementEvent);
            stream.Add(engagementEvent);
        }
    }

    private static Dictionary<string, string> BuildActorGroupIdMap(IEnumerable<BattleRuntimeActor> actors)
    {
        Dictionary<string, string> actorGroupIds = new(System.StringComparer.Ordinal);
        foreach (BattleRuntimeActor actor in actors ?? System.Array.Empty<BattleRuntimeActor>())
        {
            string actorId = actor?.ActorId ?? "";
            string battleGroupId = actor?.BattleGroupId ?? "";
            if (string.IsNullOrWhiteSpace(actorId) || string.IsNullOrWhiteSpace(battleGroupId))
            {
                continue;
            }

            if (!actorGroupIds.TryAdd(actorId, battleGroupId))
            {
                throw new System.InvalidOperationException($"duplicate runtime actor id in tactical observation: actorId={actorId}");
            }
        }

        return actorGroupIds;
    }

    private static void LogEngagementTransition(BattleEvent engagementEvent)
    {
        if (engagementEvent == null)
        {
            return;
        }

        GameLog.Info(
            "BattleRuntimeStateTransition",
            $"BattleRuntimeStateTransition battle={engagementEvent.BattleId ?? ""} tick={engagementEvent.RuntimeTick} time={engagementEvent.RuntimeTimeSeconds:0.00} group={engagementEvent.BattleGroupId ?? ""} state=EngagementChanged reason={engagementEvent.ReasonCode ?? ""} actor={engagementEvent.ActorId ?? ""} target={engagementEvent.TargetId ?? ""}");
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

    private static void RefreshSelectedRegionLifecycles(
        BattleRuntimeState state,
        BattleRuntimeActor[] livingCorps)
    {
        if (state?.TacticalStateStore == null || livingCorps == null)
        {
            return;
        }

        foreach (BattleGroupTacticalState tacticalState in state.TacticalStates.Values
                     .OrderBy(item => item.BattleGroupId, System.StringComparer.Ordinal))
        {
            BattleTacticalRegionSnapshot selected = tacticalState.SelectedRegion;
            if (selected == null)
            {
                continue;
            }

            if (tacticalState.SelectedRegionCommandSource == BattleGroupTacticalCommandSource.SelfCalculated &&
                tacticalState.EngagementState == BattleGroupEngagementState.Engaged)
            {
                state.TacticalStateStore.TryClearSelectedRegion(
                    tacticalState.BattleGroupId,
                    BattleGroupTacticalCommandSource.SelfCalculated);
                continue;
            }

            bool commandOwnedRegion = tacticalState.SelectedRegionCommandSource is
                BattleGroupTacticalCommandSource.PlayerCommand or
                BattleGroupTacticalCommandSource.SelfCalculated;
            if (!commandOwnedRegion ||
                tacticalState.EngagementState != BattleGroupEngagementState.NotEngaged ||
                RegionContainsOpposingActor(tacticalState.BattleGroupId, selected, livingCorps) ||
                !GroupHasReachedRegion(tacticalState.BattleGroupId, selected, livingCorps))
            {
                continue;
            }

            state.TacticalStateStore.TryClearSelectedRegion(
                tacticalState.BattleGroupId,
                tacticalState.SelectedRegionCommandSource);
        }
    }

    private static void RefreshTemporaryTargetRegions(
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
                if (!ShouldUsePlayerAutonomousTemporaryRegion(tacticalState, tick))
                {
                    continue;
                }

                BattleTacticalRegionSnapshot autonomousRegion = BattleTemporaryTargetRegionBuilder.BuildForGroup(
                    tacticalState.BattleGroupId,
                    livingCorps,
                    tick);
                if (autonomousRegion == null)
                {
                    continue;
                }

                autonomousRegion.ReasonCode = BattleGroupTacticalReasonCode.PlayerAutonomousTemporaryRegionCreatedCluster;
                BattleGroupTacticalRegionMutationResult autonomousResult = state.TacticalStateStore.TrySetPlayerAutonomousTemporaryRegion(
                    tacticalState.BattleGroupId,
                    autonomousRegion,
                    tick);
                if (autonomousResult.Event != null)
                {
                    stream.Add(autonomousResult.Event);
                }

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

        if (!BattleTacticalIntentPolicy.AllowsVolatileObservationRetarget(tacticalState))
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

    private static bool ShouldUsePlayerAutonomousTemporaryRegion(
        BattleGroupTacticalState tacticalState,
        int tick)
    {
        if (tacticalState == null ||
            tacticalState.EngagementState != BattleGroupEngagementState.NotEngaged ||
            tacticalState.TacticalMode != BattleGroupTacticalMode.PlayerCommanded ||
            !tacticalState.AllowAutonomousFallbackTargeting)
        {
            return false;
        }

        BattleTacticalRegionSnapshot selected = tacticalState.SelectedRegion;
        if (selected == null)
        {
            return true;
        }

        return tacticalState.SelectedRegionCommandSource == BattleGroupTacticalCommandSource.SelfCalculated &&
               selected.Kind == BattleTacticalRegionKind.TemporaryTarget &&
               tick - tacticalState.LastTemporaryRegionRefreshTick >= BattleGroupTacticalPolicySettings.DefaultTemporaryRegionRefreshTicks;
    }

    private static bool GroupHasReachedRegion(
        string ownerBattleGroupId,
        BattleTacticalRegionSnapshot region,
        IEnumerable<BattleRuntimeActor> livingCorps)
    {
        BattleRuntimeActor[] members = (livingCorps ?? System.Array.Empty<BattleRuntimeActor>())
            .Where(item => string.Equals(item.BattleGroupId ?? "", ownerBattleGroupId ?? "", System.StringComparison.Ordinal))
            .ToArray();
        if (members.Length == 0 || region == null)
        {
            return false;
        }

        int width = System.Math.Max(1, region.Width);
        int height = System.Math.Max(1, region.Height);
        var regionAnchor = new BattleGridCoord(
            region.CenterCellX - (width - 1) / 2,
            region.CenterCellY - (height - 1) / 2,
            region.CenterCellHeight);
        var regionActor = new BattleRuntimeActor
        {
            GridX = regionAnchor.X,
            GridY = regionAnchor.Y,
            GridHeight = regionAnchor.Height,
            FootprintWidth = width,
            FootprintHeight = height
        };

        return members.All(actor =>
            BattleActorFootprint.GetGap(actor, new BattleGridCoord(actor.GridX, actor.GridY, actor.GridHeight), regionActor, regionAnchor) <= 1);
    }

    private static bool RegionContainsOpposingActor(
        string ownerBattleGroupId,
        BattleTacticalRegionSnapshot region,
        IEnumerable<BattleRuntimeActor> livingCorps)
    {
        string[] ownerFactions = (livingCorps ?? System.Array.Empty<BattleRuntimeActor>())
            .Where(item => string.Equals(item.BattleGroupId ?? "", ownerBattleGroupId ?? "", System.StringComparison.Ordinal))
            .Select(item => BattleRuntimeIdentityRules.NormalizeFaction(item.FactionId))
            .Distinct(System.StringComparer.Ordinal)
            .ToArray();
        if (ownerFactions.Length == 0)
        {
            return false;
        }

        int width = System.Math.Max(1, region?.Width ?? 1);
        int height = System.Math.Max(1, region?.Height ?? 1);
        int minX = (region?.CenterCellX ?? 0) - (width - 1) / 2;
        int minY = (region?.CenterCellY ?? 0) - (height - 1) / 2;
        int maxXExclusive = minX + width;
        int maxYExclusive = minY + height;
        return (livingCorps ?? System.Array.Empty<BattleRuntimeActor>())
            .Any(actor => actor != null &&
                          actor.HitPoints > 0 &&
                          !ownerFactions.Contains(BattleRuntimeIdentityRules.NormalizeFaction(actor.FactionId)) &&
                          actor.GridHeight == (region?.CenterCellHeight ?? 0) &&
                          actor.GridX >= minX &&
                          actor.GridX < maxXExclusive &&
                          actor.GridY >= minY &&
                          actor.GridY < maxYExclusive);
    }

    private static void RefreshGroupActionZonesAndLog(
        BattleRuntimeState state,
        BattleRuntimeActor[] livingCorps,
        int tick,
        bool combatChanged)
    {
        if (state == null)
        {
            return;
        }

        string previousActionSignature = BuildGroupActionZoneSignature(state.GroupActionZoneStore);
        state.GroupActionZoneStore = BattleGroupActionZoneBuilder.Build(
            state.TacticalStates,
            livingCorps,
            state.CombatZones,
            tick);
        bool actionChanged = !string.Equals(
            previousActionSignature,
            BuildGroupActionZoneSignature(state.GroupActionZoneStore),
            System.StringComparison.Ordinal);

        if (combatChanged)
        {
            BattleTacticalAreaDiagnosticLogger.LogAreaSnapshot(state, livingCorps, tick, "combat_zone_rebuilt");
        }

        if (actionChanged)
        {
            BattleTacticalAreaDiagnosticLogger.LogAreaSnapshot(state, livingCorps, tick, "group_action_zone_rebuilt");
        }
    }

    private static string BuildCombatZoneSignature(IReadOnlyDictionary<string, BattleCombatZoneSnapshot> zones)
    {
        StringBuilder builder = new();
        foreach (BattleCombatZoneSnapshot zone in (zones?.Values ?? System.Array.Empty<BattleCombatZoneSnapshot>())
                     .OrderBy(item => item.CombatZoneId, System.StringComparer.Ordinal))
        {
            builder
                .Append(zone.CombatZoneId).Append(':')
                .Append(zone.HasCloseHostileContact).Append(':')
                .Append(zone.MinCellX).Append(',')
                .Append(zone.MinCellY).Append(',')
                .Append(zone.MaxCellX).Append(',')
                .Append(zone.MaxCellY).Append(':')
                .Append(string.Join(",", zone.ActorIds)).Append('|');
        }

        return builder.ToString();
    }

    private static string BuildGroupActionZoneSignature(IReadOnlyDictionary<string, BattleGroupActionZoneSnapshot> zones)
    {
        StringBuilder builder = new();
        foreach (BattleGroupActionZoneSnapshot zone in (zones?.Values ?? System.Array.Empty<BattleGroupActionZoneSnapshot>())
                     .OrderBy(item => item.BattleGroupId, System.StringComparer.Ordinal))
        {
            builder
                .Append(zone.BattleGroupId).Append(':')
                .Append(zone.Kind).Append(':')
                .Append(zone.TargetCombatZoneId).Append(':')
                .Append(zone.TargetRegionId).Append(':')
                .Append(zone.MinCellX).Append(',')
                .Append(zone.MinCellY).Append(',')
                .Append(zone.MaxCellX).Append(',')
                .Append(zone.MaxCellY).Append('|');
        }

        return builder.ToString();
    }
}
