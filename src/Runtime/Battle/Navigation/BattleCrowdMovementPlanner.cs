using System.Collections.Generic;
using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Diagnostics;

namespace Rpg.Runtime.Battle.Navigation;

internal static class BattleCrowdMovementPlanner
{
    private const int FlowCostWeight = 100;
    private const int AxisGapPenalty = 10000;
    private const int LateralStepPenalty = 1000;
    private const int LocalObjectiveAvoidanceMaxDepth = 6;
    private const int LocalObjectiveAvoidanceMaxNodes = 64;
    private const int LocalObjectiveFollowBudget = 20;
    private const int LocalAttackSlotProofMaxCellDistance = 7;
    private const int ObstacleFollowPenalty = 50000;

    public static bool TryFindNextStepTowardTarget(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattleMovementReservationMap reservations,
        bool preferSupportSlots,
        bool avoidOpeningNewAxisGapNearEngagedTarget,
        out BattleGridCoord nextStep,
        BattlePerformanceCounters performanceCounters = null,
        BattleTacticalRegionSnapshot localCombatRegion = null)
    {
        nextStep = default;
        IReadOnlyList<BattleGridCoord> candidates = FindNextStepCandidatesTowardTarget(
            actor,
            target,
            graph,
            occupancy,
            reservations,
            preferSupportSlots,
            avoidOpeningNewAxisGapNearEngagedTarget,
            performanceCounters,
            localCombatRegion);
        if (candidates.Count == 0)
        {
            return false;
        }

        nextStep = candidates[0];
        return true;
    }

    public static IReadOnlyList<BattleGridCoord> FindNextStepCandidatesTowardTarget(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattleMovementReservationMap reservations,
        bool preferSupportSlots,
        bool avoidOpeningNewAxisGapNearEngagedTarget,
        BattlePerformanceCounters performanceCounters = null,
        BattleTacticalRegionSnapshot localCombatRegion = null)
    {
        if (actor == null || target == null || graph == null || occupancy == null || reservations == null)
        {
            return new List<BattleGridCoord>();
        }

        BattleGridCoord start = new(actor.GridX, actor.GridY, actor.GridHeight);
        if (!graph.Contains(start))
        {
            return new List<BattleGridCoord>();
        }

        // First-slice movement uses bounded local neighbor checks. Flow-field
        // construction is intentionally out of the Runtime hot path; if no
        // useful neighbor exists, the state machine degrades to support/hold.
        return FindGreedyNextStepCandidatesTowardTarget(
            actor,
            target,
            graph,
            occupancy,
            reservations,
            avoidOpeningNewAxisGapNearEngagedTarget,
            localCombatRegion);
    }

    public static IReadOnlyList<BattleGridCoord> FindGreedyNextStepCandidatesTowardTarget(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattleMovementReservationMap reservations,
        bool avoidOpeningNewAxisGapNearEngagedTarget = false,
        BattleTacticalRegionSnapshot localCombatRegion = null)
    {
        if (actor == null || target == null || graph == null || occupancy == null || reservations == null)
        {
            return System.Array.Empty<BattleGridCoord>();
        }

        BattleGridCoord start = new(actor.GridX, actor.GridY, actor.GridHeight);
        BattleGridCoord targetAnchor = new(target.GridX, target.GridY, target.GridHeight);
        int attackRange = System.Math.Max(1, actor.AttackRange);
        if (!graph.Contains(start))
        {
            return System.Array.Empty<BattleGridCoord>();
        }

        int startGap = BattleActorFootprint.GetGap(actor, start, target, targetAnchor);
        int startCenterDistance = GetCenterManhattanDistance(actor, start, target, targetAnchor);
        List<MoveOption> options = new();
        foreach (BattleGridCoord neighbor in graph.GetNeighbors(start))
        {
            if (!BattlePathStepRules.CanUseStaticStep(actor, start, neighbor, graph) ||
                !reservations.CanReserveMove(actor, start, neighbor, occupancy) ||
                IsRecentBacktrackStep(actor, start, neighbor) ||
                avoidOpeningNewAxisGapNearEngagedTarget && OpensNewAxisGap(actor, target, start, neighbor))
            {
                continue;
            }

            if (!HasBoundedStaticRouteToAttackRange(actor, target, neighbor, graph, attackRange))
            {
                continue;
            }

            int candidateGap = BattleActorFootprint.GetGap(actor, neighbor, target, targetAnchor);
            int candidateCenterDistance = GetCenterManhattanDistance(actor, neighbor, target, targetAnchor);
            if (candidateGap > startGap ||
                candidateGap == startGap && candidateCenterDistance >= startCenterDistance)
            {
                continue;
            }

            // Ordinary corps use a cheap local pressure step. The score stays
            // footprint-aware so "closer" means closer to legal attack range,
            // not just closer by sprite center.
            int score = candidateGap * FlowCostWeight +
                        candidateCenterDistance * 10 +
                        BattleLocalRegionPreference.GetStepPenalty(neighbor, localCombatRegion) +
                        GetStepCost(start, neighbor);
            options.Add(new MoveOption(neighbor, score, candidateGap));
        }

        return SortMoveOptions(options);
    }

    private static IReadOnlyList<BattleGridCoord> FindLocalObstacleAvoidanceCandidatesTowardObjective(
        BattleRuntimeActor actor,
        BattleRuntimeActor objective,
        BattleGridCoord objectiveAnchor,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattleMovementReservationMap reservations,
        BattleGridCoord start,
        int startGap,
        int startCenterDistance)
    {
        Dictionary<BattleGridCoord, MoveOption> bestByFirstStep = new();
        Queue<LocalAvoidanceNode> frontier = new();
        HashSet<BattleGridCoord> visited = new() { start };

        foreach (BattleGridCoord neighbor in graph.GetNeighbors(start))
        {
            if (!BattlePathStepRules.CanUseAnchor(actor, start, start, neighbor, graph, occupancy, reservations) ||
                IsRecentBacktrackStep(actor, start, neighbor))
            {
                continue;
            }

            visited.Add(neighbor);
            LocalAvoidanceNode node = new(
                neighbor,
                neighbor,
                1,
                GetStepCost(start, neighbor));
            AddObjectiveAvoidanceCandidate(
                bestByFirstStep,
                node,
                actor,
                objective,
                objectiveAnchor,
                startGap,
                startCenterDistance);
            frontier.Enqueue(node);
        }

        while (frontier.Count > 0 && visited.Count < LocalObjectiveAvoidanceMaxNodes)
        {
            LocalAvoidanceNode current = frontier.Dequeue();
            if (current.Depth >= LocalObjectiveAvoidanceMaxDepth)
            {
                continue;
            }

            foreach (BattleGridCoord neighbor in graph.GetNeighbors(current.Anchor))
            {
                if (visited.Count >= LocalObjectiveAvoidanceMaxNodes)
                {
                    break;
                }

                if (visited.Contains(neighbor) ||
                    !BattlePathStepRules.CanUseAnchor(actor, start, current.Anchor, neighbor, graph, occupancy, reservations))
                {
                    continue;
                }

                visited.Add(neighbor);
                LocalAvoidanceNode node = new(
                    neighbor,
                    current.FirstStep,
                    current.Depth + 1,
                    current.TravelCost + GetStepCost(current.Anchor, neighbor));
                AddObjectiveAvoidanceCandidate(
                    bestByFirstStep,
                    node,
                    actor,
                    objective,
                    objectiveAnchor,
                    startGap,
                    startCenterDistance);
                frontier.Enqueue(node);
            }
        }

        if (bestByFirstStep.Count == 0)
        {
            return System.Array.Empty<BattleGridCoord>();
        }

        List<MoveOption> options = new(bestByFirstStep.Count);
        foreach (MoveOption option in bestByFirstStep.Values)
        {
            options.Add(option);
        }

        return SortMoveOptions(options);
    }

    private static void AddObjectiveAvoidanceCandidate(
        Dictionary<BattleGridCoord, MoveOption> bestByFirstStep,
        LocalAvoidanceNode node,
        BattleRuntimeActor actor,
        BattleRuntimeActor objective,
        BattleGridCoord objectiveAnchor,
        int startGap,
        int startCenterDistance)
    {
        int candidateGap = BattleActorFootprint.GetGap(actor, node.Anchor, objective, objectiveAnchor);
        int candidateCenterDistance = GetCenterManhattanDistance(actor, node.Anchor, objective, objectiveAnchor);
        if (candidateGap > startGap ||
            candidateGap == startGap && candidateCenterDistance >= startCenterDistance)
        {
            return;
        }

        // This is bounded obstacle avoidance for objective marching: the
        // runtime only commits the first step, then the state machine validates
        // again next tick instead of owning a long route or flow field.
        int score = candidateGap * FlowCostWeight +
                    candidateCenterDistance * 10 +
                    node.Depth * 25 +
                    node.TravelCost +
                    GetStepCost(new BattleGridCoord(actor.GridX, actor.GridY, actor.GridHeight), node.FirstStep);
        MoveOption option = new(node.FirstStep, score, candidateGap);
        if (!bestByFirstStep.TryGetValue(node.FirstStep, out MoveOption known) ||
            IsBetter(option, known))
        {
            bestByFirstStep[node.FirstStep] = option;
        }
    }

    private static bool HasBoundedStaticRouteToAttackRange(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleGridCoord start,
        BattleNavigationGraph graph,
        int attackRange)
    {
        const int MaxGreedyLookaheadNodes = 256;
        BattleGridCoord targetAnchor = new(target.GridX, target.GridY, target.GridHeight);
        int startCellDistance = GetAnchorDistance(start, targetAnchor) / BattlePathCostPolicy.StepCost;
        if (startCellDistance > LocalAttackSlotProofMaxCellDistance)
        {
            // The attack-slot proof is a near-field guard against walking into
            // a locally blocked target. Far pressure movement must not require
            // proving the final attack slot through a tiny BFS horizon; each
            // committed step is still validated again at Runtime boundaries.
            return true;
        }

        Queue<BattleGridCoord> frontier = new();
        HashSet<BattleGridCoord> visited = new();
        frontier.Enqueue(start);
        visited.Add(start);
        while (frontier.Count > 0 && visited.Count <= MaxGreedyLookaheadNodes)
        {
            BattleGridCoord current = frontier.Dequeue();
            int orthogonalGap = BattleActorFootprint.GetOrthogonalGap(actor, current, target, targetAnchor);
            if (orthogonalGap <= attackRange)
            {
                return true;
            }

            foreach (BattleGridCoord neighbor in graph.GetNeighbors(current))
            {
                if (visited.Contains(neighbor) ||
                    !BattlePathStepRules.CanUseStaticStep(actor, current, neighbor, graph))
                {
                    continue;
                }

                visited.Add(neighbor);
                frontier.Enqueue(neighbor);
            }
        }

        return false;
    }

    public static IReadOnlyList<BattleGridCoord> FindNextStepCandidatesTowardCombatSlot(
        BattleRuntimeActor actor,
        BattleGridCoord combatSlotAnchor,
        BattleCombatSlotKind combatSlotKind,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattleMovementReservationMap reservations,
        BattlePerformanceCounters performanceCounters = null,
        BattleTacticalRegionSnapshot localCombatRegion = null,
        bool allowNonImprovingLocalAvoidance = true)
    {
        if (actor == null || graph == null || occupancy == null || reservations == null)
        {
            return new List<BattleGridCoord>();
        }

        BattleGridCoord start = new(actor.GridX, actor.GridY, actor.GridHeight);
        if (!graph.Contains(start) || start == combatSlotAnchor)
        {
            return new List<BattleGridCoord>();
        }

        return FindNextStepCandidatesTowardAnchor(
            actor,
            combatSlotAnchor,
            graph,
            occupancy,
            reservations,
            localCombatRegion,
            allowNonImprovingLocalAvoidance);
    }

    public static IReadOnlyList<BattleGridCoord> FindNextStepCandidatesTowardObjective(
        BattleRuntimeActor actor,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattleMovementReservationMap reservations,
        BattlePerformanceCounters performanceCounters = null,
        string battleId = "",
        int tick = -1)
    {
        if (actor == null || graph == null || occupancy == null || reservations == null || !actor.HasObjectiveAnchor)
        {
            return new List<BattleGridCoord>();
        }

        BattleGridCoord start = new(actor.GridX, actor.GridY, actor.GridHeight);
        if (!graph.Contains(start))
        {
            return new List<BattleGridCoord>();
        }

        BattleGridCoord objectiveAnchor = new(actor.ObjectiveGridX, actor.ObjectiveGridY, actor.ObjectiveGridHeight);
        BattleRuntimeActor objective = new()
        {
            GridX = objectiveAnchor.X,
            GridY = objectiveAnchor.Y,
            GridHeight = objectiveAnchor.Height,
            FootprintWidth = System.Math.Max(1, actor.ObjectiveWidth),
            FootprintHeight = System.Math.Max(1, actor.ObjectiveHeight)
        };
        bool routeHintAvailable = false;
        BattleRouteHint routeHint = default;
        void LogRouteHint(string source, IReadOnlyList<BattleGridCoord> candidates)
        {
            BattleObjectiveSteeringDiagnostics.Log(battleId, tick, actor, start, objectiveAnchor, source, routeHint, candidates);
        }

        if (graph.TryGetRouteHintTowardObjective(
                actor,
                objectiveAnchor,
                objective.FootprintWidth,
                objective.FootprintHeight,
                out routeHint,
                battleId,
                tick))
        {
            routeHintAvailable = routeHint.Anchor != start && routeHint.Anchor != objectiveAnchor;
            if (routeHintAvailable)
            {
                IReadOnlyList<BattleGridCoord> routeOptions = FindNextStepCandidatesTowardAnchor(
                    actor,
                    routeHint.Anchor,
                    graph,
                    occupancy,
                    reservations,
                    localCombatRegion: null,
                    allowNonImprovingLocalAvoidance: true);
                if (routeOptions.Count > 0)
                {
                    BattleRuntimeActor routeAnchor = new() { GridX = routeHint.Anchor.X, GridY = routeHint.Anchor.Y, GridHeight = routeHint.Anchor.Height, FootprintWidth = 1, FootprintHeight = 1 };
                    // Route hints solve map-scale static barriers. The selected
                    // neighbor still goes through normal local movement validation.
                    RecordSeekGoalSteering(
                        actor,
                        $"{BuildObjectiveSteeringIntentKey(actor, objectiveAnchor)}:route:{routeHint.CorridorId}",
                        routeOptions[0],
                        routeAnchor,
                        routeHint.Anchor);
                    LogRouteHint("route_hint_used", routeOptions);
                    return routeOptions;
                }

                LogRouteHint("route_hint_no_local_step", routeOptions);
            }
            else
            {
                LogRouteHint("route_hint_degenerate", System.Array.Empty<BattleGridCoord>());
            }
        }

        int startGap = BattleActorFootprint.GetGap(actor, start, objective, objectiveAnchor);
        int startCenterDistance = GetCenterManhattanDistance(actor, start, objective, objectiveAnchor);

        List<MoveOption> options = new();
        foreach (BattleGridCoord neighbor in graph.GetNeighbors(start))
        {
            if (!BattlePathStepRules.CanUseStaticStep(actor, start, neighbor, graph) ||
                !reservations.CanReserveMove(actor, start, neighbor, occupancy) ||
                IsRecentBacktrackStep(actor, start, neighbor))
            {
                continue;
            }

            int candidateGap = BattleActorFootprint.GetGap(actor, neighbor, objective, objectiveAnchor);
            int candidateCenterDistance = GetCenterManhattanDistance(actor, neighbor, objective, objectiveAnchor);
            if (candidateGap > startGap ||
                candidateGap == startGap && candidateCenterDistance >= startCenterDistance)
            {
                continue;
            }

            int score = candidateGap * FlowCostWeight +
                        candidateCenterDistance * 10 +
                        GetStepCost(start, neighbor);
            options.Add(new MoveOption(neighbor, score, candidateGap));
        }

        string steeringIntentKey = BuildObjectiveSteeringIntentKey(actor, objectiveAnchor);
        BattleGridCoord[] ordered = SortMoveOptions(options);
        if (ordered.Length > 0)
        {
            RecordSeekGoalSteering(actor, steeringIntentKey, ordered[0], objective, objectiveAnchor);
            if (routeHintAvailable)
            {
                LogRouteHint("direct_fallback_after_route_hint", ordered);
            }

            return ordered;
        }

        IReadOnlyList<BattleGridCoord> localAvoidance = FindLocalObstacleAvoidanceCandidatesTowardObjective(
            actor,
            objective,
            objectiveAnchor,
            graph,
            occupancy,
            reservations,
            start,
            startGap,
            startCenterDistance);
        if (localAvoidance.Count > 0)
        {
            RecordFollowObstacleSteering(actor, steeringIntentKey, start, localAvoidance[0], objective, objectiveAnchor, ResolveStepSide(start, objectiveAnchor, localAvoidance[0]));
            if (routeHintAvailable)
            {
                LogRouteHint("local_avoidance_after_route_hint", localAvoidance);
            }

            return localAvoidance;
        }

        IReadOnlyList<BattleGridCoord> obstacleFollow = FindObstacleFollowCandidatesTowardObjective(
            actor,
            objective,
            objectiveAnchor,
            graph,
            occupancy,
            reservations,
            start,
            startCenterDistance,
            steeringIntentKey);
        if (routeHintAvailable)
        {
            LogRouteHint(obstacleFollow.Count > 0 ? "obstacle_follow_after_route_hint" : "no_candidate_after_route_hint", obstacleFollow);
        }

        return obstacleFollow;
    }

    private static IReadOnlyList<BattleGridCoord> FindObstacleFollowCandidatesTowardObjective(
        BattleRuntimeActor actor,
        BattleRuntimeActor objective,
        BattleGridCoord objectiveAnchor,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattleMovementReservationMap reservations,
        BattleGridCoord start,
        int startCenterDistance,
        string steeringIntentKey)
    {
        int primarySide = ResolveFollowSide(actor, start, objectiveAnchor, graph, occupancy, reservations, steeringIntentKey);
        if (primarySide == 0)
        {
            return System.Array.Empty<BattleGridCoord>();
        }

        List<MoveOption> options = new();
        AddObstacleFollowOptions(options, actor, objective, objectiveAnchor, graph, occupancy, reservations, start, primarySide, startCenterDistance);
        if (options.Count == 0)
        {
            int fallbackSide = -primarySide;
            AddObstacleFollowOptions(options, actor, objective, objectiveAnchor, graph, occupancy, reservations, start, fallbackSide, startCenterDistance);
            primarySide = options.Count > 0 ? fallbackSide : primarySide;
        }

        BattleGridCoord[] ordered = SortMoveOptions(options);
        if (ordered.Length == 0)
        {
            return ordered;
        }

        RecordFollowObstacleSteering(actor, steeringIntentKey, start, ordered[0], objective, objectiveAnchor, primarySide);
        return ordered;
    }

    private static int ResolveFollowSide(
        BattleRuntimeActor actor,
        BattleGridCoord start,
        BattleGridCoord objectiveAnchor,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattleMovementReservationMap reservations,
        string steeringIntentKey)
    {
        if (actor?.MovementSteeringMode == BattleLocalSteeringMode.FollowObstacle &&
            actor.MovementSteeringSide != 0 &&
            string.Equals(actor.MovementSteeringIntentKey ?? "", steeringIntentKey ?? "", System.StringComparison.Ordinal))
        {
            if (actor.MovementSteeringBudgetRemaining <= 0)
            {
                return 0;
            }

            if (HasExecutableObstacleFollowOption(actor, start, objectiveAnchor, graph, occupancy, reservations, actor.MovementSteeringSide))
            {
                return actor.MovementSteeringSide;
            }
        }

        if (HasExecutableObstacleFollowOption(actor, start, objectiveAnchor, graph, occupancy, reservations, -1))
        {
            return -1;
        }

        return HasExecutableObstacleFollowOption(actor, start, objectiveAnchor, graph, occupancy, reservations, 1)
            ? 1
            : 0;
    }

    private static bool HasExecutableObstacleFollowOption(
        BattleRuntimeActor actor,
        BattleGridCoord start,
        BattleGridCoord objectiveAnchor,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattleMovementReservationMap reservations,
        int side)
    {
        List<BattleGridCoord> candidates = BuildObstacleFollowCandidates(start, objectiveAnchor, side);
        foreach (BattleGridCoord candidate in candidates)
        {
            if (IsExecutableFollowCandidate(actor, start, candidate, graph, occupancy, reservations))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddObstacleFollowOptions(
        List<MoveOption> options,
        BattleRuntimeActor actor,
        BattleRuntimeActor objective,
        BattleGridCoord objectiveAnchor,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattleMovementReservationMap reservations,
        BattleGridCoord start,
        int side,
        int startCenterDistance)
    {
        List<BattleGridCoord> candidates = BuildObstacleFollowCandidates(start, objectiveAnchor, side);
        for (int i = 0; i < candidates.Count; i++)
        {
            BattleGridCoord candidate = candidates[i];
            if (!IsExecutableFollowCandidate(actor, start, candidate, graph, occupancy, reservations))
            {
                continue;
            }

            int candidateCenterDistance = GetCenterManhattanDistance(actor, candidate, objective, objectiveAnchor);
            int score = ObstacleFollowPenalty +
                        i * LateralStepPenalty +
                        System.Math.Max(0, candidateCenterDistance - startCenterDistance) * 10 +
                        GetStepCost(start, candidate);
            options.Add(new MoveOption(candidate, score, candidateCenterDistance));
        }
    }

    private static List<BattleGridCoord> BuildObstacleFollowCandidates(
        BattleGridCoord start,
        BattleGridCoord objectiveAnchor,
        int side)
    {
        List<BattleGridCoord> candidates = new(3);
        int dx = objectiveAnchor.X.CompareTo(start.X);
        int dy = objectiveAnchor.Y.CompareTo(start.Y);
        bool horizontal = System.Math.Abs(objectiveAnchor.X - start.X) >= System.Math.Abs(objectiveAnchor.Y - start.Y);
        if (horizontal && dx != 0)
        {
            candidates.Add(new BattleGridCoord(start.X, start.Y + side, start.Height));
            candidates.Add(new BattleGridCoord(start.X + dx, start.Y + side, start.Height));
            candidates.Add(new BattleGridCoord(start.X - dx, start.Y + side, start.Height));
            return candidates;
        }

        if (dy != 0)
        {
            candidates.Add(new BattleGridCoord(start.X + side, start.Y, start.Height));
            candidates.Add(new BattleGridCoord(start.X + side, start.Y + dy, start.Height));
            candidates.Add(new BattleGridCoord(start.X + side, start.Y - dy, start.Height));
        }

        return candidates;
    }

    private static bool IsExecutableFollowCandidate(
        BattleRuntimeActor actor,
        BattleGridCoord start,
        BattleGridCoord candidate,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattleMovementReservationMap reservations)
    {
        return IsGraphNeighbor(graph, start, candidate) &&
               BattlePathStepRules.CanUseAnchor(actor, start, start, candidate, graph, occupancy, reservations) &&
               !IsRecentBacktrackStep(actor, start, candidate);
    }

    private static bool IsGraphNeighbor(
        BattleNavigationGraph graph,
        BattleGridCoord start,
        BattleGridCoord candidate)
    {
        foreach (BattleGridCoord neighbor in graph.GetNeighbors(start))
        {
            if (neighbor == candidate)
            {
                return true;
            }
        }

        return false;
    }

    private static void RecordSeekGoalSteering(
        BattleRuntimeActor actor,
        string steeringIntentKey,
        BattleGridCoord selected,
        BattleRuntimeActor objective,
        BattleGridCoord objectiveAnchor)
    {
        if (actor == null)
        {
            return;
        }

        actor.MovementSteeringMode = BattleLocalSteeringMode.SeekGoal;
        actor.MovementSteeringSide = 0;
        actor.MovementSteeringBestDistance = GetCenterManhattanDistance(actor, selected, objective, objectiveAnchor);
        actor.MovementSteeringBudgetRemaining = 0;
        actor.MovementSteeringIntentKey = steeringIntentKey ?? "";
    }

    private static void RecordFollowObstacleSteering(
        BattleRuntimeActor actor,
        string steeringIntentKey,
        BattleGridCoord start,
        BattleGridCoord selected,
        BattleRuntimeActor objective,
        BattleGridCoord objectiveAnchor,
        int side)
    {
        if (actor == null)
        {
            return;
        }

        int resolvedSide = side == 0 ? ResolveStepSide(start, objectiveAnchor, selected) : side;
        // The progress budget prevents a dead static wall from becoming an
        // infinite same-intent edge walk. A true rejoin goes through SeekGoal
        // first, then any later obstacle-follow starts a fresh bounded attempt.
        bool sameFollow = actor.MovementSteeringMode == BattleLocalSteeringMode.FollowObstacle &&
                          actor.MovementSteeringSide == resolvedSide &&
                          actor.MovementSteeringBudgetRemaining > 0 &&
                          string.Equals(actor.MovementSteeringIntentKey ?? "", steeringIntentKey ?? "", System.StringComparison.Ordinal);
        int selectedDistance = GetCenterManhattanDistance(actor, selected, objective, objectiveAnchor);
        int startDistance = GetCenterManhattanDistance(actor, start, objective, objectiveAnchor);
        actor.MovementSteeringMode = BattleLocalSteeringMode.FollowObstacle;
        actor.MovementSteeringSide = resolvedSide;
        actor.MovementSteeringBestDistance = sameFollow
            ? System.Math.Min(actor.MovementSteeringBestDistance, selectedDistance)
            : System.Math.Min(startDistance, selectedDistance);
        actor.MovementSteeringBudgetRemaining = sameFollow
            ? System.Math.Max(0, actor.MovementSteeringBudgetRemaining - 1)
            : LocalObjectiveFollowBudget;
        actor.MovementSteeringIntentKey = steeringIntentKey ?? "";
    }

    private static int ResolveStepSide(
        BattleGridCoord start,
        BattleGridCoord objectiveAnchor,
        BattleGridCoord selected)
    {
        bool horizontal = System.Math.Abs(objectiveAnchor.X - start.X) >= System.Math.Abs(objectiveAnchor.Y - start.Y);
        int delta = horizontal
            ? selected.Y - start.Y
            : selected.X - start.X;
        return delta.CompareTo(0);
    }

    private static string BuildObjectiveSteeringIntentKey(
        BattleRuntimeActor actor,
        BattleGridCoord objectiveAnchor)
    {
        return $"{actor?.ObjectiveZoneId ?? ""}:{objectiveAnchor.X}:{objectiveAnchor.Y}:{objectiveAnchor.Height}:{System.Math.Max(1, actor?.ObjectiveWidth ?? 1)}:{System.Math.Max(1, actor?.ObjectiveHeight ?? 1)}";
    }

    private static IReadOnlyList<BattleGridCoord> FindNextStepCandidatesTowardAnchor(
        BattleRuntimeActor actor,
        BattleGridCoord goalAnchor,
        BattleNavigationGraph graph,
        BattleDynamicOccupancy occupancy,
        BattleMovementReservationMap reservations,
        BattleTacticalRegionSnapshot localCombatRegion,
        bool allowNonImprovingLocalAvoidance = true)
    {
        if (actor == null || graph == null || occupancy == null || reservations == null)
        {
            return System.Array.Empty<BattleGridCoord>();
        }

        BattleGridCoord start = new(actor.GridX, actor.GridY, actor.GridHeight);
        if (!graph.Contains(start))
        {
            return System.Array.Empty<BattleGridCoord>();
        }

        int startDistance = GetAnchorDistance(start, goalAnchor);
        List<MoveOption> improving = new();
        List<MoveOption> lateral = new();
        List<MoveOption> detours = new();
        foreach (BattleGridCoord neighbor in graph.GetNeighbors(start))
        {
            if (!BattlePathStepRules.CanUseStaticStep(actor, start, neighbor, graph) ||
                !reservations.CanReserveMove(actor, start, neighbor, occupancy) ||
                IsRecentBacktrackStep(actor, start, neighbor))
            {
                continue;
            }

            int candidateDistance = GetAnchorDistance(neighbor, goalAnchor);
            int score = candidateDistance * FlowCostWeight +
                        BattleLocalRegionPreference.GetStepPenalty(neighbor, localCombatRegion) +
                        GetStepCost(start, neighbor);
            if (candidateDistance < startDistance)
            {
                improving.Add(new MoveOption(neighbor, score, candidateDistance));
            }
            else if (allowNonImprovingLocalAvoidance &&
                     candidateDistance == startDistance &&
                     !actor.HasSecondaryMovementBacktrackGuardCell)
            {
                // A single non-worsening side step is local obstacle avoidance,
                // not a route search. Backtrack guards prevent repeated orbiting.
                lateral.Add(new MoveOption(neighbor, score + LateralStepPenalty, candidateDistance));
            }
            else if (allowNonImprovingLocalAvoidance &&
                     localCombatRegion != null &&
                     !actor.HasMovementBacktrackGuardCell)
            {
                // Local combat may need one outward step when live footprints
                // occupy every closer entry. This is bounded obstacle avoidance,
                // not a planned route; the next boundary must validate again.
                detours.Add(new MoveOption(neighbor, score + LateralStepPenalty * 4, candidateDistance));
            }
        }

        BattleGridCoord[] ordered = SortMoveOptions(improving);
        if (ordered.Length > 0)
        {
            return ordered;
        }

        ordered = SortMoveOptions(lateral);
        return ordered.Length > 0 ? ordered : SortMoveOptions(detours);
    }

    private static bool OpensNewAxisGap(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleGridCoord start,
        BattleGridCoord candidate)
    {
        BattleGridCoord targetAnchor = new(target.GridX, target.GridY, target.GridHeight);
        BattleActorFootprint.GetAxisGaps(actor, start, target, targetAnchor, out int startGapX, out int startGapY);
        BattleActorFootprint.GetAxisGaps(actor, candidate, target, targetAnchor, out int candidateGapX, out int candidateGapY);
        return candidateGapX > startGapX || candidateGapY > startGapY;
    }

    private static bool IsBetter(MoveOption candidate, MoveOption known)
    {
        return candidate.Score < known.Score ||
               candidate.Score == known.Score && candidate.FlowCost < known.FlowCost ||
               candidate.Score == known.Score && candidate.FlowCost == known.FlowCost && IsBefore(candidate.Anchor, known.Anchor);
    }

    private static bool IsBefore(BattleGridCoord candidate, BattleGridCoord known)
    {
        return candidate.Height < known.Height ||
               candidate.Height == known.Height && candidate.Y < known.Y ||
               candidate.Height == known.Height && candidate.Y == known.Y && candidate.X < known.X;
    }

    private static int GetStepCost(BattleGridCoord from, BattleGridCoord to)
    {
        return from.X != to.X && from.Y != to.Y
            ? BattlePathCostPolicy.DiagonalStepCost
            : BattlePathCostPolicy.StepCost;
    }

    private static int GetCenterManhattanDistance(
        BattleRuntimeActor actor,
        BattleGridCoord actorAnchor,
        BattleRuntimeActor target,
        BattleGridCoord targetAnchor)
    {
        int actorCenterX2 = actorAnchor.X * 2 + BattleActorFootprint.NormalizeSize(actor?.FootprintWidth ?? 1) - 1;
        int actorCenterY2 = actorAnchor.Y * 2 + BattleActorFootprint.NormalizeSize(actor?.FootprintHeight ?? 1) - 1;
        int targetCenterX2 = targetAnchor.X * 2 + BattleActorFootprint.NormalizeSize(target?.FootprintWidth ?? 1) - 1;
        int targetCenterY2 = targetAnchor.Y * 2 + BattleActorFootprint.NormalizeSize(target?.FootprintHeight ?? 1) - 1;
        return System.Math.Abs(actorCenterX2 - targetCenterX2) +
               System.Math.Abs(actorCenterY2 - targetCenterY2);
    }

    private static int GetAnchorDistance(BattleGridCoord first, BattleGridCoord second)
    {
        int dx = System.Math.Abs(first.X - second.X);
        int dy = System.Math.Abs(first.Y - second.Y);
        int dh = System.Math.Abs(first.Height - second.Height);
        return System.Math.Max(dx, dy) * BattlePathCostPolicy.StepCost +
               System.Math.Min(dx, dy) * (BattlePathCostPolicy.DiagonalStepCost - BattlePathCostPolicy.StepCost) +
               dh * BattlePathCostPolicy.StepCost * 4;
    }

    private static BattleGridCoord[] SortMoveOptions(List<MoveOption> options)
    {
        if (options == null || options.Count == 0)
        {
            return System.Array.Empty<BattleGridCoord>();
        }

        options.Sort(MoveOptionComparer.Instance);
        BattleGridCoord[] ordered = new BattleGridCoord[options.Count];
        for (int i = 0; i < options.Count; i++)
        {
            ordered[i] = options[i].Anchor;
        }

        return ordered;
    }

    private static bool IsRecentBacktrackStep(
        BattleRuntimeActor actor,
        BattleGridCoord start,
        BattleGridCoord candidate)
    {
        BattleGridCoord? reverseStep = GetImmediateReverseStep(actor, start);
        if (reverseStep.HasValue && reverseStep.Value == candidate)
        {
            return true;
        }

        BattleGridCoord? cycleStep = GetBacktrackGuardStep(actor);
        if (cycleStep.HasValue && cycleStep.Value == candidate)
        {
            return true;
        }

        BattleGridCoord? secondaryCycleStep = GetSecondaryBacktrackGuardStep(actor);
        return secondaryCycleStep.HasValue && secondaryCycleStep.Value == candidate;
    }

    private static BattleGridCoord? GetImmediateReverseStep(
        BattleRuntimeActor actor,
        BattleGridCoord start)
    {
        if (actor == null)
        {
            return null;
        }

        BattleGridCoord previousFrom = new(
            actor.MovementFromGridX,
            actor.MovementFromGridY,
            actor.MovementFromGridHeight);
        BattleGridCoord previousTo = new(
            actor.MovementToGridX,
            actor.MovementToGridY,
            actor.MovementToGridHeight);
        if (previousFrom == previousTo || previousTo != start)
        {
            return null;
        }

        // Prefer any other currently executable route first. If the full local
        // path genuinely needs a backtrack around live footprints, the detour
        // search may still use it instead of freezing the actor in place.
        return previousFrom;
    }

    private static BattleGridCoord? GetBacktrackGuardStep(BattleRuntimeActor actor)
    {
        return actor?.HasMovementBacktrackGuardCell == true
            ? new BattleGridCoord(
                actor.MovementBacktrackGuardGridX,
                actor.MovementBacktrackGuardGridY,
                actor.MovementBacktrackGuardGridHeight)
            : null;
    }

    private static BattleGridCoord? GetSecondaryBacktrackGuardStep(BattleRuntimeActor actor)
    {
        return actor?.HasSecondaryMovementBacktrackGuardCell == true
            ? new BattleGridCoord(
                actor.SecondaryMovementBacktrackGuardGridX,
                actor.SecondaryMovementBacktrackGuardGridY,
                actor.SecondaryMovementBacktrackGuardGridHeight)
            : null;
    }

    private readonly record struct MoveOption(BattleGridCoord Anchor, int Score, int FlowCost);

    private readonly record struct LocalAvoidanceNode(
        BattleGridCoord Anchor,
        BattleGridCoord FirstStep,
        int Depth,
        int TravelCost);

    private sealed class MoveOptionComparer : IComparer<MoveOption>
    {
        public static readonly MoveOptionComparer Instance = new();

        public int Compare(MoveOption x, MoveOption y)
        {
            if (IsBetter(x, y))
            {
                return -1;
            }

            return IsBetter(y, x) ? 1 : 0;
        }
    }
}
