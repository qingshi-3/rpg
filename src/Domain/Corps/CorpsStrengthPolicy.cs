using System;

namespace Rpg.Domain.Corps;

public static class CorpsStrengthPolicy
{
    public const int MinStrength = 0;
    public const int MaxStrength = 100;

    public static int Clamp(int value)
    {
        return Math.Clamp(value, MinStrength, MaxStrength);
    }

    public static int CalculateVisibleSoldiers(int corpsStrength, int maxVisibleSoldiers)
    {
        int clampedStrength = Clamp(corpsStrength);
        int clampedMax = Math.Max(0, maxVisibleSoldiers);
        if (clampedStrength <= 0 || clampedMax == 0)
        {
            return 0;
        }

        return Math.Clamp((int)Math.Ceiling(clampedStrength / 100.0 * clampedMax), 1, clampedMax);
    }
}
