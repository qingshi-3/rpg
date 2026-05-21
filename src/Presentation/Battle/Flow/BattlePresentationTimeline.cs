using System.Collections.Generic;

namespace Rpg.Presentation.Battle.Flow;

public readonly record struct BattlePresentationActionSpan(
    double StartSeconds,
    double ImpactSeconds,
    double EndSeconds);

public sealed class BattlePresentationTimeline
{
    private readonly Dictionary<string, double> _actorActionCursors = new(System.StringComparer.Ordinal);
    private readonly Dictionary<string, double> _actorMovementCursors = new(System.StringComparer.Ordinal);

    public BattlePresentationActionSpan ScheduleMovement(
        string actorId,
        double observedAtSeconds,
        double actionDurationSeconds)
    {
        string actorKey = NormalizeActorId(actorId);
        double startSeconds = NormalizeSeconds(observedAtSeconds);
        double durationSeconds = NormalizeDuration(actionDurationSeconds);
        double endSeconds = startSeconds + durationSeconds;

        _actorMovementCursors[actorKey] = endSeconds;
        _actorActionCursors[actorKey] = System.Math.Max(ReadCursor(_actorActionCursors, actorKey), endSeconds);
        return new BattlePresentationActionSpan(startSeconds, endSeconds, endSeconds);
    }

    public BattlePresentationActionSpan ScheduleAttack(
        string actorId,
        string targetId,
        double observedAtSeconds,
        double actionDurationSeconds,
        double impactDelaySeconds)
    {
        string actorKey = NormalizeActorId(actorId);
        string targetKey = NormalizeActorId(targetId);
        double startSeconds = System.Math.Max(
            NormalizeSeconds(observedAtSeconds),
            ReadCursor(_actorActionCursors, actorKey));
        startSeconds = System.Math.Max(startSeconds, ReadCursor(_actorMovementCursors, targetKey));

        double durationSeconds = NormalizeDuration(actionDurationSeconds);
        double impactSeconds = startSeconds + System.Math.Clamp(NormalizeSeconds(impactDelaySeconds), 0, durationSeconds);
        double endSeconds = startSeconds + durationSeconds;
        _actorActionCursors[actorKey] = endSeconds;
        return new BattlePresentationActionSpan(startSeconds, impactSeconds, endSeconds);
    }

    private static string NormalizeActorId(string actorId)
    {
        return actorId?.Trim() ?? "";
    }

    private static double ReadCursor(IReadOnlyDictionary<string, double> cursors, string actorId)
    {
        return !string.IsNullOrWhiteSpace(actorId) && cursors.TryGetValue(actorId, out double cursor)
            ? cursor
            : 0;
    }

    private static double NormalizeSeconds(double seconds)
    {
        return double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0 ? 0 : seconds;
    }

    private static double NormalizeDuration(double seconds)
    {
        return System.Math.Max(0.01, NormalizeSeconds(seconds));
    }
}
