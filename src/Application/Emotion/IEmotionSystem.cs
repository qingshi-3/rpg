using System.Collections.Generic;
using Rpg.Definitions.Characters;
using Rpg.Definitions.Emotion;
using Rpg.Domain.Emotion;

namespace Rpg.Application.Emotion;

public interface IEmotionSystem
{
    EmotionOperationResult<EmotionActorSnapshot> GenerateActor(CharacterDefinition character, int seed = 0);
    EmotionOperationResult<EmotionActorSnapshot> GenerateActor(EmotionNpcGenerationRequest request);
    EmotionOperationResult<EmotionEventResult> ApplyEvent(EmotionEvent emotionEvent);
    EmotionOperationResult<EmotionEventResult> ApplyEvent(string eventDefinitionId, EmotionConditionContext context);
    EmotionOperationResult<EmotionEventResult> ApplyEvent(EmotionEventDefinition eventDefinition, EmotionConditionContext context);
    EmotionOperationResult<EmotionBatchEventResult> ApplyEvents(IEnumerable<EmotionEvent> emotionEvents);
    EmotionOperationResult<EmotionActorSnapshot> QueryActor(string actorId);
    EmotionOperationResult<int> QueryTrait(string actorId, EmotionAxis axis);
    EmotionOperationResult<EmotionRelationshipSnapshot> QueryRelationship(string sourceActorId, string targetId);
    EmotionOperationResult<EmotionDispositionResult> EvaluateDisposition(string actorId, string targetId);
    EmotionOperationResult<EmotionRecruitmentResult> EvaluateRecruitment(EmotionRecruitmentQuery query);
    EmotionOperationResult<EmotionTaskAssignmentResult> EvaluateTaskAssignment(EmotionTaskAssignmentQuery query);
    EmotionOperationResult<EmotionLoyaltyResult> EvaluateLoyalty(EmotionLoyaltyQuery query);
    EmotionOperationResult<EmotionBattleSupportResult> EvaluateBattleSupport(EmotionBattleSupportQuery query);
    EmotionOperationResult<EmotionRelationshipGateResult> EvaluateRelationshipGate(EmotionRelationshipGateQuery query);
    EmotionOperationResult<EmotionEventReactionResult> EvaluateEventReaction(EmotionEventReactionQuery query);
    EmotionOperationResult<EmotionConditionResult> CheckCondition(EmotionConditionDefinition condition, EmotionConditionContext context = null);
    EmotionOperationResult<IReadOnlyList<EmotionConditionResult>> CheckConditions(IEnumerable<EmotionConditionDefinition> conditions, EmotionConditionContext context = null);
    bool IsTraitAtLeast(string actorId, EmotionAxis axis, int minimum);
    bool IsRelationshipAtLeast(string sourceActorId, string targetId, EmotionRelationshipMetric metric, int minimum);
    bool IsDispositionAtLeast(string actorId, string targetId, EmotionDisposition minimum);
    bool HasMemoryTag(string actorId, string tag);
    IReadOnlyList<EmotionActorSnapshot> QueryActors();
    EmotionWorldState ExportState();
    EmotionOperationResult<EmotionWorldState> UseState(EmotionWorldState state);
}
