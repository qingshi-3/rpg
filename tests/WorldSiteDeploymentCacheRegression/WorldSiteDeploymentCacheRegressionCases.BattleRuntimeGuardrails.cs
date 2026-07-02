internal static partial class WorldSiteDeploymentCacheRegressionCases
{
internal static void BattleRuntimePresentationFailureBlocksCompletion()
{
    string rootSource = ReadWorldSiteRootSource();
    string runtimeBody = ExtractMethodBody(rootSource, "private async Task PlayBattleGroupRuntimeAndApplyResultAsync(");
    int catchIndex = runtimeBody.IndexOf("catch (System.Exception ex)", StringComparison.Ordinal);
    int completeIndex = runtimeBody.IndexOf("_battleGroupRuntimeAdapter.CompleteResolvedBattle", catchIndex, StringComparison.Ordinal);
    int returnIndex = runtimeBody.IndexOf("return;", catchIndex, StringComparison.Ordinal);

    AssertTrue(catchIndex >= 0, "battle runtime playback should have an explicit presentation failure boundary");
    AssertTrue(completeIndex > catchIndex, "battle runtime completion call should stay after the playback try/catch");
    AssertTrue(
        returnIndex > catchIndex && returnIndex < completeIndex,
        "presentation/runtime playback failure must return before CompleteResolvedBattle so a failed handoff cannot write back");
    AssertTrue(
        runtimeBody.Contains("battle_group_runtime_presentation_failed", StringComparison.Ordinal),
        "presentation/runtime playback failure should record a named failure reason instead of silently continuing");
}

internal static void BattleRuntimeCompletionDoesNotForceAdvanceIncompleteLiveRuntime()
{
    string adapterSource = File.ReadAllText(Path.Combine(
        ProjectRoot(),
        "src",
        "Application",
        "World",
        "WorldSiteBattleGroupRuntimeAdapter.cs"));
    string legacyCompleteBody = ExtractMethodBody(
        adapterSource,
        "public WorldSiteBattleGroupRuntimeResolveResult CompleteResolvedBattle(WorldSiteBattleGroupRuntimeResolveResult started)");
    string strategicCompleteBody = ExtractMethodBody(
        adapterSource,
        "public WorldSiteBattleGroupRuntimeResolveResult CompleteResolvedBattle(\n        WorldSiteBattleGroupRuntimeResolveResult started,");

    foreach (string body in new[] { legacyCompleteBody, strategicCompleteBody })
    {
        AssertTrue(
            !body.Contains("AdvanceToCompletion()", StringComparison.Ordinal),
            "CompleteResolvedBattle must reject incomplete live-clock output instead of force-advancing it to settlement");
        AssertTrue(
            body.Contains("battle_group_runtime_incomplete", StringComparison.Ordinal),
            "CompleteResolvedBattle should expose a named incomplete-runtime rejection reason");
    }
}

internal static void StrategicActiveContextLaunchDoesNotUseProbeSnapshotAuthority()
{
    string adapterSource = File.ReadAllText(Path.Combine(
        ProjectRoot(),
        "src",
        "Application",
        "World",
        "WorldSiteBattleGroupRuntimeAdapter.cs"));
    string launchBody = ExtractMethodBody(
        adapterSource,
        "private bool TryBuildStrategicLaunchSnapshot(");

    AssertTrue(
        launchBody.Contains("_strategicLaunchSnapshotSync.Sync(activeContext, request)", StringComparison.Ordinal),
        "Strategic active-context launch should synchronize through the bridge-owned snapshot adapter");
    AssertTrue(
        !launchBody.Contains("_sessionService.PrepareSnapshot(request)", StringComparison.Ordinal) &&
        !launchBody.Contains("PrepareSnapshot(request)", StringComparison.Ordinal),
        "Strategic active-context launch must not use the legacy probe snapshot as Runtime authority");
}

internal static void BattleRuntimeLaunchRequiresStrategicActiveContext()
{
    string root = ProjectRoot();
    string adapterSource = File.ReadAllText(Path.Combine(
        root,
        "src",
        "Application",
        "World",
        "WorldSiteBattleGroupRuntimeAdapter.cs"));
    string rootSource = ReadWorldSiteRootSource();

    AssertTrue(
        !adapterSource.Contains("_sessionService.PrepareSnapshot(request)", StringComparison.Ordinal),
        "battle runtime adapter must not keep a no-active-context probe snapshot startup path");
    AssertTrue(
        !rootSource.Contains(": _battleGroupRuntimeAdapter.TryStartActiveBattle(out resolution)", StringComparison.Ordinal),
        "WorldSiteRoot battle runtime activation should fail without Strategic Active Context instead of falling back to legacy handoff");
}

internal static void LegacyAbilityResourcesCannotExecutePresentationDamage()
{
    string root = ProjectRoot();
    string effectReceiverSource = File.ReadAllText(Path.Combine(
        root,
        "src",
        "Runtime",
        "Battle",
        "BattleEffectReceiver.cs"));
    string executorSource = File.ReadAllText(Path.Combine(
        root,
        "src",
        "Runtime",
        "Battle",
        "Effects",
        "DamageSkillEffectExecutor.cs"));

    AssertTrue(
        !File.Exists(Path.Combine(root, "src", "Definitions", "Battle", "Abilities", "AbilityEffect.cs")) &&
        !File.Exists(Path.Combine(root, "src", "Definitions", "Battle", "Abilities", "DamageAbilityEffect.cs")) &&
        !File.Exists(Path.Combine(root, "src", "Presentation", "Battle", "Abilities", "BattleAbilityQueries.cs")),
        "legacy ability resource execution path should be deleted, not kept as a data-only fallback");
    AssertTrue(
        executorSource.Contains("DamageSkillEffectSnapshot", StringComparison.Ordinal) &&
        effectReceiverSource.Contains("ReceiveDamage", StringComparison.Ordinal),
        "runtime damage should execute through typed skill effect snapshots and runtime receivers");
}

internal static void PresentationHealthComponentExposesOnlyRuntimeDamageMirror()
{
    string healthSource = File.ReadAllText(Path.Combine(
        ProjectRoot(),
        "src",
        "Presentation",
        "Battle",
        "Entities",
        "HealthComponent.cs"));

    AssertTrue(
        !healthSource.Contains("public int ApplyDamage(", StringComparison.Ordinal),
        "Presentation health must not expose direct damage mutation; active battle damage is mirrored from Runtime facts");
    AssertTrue(
        healthSource.Contains("MirrorRuntimeDamage(", StringComparison.Ordinal),
        "Presentation health should keep only the Runtime damage mirror entry point for battle feedback");
}

internal static void WorldSiteBattleModifiersDoNotApplyPresentationDamage()
{
    string rootSource = ReadWorldSiteRootSource();

    AssertTrue(
        !rootSource.Contains("ApplyBattleModifiers", StringComparison.Ordinal),
        "world site root must not keep a legacy battle modifier damage entry point");
    AssertTrue(
        !rootSource.Contains("target.GetComponent<HealthComponent>()?.ApplyDamage", StringComparison.Ordinal),
        "battle modifiers must not apply damage through Presentation HealthComponent");
}

internal static void BattleRuntimeHeroSkillHudDoesNotFallbackToFirstSliceSkill()
{
    string root = ProjectRoot();
    string commandHudSource = File.ReadAllText(Path.Combine(
        root,
        "src",
        "Presentation",
        "World",
        "Sites",
        "WorldSiteRoot.BattleRuntimeCommandHud.cs"));
    string usageResolverSource = File.ReadAllText(Path.Combine(
        root,
        "src",
        "Presentation",
        "World",
        "Sites",
        "BattleRuntimeSkillUsageResolver.cs"));

    string pressBody = ExtractMethodBody(commandHudSource, "private void OnBattleRuntimeHeroSkillPressed()");
    string beginPressBody = ExtractMethodBody(commandHudSource, "private void BeginBattleRuntimeSkillPress(");
    string targetPickingBody = ExtractMethodBody(commandHudSource, "private void BeginBattleRuntimeHeroSkillTargetPicking(");
    string commandRequestBody = ExtractMethodBody(commandHudSource, "private CommandRequest BuildBattleRuntimeHeroSkillCommandRequest(");
    string resolveRangeBody = ExtractMethodBody(commandHudSource, "private int ResolveBattleRuntimeHeroSkillRange()");

    AssertTrue(
        !pressBody.Contains("HeroSkillCommandIds.FirstSliceHeroSkillId", StringComparison.Ordinal),
        "runtime hero skill press must not fabricate a default skill id when the selected skill snapshot is missing");
    AssertTrue(
        !beginPressBody.Contains("HeroSkillCommandIds.FirstSliceHeroSkillId", StringComparison.Ordinal),
        "runtime hero skill press handling must reject blank skill ids instead of normalizing them to first-slice skill");
    AssertTrue(
        !targetPickingBody.Contains("HeroSkillCommandIds.FirstSliceHeroSkillId", StringComparison.Ordinal),
        "runtime hero skill target picking must keep the chosen skill id explicit");
    AssertTrue(
        !commandRequestBody.Contains("HeroSkillCommandIds.FirstSliceHeroSkillId", StringComparison.Ordinal),
        "runtime hero skill command requests must not fabricate first-slice skill ids");
    AssertTrue(
        !resolveRangeBody.Contains("HeroSkillCommandIds.FirstSliceHeroSkillId", StringComparison.Ordinal),
        "runtime hero skill range preview must not use first-slice skill id as an implicit target-picking fallback");
    AssertTrue(
        !usageResolverSource.Contains("HeroSkillCommandIds.FirstSliceHeroSkillId", StringComparison.Ordinal),
        "runtime skill usage resolver must treat missing skill ids as unavailable instead of checking first-slice skill state");
}
}
