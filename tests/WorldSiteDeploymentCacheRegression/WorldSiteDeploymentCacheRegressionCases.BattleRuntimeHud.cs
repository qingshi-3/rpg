using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;

internal static partial class WorldSiteDeploymentCacheRegressionCases
{
internal static void BattleRuntimePresentationMapsStrategicParticipantActorIds()
{
    BattleForceRequest heroForce = new()
    {
        ForceId = "strategic:expedition_0001:f1_windbladecommander",
        UnitDefinitionId = "f1_windbladecommander",
        StrategicParticipantId = "strategic_participant:expedition_0001:hero_archer_captain:corps_0002",
        Count = 1
    };
    BattleForceRequest corpsForce = new()
    {
        ForceId = "strategic:expedition_0001:f1_backlinearcher",
        UnitDefinitionId = "f1_backlinearcher",
        StrategicParticipantId = "strategic_participant:expedition_0001:hero_archer_captain:corps_0002",
        Count = 3
    };
    BattleForceRequest enemyForce = new()
    {
        ForceId = "bonefield:f6_spiritwolf",
        UnitDefinitionId = "f6_spiritwolf",
        Count = 2
    };

    IReadOnlyDictionary<string, string> map = BattleRuntimeActorIdentity.BuildPresentationEntityToRuntimeActorMap(
        new[] { heroForce, corpsForce },
        new[] { enemyForce });

    AssertEqual(
        "strategic_participant:expedition_0001:hero_archer_captain:corps_0002:1",
        map["strategic:expedition_0001:f1_windbladecommander:1"],
        "first force in a strategic participant should map to runtime actor 1");
    AssertEqual(
        "strategic_participant:expedition_0001:hero_archer_captain:corps_0002:2",
        map["strategic:expedition_0001:f1_backlinearcher:1"],
        "second visual force sharing the participant should continue runtime actor numbering");
    AssertEqual(
        "strategic_participant:expedition_0001:hero_archer_captain:corps_0002:4",
        map["strategic:expedition_0001:f1_backlinearcher:3"],
        "strategic participant numbering should cover every displayed corps entity");
    AssertEqual(
        "bonefield:f6_spiritwolf:1",
        map["bonefield:f6_spiritwolf:1"],
        "legacy non-strategic forces should keep their existing runtime actor id");
}

internal static void BattleRuntimePresentationMapsStrategicSnapshotEnemyActorIds()
{
    BattleStartRequest request = new();
    request.PlayerForces.Add(new BattleForceRequest
    {
        ForceId = "strategic:expedition_0001:f1_windbladecommander",
        UnitDefinitionId = "f1_windbladecommander",
        StrategicParticipantId = "strategic_participant:expedition_0001:hero_archer_captain:corps_0002",
        Count = 1
    });
    request.PlayerForces.Add(new BattleForceRequest
    {
        ForceId = "strategic:expedition_0001:f1_backlinearcher",
        UnitDefinitionId = "f1_backlinearcher",
        StrategicParticipantId = "strategic_participant:expedition_0001:hero_archer_captain:corps_0002",
        Count = 3
    });
    request.EnemyForces.Add(new BattleForceRequest
    {
        ForceId = "bonefield:f6_draugarlord",
        SourceKind = "DefenderSite",
        SourceId = "bonefield",
        UnitDefinitionId = "f6_draugarlord",
        Count = 2
    });
    request.EnemyForces.Add(new BattleForceRequest
    {
        ForceId = "bonefield:f6_spiritwolf",
        SourceKind = "DefenderSite",
        SourceId = "bonefield",
        UnitDefinitionId = "f6_spiritwolf",
        Count = 2
    });
    request.EnemyForces.Add(new BattleForceRequest
    {
        ForceId = "bonefield:f4_skullcaster",
        SourceKind = "DefenderSite",
        SourceId = "bonefield",
        UnitDefinitionId = "f4_skullcaster",
        Count = 2
    });

    BattleStartSnapshot launchedSnapshot = new();
    AddSnapshotGroups(
        launchedSnapshot,
        "strategic_participant:expedition_0001:hero_archer_captain:corps_0002",
        4);
    AddSnapshotGroups(launchedSnapshot, "bonefield", 6);

    IReadOnlyDictionary<string, string> map =
        BattleRuntimeActorIdentity.BuildPresentationEntityToRuntimeActorMap(request, launchedSnapshot);

    AssertEqual(
        "strategic_participant:expedition_0001:hero_archer_captain:corps_0002:4",
        map["strategic:expedition_0001:f1_backlinearcher:3"],
        "snapshot-backed mapping should preserve player strategic participant runtime numbering");
    AssertEqual(
        "bonefield:1",
        map["bonefield:f6_draugarlord:1"],
        "first defender visual entity should map to the first launched site-source runtime actor");
    AssertEqual(
        "bonefield:3",
        map["bonefield:f6_spiritwolf:1"],
        "second defender force should continue numbering under the launched source force id");
    AssertEqual(
        "bonefield:6",
        map["bonefield:f4_skullcaster:2"],
        "all defender visual entities should resolve to Runtime actor ids emitted from the launched snapshot");
}

private static void AddSnapshotGroups(BattleStartSnapshot snapshot, string sourceForceId, int count)
{
    for (int index = 0; index < count; index++)
    {
        snapshot.BattleGroups.Add(new BattleGroupSnapshot
        {
            BattleGroupId = $"{sourceForceId}:group:{index}",
            SourceForceId = sourceForceId,
            HeroId = $"{sourceForceId}:hero:{index}",
            CorpsId = $"{sourceForceId}:corps:{index}",
            HeroDefinitionId = "test_hero",
            CorpsDefinitionId = "test_corps",
            SourceLocationId = "test_location"
        });
    }
}

internal static void BattleRuntimeHudUsesFullscreenHeroFrame()
{
    string root = ProjectRoot();
    string siteScene = File.ReadAllText(Path.Combine(
        root,
        "scenes",
        "world",
        "ui",
        "WorldSitePeacetimeHud.tscn"));

    AssertTrue(
        !siteScene.Contains("BattleRuntimeCommandPanel", StringComparison.Ordinal) &&
        !siteScene.Contains("BattleRuntimeHeroCommandList", StringComparison.Ordinal) &&
        !siteScene.Contains("BattleRuntimeCorpsCommandList", StringComparison.Ordinal) &&
        !siteScene.Contains("BattleRuntimeCombinedCommandList", StringComparison.Ordinal),
        "battle runtime must not keep a left-side command encyclopedia panel in the active HUD scene");
    AssertTrue(
        siteScene.Contains("BattleRuntimeHeroFrame", StringComparison.Ordinal) &&
        siteScene.Contains("BattleRuntimeHeroSelectorList", StringComparison.Ordinal) &&
        siteScene.Contains("BattleRuntimeHeroNameLabel", StringComparison.Ordinal) &&
        siteScene.Contains("BattleRuntimeHeroStateLabel", StringComparison.Ordinal) &&
        siteScene.Contains("BattleRuntimeHeroHealthBar", StringComparison.Ordinal) &&
        siteScene.Contains("BattleRuntimeHeroManaBar", StringComparison.Ordinal) &&
        siteScene.Contains("BattleRuntimeHeroSkillList", StringComparison.Ordinal) &&
        !siteScene.Contains("BattleRuntimeRegroupButton", StringComparison.Ordinal),
        "battle runtime HUD should author a persistent hero frame with HP, mana, and skill slots without the removed regroup button");
    AssertTrue(
        File.Exists(Path.Combine(root, "scenes", "world", "ui", "BattleRuntimeSkillSlot.tscn")) &&
        File.Exists(Path.Combine(root, "src", "Presentation", "World", "Sites", "BattleRuntimeSkillSlot.cs")) &&
        File.Exists(Path.Combine(root, "scenes", "world", "ui", "BattleRuntimeHeroSwitchButton.tscn")) &&
        File.Exists(Path.Combine(root, "src", "Presentation", "World", "Sites", "BattleRuntimeHeroSwitchButton.cs")),
        "runtime hero and skill controls should use reusable authored scenes instead of hardcoded single buttons");
}

internal static void BattleRuntimeHudUsesReferenceDrivenMapFirstCommandFlow()
{
    string root = ProjectRoot();
    string siteScene = File.ReadAllText(Path.Combine(root, "scenes", "world", "ui", "WorldSitePeacetimeHud.tscn"));
    string skillSlotScene = File.ReadAllText(Path.Combine(root, "scenes", "world", "ui", "BattleRuntimeSkillSlot.tscn"));
    string heroSwitchScene = File.ReadAllText(Path.Combine(root, "scenes", "world", "ui", "BattleRuntimeHeroSwitchButton.tscn"));
    string summaryRowScene = File.ReadAllText(Path.Combine(root, "scenes", "world", "ui", "BattleRuntimeHeroTroopSummaryRow.tscn"));
    string nodeRefsSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "Sites", "WorldSitePeacetimeHudNodeRefs.cs"));
    string rootSource = ReadWorldSiteRootSource();
    string commandHudSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRuntimeCommandHud.cs"));
    string skillSlotSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "Sites", "BattleRuntimeSkillSlot.cs"));
    string switchButtonSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "Sites", "BattleRuntimeHeroSwitchButton.cs"));
    string selectorPresenterSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "Sites", "BattleRuntimeHeroSelectorPresenter.cs"));
    string presenterSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "Sites", "BattleRuntimeHeroFramePresenter.cs"));
    string animatedPreviewSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "Common", "BattleUnitAnimatedPreview.cs"));
    string plinthPreviewSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "Common", "BattleUnitPlinthPreview.cs"));
    string wheelPath = Path.Combine(root, "assets", "textures", "ui", "battle-runtime", "battle_runtime_command_wheel.png");
    string fantasyAtlasPath = Path.Combine(root, "assets", "textures", "ui", "fantasy-hud-generated", "fantasy_hud_modular_atlas.png");
    string pauseDecorScenePath = Path.Combine(root, "scenes", "world", "ui", "BattleRuntimePauseDecorFrame.tscn");
    string pausePanelStylePath = Path.Combine(root, "resource", "ui", "themes", "battle-runtime", "battle_runtime_pause_scroll_panel.tres");
    string pauseInnerStylePath = Path.Combine(root, "resource", "ui", "themes", "battle-runtime", "battle_runtime_pause_scroll_inner_panel.tres");
    string fantasyPanelStylePath = Path.Combine(root, "resource", "ui", "themes", "battle-runtime", "battle_runtime_fantasy_panel.tres");
    string fantasyInnerStylePath = Path.Combine(root, "resource", "ui", "themes", "battle-runtime", "battle_runtime_fantasy_inner_panel.tres");
    string fantasySlotStylePath = Path.Combine(root, "resource", "ui", "themes", "battle-runtime", "battle_runtime_fantasy_slot.tres");
    string fantasySlotSelectedStylePath = Path.Combine(root, "resource", "ui", "themes", "battle-runtime", "battle_runtime_fantasy_slot_selected.tres");
    string fantasyButtonNormalStylePath = Path.Combine(root, "resource", "ui", "themes", "battle-runtime", "battle_runtime_fantasy_button_normal.tres");
    string fantasyButtonHoverStylePath = Path.Combine(root, "resource", "ui", "themes", "battle-runtime", "battle_runtime_fantasy_button_hover.tres");
    string fantasyButtonPressedStylePath = Path.Combine(root, "resource", "ui", "themes", "battle-runtime", "battle_runtime_fantasy_button_pressed.tres");
    string fantasyBarFrameStylePath = Path.Combine(root, "resource", "ui", "themes", "battle-runtime", "battle_runtime_fantasy_bar_frame.tres");
    string fantasyBarHealthStylePath = Path.Combine(root, "resource", "ui", "themes", "battle-runtime", "battle_runtime_fantasy_bar_health_fill.tres");
    string fantasyBarManaStylePath = Path.Combine(root, "resource", "ui", "themes", "battle-runtime", "battle_runtime_fantasy_bar_mana_fill.tres");
    string pausePresentationBody = ExtractMethodBody(commandHudSource, "private void RefreshBattleRuntimeCommandPausePresentation()");
    string pauseDecorScene = File.Exists(pauseDecorScenePath) ? File.ReadAllText(pauseDecorScenePath) : "";
    string pausePanelStyle = File.Exists(pausePanelStylePath) ? File.ReadAllText(pausePanelStylePath) : "";
    string pauseInnerStyle = File.Exists(pauseInnerStylePath) ? File.ReadAllText(pauseInnerStylePath) : "";
    string fantasyPanelStyle = File.Exists(fantasyPanelStylePath) ? File.ReadAllText(fantasyPanelStylePath) : "";
    string fantasyInnerStyle = File.Exists(fantasyInnerStylePath) ? File.ReadAllText(fantasyInnerStylePath) : "";
    string fantasySlotStyle = File.Exists(fantasySlotStylePath) ? File.ReadAllText(fantasySlotStylePath) : "";
    string fantasySlotSelectedStyle = File.Exists(fantasySlotSelectedStylePath) ? File.ReadAllText(fantasySlotSelectedStylePath) : "";
    string fantasyButtonNormalStyle = File.Exists(fantasyButtonNormalStylePath) ? File.ReadAllText(fantasyButtonNormalStylePath) : "";
    string fantasyButtonHoverStyle = File.Exists(fantasyButtonHoverStylePath) ? File.ReadAllText(fantasyButtonHoverStylePath) : "";
    string fantasyButtonPressedStyle = File.Exists(fantasyButtonPressedStylePath) ? File.ReadAllText(fantasyButtonPressedStylePath) : "";
    string fantasyBarFrameStyle = File.Exists(fantasyBarFrameStylePath) ? File.ReadAllText(fantasyBarFrameStylePath) : "";
    string fantasyBarHealthStyle = File.Exists(fantasyBarHealthStylePath) ? File.ReadAllText(fantasyBarHealthStylePath) : "";
    string fantasyBarManaStyle = File.Exists(fantasyBarManaStylePath) ? File.ReadAllText(fantasyBarManaStylePath) : "";

    AssertTrue(
        !File.Exists(wheelPath),
        $"battle runtime should remove the old round command wheel texture path={wheelPath}");
    AssertTrue(File.Exists(fantasyAtlasPath), $"battle runtime fantasy HUD atlas should exist path={fantasyAtlasPath}");
    (int fantasyAtlasWidth, int fantasyAtlasHeight) = ReadPngDimensions(fantasyAtlasPath);
    AssertTrue(
        fantasyAtlasWidth == 1536 && fantasyAtlasHeight == 1024,
        $"battle runtime fantasy HUD atlas should keep the generated atlas dimensions width={fantasyAtlasWidth} height={fantasyAtlasHeight}");
    AssertTrue(
        File.Exists(fantasyPanelStylePath) &&
        File.Exists(fantasyInnerStylePath) &&
        File.Exists(fantasySlotStylePath) &&
        File.Exists(fantasySlotSelectedStylePath) &&
        File.Exists(fantasyButtonNormalStylePath) &&
        File.Exists(fantasyButtonHoverStylePath) &&
        File.Exists(fantasyButtonPressedStylePath) &&
        File.Exists(fantasyBarFrameStylePath) &&
        File.Exists(fantasyBarHealthStylePath) &&
        File.Exists(fantasyBarManaStylePath) &&
        fantasyPanelStyle.Contains("fantasy_hud_modular_atlas.png", StringComparison.Ordinal) &&
        fantasyPanelStyle.Contains("region = Rect2(36, 31, 587, 300)", StringComparison.Ordinal) &&
        fantasyInnerStyle.Contains("fantasy_hud_modular_atlas.png", StringComparison.Ordinal) &&
        fantasyInnerStyle.Contains("region = Rect2(773, 123, 226, 124)", StringComparison.Ordinal) &&
        fantasySlotStyle.Contains("region = Rect2(1166, 396, 144, 147)", StringComparison.Ordinal) &&
        fantasySlotSelectedStyle.Contains("region = Rect2(1340, 399, 144, 144)", StringComparison.Ordinal) &&
        fantasyButtonNormalStyle.Contains("region = Rect2(1342, 834, 124, 49)", StringComparison.Ordinal) &&
        fantasyButtonHoverStyle.Contains("region = Rect2(355, 386, 241, 57)", StringComparison.Ordinal) &&
        fantasyButtonPressedStyle.Contains("region = Rect2(362, 492, 229, 74)", StringComparison.Ordinal) &&
        fantasyBarFrameStyle.Contains("region = Rect2(428, 637, 422, 71)", StringComparison.Ordinal) &&
        fantasyBarHealthStyle.Contains("region = Rect2(492, 750, 327, 34)", StringComparison.Ordinal) &&
        fantasyBarManaStyle.Contains("region = Rect2(851, 750, 305, 34)", StringComparison.Ordinal),
        "battle runtime skin should slice the generated modular fantasy HUD atlas into panel, slot, button, and bar style resources");
    AssertTrue(
        !siteScene.Contains("BattleRuntimeRadialCommandMenu", StringComparison.Ordinal) &&
        !siteScene.Contains("BattleRuntimeCommandWheelFrame", StringComparison.Ordinal) &&
        !siteScene.Contains("battle_runtime_command_wheel.png", StringComparison.Ordinal),
        "battle runtime tactical-pause commands should not keep the rejected round command wheel or overlay radial menu");
    AssertTrue(
        siteScene.Contains("[node name=\"BattleRuntimeSkillCommandPanel\" type=\"PanelContainer\" parent=\"BottomCommandHost/BattleRuntimePauseDetailPanel/PauseDetailMargin/PauseDetailStack/PauseDetailBody\"]", StringComparison.Ordinal) &&
        siteScene.Contains("[node name=\"BattleRuntimeHeroSkillList\" type=\"HBoxContainer\" parent=\"BottomCommandHost/BattleRuntimePauseDetailPanel/PauseDetailMargin/PauseDetailStack/PauseDetailBody/BattleRuntimeSkillCommandPanel/SkillCommandMargin/BattleRuntimeSkillCommandStack\"]", StringComparison.Ordinal) &&
        !siteScene.Contains("BattleRuntimeRegroupButton", StringComparison.Ordinal),
        "battle runtime skills should live inside the bottom tactical-pause detail area without the removed regroup button");
    AssertTrue(
        File.Exists(pausePanelStylePath) &&
        File.Exists(pauseInnerStylePath) &&
        siteScene.Contains("BattleRuntimePauseDetailPanel", StringComparison.Ordinal) &&
        !siteScene.Contains("BattleRuntimePauseDetailFrame", StringComparison.Ordinal) &&
        siteScene.Contains("BattleRuntimePauseCommandQueueList", StringComparison.Ordinal) &&
        !siteScene.Contains("BattleRuntimePauseCollapseButton", StringComparison.Ordinal) &&
        siteScene.Contains("battle_runtime_pause_scroll_panel.tres", StringComparison.Ordinal) &&
        siteScene.Contains("battle_runtime_pause_scroll_inner_panel.tres", StringComparison.Ordinal) &&
        siteScene.Contains("battle_runtime_fantasy_bar_frame.tres", StringComparison.Ordinal) &&
        siteScene.Contains("battle_runtime_fantasy_bar_health_fill.tres", StringComparison.Ordinal) &&
        siteScene.Contains("battle_runtime_fantasy_bar_mana_fill.tres", StringComparison.Ordinal) &&
        File.Exists(pauseDecorScenePath) &&
        siteScene.Contains("BattleRuntimePauseDecorFrame.tscn", StringComparison.Ordinal) &&
        siteScene.Contains("BattleRuntimePauseBackgroundPanel", StringComparison.Ordinal) &&
        siteScene.Contains("BattleRuntimePauseDecorFrame", StringComparison.Ordinal) &&
        pauseDecorScene.Contains("fantasy_hud_modular_atlas.png", StringComparison.Ordinal) &&
        pauseDecorScene.Contains("LeftScrollRoll", StringComparison.Ordinal) &&
        !pauseDecorScene.Contains("RightScrollRoll", StringComparison.Ordinal) &&
        !pauseDecorScene.Contains("TopWoodPlaque", StringComparison.Ordinal) &&
        pauseDecorScene.Contains("region = Rect2(53, 355, 52, 227)", StringComparison.Ordinal) &&
        !pauseDecorScene.Contains("region = Rect2(262, 355, 52, 226)", StringComparison.Ordinal) &&
        !pauseDecorScene.Contains("region = Rect2(1342, 834, 124, 49)", StringComparison.Ordinal) &&
        pauseDecorScene.Contains("mouse_filter = 2", StringComparison.Ordinal) &&
        pausePanelStyle.Contains("[gd_resource type=\"StyleBoxTexture\"", StringComparison.Ordinal) &&
        pausePanelStyle.Contains("fantasy_hud_modular_atlas.png", StringComparison.Ordinal) &&
        pausePanelStyle.Contains("region = Rect2(36, 31, 587, 300)", StringComparison.Ordinal) &&
        pauseInnerStyle.Contains("[gd_resource type=\"StyleBoxTexture\"", StringComparison.Ordinal) &&
        pauseInnerStyle.Contains("fantasy_hud_modular_atlas.png", StringComparison.Ordinal) &&
        pauseInnerStyle.Contains("region = Rect2(773, 123, 226, 124)", StringComparison.Ordinal) &&
        !pauseDecorScene.Contains("battle_runtime_keyboard_panel_sheet.png", StringComparison.Ordinal) &&
        !pausePanelStyle.Contains("StyleBoxFlat", StringComparison.Ordinal) &&
        !siteScene.Contains("battle_runtime_pause_panel.tres", StringComparison.Ordinal),
        "tactical pause detail should use a stretchable fantasy parchment panel with only left-side scroll decoration from the modular atlas");
    AssertTrue(
        skillSlotScene.Contains("battle_runtime_fantasy_slot.tres", StringComparison.Ordinal) &&
        skillSlotScene.Contains("battle_runtime_fantasy_slot_selected.tres", StringComparison.Ordinal) &&
        skillSlotScene.Contains("theme_override_styles/panel = ExtResource(\"4_fantasy_slot_selected\")", StringComparison.Ordinal) &&
        skillSlotScene.Contains("NormalPanelStyle = ExtResource(\"4_fantasy_slot_selected\")", StringComparison.Ordinal) &&
        skillSlotScene.Contains("HoverPanelStyle = ExtResource(\"2_fantasy_slot\")", StringComparison.Ordinal) &&
        skillSlotScene.Contains("[node name=\"IconTexture\" type=\"TextureRect\"", StringComparison.Ordinal) &&
        skillSlotScene.Contains("IconGlyph", StringComparison.Ordinal) &&
        skillSlotSource.Contains("MouseEntered += ApplyHoverPanelStyle", StringComparison.Ordinal) &&
        skillSlotSource.Contains("MouseExited += ApplyNormalPanelStyle", StringComparison.Ordinal) &&
        skillSlotSource.Contains("AddThemeStyleboxOverride(\"panel\"", StringComparison.Ordinal) &&
        !skillSlotScene.Contains("[node name=\"Icon\" type=\"ColorRect\"", StringComparison.Ordinal) &&
        !skillSlotScene.Contains("mana_soul_item_tall_frame_normal.tres", StringComparison.Ordinal) &&
        !skillSlotScene.Contains("basic_ui_1_panel_slot.tres", StringComparison.Ordinal),
        "battle runtime skill slots should use the dark slot frame by default and switch to the gold slot frame only on hover");
    AssertTrue(
        !heroSwitchScene.Contains("battle_runtime_fantasy_slot.tres", StringComparison.Ordinal) &&
        !heroSwitchScene.Contains("battle_runtime_fantasy_slot_selected.tres", StringComparison.Ordinal) &&
        !heroSwitchScene.Contains("theme_override_styles/", StringComparison.Ordinal) &&
        heroSwitchScene.Contains("flat = true", StringComparison.Ordinal) &&
        heroSwitchScene.Contains("custom_minimum_size = Vector2(104, 118)", StringComparison.Ordinal) &&
        heroSwitchScene.Contains("position = Vector2(52, 86)", StringComparison.Ordinal) &&
        heroSwitchScene.Contains("scale = Vector2(0.38, 0.38)", StringComparison.Ordinal) &&
        heroSwitchScene.Contains("[node name=\"SelectedBackplate\" type=\"ColorRect\"", StringComparison.Ordinal) &&
        heroSwitchScene.Contains("color = Color(0.05, 0.08, 0.13, 0.64)", StringComparison.Ordinal) &&
        heroSwitchScene.Contains("[node name=\"SelectedSideMark\" type=\"ColorRect\"", StringComparison.Ordinal) &&
        heroSwitchScene.Contains("color = Color(0.55, 0.92, 1, 0.95)", StringComparison.Ordinal) &&
        heroSwitchScene.Contains("res://scenes/ui/common/BattleUnitPlinthPreview.tscn", StringComparison.Ordinal) &&
        heroSwitchScene.Contains("[node name=\"HeroPlinthPreview\"", StringComparison.Ordinal) &&
        heroSwitchScene.Contains("[node name=\"HeroNameLabel\" type=\"Label\"", StringComparison.Ordinal) &&
        !heroSwitchScene.Contains("[node name=\"Glyph\" type=\"Label\"", StringComparison.Ordinal) &&
        !heroSwitchScene.Contains("[node name=\"Status\" type=\"Label\"", StringComparison.Ordinal) &&
        !heroSwitchScene.Contains("ManaSoulItemFrameButton", StringComparison.Ordinal) &&
        !heroSwitchScene.Contains("WorldCompactActionButton", StringComparison.Ordinal),
        "battle runtime hero switch should be a frameless plinth/unit idle preview with only the secondary bottom name label");
    AssertTrue(
        switchButtonSource.Contains("BattleUnitPreviewResolver.ResolveAnimatedPreview", StringComparison.Ordinal) &&
        switchButtonSource.Contains("_heroPlinthPreview.Bind", StringComparison.Ordinal) &&
        switchButtonSource.Contains("_selectedBackplate.Visible = selected", StringComparison.Ordinal) &&
        switchButtonSource.Contains("_selectedSideMark.Visible = selected", StringComparison.Ordinal) &&
        !switchButtonSource.Contains("SetSelectionOutline", StringComparison.Ordinal) &&
        selectorPresenterSource.Contains("group.HeroBattleUnitId", StringComparison.Ordinal) &&
        !switchButtonSource.Contains("BuildShortLabel", StringComparison.Ordinal) &&
        !animatedPreviewSource.Contains("unit_body_outline.gdshader", StringComparison.Ordinal) &&
        !animatedPreviewSource.Contains("public void SetSelectionOutline(bool selected)", StringComparison.Ordinal) &&
        !plinthPreviewSource.Contains("public void SetSelectionOutline(bool selected)", StringComparison.Ordinal),
        "runtime hero switching should use a larger plinth/unit component and a cool-color selected backplate instead of a yellow outline");
    AssertTrue(
        summaryRowScene.Contains("battle_runtime_fantasy_inner_panel.tres", StringComparison.Ordinal) &&
        summaryRowScene.Contains("battle_runtime_fantasy_bar_frame.tres", StringComparison.Ordinal) &&
        summaryRowScene.Contains("battle_runtime_fantasy_bar_health_fill.tres", StringComparison.Ordinal) &&
        !summaryRowScene.Contains("ManaSoulPanelD", StringComparison.Ordinal) &&
        !summaryRowScene.Contains("ManaSoulHealthBar", StringComparison.Ordinal) &&
        !summaryRowScene.Contains("basic_ui_1_panel_slot.tres", StringComparison.Ordinal),
        "battle runtime summary rows should use the same fantasy atlas skin as the detail frame");
    AssertTrue(
        nodeRefsSource.Contains("BottomCommandHost/BattleRuntimePauseDetailPanel/PauseDetailMargin/PauseDetailStack/PauseDetailBody/BattleRuntimeSkillCommandPanel/SkillCommandMargin/BattleRuntimeSkillCommandStack/BattleRuntimeHeroSkillList", StringComparison.Ordinal) &&
        !nodeRefsSource.Contains("BattleRuntimeRegroupButton", StringComparison.Ordinal) &&
        !nodeRefsSource.Contains("OverlayHost/BattleRuntimeRadialCommandMenu", StringComparison.Ordinal) &&
        nodeRefsSource.Contains("BattleRuntimePauseDetailPanel", StringComparison.Ordinal) &&
        !nodeRefsSource.Contains("BattleRuntimeHeroPlinthPreview", StringComparison.Ordinal) &&
        !nodeRefsSource.Contains("BattleRuntimeHeroAvatarLabel", StringComparison.Ordinal),
        "world-site HUD node refs should bind the bottom skill surface without regroup or a redundant selected-hero preview");
    AssertTrue(
        pausePresentationBody.Contains("_siteBottomCommandHost.Visible = false", StringComparison.Ordinal) &&
        pausePresentationBody.Contains("_battleRuntimeSummaryBar.Visible = false", StringComparison.Ordinal) &&
        pausePresentationBody.Contains("_battleRuntimePauseDetailPanel.Visible = false", StringComparison.Ordinal) &&
        pausePresentationBody.IndexOf("if (!_battleRuntimeCommandPauseActive)", StringComparison.Ordinal) <
        pausePresentationBody.IndexOf("_siteBottomCommandHost.Visible = false", StringComparison.Ordinal) &&
        !pausePresentationBody.Contains("bool showRuntimeSummary = IsBattleRuntimeHudActive()", StringComparison.Ordinal),
        "live/default battle runtime should hide bottom summary, command, and pause-detail panels instead of showing a persistent bottom HUD");
    AssertTrue(
        pausePresentationBody.Contains("_battleRuntimePauseDetailPanel.Visible = true", StringComparison.Ordinal) &&
        pausePresentationBody.Contains("_battleRuntimeCommandBar.Visible = false", StringComparison.Ordinal) &&
        !pausePresentationBody.Contains("RefreshBattleRuntimeRadialCommandMenuPosition()", StringComparison.Ordinal),
        "tactical pause should show the bottom detail panel without refreshing a separate radial command menu");
    AssertTrue(
        !rootSource.Contains("private Control _battleRuntimeRadialCommandMenu", StringComparison.Ordinal) &&
        rootSource.Contains("private Control _battleRuntimePauseDetailPanel", StringComparison.Ordinal) &&
        !commandHudSource.Contains("private void RefreshBattleRuntimeRadialCommandMenuPosition()", StringComparison.Ordinal) &&
        !commandHudSource.Contains("BattleRuntimeRadialCommandMenuPresenter.Refresh", StringComparison.Ordinal),
        "runtime command presentation should remove the separate radial command-menu owner after moving skills into the bottom detail panel");
    AssertTrue(
        !siteScene.Contains("[node name=\"BattleRuntimeHeroAvatar\"", StringComparison.Ordinal) &&
        !siteScene.Contains("[node name=\"BattleRuntimeHeroPlinthPreview\"", StringComparison.Ordinal) &&
        siteScene.Contains("res://scenes/ui/common/BattleUnitPlinthPreview.tscn", StringComparison.Ordinal) &&
        presenterSource.Contains("skill.IconText", StringComparison.Ordinal) &&
        presenterSource.Contains("skill.IconPath", StringComparison.Ordinal) &&
        !presenterSource.Contains("BattleUnitPreviewResolver.ResolveAnimatedPreview", StringComparison.Ordinal) &&
        !presenterSource.Contains("_heroPlinthPreview", StringComparison.Ordinal) &&
        !presenterSource.Contains("BuildAvatarGlyph", StringComparison.Ordinal) &&
        skillSlotSource.Contains("GD.Load<Texture2D>(iconPath)", StringComparison.Ordinal) &&
        !rootSource.Contains("_battleRuntimeRegroupButton", StringComparison.Ordinal) &&
        !commandHudSource.Contains("OnBattleRuntimeRegroupPressed", StringComparison.Ordinal),
        "battle runtime hero frame presenter should bind skill icon textures without a redundant selected-hero preview or regroup button");
}

internal static void WorldSiteRuntimeHudBindsHeroFrameInsteadOfLeftCommandPanel()
{
    string rootSource = ReadWorldSiteRootSource();
    string siteManagementSource = File.ReadAllText(Path.Combine(
        ProjectRoot(),
        "src",
        "Presentation",
        "World",
        "Sites",
        "WorldSiteRoot.SiteManagementHud.cs"));

    AssertTrue(
        !rootSource.Contains("_battleRuntimeCommandPanel", StringComparison.Ordinal) &&
        !rootSource.Contains("_battleRuntimeHeroCommandList", StringComparison.Ordinal) &&
        !rootSource.Contains("_battleRuntimeCorpsCommandList", StringComparison.Ordinal) &&
        !rootSource.Contains("_battleRuntimeCombinedCommandList", StringComparison.Ordinal) &&
        !rootSource.Contains("SetBattleRuntimeCommandPanelVisible", StringComparison.Ordinal),
        "WorldSiteRoot should stop binding or toggling the left battle-runtime command panel");
    AssertTrue(
        siteManagementSource.Contains("_battleRuntimeHeroFrame", StringComparison.Ordinal) &&
        siteManagementSource.Contains("_battleRuntimeSummaryList", StringComparison.Ordinal) &&
        siteManagementSource.Contains("_battleRuntimeSummaryPresenter", StringComparison.Ordinal) &&
        siteManagementSource.Contains("_battleRuntimeHeroSelectorPresenter", StringComparison.Ordinal) &&
        siteManagementSource.Contains("_battleRuntimeHeroNameLabel", StringComparison.Ordinal) &&
        siteManagementSource.Contains("_battleRuntimeHeroHealthBar", StringComparison.Ordinal) &&
        siteManagementSource.Contains("_battleRuntimeHeroManaBar", StringComparison.Ordinal) &&
        siteManagementSource.Contains("_battleRuntimeHeroSkillList", StringComparison.Ordinal) &&
        !siteManagementSource.Contains("_battleRuntimeRegroupButton", StringComparison.Ordinal),
        "WorldSiteRoot should bind the authored runtime hero frame controls without the removed regroup button");
}

internal static void BattleRuntimeHudShowsHeroTroopSummaryPanel()
{
    string root = ProjectRoot();
    string siteScene = File.ReadAllText(Path.Combine(root, "scenes", "world", "ui", "WorldSitePeacetimeHud.tscn"));
    string nodeRefsSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "Sites", "WorldSitePeacetimeHudNodeRefs.cs"));
    string rootSource = ReadWorldSiteRootSource();
    string commandHudSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "Sites", "WorldSiteRoot.BattleRuntimeCommandHud.cs"));
    string siteManagementSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "Sites", "WorldSiteRoot.SiteManagementHud.cs"));
    string modelSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "Sites", "BattleRuntimeHeroTroopSummaryModel.cs"));
    string presenterSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "Sites", "BattleRuntimeHeroTroopSummaryPresenter.cs"));

    AssertTrue(
        siteScene.Contains("BattleRuntimeSummaryList", StringComparison.Ordinal) &&
        siteScene.Contains("BattleRuntimeSummaryScroll", StringComparison.Ordinal),
        "battle runtime HUD scene should author a bottom summary list instead of building the panel ad hoc in code");
    AssertTrue(
        nodeRefsSource.Contains("internal HBoxContainer BattleRuntimeSummaryList", StringComparison.Ordinal) &&
        nodeRefsSource.Contains("BattleRuntimeSummaryList = Get<HBoxContainer>", StringComparison.Ordinal),
        "WorldSitePeacetimeHudNodeRefs should expose the authored summary list to the site root binder");
    AssertTrue(
        rootSource.Contains("private HBoxContainer _battleRuntimeSummaryList", StringComparison.Ordinal) &&
        rootSource.Contains("private BattleRuntimeHeroTroopSummaryPresenter _battleRuntimeSummaryPresenter", StringComparison.Ordinal) &&
        siteManagementSource.Contains("_battleRuntimeSummaryPresenter = new BattleRuntimeHeroTroopSummaryPresenter", StringComparison.Ordinal),
        "WorldSiteRoot should delegate battle summary binding to a focused presenter");
    AssertTrue(
        commandHudSource.Contains("BuildBattleRuntimeHeroTroopSummaries", StringComparison.Ordinal) &&
        commandHudSource.Contains("_battleRuntimeSummaryPresenter.Refresh", StringComparison.Ordinal) &&
        commandHudSource.Contains("_activeBattleGroupRuntimeResolution?.RuntimeController?.State", StringComparison.Ordinal),
        "battle runtime refresh should build hero troop summaries from the live runtime state");
    AssertTrue(
        modelSource.Contains("BattleRuntimeActorKind.Corps", StringComparison.Ordinal) &&
        modelSource.Contains("Sum(actor => System.Math.Max(0, actor.HitPoints))", StringComparison.Ordinal) &&
        modelSource.Contains("Sum(actor => ResolveMaxHitPoints", StringComparison.Ordinal) &&
        modelSource.Contains("RemainingTroopCount", StringComparison.Ordinal) &&
        modelSource.Contains("TotalTroopCount", StringComparison.Ordinal),
        "summary model should compute n/m and aggregate troop HP from grouped runtime corps actors");
    AssertTrue(
        presenterSource.Contains("ProgressBar", StringComparison.Ordinal) &&
        presenterSource.Contains("SoldierCountText", StringComparison.Ordinal) &&
        presenterSource.Contains("TroopHpCurrent", StringComparison.Ordinal) &&
        presenterSource.Contains("HeroHpCurrent", StringComparison.Ordinal),
        "summary presenter should bind hero HP, troop count, and aggregate troop HP into reusable row controls");
}

internal static void BattleRuntimeHudSwitchesSelectedHeroCompany()
{
    string root = ProjectRoot();
    string rootSource = ReadWorldSiteRootSource();
    string siteScene = File.ReadAllText(Path.Combine(root, "scenes", "world", "ui", "WorldSitePeacetimeHud.tscn"));
    string sceneFactorySource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "Common", "GameUiSceneFactory.cs"));
    string selectorPresenterSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "Sites", "BattleRuntimeHeroSelectorPresenter.cs"));
    string heroFramePresenterSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "Sites", "BattleRuntimeHeroFramePresenter.cs"));
    string switchButtonSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "Sites", "BattleRuntimeHeroSwitchButton.cs"));
    string presenterRefreshBody = ExtractMethodBody(heroFramePresenterSource, "public void Refresh(");

    AssertTrue(
        siteScene.Contains("BattleRuntimeHeroSelectorList", StringComparison.Ordinal) &&
        sceneFactorySource.Contains("BattleRuntimeHeroSwitchButtonScenePath", StringComparison.Ordinal) &&
        sceneFactorySource.Contains("CreateBattleRuntimeHeroSwitchButton", StringComparison.Ordinal),
        "battle runtime HUD should author and instantiate a reusable hero switch row");
    AssertTrue(
        selectorPresenterSource.Contains("SelectBattleRuntimeCommandGroup", StringComparison.Ordinal) &&
        selectorPresenterSource.Contains("Refresh(", StringComparison.Ordinal) &&
        switchButtonSource.Contains("SelectedEventHandler", StringComparison.Ordinal),
        "hero switch buttons should emit selected battle group ids through a focused presenter");
    AssertTrue(
        presenterRefreshBody.Contains("_heroSelectorPresenter?.Refresh", StringComparison.Ordinal) &&
        presenterRefreshBody.Contains("playerGroups", StringComparison.Ordinal) &&
        presenterRefreshBody.IndexOf("_heroSelectorPresenter?.Refresh", StringComparison.Ordinal) <
        presenterRefreshBody.IndexOf("RefreshSkillList", StringComparison.Ordinal),
        "refreshing the hero frame should rebuild hero switch state before refreshing the selected hero skill list");
}

internal static void BattleRuntimeHudUsesSkillListNotSingleTextButton()
{
    string root = ProjectRoot();
    string rootSource = ReadWorldSiteRootSource();
    string sceneFactorySource = File.ReadAllText(Path.Combine(
        root,
        "src",
        "Presentation",
        "Common",
        "GameUiSceneFactory.cs"));
    string slotSource = File.ReadAllText(Path.Combine(
        root,
        "src",
        "Presentation",
        "World",
        "Sites",
        "BattleRuntimeSkillSlot.cs"));
    string skillDefinitions = string.Join("\n", new[]
    {
        File.ReadAllText(Path.Combine(root, "config", "battle", "battle_skill_definitions.json")),
        File.ReadAllText(Path.Combine(root, "resource", "battle", "skills", "skill_shield_barrier.tres")),
        File.ReadAllText(Path.Combine(root, "resource", "battle", "skills", "skill_sun_piercer.tres")),
        File.ReadAllText(Path.Combine(root, "resource", "battle", "skills", "skill_thunder_tag_throw.tres")),
        File.ReadAllText(Path.Combine(root, "resource", "battle", "skills", "skill_thunder_mark_fold.tres")),
        File.ReadAllText(Path.Combine(root, "resource", "battle", "skills", "skill_thunder_spiral_break.tres"))
    });
    string heroFramePresenterSource = File.ReadAllText(Path.Combine(
        root,
        "src",
        "Presentation",
        "World",
        "Sites",
        "BattleRuntimeHeroFramePresenter.cs"));

    AssertTrue(
        sceneFactorySource.Contains("BattleRuntimeSkillSlotScenePath", StringComparison.Ordinal) &&
        sceneFactorySource.Contains("CreateBattleRuntimeSkillSlot", StringComparison.Ordinal),
        "runtime skill list should instantiate reusable authored skill-slot resources");
    AssertTrue(
        rootSource.Contains("_battleRuntimeHeroFramePresenter.Refresh", StringComparison.Ordinal) &&
        heroFramePresenterSource.Contains("RefreshSkillList", StringComparison.Ordinal) &&
        heroFramePresenterSource.Contains("CreateBattleRuntimeSkillSlot", StringComparison.Ordinal) &&
        heroFramePresenterSource.Contains("skill.DisplayName", StringComparison.Ordinal) &&
        !rootSource.Contains("_battleRuntimeHeroSkillButton", StringComparison.Ordinal),
        "battle runtime HUD should populate skill slots from skill snapshots instead of hardcoding one '技' button");
    AssertTrue(
        slotSource.Contains("public void Bind(", StringComparison.Ordinal) &&
        slotSource.Contains("StatusText", StringComparison.Ordinal) &&
        slotSource.Contains("CooldownRemainingSeconds", StringComparison.Ordinal) &&
        slotSource.Contains("EmitSignal(SignalName.Pressed", StringComparison.Ordinal),
        "runtime skill slot should expose bindable state for future cooldown text and current lock status");
    AssertTrue(
        skillDefinitions.Contains("skill_shield_barrier", StringComparison.Ordinal) &&
        skillDefinitions.Contains("skill_sun_piercer", StringComparison.Ordinal) &&
        skillDefinitions.Contains("skill_thunder_tag_throw", StringComparison.Ordinal) &&
        skillDefinitions.Contains("skill_thunder_mark_fold", StringComparison.Ordinal) &&
        skillDefinitions.Contains("skill_thunder_spiral_break", StringComparison.Ordinal) ||
        skillDefinitions.Contains("DisplayName = \"曦盾结界\"", StringComparison.Ordinal) &&
        skillDefinitions.Contains("DisplayName = \"贯日一击\"", StringComparison.Ordinal) &&
        skillDefinitions.Contains("DisplayName = \"雷签飞投\"", StringComparison.Ordinal) &&
        skillDefinitions.Contains("DisplayName = \"雷印折跃\"", StringComparison.Ordinal) &&
        skillDefinitions.Contains("DisplayName = \"雷旋破\"", StringComparison.Ordinal) &&
        !skillDefinitions.Contains("DisplayName = \"回旋破阵\"", StringComparison.Ordinal),
        "runtime skill list should expose the thunder demo kit instead of the old single placeholder skill");
}

internal static void BattleRuntimeLiveSkillButtonEntersTacticalPauseBeforeTargetPicking()
{
    string rootSource = ReadWorldSiteRootSource();
    string pressBody = ExtractMethodBody(rootSource, "private void BeginBattleRuntimeSkillPress(");

    AssertTrue(
        pressBody.Contains("SetBattleRuntimeCommandPauseActive(true", StringComparison.Ordinal) &&
        pressBody.IndexOf("SetBattleRuntimeCommandPauseActive(true", StringComparison.Ordinal) <
        pressBody.IndexOf("BeginBattleRuntimeHeroSkillTargetPicking", StringComparison.Ordinal),
        "live skill clicks should enter tactical pause before target picking begins");
    AssertTrue(
        rootSource.Contains("BattleRuntimeHeroSkillPressed", StringComparison.Ordinal),
        "skill entry should leave a low-noise diagnostic for the runtime HUD interaction");
}

internal static void BattleRuntimeTargetPickingSuppressesCommandHud()
{
    string rootSource = ReadWorldSiteRootSource();
    string suppressionPath = Path.Combine(
        ProjectRoot(),
        "src",
        "Presentation",
        "World",
        "Sites",
        "WorldSiteRoot.BattleMapOperationHud.cs");
    string suppressionSource = File.Exists(suppressionPath) ? File.ReadAllText(suppressionPath) : "";
    string beginBody = ExtractMethodBody(rootSource, "private void BeginBattleRuntimeHeroSkillTargetPicking(");
    string cancelBody = ExtractMethodBody(rootSource, "private void CancelBattleRuntimeHeroSkillTargetPicking(");
    string targetInputBody = ExtractMethodBody(rootSource, "private bool TryHandleBattleRuntimeHeroSkillTargetInput(InputEvent inputEvent)");

    AssertTrue(
        beginBody.Contains("EnterBattleMapOperationHudSuppression(BattleMapOperationHudSuppressionReason.RuntimeSkillTarget", StringComparison.Ordinal) &&
        cancelBody.Contains("ExitBattleMapOperationHudSuppression(BattleMapOperationHudSuppressionReason.RuntimeSkillTarget", StringComparison.Ordinal),
        "target-picking should hide blocking command HUD on entry and restore the previous HUD layer on exit");
    AssertTrue(
        suppressionSource.Contains("_battleRuntimeCommandBar", StringComparison.Ordinal) &&
        suppressionSource.Contains("_battleRuntimeSummaryBar", StringComparison.Ordinal) &&
        !targetInputBody.Contains("BattleRuntimeCommandHudPointerGate.ContainsPointer", StringComparison.Ordinal),
        "target-picking map clicks should not be blocked by still-visible command or summary HUD pointer gates");
}

internal static void BattleRuntimePresentationHandlesThunderFoldAsTeleport()
{
    string presentationSource = ReadWorldSitePresentationSource();
    string unitRootSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "Battle", "Entities", "BattleUnitRoot.Movement.cs"));
    string observeBody = ExtractMethodBody(presentationSource, "public Task ObserveAsync(");

    AssertTrue(
        observeBody.Contains("BattleEventKind.ThunderMarkTeleported", StringComparison.Ordinal) &&
        observeBody.Contains("ObserveRuntimeTeleportEvent", StringComparison.Ordinal),
        "presentation should observe thunder fold teleport events separately from ordinary movement events");
    AssertTrue(
        unitRootSource.Contains("SnapEntityToSurface", StringComparison.Ordinal) &&
        unitRootSource.Contains("StopEntityMovement(entity, snapToLogicalGrid: false)", StringComparison.Ordinal),
        "teleport presentation should cancel any queued movement lane and snap to the Runtime destination cell");
}

internal static void BattleRuntimeHudHidesHeroControlsWhenPauseEnds()
{
    string rootSource = ReadWorldSiteRootSource();
    string pausePresentationBody = ExtractMethodBody(rootSource, "private void RefreshBattleRuntimeCommandPausePresentation()");
    string setPauseBody = ExtractMethodBody(rootSource, "private void SetBattleRuntimeCommandPauseActive(bool paused, string reason)");

    AssertTrue(
        pausePresentationBody.Contains("_battleRuntimeCommandPauseActive", StringComparison.Ordinal) &&
        pausePresentationBody.Contains("_battleRuntimeCommandBar.Visible = false", StringComparison.Ordinal) &&
        pausePresentationBody.Contains("_battleRuntimePauseDetailPanel.Visible = false", StringComparison.Ordinal),
        "turning tactical pause off should hide the bottom command/detail surfaces instead of leaving them on screen");
    AssertTrue(
        pausePresentationBody.Contains("_battleRuntimeSummaryBar.Visible = false", StringComparison.Ordinal) &&
        pausePresentationBody.Contains("_battleRuntimePauseDetailPanel.Visible = true", StringComparison.Ordinal) &&
        !pausePresentationBody.Contains("RefreshBattleRuntimeRadialCommandMenuPosition()", StringComparison.Ordinal),
        "tactical pause should hide the live summary strip and show the combat detail panel without a separate selected-unit radial");
    AssertTrue(
        setPauseBody.Contains("CancelBattleRuntimeHeroSkillTargetPicking(\"pause_off\")", StringComparison.Ordinal) &&
        setPauseBody.Contains("RefreshBattleRuntimeCommandPausePresentation()", StringComparison.Ordinal),
        "pause-off should cancel target picking and refresh the HUD visibility in the same state transition");
}

internal static void BattleRuntimeViewportStaysFullscreenDuringHudAndPause()
{
    string rootSource = ReadWorldSiteRootSource();
    string siteManagementSource = File.ReadAllText(Path.Combine(
        ProjectRoot(),
        "src",
        "Presentation",
        "World",
        "Sites",
        "WorldSiteRoot.SiteManagementHud.cs"));
    string resolveRectBody = ExtractMethodBody(rootSource, "private Rect2 ResolveMainWorldViewportRect()");
    string pausePresentationBody = ExtractMethodBody(rootSource, "private void RefreshBattleRuntimeCommandPausePresentation()");
    string panelVisibilityBody = ExtractMethodBody(siteManagementSource, "private void UpdateSiteManagementEntryVisibility(");

    AssertTrue(
        resolveRectBody.Contains("_battleRuntimeEnabled", StringComparison.Ordinal) &&
        resolveRectBody.Contains("new Rect2(Vector2.Zero, rootViewportSize)", StringComparison.Ordinal),
        "battle runtime should resolve the world viewport to fullscreen instead of reserving left or bottom HUD workspace");
    AssertTrue(
        pausePresentationBody.Contains("_sitePeacetimePanel.Visible = false", StringComparison.Ordinal) &&
        !pausePresentationBody.Contains("UpdateSitePeacetimePanelVisibility(\"battle_runtime_command_pause\")", StringComparison.Ordinal),
        "tactical pause should keep the left primary panel hidden and not re-run management panel visibility");
    AssertTrue(
        panelVisibilityBody.Contains("_battleRuntimeEnabled", StringComparison.Ordinal) &&
        panelVisibilityBody.Contains("SetSiteManagementPanelVisible(false)", StringComparison.Ordinal) &&
        panelVisibilityBody.IndexOf("_battleRuntimeEnabled", StringComparison.Ordinal) <
        panelVisibilityBody.IndexOf("bool shouldShowEntry", StringComparison.Ordinal),
        "site panel visibility must short-circuit during battle runtime so tactical pause cannot reopen the management panel");
}

internal static void BattleRuntimeSelectionSupportsLiveMultiSelectForBeaconCommands()
{
    string rootSource = ReadWorldSiteRootSource();
    string inputBody = ExtractMethodBody(rootSource, "public override void _Input(InputEvent @event)");
    string selectionInputBody = ExtractMethodBody(rootSource, "private bool TryHandleBattleRuntimeCommandSelectionInput(InputEvent inputEvent)");
    string selectBody = ExtractMethodBody(rootSource, "private void SelectBattleRuntimeCommandGroup(string groupKey, bool additive)");
    string highlightBody = ExtractMethodBody(rootSource, "private void ApplyBattleRuntimeCommandGroupHighlight()");

    AssertTrue(
        rootSource.Contains("private readonly HashSet<string> _selectedBattleRuntimeGroupKeys", StringComparison.Ordinal) &&
        inputBody.Contains("TryHandleBattleRuntimeCommandSelectionInput(@event)", StringComparison.Ordinal) &&
        inputBody.Contains("TryHandleBattleRuntimeDestinationBeaconInput(@event)", StringComparison.Ordinal),
        "battle runtime input should support selecting groups and issuing beacon commands while battle is live or tactically paused.");
    AssertTrue(
        selectionInputBody.Contains("InputEventMouseButton", StringComparison.Ordinal) &&
        selectionInputBody.Contains("mouseButton.ButtonIndex == MouseButton.Left", StringComparison.Ordinal) &&
        selectionInputBody.Contains("TryGetMouseGridPosition(out GridPosition position)", StringComparison.Ordinal) &&
        selectionInputBody.Contains("TryResolveBattleRuntimeCommandGroupKeyAtPosition(position, out string groupKey)", StringComparison.Ordinal) &&
        selectionInputBody.Contains("IsBattleRuntimeAdditiveSelectionInput(mouseButton)", StringComparison.Ordinal) &&
        !selectionInputBody.Contains("SetBattleRuntimeCommandPauseActive", StringComparison.Ordinal),
        "left-click group selection should work as live input without forcing tactical pause.");
    AssertTrue(
        selectBody.Contains("_selectedBattleRuntimeGroupKeys.Add", StringComparison.Ordinal) &&
        selectBody.Contains("_selectedBattleRuntimeGroupKeys.Clear", StringComparison.Ordinal) &&
        selectBody.Contains("_selectedBattleRuntimeGroupKey", StringComparison.Ordinal) &&
        highlightBody.Contains("BuildSelectedBattleRuntimeCommandGroupEntityIds", StringComparison.Ordinal) &&
        highlightBody.Contains("SetCommandSelectionByEntityIds", StringComparison.Ordinal),
        "selection state should support multi-selected battle groups while preserving the primary selected group for hero skills.");
}

internal static void BattleRuntimeRightClickSubmitsDestinationBeaconCommand()
{
    string rootSource = ReadWorldSiteRootSource();
    string inputBody = ExtractMethodBody(rootSource, "public override void _Input(InputEvent @event)");
    string destinationInputBody = ExtractMethodBody(rootSource, "private bool TryHandleBattleRuntimeDestinationBeaconInput(InputEvent inputEvent)");
    string buildRequestBody = ExtractMethodBody(rootSource, "private CommandRequest BuildBattleRuntimeDestinationBeaconCommandRequest(");

    AssertTrue(
        inputBody.IndexOf("TryHandleBattleRuntimeHeroSkillTargetInput(@event)", StringComparison.Ordinal) <
        inputBody.IndexOf("TryHandleBattleRuntimeDestinationBeaconInput(@event)", StringComparison.Ordinal) &&
        inputBody.IndexOf("TryHandleBattleRuntimeDestinationBeaconInput(@event)", StringComparison.Ordinal) <
        inputBody.IndexOf("TryHandleBattleRuntimePauseInput(@event)", StringComparison.Ordinal),
        "runtime beacon right-click should run after active skill targeting and before pause-toggle input consumes the event.");
    AssertTrue(
        destinationInputBody.Contains("InputEventMouseButton", StringComparison.Ordinal) &&
        destinationInputBody.Contains("mouseButton.ButtonIndex == MouseButton.Right", StringComparison.Ordinal) &&
        destinationInputBody.Contains("mouseButton.Pressed", StringComparison.Ordinal) &&
        destinationInputBody.Contains("EnterBattleMapOperationHudSuppression(BattleMapOperationHudSuppressionReason.RuntimeDestinationBeacon", StringComparison.Ordinal) &&
        destinationInputBody.IndexOf("EnterBattleMapOperationHudSuppression", StringComparison.Ordinal) <
        destinationInputBody.IndexOf("TryGetMouseGridPosition(out GridPosition position)", StringComparison.Ordinal) &&
        !destinationInputBody.Contains("BattleRuntimeCommandHudPointerGate.ContainsPointer", StringComparison.Ordinal) &&
        destinationInputBody.Contains("TryGetMouseGridPosition(out GridPosition position)", StringComparison.Ordinal) &&
        destinationInputBody.Contains("BuildBattleRuntimeDestinationBeaconCommandRequest", StringComparison.Ordinal) &&
        destinationInputBody.Contains("_activeBattleGroupRuntimeResolution?.RuntimeController?.SubmitCommand(commandRequest)", StringComparison.Ordinal) &&
        !destinationInputBody.Contains("MovementRangeFinder", StringComparison.Ordinal) &&
        !destinationInputBody.Contains("SetEntityPosition", StringComparison.Ordinal) &&
        !destinationInputBody.Contains("SnapEntity", StringComparison.Ordinal),
        "right-click destination input should submit command intent and must not move presentation entities directly.");
    AssertTrue(
        buildRequestBody.Contains("Kind = CommandKind.DestinationBeacon", StringComparison.Ordinal) &&
        buildRequestBody.Contains("Channel = CommandChannel.Combined", StringComparison.Ordinal) &&
        buildRequestBody.Contains("HasTargetGrid = true", StringComparison.Ordinal) &&
        buildRequestBody.Contains("BattleGroupIds.Add", StringComparison.Ordinal) &&
        buildRequestBody.Contains("TargetGridX = target.X", StringComparison.Ordinal) &&
        buildRequestBody.Contains("TargetGridY = target.Y", StringComparison.Ordinal),
        "destination beacon request should carry all selected battle groups and the clicked target grid.");
}

internal static void BattleRuntimeDestinationBeaconOverlayUsesRuntimeFactsOnly()
{
    string rootSource = ReadWorldSiteRootSource();
    string presentationSource = ReadWorldSitePresentationSource();
    string overlayBody = ExtractMethodBody(rootSource, "private void RefreshBattleRuntimeDestinationBeaconOverlays()");

    AssertTrue(
        overlayBody.Contains("RuntimeController?.State?.DestinationBeacons", StringComparison.Ordinal) &&
        overlayBody.Contains("_battleDestinationBeaconMarkerPresenter.RefreshRuntime", StringComparison.Ordinal) &&
        overlayBody.Contains("BuildBattleRuntimePlayerGroups", StringComparison.Ordinal),
        "destination beacon marker overlays should be rebuilt from Runtime destination-beacon facts.");
    AssertTrue(
        !presentationSource.Contains("BattleBeaconFlowField", StringComparison.Ordinal) &&
        !presentationSource.Contains("BeaconFlowFields", StringComparison.Ordinal) &&
        !presentationSource.Contains("BattleBeaconMovementPlanner", StringComparison.Ordinal),
        "Presentation must not sample flow fields or beacon movement planners; Runtime owns pathing and movement commits.");
}

internal static void BattleRuntimeDestinationBeaconsFollowTacticalPauseVisibility()
{
    string rootSource = ReadWorldSiteRootSource();
    string beaconSource = File.ReadAllText(Path.Combine(
        ProjectRoot(),
        "src",
        "Presentation",
        "World",
        "Sites",
        "WorldSiteRoot.BattleRuntimeDestinationBeacon.cs"));
    string setPauseBody = ExtractMethodBody(rootSource, "private void SetBattleRuntimeCommandPauseActive(bool paused, string reason)");
    string destinationInputBody = ExtractMethodBody(beaconSource, "private bool TryHandleBattleRuntimeDestinationBeaconInput(InputEvent inputEvent)");
    string visibilityBody = ExtractMethodBody(beaconSource, "private void RefreshBattleRuntimeDestinationBeaconOverlayVisibility()");

    AssertTrue(
        setPauseBody.Contains("RefreshBattleRuntimeDestinationBeaconOverlayVisibility()", StringComparison.Ordinal),
        "tactical pause toggles should immediately refresh destination beacon visibility.");
    AssertTrue(
        destinationInputBody.Contains("RefreshBattleRuntimeDestinationBeaconOverlayVisibility()", StringComparison.Ordinal) &&
        !destinationInputBody.Contains("RefreshBattleRuntimeDestinationBeaconOverlays();", StringComparison.Ordinal),
        "right-click beacon commands should update through the pause-aware visibility entry instead of unconditionally showing beacons.");
    AssertTrue(
        visibilityBody.Contains("_battleRuntimeCommandPauseActive", StringComparison.Ordinal) &&
        visibilityBody.Contains("RefreshBattleRuntimeDestinationBeaconOverlays()", StringComparison.Ordinal) &&
        visibilityBody.Contains("_battleDestinationBeaconMarkerPresenter.Clear()", StringComparison.Ordinal),
        "runtime beacon overlays should show from Runtime facts only while tactically paused and clear when battle resumes.");
}
}
