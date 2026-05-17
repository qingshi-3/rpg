using System.Collections.Generic;
using Rpg.Domain.Battle.Grid;

namespace Rpg.Application.Battle;

public static class BattleFootprintCells
{
    public const int MaxSupportedFootprintSize = 3;

    public static IReadOnlyList<GridPosition> Enumerate(GridPosition anchor, int width, int height)
    {
        int normalizedWidth = NormalizeSize(width);
        int normalizedHeight = NormalizeSize(height);
        var cells = new List<GridPosition>(normalizedWidth * normalizedHeight);
        for (int y = 0; y < normalizedHeight; y++)
        {
            for (int x = 0; x < normalizedWidth; x++)
            {
                cells.Add(new GridPosition(anchor.X + x, anchor.Y + y));
            }
        }

        return cells;
    }

    public static GridPosition ResolveAnchorFromCenter(float centerX, float centerY, int width, int height)
    {
        return new GridPosition(
            ResolveAnchorAxisFromCenter(centerX, NormalizeSize(width)),
            ResolveAnchorAxisFromCenter(centerY, NormalizeSize(height)));
    }

    public static int NormalizeSize(int value)
    {
        return System.Math.Clamp(value <= 0 ? 1 : value, 1, MaxSupportedFootprintSize);
    }

    private static int ResolveAnchorAxisFromCenter(float centerCoordinate, int footprintSize)
    {
        double anchorOffset = (footprintSize - 2) * 0.5;
        return (int)System.Math.Floor(centerCoordinate - anchorOffset);
    }
}
