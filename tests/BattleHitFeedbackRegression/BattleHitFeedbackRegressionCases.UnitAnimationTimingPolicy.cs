internal static partial class BattleHitFeedbackRegressionCases
{
internal static void UnitAnimationComponentDelegatesCueTimingPolicy()
{
    string policyPath = Path.Combine("src", "Presentation", "Battle", "Entities", "UnitAnimationTimingPolicy.cs");
    string component = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "UnitAnimationComponent.cs"));

    AssertTrue(File.Exists(policyPath), "unit animation cue timing should live in a focused presentation policy");
    string policy = File.ReadAllText(policyPath);

    AssertTrue(
        policy.Contains("internal static class UnitAnimationTimingPolicy", StringComparison.Ordinal) &&
        policy.Contains("ResolveTargetSpriteSeconds", StringComparison.Ordinal) &&
        policy.Contains("ApplyAnimatedSpriteLoopMode", StringComparison.Ordinal) &&
        policy.Contains("ShouldReturnToIdleAfterCue", StringComparison.Ordinal) &&
        policy.Contains("ResolveAnimationPlayerSpeedScale", StringComparison.Ordinal) &&
        policy.Contains("ScaleAnimationSecondsByAttackSpeed", StringComparison.Ordinal),
        "timing policy should own cue target seconds, loop policy, one-shot return policy, and attack-speed scaling helpers");
    AssertTrue(
        component.Contains("UnitAnimationTimingPolicy.ResolveTargetSpriteSeconds", StringComparison.Ordinal) &&
        component.Contains("UnitAnimationTimingPolicy.ApplyAnimatedSpriteLoopMode", StringComparison.Ordinal) &&
        component.Contains("UnitAnimationTimingPolicy.ShouldReturnToIdleAfterCue", StringComparison.Ordinal) &&
        component.Contains("UnitAnimationTimingPolicy.ResolveAnimationPlayerSpeedScale", StringComparison.Ordinal),
        "unit animation component should delegate cue timing decisions to the policy");
    AssertTrue(
        !component.Contains("private double ResolveTargetSpriteSeconds", StringComparison.Ordinal) &&
        !component.Contains("private static void ApplyAnimatedSpriteLoopMode", StringComparison.Ordinal) &&
        !component.Contains("private static bool ShouldReturnToIdleAfterCue", StringComparison.Ordinal),
        "unit animation component should not keep the extracted timing policy implementations");
}
}
