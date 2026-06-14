internal static partial class BattleHitFeedbackRegressionCases
{
internal static void UnitAttackSpeedContract()
{
    string definition = File.ReadAllText(Path.Combine("src", "Definitions", "Battle", "BattleUnitDefinition.cs"));
    string forceRequest = File.ReadAllText(Path.Combine("src", "Application", "Battle", "BattleForceRequest.cs"));
    string snapshot = File.ReadAllText(Path.Combine("src", "Application", "Battle", "Snapshots", "BattleGroupSnapshot.cs"));
    string attackSpeedPolicy = File.ReadAllText(Path.Combine("src", "Application", "Battle", "BattleAttackSpeedPolicy.cs"));
    string probe = File.ReadAllText(Path.Combine("src", "Application", "Battle", "BattleGroupSessionProbeService.cs"));
    string runtimeActor = File.ReadAllText(Path.Combine("src", "Runtime", "Battle", "BattleRuntimeActor.cs"));
    string runtimeSession = File.ReadAllText(Path.Combine("src", "Runtime", "Battle", "BattleRuntimeSession.cs"));
    string runtimeTickResolver = File.ReadAllText(Path.Combine("src", "Runtime", "Battle", "BattleRuntimeTickResolver.cs"));
    string unitFactory = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "BattleUnitFactory.cs"));
    string attackComponent = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "AttackComponent.cs"));
    string animationComponent = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "UnitAnimationComponent.cs"));
    string timingPolicy = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "UnitAnimationTimingPolicy.cs"));
    string siteRuntime = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRequestDeployment.cs"));

    AssertTrue(
        attackSpeedPolicy.Contains("public const double DefaultAttackSpeed = 0.85;", StringComparison.Ordinal),
        "battle attack speed policy should expose the slower first-slice default cadence");
    AssertTrue(
        definition.Contains("public double AttackSpeed { get; set; } = 0.85;", StringComparison.Ordinal),
        "battle unit definitions should expose attack speed with the stable first-slice default");
    AssertTrue(
        definition.Contains("public double AttackImpactNormalizedTimeOverride { get; set; } = -1.0;", StringComparison.Ordinal),
        "battle unit definitions should expose an optional per-unit attack impact timing override");
    AssertTrue(
        forceRequest.Contains("public double AttackSpeed { get; set; } = BattleAttackSpeedPolicy.DefaultAttackSpeed;", StringComparison.Ordinal) &&
        snapshot.Contains("public double AttackSpeed { get; set; } = BattleAttackSpeedPolicy.DefaultAttackSpeed;", StringComparison.Ordinal) &&
        probe.Contains("public double AttackSpeed { get; init; } = BattleAttackSpeedPolicy.DefaultAttackSpeed;", StringComparison.Ordinal),
        "battle handoff contracts should carry attack speed from request to snapshot");
    AssertTrue(
        runtimeActor.Contains("public double AttackSpeed { get; set; } = BattleAttackSpeedPolicy.DefaultAttackSpeed;", StringComparison.Ordinal) &&
        runtimeActor.Contains("public double AttackActionSeconds", StringComparison.Ordinal) &&
        runtimeActor.Contains("public double ActionReadyAtSeconds", StringComparison.Ordinal) &&
        runtimeSession.Contains("AttackSpeed = BattleAttackSpeedPolicy.Normalize(group.AttackSpeed)", StringComparison.Ordinal),
        "runtime actors should consume snapshot attack speed and actor-local action timing");
    AssertTrue(
        runtimeSession.Contains("ResolveAttackActionSeconds", StringComparison.Ordinal) &&
        runtimeTickResolver.Contains("RuntimeTimeSeconds = currentTimeSeconds", StringComparison.Ordinal),
        "runtime attack cadence should be gated by action seconds on the central timeline rather than attacking every integer tick");
    AssertTrue(
        unitFactory.Contains("attack.AttackSpeed = definition.AttackSpeed;", StringComparison.Ordinal) &&
        unitFactory.Contains("animationComponent.AttackSpeed = definition.AttackSpeed;", StringComparison.Ordinal),
        "unit factory should apply attack speed to presentation attack data and animation playback");
    AssertTrue(
        attackComponent.Contains("public double AttackSpeed { get; set; } = BattleAttackSpeedPolicy.DefaultAttackSpeed;", StringComparison.Ordinal) &&
        animationComponent.Contains("public double AttackSpeed { get; set; } = BattleAttackSpeedPolicy.DefaultAttackSpeed;", StringComparison.Ordinal),
        "presentation components should default to the same slower attack speed when no unit definition overrides it");
    AssertTrue(
        unitFactory.Contains("animationComponent.AttackImpactNormalizedTimeOverride = definition.AttackImpactNormalizedTimeOverride;", StringComparison.Ordinal) &&
        animationComponent.Contains("public double AttackImpactNormalizedTimeOverride { get; set; } = -1.0;", StringComparison.Ordinal),
        "unit factory should apply the per-unit impact timing override to presentation hit feedback");
    AssertTrue(
        timingPolicy.Contains("BattleAttackSpeedPolicy.ScaleTargetSeconds(targetSeconds, attackSpeed)", StringComparison.Ordinal) &&
        animationComponent.Contains("UnitAnimationTimingPolicy.ResolveTargetSpriteSeconds", StringComparison.Ordinal) &&
        animationComponent.Contains("ResolveAttackImpactNormalizedTime()", StringComparison.Ordinal),
        "attack animation target duration policy and impact point should use the configured unit attack timing");
    AssertTrue(
        siteRuntime.Contains("ResolveAttackImpactNormalizedTime(definition)", StringComparison.Ordinal) &&
        siteRuntime.Contains("definition.AttackImpactNormalizedTimeOverride >= 0", StringComparison.Ordinal),
        "site battle runtime should prefer per-unit attack impact timing before falling back to the animation resource");
}

internal static void UnitIdleAndMoveAnimationPlaybackIsPacedForReadability()
{
    string animationSet = File.ReadAllText(Path.Combine("src", "Definitions", "Battle", "Animation", "BattleUnitAnimationSet.cs"));
    string animationComponent = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "UnitAnimationComponent.cs"));
    string timingPolicy = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "UnitAnimationTimingPolicy.cs"));

    AssertTrue(
        animationSet.Contains("public float MinBalancedSpeedScale { get; set; } = 0.5f;", StringComparison.Ordinal) &&
        animationComponent.Contains("float minScale = AnimationSet?.MinBalancedSpeedScale ?? 0.5f;", StringComparison.Ordinal),
        "unit sprite frame balancing should allow slowing authored idle/move frames below their imported fps");
    AssertTrue(
        animationSet.Contains("public double TargetIdleCycleSeconds { get; set; } = 2.0;", StringComparison.Ordinal) &&
        timingPolicy.Contains("\"idle\" => animationSet?.TargetIdleCycleSeconds ?? 2.0", StringComparison.Ordinal),
        "idle animation should default to a slower readable loop");
    AssertTrue(
        animationSet.Contains("public double TargetMoveCycleSeconds { get; set; } = 0.55;", StringComparison.Ordinal) &&
        timingPolicy.Contains("\"move\" => animationSet?.TargetMoveCycleSeconds ?? 0.55", StringComparison.Ordinal),
        "move animation should default to a responsive loop while idle remains slower and readable");
    AssertTrue(
        animationComponent.Contains("UnitAnimationTimingPolicy.ResolveTargetSpriteSeconds(cue, minimumTargetSeconds, AnimationSet, AttackSpeed)", StringComparison.Ordinal) &&
        animationComponent.Contains("ResolveBalancedSpriteSpeedScale(authoredSeconds, targetSeconds)", StringComparison.Ordinal),
        "animation pacing should stay inside Presentation sprite playback, not runtime movement timing");
}
}
