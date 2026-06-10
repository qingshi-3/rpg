using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Definitions.Battle.Audio;
using Rpg.Domain.Battle.Grid;
using Rpg.Infrastructure.Logging;

namespace Rpg.Presentation.Battle.Entities;

public partial class BattleUnitRoot
{
    [ExportGroup("Unit Movement Presentation")]

    [Export]
    // Default is 0.28s; battle site scenes may lower this during prototype tuning to keep turn flow readable.
    public double UnitMoveDuration { get; set; } = 0.28;

    [Export]
    // Presentation buffers only committed Runtime movement events. This keeps
    // visual lanes continuous without predicting future cells.
    public double VisualMoveSmoothingSeconds { get; set; } = 0.1;

    private readonly Dictionary<BattleEntity, MovementLane> _movementLanes = new();
    private readonly Dictionary<BattleEntity, double> _pendingMovementIdleSeconds = new();

    public bool HasActiveMovementTweens => _movementLanes.Count > 0;
    public int ActiveMovementTweenCount => _movementLanes.Count;

    public override void _Process(double delta)
    {
        if (_battlePresentationPaused ||
            (_movementLanes.Count == 0 && _pendingMovementIdleSeconds.Count == 0) ||
            delta <= 0)
        {
            return;
        }

        foreach ((BattleEntity entity, MovementLane lane) in _movementLanes.ToArray())
        {
            if (!IsEntityAlive(entity) || !AdvanceMovementLane(entity, lane, delta))
            {
                _movementLanes.Remove(entity);
            }
        }

        UpdatePendingMovementIdle(delta);
    }

    public double MoveEntityTo(
        BattleEntity entity,
        IReadOnlyList<GridSurfacePosition> path,
        bool restartMoveAnimation = true,
        bool returnToIdleOnComplete = true,
        double stepDurationSeconds = -1)
    {
        GridOccupantComponent gridOccupant = entity?.GetComponent<GridOccupantComponent>();
        if (gridOccupant == null)
        {
            return 0;
        }

        GridSurfacePosition previousPosition = gridOccupant.SurfacePosition;
        GridSurfacePosition targetPosition = path == null || path.Count == 0
            ? previousPosition
            : path[^1];
        Vector2 previousGlobal = entity.GlobalPosition;
        gridOccupant.SetSurfacePosition(targetPosition);
        ApplyRenderSort(entity, previousPosition);
        if (TryBuildMovementGlobalPath(
                path,
                gridOccupant,
                previousGlobal,
                previousPosition,
                out Vector2[] globalPath,
                out GridSurfacePosition[] surfacePath))
        {
            _pendingMovementIdleSeconds.Remove(entity);
            entity.GetComponent<BattleUnitAudioComponent>()?.PlayCue(BattleUnitAudioCue.Move);
            double visualDurationSeconds = AnimateEntityMove(
                entity,
                globalPath,
                surfacePath,
                returnToIdleOnComplete,
                stepDurationSeconds,
                restartMoveAnimation);
            GameLog.Trace(nameof(BattleUnitRoot),
                $"Entity visual move id={entity.EntityId} fromCell={previousPosition} toCell={targetPosition} footprint={gridOccupant.FootprintWidth}x{gridOccupant.FootprintHeight} steps={System.Math.Max(0, globalPath.Length - 1)} fromGlobal={previousGlobal} toGlobal={globalPath[^1]} stepDuration={ResolveMoveStepDurationSeconds(stepDurationSeconds):0.00}");
            return visualDurationSeconds;
        }

        if (TryResolveMovementGlobalPosition(gridOccupant, targetPosition, out Vector2 fallbackGlobal))
        {
            entity.GetComponent<UnitAnimationComponent>()?.FaceToward(fallbackGlobal);
            entity.GlobalPosition = fallbackGlobal;
            ApplyRenderSort(entity, targetPosition);
        }

        GameLog.Warn(
            nameof(BattleUnitRoot),
            $"Entity grid moved without step animation id={entity.EntityId} fromCell={previousPosition} toCell={targetPosition}");
        return 0;
    }

    private void ClearMovementPresentationState()
    {
        _movementLanes.Clear();
        _pendingMovementIdleSeconds.Clear();
    }

    private bool TryBuildMovementGlobalPath(
        IReadOnlyList<GridSurfacePosition> path,
        GridOccupantComponent gridOccupant,
        Vector2 currentGlobal,
        GridSurfacePosition currentSurfacePosition,
        out Vector2[] globalPath,
        out GridSurfacePosition[] surfacePath)
    {
        var points = new List<Vector2> { currentGlobal };
        var surfaces = new List<GridSurfacePosition> { currentSurfacePosition };

        if (path == null || path.Count == 0)
        {
            globalPath = points.ToArray();
            surfacePath = surfaces.ToArray();
            return false;
        }

        for (int index = 1; index < path.Count; index++)
        {
            if (!TryResolveMovementGlobalPosition(gridOccupant, path[index], out Vector2 globalPosition))
            {
                globalPath = points.ToArray();
                surfacePath = surfaces.ToArray();
                return false;
            }

            if (points[^1].DistanceSquaredTo(globalPosition) > 0.01f)
            {
                points.Add(globalPosition);
                surfaces.Add(path[index]);
            }
        }

        globalPath = points.ToArray();
        surfacePath = surfaces.ToArray();
        return globalPath.Length > 1;
    }

    private bool TryResolveMovementGlobalPosition(
        GridOccupantComponent gridOccupant,
        GridSurfacePosition surfacePosition,
        out Vector2 globalPosition)
    {
        globalPosition = default;
        if (gridOccupant == null)
        {
            return false;
        }

        if (_tryResolveFootprintGlobalPosition?.Invoke(
                surfacePosition.Position,
                new Vector2I(gridOccupant.FootprintWidth, gridOccupant.FootprintHeight),
                out globalPosition) == true)
        {
            return true;
        }

        return false;
    }

    private double AnimateEntityMove(
        BattleEntity entity,
        IReadOnlyList<Vector2> globalPath,
        IReadOnlyList<GridSurfacePosition> surfacePath,
        bool returnToIdleOnComplete,
        double stepDurationSeconds,
        bool restartMoveAnimation)
    {
        if (!IsEntityAlive(entity))
        {
            return 0;
        }

        double resolvedStepDurationSeconds = ResolveMoveStepDurationSeconds(stepDurationSeconds);
        if (!IsInsideTree() || resolvedStepDurationSeconds <= 0)
        {
            entity.GlobalPosition = globalPath[^1];
            if (surfacePath?.Count > 0)
            {
                ApplyRenderSort(entity, surfacePath[^1]);
            }
            return 0;
        }

        bool createdLane = false;
        if (!_movementLanes.TryGetValue(entity, out MovementLane lane))
        {
            lane = new MovementLane(entity.GlobalPosition, ResolveSurfaceAt(surfacePath, 0), ResolveVisualMoveBufferSeconds());
            _movementLanes[entity] = lane;
            createdLane = true;
        }

        // Movement events are Runtime facts, but the visual path is actor-local
        // so consecutive grid steps can be stitched into one continuous stream.
        lane.CancelContinuationHold();
        lane.Enqueue(globalPath, surfacePath, resolvedStepDurationSeconds, returnToIdleOnComplete);
        if (lane.HasSegments)
        {
            if (createdLane)
            {
                StartMoveAnimationForLane(entity, restartMoveAnimation);
            }

            SetProcess(true);
        }

        int segmentCount = System.Math.Max(0, globalPath.Count - 1);
        double startupDelaySeconds = createdLane ? ResolveVisualMoveBufferSeconds() : 0;
        return startupDelaySeconds + resolvedStepDurationSeconds * segmentCount;
    }

    private bool AdvanceMovementLane(BattleEntity entity, MovementLane lane, double delta)
    {
        if (!IsInsideTree() || lane == null || !lane.IsActive)
        {
            return false;
        }

        double remainingSeconds = delta;
        if (lane.HasStartupDelay)
        {
            remainingSeconds = lane.ConsumeStartupDelay(remainingSeconds);
            if (remainingSeconds <= 0)
            {
                return true;
            }
        }

        while (remainingSeconds > 0 && lane.TryGetCurrent(out MovementSegment segment))
        {
            if (lane.IsNewSegment)
            {
                ApplyRenderSort(entity, segment.FromSurface);
                FaceAlongSegment(entity.GetComponent<UnitAnimationComponent>(), segment.From, segment.To);
                lane.MarkSegmentApplied();
            }

            double segmentDuration = System.Math.Max(0.001, segment.DurationSeconds);
            double consumeSeconds = System.Math.Min(remainingSeconds, segmentDuration - lane.ElapsedSeconds);
            lane.ElapsedSeconds += consumeSeconds;
            remainingSeconds -= consumeSeconds;

            float progress = (float)Mathf.Clamp(lane.ElapsedSeconds / segmentDuration, 0, 1);
            entity.GlobalPosition = segment.From.Lerp(segment.To, progress);

            if (lane.ElapsedSeconds + 0.0001 < segmentDuration)
            {
                break;
            }

            entity.GlobalPosition = segment.To;
            ApplyRenderSort(entity, segment.ToSurface);
            lane.CompleteCurrentSegment();
        }

        if (lane.HasSegments)
        {
            return true;
        }

        if (!lane.HasContinuationHold)
        {
            lane.BeginContinuationHold(ResolveVisualMoveBufferSeconds());
        }

        if (lane.HasContinuationHold)
        {
            lane.ConsumeContinuationHold(remainingSeconds);
            if (lane.HasContinuationHold)
            {
                return true;
            }
        }

        if (lane.ReturnToIdleOnComplete)
        {
            // Fixed-clock RTS runtime can enqueue a continuation shortly after one segment drains.
            // This grace window keeps move animation stable instead of flashing idle between cells.
            _pendingMovementIdleSeconds[entity] = ResolveMovementIdleGraceSeconds();
        }

        return false;
    }

    private void UpdatePendingMovementIdle(double delta)
    {
        if (_pendingMovementIdleSeconds.Count == 0)
        {
            return;
        }

        foreach ((BattleEntity entity, double seconds) in _pendingMovementIdleSeconds.ToArray())
        {
            if (!IsEntityAlive(entity) || _movementLanes.ContainsKey(entity))
            {
                _pendingMovementIdleSeconds.Remove(entity);
                continue;
            }

            double remaining = seconds - delta;
            if (remaining > 0)
            {
                _pendingMovementIdleSeconds[entity] = remaining;
                continue;
            }

            _pendingMovementIdleSeconds.Remove(entity);
            entity.GetComponent<UnitAnimationComponent>()?.PlayIdle();
        }
    }

    private double ResolveMovementIdleGraceSeconds()
    {
        return System.Math.Max(0.04, ResolveVisualMoveBufferSeconds());
    }

    private double ResolveVisualMoveBufferSeconds()
    {
        return System.Math.Clamp(VisualMoveSmoothingSeconds, 0.02, 0.16);
    }

    private void StartMoveAnimationForLane(BattleEntity entity, bool restartMoveAnimation)
    {
        if (!IsEntityAlive(entity))
        {
            return;
        }

        entity.GetComponent<UnitAnimationComponent>()?.PlayMove(restartMoveAnimation);
    }

    private double ResolveMoveStepDurationSeconds(double stepDurationSeconds)
    {
        return ResolveVisualMoveStepDurationSeconds(stepDurationSeconds);
    }

    public double ResolveVisualMoveStepDurationSeconds(double stepDurationSeconds)
    {
        double baseSeconds = stepDurationSeconds > 0 ? stepDurationSeconds : UnitMoveDuration;
        return System.Math.Max(0.01, baseSeconds);
    }

    private static bool IsEntityAlive(BattleEntity entity)
    {
        return entity != null && GodotObject.IsInstanceValid(entity);
    }

    private static GridSurfacePosition ResolveSurfaceAt(
        IReadOnlyList<GridSurfacePosition> surfacePath,
        int index)
    {
        if (surfacePath == null || surfacePath.Count == 0)
        {
            return default;
        }

        return surfacePath[System.Math.Clamp(index, 0, surfacePath.Count - 1)];
    }

    private static void FaceAlongSegment(UnitAnimationComponent animation, Vector2 fromGlobal, Vector2 toGlobal)
    {
        animation?.FaceHorizontalDirection(toGlobal.X - fromGlobal.X);
    }

    private sealed class MovementLane
    {
        private readonly Queue<MovementSegment> _segments = new();
        private MovementSegment _current;
        private bool _hasCurrent;
        private Vector2 _queuedEndPoint;
        private GridSurfacePosition _queuedEndSurface;
        private double _startupDelaySeconds;
        private double _continuationHoldSeconds;

        public MovementLane(Vector2 startPoint, GridSurfacePosition startSurface, double startupDelaySeconds)
        {
            _queuedEndPoint = startPoint;
            _queuedEndSurface = startSurface;
            _startupDelaySeconds = System.Math.Max(0, startupDelaySeconds);
        }

        public double ElapsedSeconds { get; set; }
        public bool IsNewSegment { get; private set; }
        public bool ReturnToIdleOnComplete { get; private set; }
        public bool HasSegments => _hasCurrent || _segments.Count > 0;
        public bool HasStartupDelay => _startupDelaySeconds > 0 && HasSegments;
        public bool HasContinuationHold => _continuationHoldSeconds > 0;
        public bool IsActive => HasSegments || HasContinuationHold;

        public void Enqueue(
            IReadOnlyList<Vector2> globalPath,
            IReadOnlyList<GridSurfacePosition> surfacePath,
            double stepDurationSeconds,
            bool returnToIdleOnComplete)
        {
            ReturnToIdleOnComplete = returnToIdleOnComplete;
            if (globalPath == null || globalPath.Count < 2)
            {
                return;
            }

            Vector2 from = _queuedEndPoint;
            GridSurfacePosition fromSurface = _queuedEndSurface;
            for (int index = 1; index < globalPath.Count; index++)
            {
                Vector2 to = globalPath[index];
                GridSurfacePosition toSurface = ResolveSurfaceAt(surfacePath, index);
                if (from.DistanceSquaredTo(to) <= 0.01f)
                {
                    from = to;
                    fromSurface = toSurface;
                    continue;
                }

                _segments.Enqueue(new MovementSegment(from, to, fromSurface, toSurface, stepDurationSeconds));
                from = to;
                fromSurface = toSurface;
            }

            _queuedEndPoint = from;
            _queuedEndSurface = fromSurface;
        }

        public double ConsumeStartupDelay(double seconds)
        {
            if (_startupDelaySeconds <= 0 || seconds <= 0)
            {
                return seconds;
            }

            double consumed = System.Math.Min(seconds, _startupDelaySeconds);
            _startupDelaySeconds -= consumed;
            return seconds - consumed;
        }

        public void BeginContinuationHold(double seconds)
        {
            _continuationHoldSeconds = System.Math.Max(_continuationHoldSeconds, System.Math.Max(0, seconds));
        }

        public void CancelContinuationHold()
        {
            _continuationHoldSeconds = 0;
        }

        public void ConsumeContinuationHold(double seconds)
        {
            if (_continuationHoldSeconds <= 0 || seconds <= 0)
            {
                return;
            }

            _continuationHoldSeconds = System.Math.Max(0, _continuationHoldSeconds - seconds);
        }

        public bool TryGetCurrent(out MovementSegment segment)
        {
            if (_hasCurrent)
            {
                segment = _current;
                return true;
            }

            if (_segments.Count == 0)
            {
                segment = default;
                return false;
            }

            _current = _segments.Dequeue();
            _hasCurrent = true;
            IsNewSegment = true;
            ElapsedSeconds = 0;
            segment = _current;
            return true;
        }

        public void MarkSegmentApplied()
        {
            IsNewSegment = false;
        }

        public void CompleteCurrentSegment()
        {
            _hasCurrent = false;
            IsNewSegment = false;
            ElapsedSeconds = 0;
        }
    }

    private readonly record struct MovementSegment(
        Vector2 From,
        Vector2 To,
        GridSurfacePosition FromSurface,
        GridSurfacePosition ToSurface,
        double DurationSeconds);
}
