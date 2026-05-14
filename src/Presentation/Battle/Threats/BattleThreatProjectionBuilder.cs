using System.Collections.Generic;
using System.Linq;
using Rpg.Definitions.Battle.Abilities;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle.Abilities;
using Rpg.Presentation.Battle.AI;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Rules;

namespace Rpg.Presentation.Battle.Threats;

public static class BattleThreatProjectionBuilder
{
    public static BattleThreatProjection Build(
        BattleAiContext context,
        BattleEntity actor,
        ISet<GridSurfacePosition> blockedMovementSurfaces,
        bool includeSources = true)
    {
        if (context?.GridMap == null || actor == null || BattleRuleQueries.IsDefeated(actor))
        {
            return Empty();
        }

        GridOccupantComponent actorGrid = actor.GetComponent<GridOccupantComponent>();
        if (actorGrid == null)
        {
            return Empty();
        }

        HashSet<GridSurfacePosition> origins = BuildMovementOrigins(
            context,
            actor,
            actorGrid.SurfacePosition,
            blockedMovementSurfaces);
        HashSet<GridPosition> movementCells = origins
            .Where(surface => surface != actorGrid.SurfacePosition)
            .Select(surface => surface.Position)
            .ToHashSet();

        List<BattleThreatSource> sources = includeSources ? new List<BattleThreatSource>() : null;
        HashSet<GridPosition> threatCells = includeSources ? null : new HashSet<GridPosition>();
        foreach (GridSurfacePosition origin in origins)
        {
            foreach (AbilityDefinition ability in BattleAbilityQueries.GetAbilities(actor).Where(CanProjectThreat))
            {
                IBattleAttackPattern pattern = BattleAttackPatternFactory.Resolve(ability);
                var patternContext = new BattleAttackPatternContext(context.GridMap, actor, origin, ability);

                foreach (GridPosition cell in pattern.ProjectThreatCells(patternContext))
                {
                    if (includeSources)
                    {
                        sources.Add(new BattleThreatSource(origin, cell, ability));
                    }
                    else
                    {
                        threatCells.Add(cell);
                    }
                }
            }
        }

        threatCells ??= sources
            .Select(source => source.ThreatCell)
            .Where(position => position != actorGrid.Position)
            .ToHashSet();

        threatCells.Remove(actorGrid.Position);

        BattleEntity[] threatenedTargets = (context.Entities ?? System.Array.Empty<BattleEntity>())
            .Where(entity => IsThreatenedTarget(actor, entity, threatCells))
            .ToArray();

        GridPosition[] targetCells = threatenedTargets
            .Select(target => target.GetComponent<GridOccupantComponent>().Position)
            .Distinct()
            .ToArray();

        IReadOnlyCollection<BattleThreatSource> sourceView =
            sources ?? (IReadOnlyCollection<BattleThreatSource>)System.Array.Empty<BattleThreatSource>();

        return new BattleThreatProjection(
            movementCells,
            threatCells,
            targetCells,
            threatenedTargets,
            sourceView);
    }

    private static HashSet<GridSurfacePosition> BuildMovementOrigins(
        BattleAiContext context,
        BattleEntity actor,
        GridSurfacePosition start,
        ISet<GridSurfacePosition> blockedMovementSurfaces)
    {
        var origins = new HashSet<GridSurfacePosition> { start };

        MovementComponent movement = actor.GetComponent<MovementComponent>();
        if (movement == null ||
            movement.MoveRange <= 0 ||
            !movement.CanUseMove() ||
            !BattleRuleQueries.CanSpendActionPoints(actor, movement.ApCost))
        {
            return origins;
        }

        MovementRangeResult movementRange = MovementRangeFinder.FindReachableCells(
            context.GridMap,
            start,
            movement.MoveRange,
            blockedMovementSurfaces ?? new HashSet<GridSurfacePosition>(),
            surface => BattleRuleQueries.CanEnterSurface(actor, surface));

        foreach (GridSurfacePosition surface in movementRange.DestinationSurfaces)
        {
            origins.Add(surface);
        }

        return origins;
    }

    private static bool CanProjectThreat(AbilityDefinition ability)
    {
        return ability != null && ability.Range >= 0;
    }

    private static bool IsThreatenedTarget(
        BattleEntity actor,
        BattleEntity target,
        ISet<GridPosition> threatCells)
    {
        if (target == null ||
            BattleRuleQueries.IsDefeated(target) ||
            !BattleRuleQueries.AreHostile(actor, target))
        {
            return false;
        }

        if (target.GetComponent<TargetableComponent>() is { IsTargetable: false } ||
            target.GetComponent<HealthComponent>() == null)
        {
            return false;
        }

        GridOccupantComponent targetGrid = target.GetComponent<GridOccupantComponent>();
        return targetGrid != null && threatCells.Contains(targetGrid.Position);
    }

    private static BattleThreatProjection Empty()
    {
        return new BattleThreatProjection(
            System.Array.Empty<GridPosition>(),
            System.Array.Empty<GridPosition>(),
            System.Array.Empty<GridPosition>(),
            System.Array.Empty<BattleEntity>(),
            System.Array.Empty<BattleThreatSource>());
    }
}
