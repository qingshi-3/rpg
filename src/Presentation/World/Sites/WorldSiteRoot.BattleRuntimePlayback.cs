using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rpg.Domain.Battle.Grid;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle.Actions;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Rules;
using Rpg.Runtime.Battle.Events;

namespace Rpg.Presentation.World.Sites;

public partial class WorldSiteRoot
{
    private async Task PlayRuntimeMovementEventAsync(
        BattleEvent runtimeEvent,
        IReadOnlyDictionary<string, BattleEntity> entitiesByRuntimeActor)
    {
        if (!entitiesByRuntimeActor.TryGetValue(runtimeEvent.ActorId ?? "", out BattleEntity actor) ||
            !entitiesByRuntimeActor.TryGetValue(runtimeEvent.TargetId ?? "", out BattleEntity target) ||
            !TryResolveNextCombatStep(actor, target, out GridSurfacePosition nextStep))
        {
            return;
        }

        GridOccupantComponent actorGrid = actor.GetComponent<GridOccupantComponent>();
        _unitRoot.MoveEntityTo(actor, new[] { actorGrid.SurfacePosition, nextStep });
        await WaitSiteBattlePresentationSeconds(_unitRoot.UnitMoveDuration);
    }

    private async Task PlayRuntimeDamageEventAsync(
        BattleEvent runtimeEvent,
        IReadOnlyDictionary<string, BattleEntity> entitiesByRuntimeActor)
    {
        if (!entitiesByRuntimeActor.TryGetValue(runtimeEvent.ActorId ?? "", out BattleEntity actor) ||
            !entitiesByRuntimeActor.TryGetValue(runtimeEvent.TargetId ?? "", out BattleEntity target))
        {
            return;
        }

        await MoveRuntimeActorIntoVisualAttackRangeAsync(actor, target);
        if (!IsInRuntimeVisualAttackRange(actor, target))
        {
            GameLog.Warn(
                nameof(WorldSiteRoot),
                $"Runtime visual attack played before adjacency actor={actor.EntityId} target={target.EntityId} reason={runtimeEvent.ReasonCode}");
        }

        int damage = System.Math.Max(0, -runtimeEvent.CorpsStrengthDelta);
        HealthComponent health = target.GetComponent<HealthComponent>();
        int applied = health?.ApplyDamage(damage, actor) ?? 0;
        bool defeated = BattleRuleQueries.IsDefeated(target);
        _unitRoot.PlayActionResultAnimation(BattleActionResult.AttackSucceeded(
            actor,
            target,
            applied,
            defeated,
            runtimeEvent.ReasonCode));
        if (defeated)
        {
            _unitRoot.MarkEntityDefeated(target);
        }

        await WaitSiteBattlePresentationSeconds(0.42);
    }

    private async Task MoveRuntimeActorIntoVisualAttackRangeAsync(BattleEntity actor, BattleEntity target)
    {
        const int maxApproachStepsPerDamageEvent = 8;

        // Runtime owns the result, while this presentation bridge must keep the
        // visible actors on valid grid surfaces before playing melee damage.
        for (int i = 0; i < maxApproachStepsPerDamageEvent && !IsInRuntimeVisualAttackRange(actor, target); i++)
        {
            if (!TryResolveNextCombatStep(actor, target, out GridSurfacePosition nextStep))
            {
                return;
            }

            GridOccupantComponent actorGrid = actor.GetComponent<GridOccupantComponent>();
            _unitRoot.MoveEntityTo(actor, new[] { actorGrid.SurfacePosition, nextStep });
            await WaitSiteBattlePresentationSeconds(_unitRoot.UnitMoveDuration);
        }
    }

    private bool TryResolveNextCombatStep(
        BattleEntity actor,
        BattleEntity target,
        out GridSurfacePosition nextStep)
    {
        nextStep = default;
        GridOccupantComponent actorGrid = actor?.GetComponent<GridOccupantComponent>();
        GridOccupantComponent targetGrid = target?.GetComponent<GridOccupantComponent>();
        if (actorGrid == null || targetGrid == null)
        {
            return false;
        }

        if (IsInRuntimeVisualAttackRange(actorGrid, targetGrid))
        {
            return false;
        }

        return TryResolveRuntimeVisualPathStep(actor, actorGrid.SurfacePosition, targetGrid.SurfacePosition, out nextStep);
    }

    private bool TryResolveRuntimeVisualPathStep(
        BattleEntity actor,
        GridSurfacePosition start,
        GridSurfacePosition target,
        out GridSurfacePosition nextStep)
    {
        nextStep = default;
        if (_activeGridMap == null)
        {
            return false;
        }

        ISet<GridSurfacePosition> blockedSurfaces = BuildBlockedMovementSurfaces(actor);
        blockedSurfaces.Remove(start);
        MovementRangeResult movementRange = MovementRangeFinder.FindReachableCells(
            _activeGridMap,
            start,
            maxMoveCost: 256,
            blockedSurfaces,
            surface => IsValidMovementDestination(actor, surface));
        if (!movementRange.HasValidStart || movementRange.DestinationSurfaces.Count == 0)
        {
            return false;
        }

        GridSurfacePosition destination = movementRange.DestinationSurfaces
            .Where(surface => BattleRuleQueries.GetManhattanDistance(surface.Position, target.Position) <= 1)
            .OrderBy(surface => movementRange.ReachableSurfaceCosts.TryGetValue(surface, out int cost) ? cost : int.MaxValue)
            .ThenBy(surface => BattleRuleQueries.GetManhattanDistance(start.Position, surface.Position))
            .ThenBy(surface => surface.Y)
            .ThenBy(surface => surface.X)
            .DefaultIfEmpty(movementRange.DestinationSurfaces
                .OrderBy(surface => BattleRuleQueries.GetManhattanDistance(surface.Position, target.Position))
                .ThenBy(surface => movementRange.ReachableSurfaceCosts.TryGetValue(surface, out int cost) ? cost : int.MaxValue)
                .ThenBy(surface => surface.Y)
                .ThenBy(surface => surface.X)
                .First())
            .First();

        if (!movementRange.TryBuildPathTo(destination, out IReadOnlyList<GridSurfacePosition> path) || path.Count < 2)
        {
            return false;
        }

        nextStep = path[1];
        return true;
    }

    private static bool IsInRuntimeVisualAttackRange(BattleEntity actor, BattleEntity target)
    {
        GridOccupantComponent actorGrid = actor?.GetComponent<GridOccupantComponent>();
        GridOccupantComponent targetGrid = target?.GetComponent<GridOccupantComponent>();
        return IsInRuntimeVisualAttackRange(actorGrid, targetGrid);
    }

    private static bool IsInRuntimeVisualAttackRange(GridOccupantComponent actorGrid, GridOccupantComponent targetGrid)
    {
        return actorGrid != null &&
               targetGrid != null &&
               BattleRuleQueries.GetManhattanDistance(actorGrid.Position, targetGrid.Position) <= 1;
    }
}
