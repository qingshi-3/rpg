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
        policy.Contains("ScaleAnimationSecondsByAttackSpeed", StringComparison.Ordinal),
        "timing policy should own SpriteFrames cue target seconds, loop policy, one-shot return policy, and attack-speed scaling helpers");
    AssertTrue(
        component.Contains("UnitAnimationTimingPolicy.ResolveTargetSpriteSeconds", StringComparison.Ordinal) &&
        component.Contains("UnitAnimationTimingPolicy.ApplyAnimatedSpriteLoopMode", StringComparison.Ordinal) &&
        component.Contains("UnitAnimationTimingPolicy.ShouldReturnToIdleAfterCue", StringComparison.Ordinal) &&
        !component.Contains("AnimationPlayer", StringComparison.Ordinal) &&
        !policy.Contains("ResolveAnimationPlayerSpeedScale", StringComparison.Ordinal),
        "unit animation component should delegate cue timing decisions to the policy");
    AssertTrue(
        !component.Contains("private double ResolveTargetSpriteSeconds", StringComparison.Ordinal) &&
        !component.Contains("private static void ApplyAnimatedSpriteLoopMode", StringComparison.Ordinal) &&
        !component.Contains("private static bool ShouldReturnToIdleAfterCue", StringComparison.Ordinal),
        "unit animation component should not keep the extracted timing policy implementations");
}

internal static void UnitAnimationComponentSupportsSpriteFrameHoldAndResume()
{
    string component = string.Join("\n", Directory
        .GetFiles(Path.Combine("src", "Presentation", "Battle", "Entities"), "UnitAnimationComponent*.cs")
        .OrderBy(path => path, StringComparer.Ordinal)
        .Select(File.ReadAllText));
    string animationSet = File.ReadAllText(Path.Combine("src", "Definitions", "Battle", "Animation", "BattleUnitAnimationSet.cs"));
    string unitRoot = ReadBattleUnitRootSource();
    string liveObservation = ReadBattleRuntimeLiveObservationSource();

    AssertTrue(
        component.Contains("public bool PlaySkillCastHoldAtFrame(", StringComparison.Ordinal) &&
        component.Contains("public bool ResumeHeldAnimationFromNextFrame(", StringComparison.Ordinal) &&
        component.Contains("private void OnAnimatedSpriteFrameChanged()", StringComparison.Ordinal) &&
        component.Contains("_animatedSprite.Frame >= _heldAnimationFrame", StringComparison.Ordinal) &&
        component.Contains("_animatedSprite.Pause()", StringComparison.Ordinal) &&
        component.Contains("_animatedSprite.SetFrameAndProgress(resumeFrame, 0f)", StringComparison.Ordinal) &&
        component.Contains("_animatedSprite.Play()", StringComparison.Ordinal) &&
        !component.Contains("AnimationPlayer", StringComparison.Ordinal),
        "unit animation frame control should use AnimatedSprite2D/SpriteFrames frame numbers, pause exactly at the held frame, and resume from the next frame without AnimationPlayer fallback");
    AssertTrue(
        animationSet.Contains("public int ChanneledSkillCastHoldFrame { get; set; } = 2;", StringComparison.Ordinal),
        "unit animation resources should expose the channeled skill cast hold frame as an authored concrete frame number");
    AssertTrue(
        unitRoot.Contains("HeroSkillCommandIds.ThunderSpiralBreakSkillId", StringComparison.Ordinal) &&
        unitRoot.Contains("PlaySkillCastHoldAtFrame", StringComparison.Ordinal) &&
        unitRoot.Contains("ResumeHeldAnimationFromNextFrame", StringComparison.Ordinal),
        "thunder spiral presentation should route through the frame-hold cast path and release the held animation when the runtime duration ends");
    AssertTrue(
        liveObservation.Contains("runtimeEvent.SourceDefinitionId", StringComparison.Ordinal) &&
        liveObservation.Contains("sourceDefinitionId: runtimeEvent.SourceDefinitionId", StringComparison.Ordinal) &&
        liveObservation.Contains("runtimeEvent.ActionDurationSeconds", StringComparison.Ordinal),
        "live SkillUsed observation should pass the runtime skill id and action duration into caster-side presentation instead of guessing locally");
}
}
