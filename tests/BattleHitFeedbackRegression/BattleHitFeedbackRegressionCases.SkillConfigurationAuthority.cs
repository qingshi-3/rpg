internal static partial class BattleHitFeedbackRegressionCases
{
internal static void SkillPresentationUsesProfileIdsNotThunderSkillIds()
{
    Type eventType = typeof(Rpg.Runtime.Battle.Events.BattleEvent);

    AssertTrue(eventType.GetProperty("PresentationProfileId") != null, "BattleEvent should expose PresentationProfileId");
    AssertTrue(eventType.GetProperty("CastFxProfileId") != null, "BattleEvent should expose CastFxProfileId");
    AssertTrue(eventType.GetProperty("ImpactFxProfileId") != null, "BattleEvent should expose ImpactFxProfileId");
    AssertTrue(eventType.GetProperty("AreaFxProfileId") != null, "BattleEvent should expose AreaFxProfileId");
    AssertTrue(eventType.GetProperty("SuppressActorCastFx") != null, "BattleEvent should expose SuppressActorCastFx");
    AssertTrue(eventType.GetProperty("HoldCastAnimationDuringAction") != null, "BattleEvent should expose HoldCastAnimationDuringAction");

    string presentationSource = ReadBattleRuntimeLiveObservationSource();
    AssertTrue(
        !presentationSource.Contains("HeroSkillCommandIds", StringComparison.Ordinal) &&
        !presentationSource.Contains("ThunderTagThrowSkillId", StringComparison.Ordinal) &&
        !presentationSource.Contains("ThunderSpiralBreakSkillId", StringComparison.Ordinal),
        "skill presentation must not select behavior by concrete thunder skill ids");
}

internal static void SkillUsedEventsExposePresentationProfileFields()
{
    string runtimeSource = string.Join("\n", Directory
        .GetFiles(Path.Combine("src", "Runtime", "Battle"), "*.cs", SearchOption.AllDirectories)
        .OrderBy(path => path, StringComparer.Ordinal)
        .Select(File.ReadAllText));

    AssertTrue(
        runtimeSource.Contains("PresentationProfileId", StringComparison.Ordinal),
        "Runtime should copy skill presentation profile facts into skill events");
    AssertTrue(
        runtimeSource.Contains("SuppressActorCastFx", StringComparison.Ordinal),
        "Runtime skill events should carry actor cast FX suppression");
    AssertTrue(
        runtimeSource.Contains("HoldCastAnimationDuringAction", StringComparison.Ordinal),
        "Runtime skill events should carry hold-animation presentation traits");
}

internal static void MarkAndChannelPresentationObserversAvoidConcreteSkillIds()
{
    string presentationSource = ReadBattleRuntimeLiveObservationSource();

    AssertTrue(
        presentationSource.Contains("skill_mark_projectile", StringComparison.Ordinal) ||
        presentationSource.Contains("PresentationProfileId", StringComparison.Ordinal),
        "mark projectile presentation should be selected by profile facts");
    AssertTrue(
        presentationSource.Contains("skill_channeled_area", StringComparison.Ordinal) ||
        presentationSource.Contains("PresentationProfileId", StringComparison.Ordinal),
        "channeled area presentation should be selected by profile facts");
    AssertTrue(
        !presentationSource.Contains("SourceDefinitionId == ", StringComparison.Ordinal) &&
        !presentationSource.Contains("SourceDefinitionId !=", StringComparison.Ordinal),
        "presentation must not branch on SourceDefinitionId comparisons for skill visuals");
}
}
