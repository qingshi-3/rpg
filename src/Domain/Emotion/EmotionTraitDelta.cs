using Rpg.Definitions.Emotion;

namespace Rpg.Domain.Emotion;

public sealed class EmotionTraitDelta
{
    public EmotionTraitDelta(string actorId, EmotionAxis axis, int amount)
    {
        ActorId = actorId ?? "";
        Axis = axis;
        Amount = amount;
    }

    public string ActorId { get; }
    public EmotionAxis Axis { get; }
    public int Amount { get; }
}
