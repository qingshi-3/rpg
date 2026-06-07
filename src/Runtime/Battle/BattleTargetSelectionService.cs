using System.Collections.Generic;
using System.Diagnostics;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Diagnostics;
using Rpg.Runtime.Battle.Navigation;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle;

internal static class BattleTargetSelectionService
{
    private const int PlannedLocalPerceptionRange = BattlePerceptionPolicy.DefaultLocalPerceptionRange;

    private readonly record struct AssaultTargetScore(int TravelCost, int RetainedPriority, int Gap, string ActorId);
    internal readonly record struct BattleTargetCandidateSet(
        string SelectionPolicy,
        IReadOnlyList<BattleRuntimeAiTargetCandidateFacts> Candidates);

    // Candidate builders preserve command and region scope, but they do not pick
    // the ordinary target. The behavior tree owns that reusable tactical choice.
    internal static BattleTargetCandidateSet BuildTargetCandidatesForCommand(
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> facts,
        BattleRuntimeTickStartActorFact actorFact,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
        BattleFlowFieldCache flowFields,
        BattlePerformanceCounters performanceCounters)
    {
        string policy = ResolveCommandTargetSelectionPolicy(actorFact);
        long startedAt = Stopwatch.GetTimestamp();
        try
        {
            return new BattleTargetCandidateSet(
                policy,
                BuildTargetCandidates(
                    facts,
                    actorFact,
                    policy,
                    navigationGraph,
                    occupancy,
                    flowFields,
                    performanceCounters));
        }
        finally
        {
            if (string.Equals(policy, BattleRuntimeAiTargetSelectionPolicy.Default, System.StringComparison.Ordinal))
            {
                performanceCounters?.RecordTargetScoringElapsedTicks(Stopwatch.GetTimestamp() - startedAt);
            }
        }
    }

    internal static BattleTargetCandidateSet BuildRegionScopedTargetCandidates(
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> facts,
        BattleRuntimeTickStartActorFact actorFact)
    {
        return new BattleTargetCandidateSet(
            BattleRuntimeAiTargetSelectionPolicy.RegionScoped,
            BuildTargetCandidates(
                facts,
                actorFact,
                BattleRuntimeAiTargetSelectionPolicy.RegionScoped,
                navigationGraph: null,
                occupancy: null,
                flowFields: null,
                performanceCounters: null));
    }

    internal static BattleTargetCandidateSet BuildCombatZoneScopedTargetCandidates(
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> facts,
        BattleRuntimeTickStartActorFact actorFact,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
        BattleFlowFieldCache flowFields,
        BattlePerformanceCounters performanceCounters,
        BattleTacticalRegionSnapshot localCombatRegion)
    {
        return new BattleTargetCandidateSet(
            BattleRuntimeAiTargetSelectionPolicy.CombatZoneScoped,
            BuildTargetCandidates(
                facts,
                actorFact,
                BattleRuntimeAiTargetSelectionPolicy.CombatZoneScoped,
                navigationGraph,
                occupancy,
                flowFields,
                performanceCounters,
                localCombatRegion));
    }

    private static string ResolveCommandTargetSelectionPolicy(BattleRuntimeTickStartActorFact actorFact)
    {
        if (BattleRuntimeTickResolver.IsFocusFireCommand(actorFact.CommandId))
        {
            return BattleRuntimeAiTargetSelectionPolicy.FocusFire;
        }

        if (BattleRuntimeTickResolver.IsHoldLineCommand(actorFact.CommandId))
        {
            return BattleRuntimeAiTargetSelectionPolicy.HoldLine;
        }

        if (actorFact.Actor.EngagementRule == BattleEngagementRule.Hold)
        {
            return BattleRuntimeAiTargetSelectionPolicy.PlanScoped;
        }

        if (actorFact.Actor.HasObjectiveAnchor &&
            !BattleObjectiveAdvancePlanner.IsObjectiveReached(actorFact))
        {
            return actorFact.Actor.EngagementRule == BattleEngagementRule.MoveFirst
                ? BattleRuntimeAiTargetSelectionPolicy.MoveFirstPlanScoped
                : BattleRuntimeAiTargetSelectionPolicy.PlanScoped;
        }

        return BattleRuntimeAiTargetSelectionPolicy.Default;
    }

    private static IReadOnlyList<BattleRuntimeAiTargetCandidateFacts> BuildTargetCandidates(
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> facts,
        BattleRuntimeTickStartActorFact actorFact,
        string policy,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
        BattleFlowFieldCache flowFields,
        BattlePerformanceCounters performanceCounters,
        BattleTacticalRegionSnapshot localCombatRegion = null)
    {
        if (facts == null || actorFact.Actor == null)
        {
            return System.Array.Empty<BattleRuntimeAiTargetCandidateFacts>();
        }

        bool demoteRetainedTarget = ShouldDemoteRetainedTarget(actorFact.Actor);
        bool hasLowerTierCandidate = HasLowerTierCandidate(facts, actorFact, policy, demoteRetainedTarget);
        List<BattleRuntimeAiTargetCandidateFacts> candidates = new();
        int attackRange = System.Math.Max(1, actorFact.Actor.AttackRange);
        foreach (BattleRuntimeTickStartActorFact candidate in facts.Values)
        {
            if (candidate.Actor.ActorId == actorFact.Actor.ActorId ||
                GetCurrentHitPoints(candidate) <= 0 ||
                BattleRuntimeTickResolver.SameFaction(candidate.Actor, actorFact.Actor))
            {
                continue;
            }

            int orthogonalGap = BattleRuntimeTickResolver.GetOrthogonalAttackGap(actorFact.Actor, actorFact.Anchor, candidate.Actor, candidate.Anchor);
            int gridGap = GetSquareGridDistance(actorFact, candidate);
            bool immediate = orthogonalGap <= attackRange;
            bool retained = string.Equals(actorFact.TargetActorId, candidate.Actor.ActorId, System.StringComparison.Ordinal);
            bool combatZoneScoped = string.Equals(policy, BattleRuntimeAiTargetSelectionPolicy.CombatZoneScoped, System.StringComparison.Ordinal);
            bool retainedForPriority = retained && !demoteRetainedTarget;
            bool routeBlocking = BlocksObjectiveRoute(actorFact.Actor, candidate.Actor);
            bool executableCombatJoin = combatZoneScoped &&
                                        !immediate &&
                                        HasExecutableCombatZoneJoinStep(
                                            actorFact,
                                            candidate,
                                            navigationGraph,
                                            occupancy,
                                            flowFields,
                                            performanceCounters,
                                            localCombatRegion);
            int tier = combatZoneScoped && !immediate
                ? ResolveCombatZoneScopedSelectionTier(retained, executableCombatJoin)
                : ResolveSelectionTier(policy, immediate, retainedForPriority, routeBlocking, gridGap);
            if (tier == int.MaxValue)
            {
                continue;
            }

            int travelCost = ShouldScoreTravelCost(policy, tier, hasLowerTierCandidate, demoteRetainedTarget)
                ? ResolveAttackOpportunityTravelCost(
                    BattleTickStartProjectionBuilder.Build(actorFact),
                    BattleTickStartProjectionBuilder.Build(candidate),
                    actorFact.Anchor,
                    navigationGraph,
                    occupancy,
                    flowFields,
                    performanceCounters,
                    gridGap,
                    localCombatRegion)
                : int.MaxValue;
            candidates.Add(new BattleRuntimeAiTargetCandidateFacts
            {
                ActorId = candidate.Actor.ActorId ?? "",
                SelectionTier = tier,
                OrthogonalAttackGap = orthogonalGap,
                GridGap = gridGap,
                CenterManhattanDistance = GetCenterManhattanDistance(actorFact, candidate),
                HitPoints = GetCurrentHitPoints(candidate),
                TravelCost = travelCost,
                IsImmediateAttackOpportunity = immediate,
                IsRetainedTarget = retained,
                IsRouteBlockingObjective = routeBlocking
            });
        }

        return candidates;
    }

    private static bool HasLowerTierCandidate(
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> facts,
        BattleRuntimeTickStartActorFact actorFact,
        string policy,
        bool demoteRetainedTarget)
    {
        if (!string.Equals(policy, BattleRuntimeAiTargetSelectionPolicy.Default, System.StringComparison.Ordinal))
        {
            return false;
        }

        int attackRange = System.Math.Max(1, actorFact.Actor.AttackRange);
        foreach (BattleRuntimeTickStartActorFact candidate in facts.Values)
        {
            if (candidate.Actor.ActorId == actorFact.Actor.ActorId ||
                GetCurrentHitPoints(candidate) <= 0 ||
                BattleRuntimeTickResolver.SameFaction(candidate.Actor, actorFact.Actor))
            {
                continue;
            }

            int orthogonalGap = BattleRuntimeTickResolver.GetOrthogonalAttackGap(actorFact.Actor, actorFact.Anchor, candidate.Actor, candidate.Anchor);
            bool immediate = orthogonalGap <= attackRange;
            bool retained = string.Equals(actorFact.TargetActorId, candidate.Actor.ActorId, System.StringComparison.Ordinal);
            if (immediate || retained && !demoteRetainedTarget)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ShouldDemoteRetainedTarget(BattleRuntimeActor actor)
    {
        if (actor == null || actor.ConsecutiveAdvanceFailures <= 0)
        {
            return false;
        }

        string reason = actor.LastAdvanceFailureReason ?? "";
        // Target retention prevents jitter during normal marching. Once local
        // combat ingress has explicitly failed, the retained target becomes a
        // degraded preference so the same bounded candidate set can choose an
        // executable local target instead of parking forever behind a full front.
        return string.Equals(reason, LocalCombatDecisionReason.HoldSupportAttackSlotsFull, System.StringComparison.Ordinal) ||
               string.Equals(reason, LocalCombatDecisionReason.RejectNoReachableSlot, System.StringComparison.Ordinal) ||
               string.Equals(reason, BattleGroupTacticalReasonCode.LocalRegionDegradeNoReachableSlot, System.StringComparison.Ordinal);
    }

    private static int ResolveSelectionTier(
        string policy,
        bool immediate,
        bool retained,
        bool routeBlocking,
        int gridGap)
    {
        if (string.Equals(policy, BattleRuntimeAiTargetSelectionPolicy.FocusFire, System.StringComparison.Ordinal))
        {
            return 0;
        }

        if (string.Equals(policy, BattleRuntimeAiTargetSelectionPolicy.HoldLine, System.StringComparison.Ordinal))
        {
            return immediate ? 0 : int.MaxValue;
        }

        if (immediate)
        {
            return 0;
        }

        if (string.Equals(policy, BattleRuntimeAiTargetSelectionPolicy.MoveFirstPlanScoped, System.StringComparison.Ordinal))
        {
            return routeBlocking && gridGap <= PlannedLocalPerceptionRange ? 1 : int.MaxValue;
        }

        if (string.Equals(policy, BattleRuntimeAiTargetSelectionPolicy.PlanScoped, System.StringComparison.Ordinal) ||
            string.Equals(policy, BattleRuntimeAiTargetSelectionPolicy.RegionScoped, System.StringComparison.Ordinal))
        {
            if (gridGap > PlannedLocalPerceptionRange)
            {
                return int.MaxValue;
            }

            return retained ? 1 : 2;
        }

        if (retained)
        {
            return 1;
        }

        return 2;
    }

    private static int ResolveCombatZoneScopedSelectionTier(bool retained, bool executableJoin)
    {
        // Combat-zone joining is zone-first, but still stable: keep a retained
        // executable slot assignment, switch to another executable front when
        // the retained target is blocked, and preserve blocked retained targets
        // only when no executable join target exists so diagnostics stay named.
        if (retained && executableJoin)
        {
            return 1;
        }

        if (executableJoin)
        {
            return 2;
        }

        return retained ? 3 : 4;
    }

    private static bool HasExecutableCombatZoneJoinStep(
        BattleRuntimeTickStartActorFact actorFact,
        BattleRuntimeTickStartActorFact targetFact,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
        BattleFlowFieldCache flowFields,
        BattlePerformanceCounters performanceCounters,
        BattleTacticalRegionSnapshot localCombatRegion)
    {
        if (navigationGraph == null || occupancy == null)
        {
            return false;
        }

        BattleRuntimeActor tickStartActor = BattleTickStartProjectionBuilder.Build(actorFact);
        BattleRuntimeActor tickStartTarget = BattleTickStartProjectionBuilder.Build(targetFact);
        return BattleCombatSlotIntentResolver.TrySelectExecutableIntent(
            tickStartActor,
            tickStartTarget,
            actorFact.Anchor,
            navigationGraph,
            occupancy,
            new BattleMovementReservationMap(),
            flowFields,
            preferSupportSlots: false,
            performanceCounters,
            localCombatRegion,
            out _,
            out IReadOnlyList<BattleGridCoord> moveOptions) &&
            moveOptions.Count > 0;
    }

    private static bool ShouldScoreTravelCost(string policy, int tier, bool hasLowerTierCandidate, bool demoteRetainedTarget)
    {
        if (tier < 2)
        {
            return false;
        }

        if (string.Equals(policy, BattleRuntimeAiTargetSelectionPolicy.Default, System.StringComparison.Ordinal))
        {
            return !hasLowerTierCandidate;
        }

        // Combat-zone joining is already bounded by the commander action zone.
        // Combat-zone scoped decisions have already selected the action area.
        // Score executable local entry before target stickiness so a unit does
        // not need to fail on an old target before joining another open front.
        return string.Equals(policy, BattleRuntimeAiTargetSelectionPolicy.CombatZoneScoped, System.StringComparison.Ordinal);
    }

    private static int GetCenterManhattanDistance(
        BattleRuntimeTickStartActorFact first,
        BattleRuntimeTickStartActorFact second)
    {
        GetCenter2(first.Actor, first.Anchor, out int firstX, out int firstY);
        GetCenter2(second.Actor, second.Anchor, out int secondX, out int secondY);
        return System.Math.Abs(firstX - secondX) + System.Math.Abs(firstY - secondY);
    }

    private static void GetCenter2(BattleRuntimeActor actor, BattleGridCoord anchor, out int x, out int y)
    {
        x = anchor.X * 2 + BattleActorFootprint.NormalizeSize(actor?.FootprintWidth ?? 1) - 1;
        y = anchor.Y * 2 + BattleActorFootprint.NormalizeSize(actor?.FootprintHeight ?? 1) - 1;
    }

    internal static BattleRuntimeTickStartActorFact? FindEnemyCorpsForCommand(
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> facts,
        BattleRuntimeTickStartActorFact actorFact,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
        BattleFlowFieldCache flowFields,
        BattlePerformanceCounters performanceCounters)
    {
        if (BattleRuntimeTickResolver.IsFocusFireCommand(actorFact.CommandId))
        {
            return FindLowestHealthEnemyCorps(facts, actorFact);
        }

        if (BattleRuntimeTickResolver.IsHoldLineCommand(actorFact.CommandId))
        {
            return FindImmediateAttackOpportunityEnemyCorps(facts, actorFact);
        }

        if (actorFact.Actor.EngagementRule == BattleEngagementRule.Hold)
        {
            // Hold-line player commands stay fixed, but an authored hold plan is
            // a guard posture: it should wake on local contact without global
            // attack-slot scoring across the whole map.
            return FindPlanScopedEnemyCorps(facts, actorFact);
        }

        if (actorFact.Actor.HasObjectiveAnchor)
        {
            if (BattleObjectiveAdvancePlanner.IsObjectiveReached(actorFact))
            {
                // Objective zones are rally/approach intent, not a final idle
                // command. Once the group has reached the zone, normal contact
                // acquisition resumes so it can push into combat.
                return FindImmediateAttackOpportunityEnemyCorps(facts, actorFact) ??
                       FindRetainedEnemyCorps(facts, actorFact) ??
                       FindFastestAttackOpportunityEnemyCorps(facts, actorFact, navigationGraph, occupancy, flowFields, performanceCounters) ??
                       FindNearestEnemyCorps(facts, actorFact);
            }

            return FindPlanScopedEnemyCorps(facts, actorFact);
        }

        // Mature RTS movement keeps target acquisition sticky while units are
        // marching. Full attack-opportunity scoring builds flow fields, so doing
        // it every movement step turns pathing into visible frame spikes. Units
        // still snap to immediate melee opportunities and fully score when they
        // do not yet have a live target.
        return FindImmediateAttackOpportunityEnemyCorps(facts, actorFact) ??
               FindRetainedEnemyCorps(facts, actorFact) ??
               FindFastestAttackOpportunityEnemyCorps(facts, actorFact, navigationGraph, occupancy, flowFields, performanceCounters) ??
               FindNearestEnemyCorps(facts, actorFact);
    }

    internal static BattleRuntimeTickStartActorFact? FindRegionScopedEnemyCorps(
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> facts,
        BattleRuntimeTickStartActorFact actorFact)
    {
        return FindImmediateAttackOpportunityEnemyCorps(facts, actorFact) ??
               FindRetainedEnemyCorps(facts, actorFact, PlannedLocalPerceptionRange) ??
               FindNearestEnemyCorps(facts, actorFact, PlannedLocalPerceptionRange);
    }

    internal static BattleRuntimeTickStartActorFact? FindCombatZoneScopedEnemyCorps(
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> facts,
        BattleRuntimeTickStartActorFact actorFact)
    {
        // CombatJoin is already bounded by a commander-selected combat zone, so
        // the actor does not require its own local perception hit to join the fight.
        return FindImmediateAttackOpportunityEnemyCorps(facts, actorFact) ??
               FindRetainedEnemyCorps(facts, actorFact) ??
               FindNearestEnemyCorps(facts, actorFact);
    }

    private static BattleRuntimeTickStartActorFact? FindPlanScopedEnemyCorps(
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> facts,
        BattleRuntimeTickStartActorFact actorFact)
    {
        // Once a battle group has an authored objective, local perception owns
        // target acquisition. This preserves the player plan and prevents
        // ordinary marching from rebuilding global attack-slot fields.
        BattleRuntimeTickStartActorFact? immediate = FindImmediateAttackOpportunityEnemyCorps(facts, actorFact);
        if (immediate != null)
        {
            return immediate;
        }

        if (actorFact.Actor.EngagementRule == BattleEngagementRule.MoveFirst)
        {
            return FindRouteBlockingEnemyCorps(facts, actorFact, PlannedLocalPerceptionRange);
        }

        BattleRuntimeTickStartActorFact? retained = FindRetainedEnemyCorps(
            facts,
            actorFact,
            PlannedLocalPerceptionRange);
        if (retained != null)
        {
            return retained;
        }

        return FindNearestEnemyCorps(
            facts,
            actorFact,
            PlannedLocalPerceptionRange);
    }

    private static BattleRuntimeTickStartActorFact? FindRetainedEnemyCorps(
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> facts,
        BattleRuntimeTickStartActorFact actorFact,
        int maxGap = int.MaxValue)
    {
        if (string.IsNullOrWhiteSpace(actorFact.TargetActorId) ||
            !facts.TryGetValue(actorFact.TargetActorId, out BattleRuntimeTickStartActorFact retained))
        {
            return null;
        }

        return GetCurrentHitPoints(retained) > 0 &&
               !BattleRuntimeTickResolver.SameFaction(actorFact.Actor, retained.Actor) &&
               GetSquareGridDistance(actorFact, retained) <= maxGap
            ? retained
            : null;
    }

    internal static BattleRuntimeTickStartActorFact? FindImmediateAttackOpportunityEnemyCorps(
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> facts,
        BattleRuntimeTickStartActorFact actorFact)
    {
        BattleRuntimeTickStartActorFact? selected = null;
        int selectedGap = int.MaxValue;
        int selectedHitPoints = int.MaxValue;
        int attackRange = System.Math.Max(1, actorFact.Actor.AttackRange);

        foreach (BattleRuntimeTickStartActorFact candidate in facts.Values)
        {
            if (candidate.Actor.ActorId == actorFact.Actor.ActorId ||
                GetCurrentHitPoints(candidate) <= 0 ||
                BattleRuntimeTickResolver.SameFaction(candidate.Actor, actorFact.Actor))
            {
                continue;
            }

            int gap = BattleRuntimeTickResolver.GetOrthogonalAttackGap(actorFact.Actor, actorFact.Anchor, candidate.Actor, candidate.Anchor);
            if (gap > attackRange)
            {
                continue;
            }

            int hitPoints = GetCurrentHitPoints(candidate);
            if (selected == null ||
                gap < selectedGap ||
                gap == selectedGap && hitPoints < selectedHitPoints ||
                gap == selectedGap && hitPoints == selectedHitPoints &&
                string.CompareOrdinal(candidate.Actor.ActorId, selected.Value.Actor.ActorId) < 0)
            {
                selected = candidate;
                selectedGap = gap;
                selectedHitPoints = hitPoints;
            }
        }

        return selected;
    }

    private static BattleRuntimeTickStartActorFact? FindNearestEnemyCorps(
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> facts,
        BattleRuntimeTickStartActorFact actorFact,
        int maxGap = int.MaxValue)
    {
        BattleRuntimeTickStartActorFact? selected = null;
        int selectedGap = int.MaxValue;
        foreach (BattleRuntimeTickStartActorFact candidate in facts.Values)
        {
            if (candidate.Actor.ActorId == actorFact.Actor.ActorId ||
                GetCurrentHitPoints(candidate) <= 0 ||
                BattleRuntimeTickResolver.SameFaction(candidate.Actor, actorFact.Actor))
            {
                continue;
            }

            int gap = GetSquareGridDistance(actorFact, candidate);
            if (gap > maxGap)
            {
                continue;
            }

            if (selected == null ||
                gap < selectedGap ||
                gap == selectedGap && string.CompareOrdinal(candidate.Actor.ActorId, selected.Value.Actor.ActorId) < 0)
            {
                selected = candidate;
                selectedGap = gap;
            }
        }

        return selected;
    }

    private static BattleRuntimeTickStartActorFact? FindRouteBlockingEnemyCorps(
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> facts,
        BattleRuntimeTickStartActorFact actorFact,
        int maxGap)
    {
        if (actorFact.Actor.HasObjectiveAnchor == false)
        {
            return null;
        }

        BattleRuntimeTickStartActorFact? selected = null;
        int selectedGap = int.MaxValue;
        foreach (BattleRuntimeTickStartActorFact candidate in facts.Values)
        {
            if (candidate.Actor.ActorId == actorFact.Actor.ActorId ||
                GetCurrentHitPoints(candidate) <= 0 ||
                BattleRuntimeTickResolver.SameFaction(candidate.Actor, actorFact.Actor) ||
                !BlocksObjectiveRoute(actorFact.Actor, candidate.Actor))
            {
                continue;
            }

            int gap = GetSquareGridDistance(actorFact, candidate);
            if (gap > maxGap)
            {
                continue;
            }

            if (selected == null ||
                gap < selectedGap ||
                gap == selectedGap && string.CompareOrdinal(candidate.Actor.ActorId, selected.Value.Actor.ActorId) < 0)
            {
                selected = candidate;
                selectedGap = gap;
            }
        }

        return selected;
    }

    private static bool BlocksObjectiveRoute(BattleRuntimeActor actor, BattleRuntimeActor target)
    {
        if (actor?.HasObjectiveAnchor != true || target == null)
        {
            return false;
        }

        bool horizontalCorridor = actor.GridY == actor.ObjectiveGridY && target.GridY == actor.GridY;
        bool verticalCorridor = actor.GridX == actor.ObjectiveGridX && target.GridX == actor.GridX;
        return horizontalCorridor && IsBetween(actor.GridX, actor.ObjectiveGridX, target.GridX) ||
               verticalCorridor && IsBetween(actor.GridY, actor.ObjectiveGridY, target.GridY);
    }

    private static bool IsBetween(int first, int second, int value)
    {
        return value >= System.Math.Min(first, second) && value <= System.Math.Max(first, second);
    }

    private static BattleRuntimeTickStartActorFact? FindFastestAttackOpportunityEnemyCorps(
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> facts,
        BattleRuntimeTickStartActorFact actorFact,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
        BattleFlowFieldCache flowFields,
        BattlePerformanceCounters performanceCounters)
    {
        long startedAt = Stopwatch.GetTimestamp();
        BattleRuntimeTickStartActorFact? retained = FindRetainedEnemyCorps(facts, actorFact);
        BattleRuntimeTickStartActorFact? selected = null;
        AssaultTargetScore selectedScore = default;
        try
        {
            foreach (BattleRuntimeTickStartActorFact candidate in facts.Values)
            {
                if (candidate.Actor.ActorId == actorFact.Actor.ActorId ||
                    GetCurrentHitPoints(candidate) <= 0 ||
                    BattleRuntimeTickResolver.SameFaction(candidate.Actor, actorFact.Actor))
                {
                    continue;
                }

                AssaultTargetScore score = ScoreAssaultTarget(
                    actorFact,
                    candidate,
                    retained?.Actor.ActorId ?? "",
                    navigationGraph,
                    occupancy,
                    flowFields,
                    performanceCounters);
                if (selected == null || IsBetterAssaultTarget(score, selectedScore))
                {
                    selected = candidate;
                    selectedScore = score;
                }
            }

            return selected;
        }
        finally
        {
            performanceCounters?.RecordTargetScoringElapsedTicks(Stopwatch.GetTimestamp() - startedAt);
        }
    }

    private static AssaultTargetScore ScoreAssaultTarget(
        BattleRuntimeTickStartActorFact actorFact,
        BattleRuntimeTickStartActorFact targetFact,
        string retainedTargetId,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
        BattleFlowFieldCache flowFields,
        BattlePerformanceCounters performanceCounters)
    {
        int attackRange = System.Math.Max(1, actorFact.Actor.AttackRange);
        int gap = GetSquareGridDistance(actorFact, targetFact);
        int travelCost = gap <= attackRange
            ? 0
            : ResolveAttackOpportunityTravelCost(
                BattleTickStartProjectionBuilder.Build(actorFact),
                BattleTickStartProjectionBuilder.Build(targetFact),
                actorFact.Anchor,
                navigationGraph,
                occupancy,
                flowFields,
                performanceCounters,
                gap);
        int retainedPriority = string.Equals(targetFact.Actor.ActorId, retainedTargetId, System.StringComparison.Ordinal)
            ? 0
            : 1;
        return new AssaultTargetScore(travelCost, retainedPriority, gap, targetFact.Actor.ActorId ?? "");
    }

    private static int ResolveAttackOpportunityTravelCost(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleGridCoord actorAnchor,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
        BattleFlowFieldCache flowFields,
        BattlePerformanceCounters performanceCounters,
        int fallbackGap,
        BattleTacticalRegionSnapshot localCombatRegion = null)
    {
        if (actor == null || target == null || navigationGraph == null)
        {
            return 100000 + fallbackGap;
        }

        BattleFlowFieldCache cache = flowFields ?? new BattleFlowFieldCache(performanceCounters);
        BattleFlowField field = cache.GetOrBuild(
            actor,
            target,
            navigationGraph,
            preferSupportSlots: false,
            localCombatRegion: localCombatRegion);
        field = cache.PreferOpenAttackSlots(actor, navigationGraph, occupancy, field, localCombatRegion);
        if (!field.TryGetCost(actorAnchor, out int cost))
        {
            return 100000 + fallbackGap;
        }

        bool hasOpenAttackSlot = HasAttackGoal(field);
        return hasOpenAttackSlot ? cost : cost + 5000;
    }

    internal static bool HasReachableAttackSlot(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleGridCoord actorAnchor,
        BattleNavigationGraph navigationGraph,
        BattleDynamicOccupancy occupancy,
        BattleFlowFieldCache flowFields,
        BattlePerformanceCounters performanceCounters,
        BattleTacticalRegionSnapshot localCombatRegion = null)
    {
        if (actor == null || target == null || navigationGraph == null)
        {
            return false;
        }

        BattleFlowFieldCache cache = flowFields ?? new BattleFlowFieldCache(performanceCounters);
        BattleFlowField field = cache.GetOrBuild(
            actor,
            target,
            navigationGraph,
            preferSupportSlots: false,
            localCombatRegion: localCombatRegion);
        field = cache.PreferOpenAttackSlots(actor, navigationGraph, occupancy, field, localCombatRegion);
        // Reachability is a topology/open-slot fact. Local combat region penalties
        // can make an outside slot expensive, but must not erase the fallback.
        return field.TryGetCost(actorAnchor, out _) && HasAttackGoal(field);
    }

    private static bool HasAttackGoal(BattleFlowField field)
    {
        foreach (BattleCombatSlot slot in field?.GoalSlots ?? System.Array.Empty<BattleCombatSlot>())
        {
            if (slot.Kind == BattleCombatSlotKind.Attack)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsBetterAssaultTarget(AssaultTargetScore candidate, AssaultTargetScore known)
    {
        return candidate.TravelCost < known.TravelCost ||
               candidate.TravelCost == known.TravelCost && candidate.RetainedPriority < known.RetainedPriority ||
               candidate.TravelCost == known.TravelCost && candidate.RetainedPriority == known.RetainedPriority && candidate.Gap < known.Gap ||
               candidate.TravelCost == known.TravelCost && candidate.RetainedPriority == known.RetainedPriority && candidate.Gap == known.Gap &&
               string.CompareOrdinal(candidate.ActorId, known.ActorId) < 0;
    }

    private static BattleRuntimeTickStartActorFact? FindLowestHealthEnemyCorps(
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> facts,
        BattleRuntimeTickStartActorFact actorFact)
    {
        BattleRuntimeTickStartActorFact? selected = null;
        foreach (BattleRuntimeTickStartActorFact candidate in facts.Values)
        {
            if (candidate.Actor.ActorId == actorFact.Actor.ActorId ||
                GetCurrentHitPoints(candidate) <= 0 ||
                BattleRuntimeTickResolver.SameFaction(candidate.Actor, actorFact.Actor))
            {
                continue;
            }

            if (selected == null)
            {
                selected = candidate;
                continue;
            }

            int compare = GetCurrentHitPoints(candidate).CompareTo(GetCurrentHitPoints(selected.Value));
            if (compare < 0)
            {
                selected = candidate;
                continue;
            }

            if (compare > 0)
            {
                continue;
            }

            int gapCompare = GetSquareGridDistance(actorFact, candidate)
                .CompareTo(GetSquareGridDistance(actorFact, selected.Value));
            if (gapCompare < 0 ||
                gapCompare == 0 && string.CompareOrdinal(candidate.Actor.ActorId, selected.Value.Actor.ActorId) < 0)
            {
                selected = candidate;
            }
        }

        return selected;
    }

    internal static bool IsTargetEngagedBySameFactionActor(
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> facts,
        BattleRuntimeTickStartActorFact actorFact,
        BattleRuntimeTickStartActorFact targetFact)
    {
        foreach (BattleRuntimeTickStartActorFact candidate in facts.Values)
        {
            if (candidate.Actor.ActorId == actorFact.Actor.ActorId ||
                GetCurrentHitPoints(candidate) <= 0 ||
                !BattleRuntimeTickResolver.SameFaction(candidate.Actor, actorFact.Actor))
            {
                continue;
            }

            if (BattleRuntimeTickResolver.GetOrthogonalAttackGap(
                    candidate.Actor,
                    candidate.Anchor,
                    targetFact.Actor,
                    targetFact.Anchor) <= System.Math.Max(1, candidate.Actor.AttackRange))
            {
                return true;
            }
        }

        return false;
    }

    private static int GetCurrentHitPoints(BattleRuntimeTickStartActorFact fact)
    {
        return fact.Actor?.HitPoints ?? fact.HitPoints;
    }

    private static int GetSquareGridDistance(BattleRuntimeTickStartActorFact first, BattleRuntimeTickStartActorFact second)
    {
        return BattleActorFootprint.GetGap(first.Actor, first.Anchor, second.Actor, second.Anchor);
    }
}
