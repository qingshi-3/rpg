namespace Rpg.Application.Battle;

public static class BattleActionTimingPolicy
{
    public const double MinActionSeconds = 0.05;
    public const double MaxActionSeconds = 10.0;
    public const double DefaultSimulationTickSeconds = 0.04;
    public const double DefaultMoveStepSeconds = 0.16;
    public const double DefaultAttackActionSeconds = 1.2;
    public const double DefaultAttackImpactNormalizedTime = 0.55;

    public static double NormalizeActionSeconds(double seconds, double fallbackSeconds)
    {
        double fallback = NormalizeFinite(fallbackSeconds, DefaultMoveStepSeconds);
        double value = NormalizeFinite(seconds, fallback);
        return System.Math.Clamp(value, MinActionSeconds, MaxActionSeconds);
    }

    public static double NormalizeMoveStepSeconds(double seconds, double fallbackSeconds = DefaultMoveStepSeconds)
    {
        double fallback = NormalizeFinite(fallbackSeconds, DefaultMoveStepSeconds);
        double value = NormalizeFinite(seconds, fallback);
        return System.Math.Clamp(value, DefaultSimulationTickSeconds, MaxActionSeconds);
    }

    public static double ResolveAttackActionSeconds(double targetAttackSeconds, double attackSpeed)
    {
        double normalizedTarget = NormalizeActionSeconds(targetAttackSeconds, DefaultAttackActionSeconds);
        return NormalizeActionSeconds(
            BattleAttackSpeedPolicy.ScaleTargetSeconds(normalizedTarget, attackSpeed),
            DefaultAttackActionSeconds);
    }

    public static double ResolveAttackImpactDelaySeconds(double attackActionSeconds, double normalizedImpactTime)
    {
        double duration = NormalizeActionSeconds(attackActionSeconds, DefaultAttackActionSeconds);
        double impactTime = double.IsNaN(normalizedImpactTime) || double.IsInfinity(normalizedImpactTime)
            ? DefaultAttackImpactNormalizedTime
            : System.Math.Clamp(normalizedImpactTime, 0, 1);
        return System.Math.Clamp(duration * impactTime, 0, duration);
    }

    public static double NormalizeAttackImpactDelaySeconds(double impactDelaySeconds, double attackActionSeconds)
    {
        double duration = NormalizeActionSeconds(attackActionSeconds, DefaultAttackActionSeconds);
        if (double.IsNaN(impactDelaySeconds) || double.IsInfinity(impactDelaySeconds) || impactDelaySeconds < 0)
        {
            return ResolveAttackImpactDelaySeconds(duration, DefaultAttackImpactNormalizedTime);
        }

        return System.Math.Clamp(impactDelaySeconds, 0, duration);
    }

    private static double NormalizeFinite(double value, double fallback)
    {
        return double.IsNaN(value) || double.IsInfinity(value) || value <= 0
            ? fallback
            : value;
    }
}
