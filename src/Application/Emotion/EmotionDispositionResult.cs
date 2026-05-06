using Rpg.Domain.Emotion;

namespace Rpg.Application.Emotion;

public sealed class EmotionDispositionResult
{
    public EmotionDispositionResult(string actorId, string targetId, int score, EmotionDisposition disposition)
    {
        ActorId = actorId ?? "";
        TargetId = targetId ?? "";
        Score = score;
        Disposition = disposition;
    }

    public string ActorId { get; }
    public string TargetId { get; }
    public int Score { get; }
    public EmotionDisposition Disposition { get; }
}
