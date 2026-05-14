using Rpg.Presentation.Battle.Actions;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Debug;
using Rpg.Presentation.Battle.Feedback;
using Rpg.Presentation.Battle.Flow;
using Rpg.Presentation.Battle.Preview;
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
Run("unit audio definition resolves cue variants deterministically", UnitAudioDefinitionResolvesCueVariants);
Run("starter battle unit definitions reference audio profiles", StarterUnitDefinitionsReferenceAudioProfiles);
Run("starter audio migration is mapped from source visuals", StarterAudioMigrationUsesSourceVisuals);
Run("battle unit display names use resource label plus two digit instance index", BattleUnitDisplayNamesUseIndexedResourceLabel);
Run("starter unit display names use source visual translations", StarterUnitDisplayNamesUseSourceVisualTranslations);
Run("world unit labels resolve through battle unit definitions", WorldUnitLabelsResolveThroughBattleDefinitions);
Run("world resource and faction labels resolve through strategic definitions", WorldResourceAndFactionLabelsResolveThroughDefinitions);
Run("world action resource text uses custom resource display names", WorldActionResourceTextUsesCustomDisplayNames);
Run("world action non-population shortage uses custom resource display name", WorldActionNonPopulationShortageUsesCustomDisplayName);
Run("world action blank resource display name falls back to id", WorldActionBlankResourceDisplayNameFallsBackToId);
Run("world opportunity reward text uses custom resource display name", WorldOpportunityRewardTextUsesCustomResourceDisplayName);
Run("world tick production text uses custom resource display names", WorldTickProductionTextUsesCustomDisplayNames);
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
        !result.Messages.Any(message => message.Contains("石材 +2", StringComparison.Ordinal)),
        "mine production message should not hardcode the default stone label");
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
        SiteDefinitions =
        {
            BuildBattleResultApplierTestSite(StrategicWorldIds.SitePlayerCamp, StrategicWorldIds.FactionPlayer, SiteControlState.PlayerHeld),
            BuildBattleResultApplierTestSite(StrategicWorldIds.SiteBonefield, StrategicWorldIds.FactionUndead, SiteControlState.Hostile)
        }
    };
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
        SiteDefinitions =
        {
            new WorldSiteDefinition
            {
                Id = StrategicWorldIds.SiteBonefield,
                DisplayName = "Test Quarry",
                InitialOwnerFactionId = StrategicWorldIds.FactionPlayer,
                InitialControlState = SiteControlState.PlayerHeld
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
            [StrategicWorldIds.SiteBonefield] = new WorldSiteState
            {
                SiteId = StrategicWorldIds.SiteBonefield,
                OwnerFactionId = StrategicWorldIds.FactionPlayer,
                ControlState = SiteControlState.PlayerHeld,
                SiteMode = WorldSiteMode.Peacetime
            }
        }
    };
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
