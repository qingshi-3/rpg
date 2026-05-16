using System.Linq;

namespace Rpg.Domain.Battle.Grid;

public static class BattleGridTerrainQueries
{
    private const string WaterTerrainTag = "water";

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
}
