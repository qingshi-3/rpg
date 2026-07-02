using Rpg.Runtime.Battle.Events;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Flow;
using Godot;

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
    string liveObservation = ReadBattleRuntimeLiveObservationSource();
    AssertTrue(
        runtime.Contains("AdvanceBattleGroupRuntimeOnLiveClockAsync", StringComparison.Ordinal) &&
        runtime.Contains("_battleRuntimeLivePresentationObserver.ObserveAsync", StringComparison.Ordinal) &&
        liveObservation.Contains("TrackActorAction", StringComparison.Ordinal),
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
    string liveObservation = ReadBattleRuntimeLiveObservationSource();
    string unitRoot = ReadBattleUnitRootSource();

    AssertTrue(
        liveObservation.Contains("TrackActorMovement", StringComparison.Ordinal) &&
        liveObservation.Contains("returnToIdleOnComplete: true", StringComparison.Ordinal) &&
        !liveObservation.Contains("returnToIdleOnComplete: false", StringComparison.Ordinal),
        "live movement observation should let movement lanes close the move loop once no committed continuation arrives");
    AssertTrue(
        unitRoot.Contains("lane.BeginContinuationHold(ResolveMovementContinuationHoldSeconds(lane.LastCompletedSegmentDurationSeconds));", StringComparison.Ordinal) &&
        unitRoot.Contains("if (lane.ReturnToIdleOnComplete)", StringComparison.Ordinal) &&
        unitRoot.Contains("_pendingMovementIdleSeconds[entity] = ResolveMovementIdleGraceSeconds();", StringComparison.Ordinal),
        "movement lanes should still hold briefly for a continuation before returning a stopped unit to idle");
}

internal static void BattleRuntimeLiveMovementUsesActorMotionLane()
{
    string runtime = ReadBattleRuntimeLiveObservationSource();
    string unitRoot = ReadBattleUnitRootSource();

    AssertTrue(
        runtime.Contains("TrackActorMovement", StringComparison.Ordinal) &&
        runtime.Contains("ObserveRuntimeMovementEvent(", StringComparison.Ordinal) &&
        runtime.Contains("WaitSiteBattlePresentationSeconds", StringComparison.Ordinal),
        "live movement should enqueue same-actor visual steps immediately while extending the actor motion tail");
    AssertTrue(
        runtime.Contains("_ = _battleRuntimeLivePresentationObserver.ObserveAsync", StringComparison.Ordinal),
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
    string runtime = ReadBattleRuntimeLiveObservationSource();

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
        unitRoot.Contains("StartMoveAnimationForLane(entity, createdLane && restartMoveAnimation);", StringComparison.Ordinal) &&
        !unitRoot.Contains("animation?.PlayMove(restartMoveAnimation);", StringComparison.Ordinal),
        "movement should refresh the move cue on every committed segment without restarting an already-moving sprite");
    AssertTrue(
        unitRoot.Contains("A live lane can survive while an attack/idle cue takes over the sprite", StringComparison.Ordinal),
        "movement lane playback should document why appended segments must reassert move animation after attacks");
    AssertTrue(
        unitRoot.Contains("lane.BeginContinuationHold(ResolveMovementContinuationHoldSeconds(lane.LastCompletedSegmentDurationSeconds));", StringComparison.Ordinal) &&
        unitRoot.Contains("lane.CancelContinuationHold();", StringComparison.Ordinal),
        "movement lanes should stay alive briefly between confirmed segments so visual movement does not stop at every cell");
    AssertTrue(
        File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "BattleRuntimeLivePresentationState.cs"))
            .Contains("_actorMovementTails.TryGetValue(actorId, out Task previousMovementTask);", StringComparison.Ordinal),
        "same-actor movement completion should serialize through the previous movement tail without delaying the immediate enqueue");
}

internal static void BattleRuntimeLiveMovementContinuationHoldCoversSingleStepGap()
{
    string unitRoot = ReadBattleUnitRootSource();

    AssertTrue(
        unitRoot.Contains("ResolveMovementContinuationHoldSeconds", StringComparison.Ordinal) &&
        unitRoot.Contains("lane.LastCompletedSegmentDurationSeconds", StringComparison.Ordinal) &&
        unitRoot.Contains("lane.BeginContinuationHold(ResolveMovementContinuationHoldSeconds(lane.LastCompletedSegmentDurationSeconds));", StringComparison.Ordinal),
        "movement lanes should keep the continuation window tied to the last committed step duration so route rebuild jitter does not restart every cell");
    AssertTrue(
        unitRoot.Contains("System.Math.Clamp(segmentSeconds", StringComparison.Ordinal) &&
        unitRoot.Contains("ResolveVisualMoveBufferSeconds()", StringComparison.Ordinal) &&
        unitRoot.Contains("0.32", StringComparison.Ordinal),
        "movement continuation hold should cover about one fixed-clock step while staying bounded for real stop-to-idle cases");
}

internal static void BattleRuntimeMovementKeepsSurfacePositionAtVisualCommit()
{
    string unitRoot = ReadBattleUnitRootSource();
    string moveBody = ExtractMethodBlock(unitRoot, "public double MoveEntityTo(");
    string advanceBody = ExtractMethodBlock(unitRoot, "private bool AdvanceMovementLane(");
    string stopBody = ExtractMethodBlock(unitRoot, "private void StopEntityMovement(BattleEntity entity, bool snapToLogicalGrid)");

    AssertTrue(
        !moveBody.Contains("gridOccupant.SetSurfacePosition(targetPosition);", StringComparison.Ordinal),
        "movement presentation must not promote a queued target surface into the current surface before the visual lane reaches it");
    AssertTrue(
        advanceBody.Contains("CommitMovementSegmentSurface(entity, segment.ToSurface)", StringComparison.Ordinal) &&
        unitRoot.Contains("private void CommitMovementSegmentSurface(BattleEntity entity, GridSurfacePosition surfacePosition)", StringComparison.Ordinal),
        "movement lane completion should commit the Presentation surface only when the visible segment reaches its target");
    AssertTrue(
        stopBody.Contains("ResolveMovementInterruptionSurface(entity, gridOccupant)", StringComparison.Ordinal) &&
        !stopBody.Contains("TryResolveMovementGlobalPosition(gridOccupant, gridOccupant.SurfacePosition", StringComparison.Ordinal),
        "anchored action interruption should snap through a dedicated interruption surface resolver instead of reading a queued future target");
    AssertTrue(
        stopBody.IndexOf("ResolveMovementInterruptionSurface(entity, gridOccupant)", StringComparison.Ordinal) <
        stopBody.IndexOf("_movementLanes.Remove(entity)", StringComparison.Ordinal),
        "movement interruption must resolve the active lane's committed surface before removing the lane");
}

internal static void BattleRuntimeTeleportCancelsStaleQueuedMovementPresentation()
{
    string state = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "BattleRuntimeLivePresentationState.cs"));
    string liveObservation = ReadBattleRuntimeLiveObservationSource();
    string teleportObserver = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "BattleRuntimeTeleportPresentationObserver.cs"));
    string displacementBoundary = File.ReadAllText(Path.Combine("src", "Runtime", "Battle", "BattleDisplacementCommitBoundary.cs"));

    AssertTrue(
        state.Contains("_actorMovementGenerations", StringComparison.Ordinal) &&
        state.Contains("AdvanceActorTeleportGeneration", StringComparison.Ordinal) &&
        state.Contains("IsActorMovementGenerationCurrent", StringComparison.Ordinal) &&
        state.Contains("BattleRuntimeStaleMovementSkipped", StringComparison.Ordinal),
        "teleport should advance an actor-local movement generation so queued pre-teleport movement observers cannot mutate presentation after the snap");
    AssertTrue(
        state.Contains("AdvanceActorTeleportGeneration(actorId)", StringComparison.Ordinal) &&
        state.IndexOf("AdvanceActorTeleportGeneration(actorId)", StringComparison.Ordinal) <
        state.IndexOf("observeTeleport()", StringComparison.Ordinal),
        "ObserveActorTeleportNow should install the teleport movement barrier before observing the snap");
    string teleportLoopBody = ExtractForeachBlock(liveObservation, "item?.Kind == BattleEventKind.ThunderMarkTeleported");
    AssertTrue(
        teleportLoopBody.Contains("presentationState.ObserveActorTeleportNow", StringComparison.Ordinal) &&
        !teleportLoopBody.Contains("presentationState.TrackActorMovement", StringComparison.Ordinal),
        "teleport is a presentation hard barrier: it must snap immediately instead of waiting behind the actor movement tail");
    AssertTrue(
        state.Contains("ObserveActorTeleportNow", StringComparison.Ordinal) &&
        state.Contains("_actorMovementStartGates.Remove(actorId)", StringComparison.Ordinal) &&
        state.Contains("_actorActionTails.Remove(actorId)", StringComparison.Ordinal),
        "teleport should clear actor-local presentation gates and action/movement tails so old visual work cannot delay the snap");
    AssertTrue(
        state.Contains("RunActorActionIfGenerationCurrentAsync", StringComparison.Ordinal) &&
        state.Contains("BattleRuntimeStaleActionSkipped", StringComparison.Ordinal),
        "actor actions queued before a teleport barrier should not run later and stop post-teleport movement presentation");
    AssertTrue(
        teleportObserver.Contains("BattleRuntimeTeleportPresentation", StringComparison.Ordinal) &&
        teleportObserver.Contains("activeMovementTweens", StringComparison.Ordinal),
        "teleport presentation should log the visual snap boundary and active movement lanes for manual QA");
    AssertTrue(
        displacementBoundary.Contains("BattleRuntimeMarkTeleportDisplacementCommitted", StringComparison.Ordinal) &&
        displacementBoundary.Contains("DescribeActorDisplacementState", StringComparison.Ordinal),
        "runtime mark teleport boundary should log displacement state before and after CommitDisplacement so movement-command conflicts are diagnosable");
}

internal static void BattleRuntimeMovementQueuesPerceptionOverlayRefresh()
{
    string playback = ReadBattleRuntimePlaybackSource();
    string overlay = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.BattlePerceptionOverlay.cs"));

    AssertTrue(
        playback.Contains("_queueBattlePerceptionOverlayRefresh?.Invoke();", StringComparison.Ordinal) &&
        !playback.Contains("        RefreshBattlePerceptionOverlay();\n        return _unitRoot.ResolveVisualMoveStepDurationSeconds", StringComparison.Ordinal),
        "movement observation should queue perception overlay refresh instead of rebuilding it once per movement event");
    AssertTrue(
        overlay.Contains("_battlePerceptionOverlayRefreshQueued", StringComparison.Ordinal) &&
        overlay.Contains("Callable.From(FlushBattlePerceptionOverlayRefresh).CallDeferred();", StringComparison.Ordinal),
        "perception overlay refresh should be coalesced on the presentation frame");
}

internal static void BattleRuntimeLiveObservationConsumesSkillUsedAsCastCue()
{
    string liveObservation = ReadBattleRuntimeLiveObservationSource();
    string playback = ReadBattleRuntimePlaybackSource();
    string unitRoot = ReadBattleUnitRootSource();

    AssertTrue(
        liveObservation.Contains("BattleEventKind.SkillUsed", StringComparison.Ordinal) &&
        liveObservation.Contains("ObserveRuntimeSkillUsedEventAsync", StringComparison.Ordinal) &&
        liveObservation.Contains("TrackActorAction(", StringComparison.Ordinal),
        "live runtime observation should consume SkillUsed as the caster-side cast cue on the actor action tail");
    AssertTrue(
        playback.Contains("ObserveRuntimeSkillUsedEventAsync", StringComparison.Ordinal) &&
        unitRoot.Contains("public double PlaySkillCastPresentation(", StringComparison.Ordinal),
        "runtime SkillUsed playback should call a dedicated caster-side skill presentation entry point");
}

internal static void ThunderTagOffhandPresentationDoesNotInterruptMovement()
{
    string liveObservation = ReadBattleRuntimeLiveObservationSource();
    string playback = ReadBattleRuntimePlaybackSource();
    string profileObserver = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "BattleRuntimeSkillProfilePresentationObserver.cs"));
    string unitRoot = ReadBattleUnitRootSource();
    string skillPresentationBody = ExtractMethodBlock(unitRoot, "public double PlaySkillCastPresentation(");

    AssertTrue(
        liveObservation.Contains("IsOffhandSkillReleaseEvent(runtimeEvent)", StringComparison.Ordinal) &&
        liveObservation.Contains("gateMovementStart: !BattleRuntimeSkillProfilePresentationObserver.IsOffhandSkillReleaseEvent(runtimeEvent)", StringComparison.Ordinal),
        "offhand skill releases should not gate later runtime movement starts in live presentation");
    AssertTrue(
        playback.Contains("preserveMovement: BattleRuntimeSkillProfilePresentationObserver.IsOffhandSkillReleaseEvent(runtimeEvent)", StringComparison.Ordinal) &&
        profileObserver.Contains("skill_mark_projectile", StringComparison.Ordinal),
        "mark projectile SkillUsed playback should pass the offhand movement-preservation trait from the runtime event profile");
    AssertTrue(
        unitRoot.Contains("bool preserveMovement = false", StringComparison.Ordinal) &&
        skillPresentationBody.Contains("if (!preserveMovement)", StringComparison.Ordinal) &&
        skillPresentationBody.Contains("StopEntityMovement(actor, snapToLogicalGrid: true)", StringComparison.Ordinal) &&
        skillPresentationBody.Contains("actor.GetComponent<BattleSkillCastFxComponent>()?.PlaySkillCastFx", StringComparison.Ordinal),
        "caster skill presentation should keep anchored skills snapping, while offhand thunder tag keeps movement lanes alive and still plays release FX");
}

internal static void ThunderTagPresentationShowsLightningAndMark()
{
    string liveObservation = ReadBattleRuntimeLiveObservationSource();
    string profileObserver = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "BattleRuntimeSkillProfilePresentationObserver.cs"));
    string unitRoot = ReadBattleUnitRootSource();
    string thunderLinkScenePath = Path.Combine("scenes", "battle", "entities", "fx", "BattleThunderLinkFx.tscn");
    string thunderMarkScenePath = Path.Combine("scenes", "battle", "entities", "fx", "BattleThunderMarkFx.tscn");

    AssertTrue(
        liveObservation.Contains("BattleEventKind.ThunderMarkCreated", StringComparison.Ordinal) &&
        liveObservation.Contains("ObserveRuntimeMarkCreatedEvent", StringComparison.Ordinal),
        "live presentation should consume ThunderMarkCreated so the runtime mark is visible");
    AssertTrue(
        profileObserver.Contains("ObserveRuntimeMarkCreatedEvent", StringComparison.Ordinal) &&
        profileObserver.Contains("unitRoot.PlayMarkProjectilePresentation", StringComparison.Ordinal),
        "thunder mark playback should route through BattleUnitRoot instead of leaving the mark as a log-only runtime event");
    AssertTrue(
        unitRoot.Contains("public double PlayMarkProjectilePresentation(", StringComparison.Ordinal) &&
        unitRoot.Contains("BattleThunderLinkFx.tscn", StringComparison.Ordinal) &&
        unitRoot.Contains("BattleThunderMarkFx.tscn", StringComparison.Ordinal),
        "battle unit root should instantiate authored lightning-link and thunder-mark FX resources for thunder tag");
    AssertTrue(File.Exists(thunderLinkScenePath), "thunder tag should have an authored lightning link scene");
    AssertTrue(File.Exists(thunderMarkScenePath), "thunder tag should have an authored persistent mark scene");
}

internal static void ThunderMarkLifetimeTimerIsGenerationGuarded()
{
    string source = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "BattleThunderMarkFx.cs"));
    string playBody = ExtractMethodBlock(source, "public void Play()");
    string exitTreeBody = ExtractMethodBlock(source, "public override void _ExitTree()");
    string lifetimeBody = ExtractMethodBlock(source, "private async System.Threading.Tasks.Task QueueFreeAfterLifetime(");

    AssertTrue(
        source.Contains("private int _lifetimeVersion", StringComparison.Ordinal),
        "thunder mark FX should keep a local lifetime generation for repeated Play calls");
    AssertTrue(
        playBody.Contains("int lifetimeVersion = ++_lifetimeVersion", StringComparison.Ordinal) &&
        playBody.Contains("QueueFreeAfterLifetime(lifetimeVersion)", StringComparison.Ordinal),
        "each Play call should start a lifetime wait scoped to the current generation");
    AssertTrue(
        exitTreeBody.Contains("_lifetimeVersion++", StringComparison.Ordinal) &&
        exitTreeBody.IndexOf("_lifetimeVersion++", StringComparison.Ordinal) <
        exitTreeBody.IndexOf("KillTween()", StringComparison.Ordinal),
        "exiting the tree should invalidate pending lifetime waits before killing local presentation state");
    AssertTrue(
        lifetimeBody.Contains("CreateTimer(System.Math.Max(0.5, LifetimeSeconds), processAlways: false)", StringComparison.Ordinal),
        "thunder mark lifetime timer should remain pause-aware instead of completing while the battle scene tree is paused");

    string lifetimeGuard = ExtractMethodBlock(source, "private bool IsLifetimeVersionCurrent(");
    int awaitIndex = lifetimeBody.IndexOf("await ToSignal(", StringComparison.Ordinal);
    int postAwaitGuardIndex = lifetimeBody.IndexOf("if (IsLifetimeVersionCurrent(lifetimeVersion))", awaitIndex, StringComparison.Ordinal);
    int queueFreeIndex = lifetimeBody.IndexOf("QueueFree()", StringComparison.Ordinal);
    AssertTrue(
        lifetimeGuard.Contains("GodotObject.IsInstanceValid(this)", StringComparison.Ordinal) &&
        lifetimeGuard.Contains("IsInsideTree()", StringComparison.Ordinal) &&
        lifetimeGuard.Contains("lifetimeVersion == _lifetimeVersion", StringComparison.Ordinal),
        "post-await lifetime continuation should require the node to still be valid, inside the tree, and on the same generation");
    AssertTrue(
        awaitIndex >= 0 &&
        postAwaitGuardIndex > awaitIndex &&
        queueFreeIndex > postAwaitGuardIndex,
        "QueueFree should only run after the lifetime timer continuation rechecks the current generation and node lifecycle");
    AssertTrue(
        source.Contains("CreateTween().BindNode(this)", StringComparison.Ordinal),
        "thunder mark pulse Tween should be bound to the FX node lifecycle");
}

internal static void ThunderTagProjectileReusesChainLightningFxFrames()
{
    string linkScene = File.ReadAllText(Path.Combine("scenes", "battle", "entities", "fx", "BattleThunderLinkFx.tscn"));
    string linkScript = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "BattleThunderLinkFx.cs"));

    AssertTrue(
        linkScene.Contains("assets/battle/abilities/fx/duelyst/damage/fx_chainlightning/frames.tres", StringComparison.Ordinal),
        "thunder tag projectile should reuse the authored chain lightning SpriteFrames asset");
    AssertTrue(
        !linkScene.Contains("fx_f2_killingedge/frames.tres", StringComparison.Ordinal) &&
        !linkScene.Contains("[node name=\"GlowBolt\" type=\"Line2D\"", StringComparison.Ordinal) &&
        !linkScene.Contains("[node name=\"CoreBolt\" type=\"Line2D\"", StringComparison.Ordinal),
        "thunder tag projectile should not use the sword-edge substitute or hand-drawn bolt lines as the primary lightning");
    AssertTrue(
        linkScene.Contains("[node name=\"ChainLightningSprite\" type=\"AnimatedSprite2D\"", StringComparison.Ordinal) &&
        linkScript.Contains("ChainLightningSpritePath", StringComparison.Ordinal) &&
        linkScript.Contains("_projectileRoot.Rotation = endpointLocal.Angle()", StringComparison.Ordinal) &&
        linkScript.Contains("ChainLightningFrameWidthPixels", StringComparison.Ordinal),
        "chain lightning presentation should author an AnimatedSprite2D and rotate/scale it along the cast direction");
}

internal static void ThunderSpiralPresentationUsesAuthoredAreaFx()
{
    string scenePath = Path.Combine("scenes", "battle", "entities", "fx", "BattleThunderSpiralFx.tscn");
    string scriptPath = Path.Combine("src", "Presentation", "Battle", "Entities", "BattleThunderSpiralFx.cs");
    string observerPath = Path.Combine("src", "Presentation", "World", "Sites", "BattleRuntimeSkillProfilePresentationObserver.cs");
    string liveObservation = ReadBattleRuntimeLiveObservationSource();
    string unitRoot = ReadBattleUnitRootSource();
    string worldSiteRoot = ReadWorldSiteRootSource();

    AssertTrue(File.Exists(scenePath), "thunder spiral should have an authored area FX scene");
    AssertTrue(File.Exists(scriptPath), "thunder spiral area FX should have a focused scene script");
    AssertTrue(File.Exists(observerPath), "channeled-area runtime presentation routing should live in a focused profile observer helper");

    string scene = File.ReadAllText(scenePath);
    string script = File.ReadAllText(scriptPath);
    string observer = File.ReadAllText(observerPath);

    AssertTrue(
        scene.Contains("AnimatedSprite2D", StringComparison.Ordinal) &&
        scene.Contains("fx_vortexswirl/frames.tres", StringComparison.Ordinal) &&
        scene.Contains("BattleThunderSpiralFx.cs", StringComparison.Ordinal),
        "thunder spiral should reuse an authored swirl SpriteFrames asset instead of drawing a new exact hand-held effect in code");
    AssertTrue(
        script.Contains("AnimatedSprite2D", StringComparison.Ordinal) &&
        script.Contains("QueueFreeAfterLifetime", StringComparison.Ordinal) &&
        script.Contains("OnSpiralAnimationFinished", StringComparison.Ordinal) &&
        !script.Contains("SetAnimationLoop", StringComparison.Ordinal),
        "thunder spiral FX should loop the authored one-shot SpriteFrames for the Runtime channel duration without mutating the shared resource");
    AssertTrue(
        !scene.Contains("position = Vector2(0, -14)", StringComparison.Ordinal),
        "thunder spiral authored sprite should stay centered on the Runtime 3x3 area center");
    AssertTrue(
        scene.Contains("scale = Vector2(0.5625, 0.5625)", StringComparison.Ordinal),
        "thunder spiral authored scene should default to the 1.5x tuned area scale");
    Vector2 resolvedAreaScale = BattleThunderSpiralFx.ResolveAreaScale(new Vector2(72f, 72f), 128f);
    AssertFloatEqual(0.5625f, resolvedAreaScale.X, 0.0001f, "thunder spiral core should cover the 1.5x tuned 3x3 tile width");
    AssertFloatEqual(0.5625f, resolvedAreaScale.Y, 0.0001f, "thunder spiral core should cover the 1.5x tuned 3x3 tile height");
    Vector2 fixedAreaSize = BattleThunderSpiralFx.ResolveDefaultAreaPixelSize();
    AssertFloatEqual(72f, fixedAreaSize.X, 0.0001f, "thunder spiral fixed visual width should be three 16 px tiles tuned 1.5x larger");
    AssertFloatEqual(72f, fixedAreaSize.Y, 0.0001f, "thunder spiral fixed visual height should be three 16 px tiles tuned 1.5x larger");
    AssertTrue(
        observer.Contains("skill_channeled_area", StringComparison.Ordinal) &&
        observer.Contains("HoldCastAnimationDuringAction", StringComparison.Ordinal) &&
        observer.Contains("runtimeEvent.HasTargetCells", StringComparison.Ordinal) &&
        observer.Contains("runtimeEvent.TargetGridX", StringComparison.Ordinal) &&
        observer.Contains("runtimeEvent.TargetGridY", StringComparison.Ordinal) &&
        observer.Contains("unitRoot.PlayChanneledAreaPresentation", StringComparison.Ordinal),
        "channeled-area presentation should consume the Runtime SkillUsed target-cell center instead of the caster cell or HUD preview");
    AssertTrue(
        liveObservation.Contains("BattleRuntimeSkillProfilePresentationObserver.IsChanneledAreaSkillUsedEvent", StringComparison.Ordinal) &&
        liveObservation.Contains("BattleRuntimeSkillProfilePresentationObserver.ObserveRuntimeChanneledAreaSkillUsedEvent", StringComparison.Ordinal) &&
        liveObservation.Contains("_focusBattleActionEntity?.Invoke(actor, true)", StringComparison.Ordinal),
        "live SkillUsed observation should route channeled-area profiles to area FX and a coarse action focus");
    AssertTrue(
        unitRoot.Contains("DefaultThunderSpiralFxScenePath", StringComparison.Ordinal) &&
        unitRoot.Contains("ThunderSpiralAreaOffset = Vector2.Zero", StringComparison.Ordinal) &&
        unitRoot.Contains("public double PlayChanneledAreaPresentation(", StringComparison.Ordinal) &&
        unitRoot.Contains("_tryResolveCellGlobalPosition?.Invoke(targetSurface.Position", StringComparison.Ordinal) &&
        unitRoot.Contains("ResolveChanneledAreaPixelSize", StringComparison.Ordinal) &&
        unitRoot.Contains("ConfigureAreaCoreSize", StringComparison.Ordinal) &&
        unitRoot.Contains("SuppressActorAttachedSkillCastFx(suppressActorCastFx)", StringComparison.Ordinal) &&
        !unitRoot.Contains("new GridPosition(targetCenter.X + 1, targetCenter.Y)", StringComparison.Ordinal) &&
        !unitRoot.Contains("new GridPosition(targetCenter.X, targetCenter.Y + 1)", StringComparison.Ordinal) &&
        !unitRoot.Contains("HandPath", StringComparison.Ordinal) &&
        !unitRoot.Contains("PalmPath", StringComparison.Ordinal),
        "BattleUnitRoot should spawn thunder spiral at the Runtime 3x3 target center, keep the tuned fixed visual size, and avoid a second caster-foot skill FX");
    AssertTrue(
        worldSiteRoot.Contains("_battleCamera?.FollowActionEntityIfNeeded(entity, force)", StringComparison.Ordinal),
        "world-site battle runtime should wire the existing coarse action camera follow as the skill focus path");
}

internal static void RuntimeSkillDamageDoesNotReplayCasterCast()
{
    string playback = ReadBattleRuntimePlaybackSource();

    AssertTrue(
        playback.Contains("PlayRuntimeDamageFeedback(", StringComparison.Ordinal) &&
        !playback.Contains("BattleActionResult.AbilitySucceeded", StringComparison.Ordinal),
        "runtime skill damage should keep target impact and damage feedback without replaying the caster skill-cast animation");
}

internal static void RuntimeSkillDamageFeedbackDoesNotExtendCasterActionTail()
{
    Type? stateType = Type.GetType("Rpg.Presentation.World.Sites.BattleRuntimeLivePresentationState, rpg");
    AssertTrue(stateType != null, "missing live presentation state type");
    System.Reflection.MethodInfo? trackSkillDamageFeedback = stateType!.GetMethod("TrackSkillDamageFeedback");
    AssertTrue(trackSkillDamageFeedback != null, "skill damage feedback should have a caster-action-tail-free scheduling entry");

    object state = Activator.CreateInstance(
        stateType,
        new object[] { new Dictionary<string, BattleEntity>(StringComparer.Ordinal) })!;
    TaskCompletionSource skillCastTail = new(TaskCreationOptions.RunContinuationsAsynchronously);
    TaskCompletionSource<bool> skillDamageFeedbackStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    TaskCompletionSource<bool> basicAttackFeedbackStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    stateType.GetMethod("TrackActorAction")!.Invoke(
        state,
        new object[]
        {
            "caster",
            new Func<Task>(() => skillCastTail.Task),
            true
        });
    trackSkillDamageFeedback!.Invoke(
        state,
        new object[]
        {
            "caster",
            "target",
            new Func<Task>(() =>
            {
                skillDamageFeedbackStarted.SetResult(true);
                return Task.CompletedTask;
            })
        });
    stateType.GetMethod("TrackActorDamage")!.Invoke(
        state,
        new object[]
        {
            "caster",
            "target",
            new Func<Task>(() =>
            {
                basicAttackFeedbackStarted.SetResult(true);
                return Task.CompletedTask;
            })
        });

    bool skillFeedbackStartedBeforeCastTail = Task.WhenAny(
        skillDamageFeedbackStarted.Task,
        Task.Delay(120)).GetAwaiter().GetResult() == skillDamageFeedbackStarted.Task;
    bool basicAttackWaitedForCastTail = Task.WhenAny(
        basicAttackFeedbackStarted.Task,
        Task.Delay(120)).GetAwaiter().GetResult() != basicAttackFeedbackStarted.Task;

    skillCastTail.SetResult();
    ((Task)stateType.GetMethod("WaitForAllAsync")!.Invoke(state, Array.Empty<object>())!)
        .GetAwaiter()
        .GetResult();

    AssertTrue(
        skillFeedbackStartedBeforeCastTail,
        "skill damage impact feedback should not wait for or extend the caster's channeled SkillUsed presentation tail");
    AssertTrue(
        basicAttackWaitedForCastTail && basicAttackFeedbackStarted.Task.IsCompleted,
        "ordinary attack feedback should still serialize through the caster action tail");
}

internal static void BattleRuntimeVisualMovementKeepsRuntimeActionDuration()
{
    string unitRoot = ReadBattleUnitRootSource();
    string playback = ReadBattleRuntimePlaybackSource();

    AssertTrue(
        unitRoot.Contains("public double ResolveVisualMoveStepDurationSeconds", StringComparison.Ordinal) &&
        unitRoot.Contains("return System.Math.Max(0.01, baseSeconds);", StringComparison.Ordinal),
        "each visual movement segment should still use the Runtime action duration instead of predicting future cells");
    AssertTrue(
        playback.Contains("double visualMoveSeconds = unitRoot.MoveEntityTo", StringComparison.Ordinal) &&
        playback.Contains("return visualMoveSeconds;", StringComparison.Ordinal),
        "movement observer should report the actor-local motion lane duration, including only committed-event buffer time");
}

internal static void BattleRuntimeMovementPlaybackDoesNotUseLookaheadCorrectionPath()
{
    string playback = ReadBattleRuntimePlaybackSource();
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
        runtime.Contains("PlayRuntimeDamageFeedbackEventAsync", StringComparison.Ordinal),
        "live presentation should delay dependent damage through actor and target movement tails instead of a same-tick duration heuristic");
}

internal static void BattleRuntimeLiveObservationWaitsForSameTickMovementBeforeDependentAttack()
{
    string runtime = ReadBattleRuntimeLiveObservationSource();
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

internal static void RuntimeTargetDamageDoesNotWaitForAttackerMovementBacklog()
{
    Type? stateType = Type.GetType("Rpg.Presentation.World.Sites.BattleRuntimeLivePresentationState, rpg");
    AssertTrue(stateType != null, "missing live presentation state type");
    object state = Activator.CreateInstance(
        stateType!,
        new object[] { new Dictionary<string, BattleEntity>(StringComparer.Ordinal) })!;
    TaskCompletionSource attackerMovement = new(TaskCreationOptions.RunContinuationsAsynchronously);
    TaskCompletionSource<bool> damageTaskStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    stateType!.GetMethod("TrackActorMovement")!.Invoke(
        state,
        new object[]
        {
            "attacker",
            new Func<double>(() => 4.8),
            new Func<double, Task>(_ => attackerMovement.Task)
        });
    stateType.GetMethod("TrackTargetDamage")!.Invoke(
        state,
        new object?[]
        {
            "attacker",
            "target",
            new Func<Task, Task>(previousTargetDamageTail =>
            {
                damageTaskStarted.SetResult(previousTargetDamageTail != null);
                return Task.CompletedTask;
            }),
            null
        });

    bool startedBeforeAttackerMovementFinished = Task.WhenAny(
        damageTaskStarted.Task,
        Task.Delay(120)).GetAwaiter().GetResult() == damageTaskStarted.Task;
    attackerMovement.SetResult();
    ((Task)stateType.GetMethod("WaitForAllAsync")!.Invoke(state, Array.Empty<object>())!)
        .GetAwaiter()
        .GetResult();

    AssertTrue(
        startedBeforeAttackerMovementFinished,
        "target damage semantics should not wait behind the attacker's stale movement presentation backlog");
    AssertTrue(
        damageTaskStarted.Task.Result == false,
        "first target damage task should not receive a previous same-target damage tail");
}

internal static void RuntimePlaybackAppliesDamageSemanticsThroughTargetQueue()
{
    string state = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "BattleRuntimeLivePresentationState.cs"));
    string runtime = ReadBattleRuntimeLiveObservationSource();
    string playback = ReadBattleRuntimePlaybackSource();

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

internal static void RuntimePlaybackMirrorsDamageFromRuntimeHpFacts()
{
    string liveObservation = ReadBattleRuntimeLiveObservationSource();
    string health = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "HealthComponent.cs"));
    string runtimeEvent = File.ReadAllText(Path.Combine("src", "Runtime", "Battle", "Events", "BattleEvent.cs"));

    AssertTrue(
        runtimeEvent.Contains("HasTargetHitPoints", StringComparison.Ordinal) &&
        runtimeEvent.Contains("TargetHpBefore", StringComparison.Ordinal) &&
        runtimeEvent.Contains("TargetHpAfter", StringComparison.Ordinal),
        "Runtime damage events should expose target HP before/after facts for Presentation mirrors");
    AssertTrue(
        health.Contains("MirrorRuntimeDamage", StringComparison.Ordinal),
        "Presentation health should expose a mirror method for Runtime damage facts");
    AssertTrue(
        liveObservation.Contains("health.MirrorRuntimeDamage", StringComparison.Ordinal) &&
        liveObservation.Contains("runtimeEvent.TargetHpBefore", StringComparison.Ordinal) &&
        liveObservation.Contains("runtimeEvent.TargetHpAfter", StringComparison.Ordinal),
        "live presentation should mirror Runtime HP facts instead of recalculating target HP locally");
    AssertTrue(
        !liveObservation.Contains("health.ApplyDamage(damage, actor)", StringComparison.Ordinal),
        "live presentation must not apply damage through the Presentation health authority");
}

internal static void RuntimePlaybackTargetDamageQueueDoesNotSerializeImpactDelay()
{
    string state = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "BattleRuntimeLivePresentationState.cs"));
    string playback = ReadBattleRuntimePlaybackSource();

    AssertTrue(
        state.Contains("System.Func<Task, Task> createTask", StringComparison.Ordinal) &&
        state.Contains("createTask(previousTargetDamageTail)", StringComparison.Ordinal) &&
        !state.Contains("new[] { actorMovementTail, targetMovementTail, previousTargetDamageTail }", StringComparison.Ordinal),
        "target damage queue should pass the previous target damage tail into the task instead of waiting it before impact delay starts");

    int impactWaitIndex = playback.IndexOf("await WaitPresentationSeconds(clampedImpactDelaySeconds)", StringComparison.Ordinal);
    int previousTailIndex = playback.IndexOf("await previousTargetDamageTail", StringComparison.Ordinal);
    AssertTrue(
        impactWaitIndex >= 0 &&
        previousTailIndex > impactWaitIndex,
        "runtime damage application should let impact delay elapse before waiting for prior target damage application order");
    AssertTrue(
        !playback.Contains("actionDurationSeconds,\r\n            runtimeEvent.ActionImpactDelaySeconds", StringComparison.Ordinal) &&
        !playback.Contains("actionDurationSeconds,\n            runtimeEvent.ActionImpactDelaySeconds", StringComparison.Ordinal) &&
        (playback.Contains("actionDurationSeconds,\r\n            0,", StringComparison.Ordinal) ||
         playback.Contains("actionDurationSeconds,\n            0,", StringComparison.Ordinal)) &&
        playback.Contains("fallbackToActorAttackImpactDelay: false", StringComparison.Ordinal),
        "DamageApplied is already the Runtime impact boundary; target-side HP and hit feedback must not wait ActionImpactDelaySeconds again");
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
        !runtime.Contains("TryResolveActiveBattle(", StringComparison.Ordinal),
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
    string playback = ReadBattleRuntimePlaybackSource();

    AssertTrue(
        runtime.Contains("_battleRuntimeLivePresentationObserver.ObserveAsync", StringComparison.Ordinal) &&
        runtime.Contains("_ = _battleRuntimeLivePresentationObserver.ObserveAsync", StringComparison.Ordinal),
        "runtime events should be observed by presentation without awaiting the whole visual action lane");
    AssertTrue(
        playback.Contains("ObserveRuntimeMovementEvent", StringComparison.Ordinal) &&
        playback.Contains("PlayRuntimeDamageFeedbackEventAsync", StringComparison.Ordinal) &&
        !playback.Contains("await WaitPresentationSeconds(movementSeconds)", StringComparison.Ordinal) &&
        !playback.Contains("await WaitPresentationSeconds(attackPresentationSeconds)", StringComparison.Ordinal),
        "movement and attack visuals must not block unrelated future simulation ticks");
}

}
