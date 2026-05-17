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
}
