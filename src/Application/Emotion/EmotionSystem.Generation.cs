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
}
