using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Definitions.Characters;
using Rpg.Definitions.Emotion;
using Rpg.Definitions.World;
using Rpg.Domain.Emotion;

namespace Rpg.Application.Emotion;

public sealed class EmotionSystem : IEmotionSystem
{
    private const int MinValue = -100;
    private const int MaxValue = 100;

    private EmotionWorldState _state;
    private readonly Dictionary<string, RaceEmotionProfileDefinition> _raceProfiles = new();
    private readonly Dictionary<string, EmotionProfileModifierDefinition> _modifiers = new();
    private readonly Dictionary<string, EmotionEventDefinition> _eventDefinitions = new();
    private readonly int _runSeed;

    public EmotionSystem(EmotionDefinitionDatabase database, int runSeed = 0, EmotionWorldState state = null)
    {
        _runSeed = runSeed;
        _state = state ?? new EmotionWorldState();
        IndexDefinitions(database);
    }

    public EmotionWorldState ExportState()
    {
        return _state.Clone();
    }

    public EmotionOperationResult<EmotionActorSnapshot> GenerateActor(CharacterDefinition character, int seed = 0)
    {
        EmotionOperationResult<EmotionNpcGenerationRequest> request = BuildGenerationRequest(character, seed);
        return request.Success
            ? GenerateActor(request.Value)
            : EmotionOperationResult<EmotionActorSnapshot>.Fail(request.Error);
    }

    public EmotionOperationResult<EmotionActorSnapshot> GenerateActor(EmotionNpcGenerationRequest request)
    {
        if (request == null)
        {
            return EmotionOperationResult<EmotionActorSnapshot>.Fail("Generation request is null.");
        }

        if (string.IsNullOrWhiteSpace(request.ActorId))
        {
            return EmotionOperationResult<EmotionActorSnapshot>.Fail("Actor id is required.");
        }

        if (!_raceProfiles.TryGetValue(request.RaceId, out RaceEmotionProfileDefinition raceProfile))
        {
            return EmotionOperationResult<EmotionActorSnapshot>.Fail($"Race emotion profile for race '{request.RaceId}' was not found.");
        }

        EmotionActorState actor = new(
            request.ActorId,
            string.IsNullOrWhiteSpace(request.DisplayName) ? request.ActorId : request.DisplayName,
            raceProfile.RaceId,
            request.IsSpecial);

        ApplyTraits(actor, raceProfile.BaselineTraits, additive: false);
        ApplyRelationshipModifiers(actor, raceProfile.InitialRelationshipModifiers, defaultSourceActorId: actor.ActorId);
        ApplyMemoryTags(actor, "race_baseline", raceProfile.MemoryTags);

        int variance = Math.Max(0, raceProfile.IndividualVariance);
        foreach (string modifierId in request.ModifierIds)
        {
            if (!_modifiers.TryGetValue(modifierId, out EmotionProfileModifierDefinition modifier))
            {
                continue;
            }

            ApplyTraits(actor, modifier.TraitModifiers, additive: true);
            ApplyRelationshipModifiers(actor, modifier.RelationshipModifiers, defaultSourceActorId: actor.ActorId);
            ApplyMemoryTags(actor, $"modifier:{modifier.Id}", modifier.MemoryTags);
            variance += Math.Max(0, modifier.VarianceBonus);
        }

        if (!request.DisableIndividualVariance && !request.IsSpecial)
        {
            ApplyIndividualVariance(actor, request, variance);
        }

        foreach (EmotionTraitDelta traitInput in request.TraitInputs)
        {
            if (!IsForActor(traitInput.ActorId, actor.ActorId))
            {
                continue;
            }

            actor.AddTrait(traitInput.Axis, traitInput.Amount);
        }

        foreach (EmotionRelationshipDelta relationshipInput in request.RelationshipInputs)
        {
            string sourceActorId = string.IsNullOrWhiteSpace(relationshipInput.SourceActorId)
                ? actor.ActorId
                : relationshipInput.SourceActorId;
            if (sourceActorId != actor.ActorId)
            {
                continue;
            }

            actor.GetOrCreateRelationship(relationshipInput.TargetId)
                .Add(relationshipInput.Metric, relationshipInput.Amount);
        }

        _state.SetActor(actor);
        return EmotionOperationResult<EmotionActorSnapshot>.Ok(new EmotionActorSnapshot(actor));
    }

    public EmotionOperationResult<EmotionEventResult> ApplyEvent(EmotionEvent emotionEvent)
    {
        if (emotionEvent == null)
        {
            return EmotionOperationResult<EmotionEventResult>.Fail("Emotion event is null.");
        }

        return EmotionOperationResult<EmotionEventResult>.Ok(_state.ApplyEvent(emotionEvent));
    }

    public EmotionOperationResult<EmotionEventResult> ApplyEvent(EmotionEventDefinition eventDefinition, EmotionConditionContext context)
    {
        EmotionOperationResult<EmotionEvent> buildResult = BuildEvent(eventDefinition, context);
        return buildResult.Success
            ? ApplyEvent(buildResult.Value)
            : EmotionOperationResult<EmotionEventResult>.Fail(buildResult.Error);
    }

    public EmotionOperationResult<EmotionEventResult> ApplyEvent(string eventDefinitionId, EmotionConditionContext context)
    {
        if (string.IsNullOrWhiteSpace(eventDefinitionId))
        {
            return EmotionOperationResult<EmotionEventResult>.Fail("Event definition id is required.");
        }

        if (!_eventDefinitions.TryGetValue(eventDefinitionId, out EmotionEventDefinition eventDefinition))
        {
            return EmotionOperationResult<EmotionEventResult>.Fail($"Event definition '{eventDefinitionId}' was not found.");
        }

        return ApplyEvent(eventDefinition, context);
    }

    public EmotionOperationResult<EmotionBatchEventResult> ApplyEvents(IEnumerable<EmotionEvent> emotionEvents)
    {
        if (emotionEvents == null)
        {
            return EmotionOperationResult<EmotionBatchEventResult>.Fail("Emotion events are null.");
        }

        List<EmotionEventResult> results = new();
        HashSet<string> changedActorIds = new();
        List<string> warnings = new();

        foreach (EmotionEvent emotionEvent in emotionEvents)
        {
            EmotionOperationResult<EmotionEventResult> result = ApplyEvent(emotionEvent);
            if (!result.Success)
            {
                warnings.Add(result.Error);
                continue;
            }

            results.Add(result.Value);
            foreach (string actorId in result.Value.ChangedActorIds)
            {
                changedActorIds.Add(actorId);
            }

            foreach (string warning in result.Value.Warnings)
            {
                warnings.Add(warning);
            }
        }

        return EmotionOperationResult<EmotionBatchEventResult>.Ok(new EmotionBatchEventResult(results, changedActorIds, warnings));
    }

    public EmotionOperationResult<EmotionActorSnapshot> QueryActor(string actorId)
    {
        EmotionActorState actor = _state.GetActor(actorId);
        return actor == null
            ? EmotionOperationResult<EmotionActorSnapshot>.Fail($"Actor '{actorId}' was not found.")
            : EmotionOperationResult<EmotionActorSnapshot>.Ok(new EmotionActorSnapshot(actor));
    }

    public EmotionOperationResult<int> QueryTrait(string actorId, EmotionAxis axis)
    {
        EmotionActorState actor = _state.GetActor(actorId);
        return actor == null
            ? EmotionOperationResult<int>.Fail($"Actor '{actorId}' was not found.")
            : EmotionOperationResult<int>.Ok(actor.GetTrait(axis));
    }

    public EmotionOperationResult<EmotionRelationshipSnapshot> QueryRelationship(string sourceActorId, string targetId)
    {
        EmotionActorState actor = _state.GetActor(sourceActorId);
        if (actor == null)
        {
            return EmotionOperationResult<EmotionRelationshipSnapshot>.Fail($"Actor '{sourceActorId}' was not found.");
        }

        EmotionRelationshipState relationship = actor.GetRelationship(targetId) ?? new EmotionRelationshipState(targetId);
        return EmotionOperationResult<EmotionRelationshipSnapshot>.Ok(new EmotionRelationshipSnapshot(relationship));
    }

    public EmotionOperationResult<EmotionDispositionResult> EvaluateDisposition(string actorId, string targetId)
    {
        EmotionActorState actor = _state.GetActor(actorId);
        if (actor == null)
        {
            return EmotionOperationResult<EmotionDispositionResult>.Fail($"Actor '{actorId}' was not found.");
        }

        EmotionRelationshipState relationship = actor.GetRelationship(targetId) ?? new EmotionRelationshipState(targetId);
        int score =
            relationship.Get(EmotionRelationshipMetric.Trust) +
            relationship.Get(EmotionRelationshipMetric.Affinity) +
            relationship.Get(EmotionRelationshipMetric.Respect) / 2 -
            relationship.Get(EmotionRelationshipMetric.Fear) / 2 -
            relationship.Get(EmotionRelationshipMetric.Grievance) +
            actor.GetTrait(EmotionAxis.Empathy) / 3 +
            actor.GetTrait(EmotionAxis.Helpfulness) / 3 -
            actor.GetTrait(EmotionAxis.Aggression) / 4;

        score = Clamp(score);
        EmotionDisposition disposition = score switch
        {
            <= -50 => EmotionDisposition.Hostile,
            <= -15 => EmotionDisposition.Wary,
            < 25 => EmotionDisposition.Neutral,
            < 65 => EmotionDisposition.Friendly,
            _ => EmotionDisposition.Devoted
        };

        return EmotionOperationResult<EmotionDispositionResult>.Ok(new EmotionDispositionResult(actorId, targetId, score, disposition));
    }

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

    public EmotionOperationResult<EmotionConditionResult> CheckCondition(EmotionConditionDefinition condition, EmotionConditionContext context = null)
    {
        if (condition == null)
        {
            return EmotionOperationResult<EmotionConditionResult>.Fail("Emotion condition is null.");
        }

        context ??= new EmotionConditionContext();
        string actorId = ResolveActorId(condition.ActorId, context);
        string targetId = ResolveTargetId(condition.TargetId, context);
        EmotionActorState actor = _state.GetActor(actorId);

        EmotionConditionResult result = condition.Kind switch
        {
            EmotionConditionKind.ActorExists => BuildConditionResult(
                condition,
                actorId,
                targetId,
                actor != null,
                actor != null ? "Actor exists." : $"Actor '{actorId}' was not found."),

            EmotionConditionKind.TraitThreshold => CheckTraitCondition(condition, actor, actorId, targetId),
            EmotionConditionKind.RelationshipThreshold => CheckRelationshipCondition(condition, actor, actorId, targetId),
            EmotionConditionKind.DispositionThreshold => CheckDispositionCondition(condition, actorId, targetId),
            EmotionConditionKind.MemoryTagExists => CheckMemoryTagCondition(condition, actor, actorId, targetId, expected: true),
            EmotionConditionKind.MemoryTagMissing => CheckMemoryTagCondition(condition, actor, actorId, targetId, expected: false),
            _ => BuildConditionResult(condition, actorId, targetId, false, $"Unsupported condition kind '{condition.Kind}'.")
        };

        return EmotionOperationResult<EmotionConditionResult>.Ok(result);
    }

    public EmotionOperationResult<IReadOnlyList<EmotionConditionResult>> CheckConditions(
        IEnumerable<EmotionConditionDefinition> conditions,
        EmotionConditionContext context = null)
    {
        if (conditions == null)
        {
            return EmotionOperationResult<IReadOnlyList<EmotionConditionResult>>.Fail("Emotion conditions are null.");
        }

        List<EmotionConditionResult> results = new();
        foreach (EmotionConditionDefinition condition in conditions)
        {
            EmotionOperationResult<EmotionConditionResult> result = CheckCondition(condition, context);
            if (!result.Success)
            {
                return EmotionOperationResult<IReadOnlyList<EmotionConditionResult>>.Fail(result.Error);
            }

            results.Add(result.Value);
        }

        return EmotionOperationResult<IReadOnlyList<EmotionConditionResult>>.Ok(results);
    }

    public bool IsTraitAtLeast(string actorId, EmotionAxis axis, int minimum)
    {
        EmotionActorState actor = _state.GetActor(actorId);
        return actor != null && actor.GetTrait(axis) >= minimum;
    }

    public bool IsRelationshipAtLeast(string sourceActorId, string targetId, EmotionRelationshipMetric metric, int minimum)
    {
        EmotionActorState actor = _state.GetActor(sourceActorId);
        EmotionRelationshipState relationship = actor?.GetRelationship(targetId);
        return relationship != null && relationship.Get(metric) >= minimum;
    }

    public bool IsDispositionAtLeast(string actorId, string targetId, EmotionDisposition minimum)
    {
        EmotionOperationResult<EmotionDispositionResult> result = EvaluateDisposition(actorId, targetId);
        return result.Success && result.Value.Disposition >= minimum;
    }

    public bool HasMemoryTag(string actorId, string tag)
    {
        EmotionActorState actor = _state.GetActor(actorId);
        return actor != null && actor.HasMemoryTag(tag);
    }

    public IReadOnlyList<EmotionActorSnapshot> QueryActors()
    {
        return _state.Actors.Values
            .Select(actor => new EmotionActorSnapshot(actor))
            .ToArray();
    }

    public EmotionOperationResult<EmotionWorldState> UseState(EmotionWorldState state)
    {
        if (state == null)
        {
            return EmotionOperationResult<EmotionWorldState>.Fail("Emotion world state is null.");
        }

        _state = state.Clone();
        return EmotionOperationResult<EmotionWorldState>.Ok(ExportState());
    }

    private void IndexDefinitions(EmotionDefinitionDatabase database)
    {
        _raceProfiles.Clear();
        _modifiers.Clear();
        _eventDefinitions.Clear();

        if (database == null)
        {
            return;
        }

        foreach (RaceEmotionProfileDefinition raceProfile in database.RaceProfiles)
        {
            if (raceProfile == null || string.IsNullOrWhiteSpace(raceProfile.RaceId))
            {
                continue;
            }

            _raceProfiles[raceProfile.RaceId] = raceProfile;
        }

        foreach (EmotionProfileModifierDefinition modifier in database.ProfileModifiers)
        {
            if (modifier == null || string.IsNullOrWhiteSpace(modifier.Id))
            {
                continue;
            }

            _modifiers[modifier.Id] = modifier;
        }

        foreach (EmotionEventDefinition eventDefinition in database.EventDefinitions)
        {
            if (eventDefinition == null || string.IsNullOrWhiteSpace(eventDefinition.Id))
            {
                continue;
            }

            _eventDefinitions[eventDefinition.Id] = eventDefinition;
        }
    }

    private EmotionOperationResult<EmotionNpcGenerationRequest> BuildGenerationRequest(CharacterDefinition character, int seed)
    {
        if (character == null)
        {
            return EmotionOperationResult<EmotionNpcGenerationRequest>.Fail("Character definition is null.");
        }

        if (string.IsNullOrWhiteSpace(character.Id))
        {
            return EmotionOperationResult<EmotionNpcGenerationRequest>.Fail("Character id is required.");
        }

        if (character.Race == null || string.IsNullOrWhiteSpace(character.Race.Id))
        {
            return EmotionOperationResult<EmotionNpcGenerationRequest>.Fail($"Character '{character.Id}' has no race definition.");
        }

        List<string> modifierIds = new();
        modifierIds.AddRange(character.Race.DefaultEmotionModifierIds.Where(id => !string.IsNullOrWhiteSpace(id)));
        AddContextModifierId(modifierIds, EmotionProfileModifierKind.Culture, character.CultureId);
        AddContextModifierId(modifierIds, EmotionProfileModifierKind.Faction, character.FactionId);
        AddContextModifierId(modifierIds, EmotionProfileModifierKind.Profession, character.ProfessionId);
        modifierIds.AddRange(character.EmotionModifierIds.Where(id => !string.IsNullOrWhiteSpace(id)));

        Dictionary<CharacterAttribute, int> attributes = BuildCharacterAttributes(character);
        List<EmotionTraitDelta> traitInputs = BuildTraitInputsFromAttributes(character.Id, attributes);

        EmotionNpcGenerationRequest request = new(
            character.Id,
            character.DisplayName,
            character.Race.Id,
            modifierIds,
            seed,
            character.IsSpecial,
            disableIndividualVariance: false,
            traitInputs,
            relationshipInputs: null,
            BuildVariationKey(character));
        return EmotionOperationResult<EmotionNpcGenerationRequest>.Ok(request);
    }

    private EmotionOperationResult<EmotionEvent> BuildEvent(EmotionEventDefinition eventDefinition, EmotionConditionContext context)
    {
        if (eventDefinition == null)
        {
            return EmotionOperationResult<EmotionEvent>.Fail("Event definition is null.");
        }

        if (string.IsNullOrWhiteSpace(eventDefinition.Id))
        {
            return EmotionOperationResult<EmotionEvent>.Fail("Event definition id is required.");
        }

        context ??= new EmotionConditionContext();
        IEnumerable<EmotionConditionDefinition> conditions = eventDefinition.Conditions != null
            ? eventDefinition.Conditions
            : Array.Empty<EmotionConditionDefinition>();
        EmotionOperationResult<IReadOnlyList<EmotionConditionResult>> conditionResult =
            CheckConditions(conditions, context);
        if (!conditionResult.Success)
        {
            return EmotionOperationResult<EmotionEvent>.Fail(conditionResult.Error);
        }

        List<EmotionConditionResult> failedConditions = conditionResult.Value.Where(result => !result.Passed).ToList();
        if (failedConditions.Count > 0)
        {
            string reasons = string.Join("; ", failedConditions.Select(result => result.Reason));
            return EmotionOperationResult<EmotionEvent>.Fail($"Event definition '{eventDefinition.Id}' conditions were not met: {reasons}");
        }

        string eventId = string.IsNullOrWhiteSpace(context.EventId) ? eventDefinition.Id : context.EventId;
        string sourceId = string.IsNullOrWhiteSpace(context.SourceId) ? $"emotion_definition:{eventDefinition.Id}" : context.SourceId;
        string subjectActorId = context.SubjectActorId;

        List<EmotionTraitDelta> traitDeltas = new();
        List<EmotionRelationshipDelta> relationshipDeltas = new();
        List<EmotionMemoryDelta> memoryDeltas = new();

        int memoryIndex = 0;
        IEnumerable<EmotionEffectDefinition> effects = eventDefinition.Effects != null
            ? eventDefinition.Effects
            : Array.Empty<EmotionEffectDefinition>();
        foreach (EmotionEffectDefinition effect in effects)
        {
            if (effect == null)
            {
                continue;
            }

            string actorId = ResolveActorId(effect.ActorId, context);
            switch (effect.Kind)
            {
                case EmotionEffectKind.TraitDelta:
                    traitDeltas.Add(new EmotionTraitDelta(actorId, effect.Axis, effect.Amount));
                    break;

                case EmotionEffectKind.RelationshipDelta:
                    relationshipDeltas.Add(new EmotionRelationshipDelta(
                        actorId,
                        ResolveTargetId(effect.TargetId, context),
                        effect.RelationshipMetric,
                        effect.Amount));
                    break;

                case EmotionEffectKind.Memory:
                    memoryDeltas.Add(new EmotionMemoryDelta(
                        actorId,
                        BuildMemory(eventId, effect, memoryIndex++)));
                    break;
            }
        }

        return EmotionOperationResult<EmotionEvent>.Ok(new EmotionEvent(
            eventId,
            eventDefinition.Kind,
            subjectActorId,
            sourceId,
            traitDeltas,
            relationshipDeltas,
            memory: null,
            memoryDeltas));
    }

    private static EmotionMemoryState BuildMemory(string eventId, EmotionEffectDefinition effect, int index)
    {
        string memoryId = string.IsNullOrWhiteSpace(effect.MemoryId)
            ? $"{eventId}:memory:{index}"
            : effect.MemoryId;

        return new EmotionMemoryState(
            memoryId,
            eventId,
            effect.MemoryDescription,
            effect.MemoryWeight,
            effect.MemoryTags);
    }

    private static Dictionary<CharacterAttribute, int> BuildCharacterAttributes(CharacterDefinition character)
    {
        Dictionary<CharacterAttribute, int> attributes = new();

        if (character?.Race != null)
        {
            foreach (CharacterAttributeValue value in character.Race.BaselineAttributes)
            {
                if (value == null)
                {
                    continue;
                }

                attributes[value.Attribute] = Clamp(value.Value);
            }
        }

        if (character != null)
        {
            foreach (CharacterAttributeValue value in character.AttributeModifiers)
            {
                if (value == null)
                {
                    continue;
                }

                attributes.TryGetValue(value.Attribute, out int current);
                attributes[value.Attribute] = Clamp(current + value.Value);
            }
        }

        return attributes;
    }

    private void AddContextModifierId(List<string> modifierIds, EmotionProfileModifierKind kind, string contextId)
    {
        if (modifierIds == null || string.IsNullOrWhiteSpace(contextId))
        {
            return;
        }

        string prefixedId = $"{kind.ToString().ToLowerInvariant()}:{contextId}";
        bool added = false;

        if (_modifiers.TryGetValue(contextId, out EmotionProfileModifierDefinition directModifier) && directModifier.Kind == kind)
        {
            AddUnique(modifierIds, contextId);
            added = true;
        }

        if (_modifiers.TryGetValue(prefixedId, out EmotionProfileModifierDefinition prefixedModifier) && prefixedModifier.Kind == kind)
        {
            AddUnique(modifierIds, prefixedId);
            added = true;
        }

        if (!added)
        {
            AddUnique(modifierIds, contextId);
        }
    }

    private static void AddUnique(List<string> values, string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !values.Contains(value))
        {
            values.Add(value);
        }
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

    private EmotionConditionResult CheckTraitCondition(
        EmotionConditionDefinition condition,
        EmotionActorState actor,
        string actorId,
        string targetId)
    {
        if (actor == null)
        {
            return BuildConditionResult(condition, actorId, targetId, false, $"Actor '{actorId}' was not found.");
        }

        int value = actor.GetTrait(condition.Axis);
        bool passed = Compare(value, condition.Comparison, condition.Threshold);
        return BuildConditionResult(
            condition,
            actorId,
            targetId,
            passed,
            $"{condition.Axis} is {value}; required {condition.Comparison} {condition.Threshold}.");
    }

    private EmotionConditionResult CheckRelationshipCondition(
        EmotionConditionDefinition condition,
        EmotionActorState actor,
        string actorId,
        string targetId)
    {
        if (actor == null)
        {
            return BuildConditionResult(condition, actorId, targetId, false, $"Actor '{actorId}' was not found.");
        }

        EmotionRelationshipState relationship = actor.GetRelationship(targetId);
        int value = relationship?.Get(condition.RelationshipMetric) ?? 0;
        bool passed = Compare(value, condition.Comparison, condition.Threshold);
        return BuildConditionResult(
            condition,
            actorId,
            targetId,
            passed,
            $"{condition.RelationshipMetric} toward '{targetId}' is {value}; required {condition.Comparison} {condition.Threshold}.");
    }

    private EmotionConditionResult CheckDispositionCondition(
        EmotionConditionDefinition condition,
        string actorId,
        string targetId)
    {
        EmotionOperationResult<EmotionDispositionResult> disposition = EvaluateDisposition(actorId, targetId);
        if (!disposition.Success)
        {
            return BuildConditionResult(condition, actorId, targetId, false, disposition.Error);
        }

        bool passed = disposition.Value.Disposition >= ToDisposition(condition.Disposition);
        return BuildConditionResult(
            condition,
            actorId,
            targetId,
            passed,
            $"Disposition is {disposition.Value.Disposition}; required at least {condition.Disposition}.");
    }

    private static EmotionConditionResult CheckMemoryTagCondition(
        EmotionConditionDefinition condition,
        EmotionActorState actor,
        string actorId,
        string targetId,
        bool expected)
    {
        if (actor == null)
        {
            return BuildConditionResult(condition, actorId, targetId, false, $"Actor '{actorId}' was not found.");
        }

        bool hasTag = actor.HasMemoryTag(condition.MemoryTag);
        bool passed = hasTag == expected;
        string expectation = expected ? "present" : "missing";
        return BuildConditionResult(
            condition,
            actorId,
            targetId,
            passed,
            $"Memory tag '{condition.MemoryTag}' is {(hasTag ? "present" : "missing")}; required {expectation}.");
    }

    private static EmotionConditionResult BuildConditionResult(
        EmotionConditionDefinition condition,
        string actorId,
        string targetId,
        bool passed,
        string reason)
    {
        return new EmotionConditionResult(condition.Kind, actorId, targetId, passed, reason);
    }

    private static bool Compare(int value, EmotionComparisonOperator comparison, int threshold)
    {
        return comparison switch
        {
            EmotionComparisonOperator.AtLeast => value >= threshold,
            EmotionComparisonOperator.AtMost => value <= threshold,
            EmotionComparisonOperator.GreaterThan => value > threshold,
            EmotionComparisonOperator.LessThan => value < threshold,
            EmotionComparisonOperator.Equal => value == threshold,
            EmotionComparisonOperator.NotEqual => value != threshold,
            _ => false
        };
    }

    private static EmotionDisposition ToDisposition(EmotionDispositionMinimum minimum)
    {
        return minimum switch
        {
            EmotionDispositionMinimum.Hostile => EmotionDisposition.Hostile,
            EmotionDispositionMinimum.Wary => EmotionDisposition.Wary,
            EmotionDispositionMinimum.Neutral => EmotionDisposition.Neutral,
            EmotionDispositionMinimum.Friendly => EmotionDisposition.Friendly,
            EmotionDispositionMinimum.Devoted => EmotionDisposition.Devoted,
            _ => EmotionDisposition.Neutral
        };
    }

    private static string ResolveActorId(string actorId, EmotionConditionContext context)
    {
        return string.IsNullOrWhiteSpace(actorId) ? context?.SubjectActorId ?? "" : actorId;
    }

    private static string ResolveTargetId(string targetId, EmotionConditionContext context)
    {
        return string.IsNullOrWhiteSpace(targetId) ? context?.TargetId ?? "" : targetId;
    }

    private static List<EmotionTraitDelta> BuildTraitInputsFromAttributes(string actorId, IReadOnlyDictionary<CharacterAttribute, int> attributes)
    {
        List<EmotionTraitDelta> deltas = new();

        AddScaledTrait(deltas, actorId, EmotionAxis.Empathy, GetAttribute(attributes, CharacterAttribute.Empathy), 2);
        AddScaledTrait(deltas, actorId, EmotionAxis.Helpfulness, GetAttribute(attributes, CharacterAttribute.Empathy), 3);
        AddScaledTrait(deltas, actorId, EmotionAxis.Order, GetAttribute(attributes, CharacterAttribute.Discipline), 3);
        AddScaledTrait(deltas, actorId, EmotionAxis.Loyalty, GetAttribute(attributes, CharacterAttribute.Discipline), 4);
        AddScaledTrait(deltas, actorId, EmotionAxis.Courage, GetAttribute(attributes, CharacterAttribute.Willpower), 3);
        AddScaledTrait(deltas, actorId, EmotionAxis.Honor, GetAttribute(attributes, CharacterAttribute.Faith), 4);
        AddScaledTrait(deltas, actorId, EmotionAxis.Order, GetAttribute(attributes, CharacterAttribute.Craft), 5);
        AddScaledTrait(deltas, actorId, EmotionAxis.Courage, GetAttribute(attributes, CharacterAttribute.Strength), 5);
        AddScaledTrait(deltas, actorId, EmotionAxis.Curiosity, GetAttribute(attributes, CharacterAttribute.Intellect), 4);
        AddScaledTrait(deltas, actorId, EmotionAxis.Aggression, GetAttribute(attributes, CharacterAttribute.Instinct), 4);
        AddScaledTrait(deltas, actorId, EmotionAxis.Freedom, GetAttribute(attributes, CharacterAttribute.Social), 5);
        AddScaledTrait(deltas, actorId, EmotionAxis.Courage, GetAttribute(attributes, CharacterAttribute.Survival), 5);

        return deltas;
    }

    private static int GetAttribute(IReadOnlyDictionary<CharacterAttribute, int> attributes, CharacterAttribute attribute)
    {
        return attributes != null && attributes.TryGetValue(attribute, out int value) ? value : 0;
    }

    private static void AddScaledTrait(List<EmotionTraitDelta> deltas, string actorId, EmotionAxis axis, int attributeValue, int divisor)
    {
        if (attributeValue == 0 || divisor <= 0)
        {
            return;
        }

        int amount = attributeValue / divisor;
        if (amount != 0)
        {
            deltas.Add(new EmotionTraitDelta(actorId, axis, amount));
        }
    }

    private static void ApplyTraits(EmotionActorState actor, IEnumerable<EmotionTraitDefinition> traits, bool additive)
    {
        if (actor == null || traits == null)
        {
            return;
        }

        foreach (EmotionTraitDefinition trait in traits)
        {
            if (trait == null)
            {
                continue;
            }

            if (additive)
            {
                actor.AddTrait(trait.Axis, trait.Value);
            }
            else
            {
                actor.SetTrait(trait.Axis, trait.Value);
            }
        }
    }

    private static void ApplyRelationshipModifiers(
        EmotionActorState actor,
        IEnumerable<EmotionRelationshipModifierDefinition> modifiers,
        string defaultSourceActorId)
    {
        if (actor == null || modifiers == null)
        {
            return;
        }

        foreach (EmotionRelationshipModifierDefinition modifier in modifiers)
        {
            if (modifier == null)
            {
                continue;
            }

            string sourceActorId = string.IsNullOrWhiteSpace(defaultSourceActorId)
                ? actor.ActorId
                : defaultSourceActorId;
            if (sourceActorId != actor.ActorId)
            {
                continue;
            }

            actor.GetOrCreateRelationship(modifier.TargetId)
                .Add(modifier.Metric, modifier.Amount);
        }
    }

    private static void ApplyMemoryTags(EmotionActorState actor, string sourceId, IEnumerable<string> tags)
    {
        if (actor == null || tags == null)
        {
            return;
        }

        foreach (string tag in tags)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                continue;
            }

            actor.AddMemory(new EmotionMemoryState(
                $"{sourceId}:{tag}",
                sourceId,
                tag,
                0,
                new[] { tag }));
        }
    }

    private void ApplyIndividualVariance(EmotionActorState actor, EmotionNpcGenerationRequest request, int variance)
    {
        if (actor == null || request == null || variance <= 0)
        {
            return;
        }

        int cappedVariance = Math.Clamp(variance, 0, 40);
        string variationKey = string.IsNullOrWhiteSpace(request.VariationKey)
            ? BuildVariationKey(request.RaceId, request.ModifierIds)
            : request.VariationKey;
        foreach (EmotionAxis axis in Enum.GetValues<EmotionAxis>())
        {
            int roll = StableRange($"{_runSeed}:{request.Seed}:{variationKey}:{axis}", -cappedVariance, cappedVariance);
            actor.AddTrait(axis, roll);
        }
    }

    private static string BuildVariationKey(CharacterDefinition character)
    {
        if (character == null)
        {
            return "";
        }

        List<string> parts = new()
        {
            character.Race?.Id ?? "",
            character.CultureId ?? "",
            character.FactionId ?? "",
            character.ProfessionId ?? ""
        };
        if (character.Race?.DefaultEmotionModifierIds != null)
        {
            parts.AddRange(character.Race.DefaultEmotionModifierIds);
        }

        if (character.EmotionModifierIds != null)
        {
            parts.AddRange(character.EmotionModifierIds);
        }

        return BuildVariationKey(character.Race?.Id ?? "", parts);
    }

    private static string BuildVariationKey(string raceId, IEnumerable<string> modifierIds)
    {
        IEnumerable<string> modifiers = modifierIds == null
            ? Array.Empty<string>()
            : modifierIds.Where(id => !string.IsNullOrWhiteSpace(id)).OrderBy(id => id, StringComparer.Ordinal);
        return $"{raceId}|{string.Join("|", modifiers)}";
    }

    private static int StableRange(string key, int minInclusive, int maxInclusive)
    {
        uint hash = 2166136261;
        foreach (char c in key ?? "")
        {
            hash ^= c;
            hash *= 16777619;
        }

        int range = maxInclusive - minInclusive + 1;
        if (range <= 0)
        {
            return minInclusive;
        }

        return minInclusive + (int)(hash % (uint)range);
    }

    private static bool IsForActor(string deltaActorId, string actorId)
    {
        return string.IsNullOrWhiteSpace(deltaActorId) || deltaActorId == actorId;
    }

    private static int Clamp(int value)
    {
        return Math.Clamp(value, MinValue, MaxValue);
    }

    private sealed class EmotionSocialContext
    {
        public EmotionSocialContext(
            EmotionActorState actor,
            EmotionRelationshipState relationship,
            EmotionDispositionResult disposition)
        {
            Actor = actor;
            Relationship = relationship;
            Disposition = disposition;
        }

        public EmotionActorState Actor { get; }
        public EmotionRelationshipState Relationship { get; }
        public EmotionDispositionResult Disposition { get; }

        public int Trust => Relationship.Get(EmotionRelationshipMetric.Trust);
        public int Affinity => Relationship.Get(EmotionRelationshipMetric.Affinity);
        public int Fear => Relationship.Get(EmotionRelationshipMetric.Fear);
        public int Respect => Relationship.Get(EmotionRelationshipMetric.Respect);
        public int Grievance => Relationship.Get(EmotionRelationshipMetric.Grievance);
    }
}
