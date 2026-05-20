using Rpg.Runtime.Battle.Events;

internal static partial class BattleHitFeedbackRegressionCases
{
internal static void BattleRuntimeLiveObservationUsesTickClock()
{
    System.Reflection.PropertyInfo? runtimeTick = typeof(BattleEvent).GetProperty("RuntimeTick");
    AssertTrue(runtimeTick != null, "battle events should carry a structured runtime tick for playback pacing");
    System.Reflection.PropertyInfo? runtimeTime = typeof(BattleEvent).GetProperty("RuntimeTimeSeconds");
    AssertTrue(runtimeTime != null, "battle events should carry runtime seconds for actor-local action pacing");

    string runtime = string.Join("\n", Directory
        .GetFiles(Path.Combine("src", "Presentation", "World", "Sites"), "WorldSiteRoot.BattleRuntime*.cs")
        .OrderBy(path => path, StringComparer.Ordinal)
        .Select(File.ReadAllText));
    AssertTrue(
        runtime.Contains("AdvanceBattleGroupRuntimeOnLiveClockAsync", StringComparison.Ordinal) &&
        runtime.Contains("advance.NextAdvanceDelaySeconds", StringComparison.Ordinal) &&
        runtime.Contains("WaitSiteBattlePresentationSeconds(waitSeconds)", StringComparison.Ordinal),
        "presentation-backed runtime should be paced by the live action timeline");
}

internal static void BattleRuntimePlaybackDoesNotGloballyGateMovementOnAttackAnimation()
{
    string runtime = string.Join("\n", Directory
        .GetFiles(Path.Combine("src", "Presentation", "World", "Sites"), "WorldSiteRoot.BattleRuntime*.cs")
        .OrderBy(path => path, StringComparer.Ordinal)
        .Select(File.ReadAllText));
    AssertTrue(
        runtime.Contains("AdvanceBattleGroupRuntimeOnLiveClockAsync", StringComparison.Ordinal) &&
        runtime.Contains("ObserveRuntimeEventsOnPresentationAsync", StringComparison.Ordinal) &&
        runtime.Contains("TrackActorAction", StringComparison.Ordinal),
        "world site runtime should advance the simulation clock directly and let presentation observe events asynchronously");
    AssertTrue(
        runtime.Contains("ResolveRuntimePlaybackTickSeconds", StringComparison.Ordinal) &&
        runtime.Contains("controller.AdvanceNextTick()", StringComparison.Ordinal) &&
        !runtime.Contains("PlayRuntimeActorLaneAsync", StringComparison.Ordinal) &&
        !runtime.Contains("BuildTickBatches", StringComparison.Ordinal) &&
        !runtime.Contains("PlayRuntimeEventBatchAsync", StringComparison.Ordinal),
        "runtime presentation should pace simulation ticks directly instead of rebuilding a playback event pipeline");
}

internal static void BattleRuntimePlaybackPlansMoveIdleOnlyAtSequenceBoundary()
{
    string runtime = string.Join("\n", Directory
        .GetFiles(Path.Combine("src", "Presentation", "World", "Sites"), "WorldSiteRoot.BattleRuntime*.cs")
        .OrderBy(path => path, StringComparer.Ordinal)
        .Select(File.ReadAllText));
    string playback = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRuntimePlayback.cs"));

    AssertTrue(
        runtime.Contains("ObserveRuntimeMovementEvent(runtimeEvent, presentationState.EntitiesByRuntimeActor)", StringComparison.Ordinal) &&
        playback.Contains("returnToIdleOnComplete: false", StringComparison.Ordinal),
        "live movement observation should not close the move loop after every single runtime step");
}

internal static void BattleRuntimePlaybackDelaysDamageUntilSameTickTargetMovementSettles()
{
    string runtime = string.Join("\n", Directory
        .GetFiles(Path.Combine("src", "Presentation", "World", "Sites"), "WorldSiteRoot.BattleRuntime*.cs")
        .OrderBy(path => path, StringComparer.Ordinal)
        .Select(File.ReadAllText));
    AssertTrue(
        runtime.Contains("sameTickMovingActors", StringComparison.Ordinal) &&
        runtime.Contains("sameTickMovementDelaySeconds", StringComparison.Ordinal) &&
        runtime.Contains("ObserveRuntimeDamageEventAfterSameTickMovementAsync", StringComparison.Ordinal),
        "live presentation should delay only dependent same-tick damage tasks instead of globally gating simulation ticks");
}

internal static void BattleRuntimeLiveObservationWaitsForSameTickMovementBeforeDependentAttack()
{
    string runtime = string.Join("\n", Directory
        .GetFiles(Path.Combine("src", "Presentation", "World", "Sites"), "WorldSiteRoot.BattleRuntime*.cs")
        .OrderBy(path => path, StringComparer.Ordinal)
        .Select(File.ReadAllText));
    AssertTrue(
        runtime.Contains("sameTickMovingActors", StringComparison.Ordinal) &&
        !runtime.Contains("WaitForRuntimePlaybackDependenciesAsync", StringComparison.Ordinal) &&
        !runtime.Contains("BattleRuntimeActorLaneProgress", StringComparison.Ordinal),
        "live presentation should rely on the simulation clock and same-tick movement delay, not old target-lane playback dependencies");
}

internal static void RuntimePlaybackDamageWaitsForTargetMovementButNotTargetAttackBacklog()
{
    string runtime = string.Join("\n", Directory
        .GetFiles(Path.Combine("src", "Presentation", "World", "Sites"), "WorldSiteRoot.BattleRuntime*.cs")
        .OrderBy(path => path, StringComparer.Ordinal)
        .Select(File.ReadAllText));
    AssertTrue(
        runtime.Contains("runtimeEvent?.Kind == BattleEventKind.MovementCompleted", StringComparison.Ordinal) &&
        runtime.Contains("ResolveSameTickMovementDelaySeconds", StringComparison.Ordinal) &&
        runtime.Contains("targetId", StringComparison.Ordinal),
        "live damage delay should depend on same-tick movement, not old target attack backlog");
}

internal static void RuntimePlaybackMovementPathUsesFootprintCenterResolver()
{
    string unitRoot = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "BattleUnitRoot.cs"));
    string worldSiteRoot = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.cs"));

    AssertTrue(
        unitRoot.Contains("public delegate bool TryResolveFootprintGlobalPosition", StringComparison.Ordinal) &&
        unitRoot.Contains("_tryResolveFootprintGlobalPosition", StringComparison.Ordinal),
        "battle unit root should expose a footprint center resolver for runtime movement playback");
    AssertTrue(
        unitRoot.Contains("gridOccupant.FootprintWidth", StringComparison.Ordinal) &&
        unitRoot.Contains("gridOccupant.FootprintHeight", StringComparison.Ordinal) &&
        unitRoot.Contains("_tryResolveFootprintGlobalPosition", StringComparison.Ordinal),
        "movement path point resolving should use moving footprint size with the footprint center resolver");
    AssertTrue(
        !unitRoot.Contains("return _tryResolveCellGlobalPosition?.Invoke(surfacePosition.Position, out globalPosition) == true;", StringComparison.Ordinal),
        "movement path resolving should not fallback to anchor cell resolver when footprint resolver fails");
    AssertTrue(
        worldSiteRoot.Contains("_unitRoot.Initialize(TryGetCellGlobalPosition, TryGetFootprintCenterGlobalPosition, ApplyEntityRenderSort)", StringComparison.Ordinal),
        "world site root should wire runtime movement playback to the shared footprint center resolver");
}

internal static void BattleRuntimePresentationStartsIncrementalRuntimeBeforeSettlement()
{
    string adapter = File.ReadAllText(Path.Combine("src", "Application", "World", "WorldSiteBattleGroupRuntimeAdapter.cs"));
    string runtime = string.Join("\n", Directory
        .GetFiles(Path.Combine("src", "Presentation", "World", "Sites"), "WorldSiteRoot.BattleRuntime*.cs")
        .OrderBy(path => path, StringComparer.Ordinal)
        .Select(File.ReadAllText));

    AssertTrue(
        adapter.Contains("TryStartActiveBattle", StringComparison.Ordinal) &&
        adapter.Contains("CompleteResolvedBattle", StringComparison.Ordinal) &&
        adapter.Contains("BattleRuntimeSessionController", StringComparison.Ordinal),
        "world-site runtime adapter should expose a live runtime start and a separate completion boundary");
    AssertTrue(
        runtime.Contains("TryStartActiveBattle", StringComparison.Ordinal) &&
        runtime.Contains("AdvanceBattleGroupRuntimeOnLiveClockAsync", StringComparison.Ordinal) &&
        runtime.Contains("CompleteResolvedBattle", StringComparison.Ordinal),
        "world-site presentation should advance runtime on a live simulation clock before settlement");
    AssertTrue(
        !runtime.Contains("TryResolveActiveBattle", StringComparison.Ordinal),
        "presentation-backed battle should not request an already resolved full battle stream");
}

internal static void BattleRuntimeLiveClockDoesNotUseLookaheadBatches()
{
    string runtime = string.Join("\n", Directory
        .GetFiles(Path.Combine("src", "Presentation", "World", "Sites"), "WorldSiteRoot.BattleRuntime*.cs")
        .OrderBy(path => path, StringComparer.Ordinal)
        .Select(File.ReadAllText));

    AssertTrue(
        runtime.Contains("AdvanceBattleGroupRuntimeOnLiveClockAsync", StringComparison.Ordinal) &&
        runtime.Contains("controller.AdvanceNextTick()", StringComparison.Ordinal) &&
        runtime.Contains("advance.NextAdvanceDelaySeconds", StringComparison.Ordinal),
        "battle runtime should advance one simulation slice per live action time boundary");
    AssertTrue(
        !runtime.Contains("MaxRuntimePlaybackLookaheadTicks", StringComparison.Ordinal) &&
        !runtime.Contains("BuildRuntimePlaybackEventBatch", StringComparison.Ordinal) &&
        !runtime.Contains("BattleRuntimePlaybackSchedulingState", StringComparison.Ordinal) &&
        !runtime.Contains("ScheduleBattleGroupRuntimeEventsAsync", StringComparison.Ordinal),
        "live RTS runtime must not use playback lookahead batches or scheduling batches as the simulation driver");
}

internal static void BattleRuntimeLiveObservationDoesNotAwaitMovementOrAttackDurations()
{
    string runtime = string.Join("\n", Directory
        .GetFiles(Path.Combine("src", "Presentation", "World", "Sites"), "WorldSiteRoot.BattleRuntime*.cs")
        .OrderBy(path => path, StringComparer.Ordinal)
        .Select(File.ReadAllText));
    string playback = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRuntimePlayback.cs"));

    AssertTrue(
        runtime.Contains("ObserveRuntimeEventsOnPresentationAsync", StringComparison.Ordinal) &&
        runtime.Contains("_ = ObserveRuntimeEventsOnPresentationAsync", StringComparison.Ordinal),
        "runtime events should be observed by presentation without awaiting the whole visual action lane");
    AssertTrue(
        playback.Contains("ObserveRuntimeMovementEvent", StringComparison.Ordinal) &&
        playback.Contains("ObserveRuntimeDamageEventAsync", StringComparison.Ordinal) &&
        !playback.Contains("await WaitSiteBattlePresentationSeconds(movementSeconds)", StringComparison.Ordinal) &&
        !playback.Contains("await WaitSiteBattlePresentationSeconds(attackPresentationSeconds)", StringComparison.Ordinal),
        "movement and attack visuals must not block unrelated future simulation ticks");
}

}
