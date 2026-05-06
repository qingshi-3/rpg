using System.Collections.Generic;
using Rpg.Domain.Emotion;

namespace Rpg.Application.Emotion;

public sealed class EmotionBatchEventResult
{
    public EmotionBatchEventResult(
        IEnumerable<EmotionEventResult> eventResults,
        IEnumerable<string> changedActorIds,
        IEnumerable<string> warnings)
    {
        EventResults = new List<EmotionEventResult>(eventResults ?? System.Array.Empty<EmotionEventResult>());
        ChangedActorIds = new List<string>(changedActorIds ?? System.Array.Empty<string>());
        Warnings = new List<string>(warnings ?? System.Array.Empty<string>());
    }

    public IReadOnlyList<EmotionEventResult> EventResults { get; }
    public IReadOnlyList<string> ChangedActorIds { get; }
    public IReadOnlyList<string> Warnings { get; }
}
