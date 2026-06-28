internal static partial class WorldSiteDeploymentCacheRegressionCases
{
internal static void WorldSiteRootDelegatesBattleRuntimeLivePresentationObservation()
{
    string siteRootDir = Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites");
    string rootSource = ReadWorldSiteRootSource();
    string incrementalSource = File.ReadAllText(Path.Combine(siteRootDir, "WorldSiteRoot.BattleRuntimeIncremental.cs"));
    string observerPath = Path.Combine(siteRootDir, "BattleRuntimeLivePresentationObserver.cs");
    AssertTrue(File.Exists(observerPath), "live runtime event observation should live in BattleRuntimeLivePresentationObserver");

    string observerSource = File.ReadAllText(observerPath);
    AssertTrue(
        observerSource.Contains("internal sealed class BattleRuntimeLivePresentationObserver", StringComparison.Ordinal),
        "runtime live presentation observer should be a focused internal Presentation collaborator");
    AssertTrue(
        rootSource.Contains("private readonly BattleRuntimeLivePresentationObserver _battleRuntimeLivePresentationObserver", StringComparison.Ordinal),
        "WorldSiteRoot should own a focused runtime live presentation observer");

    string advanceBody = ExtractMethodBody(incrementalSource, "private async Task AdvanceBattleGroupRuntimeOnLiveClockAsync(");
    AssertTrue(
        advanceBody.Contains("controller.AdvanceFixedTick(tickSeconds)", StringComparison.Ordinal) &&
        advanceBody.Contains("_battleRuntimeLivePresentationObserver.ObserveAsync(", StringComparison.Ordinal),
        "WorldSiteRoot should keep Runtime clock ownership while delegating emitted event observation");
    AssertTrue(
        advanceBody.Contains("new(_battleRuntimeLivePresentationObserver.BuildRuntimePlaybackEntityMap())", StringComparison.Ordinal),
        "WorldSiteRoot should create live presentation state from the observer-built entity map");

    foreach (string rootMethod in new[]
    {
        "private Task ObserveRuntimeEventsOnPresentationAsync(",
        "private double ObserveRuntimeMovementEvent(",
        "private async Task<double> ObserveRuntimeSkillUsedEventCoreAsync(",
        "private async Task<double> PlayRuntimeDamageFeedbackEventAsync(",
        "private async Task ApplyRuntimeDamageEventAsync(",
        "private Dictionary<string, BattleEntity> BuildRuntimePlaybackEntityMap("
    })
    {
        AssertTrue(!rootSource.Contains(rootMethod, StringComparison.Ordinal), $"WorldSiteRoot should not own runtime event observation method {rootMethod}");
    }

    foreach (string required in new[]
    {
        "BattleEventKind.ThunderMarkTeleported",
        "presentationState.ObserveActorTeleportNow",
        "BattleEventKind.MovementStarted",
        "restartMoveAnimation: false",
        "returnToIdleOnComplete: true",
        "TrackTargetDamage",
        "previousTargetDamageTail"
    })
    {
        AssertTrue(observerSource.Contains(required, StringComparison.Ordinal), $"runtime live presentation observer should retain event behavior fragment={required}");
    }

    foreach (string forbidden in new[]
    {
        "BattleRuntimeSessionController",
        "AdvanceFixedTick",
        "CompleteResolvedBattle",
        "ApplyBattleResultToWorld",
        "StrategicWorldRuntime"
    })
    {
        AssertTrue(!observerSource.Contains(forbidden, StringComparison.Ordinal), $"runtime live presentation observer must not own Runtime lifecycle or settlement fragment={forbidden}");
    }
}

internal static void WorldSiteRootDelegatesBattlePreparationHudBinding()
{
    string siteRootDir = Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites");
    string rootSource = ReadWorldSiteRootSource();
    string hudSource = File.ReadAllText(Path.Combine(siteRootDir, "WorldSiteRoot.BattlePreparationHud.cs"));
    string refreshSource = File.ReadAllText(Path.Combine(siteRootDir, "BattlePreparationRefresh.cs"));
    string binderPath = Path.Combine(siteRootDir, "BattlePreparationHudBinder.cs");
    AssertTrue(File.Exists(binderPath), "battle-preparation roster and plan-control binding should live in BattlePreparationHudBinder");

    string binderSource = File.ReadAllText(binderPath);
    AssertTrue(
        binderSource.Contains("internal sealed class BattlePreparationHudBinder", StringComparison.Ordinal),
        "battle-preparation HUD binder should be a focused internal Presentation collaborator");
    AssertTrue(
        rootSource.Contains("private readonly BattlePreparationHudBinder _battlePreparationHudBinder", StringComparison.Ordinal),
        "WorldSiteRoot should own a focused battle-preparation HUD binder");

    string rosterBody = ExtractMethodBody(hudSource, "private void BindBattlePreparationCompanyRoster()");
    string controlsBody = ExtractMethodBody(hudSource, "private void BindBattlePreparationCompactPlanControls()");
    AssertTrue(
        rosterBody.Contains("_battlePreparationHudBinder.BindCompanyRoster(", StringComparison.Ordinal),
        "battle-preparation company roster binding should delegate to the HUD binder");
    AssertTrue(
        controlsBody.Contains("_battlePreparationHudBinder.BindCompactPlanControls(", StringComparison.Ordinal),
        "battle-preparation compact plan controls should delegate to the HUD binder");
    AssertTrue(
        refreshSource.Contains("BindBattlePreparationCompanyRoster()", StringComparison.Ordinal) &&
        refreshSource.Contains("BindBattlePreparationCompactPlanControls()", StringComparison.Ordinal) &&
        !ExtractMethodBody(refreshSource, "private void RefreshBattlePreparationPlanUi(").Contains("RefreshBattlePreparationMapEntities", StringComparison.Ordinal),
        "plan-only refresh should keep using lightweight root routing while binding details live in the HUD binder");

    foreach (string rootMethod in new[]
    {
        "private void BindBattlePreparationRuleButton(",
        "private BattlePreparationCompanyPlanStatus ResolveBattlePreparationCompanyPlanStatus("
    })
    {
        AssertTrue(!rootSource.Contains(rootMethod, StringComparison.Ordinal), $"WorldSiteRoot should not own battle-preparation HUD binder method {rootMethod}");
    }

    foreach (string required in new[]
    {
        "GameUiSceneFactory.CreateBattlePreparationRosterRow",
        "row.Selected +=",
        "row.DragStarted +=",
        "BattlePreparationPlanUiModel.ResolveCompanyPlanStatus",
        "BattlePreparationPlanUiModel.ResolveObjectiveText",
        "CanLaunchPreparedBattle"
    })
    {
        AssertTrue(binderSource.Contains(required, StringComparison.Ordinal), $"battle-preparation HUD binder should retain binding behavior fragment={required}");
    }

    foreach (string forbidden in new[]
    {
        "ActivateBattleRuntime",
        "ExcludeUndeployedBattlePreparationReserveGroups",
        "SyncBattlePreparationRequestPlacements",
        "WorldActionResolver",
        "StrategicWorldRuntime"
    })
    {
        AssertTrue(!binderSource.Contains(forbidden, StringComparison.Ordinal), $"battle-preparation HUD binder must not own launch, settlement, or strategic authority fragment={forbidden}");
    }
}
}
