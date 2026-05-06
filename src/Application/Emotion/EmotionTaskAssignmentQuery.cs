using Rpg.Definitions.World;

namespace Rpg.Application.Emotion;

public sealed class EmotionTaskAssignmentQuery
{
    public EmotionTaskAssignmentQuery(
        string actorId,
        string targetId,
        WorldTaskKind taskKind,
        int difficulty = 0,
        int danger = 0,
        bool isForced = false)
    {
        ActorId = actorId ?? "";
        TargetId = targetId ?? "";
        TaskKind = taskKind;
        Difficulty = difficulty;
        Danger = danger;
        IsForced = isForced;
    }

    public string ActorId { get; }
    public string TargetId { get; }
    public WorldTaskKind TaskKind { get; }
    public int Difficulty { get; }
    public int Danger { get; }
    public bool IsForced { get; }
}
