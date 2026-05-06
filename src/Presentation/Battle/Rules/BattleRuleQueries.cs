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
        return string.Equals(cell?.TerrainTag, WaterTerrainTag, System.StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsWater(GridCellSurface surface)
    {
        return string.Equals(surface?.TerrainTag, WaterTerrainTag, System.StringComparison.OrdinalIgnoreCase);
    }

    public static int GetManhattanDistance(GridPosition left, GridPosition right)
    {
        return System.Math.Abs(left.X - right.X) + System.Math.Abs(left.Y - right.Y);
    }
}
