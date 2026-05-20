using System.Collections.Generic;
using System.Text.Json.Serialization;
using Godot;

namespace Rpg.Domain.World;

public sealed class WorldArmyState
{
    public string ArmyId { get; set; } = "";
    public string OwnerFactionId { get; set; } = "";
    public string SourceSiteId { get; set; } = "";
    public string TargetSiteId { get; set; } = "";
    public float WorldX { get; set; }
    public float WorldY { get; set; }
    public float DestinationX { get; set; }
    public float DestinationY { get; set; }
    public float MoveSpeed { get; set; } = 80.0f;
    public float Radius { get; set; } = 18.0f;
    public WorldArmyStatus Status { get; set; } = WorldArmyStatus.Idle;
    public WorldArmyIntent Intent { get; set; } = WorldArmyIntent.None;
    public WorldSiteAttackDirection TargetApproachDirection { get; set; } = WorldSiteAttackDirection.Any;
    public List<GarrisonState> GarrisonUnits { get; set; } = new();
    public ResourceStore CargoResources { get; set; } = new();
    public int CreatedTick { get; set; }

    [JsonIgnore]
    public List<Vector2> NavigationPathPoints { get; } = new();

    [JsonIgnore]
    public int NavigationPathPointIndex { get; set; }

    [JsonIgnore]
    public int NavigationSurfaceVersion { get; set; } = -1;

    [JsonIgnore]
    public Vector2 NavigationPathDestination { get; set; }

    [JsonIgnore]
    public bool HasNavigationPath { get; private set; }

    [JsonIgnore]
    public int TransientNavigationPathFailureCount { get; private set; }

    [JsonIgnore]
    public float TransientNavigationPathFailureSeconds { get; private set; }

    [JsonIgnore]
    public bool HasArrivalApproachOffset { get; private set; }

    [JsonIgnore]
    public bool IsCompletingArrivalApproach { get; private set; }

    [JsonIgnore]
    public Vector2 ArrivalApproachOffset { get; private set; }

    [JsonIgnore]
    public Vector2 WorldPosition
    {
        get => new(WorldX, WorldY);
        set
        {
            WorldX = value.X;
            WorldY = value.Y;
        }
    }

    [JsonIgnore]
    public Vector2 Destination
    {
        get => new(DestinationX, DestinationY);
        set
        {
            DestinationX = value.X;
            DestinationY = value.Y;
        }
    }

    public bool HasValidNavigationPath(Vector2 destination, int surfaceVersion)
    {
        return HasNavigationPath &&
               NavigationSurfaceVersion == surfaceVersion &&
               NavigationPathDestination.DistanceSquaredTo(destination) <= 0.001f &&
               NavigationPathPoints.Count > 0 &&
               NavigationPathPointIndex >= 0 &&
               NavigationPathPointIndex < NavigationPathPoints.Count;
    }

    public void SetNavigationPath(IEnumerable<Vector2> points, Vector2 destination, int surfaceVersion)
    {
        NavigationPathPoints.Clear();
        if (points != null)
        {
            NavigationPathPoints.AddRange(points);
        }

        NavigationPathDestination = destination;
        NavigationSurfaceVersion = surfaceVersion;
        HasNavigationPath = NavigationPathPoints.Count > 0;
        NavigationPathPointIndex = NavigationPathPoints.Count > 1 ? 1 : 0;
        ClearTransientNavigationPathFailures();
    }

    public void ClearNavigationPath()
    {
        NavigationPathPoints.Clear();
        NavigationPathPointIndex = 0;
        NavigationSurfaceVersion = -1;
        NavigationPathDestination = default;
        HasNavigationPath = false;
        ClearTransientNavigationPathFailures();
    }

    public void RecordTransientNavigationPathFailure(double elapsedSeconds)
    {
        TransientNavigationPathFailureCount++;
        TransientNavigationPathFailureSeconds += Mathf.Max(0.0f, (float)elapsedSeconds);
    }

    public void ClearTransientNavigationPathFailures()
    {
        TransientNavigationPathFailureCount = 0;
        TransientNavigationPathFailureSeconds = 0.0f;
    }

    public void SetTargetApproachDirection(WorldSiteAttackDirection direction)
    {
        TargetApproachDirection = direction;
    }

    public void ClearTargetApproachDirection()
    {
        TargetApproachDirection = WorldSiteAttackDirection.Any;
    }

    public void SetArrivalApproachOffset(Vector2 offset)
    {
        ArrivalApproachOffset = offset;
        HasArrivalApproachOffset = offset.LengthSquared() > 0.001f;
        IsCompletingArrivalApproach = false;
    }

    public void BeginArrivalApproach()
    {
        IsCompletingArrivalApproach = HasArrivalApproachOffset;
    }

    public void ClearArrivalApproachOffset()
    {
        ArrivalApproachOffset = default;
        HasArrivalApproachOffset = false;
        IsCompletingArrivalApproach = false;
    }
}
