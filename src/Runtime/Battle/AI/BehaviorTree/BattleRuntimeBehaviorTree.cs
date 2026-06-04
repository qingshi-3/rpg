using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle.AI.BehaviorTree;

public interface IBattleRuntimeBehaviorNode
{
    BattleRuntimeBehaviorResult Tick(BattleRuntimeAiDecisionFacts facts);
}

public sealed class BattleRuntimeBehaviorResult
{
    private BattleRuntimeBehaviorResult(bool success, BattleRuntimeAiActionRequest request, string failureReason)
    {
        Success = success;
        Request = request;
        FailureReason = failureReason ?? "";
    }

    public bool Success { get; }
    public BattleRuntimeAiActionRequest Request { get; }
    public string FailureReason { get; }

    public static BattleRuntimeBehaviorResult Succeeded(BattleRuntimeAiActionRequest request)
    {
        return new BattleRuntimeBehaviorResult(true, request, "");
    }

    public static BattleRuntimeBehaviorResult Failed(string failureReason)
    {
        return new BattleRuntimeBehaviorResult(false, null, failureReason);
    }
}

public static class BattleRuntimeBehaviorNode
{
    public static IBattleRuntimeBehaviorNode Selector(params IBattleRuntimeBehaviorNode[] children)
    {
        return new SelectorNode(children);
    }

    public static IBattleRuntimeBehaviorNode Sequence(params IBattleRuntimeBehaviorNode[] children)
    {
        return new SequenceNode(children);
    }

    public static IBattleRuntimeBehaviorNode Condition(
        Func<BattleRuntimeAiDecisionFacts, bool> predicate,
        string failureReason)
    {
        return new ConditionNode(predicate, failureReason);
    }

    public static IBattleRuntimeBehaviorNode Action(
        Func<BattleRuntimeAiDecisionFacts, BattleRuntimeAiActionRequest> action,
        string failureReason = "missing_ai_request")
    {
        return new ActionNode(action, failureReason);
    }

    private sealed class SelectorNode : IBattleRuntimeBehaviorNode
    {
        private readonly IReadOnlyList<IBattleRuntimeBehaviorNode> _children;

        public SelectorNode(IEnumerable<IBattleRuntimeBehaviorNode> children)
        {
            _children = (children ?? Array.Empty<IBattleRuntimeBehaviorNode>())
                .Where(item => item != null)
                .ToArray();
        }

        public BattleRuntimeBehaviorResult Tick(BattleRuntimeAiDecisionFacts facts)
        {
            string failureReason = "selector_no_match";
            foreach (IBattleRuntimeBehaviorNode child in _children)
            {
                BattleRuntimeBehaviorResult result = child.Tick(facts);
                if (result.Success)
                {
                    return result;
                }

                if (!string.IsNullOrWhiteSpace(result.FailureReason))
                {
                    failureReason = result.FailureReason;
                }
            }

            return BattleRuntimeBehaviorResult.Failed(failureReason);
        }
    }

    private sealed class SequenceNode : IBattleRuntimeBehaviorNode
    {
        private readonly IReadOnlyList<IBattleRuntimeBehaviorNode> _children;

        public SequenceNode(IEnumerable<IBattleRuntimeBehaviorNode> children)
        {
            _children = (children ?? Array.Empty<IBattleRuntimeBehaviorNode>())
                .Where(item => item != null)
                .ToArray();
        }

        public BattleRuntimeBehaviorResult Tick(BattleRuntimeAiDecisionFacts facts)
        {
            BattleRuntimeBehaviorResult lastResult = BattleRuntimeBehaviorResult.Succeeded(null);
            foreach (IBattleRuntimeBehaviorNode child in _children)
            {
                lastResult = child.Tick(facts);
                if (!lastResult.Success)
                {
                    return lastResult;
                }
            }

            return lastResult;
        }
    }

    private sealed class ConditionNode : IBattleRuntimeBehaviorNode
    {
        private readonly Func<BattleRuntimeAiDecisionFacts, bool> _predicate;
        private readonly string _failureReason;

        public ConditionNode(Func<BattleRuntimeAiDecisionFacts, bool> predicate, string failureReason)
        {
            _predicate = predicate;
            _failureReason = failureReason ?? "condition_failed";
        }

        public BattleRuntimeBehaviorResult Tick(BattleRuntimeAiDecisionFacts facts)
        {
            return _predicate?.Invoke(facts ?? new BattleRuntimeAiDecisionFacts()) == true
                ? BattleRuntimeBehaviorResult.Succeeded(null)
                : BattleRuntimeBehaviorResult.Failed(_failureReason);
        }
    }

    private sealed class ActionNode : IBattleRuntimeBehaviorNode
    {
        private readonly Func<BattleRuntimeAiDecisionFacts, BattleRuntimeAiActionRequest> _action;
        private readonly string _failureReason;

        public ActionNode(Func<BattleRuntimeAiDecisionFacts, BattleRuntimeAiActionRequest> action, string failureReason)
        {
            _action = action;
            _failureReason = failureReason ?? "missing_ai_request";
        }

        public BattleRuntimeBehaviorResult Tick(BattleRuntimeAiDecisionFacts facts)
        {
            BattleRuntimeAiActionRequest request = _action?.Invoke(facts ?? new BattleRuntimeAiDecisionFacts());
            return request != null
                ? BattleRuntimeBehaviorResult.Succeeded(request)
                : BattleRuntimeBehaviorResult.Failed(_failureReason);
        }
    }
}

public sealed class BattleRuntimeBehaviorTreeExecutor : IBattleRuntimeAiExecutor
{
    private readonly IBattleRuntimeBehaviorNode _root;

    private BattleRuntimeBehaviorTreeExecutor(IBattleRuntimeBehaviorNode root)
    {
        _root = root ?? BattleRuntimeBehaviorNode.Action(
            facts => BattleRuntimeAiActionRequest.Hold(facts?.ActorId ?? "", "missing_behavior_tree"));
    }

    public static BattleRuntimeBehaviorTreeExecutor CreateDefault()
    {
        // The Runtime tree mirrors the authored LimboAI model: conditions select
        // intent, actions emit typed requests, and Runtime validators own truth.
        return new BattleRuntimeBehaviorTreeExecutor(BattleRuntimeBehaviorNode.Selector(
            BattleRuntimeBehaviorNode.Sequence(
                BattleRuntimeBehaviorNode.Condition(HasMissingActor, "actor_present"),
                BattleRuntimeBehaviorNode.Action(_ => BattleRuntimeAiActionRequest.Hold("", "missing_actor"))),
            BattleRuntimeBehaviorNode.Sequence(
                BattleRuntimeBehaviorNode.Condition(HasNoSelectedTarget, "target_present"),
                BattleRuntimeBehaviorNode.Action(facts => BattleRuntimeAiActionRequest.Hold(facts.ActorId, "no_target"))),
            BattleRuntimeBehaviorNode.Sequence(
                BattleRuntimeBehaviorNode.Condition(IsOutsideLocalCombatLeash, "inside_local_combat_leash"),
                BattleRuntimeBehaviorNode.Action(facts => BattleRuntimeAiActionRequest.Hold(
                    facts.ActorId,
                    string.IsNullOrWhiteSpace(facts.LocalCombatRejectReasonCode)
                        ? "reject_local_combat"
                        : facts.LocalCombatRejectReasonCode))),
            BattleRuntimeBehaviorNode.Sequence(
                BattleRuntimeBehaviorNode.Condition(facts => IsInAttackRange(facts) && facts?.CanAttackNow == true, "attack_not_ready"),
                BattleRuntimeBehaviorNode.Action(facts => BattleRuntimeAiActionRequest.AttackTarget(
                    facts.ActorId,
                    SelectTarget(facts).ActorId))),
            BattleRuntimeBehaviorNode.Sequence(
                BattleRuntimeBehaviorNode.Condition(IsInAttackRange, "target_out_of_attack_range"),
                BattleRuntimeBehaviorNode.Action(facts => BattleRuntimeAiActionRequest.WaitForAttackCharge(
                    facts.ActorId,
                    SelectTarget(facts).ActorId))),
            BattleRuntimeBehaviorNode.Sequence(
                BattleRuntimeBehaviorNode.Condition(ShouldJoinReachableAttackSlot, "no_reachable_attack_slot"),
                BattleRuntimeBehaviorNode.Action(facts => BattleRuntimeAiActionRequest.JoinLocalCombat(
                    facts.ActorId,
                    SelectLocalCombatTargetId(facts),
                    facts.LocalCombatJoinReasonCode,
                    facts.LocalCombatSituationId))),
            BattleRuntimeBehaviorNode.Sequence(
                BattleRuntimeBehaviorNode.Condition(ShouldHoldReachableSupportSlot, "no_reachable_support_slot"),
                BattleRuntimeBehaviorNode.Action(facts => BattleRuntimeAiActionRequest.HoldSupport(
                    facts.ActorId,
                    SelectLocalCombatTargetId(facts),
                    facts.LocalCombatSupportReasonCode,
                    facts.LocalCombatSituationId))),
            BattleRuntimeBehaviorNode.Sequence(
                BattleRuntimeBehaviorNode.Condition(ShouldHoldBlockedLocalCombat, "local_combat_has_slot"),
                BattleRuntimeBehaviorNode.Action(facts => BattleRuntimeAiActionRequest.Hold(
                    facts.ActorId,
                    BattleGroupTacticalReasonCode.LocalRegionDegradeNoReachableSlot))),
            BattleRuntimeBehaviorNode.Sequence(
                BattleRuntimeBehaviorNode.Condition(IsOutOfAttackRange, "target_in_attack_range"),
                BattleRuntimeBehaviorNode.Action(facts => BattleRuntimeAiActionRequest.AdvanceTowardTarget(
                    facts.ActorId,
                    SelectTarget(facts).ActorId))),
            BattleRuntimeBehaviorNode.Action(facts => BattleRuntimeAiActionRequest.WaitForAttackCharge(
                facts.ActorId,
                SelectTarget(facts).ActorId))));
    }

    public BattleRuntimeAiActionRequest ChooseAction(BattleRuntimeAiDecisionFacts facts)
    {
        BattleRuntimeAiDecisionFacts safeFacts = facts ?? new BattleRuntimeAiDecisionFacts();
        BattleRuntimeBehaviorResult result = _root.Tick(safeFacts);
        if (result.Request != null)
        {
            return result.Request;
        }

        string reason = string.IsNullOrWhiteSpace(result.FailureReason)
            ? "missing_ai_request"
            : result.FailureReason;
        return BattleRuntimeAiActionRequest.Hold(safeFacts.ActorId, reason);
    }

    private static bool HasMissingActor(BattleRuntimeAiDecisionFacts facts)
    {
        return string.IsNullOrWhiteSpace(facts?.ActorId);
    }

    private static bool HasNoSelectedTarget(BattleRuntimeAiDecisionFacts facts)
    {
        return string.IsNullOrWhiteSpace(SelectTarget(facts).ActorId);
    }

    private static bool IsOutsideLocalCombatLeash(BattleRuntimeAiDecisionFacts facts)
    {
        return facts?.HasLocalCombatSituation == true && !facts.LocalCombatInsideLeash;
    }

    private static bool ShouldJoinReachableAttackSlot(BattleRuntimeAiDecisionFacts facts)
    {
        return IsOutOfAttackRange(facts) &&
               facts.HasLocalCombatSituation &&
               facts.LocalCombatHasReachableAttackSlot &&
               !string.IsNullOrWhiteSpace(SelectLocalCombatTargetId(facts));
    }

    private static bool ShouldHoldReachableSupportSlot(BattleRuntimeAiDecisionFacts facts)
    {
        return IsOutOfAttackRange(facts) &&
               facts.HasLocalCombatSituation &&
               facts.LocalCombatHasReachableSupportSlot &&
               !string.IsNullOrWhiteSpace(SelectLocalCombatTargetId(facts));
    }

    private static bool ShouldHoldBlockedLocalCombat(BattleRuntimeAiDecisionFacts facts)
    {
        return IsOutOfAttackRange(facts) &&
               facts.HasLocalCombatSituation &&
               !facts.LocalCombatHasReachableAttackSlot &&
               !facts.LocalCombatHasReachableSupportSlot &&
               !string.IsNullOrWhiteSpace(SelectLocalCombatTargetId(facts));
    }

    private static bool IsInAttackRange(BattleRuntimeAiDecisionFacts facts)
    {
        ResolvedTarget target = SelectTarget(facts);
        return !string.IsNullOrWhiteSpace(target.ActorId) &&
               target.DistanceToTarget <= Math.Max(1, facts?.AttackRange ?? 1);
    }

    private static bool IsOutOfAttackRange(BattleRuntimeAiDecisionFacts facts)
    {
        ResolvedTarget target = SelectTarget(facts);
        return facts != null &&
               !string.IsNullOrWhiteSpace(target.ActorId) &&
               target.DistanceToTarget > Math.Max(1, facts.AttackRange);
    }

    private static string SelectLocalCombatTargetId(BattleRuntimeAiDecisionFacts facts)
    {
        string selected = SelectTarget(facts).ActorId;
        return string.IsNullOrWhiteSpace(selected)
            ? facts?.LocalCombatTargetActorId ?? ""
            : selected;
    }

    private static ResolvedTarget SelectTarget(BattleRuntimeAiDecisionFacts facts)
    {
        if (facts == null)
        {
            return default;
        }

        BattleRuntimeAiTargetCandidateFacts candidate = SelectTargetCandidate(facts);
        if (candidate != null)
        {
            int distance = candidate.OrthogonalAttackGap == int.MaxValue
                ? int.MaxValue
                : candidate.OrthogonalAttackGap;
            return new ResolvedTarget(candidate.ActorId ?? "", distance);
        }

        return facts.HasTarget && !string.IsNullOrWhiteSpace(facts.TargetActorId)
            ? new ResolvedTarget(facts.TargetActorId, facts.DistanceToTarget)
            : default;
    }

    private static BattleRuntimeAiTargetCandidateFacts SelectTargetCandidate(BattleRuntimeAiDecisionFacts facts)
    {
        BattleRuntimeAiTargetCandidateFacts[] candidates = (facts?.TargetCandidates ?? new List<BattleRuntimeAiTargetCandidateFacts>())
            .Where(item => item != null && !string.IsNullOrWhiteSpace(item.ActorId))
            .ToArray();
        if (candidates.Length == 0)
        {
            return null;
        }

        if (string.Equals(
                facts.TargetSelectionPolicy,
                BattleRuntimeAiTargetSelectionPolicy.FocusFire,
                StringComparison.Ordinal))
        {
            return candidates
                .OrderBy(item => NormalizeScore(item.HitPoints))
                .ThenBy(item => NormalizeScore(item.GridGap))
                .ThenBy(item => NormalizeScore(item.CenterManhattanDistance))
                .ThenBy(item => item.ActorId, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        return candidates
            .OrderBy(item => NormalizeScore(item.SelectionTier))
            .ThenBy(item => NormalizeScore(item.TravelCost))
            .ThenBy(item => NormalizeScore(item.OrthogonalAttackGap))
            .ThenBy(item => NormalizeScore(item.GridGap))
            // Frontage distance must beat actor id, otherwise line contacts
            // collapse onto the lexicographically first enemy.
            .ThenBy(item => NormalizeScore(item.CenterManhattanDistance))
            .ThenBy(item => NormalizeScore(item.HitPoints))
            .ThenBy(item => item.ActorId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static int NormalizeScore(int value)
    {
        return value < 0 ? int.MaxValue : value;
    }

    private readonly record struct ResolvedTarget(string ActorId, int DistanceToTarget);
}
