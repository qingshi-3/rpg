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
}
