using System.Collections.Generic;

namespace Rpg.Runtime.Battle.Navigation;

internal static class BattleActorFootprint
{
    public const int MaxSupportedFootprintSize = 3;

    public static int NormalizeSize(int value)
    {
        return System.Math.Clamp(value <= 0 ? 1 : value, 1, MaxSupportedFootprintSize);
    }

    public static IEnumerable<BattleGridCoord> Enumerate(BattleRuntimeActor actor)
    {
        return Enumerate(actor, new BattleGridCoord(actor?.GridX ?? 0, actor?.GridY ?? 0, actor?.GridHeight ?? 0));
    }

    public static IEnumerable<BattleGridCoord> Enumerate(BattleRuntimeActor actor, BattleGridCoord anchor)
    {
        int width = NormalizeSize(actor?.FootprintWidth ?? 1);
        int height = NormalizeSize(actor?.FootprintHeight ?? 1);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                yield return new BattleGridCoord(anchor.X + x, anchor.Y + y, anchor.Height);
            }
        }
    }

    public static int GetGap(BattleRuntimeActor first, BattleRuntimeActor second)
    {
        return GetGap(
            first,
            new BattleGridCoord(first?.GridX ?? 0, first?.GridY ?? 0, first?.GridHeight ?? 0),
            second,
            new BattleGridCoord(second?.GridX ?? 0, second?.GridY ?? 0, second?.GridHeight ?? 0));
    }

    public static int GetGap(
        BattleRuntimeActor first,
        BattleGridCoord firstAnchor,
        BattleRuntimeActor second,
        BattleGridCoord secondAnchor)
    {
        if (first == null || second == null)
        {
            return 0;
        }

        return System.Math.Max(
            GetAxisGap(firstAnchor.X, first.FootprintWidth, secondAnchor.X, second.FootprintWidth),
            GetAxisGap(firstAnchor.Y, first.FootprintHeight, secondAnchor.Y, second.FootprintHeight));
    }

    public static int GetOrthogonalGap(
        BattleRuntimeActor first,
        BattleGridCoord firstAnchor,
        BattleRuntimeActor second,
        BattleGridCoord secondAnchor)
    {
        GetAxisGaps(first, firstAnchor, second, secondAnchor, out int gapX, out int gapY);
        if (gapX == 0)
        {
            return gapY;
        }

        if (gapY == 0)
        {
            return gapX;
        }

        return int.MaxValue;
    }

    public static void GetAxisGaps(
        BattleRuntimeActor first,
        BattleGridCoord firstAnchor,
        BattleRuntimeActor second,
        BattleGridCoord secondAnchor,
        out int gapX,
        out int gapY)
    {
        gapX = 0;
        gapY = 0;
        if (first == null || second == null)
        {
            return;
        }

        gapX = GetAxisGap(firstAnchor.X, first.FootprintWidth, secondAnchor.X, second.FootprintWidth);
        gapY = GetAxisGap(firstAnchor.Y, first.FootprintHeight, secondAnchor.Y, second.FootprintHeight);
    }

    private static int GetAxisGap(int firstStart, int firstSize, int secondStart, int secondSize)
    {
        int firstEnd = firstStart + NormalizeSize(firstSize) - 1;
        int secondEnd = secondStart + NormalizeSize(secondSize) - 1;
        if (firstStart > secondEnd)
        {
            return firstStart - secondEnd;
        }

        if (secondStart > firstEnd)
        {
            return secondStart - firstEnd;
        }

        return 0;
    }
}
