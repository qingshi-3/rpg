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
        "target cells should remain available for arrow presentation");
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
    string source = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRuntime.cs"));

    AssertTrue(
        source.Contains("ApplyBattleRequestForceFootprints(request);", StringComparison.Ordinal),
        "site battle runtime should enrich battle requests with definition footprints before probe");
    AssertTrue(
        source.Contains("force.FootprintWidth = definition.FootprintWidth;", StringComparison.Ordinal) &&
        source.Contains("force.FootprintHeight = definition.FootprintHeight;", StringComparison.Ordinal),
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

internal static void BattleRuntimePlaybackKeepsMoveLoopAcrossRuntimeSteps()
{
    string source = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRuntimePlayback.cs"));
    string runtime = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRuntime.cs"));

    AssertTrue(
        source.Contains("restartMoveAnimation: false", StringComparison.Ordinal) &&
        source.Contains("returnToIdleOnComplete: false", StringComparison.Ordinal),
        "runtime movement playback should keep the move loop alive across adjacent step events instead of restarting and returning to idle per cell");
    AssertTrue(
        runtime.Contains("_unitRoot.PlayIdleForActiveEntities();", StringComparison.Ordinal),
        "runtime playback should return surviving units to idle after the event stream finishes");
}

internal static void BattleRuntimePlaybackWaitsForAttackAnimationDuration()
{
    string playback = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRuntimePlayback.cs"));
    string unitRoot = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "BattleUnitRoot.cs"));

    AssertTrue(
        unitRoot.Contains("public double PlayActionResultAnimation(BattleActionResult result)", StringComparison.Ordinal) &&
        unitRoot.Contains("ResolveAttackDurationSeconds()", StringComparison.Ordinal),
        "battle action playback should expose the resolved attack animation duration to the runtime event player");
    AssertTrue(
        playback.Contains("double attackAnimationSeconds = _unitRoot.PlayActionResultAnimation", StringComparison.Ordinal) &&
        playback.Contains("WaitSiteBattlePresentationSeconds(System.Math.Max(0.42, attackAnimationSeconds))", StringComparison.Ordinal),
        "runtime damage playback should wait for the full attack animation instead of advancing after a fixed short delay");
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

internal static void UnitAttackSpeedContract()
{
    string definition = File.ReadAllText(Path.Combine("src", "Definitions", "Battle", "BattleUnitDefinition.cs"));
    string forceRequest = File.ReadAllText(Path.Combine("src", "Application", "Battle", "BattleForceRequest.cs"));
    string snapshot = File.ReadAllText(Path.Combine("src", "Application", "Battle", "Snapshots", "BattleGroupSnapshot.cs"));
    string runtimeActor = File.ReadAllText(Path.Combine("src", "Runtime", "Battle", "BattleRuntimeActor.cs"));
    string runtimeSession = File.ReadAllText(Path.Combine("src", "Runtime", "Battle", "BattleRuntimeSession.cs"));
    string unitFactory = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "BattleUnitFactory.cs"));
    string animationComponent = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "UnitAnimationComponent.cs"));

    AssertTrue(
        definition.Contains("public double AttackSpeed { get; set; } = 1.0;", StringComparison.Ordinal),
        "battle unit definitions should expose attack speed with a stable default");
    AssertTrue(
        forceRequest.Contains("public double AttackSpeed { get; set; } = 1.0;", StringComparison.Ordinal) &&
        snapshot.Contains("public double AttackSpeed { get; set; } = 1.0;", StringComparison.Ordinal),
        "battle handoff contracts should carry attack speed from request to snapshot");
    AssertTrue(
        runtimeActor.Contains("public double AttackSpeed { get; set; } = 1.0;", StringComparison.Ordinal) &&
        runtimeSession.Contains("AttackSpeed = BattleAttackSpeedPolicy.Normalize(group.AttackSpeed)", StringComparison.Ordinal),
        "runtime actors should consume snapshot attack speed");
    AssertTrue(
        runtimeSession.Contains("actor.AttackCharge", StringComparison.Ordinal),
        "runtime attack cadence should be gated by attack speed rather than attacking every tick unconditionally");
    AssertTrue(
        unitFactory.Contains("attack.AttackSpeed = definition.AttackSpeed;", StringComparison.Ordinal) &&
        unitFactory.Contains("animationComponent.AttackSpeed = definition.AttackSpeed;", StringComparison.Ordinal),
        "unit factory should apply attack speed to presentation attack data and animation playback");
    AssertTrue(
        animationComponent.Contains("BattleAttackSpeedPolicy.ScaleTargetSeconds(targetSeconds, AttackSpeed)", StringComparison.Ordinal),
        "attack animation target duration should be scaled by the configured attack speed");
}

internal static void BattleUnitBaseSceneAuthorsInteractionCollisionShape()
{
    string scene = File.ReadAllText(Path.Combine("scenes", "battle", "entities", "units", "BattleUnitBase.tscn"));

    AssertTrue(scene.Contains("[sub_resource type=\"CircleShape2D\"", StringComparison.Ordinal), "battle unit base should author an interaction circle shape");
    AssertTrue(
        scene.Contains("[node name=\"InteractionShape\" type=\"CollisionShape2D\" parent=\".\"]", StringComparison.Ordinal),
        "battle unit base should include an authored interaction collision shape node");
    AssertTrue(scene.Contains("shape = SubResource(", StringComparison.Ordinal), "interaction collision shape should reference authored shape resource");
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
