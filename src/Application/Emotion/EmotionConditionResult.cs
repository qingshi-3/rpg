using Rpg.Definitions.Emotion;

namespace Rpg.Application.Emotion;

public sealed class EmotionConditionResult
{
    public EmotionConditionResult(EmotionConditionKind kind, string actorId, string targetId, bool passed, string reason)
    {
        Kind = kind;
        ActorId = actorId ?? "";
        TargetId = targetId ?? "";
        Passed = passed;
        Reason = reason ?? "";
    }

    public EmotionConditionKind Kind { get; }
    public string ActorId { get; }
    public string TargetId { get; }
    public bool Passed { get; }
    public string Reason { get; }
}
