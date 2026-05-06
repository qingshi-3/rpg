using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Domain.Battle.Grid;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle.Actions;
using Rpg.Presentation.Battle.Intents;
using Rpg.Presentation.Battle.Rules;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.Battle.Entities;

public delegate bool TryResolveCellGlobalPosition(GridPosition position, out Vector2 globalPosition);

public partial class BattleUnitRoot : Node2D
{
    [ExportGroup("单位移动表现")]

    [Export]
    public double UnitMoveDuration { get; set; } = 0.28;

    private readonly Dictionary<BattleEntity, Tween> _movementTweens = new();
    private readonly Dictionary<BattleEntity, BattleIntentMarker> _intentMarkers = new();
    private TryResolveCellGlobalPosition _tryResolveCellGlobalPosition;

    public bool HasActiveMovementTweens => _movementTweens.Count > 0;

    public override void _Ready()
    {
        GameLog.Info(nameof(BattleUnitRoot), $"Ready path={GetPath()} entities={GetEntitiesSnapshot().Count}");
    }

    public void Initialize(TryResolveCellGlobalPosition tryResolveCellGlobalPosition)
    {
        _tryResolveCellGlobalPosition = tryResolveCellGlobalPosition;
        GameLog.Info(nameof(BattleUnitRoot), $"Initialized path={GetPath()} hasCellResolver={_tryResolveCellGlobalPosition != null}");
    }

    public IReadOnlyList<BattleEntity> GetEntitiesSnapshot()
    {
        return EnumerateBattleEntities(this).ToArray();
    }

    public BattleEntity FindEntityAt(GridPosition position)
    {
        return GetEntitiesSnapshot()
            .Select(entity => new
            {
                Entity = entity,
                GridOccupant = entity.GetComponent<GridOccupantComponent>()
            })
            .Where(item => item.GridOccupant?.Position == position && !BattleRuleQueries.IsDefeated(item.Entity))
            .OrderByDescending(item => item.GridOccupant.GridHeight)
            .Select(item => item.Entity)
            .FirstOrDefault();
    }

    public IEnumerable<BattleEntity> EnumerateAliveFaction(BattleFaction faction)
    {
        foreach (BattleEntity entity in GetEntitiesSnapshot())
        {
            if (!BattleRuleQueries.IsDefeated(entity) &&
                entity.GetComponent<FactionComponent>()?.Faction == faction)
            {
                yield return entity;
            }
        }
    }

    public void RestoreTurnResourcesForFaction(BattleFaction faction)
    {
        foreach (BattleEntity entity in EnumerateAliveFaction(faction))
        {
            entity.GetComponent<ActionPointComponent>()?.RestoreToMax();
            entity.GetComponent<MovementComponent>()?.RestoreMoveUses();
        }
    }

    public ISet<GridSurfacePosition> BuildBlockedMovementSurfaces(BattleEntity movingEntity)
    {
        var blockedSurfaces = new HashSet<GridSurfacePosition>();

        foreach (BattleEntity entity in GetEntitiesSnapshot())
        {
            if (entity == movingEntity || BattleRuleQueries.IsDefeated(entity))
            {
                continue;
            }

            GridOccupantComponent gridOccupant = entity.GetComponent<GridOccupantComponent>();
            if (gridOccupant is { BlocksMovement: true })
            {
                blockedSurfaces.Add(gridOccupant.SurfacePosition);
            }
        }

        return blockedSurfaces;
    }

    public void MoveEntityTo(BattleEntity entity, IReadOnlyList<GridSurfacePosition> path)
    {
        GridOccupantComponent gridOccupant = entity?.GetComponent<GridOccupantComponent>();
        if (gridOccupant == null)
        {
            return;
        }

        GridSurfacePosition previousPosition = gridOccupant.SurfacePosition;
        GridSurfacePosition targetPosition = path == null || path.Count == 0
            ? previousPosition
            : path[^1];
        Vector2 previousGlobal = entity.GlobalPosition;
        gridOccupant.SetSurfacePosition(targetPosition);
        if (TryBuildMovementGlobalPath(path, previousGlobal, out Vector2[] globalPath))
        {
            entity.GetComponent<UnitAnimationComponent>()?.PlayMove();
            AnimateEntityMove(entity, globalPath);
            GameLog.Info(
                nameof(BattleUnitRoot),
                $"Entity visual move id={entity.EntityId} fromCell={previousPosition} toCell={targetPosition} steps={System.Math.Max(0, globalPath.Length - 1)} fromGlobal={previousGlobal} toGlobal={globalPath[^1]} stepDuration={UnitMoveDuration:0.00}");
            return;
        }

        if (_tryResolveCellGlobalPosition?.Invoke(targetPosition.Position, out Vector2 fallbackGlobal) == true)
        {
            entity.GlobalPosition = fallbackGlobal;
        }

        GameLog.Warn(
            nameof(BattleUnitRoot),
            $"Entity grid moved without step animation id={entity.EntityId} fromCell={previousPosition} toCell={targetPosition}");
    }

    public void PlayActionResultAnimation(BattleActionResult result)
    {
        if (result?.Success != true)
        {
            return;
        }

        if (result.Kind is not (BattleActionKind.Ability or BattleActionKind.Attack))
        {
            return;
        }

        result.Actor?.GetComponent<UnitAnimationComponent>()?.PlayAttack();

        if (result.TargetDefeated)
        {
            return;
        }

        if (result.DamageApplied > 0)
        {
            result.Target?.GetComponent<UnitAnimationComponent>()?.PlayHit();
        }
    }

    public void SetIntentMarker(BattleEntity entity, BattleIntent intent)
    {
        if (entity == null)
        {
            return;
        }

        if (!_intentMarkers.TryGetValue(entity, out BattleIntentMarker marker) || marker == null || !GodotObject.IsInstanceValid(marker))
        {
            marker = entity.GetNodeOrNull<BattleIntentMarker>("IntentMarker");
            if (marker == null)
            {
                marker = GameUiSceneFactory.CreateBattleIntentMarker(nameof(BattleUnitRoot));
                if (marker == null)
                {
                    return;
                }

                marker.Name = "IntentMarker";
                entity.AddChild(marker);
            }

            _intentMarkers[entity] = marker;
        }

        marker.SetIntent(intent);
    }

    public void ClearIntentMarkers()
    {
        foreach (BattleIntentMarker marker in _intentMarkers.Values.ToArray())
        {
            if (marker != null && GodotObject.IsInstanceValid(marker))
            {
                marker.SetIntent(null);
            }
        }
    }

    public void MarkEntityDefeated(BattleEntity entity)
    {
        if (entity == null)
        {
            return;
        }

        SetIntentMarker(entity, null);

        GridOccupantComponent gridOccupant = entity.GetComponent<GridOccupantComponent>();
        if (gridOccupant != null)
        {
            gridOccupant.BlocksMovement = false;
        }

        SelectableComponent selectable = entity.GetComponent<SelectableComponent>();
        if (selectable != null)
        {
            selectable.IsSelectable = false;
        }

        TargetableComponent targetable = entity.GetComponent<TargetableComponent>();
        if (targetable != null)
        {
            targetable.IsTargetable = false;
        }

        entity.InputPickable = false;
        SetCollisionShapesDisabled(entity, true);
        entity.DebugMarkerColor = new Color(0.45f, 0.45f, 0.45f, 0.55f);
        entity.QueueRedraw();

        UnitAnimationComponent animation = entity.GetComponent<UnitAnimationComponent>();
        bool willHideAfterAnimation = animation?.PlayDefeated(() => HideDefeatedEntity(entity)) == true;
        if (!willHideAfterAnimation)
        {
            HideDefeatedEntity(entity);
        }

        GameLog.Info(nameof(BattleUnitRoot), $"Entity defeated id={entity.EntityId} name={entity.DisplayName}");
    }

    private bool TryBuildMovementGlobalPath(
        IReadOnlyList<GridSurfacePosition> path,
        Vector2 currentGlobal,
        out Vector2[] globalPath)
    {
        var points = new List<Vector2> { currentGlobal };

        if (path == null || path.Count == 0)
        {
            globalPath = points.ToArray();
            return false;
        }

        for (int index = 1; index < path.Count; index++)
        {
            if (_tryResolveCellGlobalPosition?.Invoke(path[index].Position, out Vector2 globalPosition) != true)
            {
                globalPath = points.ToArray();
                return false;
            }

            if (points[^1].DistanceSquaredTo(globalPosition) > 0.01f)
            {
                points.Add(globalPosition);
            }
        }

        globalPath = points.ToArray();
        return globalPath.Length > 1;
    }

    private void AnimateEntityMove(BattleEntity entity, IReadOnlyList<Vector2> globalPath)
    {
        if (_movementTweens.Remove(entity, out Tween previousTween))
        {
            previousTween.Kill();
        }

        if (!IsInsideTree() || UnitMoveDuration <= 0)
        {
            entity.GlobalPosition = globalPath[^1];
            return;
        }

        entity.GlobalPosition = globalPath[0];
        Tween tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Cubic);
        tween.SetEase(Tween.EaseType.Out);

        for (int index = 1; index < globalPath.Count; index++)
        {
            tween.TweenProperty(entity, "global_position", globalPath[index], UnitMoveDuration);
        }

        tween.Finished += () =>
        {
            entity.GlobalPosition = globalPath[^1];
            _movementTweens.Remove(entity);
            entity.GetComponent<UnitAnimationComponent>()?.PlayIdle();
        };

        _movementTweens[entity] = tween;
    }

    private static void HideDefeatedEntity(BattleEntity entity)
    {
        if (entity == null || !GodotObject.IsInstanceValid(entity))
        {
            return;
        }

        entity.Visible = false;
        entity.Modulate = new Color(1f, 1f, 1f, 0.45f);
        entity.QueueRedraw();
    }

    private static void SetCollisionShapesDisabled(Node node, bool disabled)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is CollisionShape2D collisionShape)
            {
                collisionShape.Disabled = disabled;
            }

            SetCollisionShapesDisabled(child, disabled);
        }
    }

    private static IEnumerable<BattleEntity> EnumerateBattleEntities(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is BattleEntity entity)
            {
                yield return entity;
            }

            foreach (BattleEntity descendant in EnumerateBattleEntities(child))
            {
                yield return descendant;
            }
        }
    }
}
