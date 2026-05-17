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

internal static void StarterUnitDefinitionsReferenceAudioProfiles()
{
    string[] unitIds = { "player_knight", "militia", "skeleton_warrior", "skeleton_archer" };

    foreach (string unitId in unitIds)
    {
        string unitPath = Path.Combine("assets", "battle", "units", $"{unitId}.tres");
        string audioPath = $"res://assets/battle/units/{unitId}/audio/audio.tres";
        string text = File.ReadAllText(unitPath);
        AssertTrue(text.Contains(audioPath, StringComparison.Ordinal), $"{unitId} should reference its audio profile");

        string audioText = File.ReadAllText(Path.Combine("assets", "battle", "units", unitId, "audio", "audio.tres"));
        foreach (string cue in new[] { "deploy", "move", "attack", "attack_impact", "hit", "defeated" })
        {
            AssertTrue(
                audioText.Contains($"res://assets/battle/units/{unitId}/audio/{cue}.ogg", StringComparison.Ordinal),
                $"{unitId} audio profile should include {cue}");
        }
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
    Dictionary<string, string> expectedNames = new()
    {
        ["player_knight"] = "盾牌铸造者",
        ["militia"] = "盾牌铸造者",
        ["skeleton_warrior"] = "盾牌铸造者",
        ["skeleton_archer"] = "闪烁术士"
    };

    foreach ((string unitId, string expectedName) in expectedNames)
    {
        string unitPath = Path.Combine("assets", "battle", "units", $"{unitId}.tres");
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
