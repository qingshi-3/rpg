namespace Rpg.Presentation.Battle.Abilities;

public readonly record struct AbilityEffectResult(
    int DamageApplied,
    bool TargetDefeated)
{
    public static AbilityEffectResult None => new(0, false);
}
