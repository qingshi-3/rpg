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

Environment.SetEnvironmentVariable("RPG_GAMELOG_DIR", Path.Combine(Path.GetTempPath(), "rpg-test-logs"));

Run("multi-target hit feedback outlines every target and formats damage numbers", MultiTargetHitFeedback);
Run("damage number starts close to target and drifts lightly right upward", DamageNumberMotionDefaults);
Run("friendly hover uses green movement and yellow attack preview kinds", FriendlyHoverStyle);
Run("friendly hover keeps combined post-move attack preview", FriendlyHoverWorkload);
Run("friendly hover suppresses yellow attack cells under target units", FriendlyHoverSuppressesAttackCellsUnderTargets);
Run("highlight tile layer updates only changed cells", HighlightTileLayerDiff);
Run("attack target presentation outlines units instead of target cells", AttackTargetPresentation);
Run("movement path arrows are disabled by default", MovementPathArrowsDisabled);
Run("unit visual scale uses global 0.8 multiplier", UnitVisualScaleMultiplier);
Run("action cue waits before action and hides afterward", ActionCueSequencerOrder);
Run("hover info panel anchors to screen edge instead of mouse", HoverInfoPanelAnchorsToScreenEdge);
Run("map camera middle drag pans opposite to mouse motion", MapCameraMiddleDragPansOppositeToMouseMotion);
Run("unit audio definition resolves cue variants deterministically", UnitAudioDefinitionResolvesCueVariants);
Run("starter battle unit definitions reference audio profiles", StarterUnitDefinitionsReferenceAudioProfiles);
Run("starter audio migration is mapped from source visuals", StarterAudioMigrationUsesSourceVisuals);
Run("battle unit display names use resource label plus two digit instance index", BattleUnitDisplayNamesUseIndexedResourceLabel);
Run("starter unit display names use source visual translations", StarterUnitDisplayNamesUseSourceVisualTranslations);
Run("world unit labels resolve through battle unit definitions", WorldUnitLabelsResolveThroughBattleDefinitions);
Run("world resource and faction labels resolve through strategic definitions", WorldResourceAndFactionLabelsResolveThroughDefinitions);
Run("world site and facility labels resolve through strategic definitions", WorldSiteAndFacilityLabelsResolveThroughDefinitions);
Run("world action resource text uses custom resource display names", WorldActionResourceTextUsesCustomDisplayNames);
Run("world action site and facility preview text uses custom display names", WorldActionSiteAndFacilityPreviewTextUsesCustomDisplayNames);
Run("world threat auto resolve messages use configured display names", WorldThreatAutoResolveMessagesUseConfiguredDisplayNames);
Run("world action non-population shortage uses custom resource display name", WorldActionNonPopulationShortageUsesCustomDisplayName);
Run("world action blank resource display name falls back to id", WorldActionBlankResourceDisplayNameFallsBackToId);
Run("world opportunity reward text uses custom resource display name", WorldOpportunityRewardTextUsesCustomResourceDisplayName);
Run("world tick production text uses custom resource display names", WorldTickProductionTextUsesCustomDisplayNames);
Run("world tick threat feed uses configured display names", WorldTickThreatFeedUsesConfiguredDisplayNames);
Run("strategic fog stamps pixel circle independent of tile cells", StrategicFogStampsPixelCircleIndependentOfTileCells);
Run("strategic fog default texel stays below tile sized chunks", StrategicFogDefaultTexelStaysBelowTileSizedChunks);
Run("strategic fog persists explored cells while visible is derived", StrategicFogPersistsExploredCellsWhileVisibleIsDerived);
Run("strategic fog keeps stale site intel after leaving vision", StrategicFogKeepsStaleSiteIntelAfterLeavingVision);
Run("strategic navigation target lookup ignores fog visibility", StrategicNavigationTargetLookupIgnoresFogVisibility);
Run("strategic navigation command flow stays independent from fog", StrategicNavigationCommandFlowStaysIndependentFromFog);
Run("strategic navigation layer is isolated from camera transform", StrategicNavigationLayerIsIsolatedFromCameraTransform);
Run("strategic fog overlay uses circular visibility mask", StrategicFogOverlayUsesCircularVisibilityMask);
Run("world site grid exploration state persists position and memory", WorldSiteGridExplorationStatePersistsPositionAndMemory);
Run("world site grid exploration uses battle grid pathing outside battle turns", WorldSiteGridExplorationUsesBattleGridPathingOutsideBattleTurns);
Run("world site root routes authored exploration point actions", WorldSiteRootRoutesAuthoredExplorationPointActions);
Run("world site root gates hostile garrison text by site intel", WorldSiteRootGatesHostileGarrisonTextBySiteIntel);
Run("world site deployment uses known entrances before desired approach direction", WorldSiteDeploymentUsesKnownEntrancesBeforeDesiredApproachDirection);
Run("site exploration tick moves party by exploration AP", SiteExplorationTickMovesPartyByExplorationAp);
Run("site exploration tick moves patrol by route AP", SiteExplorationTickMovesPatrolByRouteAp);
Run("site exploration alert radius pauses simulation", SiteExplorationAlertRadiusPausesSimulation);
Run("world site exploration battle request carries exploration context", WorldSiteExplorationBattleRequestCarriesExplorationContext);
Run("exploration battle request carries patrol trigger", ExplorationBattleRequestCarriesPatrolTrigger);
Run("exploration battle victory removes triggering patrol", ExplorationBattleVictoryRemovesTriggeringPatrol);
Run("world site hover summary uses local resources and force counts", WorldSiteHoverSummaryUsesLocalResourcesAndForceCounts);
Run("world site hover summary stays inside viewport", WorldSiteHoverSummaryStaysInsideViewport);
Run("strategic world forwards middle mouse camera navigation", StrategicWorldForwardsMiddleMouseCameraNavigation);
Run("battle result applier messages use configured display names", BattleResultApplierMessagesUseConfiguredDisplayNames);
Run("battle unit factory keeps definition caches shared across scenes", BattleUnitFactoryKeepsDefinitionCachesShared);
Run("battle result applier uses survivor counts when garrisoning assault army", BattleResultApplierUsesSurvivorCountsWhenGarrisoningAssaultArmy);
Run("battle result applier keeps surviving defending garrison after defense victory", BattleResultApplierKeepsSurvivingDefendingGarrisonAfterDefenseVictory);
Run("unit display name translation report keeps low confidence review queue bounded", UnitDisplayNameTranslationReportQuality);

static void MultiTargetHitFeedback()
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

static void DamageNumberMotionDefaults()
{
    BattleDamageNumberMotionSpec spec = BattleDamageNumberMotionSpec.Default;

    AssertTrue(spec.SpawnOffset.Y > -40f, "damage number should start closer to the unit than the old high offset");
    AssertTrue(spec.FloatOffset.X > 0f, "damage number should drift slightly right");
    AssertTrue(spec.FloatOffset.Y < 0f, "damage number should drift upward");
}

static void FriendlyHoverStyle()
{
    BattleHoverPreviewStyle style = BattleHoverPreviewStyle.ForFaction(BattleFaction.Player);

    AssertEqual(BattleGridHighlightKind.FriendlyMove, style.MoveKind, "friendly hover movement should use green movement kind");
    AssertEqual(BattleGridHighlightKind.FriendlyAttack, style.AttackKind, "friendly hover attack should use yellow attack kind");
}

static void FriendlyHoverWorkload()
{
    BattleHoverPreviewStyle friendly = BattleHoverPreviewStyle.ForFaction(BattleFaction.Player);
    BattleHoverPreviewStyle enemy = BattleHoverPreviewStyle.ForFaction(BattleFaction.Enemy);

    AssertEqual(true, friendly.ProjectAttackFromReachableMoveOrigins, "friendly hover should keep combined movement plus attack preview semantics");
    AssertEqual(true, enemy.ProjectAttackFromReachableMoveOrigins, "enemy hover should keep full threat projection");
}

static void FriendlyHoverSuppressesAttackCellsUnderTargets()
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

static void HighlightTileLayerDiff()
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

static void AttackTargetPresentation()
{
    BattleTargetPresentationPlan plan = BattleTargetPresentationPlan.Build(new[] { "enemy_a", "enemy_b", "enemy_a", "" });

    AssertEqual(false, plan.ShowTargetGridCells, "attack target presentation should not use yellow target cells");
    AssertSequence(new[] { "enemy_a", "enemy_b" }, plan.TargetEntityIds, "attack target presentation should outline each target once");
}

static void MovementPathArrowsDisabled()
{
    AssertEqual(false, BattlePathArrowPresentation.Default.ShowMovementPathArrows, "movement preview should not draw path arrows");
}

static void UnitVisualScaleMultiplier()
{
    AssertFloatEqual(0.8f, BattleUnitVisualScale.Default.SpriteScaleMultiplier, 0.0001f, "unit visuals should be reduced by one fifth");
}

static void ActionCueSequencerOrder()
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

static void HoverInfoPanelAnchorsToScreenEdge()
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

static void MapCameraMiddleDragPansOppositeToMouseMotion()
{
    Godot.Vector2 position = MapCameraController.CalculateMiddleMouseDragPanPosition(
        currentPosition: new Godot.Vector2(500f, 320f),
        mouseRelative: new Godot.Vector2(40f, -20f),
        zoomScalar: 2f);

    AssertFloatEqual(480f, position.X, 0.001f, "middle drag should pan opposite to horizontal mouse motion at current zoom");
    AssertFloatEqual(330f, position.Y, 0.001f, "middle drag should pan opposite to vertical mouse motion at current zoom");
}

static void UnitAudioDefinitionResolvesCueVariants()
{
    AssertEqual(0, BattleUnitAudioDefinition.ResolveVariantIndex(0, 2), "first variant index should resolve directly");
    AssertEqual(1, BattleUnitAudioDefinition.ResolveVariantIndex(1, 2), "second variant index should resolve directly");
    AssertEqual(0, BattleUnitAudioDefinition.ResolveVariantIndex(2, 2), "variant index should wrap forward");
    AssertEqual(1, BattleUnitAudioDefinition.ResolveVariantIndex(-1, 2), "variant index should wrap backward");
    AssertEqual(0, BattleUnitAudioDefinition.ResolveVariantIndex(5, 1), "single variant should always resolve to zero");
    AssertEqual(-1, BattleUnitAudioDefinition.ResolveVariantIndex(0, 0), "empty variants should return sentinel");
}

static void StarterUnitDefinitionsReferenceAudioProfiles()
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

static void StarterAudioMigrationUsesSourceVisuals()
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

static void BattleUnitDisplayNamesUseIndexedResourceLabel()
{
    AssertEqual("盾牌铸造者01", BattleUnitDisplayNameFormatter.FormatInstanceName("盾牌铸造者", 0), "first visible unit should use 01 suffix");
    AssertEqual("盾牌铸造者02", BattleUnitDisplayNameFormatter.FormatInstanceName("盾牌铸造者", 1), "second visible unit should use 02 suffix");
    AssertEqual("战斗单位03", BattleUnitDisplayNameFormatter.FormatInstanceName("", 2), "missing display names should use a readable fallback");
}

static void StarterUnitDisplayNamesUseSourceVisualTranslations()
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

static void WorldUnitLabelsResolveThroughBattleDefinitions()
{
    string strategicRoot = File.ReadAllText(Path.Combine("src", "Presentation", "World", "StrategicWorldRoot.cs"));
    string siteRoot = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.cs"));

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

static void WorldResourceAndFactionLabelsResolveThroughDefinitions()
{
    StrategicWorldDefinition definition = BuildResourceDisplayNameTestDefinition();
    StrategicWorldDefinitionQueries queries = new(definition);

    AssertEqual("Labor", StrategicWorldDisplayNames.GetResourceLabel(queries, StrategicWorldIds.ResourcePopulation), "resource label should use custom DisplayName");
    AssertEqual("Ash Court", StrategicWorldDisplayNames.GetFactionLabel(queries, StrategicWorldIds.FactionUndead), "faction label should use custom DisplayName");

    definition.ResourceDefinitions.Single(item => item.Id == StrategicWorldIds.ResourceEconomy).DisplayName = "";
    definition.FactionDefinitions.Single(item => item.Id == StrategicWorldIds.FactionUndead).DisplayName = "";
    queries = new StrategicWorldDefinitionQueries(definition);

    AssertEqual(StrategicWorldIds.ResourceEconomy, StrategicWorldDisplayNames.GetResourceLabel(queries, StrategicWorldIds.ResourceEconomy), "blank resource DisplayName should fall back to id");
    AssertEqual(StrategicWorldIds.FactionUndead, StrategicWorldDisplayNames.GetFactionLabel(queries, StrategicWorldIds.FactionUndead), "blank faction DisplayName should fall back to id");
    AssertEqual("无", StrategicWorldDisplayNames.GetResourceLabel(queries, ""), "blank resource id should use explicit fallback");
    AssertEqual("无", StrategicWorldDisplayNames.GetFactionLabel(queries, ""), "blank faction id should use explicit fallback");
}

static void WorldSiteAndFacilityLabelsResolveThroughDefinitions()
{
    StrategicWorldDefinition definition = BuildResourceDisplayNameTestDefinition();
    StrategicWorldDefinitionQueries queries = new(definition);

    AssertEqual("Test Quarry", StrategicWorldDisplayNames.GetSiteLabel(queries, StrategicWorldIds.SiteBonefield), "site label should use custom DisplayName");
    AssertEqual("Deep Quarry", StrategicWorldDisplayNames.GetFacilityLabel(queries, StrategicWorldIds.FacilityMine), "facility label should use custom DisplayName");

    definition.SiteDefinitions.Single(item => item.Id == StrategicWorldIds.SiteBonefield).DisplayName = "";
    definition.FacilityDefinitions.Single(item => item.Id == StrategicWorldIds.FacilityMine).DisplayName = "";
    queries = new StrategicWorldDefinitionQueries(definition);

    AssertEqual(StrategicWorldIds.SiteBonefield, StrategicWorldDisplayNames.GetSiteLabel(queries, StrategicWorldIds.SiteBonefield), "blank site DisplayName should fall back to id");
    AssertEqual(StrategicWorldIds.FacilityMine, StrategicWorldDisplayNames.GetFacilityLabel(queries, StrategicWorldIds.FacilityMine), "blank facility DisplayName should fall back to id");
    AssertEqual("missing_site", StrategicWorldDisplayNames.GetSiteLabel(queries, "missing_site"), "missing site definition should fall back to id");
    AssertEqual("missing_facility", StrategicWorldDisplayNames.GetFacilityLabel(queries, "missing_facility"), "missing facility definition should fall back to id");
    AssertEqual("无", StrategicWorldDisplayNames.GetSiteLabel(queries, ""), "blank site id should use default fallback");
    AssertEqual("无", StrategicWorldDisplayNames.GetFacilityLabel(queries, ""), "blank facility id should use default fallback");
    AssertEqual("Fallback Site", StrategicWorldDisplayNames.GetSiteLabel(queries, "missing_site", "Fallback Site"), "missing site should use explicit fallback when provided");
    AssertEqual("Fallback Facility", StrategicWorldDisplayNames.GetFacilityLabel(queries, "", "Fallback Facility"), "blank facility id should use explicit fallback when provided");
}

static void WorldActionResourceTextUsesCustomDisplayNames()
{
    StrategicWorldDefinition definition = BuildResourceDisplayNameTestDefinition();
    StrategicWorldState state = BuildResourceDisplayNameTestState();
    state.PlayerResources.Set(StrategicWorldIds.ResourcePopulation, 0);

    WorldActionViewModel action = new WorldActionResolver()
        .GetAvailableActions(state, definition, StrategicWorldIds.SiteBonefield)
        .Single(item => item.ActionId == StrategicWorldIds.ActionBuildMine);

    AssertEqual(false, action.IsEnabled, "build mine should be disabled when custom population resource is missing");
    AssertEqual("Labor不足", action.DisabledReason, "population shortage should use custom resource display name");
    AssertTrue(
        action.EffectLines.Contains("占用Labor 1"),
        "build mine effect text should use custom population display name");
    AssertTrue(
        action.EffectLines.Contains("每世界步Granite +2"),
        "build mine effect text should use custom stone display name");
}

static void WorldActionSiteAndFacilityPreviewTextUsesCustomDisplayNames()
{
    StrategicWorldDefinition definition = BuildResourceDisplayNameTestDefinition();
    StrategicWorldState state = BuildResourceDisplayNameTestState();
    state.SiteStates[StrategicWorldIds.SiteBonefield].Facilities.Add(new FacilityInstance
    {
        InstanceId = "tower:test",
        FacilityId = StrategicWorldIds.FacilityDefenseTower,
        SiteId = StrategicWorldIds.SiteBonefield,
        State = FacilityState.Active
    });
    state.ThreatPlans["threat:preview"] = new EnemyThreatPlan
    {
        Id = "threat:preview",
        SourceSiteId = StrategicWorldIds.SiteGraveyard,
        TargetSiteId = StrategicWorldIds.SiteBonefield,
        Stage = ThreatStage.Attacking
    };

    WorldActionResolver resolver = new(unitTypeId =>
        unitTypeId == StrategicWorldIds.UnitMilitia ? "Guard Recruit" : unitTypeId);
    WorldActionViewModel buildMine = resolver
        .GetAvailableActions(state, definition, StrategicWorldIds.SiteBonefield)
        .Single(item => item.ActionId == StrategicWorldIds.ActionBuildMine);
    WorldActionViewModel buildDefenseTower = resolver
        .GetAvailableActions(state, definition, StrategicWorldIds.SiteBonefield)
        .Single(item => item.ActionId == StrategicWorldIds.ActionBuildDefenseTower);
    WorldActionViewModel trainMilitia = resolver
        .GetAvailableActions(state, definition, StrategicWorldIds.SitePlayerCamp)
        .Single(item => item.ActionId == StrategicWorldIds.ActionTrainMilitia);
    WorldActionViewModel autoResolveRaid = resolver
        .GetAvailableActions(state, definition, "", "threat:preview")
        .Single(item => item.ActionId == StrategicWorldIds.ActionAutoResolveRaid);

    AssertTrue(buildMine.EffectLines.Any(line => line.Contains("Test Quarry", StringComparison.Ordinal) && line.Contains("Deep Quarry", StringComparison.Ordinal)), "build mine preview should use custom site and mine names");
    AssertTrue(!buildMine.EffectLines.Any(line => line.Contains("埋骨地", StringComparison.Ordinal) || line.Contains("矿场", StringComparison.Ordinal)), "build mine preview should not hardcode default site or mine names");

    AssertTrue(buildDefenseTower.EffectLines.Any(line => line.Contains("Test Quarry", StringComparison.Ordinal)), "build defense tower preview should use custom site name");
    AssertTrue(buildDefenseTower.EffectLines.Any(line => line.Contains("Signal Spire", StringComparison.Ordinal)), "build defense tower preview should use custom tower name");
    AssertTrue(!buildDefenseTower.EffectLines.Any(line => line.Contains("埋骨地", StringComparison.Ordinal) || line.Contains("防御塔", StringComparison.Ordinal)), "build defense tower preview should not hardcode default site or tower names");

    AssertTrue(trainMilitia.EffectLines.Any(line => line.Contains("Forward Camp", StringComparison.Ordinal)), "train militia preview should use custom player camp name");
    AssertTrue(!trainMilitia.EffectLines.Any(line => line.Contains("玩家营地", StringComparison.Ordinal)), "train militia preview should not hardcode default player camp name");
    AssertTrue(trainMilitia.EffectLines.Any(line => line.Contains("Guard Recruit", StringComparison.Ordinal)), "train militia preview should use injected unit display name");
    AssertTrue(!trainMilitia.EffectLines.Any(line => line.Contains("民兵", StringComparison.Ordinal)), "train militia preview should not hardcode default militia name");

    AssertTrue(autoResolveRaid.EffectLines.Any(line => line.Contains("Signal Spire", StringComparison.Ordinal)), "auto resolve preview should use custom tower name");
    AssertTrue(autoResolveRaid.WarningLines.Any(line => line.Contains("Signal Spire", StringComparison.Ordinal)), "auto resolve warning should use custom tower name");
    AssertTrue(!autoResolveRaid.EffectLines.Concat(autoResolveRaid.WarningLines).Any(line => line.Contains("防御塔", StringComparison.Ordinal)), "auto resolve text should not hardcode default tower name");
    AssertTrue(autoResolveRaid.WarningLines.Any(line => line.Contains("Guard Recruit", StringComparison.Ordinal)), "auto resolve warning should use injected militia display name");
    AssertTrue(!autoResolveRaid.WarningLines.Any(line => line.Contains("民兵", StringComparison.Ordinal)), "auto resolve warning should not hardcode default militia name");
}

static void WorldThreatAutoResolveMessagesUseConfiguredDisplayNames()
{
    StrategicWorldDefinition definition = BuildResourceDisplayNameTestDefinition();
    WorldThreatService service = new(unitTypeId =>
        unitTypeId == StrategicWorldIds.UnitMilitia ? "Guard Recruit" : unitTypeId);

    WorldActionResult strongDefense = service.ResolveRaidAutomatically(
        BuildThreatAutoResolveState(militia: 4, towers: 0),
        definition,
        "threat:auto");
    AssertTrue(strongDefense.Message.Contains("Test Quarry", StringComparison.Ordinal), "strong defense message should use configured target site name");
    AssertTrue(strongDefense.Message.Contains("Ash Court", StringComparison.Ordinal), "strong defense message should use configured attacker faction name");
    AssertTrue(!strongDefense.Message.Contains("埋骨地", StringComparison.Ordinal) && !strongDefense.Message.Contains("亡灵", StringComparison.Ordinal), "strong defense message should not hardcode default site or faction names");

    WorldActionResult costlyDefense = service.ResolveRaidAutomatically(
        BuildThreatAutoResolveState(militia: 2, towers: 0),
        definition,
        "threat:auto");
    AssertTrue(costlyDefense.Message.Contains("Guard Recruit", StringComparison.Ordinal), "costly defense message should use injected unit display name");
    AssertTrue(!costlyDefense.Message.Contains("民兵", StringComparison.Ordinal), "costly defense message should not hardcode default militia name");

    WorldActionResult damagedDefense = service.ResolveRaidAutomatically(
        BuildThreatAutoResolveState(militia: 1, towers: 0),
        definition,
        "threat:auto");
    AssertTrue(damagedDefense.Message.Contains("Test Quarry", StringComparison.Ordinal), "damaged defense message should use configured site name");
    AssertTrue(damagedDefense.Message.Contains("Deep Quarry", StringComparison.Ordinal), "damaged defense message should use configured mine name");
    AssertTrue(!damagedDefense.Message.Contains("埋骨地", StringComparison.Ordinal) && !damagedDefense.Message.Contains("矿场", StringComparison.Ordinal), "damaged defense message should not hardcode default site or mine names");

    WorldActionResult lostDefense = service.ResolveRaidAutomatically(
        BuildThreatAutoResolveState(militia: 0, towers: 0),
        definition,
        "threat:auto");
    AssertTrue(lostDefense.Message.Contains("Test Quarry", StringComparison.Ordinal), "lost defense message should use configured site name");
    AssertTrue(lostDefense.Message.Contains("Ash Court", StringComparison.Ordinal), "lost defense message should use configured faction name");
    AssertTrue(!lostDefense.Message.Contains("埋骨地", StringComparison.Ordinal) && !lostDefense.Message.Contains("亡灵", StringComparison.Ordinal), "lost defense message should not hardcode default site or faction names");
}

static void WorldActionNonPopulationShortageUsesCustomDisplayName()
{
    StrategicWorldDefinition definition = BuildResourceDisplayNameTestDefinition();
    StrategicWorldState state = BuildResourceDisplayNameTestState();
    state.PlayerResources.Set(StrategicWorldIds.ResourceEconomy, 0);

    WorldActionViewModel action = new WorldActionResolver()
        .GetAvailableActions(state, definition, StrategicWorldIds.SiteBonefield)
        .Single(item => item.ActionId == "test_economy_cost_action");

    AssertEqual(false, action.IsEnabled, "economy-cost action should be disabled when custom economy resource is missing");
    AssertEqual("Coin不足", action.DisabledReason, "non-population shortage should use the missing resource display name");
    AssertTrue(
        !action.DisabledReason.Contains("资源不足", StringComparison.Ordinal),
        "non-population shortage should not use the generic resource shortage label");
    AssertTrue(
        !action.DisabledReason.Contains(StrategicWorldIds.ResourceEconomy, StringComparison.Ordinal),
        "non-population shortage should not expose the resource id when a display name exists");

    WorldActionResult result = new WorldActionResolver().Apply(
        state,
        definition,
        new WorldActionRequest
        {
            ActionId = "test_economy_cost_action",
            SourceSiteId = StrategicWorldIds.SiteBonefield,
            TargetSiteId = StrategicWorldIds.SiteBonefield
        },
        "",
        "");

    AssertEqual(false, result.Success, "economy-cost action should fail when applied without enough custom economy resource");
    AssertEqual("Coin不足", result.Message, "failed action result should use the missing resource display name");
    AssertTrue(
        result.FailureReason.Contains(StrategicWorldIds.ResourceEconomy, StringComparison.Ordinal),
        "failure reason should carry the concrete missing resource id for formatting");
}

static void WorldActionBlankResourceDisplayNameFallsBackToId()
{
    StrategicWorldDefinition definition = BuildResourceDisplayNameTestDefinition();
    definition.ResourceDefinitions.Single(item => item.Id == StrategicWorldIds.ResourceEconomy).DisplayName = "";
    StrategicWorldState state = BuildResourceDisplayNameTestState();
    state.PlayerResources.Set(StrategicWorldIds.ResourceEconomy, 0);

    WorldActionViewModel action = new WorldActionResolver()
        .GetAvailableActions(state, definition, StrategicWorldIds.SiteBonefield)
        .Single(item => item.ActionId == "test_economy_cost_action");

    AssertEqual(
        $"{StrategicWorldIds.ResourceEconomy}不足",
        action.DisabledReason,
        "blank resource DisplayName should fall back to the resource id instead of an empty label");
}

static void WorldOpportunityRewardTextUsesCustomResourceDisplayName()
{
    StrategicWorldDefinition definition = BuildResourceDisplayNameTestDefinition();
    definition.OpportunityDefinitions.Add(new WorldOpportunityDefinition
    {
        Id = "test_opportunity",
        DisplayName = "Test Cache",
        CompletionRewards = { new ResourceAmountDefinition(StrategicWorldIds.ResourceStone, 3) }
    });
    StrategicWorldState state = BuildResourceDisplayNameTestState();
    state.OpportunityStates["opportunity:test"] = new WorldOpportunityState
    {
        OpportunityId = "opportunity:test",
        DefinitionId = "test_opportunity",
        Status = WorldOpportunityStatus.Active,
        SpawnPointId = "spawn:test"
    };

    WorldActionResult result = new WorldOpportunityService().CompleteOpportunity(
        state,
        definition,
        "opportunity:test");

    AssertEqual(true, result.Success, "opportunity completion should succeed");
    AssertTrue(
        result.Message.Contains("Granite +3", StringComparison.Ordinal),
        "opportunity reward text should use custom resource display name");
    AssertTrue(
        !result.Message.Contains("石材 +3", StringComparison.Ordinal),
        "opportunity reward text should not hardcode the default stone label");
}

static void WorldTickProductionTextUsesCustomDisplayNames()
{
    StrategicWorldDefinition definition = BuildResourceDisplayNameTestDefinition();
    StrategicWorldState state = BuildResourceDisplayNameTestState();
    state.SiteStates[StrategicWorldIds.SiteBonefield].Facilities.Add(new FacilityInstance
    {
        InstanceId = "mine:test",
        FacilityId = StrategicWorldIds.FacilityMine,
        SiteId = StrategicWorldIds.SiteBonefield,
        State = FacilityState.Active,
        AssignedPopulation = 1
    });

    WorldTickResult result = new WorldTickService().AdvanceWorldTick(state, definition);

    AssertTrue(
        result.Messages.Any(message => message.Contains("Granite +2", StringComparison.Ordinal)),
        "mine production message should use custom stone display name");
    AssertTrue(
        result.Messages.Any(message => message.Contains("Deep Quarry", StringComparison.Ordinal)),
        "mine production message should use custom mine display name");
    AssertTrue(
        !result.Messages.Any(message => message.Contains("石材 +2", StringComparison.Ordinal) || message.Contains("矿场", StringComparison.Ordinal)),
        "mine production message should not hardcode default resource or facility labels");
}

static void WorldTickThreatFeedUsesConfiguredDisplayNames()
{
    StrategicWorldDefinition spawnDefinition = BuildResourceDisplayNameTestDefinition();
    spawnDefinition.ThreatRules.Add(new ThreatRuleDefinition
    {
        Id = "test_threat_rule",
        SourceSiteId = StrategicWorldIds.SiteGraveyard,
        TargetSiteId = StrategicWorldIds.SiteBonefield,
        InitialCountdownTicks = 3,
        ThreatType = ThreatType.Raid,
        EnemyForces = { new GarrisonDefinition { UnitTypeId = StrategicWorldIds.UnitMilitia, Count = 1 } }
    });
    StrategicWorldState spawnState = BuildResourceDisplayNameTestState();
    spawnState.SiteStates[StrategicWorldIds.SiteGraveyard].Garrison.Add(new GarrisonState
    {
        UnitTypeId = StrategicWorldIds.UnitMilitia,
        Count = 1
    });

    WorldTickResult spawnResult = new WorldTickService().AdvanceWorldTick(spawnState, spawnDefinition);

    AssertTrue(
        spawnResult.Messages.Any(message =>
            message.Contains("Ash Gate", StringComparison.Ordinal) &&
            message.Contains("Ash Court", StringComparison.Ordinal) &&
            message.Contains("Test Quarry", StringComparison.Ordinal)),
        "threat spawn message should use configured source site, faction, and target site names");
    AssertTrue(
        !spawnResult.Messages.Any(message => message.Contains("敌军", StringComparison.Ordinal)),
        "threat spawn message should not use generic hardcoded enemy label when a faction display name exists");

    StrategicWorldDefinition arrivalDefinition = BuildResourceDisplayNameTestDefinition();
    StrategicWorldState arrivalState = BuildResourceDisplayNameTestState();
    arrivalState.ThreatPlans["threat:arrival"] = new EnemyThreatPlan
    {
        Id = "threat:arrival",
        SourceSiteId = StrategicWorldIds.SiteGraveyard,
        TargetSiteId = StrategicWorldIds.SiteBonefield,
        Stage = ThreatStage.Marching,
        InitialCountdownTicks = 1,
        CountdownTicks = 1,
        CreatedTick = 0
    };

    WorldTickResult arrivalResult = new WorldTickService().AdvanceWorldTick(arrivalState, arrivalDefinition);

    AssertTrue(
        arrivalResult.Messages.Any(message =>
            message.Contains("Ash Court", StringComparison.Ordinal) &&
            message.Contains("Test Quarry", StringComparison.Ordinal)),
        "threat arrival message should use configured faction and target site names");
    AssertTrue(
        !arrivalResult.Messages.Any(message => message.Contains("敌方", StringComparison.Ordinal)),
        "threat arrival message should not use generic hardcoded enemy label when a faction display name exists");
}

static void StrategicFogStampsPixelCircleIndependentOfTileCells()
{
    StrategicFogOfWarSettings settings = new()
    {
        FogTexelWorldSize = 10f,
        SiteVisionRadius = 20f,
        ArmyVisionRadius = 20f
    };

    HashSet<string> visible = StrategicFogOfWarService.BuildVisibleCellKeys(
        new[] { new StrategicFogVisionSource(new Godot.Vector2(0f, 0f), 20f) },
        settings);

    AssertTrue(visible.Contains("0:0"), "fog circle should include the source cell");
    AssertTrue(visible.Contains("1:1"), "fog circle should include diagonal cells inside the pixel radius");
    AssertTrue(visible.Contains("-1:0"), "fog circle should include negative x cells around the source");
    AssertTrue(!visible.Contains("2:2"), "fog circle should exclude diagonal cells outside the pixel radius");
}

static void StrategicFogDefaultTexelStaysBelowTileSizedChunks()
{
    AssertFloatEqual(16f, StrategicFogOfWarService.DefaultFogTexelWorldSize, 0.001f, "default fog texel should be fine enough to avoid cell-sized chunky edges");
    StrategicFogOfWarSettings settings = new();

    AssertFloatEqual(16f, settings.FogTexelWorldSize, 0.001f, "new fog settings should use the shared default texel size");
}

static void StrategicFogPersistsExploredCellsWhileVisibleIsDerived()
{
    StrategicWorldDefinition definition = BuildResourceDisplayNameTestDefinition();
    StrategicWorldState state = BuildResourceDisplayNameTestState();
    state.PlayerFactionId = StrategicWorldIds.FactionPlayer;
    definition.SiteDefinitions.Single(site => site.Id == StrategicWorldIds.SitePlayerCamp).MapPosition = new Godot.Vector2(0f, 0f);
    definition.SiteDefinitions.Single(site => site.Id == StrategicWorldIds.SiteBonefield).MapPosition = new Godot.Vector2(12f, 0f);
    definition.SiteDefinitions.Single(site => site.Id == StrategicWorldIds.SiteGraveyard).MapPosition = new Godot.Vector2(80f, 0f);
    state.SiteStates[StrategicWorldIds.SitePlayerCamp].OwnerFactionId = StrategicWorldIds.FactionPlayer;
    state.SiteStates[StrategicWorldIds.SiteBonefield].OwnerFactionId = StrategicWorldIds.FactionUndead;

    StrategicFogOfWarSettings settings = new()
    {
        FogTexelWorldSize = 10f,
        SiteVisionRadius = 15f,
        ArmyVisionRadius = 15f
    };

    StrategicFogOfWarService.RefreshVisibility(state, definition, settings);

    AssertEqual(WorldIntelVisibility.Visible, StrategicFogOfWarService.GetSiteVisibility(state.Intel, definition.SiteDefinitions.Single(site => site.Id == StrategicWorldIds.SiteBonefield), settings), "nearby site should be visible");
    AssertEqual(WorldIntelVisibility.Unknown, StrategicFogOfWarService.GetSiteVisibility(state.Intel, definition.SiteDefinitions.Single(site => site.Id == StrategicWorldIds.SiteGraveyard), settings), "far site should remain unknown");
    AssertTrue(state.Intel.ExploredCells.Contains("1:0"), "visible cells should be merged into explored cells");

    state.SiteStates[StrategicWorldIds.SitePlayerCamp].OwnerFactionId = StrategicWorldIds.FactionUndead;
    StrategicFogOfWarService.RefreshVisibility(state, definition, settings);

    AssertTrue(state.Intel.VisibleCells.Count == 0, "visible cells should be derived fresh each refresh");
    AssertTrue(state.Intel.ExploredCells.Contains("1:0"), "explored cells should persist after vision source is gone");
}

static void StrategicFogKeepsStaleSiteIntelAfterLeavingVision()
{
    StrategicWorldDefinition definition = BuildResourceDisplayNameTestDefinition();
    StrategicWorldState state = BuildResourceDisplayNameTestState();
    state.PlayerFactionId = StrategicWorldIds.FactionPlayer;
    WorldSiteDefinition camp = definition.SiteDefinitions.Single(site => site.Id == StrategicWorldIds.SitePlayerCamp);
    WorldSiteDefinition target = definition.SiteDefinitions.Single(site => site.Id == StrategicWorldIds.SiteBonefield);
    camp.MapPosition = new Godot.Vector2(0f, 0f);
    target.MapPosition = new Godot.Vector2(12f, 0f);
    state.SiteStates[StrategicWorldIds.SitePlayerCamp].OwnerFactionId = StrategicWorldIds.FactionPlayer;
    WorldSiteState targetState = state.SiteStates[StrategicWorldIds.SiteBonefield];
    targetState.OwnerFactionId = StrategicWorldIds.FactionPlayer;
    targetState.LocalResources.Set(StrategicWorldIds.ResourceStone, 7);

    StrategicFogOfWarSettings settings = new()
    {
        FogTexelWorldSize = 10f,
        SiteVisionRadius = 15f,
        ArmyVisionRadius = 15f
    };

    state.WorldTick = 3;
    StrategicFogOfWarService.RefreshVisibility(state, definition, settings);
    targetState.LocalResources.Set(StrategicWorldIds.ResourceStone, 12);
    state.SiteStates[StrategicWorldIds.SitePlayerCamp].OwnerFactionId = StrategicWorldIds.FactionUndead;
    targetState.OwnerFactionId = StrategicWorldIds.FactionUndead;
    state.WorldTick = 4;
    StrategicFogOfWarService.RefreshVisibility(state, definition, settings);

    AssertEqual(WorldIntelVisibility.Revealed, StrategicFogOfWarService.GetSiteVisibility(state.Intel, target, settings), "known site should become revealed stale intel after leaving vision");
    AssertEqual(3, state.Intel.KnownSites[StrategicWorldIds.SiteBonefield].LastSeenWorldTick, "stale site intel should preserve last visible tick");
    AssertEqual(7, state.Intel.KnownSites[StrategicWorldIds.SiteBonefield].KnownLocalResources.GetAmount(StrategicWorldIds.ResourceStone), "stale site intel should not refresh while outside vision");
}

static void StrategicNavigationTargetLookupIgnoresFogVisibility()
{
    string strategicRoot = File.ReadAllText(Path.Combine("src", "Presentation", "World", "StrategicWorldRoot.cs"));
    string findSiteAtBody = ExtractMethodBlock(strategicRoot, "private WorldSiteDefinition FindSiteAt");
    AssertTrue(
        !findSiteAtBody.Contains("GetSiteIntelVisibility", StringComparison.Ordinal),
        "site target lookup is used by navigation commands and must not depend on fog visibility");
}

static void StrategicNavigationCommandFlowStaysIndependentFromFog()
{
    string strategicRoot = File.ReadAllText(Path.Combine("src", "Presentation", "World", "StrategicWorldRoot.cs"));
    foreach (string methodSignature in new[]
             {
                 "private bool TryCommandSelectedArmies",
                 "private bool TryCommandSelectedArmiesToSite",
                 "private bool TryIssueExpeditionToTarget",
                 "private bool TryIssueExpeditionToSite",
                 "private bool TryCreateExpedition",
                 "private bool TryResolveExpeditionNavigation",
                 "private bool TryBuildCommandPaths"
             })
    {
        string methodBody = ExtractMethodBlock(strategicRoot, methodSignature);
        AssertTrue(!methodBody.Contains("GetSiteIntelVisibility", StringComparison.Ordinal), $"{methodSignature} must not read site fog visibility");
        AssertTrue(!methodBody.Contains("IsMapPositionVisible", StringComparison.Ordinal), $"{methodSignature} must not read map fog visibility");
        AssertTrue(!methodBody.Contains("IsScreenPositionVisible", StringComparison.Ordinal), $"{methodSignature} must not read screen fog visibility");
    }
}

static void StrategicNavigationLayerIsIsolatedFromCameraTransform()
{
    string strategicRoot = File.ReadAllText(Path.Combine("src", "Presentation", "World", "StrategicWorldRoot.cs"));
    string navigationContext = File.ReadAllText(Path.Combine("src", "Application", "World", "StrategicNavigationContext.cs"));
    AssertTrue(
        strategicRoot.Contains("EnsureStrategicNavigationLayerIsStable", StringComparison.Ordinal),
        "strategic root should move navigation data under a stable root before camera transforms WorldMapRoot");
    AssertTrue(
        strategicRoot.Contains("_strategicNavigationRoot", StringComparison.Ordinal),
        "navigation context should use a root that is not panned or scaled as the visual map camera");

    string updateCameraBody = ExtractMethodBlock(strategicRoot, "private bool UpdateWorldCameraView");
    AssertTrue(
        !updateCameraBody.Contains("_strategicNavigationRoot.Global", StringComparison.Ordinal),
        "camera view updates must not transform the stable navigation root");
    AssertTrue(
        !navigationContext.Contains("NavigationServer2D", StringComparison.Ordinal),
        "strategic map navigation should not depend on Godot NavigationServer2D synchronization");
    AssertTrue(
        navigationContext.Contains("StrategicNavigationGrid", StringComparison.Ordinal),
        "strategic map navigation should use the project-owned grid provider");
}

static void StrategicFogOverlayUsesCircularVisibilityMask()
{
    string overlay = File.ReadAllText(Path.Combine("src", "Presentation", "World", "StrategicWorldFogOverlay.cs"));
    string strategicRoot = File.ReadAllText(Path.Combine("src", "Presentation", "World", "StrategicWorldRoot.cs"));
    string shader = File.ReadAllText(Path.Combine("assets", "world", "shaders", "strategic_fog_of_war.gdshader"));
    string refreshFogBody = ExtractMethodBlock(strategicRoot, "private void RefreshStrategicFogOverlay");
    AssertTrue(overlay.Contains("StrategicWorldFogOverlayCircle", StringComparison.Ordinal), "fog overlay should receive circular visible masks");
    AssertTrue(overlay.Contains("ShaderMaterial", StringComparison.Ordinal), "fog overlay should use a shader material for smooth fog movement");
    AssertTrue(shader.Contains("distance(sample_pixel, circle.xy)", StringComparison.Ordinal), "fog shader should draw circular visibility by pixel distance");
    AssertTrue(!shader.Contains("step(0.5, explored)", StringComparison.Ordinal), "explored fog should not use a hard cell-mask threshold");
    AssertTrue(shader.Contains("explored_amount", StringComparison.Ordinal), "explored fog should blend through a soft mask amount");
    AssertTrue(!shader.Contains("return;", StringComparison.Ordinal), "Godot canvas fragment shaders must not use return statements");
    AssertTrue(overlay.Contains("Visible = false", StringComparison.Ordinal), "fog overlay should stay hidden if the shader cannot be applied");
    AssertTrue(!overlay.Contains("DrawRect(cell.ScreenRect", StringComparison.Ordinal), "fog overlay should not render the full fog edge as raw cell rectangles");
    AssertTrue(overlay.Contains("FillMaskSoftCircle", StringComparison.Ordinal), "explored fog mask should stamp soft circular cells instead of hard rectangles");
    AssertTrue(!refreshFogBody.Contains("visible.Contains(cellKey)", StringComparison.Ordinal), "explored fog mask should keep current visible cells so circular edge feather does not expose unknown-color holes");
}

static void WorldSiteGridExplorationStatePersistsPositionAndMemory()
{
    WorldSiteState site = new()
    {
        SiteId = "test_site",
        Exploration = new WorldSiteExplorationState
        {
            CurrentCellX = 2,
            CurrentCellY = 3,
            CurrentCellHeight = 1,
            AlertLevel = 2
        }
    };
    site.Exploration.RevealedCellKeys.Add("2:3:1");
    site.Exploration.VisitedCellKeys.Add("1:3:1");
    site.Exploration.RevealedPointIds.Add("broken_cart");
    site.Exploration.ResolvedPointIds.Add("drain_entry");

    AssertEqual(2, site.Exploration.CurrentCellX, "exploration should persist current grid x");
    AssertEqual(3, site.Exploration.CurrentCellY, "exploration should persist current grid y");
    AssertEqual(1, site.Exploration.CurrentCellHeight, "exploration should persist current grid height");
    AssertTrue(site.Exploration.RevealedCellKeys.Contains("2:3:1"), "exploration should persist revealed cells");
    AssertTrue(site.Exploration.VisitedCellKeys.Contains("1:3:1"), "exploration should persist visited cells");
    AssertTrue(site.Exploration.RevealedPointIds.Contains("broken_cart"), "exploration should persist revealed point ids");
    AssertTrue(site.Exploration.ResolvedPointIds.Contains("drain_entry"), "exploration should persist resolved point ids");
}

static void WorldSiteGridExplorationUsesBattleGridPathingOutsideBattleTurns()
{
    BattleGridMap gridMap = new();
    for (int x = 0; x <= 2; x++)
    {
        GridCellSurface surface = gridMap.GetOrCreateSurface(new GridPosition(x, 0), 0);
        surface.AddLayer(new GridCellLayerData("test", LayerRole.Foundation, 0, true, false, false, false, true, 1, true, false, "", 0, 0, 0, 0));
    }

    gridMap.RebuildTopSurfaceIndex();
    WorldSiteExplorationState exploration = new() { CurrentCellX = 0, CurrentCellY = 0, CurrentCellHeight = 0 };

    bool moved = WorldSiteExplorationService.TryMoveParty(
        exploration,
        gridMap,
        new GridPosition(2, 0),
        out IReadOnlyList<GridSurfacePosition> path,
        out string failureReason);

    AssertTrue(moved, $"exploration should move through walkable BattleGridMap cells failure={failureReason}");
    AssertEqual(3, path.Count, "exploration path should include start, middle, and destination");
    AssertEqual(2, exploration.CurrentCellX, "exploration should update current x after movement");
    AssertEqual(0, exploration.CurrentCellY, "exploration should update current y after movement");
    AssertTrue(exploration.VisitedCellKeys.Contains("2:0:0"), "exploration should mark destination as visited");

    string worldSiteRoot = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.cs"));
    AssertTrue(worldSiteRoot.Contains("WorldSiteRuntimeMode.Exploration", StringComparison.Ordinal), "WorldSiteRoot should expose a site exploration runtime mode");
    AssertTrue(worldSiteRoot.Contains("TryHandleSiteExplorationInput", StringComparison.Ordinal), "WorldSiteRoot should route non-battle input through exploration before management drag behavior");
    AssertTrue(!ExtractMethodBlock(worldSiteRoot, "private bool TryHandleSiteExplorationInput").Contains("_turnController", StringComparison.Ordinal), "exploration input must not use battle turn controller or AP");
}

static void WorldSiteRootRoutesAuthoredExplorationPointActions()
{
    string worldSiteRoot = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.cs"));

    AssertTrue(
        worldSiteRoot.Contains("TryAppendSiteExplorationPointActions", StringComparison.Ordinal),
        "WorldSiteRoot should append authored exploration point actions during active site exploration");
    AssertTrue(
        worldSiteRoot.Contains("ExecuteSiteExplorationPointAction", StringComparison.Ordinal),
        "WorldSiteRoot should route point action button presses through a dedicated executor");

    string executeMethod = ExtractMethodBlock(worldSiteRoot, "private void ExecuteSiteExplorationPointAction");
    AssertTrue(
        executeMethod.Contains("WorldSiteExplorationService.ApplyActionResult", StringComparison.Ordinal),
        "point action execution should apply authored memory and reveal effects at runtime");
}

static void WorldSiteRootGatesHostileGarrisonTextBySiteIntel()
{
    string worldSiteRoot = File.ReadAllText(Path.Combine("src", "Presentation", "World", "Sites", "WorldSiteRoot.cs"));
    string overviewMethod = ExtractMethodBlock(worldSiteRoot, "private string BuildSiteOverview");
    string garrisonMethod = ExtractMethodBlock(worldSiteRoot, "private void RefreshGarrisonList");

    AssertTrue(
        overviewMethod.Contains("WorldSiteIntelService.BuildCurrentView", StringComparison.Ordinal),
        "site overview should build a site intel view before displaying garrison text");
    AssertTrue(
        overviewMethod.Contains("BuildSiteGarrisonOverviewText(site, intelView)", StringComparison.Ordinal),
        "site overview should route displayed garrison count through the intel-gated helper");
    AssertTrue(
        !overviewMethod.Contains("site.Garrison.Sum", StringComparison.Ordinal),
        "site overview should not directly sum exact hostile garrison counts for display");
    AssertTrue(
        garrisonMethod.Contains("WorldSiteIntelService.BuildCurrentView", StringComparison.Ordinal),
        "site garrison panel should build a site intel view");
    AssertTrue(
        garrisonMethod.Contains("AddSiteGarrisonLines(_siteGarrisonList, site, intelView)", StringComparison.Ordinal),
        "site garrison panel should route rows through the intel-gated helper");
    AssertTrue(
        !garrisonMethod.Contains("foreach (GarrisonState garrison in site.Garrison)", StringComparison.Ordinal),
        "site garrison panel should not unconditionally iterate exact hostile garrison details");
}

static void WorldSiteDeploymentUsesKnownEntrancesBeforeDesiredApproachDirection()
{
    string preparerSource = File.ReadAllText(Path.Combine("src", "Application", "World", "WorldSiteBattleDeploymentPreparer.cs"));
    string resolveForceEntrance = ExtractMethodBlock(preparerSource, "private static BattleEntranceRequest ResolveForceEntrance");
    string preparer = NormalizeWhitespace(preparerSource);

    AssertTrue(
        !resolveForceEntrance.Contains("return desiredDirection == WorldSiteAttackDirection.Any", StringComparison.Ordinal),
        "ResolveForceEntrance should return a known force entrance candidate instead of nulling non-Any hidden desired directions");
    AssertTrue(
        resolveForceEntrance.Contains("return candidates.FirstOrDefault();", StringComparison.Ordinal),
        "ResolveForceEntrance should fall back to a known AvailableEntrances candidate when preferred/exact/Any resolution misses");
    AssertTrue(
        !preparer.Contains("WorldSiteAttackDirection deploymentDirection = entrance != null && entrance.Direction != WorldSiteAttackDirection.Any ? entrance.Direction : desiredDirection;", StringComparison.Ordinal),
        "WorldSiteBattleDeploymentPreparer should not blindly reuse desiredDirection while force entrance candidates exist");
}

static void WorldSiteExplorationBattleRequestCarriesExplorationContext()
{
    BattleStartRequest request = WorldSiteExplorationService.BuildExplorationBattleRequest(
        "bonefield",
        "warehouse",
        "",
        new GridSurfacePosition(4, 5, 1),
        alertLevel: 3,
        "res://return.tscn",
        "res://site.tscn");

    AssertEqual(BattleKind.AssaultSite, request.BattleKind, "exploration battle request should enter tactical battle through an existing battle kind for first slice");
    AssertEqual("bonefield", request.TargetSiteId, "exploration battle request should carry target site");
    AssertEqual("site_exploration:warehouse", request.EncounterId, "exploration battle request should carry point encounter id");
    AssertEqual("exploration_cell=4:5:1", request.ObjectiveIds.FirstOrDefault(), "exploration battle request should carry entry cell as stable context");
    AssertTrue(request.ObjectiveIds.Contains("exploration_alert=3"), "exploration battle request should carry alert level");
}

static void SiteExplorationTickMovesPartyByExplorationAp()
{
    BattleGridMap gridMap = BuildLineGridMap(0, 2);
    WorldSiteExplorationState exploration = new() { CurrentCellX = 0, CurrentCellY = 0, CurrentCellHeight = 0 };

    bool intentSet = WorldSiteExplorationService.TrySetPartyMoveIntent(
        exploration,
        gridMap,
        new GridPosition(2, 0),
        out IReadOnlyList<GridSurfacePosition> path,
        out string failureReason);

    AssertTrue(intentSet, $"exploration should accept a reachable movement intent failure={failureReason}");
    AssertEqual(3, path.Count, "intent path should include start, middle, and destination");

    SiteExplorationTickResult result = WorldSiteExplorationService.AdvanceTick(
        exploration,
        new WorldSiteDefinition(),
        gridMap,
        partyActionPointRegenPerTick: 1,
        partyMoveCostPerCell: 1);

    AssertTrue(result.PartyMoved, "exploration tick should move party when exploration AP covers one cell");
    AssertEqual(1, exploration.CurrentCellX, "exploration tick should move one cell, not teleport to destination");
    AssertEqual(0, exploration.CurrentCellY, "exploration tick should keep y on line path");
    AssertTrue(exploration.VisitedCellKeys.Contains("1:0:0"), "exploration tick should mark the stepped cell visited");
}

static void SiteExplorationTickMovesPatrolByRouteAp()
{
    BattleGridMap gridMap = BuildLineGridMap(3, 4);
    WorldSiteDefinition definition = new()
    {
        ExplorationPatrols =
        {
            new SiteExplorationPatrolDefinition
            {
                Id = "patrol_a",
                DisplayName = "Patrol A",
                AlertRadiusCells = 0,
                ActionPointRegenPerTick = 1,
                MoveCostPerCell = 1,
                RouteCells =
                {
                    new SiteExplorationRouteCellDefinition { CellX = 3, CellY = 0, CellHeight = 0 },
                    new SiteExplorationRouteCellDefinition { CellX = 4, CellY = 0, CellHeight = 0 }
                }
            }
        }
    };
    WorldSiteExplorationState exploration = new() { CurrentCellX = 0, CurrentCellY = 0, CurrentCellHeight = 0, IsSimulationPaused = false };

    WorldSiteExplorationService.EnsurePatrolStates(exploration, definition);
    SiteExplorationTickResult result = WorldSiteExplorationService.AdvanceTick(exploration, definition, gridMap);

    AssertTrue(result.PatrolMoved, "exploration tick should move patrol when route AP covers one cell");
    AssertEqual(4, exploration.PatrolUnits[0].CellX, "patrol should advance to next route cell");
    AssertEqual(1, exploration.PatrolUnits[0].RouteIndex, "patrol route index should advance");
}

static void SiteExplorationAlertRadiusPausesSimulation()
{
    BattleGridMap gridMap = BuildLineGridMap(0, 4);
    WorldSiteDefinition definition = new()
    {
        ExplorationPatrols =
        {
            new SiteExplorationPatrolDefinition
            {
                Id = "patrol_alert",
                DisplayName = "Alert Patrol",
                AlertRadiusCells = 2,
                ActionPointRegenPerTick = 0,
                MoveCostPerCell = 1,
                RouteCells =
                {
                    new SiteExplorationRouteCellDefinition { CellX = 4, CellY = 0, CellHeight = 0 }
                }
            }
        }
    };
    WorldSiteExplorationState exploration = new() { CurrentCellX = 2, CurrentCellY = 0, CurrentCellHeight = 0, IsSimulationPaused = false };
    WorldSiteExplorationService.EnsurePatrolStates(exploration, definition);

    SiteExplorationTickResult result = WorldSiteExplorationService.AdvanceTick(exploration, definition, gridMap);

    AssertTrue(result.Paused, "alert radius should pause exploration simulation");
    AssertEqual("patrol_alert", result.AlertPatrolId, "alert result should identify triggering patrol");
    AssertEqual(true, exploration.IsSimulationPaused, "exploration state should persist paused state");
    AssertEqual("exploration_alert_radius", exploration.PauseReason, "pause reason should be stable");
}

static void ExplorationBattleRequestCarriesPatrolTrigger()
{
    BattleStartRequest request = WorldSiteExplorationService.BuildExplorationBattleRequest(
        "bonefield",
        "warehouse",
        "bonefield_patrol_01",
        new GridSurfacePosition(4, 5, 1),
        alertLevel: 4,
        "res://return.tscn",
        "res://site.tscn");

    AssertEqual("warehouse", request.ExplorationPointId, "exploration request should carry point id explicitly");
    AssertEqual("bonefield_patrol_01", request.ExplorationTriggerPatrolId, "exploration request should carry patrol trigger explicitly");
    AssertEqual(4, request.ExplorationEntryCellX, "exploration request should carry entry x");
    AssertEqual(5, request.ExplorationEntryCellY, "exploration request should carry entry y");
    AssertEqual(1, request.ExplorationEntryCellHeight, "exploration request should carry entry height");
    AssertEqual(4, request.ExplorationAlertLevel, "exploration request should carry alert level explicitly");
    AssertTrue(request.ObjectiveIds.Contains("exploration_patrol=bonefield_patrol_01"), "exploration request should keep patrol objective compatibility");
}

static void ExplorationBattleVictoryRemovesTriggeringPatrol()
{
    const string triggerPatrolId = "bonefield_patrol_01";
    const string triggerPlacementId = "garrison:skeleton_warrior:2";
    const string otherPatrolId = "bonefield_patrol_02";
    const string otherPlacementId = "garrison:skeleton_warrior:1";

    SiteExplorationPatrolDefinition triggerPatrol = new()
    {
        Id = triggerPatrolId,
        DisplayName = "Trigger Patrol",
        UnitTypeId = StrategicWorldIds.UnitSkeletonWarrior,
        SourcePlacementId = triggerPlacementId,
        RouteCells =
        {
            new SiteExplorationRouteCellDefinition { CellX = 1, CellY = 0, CellHeight = 0 }
        }
    };
    StrategicWorldDefinition definition = new()
    {
        Id = "test_world",
        SiteDefinitions =
        {
            new WorldSiteDefinition
            {
                Id = StrategicWorldIds.SiteBonefield,
                DefaultGarrisonZoneId = "bonefield_garrison",
                DeploymentZones =
                {
                    new SiteDeploymentZoneDefinition
                    {
                        ZoneId = "bonefield_garrison",
                        ZoneKind = SiteDeploymentZoneKind.DefaultGarrison,
                        Capacity = 2,
                        Cells =
                        {
                            new Godot.Vector2I(1, 0),
                            new Godot.Vector2I(2, 0)
                        }
                    }
                },
                ExplorationPatrols =
                {
                    triggerPatrol,
                    new SiteExplorationPatrolDefinition
                    {
                        Id = otherPatrolId,
                        DisplayName = "Other Patrol",
                        UnitTypeId = StrategicWorldIds.UnitSkeletonWarrior,
                        SourcePlacementId = otherPlacementId,
                        RouteCells =
                        {
                            new SiteExplorationRouteCellDefinition { CellX = 2, CellY = 0, CellHeight = 0 }
                        }
                    }
                }
            }
        }
    };
    StrategicWorldState state = new()
    {
        PlayerFactionId = StrategicWorldIds.FactionPlayer
    };
    WorldSiteState site = new()
    {
        SiteId = StrategicWorldIds.SiteBonefield,
        Exploration = new WorldSiteExplorationState
        {
            IsSimulationPaused = true,
            PauseReason = "exploration_alert_radius",
            ActiveAlertPatrolId = triggerPatrolId
        }
    };
    site.Exploration.PatrolUnits.Add(new SiteExplorationPatrolState { PatrolId = triggerPatrolId, CellX = 1, CellY = 0, CellHeight = 0 });
    site.Exploration.PatrolUnits.Add(new SiteExplorationPatrolState { PatrolId = otherPatrolId, CellX = 2, CellY = 0, CellHeight = 0 });
    site.Garrison.Add(new GarrisonState { UnitTypeId = StrategicWorldIds.UnitSkeletonWarrior, Count = 2 });
    site.UnitPlacements.Add(new WorldSiteUnitPlacement
    {
        PlacementId = triggerPlacementId,
        UnitTypeId = StrategicWorldIds.UnitSkeletonWarrior,
        UnitIndex = 2,
        FactionId = StrategicWorldIds.FactionUndead,
        PlacementKind = WorldSiteUnitPlacementKind.Garrison,
        SourceKind = "Garrison",
        SourceId = StrategicWorldIds.SiteBonefield,
        CellX = 1,
        CellY = 0,
        CellHeight = 0
    });
    site.UnitPlacements.Add(new WorldSiteUnitPlacement
    {
        PlacementId = otherPlacementId,
        UnitTypeId = StrategicWorldIds.UnitSkeletonWarrior,
        UnitIndex = 1,
        FactionId = StrategicWorldIds.FactionUndead,
        PlacementKind = WorldSiteUnitPlacementKind.Garrison,
        SourceKind = "Garrison",
        SourceId = StrategicWorldIds.SiteBonefield,
        CellX = 2,
        CellY = 0,
        CellHeight = 0
    });
    state.SiteStates[StrategicWorldIds.SiteBonefield] = site;

    BattleStartRequest request = WorldSiteExplorationService.BuildExplorationBattleRequest(
        StrategicWorldIds.SiteBonefield,
        "",
        triggerPatrolId,
        null,
        new[] { triggerPatrol },
        new GridSurfacePosition(1, 0, 0),
        alertLevel: 2,
        "res://return.tscn",
        "res://site.tscn");
    BattleResult result = BuildVictoryResult(request, "site_exploration");
    BattleForceRequest defeatedPatrolForce = request.EnemyForces.Single(force => force.SourceId == triggerPlacementId);
    result.ForceResults.Add(new BattleForceResult
    {
        ForceId = defeatedPatrolForce.ForceId,
        SourceKind = defeatedPatrolForce.SourceKind,
        SourceId = defeatedPatrolForce.SourceId,
        UnitDefinitionId = defeatedPatrolForce.UnitDefinitionId,
        InitialCount = 1,
        SurvivedCount = 0,
        DefeatedCount = 1
    });

    WorldActionResult actionResult = new WorldBattleResultApplier().Apply(
        state,
        definition,
        request,
        result);

    AssertTrue(actionResult.Success, $"exploration encounter result should apply success message={actionResult.Message}");
    AssertTrue(site.Exploration.PatrolUnits[0].IsRemoved, "victory should remove triggering patrol from exploration state");
    AssertTrue(site.Exploration.ResolvedPointIds.Contains("patrol:bonefield_patrol_01"), "victory should record resolved patrol encounter");
    AssertEqual("exploration_encounter_resolved", site.Exploration.PauseReason, "victory should leave stable exploration pause reason");
}

static BattleGridMap BuildLineGridMap(int minX, int maxX)
{
    BattleGridMap gridMap = new();
    for (int x = minX; x <= maxX; x++)
    {
        GridCellSurface surface = gridMap.GetOrCreateSurface(new GridPosition(x, 0), 0);
        surface.AddLayer(new GridCellLayerData("test", LayerRole.Foundation, 0, true, false, false, false, true, 1, true, false, "", 0, 0, 0, 0));
    }

    gridMap.RebuildTopSurfaceIndex();
    return gridMap;
}

static void WorldSiteHoverSummaryUsesLocalResourcesAndForceCounts()
{
    StrategicWorldDefinition definition = BuildResourceDisplayNameTestDefinition();
    StrategicWorldDefinitionQueries queries = new(definition);
    StrategicWorldState state = BuildResourceDisplayNameTestState();
    WorldSiteState site = state.SiteStates[StrategicWorldIds.SiteBonefield];
    site.LocalResources.Set(StrategicWorldIds.ResourcePopulation, 5);
    site.LocalResources.Reserve(StrategicWorldIds.ResourcePopulation, 2, "bonefield:test", "test");
    site.LocalResources.Set(StrategicWorldIds.ResourceEconomy, 8);
    site.LocalResources.Set(StrategicWorldIds.ResourceStone, 12);
    site.Garrison.Add(new GarrisonState { UnitTypeId = StrategicWorldIds.UnitMilitia, Count = 4 });
    site.Garrison.Add(new GarrisonState { UnitTypeId = StrategicWorldIds.UnitPlayerKnight, Count = 1 });

    WorldSiteDefinition siteDefinition = queries.GetSite(StrategicWorldIds.SiteBonefield);
    WorldSiteHoverSummaryData summary = WorldSiteHoverSummaryPresenter.Build(queries, siteDefinition, site);

    AssertEqual("Test Quarry", summary.Title, "hover summary title should use configured site display name");
    AssertEqual("Labor 3/5　Coin 8　Granite 12", summary.ResourceText, "hover summary should use local resources and configured resource labels");
    AssertEqual("兵团 4　英雄 1", summary.ForceText, "hover summary should count non-hero troops separately from heroes");
}

static void WorldSiteHoverSummaryStaysInsideViewport()
{
    var viewport = new Godot.Vector2(1280f, 720f);
    var panelSize = new Godot.Vector2(190f, 78f);
    var rightEdgeAnchor = new Godot.Rect2(new Godot.Vector2(1240f, 240f), new Godot.Vector2(50f, 70f));

    Godot.Vector2 position = WorldSiteHoverSummaryPresenter.CalculatePanelPosition(
        rightEdgeAnchor,
        panelSize,
        viewport);

    AssertFloatEqual(1078f, position.X, 0.001f, "hover summary should clamp to the right viewport edge");
    AssertFloatEqual(154f, position.Y, 0.001f, "hover summary should prefer above the site visual");

    Godot.Vector2 topPosition = WorldSiteHoverSummaryPresenter.CalculatePanelPosition(
        new Godot.Rect2(new Godot.Vector2(60f, 24f), new Godot.Vector2(80f, 48f)),
        panelSize,
        viewport);

    AssertFloatEqual(80f, topPosition.Y, 0.001f, "hover summary should move below the site when there is no space above");
}

static void StrategicWorldForwardsMiddleMouseCameraNavigation()
{
    string strategicRoot = File.ReadAllText(Path.Combine("src", "Presentation", "World", "StrategicWorldRoot.cs"));
    AssertTrue(
        strategicRoot.Contains("TryHandleWorldCameraPointerInput(@event)", StringComparison.Ordinal),
        "strategic world root should forward pointer camera navigation before world army input");
    AssertTrue(
        strategicRoot.Contains("_worldCamera.TryHandlePointerNavigationInput(@event)", StringComparison.Ordinal),
        "strategic world root should delegate middle mouse navigation to MapCameraController");
}

static void BattleResultApplierMessagesUseConfiguredDisplayNames()
{
    StrategicWorldDefinition definition = BuildBattleResultApplierTestDefinition();
    WorldBattleResultApplier applier = new();

    BattleStartRequest assaultVictoryRequest = BuildBattleResultMessageRequest(BattleKind.AssaultSite, BattleOutcome.Victory);
    WorldActionResult assaultVictory = applier.Apply(
        new StrategicWorldService().CreateInitialState(definition),
        definition,
        assaultVictoryRequest,
        BuildVictoryResult(assaultVictoryRequest, "occupy_bonefield"));
    AssertTrue(assaultVictory.Message.Contains("Test Quarry", StringComparison.Ordinal), "assault victory message should use configured site name");
    AssertTrue(assaultVictory.Message.Contains("Deep Quarry", StringComparison.Ordinal), "assault victory message should use configured mine name");
    AssertTrue(assaultVictory.Message.Contains("Signal Spire", StringComparison.Ordinal), "assault victory message should use configured tower name");
    AssertTrue(!assaultVictory.Message.Contains("埋骨地", StringComparison.Ordinal) && !assaultVictory.Message.Contains("矿场", StringComparison.Ordinal) && !assaultVictory.Message.Contains("防御塔", StringComparison.Ordinal), "assault victory message should not hardcode default entity names");

    BattleStartRequest assaultFailureRequest = BuildBattleResultMessageRequest(BattleKind.AssaultSite, BattleOutcome.Defeat);
    WorldActionResult assaultFailure = applier.Apply(
        new StrategicWorldService().CreateInitialState(definition),
        definition,
        assaultFailureRequest,
        new BattleResult
        {
            RequestId = assaultFailureRequest.RequestId,
            BattleKind = assaultFailureRequest.BattleKind,
            Outcome = BattleOutcome.Defeat
        });
    AssertTrue(assaultFailure.Message.Contains("Test Quarry", StringComparison.Ordinal), "assault failure message should use configured site name");
    AssertTrue(!assaultFailure.Message.Contains("埋骨地", StringComparison.Ordinal), "assault failure message should not hardcode default site name");

    BattleStartRequest defenseVictoryRequest = BuildBattleResultMessageRequest(BattleKind.DefenseRaid, BattleOutcome.Victory);
    WorldActionResult defenseVictory = applier.Apply(
        new StrategicWorldService().CreateInitialState(definition),
        definition,
        defenseVictoryRequest,
        BuildVictoryResult(defenseVictoryRequest, "defend_bonefield"));
    AssertTrue(defenseVictory.Message.Contains("Test Quarry", StringComparison.Ordinal), "defense victory message should use configured site name");
    AssertTrue(defenseVictory.Message.Contains("Ash Court", StringComparison.Ordinal), "defense victory message should use configured attacker faction name");
    AssertTrue(!defenseVictory.Message.Contains("埋骨地", StringComparison.Ordinal) && !defenseVictory.Message.Contains("亡灵", StringComparison.Ordinal), "defense victory message should not hardcode default site or faction names");

    BattleStartRequest defenseFailureRequest = BuildBattleResultMessageRequest(BattleKind.DefenseRaid, BattleOutcome.Defeat);
    WorldActionResult defenseFailure = applier.Apply(
        new StrategicWorldService().CreateInitialState(definition),
        definition,
        defenseFailureRequest,
        new BattleResult
        {
            RequestId = defenseFailureRequest.RequestId,
            BattleKind = defenseFailureRequest.BattleKind,
            Outcome = BattleOutcome.Defeat
        });
    AssertTrue(defenseFailure.Message.Contains("Test Quarry", StringComparison.Ordinal), "defense failure message should use configured site name");
    AssertTrue(defenseFailure.Message.Contains("Ash Court", StringComparison.Ordinal), "defense failure message should use configured attacker faction name");
    AssertTrue(!defenseFailure.Message.Contains("埋骨地", StringComparison.Ordinal) && !defenseFailure.Message.Contains("亡灵", StringComparison.Ordinal), "defense failure message should not hardcode default site or faction names");
}

static void BattleUnitFactoryKeepsDefinitionCachesShared()
{
    string factory = File.ReadAllText(Path.Combine("src", "Presentation", "Battle", "Entities", "BattleUnitFactory.cs"));

    AssertTrue(
        factory.Contains("SharedDefinitions", StringComparison.Ordinal),
        "battle unit definitions should be cached in a shared resident metadata cache");
    AssertTrue(
        factory.Contains("SharedDefinitionPathIndex", StringComparison.Ordinal),
        "nested unit definition path index should be shared instead of rebuilt per scene");
    AssertTrue(
        !factory.Contains("private readonly Dictionary<string, BattleUnitDefinition> _definitions", StringComparison.Ordinal),
        "per-scene unit definition cache rebuilds cause world detail clicks to rescan unit resources");
}

static void BattleResultApplierUsesSurvivorCountsWhenGarrisoningAssaultArmy()
{
    StrategicWorldDefinition definition = BuildBattleResultApplierTestDefinition();
    StrategicWorldState state = new StrategicWorldService().CreateInitialState(definition);
    WorldSiteState targetSite = state.SiteStates[StrategicWorldIds.SiteBonefield];
    targetSite.Garrison.Clear();
    targetSite.Garrison.Add(new GarrisonState { UnitTypeId = StrategicWorldIds.UnitSkeletonWarrior, Count = 1 });

    WorldArmyState army = new()
    {
        ArmyId = "assault:survivor-test",
        OwnerFactionId = state.PlayerFactionId,
        SourceSiteId = StrategicWorldIds.SitePlayerCamp,
        TargetSiteId = StrategicWorldIds.SiteBonefield,
        Status = WorldArmyStatus.Attacking,
        Intent = WorldArmyIntent.AssaultSite
    };
    army.GarrisonUnits.Add(new GarrisonState { UnitTypeId = StrategicWorldIds.UnitMilitia, Count = 3 });
    state.ArmyStates[army.ArmyId] = army;

    BattleStartRequest request = new()
    {
        RequestId = "assault-survivor-request",
        BattleKind = BattleKind.AssaultSite,
        SourceArmyId = army.ArmyId,
        TargetSiteId = StrategicWorldIds.SiteBonefield,
        AttackerFactionId = state.PlayerFactionId,
        DefenderFactionId = StrategicWorldIds.FactionUndead
    };
    request.ObjectiveIds.Add("occupy_bonefield");
    request.PlayerForces.Add(new BattleForceRequest
    {
        ForceId = "player:militia",
        SourceKind = "PlayerArmy",
        SourceId = army.ArmyId,
        UnitDefinitionId = StrategicWorldIds.UnitMilitia,
        Count = 3,
        FactionId = state.PlayerFactionId
    });
    request.EnemyForces.Add(new BattleForceRequest
    {
        ForceId = "defender:skeleton",
        SourceKind = "DefenderSite",
        SourceId = StrategicWorldIds.SiteBonefield,
        UnitDefinitionId = StrategicWorldIds.UnitSkeletonWarrior,
        Count = 1,
        FactionId = StrategicWorldIds.FactionUndead
    });

    BattleResult result = BuildVictoryResult(request, "occupy_bonefield");
    result.ForceResults.Add(new BattleForceResult
    {
        SourceKind = "PlayerArmy",
        SourceId = army.ArmyId,
        UnitDefinitionId = StrategicWorldIds.UnitMilitia,
        InitialCount = 3,
        SurvivedCount = 1,
        DefeatedCount = 2
    });
    result.ForceResults.Add(new BattleForceResult
    {
        SourceKind = "DefenderSite",
        SourceId = StrategicWorldIds.SiteBonefield,
        UnitDefinitionId = StrategicWorldIds.UnitSkeletonWarrior,
        InitialCount = 1,
        SurvivedCount = 0,
        DefeatedCount = 1
    });

    new WorldBattleResultApplier().Apply(state, definition, request, result);

    AssertEqual(1, targetSite.Garrison.Where(item => item.UnitTypeId == StrategicWorldIds.UnitMilitia).Sum(item => item.Count), "only surviving attacker units should garrison captured site");
    AssertEqual(0, army.GarrisonUnits.Sum(item => item.Count), "assault army should be emptied after survivor transfer");
}

static void BattleResultApplierKeepsSurvivingDefendingGarrisonAfterDefenseVictory()
{
    StrategicWorldDefinition definition = BuildBattleResultApplierTestDefinition();
    StrategicWorldState state = new StrategicWorldService().CreateInitialState(definition);
    WorldSiteState targetSite = state.SiteStates[StrategicWorldIds.SiteBonefield];
    targetSite.OwnerFactionId = state.PlayerFactionId;
    targetSite.ControlState = SiteControlState.PlayerHeld;
    targetSite.Garrison.Clear();
    targetSite.Garrison.Add(new GarrisonState { UnitTypeId = StrategicWorldIds.UnitMilitia, Count = 4 });

    BattleStartRequest request = new()
    {
        RequestId = "defense-garrison-survivor-request",
        BattleKind = BattleKind.DefenseRaid,
        TargetSiteId = StrategicWorldIds.SiteBonefield,
        AttackerFactionId = StrategicWorldIds.FactionUndead,
        DefenderFactionId = state.PlayerFactionId
    };
    request.ObjectiveIds.Add("defend_bonefield");
    request.PlayerForces.Add(new BattleForceRequest
    {
        ForceId = "garrison:militia",
        SourceKind = "Garrison",
        SourceId = StrategicWorldIds.SiteBonefield,
        UnitDefinitionId = StrategicWorldIds.UnitMilitia,
        Count = 4,
        FactionId = state.PlayerFactionId
    });

    BattleResult result = BuildVictoryResult(request, "defend_bonefield");
    result.ForceResults.Add(new BattleForceResult
    {
        ForceId = "garrison:militia",
        SourceKind = "Garrison",
        SourceId = StrategicWorldIds.SiteBonefield,
        UnitDefinitionId = StrategicWorldIds.UnitMilitia,
        InitialCount = 4,
        SurvivedCount = 2,
        DefeatedCount = 2
    });

    new WorldBattleResultApplier().Apply(state, definition, request, result);

    AssertEqual(2, targetSite.Garrison.Where(item => item.UnitTypeId == StrategicWorldIds.UnitMilitia).Sum(item => item.Count), "defending site garrison should lose only defeated units");
}

static StrategicWorldDefinition BuildBattleResultApplierTestDefinition()
{
    return new StrategicWorldDefinition
    {
        Id = "battle-result-applier-test",
        PlayerFactionId = StrategicWorldIds.FactionPlayer,
        FactionDefinitions =
        {
            new FactionDefinition { Id = StrategicWorldIds.FactionPlayer, DisplayName = "Guild" },
            new FactionDefinition { Id = StrategicWorldIds.FactionUndead, DisplayName = "Ash Court" }
        },
        FacilityDefinitions =
        {
            new FacilityDefinition { Id = StrategicWorldIds.FacilityMine, DisplayName = "Deep Quarry" },
            new FacilityDefinition { Id = StrategicWorldIds.FacilityDefenseTower, DisplayName = "Signal Spire" }
        },
        SiteDefinitions =
        {
            BuildBattleResultApplierTestSite(StrategicWorldIds.SitePlayerCamp, StrategicWorldIds.FactionPlayer, SiteControlState.PlayerHeld),
            BuildBattleResultApplierTestSite(StrategicWorldIds.SiteBonefield, StrategicWorldIds.FactionUndead, SiteControlState.Hostile)
        }
    };
}

static BattleStartRequest BuildBattleResultMessageRequest(BattleKind kind, BattleOutcome outcome)
{
    BattleStartRequest request = new()
    {
        RequestId = $"message:{kind}:{outcome}",
        BattleKind = kind,
        TargetSiteId = StrategicWorldIds.SiteBonefield,
        AttackerFactionId = kind == BattleKind.DefenseRaid ? StrategicWorldIds.FactionUndead : StrategicWorldIds.FactionPlayer,
        DefenderFactionId = kind == BattleKind.DefenseRaid ? StrategicWorldIds.FactionPlayer : StrategicWorldIds.FactionUndead
    };
    request.ObjectiveIds.Add(kind == BattleKind.DefenseRaid ? "defend_bonefield" : "occupy_bonefield");
    return request;
}

static StrategicWorldDefinition BuildResourceDisplayNameTestDefinition()
{
    return new StrategicWorldDefinition
    {
        Id = "resource-display-name-test",
        PlayerFactionId = StrategicWorldIds.FactionPlayer,
        FactionDefinitions =
        {
            new FactionDefinition { Id = StrategicWorldIds.FactionPlayer, DisplayName = "Guild" },
            new FactionDefinition { Id = StrategicWorldIds.FactionUndead, DisplayName = "Ash Court" }
        },
        ResourceDefinitions =
        {
            new ResourceDefinition { Id = StrategicWorldIds.ResourcePopulation, DisplayName = "Labor" },
            new ResourceDefinition { Id = StrategicWorldIds.ResourceStone, DisplayName = "Granite" },
            new ResourceDefinition { Id = StrategicWorldIds.ResourceEconomy, DisplayName = "Coin" }
        },
        FacilityDefinitions =
        {
            new FacilityDefinition { Id = StrategicWorldIds.FacilityMine, DisplayName = "Deep Quarry" },
            new FacilityDefinition { Id = StrategicWorldIds.FacilityDefenseTower, DisplayName = "Signal Spire" }
        },
        SiteDefinitions =
        {
            new WorldSiteDefinition
            {
                Id = StrategicWorldIds.SitePlayerCamp,
                DisplayName = "Forward Camp",
                InitialOwnerFactionId = StrategicWorldIds.FactionPlayer,
                InitialControlState = SiteControlState.PlayerHeld
            },
            new WorldSiteDefinition
            {
                Id = StrategicWorldIds.SiteBonefield,
                DisplayName = "Test Quarry",
                InitialOwnerFactionId = StrategicWorldIds.FactionPlayer,
                InitialControlState = SiteControlState.PlayerHeld
            },
            new WorldSiteDefinition
            {
                Id = StrategicWorldIds.SiteGraveyard,
                DisplayName = "Ash Gate",
                InitialOwnerFactionId = StrategicWorldIds.FactionUndead,
                InitialControlState = SiteControlState.Hostile
            }
        },
        ActionDefinitions =
        {
            new WorldActionDefinition
            {
                Id = StrategicWorldIds.ActionBuildMine,
                DisplayName = "Build Test Mine",
                Scope = WorldActionScope.Site,
                Costs = { new ResourceAmountDefinition(StrategicWorldIds.ResourcePopulation, 1) }
            },
            new WorldActionDefinition
            {
                Id = StrategicWorldIds.ActionBuildDefenseTower,
                DisplayName = "Build Test Tower",
                Scope = WorldActionScope.Site
            },
            new WorldActionDefinition
            {
                Id = StrategicWorldIds.ActionTrainMilitia,
                DisplayName = "Train Test Militia",
                Scope = WorldActionScope.Site
            },
            new WorldActionDefinition
            {
                Id = StrategicWorldIds.ActionAutoResolveRaid,
                DisplayName = "Auto Resolve Test Raid",
                Scope = WorldActionScope.Threat
            },
            new WorldActionDefinition
            {
                Id = "test_economy_cost_action",
                DisplayName = "Spend Coin",
                Scope = WorldActionScope.Site,
                Costs = { new ResourceAmountDefinition(StrategicWorldIds.ResourceEconomy, 5) }
            }
        }
    };
}

static StrategicWorldState BuildResourceDisplayNameTestState()
{
    return new StrategicWorldState
    {
        PlayerFactionId = StrategicWorldIds.FactionPlayer,
        SiteStates =
        {
            [StrategicWorldIds.SitePlayerCamp] = new WorldSiteState
            {
                SiteId = StrategicWorldIds.SitePlayerCamp,
                OwnerFactionId = StrategicWorldIds.FactionPlayer,
                ControlState = SiteControlState.PlayerHeld,
                SiteMode = WorldSiteMode.Peacetime
            },
            [StrategicWorldIds.SiteBonefield] = new WorldSiteState
            {
                SiteId = StrategicWorldIds.SiteBonefield,
                OwnerFactionId = StrategicWorldIds.FactionPlayer,
                ControlState = SiteControlState.PlayerHeld,
                SiteMode = WorldSiteMode.Peacetime
            },
            [StrategicWorldIds.SiteGraveyard] = new WorldSiteState
            {
                SiteId = StrategicWorldIds.SiteGraveyard,
                OwnerFactionId = StrategicWorldIds.FactionUndead,
                ControlState = SiteControlState.Hostile,
                SiteMode = WorldSiteMode.Peacetime
            }
        }
    };
}

static StrategicWorldState BuildThreatAutoResolveState(int militia, int towers)
{
    StrategicWorldState state = BuildResourceDisplayNameTestState();
    WorldSiteState site = state.SiteStates[StrategicWorldIds.SiteBonefield];
    site.PendingThreatIds.Add("threat:auto");
    if (militia > 0)
    {
        site.Garrison.Add(new GarrisonState
        {
            UnitTypeId = StrategicWorldIds.UnitMilitia,
            Count = militia
        });
    }

    for (int index = 0; index < towers; index++)
    {
        site.Facilities.Add(new FacilityInstance
        {
            InstanceId = $"tower:auto:{index}",
            FacilityId = StrategicWorldIds.FacilityDefenseTower,
            SiteId = StrategicWorldIds.SiteBonefield,
            State = FacilityState.Active
        });
    }

    site.Facilities.Add(new FacilityInstance
    {
        InstanceId = "mine:auto",
        FacilityId = StrategicWorldIds.FacilityMine,
        SiteId = StrategicWorldIds.SiteBonefield,
        State = FacilityState.Active
    });

    state.ThreatPlans["threat:auto"] = new EnemyThreatPlan
    {
        Id = "threat:auto",
        SourceSiteId = StrategicWorldIds.SiteGraveyard,
        TargetSiteId = StrategicWorldIds.SiteBonefield,
        Stage = ThreatStage.Attacking
    };
    return state;
}

static WorldSiteDefinition BuildBattleResultApplierTestSite(
    string siteId,
    string factionId,
    SiteControlState controlState)
{
    SiteDeploymentZoneDefinition zone = new()
    {
        ZoneId = WorldSiteDeploymentService.DefaultGarrisonZoneId,
        ZoneKind = SiteDeploymentZoneKind.DefaultGarrison,
        Capacity = 12
    };
    for (int i = 0; i < zone.Capacity; i++)
    {
        zone.Cells.Add(new Godot.Vector2I(i, 0));
    }

    return new WorldSiteDefinition
    {
        Id = siteId,
        DisplayName = siteId == StrategicWorldIds.SiteBonefield ? "Test Quarry" : "Forward Camp",
        InitialOwnerFactionId = factionId,
        InitialControlState = controlState,
        DefaultGarrisonZoneId = zone.ZoneId,
        DeploymentZones = { zone }
    };
}

static BattleResult BuildVictoryResult(BattleStartRequest request, string objectiveId)
{
    BattleResult result = new()
    {
        RequestId = request.RequestId,
        ContextId = request.ContextId,
        BattleKind = request.BattleKind,
        Outcome = BattleOutcome.Victory
    };
    result.ObjectiveResults.Add(new BattleObjectiveResult
    {
        ObjectiveId = objectiveId,
        State = BattleObjectiveState.Succeeded
    });
    return result;
}

static void UnitDisplayNameTranslationReportQuality()
{
    string reportPath = Path.Combine("assets", "battle", "units", "_display_name_translation_report.json");
    using JsonDocument report = JsonDocument.Parse(File.ReadAllText(reportPath));
    JsonElement summary = report.RootElement.GetProperty("summary");

    int lowConfidenceCount = summary.GetProperty("lowConfidenceCount").GetInt32();
    int duelystSourceNameCount = summary.GetProperty("duelystSourceNameCount").GetInt32();

    AssertTrue(
        lowConfidenceCount <= 120,
        $"translation report should leave only a bounded manual review queue, actual={lowConfidenceCount}");
    AssertTrue(
        duelystSourceNameCount >= 400,
        $"translation report should use Duelyst source names for most assets, actual={duelystSourceNameCount}");
}

static void Run(string name, Action test)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
        Environment.ExitCode = 1;
    }
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{message}: expected={expected} actual={actual}");
    }
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertFloatEqual(float expected, float actual, float tolerance, string message)
{
    if (MathF.Abs(expected - actual) > tolerance)
    {
        throw new InvalidOperationException($"{message}: expected={expected} actual={actual}");
    }
}

static string ExtractMethodBlock(string source, string methodSignature)
{
    int signatureIndex = source.IndexOf(methodSignature, StringComparison.Ordinal);
    if (signatureIndex < 0)
    {
        throw new InvalidOperationException($"missing method signature: {methodSignature}");
    }

    int braceIndex = source.IndexOf('{', signatureIndex);
    if (braceIndex < 0)
    {
        throw new InvalidOperationException($"missing method body: {methodSignature}");
    }

    int depth = 0;
    for (int i = braceIndex; i < source.Length; i++)
    {
        if (source[i] == '{')
        {
            depth++;
        }
        else if (source[i] == '}')
        {
            depth--;
            if (depth == 0)
            {
                return source[braceIndex..(i + 1)];
            }
        }
    }

    throw new InvalidOperationException($"unterminated method body: {methodSignature}");
}

static string NormalizeWhitespace(string source)
{
    return string.Join(" ", source.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}

static void AssertSequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string message)
{
    if (expected.Count != actual.Count)
    {
        throw new InvalidOperationException($"{message}: expectedCount={expected.Count} actualCount={actual.Count}");
    }

    for (int index = 0; index < expected.Count; index++)
    {
        if (!EqualityComparer<T>.Default.Equals(expected[index], actual[index]))
        {
            throw new InvalidOperationException($"{message}: index={index} expected={expected[index]} actual={actual[index]}");
        }
    }
}
