using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Definitions.Characters;
using Rpg.Definitions.Emotion;
using Rpg.Definitions.World;
using Rpg.Domain.Emotion;

namespace Rpg.Application.Emotion;

public sealed partial class EmotionSystem
{
    public EmotionOperationResult<EmotionRecruitmentResult> EvaluateRecruitment(EmotionRecruitmentQuery query)
    {
        if (query == null)
        {
            return EmotionOperationResult<EmotionRecruitmentResult>.Fail("Recruitment query is null.");
        }

        if (string.IsNullOrWhiteSpace(query.RecruiterFactionId))
        {
            return EmotionOperationResult<EmotionRecruitmentResult>.Fail("Recruiter faction id is required.");
        }

        EmotionOperationResult<EmotionSocialContext> contextResult = BuildSocialContext(query.ActorId, query.RecruiterFactionId);
        if (!contextResult.Success)
        {
            return EmotionOperationResult<EmotionRecruitmentResult>.Fail(contextResult.Error);
        }

        EmotionSocialContext context = contextResult.Value;
        List<EmotionScoreFactor> factors = new();
        int difficulty = Math.Clamp(query.Difficulty, 0, 100);
        int offerValue = Clamp(query.OfferValue);
        int loyalty = context.Actor.GetTrait(EmotionAxis.Loyalty);
        int helpfulness = context.Actor.GetTrait(EmotionAxis.Helpfulness);
        int empathy = context.Actor.GetTrait(EmotionAxis.Empathy);
        int aggression = context.Actor.GetTrait(EmotionAxis.Aggression);

        AddFactor(factors, "disposition", context.Disposition.Score);
        AddFactor(factors, "trust", context.Trust);
        AddFactor(factors, "affinity", context.Affinity / 2);
        AddFactor(factors, "respect", context.Respect / 3);
        AddFactor(factors, "fear", -context.Fear / 3);
        AddFactor(factors, "grievance", -context.Grievance);
        AddFactor(factors, "loyalty", loyalty / 3);
        AddFactor(factors, "helpfulness", helpfulness / 4);
        AddFactor(factors, "empathy", empathy / 4);
        AddFactor(factors, "aggression", -aggression / 5);
        AddFactor(factors, "offer", offerValue);
        AddFactor(factors, "difficulty", -difficulty);

        int score = Clamp(factors.Sum(factor => factor.Amount));
        int chanceModifier = Math.Clamp(score / 2, -80, 80);
        List<string> missingTags = FindMissingMemoryTags(context.Actor, query.RequiredMemoryTags);
        List<string> blockingTags = FindPresentMemoryTags(context.Actor, query.BlockingMemoryTags);
        string blockReason = BuildRecruitmentBlockReason(context, score, missingTags, blockingTags);

        return EmotionOperationResult<EmotionRecruitmentResult>.Ok(new EmotionRecruitmentResult(
            query.ActorId,
            query.RecruiterFactionId,
            string.IsNullOrEmpty(blockReason),
            score,
            chanceModifier,
            blockReason,
            context.Disposition,
            factors,
            missingTags,
            blockingTags));
    }

    public EmotionOperationResult<EmotionTaskAssignmentResult> EvaluateTaskAssignment(EmotionTaskAssignmentQuery query)
    {
        if (query == null)
        {
            return EmotionOperationResult<EmotionTaskAssignmentResult>.Fail("Task assignment query is null.");
        }

        EmotionOperationResult<EmotionSocialContext> contextResult = BuildSocialContext(query.ActorId, query.TargetId);
        if (!contextResult.Success)
        {
            return EmotionOperationResult<EmotionTaskAssignmentResult>.Fail(contextResult.Error);
        }

        EmotionSocialContext context = contextResult.Value;
        List<EmotionScoreFactor> factors = new();
        int difficulty = Math.Clamp(query.Difficulty, 0, 100);
        int danger = Math.Clamp(query.Danger, 0, 100);
        int loyalty = context.Actor.GetTrait(EmotionAxis.Loyalty);
        int taskFit = GetTaskFit(context.Actor, query.TaskKind);
        int forcedPenalty = query.IsForced ? -20 : 0;

        AddFactor(factors, "disposition", context.Disposition.Score / 3);
        AddFactor(factors, "trust", context.Trust / 4);
        AddFactor(factors, "loyalty", loyalty / 3);
        AddFactor(factors, "task_fit", taskFit);
        AddFactor(factors, "difficulty", -difficulty / 2);
        AddFactor(factors, "danger", -danger / 2);
        AddFactor(factors, "forced", forcedPenalty);

        int score = Clamp(factors.Sum(factor => factor.Amount));
        int efficiencyModifier = Math.Clamp(score / 2, -50, 50);
        int loyaltyRiskDelta = Math.Clamp(
            danger / 3 + difficulty / 4 + (query.IsForced ? 20 : 0) + context.Grievance / 5 - loyalty / 4 - context.Trust / 5,
            -50,
            50);
        string blockReason = BuildTaskAssignmentBlockReason(context, query.IsForced);

        return EmotionOperationResult<EmotionTaskAssignmentResult>.Ok(new EmotionTaskAssignmentResult(
            query.ActorId,
            query.TargetId,
            query.TaskKind,
            string.IsNullOrEmpty(blockReason),
            score,
            efficiencyModifier,
            loyaltyRiskDelta,
            blockReason,
            factors));
    }

    public EmotionOperationResult<EmotionLoyaltyResult> EvaluateLoyalty(EmotionLoyaltyQuery query)
    {
        if (query == null)
        {
            return EmotionOperationResult<EmotionLoyaltyResult>.Fail("Loyalty query is null.");
        }

        EmotionOperationResult<EmotionSocialContext> contextResult = BuildSocialContext(query.ActorId, query.FactionId);
        if (!contextResult.Success)
        {
            return EmotionOperationResult<EmotionLoyaltyResult>.Fail(contextResult.Error);
        }

        EmotionSocialContext context = contextResult.Value;
        List<EmotionScoreFactor> factors = new();
        int loyalty = context.Actor.GetTrait(EmotionAxis.Loyalty);
        int pressure = Math.Clamp(query.Pressure, -100, 100);

        AddFactor(factors, "base_risk", 50);
        AddFactor(factors, "pressure", pressure);
        AddFactor(factors, "grievance", context.Grievance / 2);
        AddFactor(factors, "fear", context.Fear / 3);
        AddFactor(factors, "loyalty", -loyalty / 2);
        AddFactor(factors, "trust", -context.Trust / 3);
        AddFactor(factors, "respect", -context.Respect / 4);
        AddFactor(factors, "disposition", -context.Disposition.Score / 3);

        int riskScore = Math.Clamp(factors.Sum(factor => factor.Amount), 0, 100);
        EmotionLoyaltyRiskLevel riskLevel = ToLoyaltyRiskLevel(riskScore);
        int desertionModifier = Math.Clamp(riskScore - 50, -50, 50);

        return EmotionOperationResult<EmotionLoyaltyResult>.Ok(new EmotionLoyaltyResult(
            query.ActorId,
            query.FactionId,
            riskScore,
            riskLevel,
            desertionModifier,
            factors));
    }

    public EmotionOperationResult<EmotionBattleSupportResult> EvaluateBattleSupport(EmotionBattleSupportQuery query)
    {
        if (query == null)
        {
            return EmotionOperationResult<EmotionBattleSupportResult>.Fail("Battle support query is null.");
        }

        EmotionOperationResult<EmotionSocialContext> contextResult = BuildSocialContext(query.ActorId, query.SupportedFactionId);
        if (!contextResult.Success)
        {
            return EmotionOperationResult<EmotionBattleSupportResult>.Fail(contextResult.Error);
        }

        EmotionSocialContext context = contextResult.Value;
        List<EmotionScoreFactor> factors = new();
        int battleRisk = Math.Clamp(query.BattleRisk, 0, 100);
        int supportCost = Math.Clamp(query.SupportCost, 0, 100);
        int loyalty = context.Actor.GetTrait(EmotionAxis.Loyalty);
        int courage = context.Actor.GetTrait(EmotionAxis.Courage);

        AddFactor(factors, "disposition", context.Disposition.Score / 2);
        AddFactor(factors, "loyalty", loyalty / 2);
        AddFactor(factors, "courage", courage / 3);
        AddFactor(factors, "trust", context.Trust / 3);
        AddFactor(factors, "respect", context.Respect / 4);
        AddFactor(factors, "fear", -context.Fear / 3);
        AddFactor(factors, "grievance", -context.Grievance / 2);
        AddFactor(factors, "battle_risk", -battleRisk / 2);
        AddFactor(factors, "support_cost", -supportCost / 2);

        int score = Clamp(factors.Sum(factor => factor.Amount));
        int supportChanceModifier = Math.Clamp(score / 2, -60, 60);
        int moraleModifier = Math.Clamp((courage + loyalty + context.Disposition.Score) / 10, -20, 20);
        string blockReason = BuildBattleSupportBlockReason(context, score);

        return EmotionOperationResult<EmotionBattleSupportResult>.Ok(new EmotionBattleSupportResult(
            query.ActorId,
            query.SupportedFactionId,
            string.IsNullOrEmpty(blockReason),
            score,
            supportChanceModifier,
            moraleModifier,
            blockReason,
            factors));
    }

    public EmotionOperationResult<EmotionRelationshipGateResult> EvaluateRelationshipGate(EmotionRelationshipGateQuery query)
    {
        if (query == null)
        {
            return EmotionOperationResult<EmotionRelationshipGateResult>.Fail("Relationship gate query is null.");
        }

        EmotionOperationResult<EmotionSocialContext> contextResult = BuildSocialContext(query.ActorId, query.TargetId);
        if (!contextResult.Success)
        {
            return EmotionOperationResult<EmotionRelationshipGateResult>.Fail(contextResult.Error);
        }

        EmotionSocialContext context = contextResult.Value;
        List<EmotionScoreFactor> factors = new();
        int score = BuildRelationshipGateScore(context, query.Kind, factors);
        int requiredScore = query.RequiredScoreOverride ?? GetDefaultGateRequiredScore(query.Kind);
        List<string> missingTags = FindMissingMemoryTags(context.Actor, query.RequiredMemoryTags);
        List<string> blockingTags = FindPresentMemoryTags(context.Actor, query.BlockingMemoryTags);
        string blockReason = BuildRelationshipGateBlockReason(query.Kind, context, score, requiredScore, missingTags, blockingTags);

        return EmotionOperationResult<EmotionRelationshipGateResult>.Ok(new EmotionRelationshipGateResult(
            query.ActorId,
            query.TargetId,
            query.Kind,
            string.IsNullOrEmpty(blockReason),
            score,
            requiredScore,
            blockReason,
            factors,
            missingTags,
            blockingTags));
    }

    public EmotionOperationResult<EmotionEventReactionResult> EvaluateEventReaction(EmotionEventReactionQuery query)
    {
        if (query == null)
        {
            return EmotionOperationResult<EmotionEventReactionResult>.Fail("Event reaction query is null.");
        }

        EmotionOperationResult<EmotionSocialContext> contextResult = BuildSocialContext(query.ActorId, query.TargetId);
        if (!contextResult.Success)
        {
            return EmotionOperationResult<EmotionEventReactionResult>.Fail(contextResult.Error);
        }

        EmotionSocialContext context = contextResult.Value;
        List<EmotionScoreFactor> factors = new();
        int baseScore = Clamp(query.BaseScore);
        int reward = Math.Clamp(query.RewardValue, -100, 100);
        int risk = Math.Clamp(query.Risk, 0, 100);

        AddFactor(factors, "base", baseScore);
        AddFactor(factors, "disposition", context.Disposition.Score / 3);
        AddFactor(factors, "reward", reward / 2);
        AddFactor(factors, "risk", -risk / 3);
        AddFactor(factors, "event_kind", GetEventKindReactionBaseline(context, query.EventKind));

        foreach (EmotionAxisWeight weight in query.TraitWeights)
        {
            if (weight == null)
            {
                continue;
            }

            AddFactor(factors, $"trait:{weight.Axis}", context.Actor.GetTrait(weight.Axis) * weight.Weight / 100);
        }

        foreach (EmotionRelationshipMetricWeight weight in query.RelationshipWeights)
        {
            if (weight == null)
            {
                continue;
            }

            AddFactor(factors, $"relationship:{weight.Metric}", context.Relationship.Get(weight.Metric) * weight.Weight / 100);
        }

        int score = Clamp(factors.Sum(factor => factor.Amount));
        EmotionEventReactionTone tone = ToEventReactionTone(score);
        int relationshipDeltaPreview = Math.Clamp(score / 10, -10, 10);

        return EmotionOperationResult<EmotionEventReactionResult>.Ok(new EmotionEventReactionResult(
            query.ActorId,
            query.TargetId,
            query.EventKind,
            score,
            tone,
            relationshipDeltaPreview,
            factors));
    }

    private EmotionOperationResult<EmotionSocialContext> BuildSocialContext(string actorId, string targetId)
    {
        if (string.IsNullOrWhiteSpace(actorId))
        {
            return EmotionOperationResult<EmotionSocialContext>.Fail("Actor id is required.");
        }

        EmotionActorState actor = _state.GetActor(actorId);
        if (actor == null)
        {
            return EmotionOperationResult<EmotionSocialContext>.Fail($"Actor '{actorId}' was not found.");
        }

        string resolvedTargetId = targetId ?? "";
        EmotionRelationshipState relationship = actor.GetRelationship(resolvedTargetId) ?? new EmotionRelationshipState(resolvedTargetId);
        EmotionOperationResult<EmotionDispositionResult> disposition = EvaluateDisposition(actorId, resolvedTargetId);
        if (!disposition.Success)
        {
            return EmotionOperationResult<EmotionSocialContext>.Fail(disposition.Error);
        }

        return EmotionOperationResult<EmotionSocialContext>.Ok(new EmotionSocialContext(actor, relationship, disposition.Value));
    }

    private static string BuildRecruitmentBlockReason(
        EmotionSocialContext context,
        int score,
        IReadOnlyCollection<string> missingTags,
        IReadOnlyCollection<string> blockingTags)
    {
        if (missingTags.Count > 0)
        {
            return "missing_required_memory";
        }

        if (blockingTags.Count > 0)
        {
            return "blocking_memory";
        }

        if (context.Disposition.Disposition == EmotionDisposition.Hostile)
        {
            return "hostile_disposition";
        }

        if (context.Grievance >= 70)
        {
            return "high_grievance";
        }

        return score < -20 ? "low_emotion_score" : "";
    }

    private static string BuildTaskAssignmentBlockReason(EmotionSocialContext context, bool isForced)
    {
        if (context.Disposition.Disposition == EmotionDisposition.Hostile)
        {
            return "hostile_disposition";
        }

        if (context.Grievance >= 75)
        {
            return "high_grievance";
        }

        return isForced && context.Trust < -30 ? "forced_low_trust" : "";
    }

    private static string BuildBattleSupportBlockReason(EmotionSocialContext context, int score)
    {
        if (context.Disposition.Disposition < EmotionDisposition.Neutral)
        {
            return "low_disposition";
        }

        if (context.Grievance >= 75)
        {
            return "high_grievance";
        }

        return score < -25 ? "low_support_score" : "";
    }

    private static EmotionLoyaltyRiskLevel ToLoyaltyRiskLevel(int riskScore)
    {
        return riskScore switch
        {
            < 20 => EmotionLoyaltyRiskLevel.Stable,
            < 45 => EmotionLoyaltyRiskLevel.Watch,
            < 70 => EmotionLoyaltyRiskLevel.AtRisk,
            _ => EmotionLoyaltyRiskLevel.Breaking
        };
    }

    private static int GetTaskFit(EmotionActorState actor, WorldTaskKind taskKind)
    {
        return taskKind switch
        {
            WorldTaskKind.Explore => actor.GetTrait(EmotionAxis.Curiosity) / 2 + actor.GetTrait(EmotionAxis.Courage) / 4,
            WorldTaskKind.Gather => actor.GetTrait(EmotionAxis.Order) / 4 + actor.GetTrait(EmotionAxis.Helpfulness) / 4,
            WorldTaskKind.Mine => actor.GetTrait(EmotionAxis.Order) / 3 + actor.GetTrait(EmotionAxis.Courage) / 5,
            WorldTaskKind.Hunt => actor.GetTrait(EmotionAxis.Courage) / 4 + actor.GetTrait(EmotionAxis.Aggression) / 5 + actor.GetTrait(EmotionAxis.Curiosity) / 5,
            WorldTaskKind.Guard => actor.GetTrait(EmotionAxis.Loyalty) / 3 + actor.GetTrait(EmotionAxis.Order) / 3 + actor.GetTrait(EmotionAxis.Courage) / 5,
            WorldTaskKind.Patrol => actor.GetTrait(EmotionAxis.Order) / 4 + actor.GetTrait(EmotionAxis.Courage) / 4 + actor.GetTrait(EmotionAxis.Curiosity) / 5,
            WorldTaskKind.Escort => actor.GetTrait(EmotionAxis.Helpfulness) / 3 + actor.GetTrait(EmotionAxis.Empathy) / 4 + actor.GetTrait(EmotionAxis.Courage) / 5,
            WorldTaskKind.Build => actor.GetTrait(EmotionAxis.Order) / 3 + actor.GetTrait(EmotionAxis.Helpfulness) / 5,
            WorldTaskKind.Craft => actor.GetTrait(EmotionAxis.Order) / 3 + actor.GetTrait(EmotionAxis.Curiosity) / 5,
            WorldTaskKind.Diplomacy => actor.GetTrait(EmotionAxis.Empathy) / 3 + actor.GetTrait(EmotionAxis.Honor) / 4 - actor.GetTrait(EmotionAxis.Aggression) / 5,
            WorldTaskKind.Rescue => actor.GetTrait(EmotionAxis.Helpfulness) / 3 + actor.GetTrait(EmotionAxis.Empathy) / 3 + actor.GetTrait(EmotionAxis.Courage) / 4,
            WorldTaskKind.Raid => actor.GetTrait(EmotionAxis.Aggression) / 3 + actor.GetTrait(EmotionAxis.Courage) / 4 - actor.GetTrait(EmotionAxis.Empathy) / 5,
            WorldTaskKind.Research => actor.GetTrait(EmotionAxis.Curiosity) / 3 + actor.GetTrait(EmotionAxis.Order) / 5,
            _ => 0
        };
    }

    private static int BuildRelationshipGateScore(
        EmotionSocialContext context,
        EmotionRelationshipGateKind kind,
        List<EmotionScoreFactor> factors)
    {
        switch (kind)
        {
            case EmotionRelationshipGateKind.Recruitment:
                AddFactor(factors, "disposition", context.Disposition.Score);
                AddFactor(factors, "trust", context.Trust / 2);
                AddFactor(factors, "respect", context.Respect / 3);
                AddFactor(factors, "grievance", -context.Grievance / 2);
                break;

            case EmotionRelationshipGateKind.Friendship:
                AddFactor(factors, "disposition", context.Disposition.Score);
                AddFactor(factors, "affinity", context.Affinity);
                AddFactor(factors, "trust", context.Trust / 2);
                AddFactor(factors, "grievance", -context.Grievance);
                break;

            case EmotionRelationshipGateKind.Romance:
                AddFactor(factors, "disposition", context.Disposition.Score);
                AddFactor(factors, "affinity", context.Affinity);
                AddFactor(factors, "trust", context.Trust / 2);
                AddFactor(factors, "respect", context.Respect / 4);
                AddFactor(factors, "fear", -context.Fear);
                AddFactor(factors, "grievance", -context.Grievance);
                AddFactor(factors, "aggression", -context.Actor.GetTrait(EmotionAxis.Aggression) / 4);
                break;

            case EmotionRelationshipGateKind.Bond:
                AddFactor(factors, "disposition", context.Disposition.Score);
                AddFactor(factors, "trust", context.Trust);
                AddFactor(factors, "respect", context.Respect);
                AddFactor(factors, "loyalty", context.Actor.GetTrait(EmotionAxis.Loyalty) / 2);
                AddFactor(factors, "affinity", context.Affinity / 2);
                AddFactor(factors, "grievance", -context.Grievance);
                break;

            case EmotionRelationshipGateKind.PersonalQuest:
                AddFactor(factors, "disposition", context.Disposition.Score / 2);
                AddFactor(factors, "trust", context.Trust);
                AddFactor(factors, "respect", context.Respect / 2);
                AddFactor(factors, "curiosity", context.Actor.GetTrait(EmotionAxis.Curiosity) / 3);
                AddFactor(factors, "grievance", -context.Grievance / 2);
                break;

            case EmotionRelationshipGateKind.SensitiveCommand:
                AddFactor(factors, "trust", context.Trust);
                AddFactor(factors, "respect", context.Respect);
                AddFactor(factors, "loyalty", context.Actor.GetTrait(EmotionAxis.Loyalty));
                AddFactor(factors, "order", context.Actor.GetTrait(EmotionAxis.Order) / 3);
                AddFactor(factors, "fear", -context.Fear / 2);
                AddFactor(factors, "grievance", -context.Grievance);
                break;

            case EmotionRelationshipGateKind.StoryChoice:
                AddFactor(factors, "disposition", context.Disposition.Score);
                AddFactor(factors, "trust", context.Trust / 2);
                AddFactor(factors, "empathy", context.Actor.GetTrait(EmotionAxis.Empathy) / 3);
                AddFactor(factors, "honor", context.Actor.GetTrait(EmotionAxis.Honor) / 3);
                AddFactor(factors, "prejudice", -context.Actor.GetTrait(EmotionAxis.Prejudice) / 4);
                break;
        }

        return Clamp(factors.Sum(factor => factor.Amount));
    }

    private static int GetDefaultGateRequiredScore(EmotionRelationshipGateKind kind)
    {
        return kind switch
        {
            EmotionRelationshipGateKind.Recruitment => 15,
            EmotionRelationshipGateKind.Friendship => 45,
            EmotionRelationshipGateKind.Romance => 60,
            EmotionRelationshipGateKind.Bond => 70,
            EmotionRelationshipGateKind.PersonalQuest => 50,
            EmotionRelationshipGateKind.SensitiveCommand => 55,
            EmotionRelationshipGateKind.StoryChoice => 30,
            _ => 30
        };
    }

    private static string BuildRelationshipGateBlockReason(
        EmotionRelationshipGateKind kind,
        EmotionSocialContext context,
        int score,
        int requiredScore,
        IReadOnlyCollection<string> missingTags,
        IReadOnlyCollection<string> blockingTags)
    {
        if (missingTags.Count > 0)
        {
            return "missing_required_memory";
        }

        if (blockingTags.Count > 0)
        {
            return "blocking_memory";
        }

        if (kind == EmotionRelationshipGateKind.Romance && (context.Fear > 30 || context.Grievance > 20))
        {
            return "unsafe_relationship_state";
        }

        if (kind == EmotionRelationshipGateKind.Bond && context.Trust < 40)
        {
            return "low_trust";
        }

        if (kind == EmotionRelationshipGateKind.SensitiveCommand && context.Trust + context.Actor.GetTrait(EmotionAxis.Loyalty) < 20)
        {
            return "low_command_trust";
        }

        return score < requiredScore ? "low_gate_score" : "";
    }

    private static int GetEventKindReactionBaseline(EmotionSocialContext context, EmotionEventKind eventKind)
    {
        return eventKind switch
        {
            EmotionEventKind.StoryChoice => context.Actor.GetTrait(EmotionAxis.Empathy) / 10 + context.Actor.GetTrait(EmotionAxis.Curiosity) / 10,
            EmotionEventKind.WorldInteraction => context.Actor.GetTrait(EmotionAxis.Helpfulness) / 8,
            EmotionEventKind.BattleResult => context.Actor.GetTrait(EmotionAxis.Courage) / 8 + context.Actor.GetTrait(EmotionAxis.Honor) / 10 - context.Fear / 10,
            EmotionEventKind.Recruitment => context.Trust / 10 + context.Affinity / 10,
            EmotionEventKind.SettlementAction => context.Actor.GetTrait(EmotionAxis.Order) / 10 + context.Actor.GetTrait(EmotionAxis.Loyalty) / 10,
            _ => 0
        };
    }

    private static EmotionEventReactionTone ToEventReactionTone(int score)
    {
        return score switch
        {
            <= -50 => EmotionEventReactionTone.Opposed,
            <= -15 => EmotionEventReactionTone.Negative,
            < 25 => EmotionEventReactionTone.Neutral,
            < 65 => EmotionEventReactionTone.Positive,
            _ => EmotionEventReactionTone.Inspired
        };
    }

    private static List<string> FindMissingMemoryTags(EmotionActorState actor, IEnumerable<string> requiredTags)
    {
        List<string> missing = new();
        if (actor == null || requiredTags == null)
        {
            return missing;
        }

        foreach (string tag in UniqueTags(requiredTags))
        {
            if (!actor.HasMemoryTag(tag))
            {
                missing.Add(tag);
            }
        }

        return missing;
    }

    private static List<string> FindPresentMemoryTags(EmotionActorState actor, IEnumerable<string> checkedTags)
    {
        List<string> present = new();
        if (actor == null || checkedTags == null)
        {
            return present;
        }

        foreach (string tag in UniqueTags(checkedTags))
        {
            if (actor.HasMemoryTag(tag))
            {
                present.Add(tag);
            }
        }

        return present;
    }

    private static IEnumerable<string> UniqueTags(IEnumerable<string> tags)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (string tag in tags)
        {
            if (string.IsNullOrWhiteSpace(tag) || !seen.Add(tag))
            {
                continue;
            }

            yield return tag;
        }
    }

    private static void AddFactor(List<EmotionScoreFactor> factors, string id, int amount)
    {
        if (factors == null || amount == 0)
        {
            return;
        }

        factors.Add(new EmotionScoreFactor(id, amount));
    }
}
