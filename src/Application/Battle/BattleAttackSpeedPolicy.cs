namespace Rpg.Application.Battle;

public static class BattleAttackSpeedPolicy
{
    // First-slice baseline keeps normal attacks readable; authored unit definitions may override this.
    public const double DefaultAttackSpeed = 0.85;
    public const double MinAttackSpeed = 0.1;
    public const double MaxAttackSpeed = 4.0;

    public static double Normalize(double attackSpeed)
    {
        if (double.IsNaN(attackSpeed) || double.IsInfinity(attackSpeed))
        {
            return DefaultAttackSpeed;
        }

        return System.Math.Clamp(attackSpeed, MinAttackSpeed, MaxAttackSpeed);
    }

    public static double ScaleTargetSeconds(double targetSeconds, double attackSpeed)
    {
        if (targetSeconds <= 0)
        {
            return 0;
        }

        return targetSeconds / Normalize(attackSpeed);
    }
}
