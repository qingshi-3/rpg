namespace Rpg.Domain.Emotion;

public sealed class EmotionMemoryDelta
{
    public EmotionMemoryDelta(string actorId, EmotionMemoryState memory)
    {
        ActorId = actorId ?? "";
        Memory = memory;
    }

    public string ActorId { get; }
    public EmotionMemoryState Memory { get; }
}
