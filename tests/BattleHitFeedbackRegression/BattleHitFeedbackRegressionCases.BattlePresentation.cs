using Rpg.Presentation.Battle.Actions;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Debug;
using Rpg.Presentation.Battle.Feedback;
using Rpg.Presentation.Battle.Flow;
using Rpg.Presentation.Battle.Preview;
using Rpg.Presentation.Common;
using Rpg.Presentation.World;
using Rpg.Definitions.Battle.Abilities;
using Rpg.Definitions.Battle.Audio;
using Rpg.Application.Battle;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using System.Text.Json;

internal static partial class BattleHitFeedbackRegressionCases
{
internal static void MultiTargetHitFeedback()
{
    BattleDamageEvent[] events =
    {
        new(null, "enemy_a", 6, false),
        new(null, "enemy_b", 12, true),
        new(null, "enemy_a", 0, false)
    };

    BattleHitFeedbackPlan plan = BattleHitFeedbackPlanner.Build(events);

    AssertSequence(new[] { "enemy_a", "enemy_b" }, plan.OutlinedTargetIds, "all impacted targets should be outlined once");
    AssertEqual(2, plan.DamageNumbers.Count, "only positive damage should create floating damage numbers");
    AssertEqual("enemy_a", plan.DamageNumbers[0].TargetId, "first damage target");
    AssertEqual("-6", plan.DamageNumbers[0].Text, "first damage text");
    AssertEqual("enemy_b", plan.DamageNumbers[1].TargetId, "second damage target");
    AssertEqual("-12", plan.DamageNumbers[1].Text, "second damage text");
}

internal static void SkillDamageFeedbackPreservesRuntimeSourceAttribution()
{
    BattleDamageEvent damage = new(
        null,
        "enemy_a",
        18,
        false,
        "cmd_skill",
        "cmd_skill:action:first_slice_hero_breakthrough",
        "first_slice_hero_breakthrough",
        "Damage");

    BattleHitFeedbackPlan plan = BattleHitFeedbackPlanner.Build(new[] { damage });

    AssertEqual(1, plan.DamageNumbers.Count, "skill damage should still create one damage number");
    BattleDamageNumberSpec number = plan.DamageNumbers[0];
    AssertEqual("cmd_skill", damage.SourceCommandId, "damage event source command");
    AssertEqual("cmd_skill", number.SourceCommandId, "damage number source command");
    AssertEqual("cmd_skill:action:first_slice_hero_breakthrough", number.SourceActionId, "damage number source action");
    AssertEqual("first_slice_hero_breakthrough", number.SourceDefinitionId, "damage number source definition");
    AssertEqual("Damage", number.EffectKind, "damage number effect kind");
}

internal static void DamageNumberMotionDefaults()
{
    BattleDamageNumberMotionSpec spec = BattleDamageNumberMotionSpec.Default;

    AssertTrue(spec.SpawnOffset.Y > -40f, "damage number should start closer to the unit than the old high offset");
    AssertTrue(spec.FloatOffset.X > 0f, "damage number should drift slightly right");
    AssertTrue(spec.FloatOffset.Y < 0f, "damage number should drift upward");
}

internal static void FriendlyHoverStyle()
{
    BattleHoverPreviewStyle style = BattleHoverPreviewStyle.ForFaction(BattleFaction.Player);

    AssertEqual(BattleGridHighlightKind.FriendlyMove, style.MoveKind, "friendly hover movement should use green movement kind");
    AssertEqual(BattleGridHighlightKind.FriendlyAttack, style.AttackKind, "friendly hover attack should use yellow attack kind");
}

internal static void FriendlyHoverWorkload()
{
    BattleHoverPreviewStyle friendly = BattleHoverPreviewStyle.ForFaction(BattleFaction.Player);
    BattleHoverPreviewStyle enemy = BattleHoverPreviewStyle.ForFaction(BattleFaction.Enemy);

    AssertEqual(true, friendly.ProjectAttackFromReachableMoveOrigins, "friendly hover should keep combined movement plus attack preview semantics");
    AssertEqual(true, enemy.ProjectAttackFromReachableMoveOrigins, "enemy hover should keep full threat projection");
}

internal static void FriendlyHoverSuppressesAttackCellsUnderTargets()
{
    BattleHoverCellPresentation presentation = BattleHoverCellPresentation.Build(
        attackCells: new[] { new GridPosition(1, 1), new GridPosition(2, 1), new GridPosition(3, 1) },
        targetCells: new[] { new GridPosition(2, 1) });

    AssertSequence(
        new[] { new GridPosition(1, 1), new GridPosition(3, 1) },
        presentation.AttackCells,
        "friendly hover should not paint yellow attack cells under target units");
    AssertSequence(
        Array.Empty<GridPosition>(),
        presentation.TargetCells,
        "friendly hover should not paint yellow target cells under target units");
    AssertSequence(
        new[] { new GridPosition(2, 1) },
        presentation.TargetPointerCells,
        "target cells should remain available for separate target-focus presentation");
}

internal static void HoverFrameUsesUnitFootprint()
{
    string overlaySource = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "BattleGridHighlightOverlay.cs"));
    string unitRootSource = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "BattleUnitRoot.cs"));
    string normalizedOverlaySource = NormalizeWhitespace(overlaySource);

    AssertTrue(
        overlaySource.Contains("TryResolveHoveredEntityFootprint", StringComparison.Ordinal) &&
        overlaySource.Contains("_siteRoot?.FindEntityAt(position)", StringComparison.Ordinal) &&
        normalizedOverlaySource.Contains("BattleFootprintCells.Enumerate( gridOccupant.Position,", StringComparison.Ordinal),
        "automatic hover should draw the existing hover frame over the hovered unit's full footprint");
    AssertTrue(
        unitRootSource.Contains("ContainsGridFootprint", StringComparison.Ordinal) &&
        !unitRootSource.Contains("GridOccupant?.Position == position", StringComparison.Ordinal),
        "hover entity lookup should hit any covered footprint cell, not only the top-left anchor");
}

internal static void HighlightTileLayerDiff()
{
    HashSet<GridPosition> current = new()
    {
        new(1, 1),
        new(2, 1),
        new(3, 1)
    };
    GridPosition[] next =
    {
        new(2, 1),
        new(3, 1),
        new(4, 1)
    };

    BattleGridHighlightCellDiff diff = BattleGridHighlightCellDiff.Build(current, next);

    AssertSequence(new[] { new GridPosition(1, 1) }, diff.CellsToErase.ToArray(), "tile highlight diff should erase only missing cells");
    AssertSequence(new[] { new GridPosition(4, 1) }, diff.CellsToPaint.ToArray(), "tile highlight diff should paint only new cells");
}

internal static void AttackTargetPresentation()
{
    BattleTargetPresentationPlan plan = BattleTargetPresentationPlan.Build(new[] { "enemy_a", "enemy_b", "enemy_a", "" });

    AssertEqual(false, plan.ShowTargetGridCells, "attack target presentation should not use yellow target cells");
    AssertSequence(new[] { "enemy_a", "enemy_b" }, plan.TargetEntityIds, "attack target presentation should outline each target once");
}

internal static void MovementPathArrowsDisabled()
{
    AssertEqual(false, BattlePathArrowPresentation.Default.ShowMovementPathArrows, "movement preview should not draw path arrows");
}

internal static void UnitVisualScaleMultiplier()
{
    AssertFloatEqual(0.8f, BattleUnitVisualScale.Default.SpriteScaleMultiplier, 0.0001f, "unit visuals should be reduced by one fifth");
    AssertFloatEqual(0.35f, BattleUnitVisualScale.Default.FootprintScaleStepMultiplier, 0.0001f, "footprint visual growth should be tunable and below one full cell per size step");
}

internal static void UnitDefinitionFootprintDefaults()
{
    string definition = File.ReadAllText(Path.Combine("src", "Definitions", "Battle", "BattleUnitDefinition.cs"));
    string forceRequest = File.ReadAllText(Path.Combine("src", "Application", "Battle", "BattleForceRequest.cs"));
    string snapshot = File.ReadAllText(Path.Combine("src", "Application", "Battle", "Snapshots", "BattleGroupSnapshot.cs"));

    AssertTrue(
        definition.Contains("public int FootprintWidth { get; set; } = 1;", StringComparison.Ordinal),
        "battle unit definitions should default footprint width to one cell");
    AssertTrue(
        definition.Contains("public int FootprintHeight { get; set; } = 1;", StringComparison.Ordinal),
        "battle unit definitions should default footprint height to one cell");
    AssertTrue(
        forceRequest.Contains("public int FootprintWidth { get; set; } = 1;", StringComparison.Ordinal) &&
        forceRequest.Contains("public int FootprintHeight { get; set; } = 1;", StringComparison.Ordinal),
        "battle force requests should carry footprint metadata with 1x1 defaults");
    AssertTrue(
        snapshot.Contains("public int FootprintWidth { get; set; } = 1;", StringComparison.Ordinal) &&
        snapshot.Contains("public int FootprintHeight { get; set; } = 1;", StringComparison.Ordinal),
        "battle snapshots should carry footprint metadata with 1x1 defaults");
}

internal static void BattleUnitFactoryScalesSpritesUniformlyByFootprint()
{
    string source = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "BattleUnitFactory.cs"))
        .Replace("\r\n", "\n", StringComparison.Ordinal);

    AssertTrue(
        source.Contains("ResolveFootprintVisualScale(definition)", StringComparison.Ordinal),
        "unit factory should derive visual footprint scale from the battle unit definition");
    AssertTrue(
        source.Contains("BattleUnitVisualScale.Default.FootprintScaleStepMultiplier", StringComparison.Ordinal),
        "footprint sprite growth should use a tunable coefficient instead of raw cell count");
    AssertTrue(
        source.Contains("int footprintSize = System.Math.Max(width, height);", StringComparison.Ordinal),
        "visual footprint scale should use the largest footprint side as the size signal");
    AssertTrue(
        source.Contains("return new Vector2(uniformScale, uniformScale);", StringComparison.Ordinal),
        "footprint sprite scale should remain uniform on X and Y");
    AssertTrue(
        !source.Contains("return new Vector2(\n            System.Math.Clamp(definition?.FootprintWidth ?? 1, 1, 3),\n            System.Math.Clamp(definition?.FootprintHeight ?? 1, 1, 3));", StringComparison.Ordinal),
        "footprint sprite scale must not stretch art by raw footprint width and height");
}

internal static void WorldSiteRuntimeCopiesDefinitionFootprintToBattleRequests()
{
    string runtime = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRuntime.cs"));
    string deployment = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRequestDeployment.cs"));

    AssertTrue(
        runtime.Contains("ApplyBattleRequestForceFootprints(request);", StringComparison.Ordinal),
        "site battle runtime should enrich battle requests with definition footprints before probe");
    AssertTrue(
        deployment.Contains("force.FootprintWidth = definition.FootprintWidth;", StringComparison.Ordinal) &&
        deployment.Contains("force.FootprintHeight = definition.FootprintHeight;", StringComparison.Ordinal),
        "site battle runtime should copy configured footprint width and height from unit definitions");
}

internal static void ActionCueSequencerOrder()
{
    List<string> events = new();
    BattleActionCueSequencer sequencer = new();

    sequencer.RunAsync(
            entityId: "unit_a",
            faction: BattleFaction.Enemy,
            showCue: cue =>
            {
                events.Add($"show:{cue.EntityId}:{cue.Faction}:{cue.DurationSeconds:0.0}");
                return Task.CompletedTask;
            },
            wait: seconds =>
            {
                events.Add($"wait:{seconds:0.0}");
                return Task.CompletedTask;
            },
            action: () =>
            {
                events.Add("action");
                return Task.CompletedTask;
            },
            hideCue: entityId =>
            {
                events.Add($"hide:{entityId}");
                return Task.CompletedTask;
            })
        .GetAwaiter()
        .GetResult();

    AssertSequence(
        new[] { "show:unit_a:Enemy:0.5", "wait:0.5", "action", "hide:unit_a" },
        events,
        "action cue should finish its visible hold before the unit action runs");
}

internal static void HoverInfoPanelAnchorsToScreenEdge()
{
    var viewport = new Godot.Vector2(1280f, 720f);
    var panelSize = new Godot.Vector2(320f, 220f);
    var margin = new Godot.Vector2(18f, 96f);

    Godot.Vector2 position = BattleHoverInfoPanelLayout.CalculateRightDockedPosition(viewport, panelSize, margin);

    AssertFloatEqual(942f, position.X, 0.001f, "hover info should dock to the right edge");
    AssertFloatEqual(96f, position.Y, 0.001f, "hover info should reserve the top HUD area");

    Godot.Vector2 smallPosition = BattleHoverInfoPanelLayout.CalculateRightDockedPosition(
        new Godot.Vector2(280f, 160f),
        panelSize,
        margin);

    AssertFloatEqual(18f, smallPosition.X, 0.001f, "hover info should stay inside narrow viewports");
    AssertFloatEqual(18f, smallPosition.Y, 0.001f, "hover info should fall back to edge padding in short viewports");
}

internal static void MapCameraMiddleDragPansOppositeToMouseMotion()
{
    Godot.Vector2 position = MapCameraController.CalculateMiddleMouseDragPanPosition(
        currentPosition: new Godot.Vector2(500f, 320f),
        mouseRelative: new Godot.Vector2(40f, -20f),
        zoomScalar: 2f);

    AssertFloatEqual(480f, position.X, 0.001f, "middle drag should pan opposite to horizontal mouse motion at current zoom");
    AssertFloatEqual(330f, position.Y, 0.001f, "middle drag should pan opposite to vertical mouse motion at current zoom");
}

internal static void UnitAudioDefinitionResolvesCueVariants()
{
    AssertEqual(0, BattleUnitAudioDefinition.ResolveVariantIndex(0, 2), "first variant index should resolve directly");
    AssertEqual(1, BattleUnitAudioDefinition.ResolveVariantIndex(1, 2), "second variant index should resolve directly");
    AssertEqual(0, BattleUnitAudioDefinition.ResolveVariantIndex(2, 2), "variant index should wrap forward");
    AssertEqual(1, BattleUnitAudioDefinition.ResolveVariantIndex(-1, 2), "variant index should wrap backward");
    AssertEqual(0, BattleUnitAudioDefinition.ResolveVariantIndex(5, 1), "single variant should always resolve to zero");
    AssertEqual(-1, BattleUnitAudioDefinition.ResolveVariantIndex(0, 0), "empty variants should return sentinel");
}

internal static void AbilitySpatialContractDefaults()
{
    string abilityDefinition = File.ReadAllText(Path.Combine("src", "Definitions", "Battle", "Abilities", "AbilityDefinition.cs"));
    string targetMode = File.ReadAllText(Path.Combine("src", "Definitions", "Battle", "Abilities", "AbilityTargetMode.cs"));
    string directionMode = File.ReadAllText(Path.Combine("src", "Definitions", "Battle", "Abilities", "AbilityDirectionMode.cs"));
    string areaShape = File.ReadAllText(Path.Combine("src", "Definitions", "Battle", "Abilities", "AbilityAreaShape.cs"));

    AssertTrue(targetMode.Contains("UnitTarget = 0", StringComparison.Ordinal), "unit target mode should be the stable default enum value");
    AssertTrue(directionMode.Contains("EightWay = 1", StringComparison.Ordinal), "eight-way direction mode should be available");
    AssertTrue(areaShape.Contains("SingleActor = 0", StringComparison.Ordinal), "single actor area shape should be the stable default enum value");
    AssertTrue(
        abilityDefinition.Contains("public AbilityTargetMode TargetMode { get; set; } = AbilityTargetMode.UnitTarget;", StringComparison.Ordinal),
        "basic ability target mode should default to unit target");
    AssertTrue(
        abilityDefinition.Contains("public AbilityDirectionMode DirectionMode { get; set; } = AbilityDirectionMode.EightWay;", StringComparison.Ordinal),
        "basic ability direction mode should default to eight-way");
    AssertTrue(
        abilityDefinition.Contains("public AbilityAreaShape AreaShape { get; set; } = AbilityAreaShape.SingleActor;", StringComparison.Ordinal),
        "basic ability area shape should default to single actor");
}

internal static void BattleRuntimePlaybackConsumesRuntimeMovementCells()
{
    string source = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRuntimePlayback.cs"));

    AssertTrue(
        source.Contains("runtimeEvent.HasMovementCells", StringComparison.Ordinal),
        "battle runtime playback should require authoritative runtime movement cells");
    AssertTrue(
        source.Contains("runtimeEvent.ToGridX", StringComparison.Ordinal) &&
        source.Contains("runtimeEvent.ToGridY", StringComparison.Ordinal),
        "battle runtime playback should consume runtime destination cells");
    AssertTrue(
        !source.Contains("MoveRuntimeActorIntoVisualAttackRangeAsync", StringComparison.Ordinal),
        "presentation must not move units into attack range independently before damage playback");
    AssertTrue(
        !source.Contains("TryResolveRuntimeVisualPathStep", StringComparison.Ordinal),
        "presentation must not run a separate visual pathfinder for runtime combat");
}

internal static void BattleRuntimePlaybackKeepsMoveLoopAcrossConsecutiveMoveSteps()
{
    string source = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRuntimePlayback.cs"));
    string runtime = string.Join("\n", Directory
        .GetFiles(Path.Combine("src", "Presentation", "World", "Sites"), "WorldSiteRoot.BattleRuntime*.cs")
        .OrderBy(path => path, StringComparer.Ordinal)
        .Select(File.ReadAllText));

    AssertTrue(
        source.Contains("restartMoveAnimation: false", StringComparison.Ordinal) &&
        source.Contains("returnToIdleOnComplete: returnToIdleOnComplete", StringComparison.Ordinal) &&
        !source.Contains("returnToIdleOnComplete: true", StringComparison.Ordinal),
        "runtime movement presentation should keep the move loop unless a caller explicitly closes it");
    AssertTrue(
        runtime.Contains("TrackActorMovement", StringComparison.Ordinal) &&
        runtime.Contains("AdvanceBattleGroupRuntimeOnLiveClockAsync", StringComparison.Ordinal),
        "live runtime observation should keep movement open during battle instead of toggling idle between single-tick move events");
    AssertTrue(
        runtime.Contains("_unitRoot?.PlayIdleForActiveEntities();", StringComparison.Ordinal),
        "runtime presentation should return surviving units to idle after the live battle finishes");
}

internal static void BattleRuntimePlaybackWaitsForAttackAnimationDuration()
{
    string playback = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRuntimePlayback.cs"));
    string unitRoot = ReadBattleUnitRootSource();
    string runtime = string.Join("\n", Directory
        .GetFiles(Path.Combine("src", "Presentation", "World", "Sites"), "WorldSiteRoot.BattleRuntime*.cs")
        .OrderBy(path => path, StringComparer.Ordinal)
        .Select(File.ReadAllText));

    AssertTrue(
        unitRoot.Contains("public double PlayActionResultAnimation(BattleActionResult result)", StringComparison.Ordinal) &&
        unitRoot.Contains("ResolveAttackDurationSeconds()", StringComparison.Ordinal),
        "battle action presentation should expose the resolved attack animation duration to the runtime event observer");
    AssertTrue(
        playback.Contains("_unitRoot.PlayActionResultAnimation(BattleActionResult.AttackSucceeded", StringComparison.Ordinal) &&
        playback.Contains("double runtimeActionSeconds = runtimeEvent.ActionDurationSeconds", StringComparison.Ordinal) &&
        playback.Contains("double attackPresentationSeconds = System.Math.Max(0.42, runtimeActionSeconds)", StringComparison.Ordinal) &&
        playback.Contains("Task attackPresentationTask = WaitSiteBattlePresentationSeconds(attackPresentationSeconds)", StringComparison.Ordinal) &&
        playback.Contains("await attackPresentationTask", StringComparison.Ordinal) &&
        runtime.Contains("TrackActorAction", StringComparison.Ordinal),
        "runtime damage presentation should serialize one actor's attack visuals by runtime action seconds without blocking unrelated simulation ticks");
}

internal static void BattleRuntimeTacticalPauseFreezesSceneTreeAndKeepsCommandUi()
{
    string runtime = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRuntime.cs"));
    string commandUi = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRuntimeCommandHud.cs"));
    string incremental = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRuntimeIncremental.cs"));
    string root = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.cs"));
    string siteHud = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.SiteManagementHud.cs"));

    AssertTrue(
        runtime.Contains("while (_battleRuntimeCommandPauseActive", StringComparison.Ordinal) &&
        runtime.Contains("BattleRuntimePresentationWaitPaused", StringComparison.Ordinal) &&
        runtime.Contains("CreateTimer(0.05, processAlways: true)", StringComparison.Ordinal),
        "runtime event playback waits should stop advancing while the command pause overlay is active.");
    AssertTrue(
        commandUi.Contains("SetBattleRuntimeCommandPauseActive", StringComparison.Ordinal) &&
        root.Contains("ApplyBattleRuntimeScenePause", StringComparison.Ordinal) &&
        root.Contains("ProcessModeEnum.Always", StringComparison.Ordinal) &&
        root.Contains("ProcessModeEnum.Pausable", StringComparison.Ordinal) &&
        root.Contains("GetTree().Paused = paused", StringComparison.Ordinal),
        "battle command pause should freeze the scene tree while keeping the root/HUD command path interactive.");
    AssertTrue(
        siteHud.Contains("SetBattleRuntimeCommandPauseActive(false, \"runtime_disabled\")", StringComparison.Ordinal) &&
        root.Contains("SetBattleRuntimeCommandPauseActive(false, \"exit_tree\")", StringComparison.Ordinal),
        "battle runtime pause must be cleared when runtime ends or the scene exits so the wider game is not left paused.");

    int gateIndex = incremental.IndexOf("await WaitForBattleRuntimeAdvanceGateAsync()", StringComparison.Ordinal);
    int advanceIndex = incremental.IndexOf("controller.AdvanceFixedTick(tickSeconds)", StringComparison.Ordinal);
    AssertTrue(
        gateIndex >= 0 &&
        advanceIndex > gateIndex,
        "live runtime should wait for tactical pause clearance before advancing the next fixed Runtime tick.");
}

internal static void BattleRuntimeTacticalPauseFreezesUnitPresentationWithoutReplay()
{
    string root = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.cs"));
    string unitRoot = ReadBattleUnitRootSource();
    string animation = string.Join("\n", Directory
        .GetFiles(Path.Combine("src", "Presentation", "Battle", "Entities"), "UnitAnimationComponent*.cs")
        .OrderBy(path => path, StringComparer.Ordinal)
        .Select(File.ReadAllText));
    string damageReaction = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "DamageReactionComponent.cs"));
    string pauseMethod = ExtractMethodBlock(root, "private void ApplyBattleRuntimeScenePause(bool paused, string reason)");
    string processMethod = ExtractMethodBlock(unitRoot, "public override void _Process(double delta)");
    string unitPauseMethod = ExtractMethodBlock(unitRoot, "public void SetBattlePresentationPaused");
    string animationPauseMethod = ExtractMethodBlock(animation, "public void SetPresentationPaused");

    AssertTrue(
        pauseMethod.Contains("_unitRoot?.SetBattlePresentationPaused(paused)", StringComparison.Ordinal),
        "world-site tactical pause should explicitly propagate the presentation pause to the battle unit root.");
    AssertTrue(
        processMethod.Contains("_battlePresentationPaused", StringComparison.Ordinal) &&
        processMethod.IndexOf("_battlePresentationPaused", StringComparison.Ordinal) <
        processMethod.IndexOf("_movementLanes.Count", StringComparison.Ordinal),
        "unit-root visual movement should stop before consuming movement lane time while presentation pause is active.");
    AssertTrue(
        unitPauseMethod.Contains("GetEntitiesSnapshot()", StringComparison.Ordinal) &&
        unitPauseMethod.Contains("SetPresentationPaused(paused)", StringComparison.Ordinal),
        "unit-root pause should apply to all current unit animation components.");
    AssertTrue(
        animationPauseMethod.Contains("PauseAnimatedSpritePlayback()", StringComparison.Ordinal) &&
        animationPauseMethod.Contains("PauseAnimationPlayerPlayback()", StringComparison.Ordinal) &&
        animationPauseMethod.Contains("PauseProceduralTweenPlayback()", StringComparison.Ordinal) &&
        animationPauseMethod.Contains("PauseDefeatedFadeTweenPlayback()", StringComparison.Ordinal) &&
        animationPauseMethod.Contains("ResumeAnimatedSpritePlayback()", StringComparison.Ordinal) &&
        animationPauseMethod.Contains("ResumeAnimationPlayerPlayback()", StringComparison.Ordinal) &&
        animationPauseMethod.Contains("ResumeProceduralTweenPlayback()", StringComparison.Ordinal) &&
        animationPauseMethod.Contains("ResumeDefeatedFadeTweenPlayback()", StringComparison.Ordinal) &&
        animation.Contains("_animatedSprite.Pause()", StringComparison.Ordinal) &&
        animation.Contains("_animationPlayer.Pause()", StringComparison.Ordinal) &&
        animation.Contains("_proceduralTween.Pause()", StringComparison.Ordinal) &&
        animation.Contains("_defeatedFadeTween.Pause()", StringComparison.Ordinal) &&
        animation.Contains("_animatedSprite.Play()", StringComparison.Ordinal) &&
        animation.Contains("_animationPlayer.Play()", StringComparison.Ordinal) &&
        animation.Contains("_proceduralTween.Play()", StringComparison.Ordinal) &&
        animation.Contains("_defeatedFadeTween.Play()", StringComparison.Ordinal),
        "unit animation pause should freeze and resume existing playback state instead of stopping or starting a new cue.");
    AssertTrue(
        !animationPauseMethod.Contains("PlayIdle()", StringComparison.Ordinal) &&
        !animationPauseMethod.Contains("PlayMove", StringComparison.Ordinal) &&
        !animationPauseMethod.Contains("Stop()", StringComparison.Ordinal),
        "unit animation pause must not return to idle, replay movement, or stop playback because that restarts cues after resume.");
    AssertTrue(
        unitRoot.Contains("CreateTimer(seconds, processAlways: false)", StringComparison.Ordinal) &&
        animation.Contains("CreateTimer(delaySeconds, processAlways: false)", StringComparison.Ordinal) &&
        animation.Contains("CreateTimer(seconds, processAlways: false)", StringComparison.Ordinal) &&
        damageReaction.Contains("CreateTimer(delaySeconds, processAlways: false)", StringComparison.Ordinal),
        "unit presentation timers should respect SceneTree pause instead of completing one-shot cues while paused.");
}

internal static void BattleUnitCommandSelectionUsesUnitOutlineShader()
{
    string unitRoot = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "BattleUnitRoot.cs"));
    string presentation = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "BattleUnitPresentationComponent.cs"));
    string normalizedPresentation = NormalizeWhitespace(presentation);

    AssertTrue(
        unitRoot.Contains("_commandSelectedEntities", StringComparison.Ordinal) &&
        unitRoot.Contains("SetCommandSelectionByEntityIds", StringComparison.Ordinal) &&
        unitRoot.Contains("SetSelected(true)", StringComparison.Ordinal) &&
        unitRoot.Contains("SetSelected(false)", StringComparison.Ordinal),
        "battle command selection should be a public unit-root presentation API that toggles selected outlines on matching entities.");
    AssertTrue(
        presentation.Contains("unit_selection_outline.gdshader", StringComparison.Ordinal) &&
        presentation.Contains("SetSelected(bool selected)", StringComparison.Ordinal),
        "command selection should reuse the authored unit selection shader instead of adding a separate hardcoded shader path.");
    AssertTrue(
        unitRoot.Contains("PlayHitOutlinePulse()", StringComparison.Ordinal) &&
        !unitRoot.Contains("SetHitOutlines(hitTargets, visible: true)", StringComparison.Ordinal),
        "hit feedback should pulse the unit outline at impact instead of holding a strong shader outline for the whole attack.");
    AssertTrue(
        presentation.Contains("public Color HitOutlineColor { get; set; } = new(1f, 0.16f, 0.08f, 0.42f);", StringComparison.Ordinal) &&
        presentation.Contains("public float HitOutlineWidth { get; set; } = 1.55f;", StringComparison.Ordinal) &&
        presentation.Contains("public double HitOutlinePulseRiseSeconds { get; set; } = 0.07;", StringComparison.Ordinal) &&
        presentation.Contains("public double HitOutlinePulseFallSeconds { get; set; } = 0.2;", StringComparison.Ordinal),
        "hit outline defaults should be weaker and shaped as a quick fade-in followed by a softer fade-out.");
    AssertTrue(
        normalizedPresentation.Contains("TweenMethod( Callable.From<float>(ApplyHitOutlinePulseIntensity), 0f, 1f,", StringComparison.Ordinal) &&
        normalizedPresentation.Contains("TweenMethod( Callable.From<float>(ApplyHitOutlinePulseIntensity), 1f, 0f,", StringComparison.Ordinal) &&
        presentation.Contains("ResolveHitOutlineColor()", StringComparison.Ordinal) &&
        presentation.Contains("ResolveHitOutlineWidth()", StringComparison.Ordinal),
        "hit outline pulse should animate shader intensity rather than toggling an immediate full-strength outline.");
}

internal static void DeploymentZonesUseDedicatedOverlayShader()
{
    string shaderPath = Path.Combine("assets", "battle", "shaders", "deployment_zone_highlight.gdshader");
    string deploymentOverlayPath = Path.Combine("src", "Presentation", "Battle", "BattleDeploymentZoneOverlay.cs");
    string overlay = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "BattleGridHighlightOverlay.cs"));
    string tileLayerRenderer = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Highlights", "BattleGridHighlightTileLayerRenderer.cs"));

    AssertTrue(File.Exists(deploymentOverlayPath), "deployment zones should have a dedicated presentation overlay.");
    AssertTrue(File.Exists(shaderPath), "deployment zones should use an authored canvas_item shader resource.");

    string deploymentOverlay = File.ReadAllText(deploymentOverlayPath);
    string shader = File.ReadAllText(shaderPath);
    AssertTrue(
        shader.Contains("shader_type canvas_item;", StringComparison.Ordinal) &&
        shader.Contains("uniform float pulse_speed", StringComparison.Ordinal) &&
        shader.Contains("uniform float pulse_strength", StringComparison.Ordinal) &&
        shader.Contains("TIME", StringComparison.Ordinal),
        "deployment zone shader should own time-based pulse for the dedicated overlay.");
    AssertTrue(
        deploymentOverlay.Contains("DeploymentZoneShaderPath", StringComparison.Ordinal) &&
        deploymentOverlay.Contains("SetZones", StringComparison.Ordinal) &&
        deploymentOverlay.Contains("DrawDeploymentZone", StringComparison.Ordinal),
        "deployment-zone overlay should own the shader binding and zone drawing.");
    AssertTrue(
        !overlay.Contains("DeploymentZoneShaderPath", StringComparison.Ordinal) &&
        !overlay.Contains("BuildDeploymentZoneMaterial", StringComparison.Ordinal) &&
        !tileLayerRenderer.Contains("resolveMaterial", StringComparison.Ordinal),
        "generic grid highlight tile layers must not own deployment-zone shader materials.");
}

internal static void SkillRangeHighlightUsesDeploymentZoneStyleRegionOverlay()
{
    string overlay = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "BattleGridHighlightOverlay.cs"));

    AssertTrue(
        overlay.Contains("AddSkillRangeDeploymentStyle", StringComparison.Ordinal) &&
        overlay.Contains("BuildBoundarySegments", StringComparison.Ordinal) &&
        overlay.Contains("SkillRangeFillColor", StringComparison.Ordinal) &&
        overlay.Contains("SkillRangeGlowWidth", StringComparison.Ordinal),
        "skill range should use the deployment-zone-style area fill, outer boundary, and glow presentation");
    AssertTrue(
        overlay.Contains("GetTileLayerDrawOrder", StringComparison.Ordinal) &&
        !overlay.Contains("BattleGridHighlightKind.Skill => BattleGridHighlightTileShape.SolidDiamond", StringComparison.Ordinal) &&
        !overlay.Contains("yield return BattleGridHighlightKind.Skill;", StringComparison.Ordinal),
        "skill range should not be rendered by the generic tile layer because that path cannot match deployment-zone boundaries");
}

internal static void SkillTargetPreviewUsesUnitFocusAndFootprintLockRing()
{
    string overlay = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "BattleGridHighlightOverlay.cs"));
    string unitRoot = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "BattleUnitRoot.cs"));
    string siteRoot = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRuntimeCommandHud.cs"));

    AssertTrue(
        overlay.Contains("AddTargetLockRing", StringComparison.Ordinal) &&
        overlay.Contains("BuildTargetLockFramePolygon", StringComparison.Ordinal) &&
        overlay.Contains("BuildHoverFramePolygon(cells)", StringComparison.Ordinal) &&
        !overlay.Contains("TargetLockVerticalScale", StringComparison.Ordinal) &&
        overlay.Contains("TargetLockRingColor", StringComparison.Ordinal) &&
        overlay.Contains("TargetLockGlowWidth", StringComparison.Ordinal),
        "skill target cells should render on the same footprint frame as unit hover rather than an offset ellipse");
    AssertTrue(
        !overlay.Contains("AddTargetPointer", StringComparison.Ordinal) &&
        !overlay.Contains("ShowTargetPointers", StringComparison.Ordinal) &&
        !overlay.Contains("yield return BattleGridHighlightKind.Target;", StringComparison.Ordinal),
        "target preview should not use the old red arrow pointers or yellow target tile layer");
    AssertTrue(
        unitRoot.Contains("SetAttackTargetPreviewByEntityId", StringComparison.Ordinal) &&
        unitRoot.Contains("ClearAttackTargetPreview", StringComparison.Ordinal) &&
        unitRoot.Contains("SetAttackTargetPreview(true)", StringComparison.Ordinal) &&
        unitRoot.Contains("SetAttackTargetPreview(false)", StringComparison.Ordinal),
        "current target focus should use the existing unit presentation target-preview outline");
    AssertTrue(
        siteRoot.Contains("_unitRoot?.SetAttackTargetPreviewByEntityId(targetActorId)", StringComparison.Ordinal) &&
        siteRoot.Contains("_unitRoot?.ClearAttackTargetPreview()", StringComparison.Ordinal) &&
        !siteRoot.Contains("_highlightOverlay?.SetTargetPointers", StringComparison.Ordinal),
        "hero skill target picking should pass the current target entity to unit focus and stop requesting pointer arrows");
}

internal static void SkillReleasePresentationUsesCastCueAndFallbackFx()
{
    string animationSet = File.ReadAllText(Path.Combine("src", "Definitions", "Battle", "Animation", "BattleUnitAnimationSet.cs"));
    string animationComponent = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "UnitAnimationComponent.cs"));
    string unitRoot = ReadBattleUnitRootSource();
    string playback = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRuntimePlayback.cs"));
    string scene = File.ReadAllText(Path.Combine("scenes", "battle", "entities", "units", "BattleUnitBase.tscn"));
    string fxComponent = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "BattleSkillCastFxComponent.cs"));
    string fxScene = File.ReadAllText(Path.Combine("scenes", "battle", "entities", "fx", "BattleSkillCastFx.tscn"));
    string impactFxComponent = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "BattleSkillImpactFxComponent.cs"));
    string impactFxScript = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "BattleSkillImpactFx.cs"));
    string impactFxScene = File.ReadAllText(Path.Combine("scenes", "battle", "entities", "fx", "BattleSkillImpactFx.tscn"));
    string impactFeedback = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Feedback", "BattleSkillImpactFeedbackPlayer.cs"));

    AssertTrue(
        animationSet.Contains("public string SkillCastAnimation { get; set; } = \"skill_cast\";", StringComparison.Ordinal) &&
        animationSet.Contains("public double TargetSkillCastSeconds { get; set; } = 1.5;", StringComparison.Ordinal) &&
        animationComponent.Contains("\"skill_cast\" => AnimationSet?.TargetSkillCastSeconds ?? 1.5", StringComparison.Ordinal),
        "unit animation resources should expose an optional authored skill-cast cue paced slightly slower than default attacks");
    AssertTrue(
        animationComponent.Contains("public bool PlaySkillCast()", StringComparison.Ordinal) &&
        animationComponent.Contains("public double ResolveSkillCastDurationSeconds()", StringComparison.Ordinal) &&
        animationComponent.Contains("private bool TryPlaySkillCastAnimation()", StringComparison.Ordinal) &&
        animationComponent.Contains("private bool HasPlayableAnimation(string animationName, string cue)", StringComparison.Ordinal) &&
        animationComponent.Contains("return PlayAttack();", StringComparison.Ordinal),
        "unit animation component should prefer authored skill-cast animation and fall back to the attack cue when the unit has no skill-cast frames");
    AssertTrue(
        scene.Contains("BattleSkillCastFxComponent.cs", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"BattleSkillCastFxComponent\" type=\"Node\" parent=\".\"]", StringComparison.Ordinal),
        "battle unit base should carry a reusable skill cast FX component");
    AssertTrue(
        scene.Contains("BattleSkillImpactFxComponent.cs", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"BattleSkillImpactFxComponent\" type=\"Node\" parent=\".\"]", StringComparison.Ordinal),
        "battle unit base should carry a reusable target-side skill impact FX component");
    AssertTrue(
        fxComponent.Contains("DefaultSkillCastFxScenePath", StringComparison.Ordinal) &&
        fxComponent.Contains("PlaySkillCastFx", StringComparison.Ordinal) &&
        fxComponent.Contains("BattleSkillCastFx.tscn", StringComparison.Ordinal),
        "skill cast FX component should load and play an authored fallback FX scene");
    AssertTrue(
        fxScene.Contains("CPUParticles2D", StringComparison.Ordinal) &&
        fxScene.Contains("BattleSkillCastFx.cs", StringComparison.Ordinal),
        "fallback skill cast FX should use an authored particle scene instead of hardcoded runtime drawing");
    AssertTrue(
        impactFxComponent.Contains("DefaultSkillImpactFxScenePath", StringComparison.Ordinal) &&
        impactFxComponent.Contains("PlaySkillImpactFx", StringComparison.Ordinal) &&
        impactFxComponent.Contains("BattleSkillImpactFx.tscn", StringComparison.Ordinal) &&
        impactFxScene.Contains("res://assets/battle/abilities/fx/duelyst/damage/fx_impact2/frames.tres", StringComparison.Ordinal) &&
        impactFxScript.Contains("AnimationFinished", StringComparison.Ordinal),
        "target-side skill impact FX should be an authored animated SpriteFrames scene that frees itself after the hit animation");
    AssertTrue(
        impactFeedback.Contains("public static void PlaySkillImpacts", StringComparison.Ordinal) &&
        impactFeedback.Contains("DamageApplied > 0", StringComparison.Ordinal) &&
        impactFeedback.Contains("damage.Target.GetComponent<BattleSkillImpactFxComponent>()?.PlaySkillImpactFx", StringComparison.Ordinal),
        "target-side skill impact feedback should play only for applied skill damage on valid targets");
    AssertTrue(
        fxScene.Contains("[node name=\"GroundEllipse\" type=\"Line2D\" parent=\".\"]", StringComparison.Ordinal) &&
        fxScene.Contains("[node name=\"InnerEllipse\" type=\"Line2D\" parent=\".\"]", StringComparison.Ordinal) &&
        fxScene.Contains("[node name=\"DissolveEllipse\" type=\"Line2D\" parent=\".\"]", StringComparison.Ordinal) &&
        fxScene.Contains("[node name=\"RisingMist\" type=\"CPUParticles2D\" parent=\".\"]", StringComparison.Ordinal),
        "fallback skill cast FX should read as a perspective ellipse magic circle with upward dissolving mist");
    AssertTrue(
        fxComponent.Contains("FxOffset", StringComparison.Ordinal) &&
        fxScene.Contains("scale = Vector2(1, 0.42)", StringComparison.Ordinal) &&
        !fxScene.Contains("[node name=\"ReleaseShockwave\"", StringComparison.Ordinal),
        "skill cast FX should use a flattened ellipse footprint instead of an angular full-circle shockwave");
    AssertTrue(
        fxComponent.Contains("PlaySkillCastFx", StringComparison.Ordinal) &&
        fxScene.Contains("RisingMist", StringComparison.Ordinal) &&
        fxScene.Contains("gravity = Vector2(0, -", StringComparison.Ordinal),
        "spell circle particles should rise vertically from the ground instead of bursting like sparks");
    AssertTrue(
        fxScene.Contains("[node name=\"GlyphNorth\" type=\"Line2D\" parent=\".\"]", StringComparison.Ordinal) &&
        fxScene.Contains("[node name=\"GlyphSouth\" type=\"Line2D\" parent=\".\"]", StringComparison.Ordinal),
        "fallback skill cast FX should include simple authored glyph strokes so the ellipse reads as a spell array");
    AssertTrue(
        fxComponent.Contains("BattleSkillCastFx.tscn", StringComparison.Ordinal) &&
        animationComponent.Contains("new Color(1f, 0.86f, 0.32f, 1f)", StringComparison.Ordinal) &&
        File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "BattleSkillCastFx.cs")).Contains("BuildEllipsePoints", StringComparison.Ordinal) &&
        File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "BattleSkillCastFx.cs")).Contains("DissolveRiseOffset", StringComparison.Ordinal),
        "skill cast FX script should generate smooth ellipse points and tween the magic circle upward while fading");
    AssertTrue(
        animationComponent.Contains("new Vector2(1.22f, 1.22f)", StringComparison.Ordinal) &&
        animationComponent.Contains("new Color(1f, 0.86f, 0.32f, 1f)", StringComparison.Ordinal),
        "the specialized skill-cast body cue should stay visibly stronger than basic hit feedback when it is explicitly used");
    string skillCastFallback = animationComponent[
        animationComponent.IndexOf("case \"skill_cast\":", StringComparison.Ordinal)..
        animationComponent.IndexOf("case \"hit\":", StringComparison.Ordinal)];
    AssertTrue(
        !skillCastFallback.Contains("\"position\"", StringComparison.Ordinal),
        "procedural skill cast fallback should keep the unit anchored; release motion belongs to the FX, not the unit body");
    AssertTrue(
        unitRoot.Contains("public double PlaySkillCastPresentation(", StringComparison.Ordinal) &&
        unitRoot.Contains("StopEntityMovement(actor, snapToLogicalGrid: true)", StringComparison.Ordinal) &&
        unitRoot.Contains("actorAnimation?.PlaySkillCast()", StringComparison.Ordinal) &&
        unitRoot.Contains("actor.GetComponent<BattleSkillCastFxComponent>()?.PlaySkillCastFx", StringComparison.Ordinal) &&
        unitRoot.Contains("public double PlayRuntimeDamageFeedback(", StringComparison.Ordinal) &&
        unitRoot.Contains("BattleSkillImpactFeedbackPlayer.PlaySkillImpacts(damageEvents, playSkillImpactFx)", StringComparison.Ordinal) &&
        unitRoot.Contains("actorAnimation?.PlayAttack()", StringComparison.Ordinal),
        "SkillUsed presentation should stop caster movement before playing skill cast animation and caster FX while damage events keep target impact FX");
    AssertTrue(
        unitRoot.Contains("private void StopEntityMovement(BattleEntity entity, bool snapToLogicalGrid)", StringComparison.Ordinal) &&
        unitRoot.Contains("_movementLanes.Remove(entity)", StringComparison.Ordinal) &&
        unitRoot.Contains("_pendingMovementIdleSeconds.Remove(entity)", StringComparison.Ordinal) &&
        unitRoot.Contains("TryResolveMovementGlobalPosition(gridOccupant, gridOccupant.SurfacePosition", StringComparison.Ordinal),
        "movement lane cancellation should clear queued movement and optionally sync the unit visual to its authoritative grid position");
    AssertTrue(
        playback.Contains("ObserveRuntimeSkillUsedEventAsync", StringComparison.Ordinal) &&
        playback.Contains("_unitRoot.PlaySkillCastPresentation", StringComparison.Ordinal) &&
        playback.Contains("PlayRuntimeDamageFeedback", StringComparison.Ordinal) &&
        playback.Contains("IsRuntimeSkillDamageEvent(runtimeEvent)", StringComparison.Ordinal) &&
        !playback.Contains("BattleActionResult.AbilitySucceeded", StringComparison.Ordinal),
        "runtime playback should let SkillUsed own caster presentation while skill damage only plays target-side impact feedback");
}

internal static void RealtimeDamageReactionDoesNotPlayHitAnimation()
{
    string damageReaction = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "DamageReactionComponent.cs"));
    string animationComponent = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "UnitAnimationComponent.cs"));

    AssertTrue(
        !damageReaction.Contains(".PlayHit(", StringComparison.Ordinal),
        "realtime damage reactions should not invoke the hit animation at the call layer");
    AssertTrue(
        damageReaction.Contains("PlayCue(BattleUnitAudioCue.Hit)", StringComparison.Ordinal),
        "damage feedback may keep hit audio after removing the hit animation call");
    AssertTrue(
        animationComponent.Contains("public bool PlayHit(double minimumDurationSeconds = 0)", StringComparison.Ordinal),
        "hit animation resources and preview API should remain available even when runtime damage no longer calls them");
}

internal static void RuntimeImpactDamageDoesNotDoubleDelayDefeatedPresentation()
{
    string playback = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRuntimePlayback.cs"));
    string damageReaction = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "DamageReactionComponent.cs"));

    AssertTrue(
        playback.Contains("BeginImpactAlignedDamageTiming()", StringComparison.Ordinal) &&
        playback.Contains("EndImpactAlignedDamageTiming()", StringComparison.Ordinal) &&
        playback.IndexOf("BeginImpactAlignedDamageTiming()", StringComparison.Ordinal) <
        playback.IndexOf("health.ApplyDamage(damage, actor)", StringComparison.Ordinal) &&
        playback.IndexOf("health.ApplyDamage(damage, actor)", StringComparison.Ordinal) <
        playback.IndexOf("EndImpactAlignedDamageTiming()", StringComparison.Ordinal),
        "runtime playback should mark visible damage as already impact-aligned before applying health changes");
    AssertTrue(
        damageReaction.Contains("public void BeginImpactAlignedDamageTiming()", StringComparison.Ordinal) &&
        damageReaction.Contains("public void EndImpactAlignedDamageTiming()", StringComparison.Ordinal) &&
        damageReaction.Contains("if (_impactAlignedDamageTimingDepth > 0)", StringComparison.Ordinal) &&
        damageReaction.Contains("return 0;", StringComparison.Ordinal),
        "damage reaction should skip source attack delay for damage that was already applied at the impact frame");
    AssertTrue(
        damageReaction.Contains("_pendingDefeatedMinimumDurationSeconds = ResolveMinimumDefeatedDurationSeconds(damage);", StringComparison.Ordinal),
        "defeated presentation should still keep the minimum death duration derived from the source attack animation");
}

internal static void UnitCombatStatsSnapshotContract()
{
    string forceRequest = File.ReadAllText(Path.Combine("src", "Application", "Battle", "BattleForceRequest.cs"));
    string snapshot = File.ReadAllText(Path.Combine("src", "Application", "Battle", "Snapshots", "BattleGroupSnapshot.cs"));
    string probe = File.ReadAllText(Path.Combine("src", "Application", "Battle", "BattleGroupSessionProbeService.cs"));
    string runtimeActor = File.ReadAllText(Path.Combine("src", "Runtime", "Battle", "BattleRuntimeActor.cs"));
    string runtimeSession = File.ReadAllText(Path.Combine("src", "Runtime", "Battle", "BattleRuntimeSession.cs"));
    string siteRuntime = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRequestDeployment.cs"));

    AssertTrue(
        forceRequest.Contains("public int MaxHitPoints { get; set; }", StringComparison.Ordinal) &&
        forceRequest.Contains("public int AttackDamage { get; set; }", StringComparison.Ordinal) &&
        forceRequest.Contains("public int AttackRange { get; set; }", StringComparison.Ordinal),
        "battle force requests should carry combat stats copied from unit definitions");
    AssertTrue(
        snapshot.Contains("public int MaxHitPoints { get; set; }", StringComparison.Ordinal) &&
        snapshot.Contains("public int AttackDamage { get; set; }", StringComparison.Ordinal) &&
        snapshot.Contains("public int AttackRange { get; set; }", StringComparison.Ordinal),
        "battle snapshots should freeze combat stats for runtime");
    AssertTrue(
        siteRuntime.Contains("force.MaxHitPoints = definition.MaxHp;", StringComparison.Ordinal) &&
        siteRuntime.Contains("force.AttackDamage = definition.AttackDamage;", StringComparison.Ordinal) &&
        siteRuntime.Contains("force.AttackRange = definition.AttackRange;", StringComparison.Ordinal),
        "site battle runtime should copy definition hp, damage, and range into the request before probing");
    AssertTrue(
        probe.Contains("MaxHitPoints = force.MaxHitPoints", StringComparison.Ordinal) &&
        probe.Contains("AttackDamage = force.AttackDamage", StringComparison.Ordinal) &&
        probe.Contains("AttackRange = force.AttackRange", StringComparison.Ordinal),
        "battle group probe should copy request combat stats into snapshot metadata");
    AssertTrue(
        runtimeActor.Contains("public int AttackDamage { get; set; }", StringComparison.Ordinal) &&
        runtimeSession.Contains("HitPoints = ResolveCombatHitPoints(group)", StringComparison.Ordinal) &&
        runtimeSession.Contains("AttackDamage = ResolveAttackDamage(group.AttackDamage)", StringComparison.Ordinal),
        "runtime actors should use snapshot combat hp and attack damage instead of hardcoded combat values");
}

internal static void BattleUnitBaseSceneAuthorsHealthBarAndFallbackAnimation()
{
    string scene = File.ReadAllText(Path.Combine("scenes", "battle", "entities", "units", "BattleUnitBase.tscn"));
    string healthBar = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "BattleUnitHealthBarComponent.cs"));
    string overlayAnchor = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "BattleUnitOverlayAnchorComponent.cs"));
    string presentation = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "BattleUnitPresentationComponent.cs"));

    AssertTrue(
        scene.Contains("BattleUnitOverlayAnchorComponent.cs", StringComparison.Ordinal) &&
        scene.Contains("BattleUnitHealthBarComponent.cs", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"HealthBarRoot\" type=\"Control\" parent=\".\"]", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"HealthBack\" type=\"ColorRect\" parent=\"HealthBarRoot\"]", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"HealthTrack\" type=\"ColorRect\" parent=\"HealthBarRoot\"]", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"HealthFill\" type=\"ColorRect\" parent=\"HealthBarRoot\"]", StringComparison.Ordinal),
        "battle unit base scene should author a reusable overlay anchor and visible health bar resource tree");
    AssertTrue(
        scene.Contains("EnableProceduralFallback = true", StringComparison.Ordinal),
        "battle unit base should keep a procedural attack fallback when a visual resource lacks configured animations");
    AssertTrue(
        healthBar.Contains("HealthChanged", StringComparison.Ordinal) &&
        healthBar.Contains("QueueRedraw", StringComparison.Ordinal),
        "health bar component should update from health events instead of polling every frame");
    AssertTrue(
        healthBar.Contains("public NodePath BackPath { get; set; } = new(\"../HealthBarRoot/HealthBack\");", StringComparison.Ordinal) &&
        healthBar.Contains("public NodePath TrackPath { get; set; } = new(\"../HealthBarRoot/HealthTrack\");", StringComparison.Ordinal),
        "health bar component should bind the authored frame, track, and fill resources instead of drawing an ad hoc bar");
    AssertTrue(
        healthBar.Contains("public Vector2 BarSize { get; set; } = new(36f, 5f);", StringComparison.Ordinal) &&
        !healthBar.Contains("BarOffset", StringComparison.Ordinal) &&
        !scene.Contains("offset_left = -18.0", StringComparison.Ordinal),
        "default health bar size should stay compact while placement comes from the reusable overlay anchor instead of a fixed offset");
    AssertTrue(
        overlayAnchor.Contains("public NodePath GridOccupantPath { get; set; } = new(\"../GridOccupantComponent\");", StringComparison.Ordinal) &&
        overlayAnchor.Contains("public NodePath VisualRootPath { get; set; } = new(\"../VisualRoot\");", StringComparison.Ordinal) &&
        overlayAnchor.Contains("public NodePath SpritePath { get; set; } = new(\"../VisualRoot/AnimatedSprite2D\");", StringComparison.Ordinal) &&
        overlayAnchor.Contains("ResolveFootprintTopY", StringComparison.Ordinal) &&
        overlayAnchor.Contains("ResolveVisualTopY", StringComparison.Ordinal),
        "unit overlay anchors should resolve head UI placement from footprint occupancy and visual bounds");
    AssertTrue(
        healthBar.Contains("BattleUnitOverlayAnchorComponent", StringComparison.Ordinal) &&
        healthBar.Contains("ResolveHeadOverlayPosition(BarSize)", StringComparison.Ordinal),
        "health bar placement should delegate to the generic unit overlay anchor");
    AssertTrue(
        healthBar.Contains("public bool HideWhenFullHp { get; set; } = true;", StringComparison.Ordinal) &&
        healthBar.Contains("public double DamagedVisibleSeconds { get; set; } = 2.4;", StringComparison.Ordinal) &&
        healthBar.Contains("public float LowHpVisibleRatio { get; set; } = 0.35f;", StringComparison.Ordinal) &&
        healthBar.Contains("public float HighHpAlpha { get; set; } = 0.45f;", StringComparison.Ordinal) &&
        healthBar.Contains("public float LowHpAlpha { get; set; } = 1f;", StringComparison.Ordinal) &&
        healthBar.Contains("public void SetAttentionVisible(bool visible)", StringComparison.Ordinal) &&
        healthBar.Contains("_attentionVisible", StringComparison.Ordinal) &&
        healthBar.Contains("ShouldShowHealthBar(maxHp, ratio)", StringComparison.Ordinal) &&
        healthBar.Contains("_damageRevealSecondsRemaining", StringComparison.Ordinal),
        "health bar should stay hidden at full HP, reveal for a longer post-damage window, and remain visible for low HP or attention-focused units");
    AssertTrue(
        healthBar.Contains("ResolveHpAlpha(ratio)", StringComparison.Ordinal) &&
        healthBar.Contains("Mathf.Lerp", StringComparison.Ordinal) &&
        healthBar.Contains("HighHpAlpha", StringComparison.Ordinal) &&
        healthBar.Contains("LowHpAlpha", StringComparison.Ordinal),
        "health bar alpha should scale by HP so high-health bars are quieter and low-health bars are more solid");
    AssertTrue(
        presentation.Contains("BattleUnitHealthBarComponent", StringComparison.Ordinal) &&
        presentation.Contains("ApplyHealthBarAttention()", StringComparison.Ordinal) &&
        presentation.Contains("_healthBar?.SetAttentionVisible(_selected || _targetPreviewed || _previewFocused);", StringComparison.Ordinal),
        "selection, target preview, and hover-style focus should ask the health bar to show when the unit is not full HP");
    AssertTrue(
        healthBar.Contains("public Color BorderColor", StringComparison.Ordinal) &&
        healthBar.Contains("public Color TrackColor", StringComparison.Ordinal) &&
        healthBar.Contains("public Color HighHpColor", StringComparison.Ordinal) &&
        healthBar.Contains("public Color MidHpColor", StringComparison.Ordinal) &&
        healthBar.Contains("public Color LowHpColor", StringComparison.Ordinal) &&
        healthBar.Contains("_back.Color = BorderColor;", StringComparison.Ordinal) &&
        healthBar.Contains("_track.Color = TrackColor;", StringComparison.Ordinal) &&
        healthBar.Contains("_fill.Color = ResolveFillColor(ratio);", StringComparison.Ordinal),
        "health bar visuals should be resourceized and use muted configurable colors");
    AssertTrue(
        healthBar.Contains("ratio <= 0.25f", StringComparison.Ordinal) &&
        healthBar.Contains("ratio <= 0.55f", StringComparison.Ordinal),
        "health bar fill should shift from green to amber to red as HP falls");
}

internal static void DefeatedUnitPresentationHidesHealthBarBeforeFastDeathAnimation()
{
    string healthBar = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "BattleUnitHealthBarComponent.cs"));
    string unitRoot = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "BattleUnitRoot.cs"));
    string animationSet = File.ReadAllText(Path.Combine("src", "Definitions", "Battle", "Animation", "BattleUnitAnimationSet.cs"));
    string animationComponent = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "UnitAnimationComponent.cs"));
    string markDefeatedBody = ExtractMethodBlock(unitRoot, "public void MarkEntityDefeated(");
    int hideHealthBarIndex = markDefeatedBody.IndexOf("HideHealthBarImmediately", StringComparison.Ordinal);
    int playDefeatedIndex = markDefeatedBody.IndexOf("animation.PlayDefeated", StringComparison.Ordinal);

    AssertTrue(
        healthBar.Contains("if (maxHp <= 1 || _health.IsDead)", StringComparison.Ordinal) &&
        healthBar.Contains("return false;", StringComparison.Ordinal) &&
        healthBar.Contains("_root.Visible = ShouldShowHealthBar(maxHp, ratio);", StringComparison.Ordinal),
        "health bar should disappear on the HealthChanged frame that drops HP to zero, before defeated animation starts");
    AssertTrue(
        healthBar.Contains("public void HideImmediately()", StringComparison.Ordinal) &&
        healthBar.Contains("_damageRevealSecondsRemaining = 0;", StringComparison.Ordinal) &&
        healthBar.Contains("_attentionVisible = false;", StringComparison.Ordinal),
        "health bar should expose an immediate hide path that clears damage reveal and attention state");
    AssertTrue(
        hideHealthBarIndex >= 0 &&
        playDefeatedIndex >= 0 &&
        hideHealthBarIndex < playDefeatedIndex,
        "defeated presentation should hide the health bar before starting or delaying the defeated animation");
    AssertTrue(
        animationSet.Contains("public double TargetDefeatedSeconds { get; set; } = 0.4;", StringComparison.Ordinal) &&
        animationComponent.Contains("\"defeated\" => AnimationSet?.TargetDefeatedSeconds ?? 0.4", StringComparison.Ordinal),
        "default defeated animation target should be twice as fast as the old 0.8 second target");
    AssertTrue(
        animationSet.Contains("public double DefeatedMinimumAttackDurationRatio { get; set; } = 0.2;", StringComparison.Ordinal) &&
        animationComponent.Contains("AnimationSet?.DefeatedMinimumAttackDurationRatio ?? 0.2", StringComparison.Ordinal),
        "defeated animation minimum duration should scale with the faster death cue");
    AssertTrue(
        animationSet.Contains("public double DefeatedFallbackSeconds { get; set; } = 0.35;", StringComparison.Ordinal) &&
        animationComponent.Contains("AnimationSet?.DefeatedFallbackSeconds ?? 0.35", StringComparison.Ordinal),
        "defeated fallback completion should not keep dead units visible after the faster death cue");
    AssertTrue(
        animationSet.Contains("public double DefeatedFadeFastSeconds { get; set; } = 0.12;", StringComparison.Ordinal) &&
        animationSet.Contains("public float DefeatedFadeMidAlpha { get; set; } = 0.38f;", StringComparison.Ordinal) &&
        animationSet.Contains("public float DefeatedFadeEndAlpha { get; set; } = 0.05f;", StringComparison.Ordinal),
        "defeated fade timing should be resourceized so the opening fade can reach semi-transparent quickly");
    AssertTrue(
        animationComponent.Contains("PlayDefeatedFade(ResolveDefeatedFadeSeconds(authoredSeconds));", StringComparison.Ordinal) &&
        animationComponent.Contains("_defeatedFadeTween.SetTrans(Tween.TransitionType.Quad);", StringComparison.Ordinal) &&
        animationComponent.Contains("_defeatedFadeTween.SetEase(Tween.EaseType.Out);", StringComparison.Ordinal) &&
        animationComponent.Contains("ResolveDefeatedFadeMidColor()", StringComparison.Ordinal) &&
        animationComponent.Contains("ResolveDefeatedFadeEndColor()", StringComparison.Ordinal),
        "all defeated animation paths should layer a front-loaded visual fade over the death cue");
    AssertTrue(
        animationComponent.Contains("KillDefeatedFadeTween(resetModulate: true);", StringComparison.Ordinal) &&
        animationComponent.Contains("KillDefeatedFadeTween(resetModulate: false);", StringComparison.Ordinal),
        "defeated fade tweens should reset only when returning to live cues and stay untouched when the entity is exiting");
}

internal static void BattleUnitBaseSceneAvoidsPhysicsInteractionShape()
{
    string scene = File.ReadAllText(Path.Combine("scenes", "battle", "entities", "units", "BattleUnitBase.tscn"));
    string entity = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "BattleEntity.cs"));

    AssertTrue(
        scene.Contains("[node name=\"BattleEntity\" type=\"Node2D\"]", StringComparison.Ordinal),
        "battle unit base should use a plain Node2D root instead of an Area2D interaction body");
    AssertTrue(
        !scene.Contains("CircleShape2D", StringComparison.Ordinal) &&
        !scene.Contains("InteractionShape", StringComparison.Ordinal) &&
        !scene.Contains("CollisionShape2D", StringComparison.Ordinal),
        "battle unit base should not author physics collision shapes for RTS movement or selection");
    AssertTrue(
        entity.Contains("public partial class BattleEntity : Node2D", StringComparison.Ordinal) &&
        !entity.Contains("InputPickable", StringComparison.Ordinal) &&
        !entity.Contains("_InputEvent", StringComparison.Ordinal),
        "battle entity presentation should not depend on Area2D pick callbacks after grid footprint selection owns interaction");
}

internal static void BattleUnitPreviewWorkbenchIsVisualResourceMirror()
{
    string scriptPath = Path.Combine("src", "Presentation", "Battle", "Preview", "BattleUnitPreviewWorkbench.cs");
    string scenePath = Path.Combine("scenes", "tools", "battle", "UnitPreviewWorkbench.tscn");
    string siteScenePath = Path.Combine("scenes", "world", "sites", "impl", "BonefieldSite.tscn");
    string worldSiteRootScenePath = Path.Combine("scenes", "world", "sites", "WorldSiteRoot.tscn");
    string factoryPath = Path.Combine("src", "Presentation", "Battle", "Entities", "BattleUnitFactory.cs");
    string layoutCalculatorPath = Path.Combine("src", "Presentation", "Battle", "Entities", "BattleUnitVisualLayoutCalculator.cs");

    AssertTrue(File.Exists(scriptPath), "unit preview workbench script should exist");
    AssertTrue(File.Exists(scenePath), "unit preview workbench scene should exist");
    AssertTrue(File.Exists(layoutCalculatorPath), "unit visual layout calculator should exist as the shared presentation layout authority");

    string script = File.ReadAllText(scriptPath);
    string scene = File.ReadAllText(scenePath);
    string siteScene = File.ReadAllText(siteScenePath);
    string worldSiteRootScene = File.ReadAllText(worldSiteRootScenePath);
    string factory = File.ReadAllText(factoryPath);
    string layoutCalculator = File.ReadAllText(layoutCalculatorPath);

    AssertTrue(script.Contains("[Tool]", StringComparison.Ordinal), "workbench should run in the Godot editor");
    AssertTrue(
        script.Contains("SetProcess(false)", StringComparison.Ordinal) &&
        script.Contains("!Engine.IsEditorHint()", StringComparison.Ordinal),
        "workbench should disable itself outside the editor so it cannot add runtime polling");
    AssertTrue(
        !script.Contains("public SpriteFrames SpriteFrames", StringComparison.Ordinal) &&
        script.Contains("ResolvePreviewSpriteFrames()", StringComparison.Ordinal) &&
        script.Contains("_previewSprite.SpriteFrames", StringComparison.Ordinal),
        "preview scene should use its child AnimatedSprite2D SpriteFrames as the direct unit-like authoring entry");
    AssertTrue(
        script.Contains("public BattleUnitPreviewAnimationNameSet AnimationNameSet", StringComparison.Ordinal) &&
        script.Contains("BattleUnitPreviewAnimationNameSet.StandardDuelyst", StringComparison.Ordinal),
        "workbench should expose an enum animation-name preset instead of requiring authors to drag an animation set resource");
    AssertTrue(
        script.Contains("public bool AutoLayoutFromSpriteFrames", StringComparison.Ordinal) &&
        script.Contains("public float TargetMaxSpriteSizePixels", StringComparison.Ordinal) &&
        script.Contains("public float GroundAnchorOffsetPixels", StringComparison.Ordinal) &&
        script.Contains("public float VisibleAlphaThreshold", StringComparison.Ordinal) &&
        script.Contains("public Vector2 Offset", StringComparison.Ordinal) &&
        script.Contains("public Vector2 ManualScale", StringComparison.Ordinal) &&
        script.Contains("public Color PreviewModulate", StringComparison.Ordinal),
        "workbench should mirror BattleUnitVisualDefinition layout fields instead of the full unit definition");
    AssertTrue(
        script.Contains("public int FootprintWidth", StringComparison.Ordinal) &&
        script.Contains("public int FootprintHeight", StringComparison.Ordinal),
        "workbench should keep footprint width and height as preview-only scale aids");
    AssertTrue(
        script.Contains("PreviewAnimation", StringComparison.Ordinal) &&
        script.Contains("PlayPreviewAnimation", StringComparison.Ordinal),
        "workbench should let authors switch authored unit animations without entering battle");
    AssertTrue(
        script.Contains("BattleUnitVisualLayoutCalculator", StringComparison.Ordinal),
        "workbench should share the same visual layout calculator as runtime presentation");
    AssertTrue(
        !script.Contains("UnitDefinitionResource", StringComparison.Ordinal) &&
        !script.Contains("UnitDefinitionPath", StringComparison.Ordinal) &&
        !script.Contains("BattleUnitAnimationSet", StringComparison.Ordinal) &&
        !script.Contains("BattleUnitFactory", StringComparison.Ordinal) &&
        !script.Contains("CreatePreview", StringComparison.Ordinal) &&
        !script.Contains("_previewSprite.SpriteFrames = null", StringComparison.Ordinal) &&
        !script.Contains("_previewSprite.SpriteFrames = SpriteFrames", StringComparison.Ordinal),
        "preview scene should not load unit.tres, animation-set resources, the full runtime battle unit scene, or clear child sprite frames");
    AssertTrue(
        factory.Contains("BattleUnitVisualLayoutCalculator.TryCalculateAutoLayout", StringComparison.Ordinal),
        "runtime unit factory should use the shared visual layout calculator");
    AssertTrue(
        layoutCalculator.Contains("public static bool TryCalculateAutoLayout", StringComparison.Ordinal) &&
        layoutCalculator.Contains("TryGetVisibleTextureBounds", StringComparison.Ordinal),
        "layout calculator should own visible-pixel sprite bounds and auto-layout math");
    AssertTrue(
        scene.Contains("res://src/Presentation/Battle/Preview/BattleUnitPreviewWorkbench.cs", StringComparison.Ordinal),
        "workbench scene should bind the preview script");
    AssertTrue(
        scene.Contains("[node name=\"AnimatedSprite2D\" type=\"AnimatedSprite2D\" parent=\"PreviewRoot\"]", StringComparison.Ordinal) &&
        !scene.Contains("visible = false", StringComparison.Ordinal) &&
        scene.Contains("PreviewRoot", StringComparison.Ordinal) &&
        scene.Contains("FootprintOverlay", StringComparison.Ordinal),
        "workbench scene should author a visible AnimatedSprite2D preview unit, preview root, and footprint overlay instead of constructing preview nodes from unit.tres");
    AssertTrue(
        scene.Contains("AnimatedSprite2D", StringComparison.Ordinal) &&
        scene.Contains("frames.tres", StringComparison.Ordinal) &&
        !scene.Contains("UnitDefinitionResource", StringComparison.Ordinal) &&
        !scene.Contains("UnitDefinitionPath", StringComparison.Ordinal),
        "workbench scene text should direct authors to put frames.tres on the child AnimatedSprite2D rather than unit.tres");
    AssertTrue(
        siteScene.Contains("res://scenes/tools/battle/UnitPreviewWorkbench.tscn", StringComparison.Ordinal) &&
        siteScene.Contains("[node name=\"UnitPreviewWorkbench\" parent=\".\" instance=", StringComparison.Ordinal),
        "the current concrete site map should carry the default unit preview workbench for scale debugging");
    AssertTrue(
        siteScene.Contains("metadata/authoring_hint = \"editor_unit_preview\"", StringComparison.Ordinal),
        "embedded field preview should be explicitly marked as editor authoring support");
    AssertTrue(
        !worldSiteRootScene.Contains("res://scenes/tools/battle/UnitPreviewWorkbench.tscn", StringComparison.Ordinal),
        "WorldSiteRoot is the site runtime shell and should not own the default authoring workbench");
}

internal static void StarterUnitDefinitionsReferenceAudioProfiles()
{
    Dictionary<string, string> unitDirs = new()
    {
        ["f1_shieldforger"] = Path.Combine("assets", "battle", "units", "莱昂纳王国", "f1_盾牌铸造者"),
        ["f1_scintilla"] = Path.Combine("assets", "battle", "units", "莱昂纳王国", "f1_闪烁")
    };

    foreach ((string unitId, string unitDir) in unitDirs)
    {
        string unitPath = Path.Combine(unitDir, "unit.tres");
        string resourceDir = unitDir.Replace(Path.DirectorySeparatorChar, '/');
        string audioPath = $"res://{resourceDir}/audio/audio.tres";
        string text = File.ReadAllText(unitPath);
        AssertTrue(text.Contains(audioPath, StringComparison.Ordinal), $"{unitId} should reference its audio profile");

        string audioText = File.ReadAllText(Path.Combine(unitDir, "audio", "audio.tres"));
        foreach (string cue in new[] { "deploy", "move", "attack", "attack_impact", "hit", "defeated" })
        {
            AssertTrue(
                audioText.Contains($"{audioPath[..^"audio.tres".Length]}{cue}.ogg", StringComparison.Ordinal),
                $"{unitId} audio profile should include {cue}");
        }
    }
}

internal static void LegacyStarterUnitPackagesAreRemoved()
{
    string[] legacyUnitIds = { "player_knight", "militia", "skeleton_warrior", "skeleton_archer" };

    foreach (string unitId in legacyUnitIds)
    {
        string legacyDir = Path.Combine("assets", "battle", "units", "neutral", $"legacy_{unitId}");
        AssertTrue(!Directory.Exists(legacyDir), $"{unitId} legacy unit package should be removed after migrating references");
    }

    string idsSource = File.ReadAllText(Path.Combine("src", "Application", "World", "StrategicWorldIds.cs"));
    foreach (string oldId in legacyUnitIds)
    {
        AssertTrue(
            !idsSource.Contains($"= \"{oldId}\"", StringComparison.Ordinal),
            $"StrategicWorldIds should not keep the removed battle unit id {oldId}");
    }
}

internal static void StarterAudioMigrationUsesSourceVisuals()
{
    string report = File.ReadAllText(Path.Combine("assets", "audio", "sfx", "duelyst_audio_migration_a.json"));

    AssertTrue(
        report.Contains("actual SpriteFrames source visual", StringComparison.Ordinal),
        "migration report should document source-visual mapping rule");
    AssertTrue(
        report.Contains("f1_shieldforger.png / RSX.f1Surgeforger*", StringComparison.Ordinal),
        "shieldforger-backed units should map from f1Surgeforger visual identity");
    AssertTrue(
        report.Contains("f1_scintilla.png / RSX.f1Scintilla*", StringComparison.Ordinal),
        "scintilla-backed units should map from f1Scintilla visual identity");
    AssertTrue(
        report.Contains("app/sdk/cards/factory/wartech/faction1.coffee:291-307", StringComparison.Ordinal),
        "surgeforger mapping should cite original Duelyst card factory sound block");
    AssertTrue(
        report.Contains("app/sdk/cards/factory/bloodstorm/faction1.coffee:100-116", StringComparison.Ordinal),
        "scintilla mapping should cite original Duelyst card factory sound block");
}

internal static void BattleUnitDisplayNamesUseIndexedResourceLabel()
{
    AssertEqual("盾牌铸造者01", BattleUnitDisplayNameFormatter.FormatInstanceName("盾牌铸造者", 0), "first visible unit should use 01 suffix");
    AssertEqual("盾牌铸造者02", BattleUnitDisplayNameFormatter.FormatInstanceName("盾牌铸造者", 1), "second visible unit should use 02 suffix");
    AssertEqual("战斗单位03", BattleUnitDisplayNameFormatter.FormatInstanceName("", 2), "missing display names should use a readable fallback");
}

internal static void StarterUnitDisplayNamesUseSourceVisualTranslations()
{
    Dictionary<string, (string UnitDir, string ExpectedName)> expectedNames = new()
    {
        ["f1_shieldforger"] = (Path.Combine("assets", "battle", "units", "莱昂纳王国", "f1_盾牌铸造者"), "盾牌铸造者"),
        ["f1_scintilla"] = (Path.Combine("assets", "battle", "units", "莱昂纳王国", "f1_闪烁"), "闪烁术士")
    };

    foreach ((string unitId, (string unitDir, string expectedName)) in expectedNames)
    {
        string unitPath = Path.Combine(unitDir, "unit.tres");
        string text = File.ReadAllText(unitPath);
        AssertTrue(
            text.Contains($"DisplayName = \"{expectedName}\"", StringComparison.Ordinal),
            $"{unitId} should use source visual translation {expectedName}");
    }
}

internal static void WorldUnitLabelsResolveThroughBattleDefinitions()
{
    string strategicRoot = ReadStrategicWorldRootSource();
    string siteRoot = ReadWorldSiteRootSource();

    AssertTrue(
        strategicRoot.Contains("_battleUnitFactory.ResolveUnitDisplayName(unitTypeId)", StringComparison.Ordinal),
        "strategic world UI should resolve unit group labels from battle unit definitions");
    AssertTrue(
        siteRoot.Contains("_battleUnitFactory.ResolveUnitDisplayName(unitTypeId)", StringComparison.Ordinal),
        "site detail UI should resolve garrison labels from battle unit definitions");
    AssertTrue(
        siteRoot.Contains("_battleUnitFactory.ResolveUnitInstanceDisplayName(placement.UnitTypeId", StringComparison.Ordinal),
        "site placement labels should use the same indexed instance names as battle units");
}
}
