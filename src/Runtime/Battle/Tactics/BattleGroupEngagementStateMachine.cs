using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle.Events;

namespace Rpg.Runtime.Battle.Tactics;

public static class BattleGroupEngagementStateMachine
{
    private readonly record struct MemberActionTrigger(
        string BattleGroupId,
        string MemberActorId,
        string OtherActorId,
        string ReasonCode,
        int Priority);

    public static IReadOnlyList<BattleEvent> ApplyPerceptionTransitions(
        BattleGroupTacticalStateStore store,
        IReadOnlyDictionary<string, BattleGroupPerceptionSummary> perceptionSummaries,
        IReadOnlyDictionary<string, BattleCombatZoneSnapshot> combatZones,
        IReadOnlySet<string> groupsWithActiveCombatActions,
        IReadOnlySet<string> groupsWithRetainedCombatJoin,
        string battleId,
        int runtimeTick,
        double runtimeTimeSeconds)
    {
        if (store == null || perceptionSummaries == null)
        {
            return Array.Empty<BattleEvent>();
        }

        List<BattleEvent> events = new();
        foreach (BattleGroupPerceptionSummary summary in perceptionSummaries.Values
                     .OrderBy(item => item.BattleGroupId, StringComparer.Ordinal))
        {
            if (summary == null ||
                string.IsNullOrWhiteSpace(summary.BattleGroupId))
            {
                continue;
            }

            BattleGroupTacticalState state;
            try
            {
                state = store.GetRequiredSnapshot(summary.BattleGroupId);
            }
            catch (KeyNotFoundException)
            {
                continue;
            }

            bool hasPerceivedHostile = summary.PerceivedHostileActorIds.Count > 0;
            bool hasCombatZoneOverlap = HasCombatZoneOverlap(summary.BattleGroupId, combatZones);
            if (!hasPerceivedHostile && !hasCombatZoneOverlap)
            {
                events.AddRange(ApplyNoPerceptionTransition(
                    store,
                    state,
                    summary.BattleGroupId,
                    groupsWithActiveCombatActions?.Contains(summary.BattleGroupId ?? "") == true,
                    groupsWithRetainedCombatJoin?.Contains(summary.BattleGroupId ?? "") == true,
                    battleId,
                    runtimeTick,
                    runtimeTimeSeconds));
                continue;
            }

            store.ResetNoPerceivedHostileTicks(summary.BattleGroupId);
            if (state.EngagementState == BattleGroupEngagementState.Engaged ||
                !CanEnterEngagementFromObservation(state))
            {
                continue;
            }

            string reasonCode = hasPerceivedHostile
                ? BattleGroupTacticalReasonCode.EngagementEnterGroupPerception
                : BattleGroupTacticalReasonCode.EngagementEnterCombatZoneOverlap;
            BattleGroupTacticalMode nextMode = state.TacticalMode == BattleGroupTacticalMode.EnemyHoldDefense
                ? BattleGroupTacticalMode.EnemyActiveDefense
                : state.TacticalMode;
            if (!store.TryApplyEngagementState(summary.BattleGroupId, BattleGroupEngagementState.Engaged, nextMode))
            {
                continue;
            }

            events.Add(new BattleEvent
            {
                EventId = $"{battleId}:tick_{runtimeTick}:{summary.BattleGroupId}:engagement:{reasonCode}",
                BattleId = battleId ?? "",
                BattleGroupId = summary.BattleGroupId ?? "",
                Kind = BattleEventKind.BattleGroupEngagementStateChanged,
                ReasonCode = reasonCode,
                RuntimeTick = runtimeTick,
                RuntimeTimeSeconds = runtimeTimeSeconds
            });
        }

        return events;
    }

    public static IReadOnlyList<BattleEvent> ApplyMemberActionTransitions(
        BattleGroupTacticalStateStore store,
        IEnumerable<BattleEvent> damageEvents,
        IReadOnlyDictionary<string, string> actorBattleGroupIds,
        string battleId,
        int runtimeTick,
        double runtimeTimeSeconds)
    {
        if (store == null || damageEvents == null || actorBattleGroupIds == null)
        {
            return Array.Empty<BattleEvent>();
        }

        Dictionary<string, MemberActionTrigger> triggers = new(StringComparer.Ordinal);
        foreach (BattleEvent damage in damageEvents
                     .Where(item => item?.Kind == BattleEventKind.DamageApplied)
                     .OrderBy(item => item.EventId, StringComparer.Ordinal))
        {
            if (actorBattleGroupIds.TryGetValue(damage.TargetId ?? "", out string damagedGroupId))
            {
                store.RecordMemberDamageTriggerTick(damagedGroupId, runtimeTick);
                AddTrigger(
                    triggers,
                    new MemberActionTrigger(
                        damagedGroupId,
                        damage.TargetId ?? "",
                        damage.ActorId ?? "",
                        BattleGroupTacticalReasonCode.EngagementEnterMemberDamaged,
                        0));
            }

            if (actorBattleGroupIds.TryGetValue(damage.ActorId ?? "", out string attackingGroupId))
            {
                store.RecordMemberAttackTriggerTick(attackingGroupId, runtimeTick);
                AddTrigger(
                    triggers,
                    new MemberActionTrigger(
                        attackingGroupId,
                        damage.ActorId ?? "",
                        damage.TargetId ?? "",
                        BattleGroupTacticalReasonCode.EngagementEnterMemberAttacked,
                        1));
            }
        }

        List<BattleEvent> events = new();
        foreach (MemberActionTrigger trigger in triggers.Values
                     .OrderBy(item => item.BattleGroupId, StringComparer.Ordinal))
        {
            BattleEvent engagement = TryActivateHoldDefense(
                store,
                trigger,
                battleId,
                runtimeTick,
                runtimeTimeSeconds);
            if (engagement != null)
            {
                events.Add(engagement);
            }
        }

        return events;
    }

    private static IReadOnlyList<BattleEvent> ApplyNoPerceptionTransition(
        BattleGroupTacticalStateStore store,
        BattleGroupTacticalState state,
        string battleGroupId,
        bool hasActiveCombatAction,
        bool hasRetainedCombatJoin,
        string battleId,
        int runtimeTick,
        double runtimeTimeSeconds)
    {
        if (state.EngagementState != BattleGroupEngagementState.Engaged)
        {
            store.ResetNoPerceivedHostileTicks(battleGroupId);
            return Array.Empty<BattleEvent>();
        }

        if (HasRecentMemberActionTrigger(state, runtimeTick))
        {
            store.ResetNoPerceivedHostileTicks(battleGroupId);
            return Array.Empty<BattleEvent>();
        }

        if (hasRetainedCombatJoin)
        {
            // A player-commanded combat join is the group-owned next combat
            // scope. Do not clear targets or fall back to objective movement
            // until that combat-zone fact has disappeared from observation.
            store.ResetNoPerceivedHostileTicks(battleGroupId);
            return Array.Empty<BattleEvent>();
        }

        int noPerceptionTicks = store.RecordNoPerceivedHostileTick(battleGroupId);
        if (hasActiveCombatAction &&
            noPerceptionTicks <= BattleGroupTacticalPolicySettings.DefaultActiveCombatDisengageGraceTicks)
        {
            return Array.Empty<BattleEvent>();
        }

        if (noPerceptionTicks < BattleGroupTacticalPolicySettings.DefaultDisengageGraceTicks)
        {
            return Array.Empty<BattleEvent>();
        }

        if (!store.TryApplyEngagementState(battleGroupId, BattleGroupEngagementState.NotEngaged, state.TacticalMode))
        {
            return Array.Empty<BattleEvent>();
        }

        return new[]
        {
            new BattleEvent
            {
                EventId = $"{battleId}:tick_{runtimeTick}:{battleGroupId}:engagement:{BattleGroupTacticalReasonCode.EngagementExitNoGroupPerception}",
                BattleId = battleId ?? "",
                BattleGroupId = battleGroupId ?? "",
                Kind = BattleEventKind.BattleGroupEngagementStateChanged,
                ReasonCode = BattleGroupTacticalReasonCode.EngagementExitNoGroupPerception,
                RuntimeTick = runtimeTick,
                RuntimeTimeSeconds = runtimeTimeSeconds
            }
        };
    }

    private static void AddTrigger(
        Dictionary<string, MemberActionTrigger> triggers,
        MemberActionTrigger trigger)
    {
        if (string.IsNullOrWhiteSpace(trigger.BattleGroupId))
        {
            return;
        }

        if (!triggers.TryGetValue(trigger.BattleGroupId, out MemberActionTrigger current) ||
            trigger.Priority < current.Priority ||
            trigger.Priority == current.Priority &&
            string.CompareOrdinal(trigger.MemberActorId, current.MemberActorId) < 0)
        {
            triggers[trigger.BattleGroupId] = trigger;
        }
    }

    private static BattleEvent TryActivateHoldDefense(
        BattleGroupTacticalStateStore store,
        MemberActionTrigger trigger,
        string battleId,
        int runtimeTick,
        double runtimeTimeSeconds)
    {
        BattleGroupTacticalState state;
        try
        {
            state = store.GetRequiredSnapshot(trigger.BattleGroupId);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }

        if (state.EngagementState == BattleGroupEngagementState.Engaged ||
            state.TacticalMode != BattleGroupTacticalMode.EnemyHoldDefense)
        {
            return null;
        }

        if (!store.TryApplyEngagementState(
                trigger.BattleGroupId,
                BattleGroupEngagementState.Engaged,
                BattleGroupTacticalMode.EnemyActiveDefense))
        {
            return null;
        }

        return new BattleEvent
        {
            EventId = $"{battleId}:tick_{runtimeTick}:{trigger.BattleGroupId}:engagement:{trigger.ReasonCode}",
            BattleId = battleId ?? "",
            BattleGroupId = trigger.BattleGroupId ?? "",
            ActorId = trigger.MemberActorId ?? "",
            TargetId = trigger.OtherActorId ?? "",
            Kind = BattleEventKind.BattleGroupEngagementStateChanged,
            ReasonCode = trigger.ReasonCode ?? "",
            RuntimeTick = runtimeTick,
            RuntimeTimeSeconds = runtimeTimeSeconds
        };
    }

    private static bool HasRecentMemberActionTrigger(BattleGroupTacticalState state, int runtimeTick)
    {
        int lastTriggerTick = Math.Max(
            state?.LastMemberDamageTriggerTick ?? -1,
            state?.LastMemberAttackTriggerTick ?? -1);
        return lastTriggerTick >= 0 &&
               runtimeTick - lastTriggerTick <= BattleGroupTacticalPolicySettings.DefaultDisengageGraceTicks;
    }

    private static bool HasCombatZoneOverlap(
        string battleGroupId,
        IReadOnlyDictionary<string, BattleCombatZoneSnapshot> combatZones)
    {
        if (string.IsNullOrWhiteSpace(battleGroupId) ||
            combatZones == null ||
            combatZones.Count == 0)
        {
            return false;
        }

        return combatZones.Values.Any(zone =>
            zone?.HasCloseHostileContact == true &&
            zone.BattleGroupIds?.Contains(battleGroupId ?? "") == true);
    }

    private static bool CanEnterEngagementFromObservation(BattleGroupTacticalState state)
    {
        BattleGroupTacticalMode mode = state?.TacticalMode ?? BattleGroupTacticalMode.PlayerCommanded;
        bool enemyPolicyCanEnter = mode is BattleGroupTacticalMode.EnemyOffense
            or BattleGroupTacticalMode.EnemyActiveDefense
            or BattleGroupTacticalMode.EnemyHoldDefense;
        return enemyPolicyCanEnter ||
               (mode == BattleGroupTacticalMode.PlayerCommanded && state?.AllowPlayerScopedEngagement == true);
    }
}
