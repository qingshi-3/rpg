using System.Linq;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.Battle.Rules;

public static class BattleRuleQueries
{
    private const string WaterTerrainTag = "water";

    public static bool IsDefeated(BattleEntity entity)
    {
        return entity?.GetComponent<HealthComponent>()?.IsDead == true;
    }

    public static bool AreHostile(BattleEntity left, BattleEntity right)
    {
        BattleFaction leftFaction = left?.GetComponent<FactionComponent>()?.Faction ?? BattleFaction.Neutral;
        BattleFaction rightFaction = right?.GetComponent<FactionComponent>()?.Faction ?? BattleFaction.Neutral;
        return leftFaction != BattleFaction.Neutral &&
               rightFaction != BattleFaction.Neutral &&
               leftFaction != rightFaction;
    }

    public static bool TryGetMovementBlockSurface(
        BattleEntity movingEntity,
        BattleEntity blocker,
        out GridSurfacePosition blockSurface)
    {
        blockSurface = default;
        if (movingEntity == null ||
            blocker == null ||
            blocker == movingEntity ||
            IsDefeated(blocker) ||
            !AreHostile(movingEntity, blocker))
        {
            return false;
        }

        GridOccupantComponent gridOccupant = blocker.GetComponent<GridOccupantComponent>();
        if (gridOccupant is not { BlocksMovement: true })
        {
            return false;
        }

        blockSurface = gridOccupant.SurfacePosition;
        return true;
    }

    public static bool CanSpendActionPoints(BattleEntity entity, int cost)
    {
        if (cost <= 0)
        {
            return true;
        }

        ActionPointComponent actionPoint = entity?.GetComponent<ActionPointComponent>();
        return actionPoint != null && actionPoint.CanSpend(cost);
    }

    public static bool CanEnterCell(BattleEntity entity, GridCell cell)
    {
        if (entity == null || cell == null)
        {
            return false;
        }

        MovementComponent movement = entity.GetComponent<MovementComponent>();
        if (movement == null)
        {
            return false;
        }

        if (IsWater(cell) && !movement.CanEnterWater)
        {
            return false;
        }

        return true;
    }

    public static bool CanEnterSurface(BattleEntity entity, GridCellSurface surface)
    {
        if (entity == null || surface == null)
        {
            return false;
        }

        MovementComponent movement = entity.GetComponent<MovementComponent>();
        if (movement == null)
        {
            return false;
        }

        if (IsWater(surface) && !movement.CanEnterWater)
        {
            return false;
        }

        return true;
    }

    public static bool IsWater(GridCell cell)
    {
        return cell != null &&
               (string.Equals(cell.TerrainTag, WaterTerrainTag, System.StringComparison.OrdinalIgnoreCase) ||
                cell.TerrainTags.Contains(WaterTerrainTag, System.StringComparer.OrdinalIgnoreCase));
    }

    public static bool IsWater(GridCellSurface surface)
    {
        return surface != null &&
               (string.Equals(surface.TerrainTag, WaterTerrainTag, System.StringComparison.OrdinalIgnoreCase) ||
                surface.TerrainTags.Contains(WaterTerrainTag, System.StringComparer.OrdinalIgnoreCase));
    }

    public static int GetManhattanDistance(GridPosition left, GridPosition right)
    {
        return System.Math.Abs(left.X - right.X) + System.Math.Abs(left.Y - right.Y);
    }
}
