using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Snapshots;

namespace Rpg.Runtime.Battle.Tactics;

internal static class BattleTacticalIntentPolicy
{
    internal static BattleTacticalIntentPlanSnapshot NormalizeForRuntime(BattleGroupSnapshot group)
    {
        BattleTacticalIntentPlanSnapshot authored = CopyIntentPlan(group?.TacticalIntentPlan);
        if (HasAuthoredPlan(authored))
        {
            authored.IntentSource = string.IsNullOrWhiteSpace(authored.IntentSource)
                ? BattleTacticalIntentPlanSources.Snapshot
                : authored.IntentSource;
            authored.IntentId = string.IsNullOrWhiteSpace(authored.IntentId)
                ? ResolveFallbackIntentId(group?.TacticalMode ?? BattleGroupTacticalMode.PlayerCommanded)
                : authored.IntentId;
            authored.PrimaryTargetSelector = string.IsNullOrWhiteSpace(authored.PrimaryTargetSelector)
                ? BattleTargetSelectors.CurrentSelectedRegion
                : authored.PrimaryTargetSelector;
            authored.RetargetPolicyId = string.IsNullOrWhiteSpace(authored.RetargetPolicyId)
                ? BattleRetargetPolicyIds.StableUntilInvalid
                : authored.RetargetPolicyId;
            return authored;
        }

        if (group?.TacticalMode is not (BattleGroupTacticalMode.EnemyOffense
            or BattleGroupTacticalMode.EnemyActiveDefense
            or BattleGroupTacticalMode.EnemyHoldDefense))
        {
            return new BattleTacticalIntentPlanSnapshot();
        }

        // Safe fallback is intentionally stable: volatile observations may inform
        // local combat, but they must not replace non-engaged enemy goals unless
        // an authored intent opts into that behavior.
        return new BattleTacticalIntentPlanSnapshot
        {
            IntentId = ResolveFallbackIntentId(group.TacticalMode),
            PrimaryTargetSelector = BattleTargetSelectors.CurrentSelectedRegion,
            RetargetPolicyId = BattleRetargetPolicyIds.StableUntilInvalid,
            FallbackIntentId = BattleTacticalIntentIds.HoldLine,
            IntentSource = BattleTacticalIntentPlanSources.SafeFallback
        };
    }

    internal static bool AllowsVolatileObservationRetarget(BattleGroupTacticalState state)
    {
        if (state == null ||
            state.TacticalMode is not (BattleGroupTacticalMode.EnemyOffense or BattleGroupTacticalMode.EnemyActiveDefense))
        {
            return false;
        }

        BattleTacticalIntentPlanSnapshot plan = state.TacticalIntentPlan;
        return Is(plan?.RetargetPolicyId, BattleRetargetPolicyIds.AllowVolatileObservation) ||
               Is(plan?.PrimaryTargetSelector, BattleTargetSelectors.RuntimeObservedHostileCluster) ||
               (plan?.SecondaryTargetSelectors ?? new List<string>())
               .Any(item => Is(item, BattleTargetSelectors.RuntimeObservedHostileCluster));
    }

    internal static BattleTacticalIntentPlanSnapshot CopyIntentPlan(BattleTacticalIntentPlanSnapshot source)
    {
        if (source == null)
        {
            return new BattleTacticalIntentPlanSnapshot();
        }

        return new BattleTacticalIntentPlanSnapshot
        {
            IntentId = source.IntentId ?? "",
            PrimaryTargetSelector = source.PrimaryTargetSelector ?? "",
            SecondaryTargetSelectors = (source.SecondaryTargetSelectors ?? new List<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToList(),
            StyleProfileId = source.StyleProfileId ?? "",
            LeashSelector = source.LeashSelector ?? "",
            RetargetPolicyId = source.RetargetPolicyId ?? "",
            EngagementPolicyId = source.EngagementPolicyId ?? "",
            FallbackIntentId = source.FallbackIntentId ?? "",
            IntentSource = source.IntentSource ?? ""
        };
    }

    private static string ResolveFallbackIntentId(BattleGroupTacticalMode mode)
    {
        return mode == BattleGroupTacticalMode.EnemyHoldDefense
            ? BattleTacticalIntentIds.HoldLine
            : BattleTacticalIntentIds.AssaultTarget;
    }

    private static bool HasAuthoredPlan(BattleTacticalIntentPlanSnapshot plan)
    {
        return plan != null &&
               (!string.IsNullOrWhiteSpace(plan.IntentId) ||
                !string.IsNullOrWhiteSpace(plan.PrimaryTargetSelector) ||
                (plan.SecondaryTargetSelectors?.Count ?? 0) > 0 ||
                !string.IsNullOrWhiteSpace(plan.StyleProfileId) ||
                !string.IsNullOrWhiteSpace(plan.LeashSelector) ||
                !string.IsNullOrWhiteSpace(plan.RetargetPolicyId) ||
                !string.IsNullOrWhiteSpace(plan.EngagementPolicyId) ||
                !string.IsNullOrWhiteSpace(plan.FallbackIntentId));
    }

    private static bool Is(string actual, string expected)
    {
        return string.Equals(actual ?? "", expected ?? "", StringComparison.Ordinal);
    }
}
