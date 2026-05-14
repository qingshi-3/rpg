using Godot;

namespace Rpg.Application.World;

public interface IStrategicNavigationContext
{
    int Version { get; }
    string PrimaryProviderId { get; }

    bool IsSynchronized(out string failureReason);
    bool IsPointNavigable(Vector2 mapPoint, out string failureReason);
    bool TryGetNearestNavigablePoint(Vector2 mapPoint, int maxCellRadius, out Vector2 navigablePoint, out string failureReason);
    bool TryBuildPath(Vector2 start, Vector2 destination, out StrategicNavigationPath path, out string failureReason);
}
