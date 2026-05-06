using System.Collections.Generic;
using Rpg.Domain.Emotion;

namespace Rpg.Application.Emotion;

public sealed class EmotionNpcGenerationRequest
{
    public EmotionNpcGenerationRequest(
        string actorId,
        string displayName,
        string raceId,
        IEnumerable<string> modifierIds = null,
        int seed = 0,
        bool isSpecial = false,
        bool disableIndividualVariance = false,
        IEnumerable<EmotionTraitDelta> traitInputs = null,
        IEnumerable<EmotionRelationshipDelta> relationshipInputs = null,
        string variationKey = "")
    {
        ActorId = actorId ?? "";
        DisplayName = displayName ?? "";
        RaceId = raceId ?? "";
        ModifierIds = new List<string>(modifierIds ?? System.Array.Empty<string>());
        Seed = seed;
        IsSpecial = isSpecial;
        DisableIndividualVariance = disableIndividualVariance;
        TraitInputs = new List<EmotionTraitDelta>(traitInputs ?? System.Array.Empty<EmotionTraitDelta>());
        RelationshipInputs = new List<EmotionRelationshipDelta>(relationshipInputs ?? System.Array.Empty<EmotionRelationshipDelta>());
        VariationKey = variationKey ?? "";
    }

    public string ActorId { get; }
    public string DisplayName { get; }
    public string RaceId { get; }
    public IReadOnlyList<string> ModifierIds { get; }
    public int Seed { get; }
    public bool IsSpecial { get; }
    public bool DisableIndividualVariance { get; }
    public string VariationKey { get; }
    public IReadOnlyList<EmotionTraitDelta> TraitInputs { get; }
    public IReadOnlyList<EmotionRelationshipDelta> RelationshipInputs { get; }

    public IReadOnlyList<EmotionTraitDelta> TraitOverrides => TraitInputs;
    public IReadOnlyList<EmotionRelationshipDelta> RelationshipOverrides => RelationshipInputs;
}
