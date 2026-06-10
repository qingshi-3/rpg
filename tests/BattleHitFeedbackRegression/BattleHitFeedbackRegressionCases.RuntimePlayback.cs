using Rpg.Runtime.Battle.Events;
using Rpg.Presentation.Battle.Flow;

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
        runtime.Contains("controller.AdvanceFixedTick(tickSeconds)", StringComparison.Ordinal) &&
        runtime.Contains("WaitSiteBattlePresentationSeconds(tickSeconds)", StringComparison.Ordinal) &&
        !runtime.Contains("WaitSiteBattlePresentationSeconds(waitSeconds)", StringComparison.Ordinal),
        "presentation-backed runtime should be paced by a fixed RTS simulation tick");
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
        runtime.Contains("controller.AdvanceFixedTick(tickSeconds)", StringComparison.Ordinal) &&
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
        runtime.Contains("TrackActorMovement", StringComparison.Ordinal) &&
        playback.Contains("returnToIdleOnComplete: false", StringComparison.Ordinal),
        "live movement observation should not close the move loop after every single runtime step");
}

internal static void BattleRuntimeLiveMovementUsesActorMotionLane()
{
    string runtime = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRuntimeIncremental.cs"));
    string unitRoot = ReadBattleUnitRootSource();

    AssertTrue(
        runtime.Contains("TrackActorMovement", StringComparison.Ordinal) &&
        runtime.Contains("ObserveRuntimeMovementEvent(", StringComparison.Ordinal) &&
        runtime.Contains("WaitSiteBattlePresentationSeconds", StringComparison.Ordinal),
        "live movement should enqueue same-actor visual steps immediately while extending the actor motion tail");
    AssertTrue(
        runtime.Contains("_ = ObserveRuntimeEventsOnPresentationAsync", StringComparison.Ordinal),
        "same-actor movement serialization must not globally await presentation before advancing the runtime clock");
    AssertTrue(
        unitRoot.Contains("MovementLane", StringComparison.Ordinal) &&
        unitRoot.Contains("_movementLanes", StringComparison.Ordinal) &&
        unitRoot.Contains("public override void _Process(double delta)", StringComparison.Ordinal),
        "battle unit root should drive live movement through actor-local motion lanes advanced every frame");
    AssertTrue(
        unitRoot.Contains("VisualMoveSmoothingSeconds", StringComparison.Ordinal) &&
        unitRoot.Contains("ResolveVisualMoveStepDurationSeconds", StringComparison.Ordinal),
        "actor motion lanes should expose movement timing through one duration resolver so Runtime and visual clocks stay aligned");
    AssertTrue(
        !unitRoot.Contains("TweenMethod(Callable.From<float>(progress =>", StringComparison.Ordinal) &&
        !unitRoot.Contains("RecordBattleMovementTweenCreated", StringComparison.Ordinal),
        "live movement should not create one Godot tween per movement step");
}

internal static void BattleRuntimeLiveMovementQueuesBeforeActorVisualTailWaits()
{
    string runtime = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRuntimeIncremental.cs"));

    AssertTrue(
        runtime.Contains("TrackActorMovement", StringComparison.Ordinal) &&
        runtime.Contains("ObserveRuntimeMovementEvent(", StringComparison.Ordinal) &&
        runtime.Contains("WaitSiteBattlePresentationSeconds", StringComparison.Ordinal),
        "live movement events should enqueue into the motion lane immediately while still extending the actor visual tail");
    AssertTrue(
        !runtime.Contains("() => ObserveRuntimeMovementEventAsync", StringComparison.Ordinal),
        "same-actor visual tail waiting must not delay enqueueing the next runtime movement event into the actor lane");
}

internal static void BattleRuntimeLiveMovementBuffersCommittedSegmentsWithoutRestartingMove()
{
    string unitRoot = ReadBattleUnitRootSource();

    AssertTrue(
        unitRoot.Contains("ResolveVisualMoveBufferSeconds", StringComparison.Ordinal) &&
        unitRoot.Contains("new MovementLane(entity.GlobalPosition, ResolveSurfaceAt(surfacePath, 0), ResolveVisualMoveBufferSeconds())", StringComparison.Ordinal),
        "live movement lanes should start with a short committed-event buffer instead of requiring prediction");
    AssertTrue(
        unitRoot.Contains("StartMoveAnimationForLane(entity, restartMoveAnimation);", StringComparison.Ordinal) &&
        !unitRoot.Contains("animation?.PlayMove(restartMoveAnimation);", StringComparison.Ordinal),
        "move animation should start only when entering a visual movement lane, not for every committed grid step");
    AssertTrue(
        unitRoot.Contains("lane.BeginContinuationHold(ResolveVisualMoveBufferSeconds());", StringComparison.Ordinal) &&
        unitRoot.Contains("lane.CancelContinuationHold();", StringComparison.Ordinal),
        "movement lanes should stay alive briefly between confirmed segments so visual movement does not stop at every cell");
    AssertTrue(
        File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "BattleRuntimeLivePresentationState.cs"))
            .Contains("_actorMovementTails.TryGetValue(actorId, out Task previousMovementTask);", StringComparison.Ordinal),
        "same-actor movement completion should serialize through the previous movement tail without delaying the immediate enqueue");
}

internal static void BattleRuntimeMovementQueuesPerceptionOverlayRefresh()
{
    string playback = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRuntimePlayback.cs"));
    string overlay = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.BattlePerceptionOverlay.cs"));

    AssertTrue(
        playback.Contains("QueueBattlePerceptionOverlayRefresh();", StringComparison.Ordinal) &&
        !playback.Contains("        RefreshBattlePerceptionOverlay();\n        return _unitRoot.ResolveVisualMoveStepDurationSeconds", StringComparison.Ordinal),
        "movement observation should queue perception overlay refresh instead of rebuilding it once per movement event");
    AssertTrue(
        overlay.Contains("_battlePerceptionOverlayRefreshQueued", StringComparison.Ordinal) &&
        overlay.Contains("Callable.From(FlushBattlePerceptionOverlayRefresh).CallDeferred();", StringComparison.Ordinal),
        "perception overlay refresh should be coalesced on the presentation frame");
}

internal static void BattleRuntimeLiveObservationConsumesSkillUsedAsCastCue()
{
    string incremental = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRuntimeIncremental.cs"));
    string playback = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRuntimePlayback.cs"));
    string unitRoot = ReadBattleUnitRootSource();

    AssertTrue(
        incremental.Contains("BattleEventKind.SkillUsed", StringComparison.Ordinal) &&
        incremental.Contains("ObserveRuntimeSkillUsedEventAsync", StringComparison.Ordinal) &&
        incremental.Contains("TrackActorAction(", StringComparison.Ordinal),
        "live runtime observation should consume SkillUsed as the caster-side cast cue on the actor action tail");
    AssertTrue(
        playback.Contains("ObserveRuntimeSkillUsedEventAsync", StringComparison.Ordinal) &&
        unitRoot.Contains("public double PlaySkillCastPresentation(", StringComparison.Ordinal),
        "runtime SkillUsed playback should call a dedicated caster-side skill presentation entry point");
}

internal static void RuntimeSkillDamageDoesNotReplayCasterCast()
{
    string playback = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRuntimePlayback.cs"));

    AssertTrue(
        playback.Contains("PlayRuntimeDamageFeedback(", StringComparison.Ordinal) &&
        !playback.Contains("BattleActionResult.AbilitySucceeded", StringComparison.Ordinal),
        "runtime skill damage should keep target impact and damage feedback without replaying the caster skill-cast animation");
}

internal static void BattleRuntimeVisualMovementKeepsRuntimeActionDuration()
{
    string unitRoot = ReadBattleUnitRootSource();
    string playback = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRuntimePlayback.cs"));

    AssertTrue(
        unitRoot.Contains("public double ResolveVisualMoveStepDurationSeconds", StringComparison.Ordinal) &&
        unitRoot.Contains("return System.Math.Max(0.01, baseSeconds);", StringComparison.Ordinal),
        "each visual movement segment should still use the Runtime action duration instead of predicting future cells");
    AssertTrue(
        playback.Contains("double visualMoveSeconds = _unitRoot.MoveEntityTo", StringComparison.Ordinal) &&
        playback.Contains("return visualMoveSeconds;", StringComparison.Ordinal),
        "movement observer should report the actor-local motion lane duration, including only committed-event buffer time");
}

internal static void BattleRuntimeMovementPlaybackDoesNotUseLookaheadCorrectionPath()
{
    string playback = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRuntimePlayback.cs"));
    string unitRoot = ReadBattleUnitRootSource();

    AssertTrue(
        !playback.Contains("BuildRuntimeMovementVisualPath", StringComparison.Ordinal) &&
        !playback.Contains("MovementPreviewPath", StringComparison.Ordinal) &&
        !playback.Contains("visualPath:", StringComparison.Ordinal),
        "runtime playback should not consume future lookahead correction paths that can desync visuals from committed runtime cells");
    AssertTrue(
        unitRoot.Contains("GridSurfacePosition targetPosition = path", StringComparison.Ordinal) &&
        unitRoot.Contains("TryBuildMovementGlobalPath(\n                path,", StringComparison.Ordinal),
        "battle unit movement should sample the same committed path that updates grid occupancy");
}

internal static void BattlePresentationTimelineSeparatesMovementCompletionFromActionBacklog()
{
    BattlePresentationTimeline timeline = new();

    BattlePresentationActionSpan targetAttack = timeline.ScheduleAttack(
        actorId: "target",
        targetId: "other",
        observedAtSeconds: 0,
        actionDurationSeconds: 1.2,
        impactDelaySeconds: 0.66);
    BattlePresentationActionSpan targetMove = timeline.ScheduleMovement(
        actorId: "target",
        observedAtSeconds: 0.1,
        actionDurationSeconds: 0.27);
    BattlePresentationActionSpan incoming = timeline.ScheduleAttack(
        actorId: "attacker",
        targetId: "target",
        observedAtSeconds: 0.2,
        actionDurationSeconds: 1.2,
        impactDelaySeconds: 0.66);

    AssertFloatEqual(0f, (float)targetAttack.StartSeconds, 0.0001f, "target attack starts at observed time");
    AssertFloatEqual(0.1f, (float)targetMove.StartSeconds, 0.0001f, "movement visual starts when the movement event is observed");
    AssertFloatEqual(0.37f, (float)targetMove.EndSeconds, 0.0001f, "movement completion should not be delayed behind target attack backlog");
    AssertFloatEqual(0.37f, (float)incoming.StartSeconds, 0.0001f, "incoming hit presentation should wait target movement completion");
}

internal static void BattlePresentationTimelineWaitsTargetMovementButNotTargetAttackBacklog()
{
    BattlePresentationTimeline timeline = new();

    timeline.ScheduleAttack(
        actorId: "target",
        targetId: "other",
        observedAtSeconds: 0,
        actionDurationSeconds: 1.2,
        impactDelaySeconds: 0.66);
    BattlePresentationActionSpan incomingWithoutMovement = timeline.ScheduleAttack(
        actorId: "attacker",
        targetId: "target",
        observedAtSeconds: 0.2,
        actionDurationSeconds: 1.2,
        impactDelaySeconds: 0.66);

    BattlePresentationTimeline movementTimeline = new();
    movementTimeline.ScheduleMovement(
        actorId: "target",
        observedAtSeconds: 0,
        actionDurationSeconds: 0.27);
    BattlePresentationActionSpan incomingWithMovement = movementTimeline.ScheduleAttack(
        actorId: "attacker",
        targetId: "target",
        observedAtSeconds: 0.2,
        actionDurationSeconds: 1.2,
        impactDelaySeconds: 0.66);

    AssertFloatEqual(0.2f, (float)incomingWithoutMovement.StartSeconds, 0.0001f, "target attack backlog must not delay incoming damage presentation");
    AssertFloatEqual(0.27f, (float)incomingWithMovement.StartSeconds, 0.0001f, "target movement completion should delay incoming damage presentation");
}

internal static void BattleRuntimeRegistersGodotPerformanceMonitors()
{
    string runtime = string.Join("\n", Directory
        .GetFiles(Path.Combine("src", "Presentation", "World", "Sites"), "WorldSiteRoot*.cs")
        .OrderBy(path => path, StringComparer.Ordinal)
        .Select(File.ReadAllText));
    string adapter = File.ReadAllText(Path.Combine("src", "Application", "World", "WorldSiteBattleGroupRuntimeAdapter.cs"));
    string unitRoot = ReadBattleUnitRootSource();
    string monitorRegistry = File.ReadAllText(Path.Combine("src", "Presentation", "Debug", "BattlePerformanceMonitorRegistry.cs"));

    AssertTrue(
        runtime.Contains("BattlePerformanceMonitorRegistry.Register", StringComparison.Ordinal) &&
        runtime.Contains("BattlePerformanceMonitorRegistry.Unregister", StringComparison.Ordinal) &&
        runtime.Contains("_battlePerformanceCounters.Reset()", StringComparison.Ordinal),
        "world site root should register pure-code Godot performance monitors and reset battle counters at runtime start");
    AssertTrue(
        adapter.Contains("BattlePerformanceCounters performanceCounters", StringComparison.Ordinal) &&
        adapter.Contains("new(performanceCounters: performanceCounters)", StringComparison.Ordinal),
        "battle runtime adapter should pass monitor counters into the runtime session");
    AssertTrue(
        unitRoot.Contains("ActiveMovementTweenCount", StringComparison.Ordinal),
        "battle unit root should expose active movement tween count for the monitor");
    AssertTrue(
        monitorRegistry.Contains("Performance.AddCustomMonitor", StringComparison.Ordinal) &&
        monitorRegistry.Contains("Performance.GetMonitor", StringComparison.Ordinal) &&
        monitorRegistry.Contains("Battle/RuntimeAdvanceMsLast", StringComparison.Ordinal) &&
        monitorRegistry.Contains("Battle/PresentationObserveMsLast", StringComparison.Ordinal) &&
        monitorRegistry.Contains("Battle/RuntimeAdvanceTickAtMax", StringComparison.Ordinal) &&
        monitorRegistry.Contains("Battle/FlowFieldBuildMsLast", StringComparison.Ordinal) &&
        monitorRegistry.Contains("Battle/FlowFieldBuildMsMax", StringComparison.Ordinal) &&
        monitorRegistry.Contains("Battle/OpenAttackFlowFieldRequests", StringComparison.Ordinal) &&
        monitorRegistry.Contains("Battle/OpenAttackFlowFieldCacheHits", StringComparison.Ordinal) &&
        monitorRegistry.Contains("Battle/OpenAttackFlowFieldBuilds", StringComparison.Ordinal) &&
        monitorRegistry.Contains("Battle/CombatSlotScanMsLast", StringComparison.Ordinal) &&
        monitorRegistry.Contains("Battle/CombatSlotScanMsMax", StringComparison.Ordinal) &&
        monitorRegistry.Contains("Battle/TargetScoringMsLast", StringComparison.Ordinal) &&
        monitorRegistry.Contains("Battle/TargetScoringMsMax", StringComparison.Ordinal) &&
        monitorRegistry.Contains("Battle/MovementResolveMsLast", StringComparison.Ordinal) &&
        monitorRegistry.Contains("Battle/MovementResolveMsMax", StringComparison.Ordinal) &&
        monitorRegistry.Contains("Battle/MovementEventsLastAdvance", StringComparison.Ordinal) &&
        monitorRegistry.Contains("Battle/ReservationRejectedCount", StringComparison.Ordinal) &&
        monitorRegistry.Contains("Battle/ReservationRejectedLastAdvance", StringComparison.Ordinal) &&
        monitorRegistry.Contains("Battle/HoldDueReservationCount", StringComparison.Ordinal) &&
        monitorRegistry.Contains("Battle/ActorsReadyNoMoveLastAdvance", StringComparison.Ordinal) &&
        monitorRegistry.Contains("Battle/MovementEventGapMsMax", StringComparison.Ordinal) &&
        monitorRegistry.Contains("Battle/MovementTweensInterrupted", StringComparison.Ordinal) &&
        monitorRegistry.Contains("Battle/ActiveMovementTweens", StringComparison.Ordinal) &&
        monitorRegistry.Contains("Godot/FrameMs", StringComparison.Ordinal) &&
        monitorRegistry.Contains("Godot/DrawCalls", StringComparison.Ordinal) &&
        monitorRegistry.Contains("Godot/Nodes", StringComparison.Ordinal) &&
        monitorRegistry.Contains("Godot/StaticMemoryMiB", StringComparison.Ordinal),
        "battle monitor registry should expose battle, presentation, and Godot engine counters to Godot Debugger > Monitors");
}

internal static void BattleRuntimePlaybackDelaysDamageUntilSameTickTargetMovementSettles()
{
    string runtime = ReadBattleRuntimePresentationSource();
    AssertTrue(
        runtime.Contains("TrackActorDamage", StringComparison.Ordinal) &&
        runtime.Contains("_actorMovementTails", StringComparison.Ordinal) &&
        runtime.Contains("ObserveRuntimeDamageEventAsync", StringComparison.Ordinal),
        "live presentation should delay dependent damage through actor and target movement tails instead of a same-tick duration heuristic");
}

internal static void BattleRuntimeLiveObservationWaitsForSameTickMovementBeforeDependentAttack()
{
    string runtime = string.Join("\n", Directory
        .GetFiles(Path.Combine("src", "Presentation", "World", "Sites"), "WorldSiteRoot.BattleRuntime*.cs")
        .OrderBy(path => path, StringComparer.Ordinal)
        .Select(File.ReadAllText));
    AssertTrue(
        runtime.Contains("TrackActorDamage", StringComparison.Ordinal) &&
        runtime.Contains("TrackActorMovement", StringComparison.Ordinal) &&
        !runtime.Contains("WaitForRuntimePlaybackDependenciesAsync", StringComparison.Ordinal) &&
        !runtime.Contains("BattleRuntimeActorLaneProgress", StringComparison.Ordinal),
        "live presentation should rely on the simulation clock and explicit presentation tails, not old target-lane playback dependencies");
}

internal static void RuntimePlaybackDamageWaitsForTargetMovementButNotTargetAttackBacklog()
{
    string runtime = ReadBattleRuntimePresentationSource();
    AssertTrue(
        runtime.Contains("_actorMovementTails.TryGetValue(targetId", StringComparison.Ordinal) &&
        !runtime.Contains("ResolveSameTickMovementDelaySeconds", StringComparison.Ordinal) &&
        runtime.Contains("targetId", StringComparison.Ordinal),
        "live damage delay should depend on target movement tail, not target attack backlog");
}

internal static void RuntimePlaybackAppliesDamageSemanticsThroughTargetQueue()
{
    string state = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "BattleRuntimeLivePresentationState.cs"));
    string runtime = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRuntimeIncremental.cs"));
    string playback = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRuntimePlayback.cs"));

    AssertTrue(
        state.Contains("_targetDamageTails", StringComparison.Ordinal) &&
        state.Contains("TrackTargetDamage", StringComparison.Ordinal) &&
        state.Contains("RunAfterTargetDamageDependenciesAsync", StringComparison.Ordinal),
        "live damage semantics should have a target-ordered queue separate from actor visual action tails");
    AssertTrue(
        runtime.Contains("presentationState.TrackTargetDamage", StringComparison.Ordinal) &&
        runtime.Contains("ApplyRuntimeDamageEventAsync", StringComparison.Ordinal),
        "live runtime observation should schedule health/death application through the target damage queue");
    AssertTrue(
        playback.Contains("PlayRuntimeDamageFeedbackEventAsync", StringComparison.Ordinal) &&
        playback.Contains("ApplyRuntimeDamageEventAsync", StringComparison.Ordinal) &&
        !playback.Contains("await impactDamageTask;", StringComparison.Ordinal),
        "attack feedback may stay actor-queued, but health/death application should not wait for the actor visual tail to finish");
}

internal static void RuntimePlaybackTargetDamageQueueDoesNotSerializeImpactDelay()
{
    string state = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "BattleRuntimeLivePresentationState.cs"));
    string playback = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRuntimePlayback.cs"));

    AssertTrue(
        state.Contains("System.Func<Task, Task> createTask", StringComparison.Ordinal) &&
        state.Contains("createTask(previousTargetDamageTail)", StringComparison.Ordinal) &&
        !state.Contains("new[] { actorMovementTail, targetMovementTail, previousTargetDamageTail }", StringComparison.Ordinal),
        "target damage queue should pass the previous target damage tail into the task instead of waiting it before impact delay starts");

    int impactWaitIndex = playback.IndexOf("await WaitSiteBattlePresentationSeconds(clampedImpactDelaySeconds)", StringComparison.Ordinal);
    int previousTailIndex = playback.IndexOf("await previousTargetDamageTail", StringComparison.Ordinal);
    AssertTrue(
        impactWaitIndex >= 0 &&
        previousTailIndex > impactWaitIndex,
        "runtime damage application should let impact delay elapse before waiting for prior target damage application order");
}

internal static void RuntimePlaybackMovementPathUsesFootprintCenterResolver()
{
    string unitRoot = ReadBattleUnitRootSource();
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
        runtime.Contains("controller.AdvanceFixedTick(tickSeconds)", StringComparison.Ordinal) &&
        runtime.Contains("WaitSiteBattlePresentationSeconds(tickSeconds)", StringComparison.Ordinal) &&
        !runtime.Contains("advance.NextAdvanceDelaySeconds", StringComparison.Ordinal),
        "battle runtime should advance one fixed simulation slice per live RTS tick");
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
