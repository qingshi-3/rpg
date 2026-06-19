internal static partial class WorldSiteDeploymentCacheRegressionCases
{
internal static void PresentationUiAuthoringStaysResourceBacked()
{
    string root = ProjectRoot();
    string siteRootDir = Path.Combine(root, "src", "Presentation", "World", "Sites");
    AssertTrue(Directory.Exists(siteRootDir), $"presentation source directory should exist path={siteRootDir}");

    List<string> files = Directory.GetFiles(siteRootDir, "WorldSiteRoot*.cs")
        .OrderBy(path => path)
        .ToList();
    AssertTrue(files.Count > 0, $"presentation source scan should include WorldSiteRoot partials dir={siteRootDir}");

    files.AddRange(Directory
        .GetFiles(Path.Combine(root, "src", "Presentation", "Battle", "Entities"), "BattleUnitRoot*.cs")
        .OrderBy(path => path));
    files.Add(Path.Combine(root, "src", "Presentation", "Battle", "BattleGridHighlightOverlay.cs"));
    files.Add(Path.Combine(root, "src", "Presentation", "Battle", "Entities", "UnitAnimationComponent.cs"));

    string[] forbiddenFragments =
    {
        "new Button(",
        "new Label(",
        "new VBoxContainer(",
        "new HBoxContainer(",
        "new Panel(",
        "new PanelContainer(",
        "new ScrollContainer(",
        "new Control(",
        "new MarginContainer(",
        "new GridContainer(",
        "new RichTextLabel(",
        "new TextureRect(",
        "new LineEdit(",
        "new OptionButton(",
        "new CheckBox(",
        "new TabContainer("
    };

    foreach (string file in files)
    {
        AssertTrue(File.Exists(file), $"presentation source scan target should exist path={file}");
        string source = File.ReadAllText(file);
        foreach (string fragment in forbiddenFragments)
        {
            AssertTrue(
                !source.Contains(fragment, StringComparison.Ordinal),
                $"presentation UI must be authored as .tscn / built via GameUiSceneFactory, not direct {fragment.Trim()} file={file}");
        }
    }

    string battlePreparationHudPath = Path.Combine(root, "src", "Presentation", "World", "Sites", "WorldSiteRoot.BattlePreparationHud.cs");
    string battlePreparationHudBinderPath = Path.Combine(root, "src", "Presentation", "World", "Sites", "BattlePreparationHudBinder.cs");
    string battlePreparationHudSource = File.ReadAllText(battlePreparationHudPath);
    string battlePreparationHudBinderSource = File.ReadAllText(battlePreparationHudBinderPath);
    AssertTrue(
        battlePreparationHudSource.Contains("_battlePreparationHudBinder.BindCompanyRoster", StringComparison.Ordinal) &&
        battlePreparationHudBinderSource.Contains("GameUiSceneFactory.CreateBattlePreparationRosterRow", StringComparison.Ordinal),
        $"battle preparation dynamic UI rows should be created through GameUiSceneFactory file={battlePreparationHudPath}");
}

internal static void StrategicWorldUiUsesBasicUi1TextureSkin()
{
    string root = ProjectRoot();
    string skinDir = Path.Combine(root, "assets", "themes", "game-ui-skin");
    string themePath = Path.Combine(skinDir, "basic_ui_1_theme.tres");
    string[] styleResources =
    {
        "basic_ui_1_panel_large.tres",
        "basic_ui_1_panel_sheet.tres",
        "basic_ui_1_panel_topbar.tres",
        "basic_ui_1_panel_card.tres",
        "basic_ui_1_panel_slot.tres",
        "basic_ui_1_button_primary.tres",
        "basic_ui_1_button_primary_hover.tres",
        "basic_ui_1_button_primary_pressed.tres",
        "basic_ui_1_button_disabled.tres",
        "basic_ui_1_button_action.tres",
        "basic_ui_1_button_action_hover.tres",
        "basic_ui_1_button_action_pressed.tres",
        "basic_ui_1_button_action_disabled.tres",
        "basic_ui_1_button_compact.tres",
        "basic_ui_1_button_compact_hover.tres",
        "basic_ui_1_button_compact_pressed.tres",
        "basic_ui_1_button_compact_disabled.tres"
    };

    foreach (string fileName in styleResources)
    {
        string path = Path.Combine(skinDir, fileName);
        AssertTrue(File.Exists(path), $"strategic UI texture skin resource should exist path={path}");
        string source = File.ReadAllText(path);
        AssertTrue(
            source.Contains("[gd_resource type=\"StyleBoxTexture\"", StringComparison.Ordinal),
            $"strategic UI skin resource should be a StyleBoxTexture path={path}");
        AssertTrue(
            source.Contains("assets/textures/ui/basic-ui/1/", StringComparison.Ordinal),
            $"strategic UI skin resource should use only the first basic UI pack path={path}");
        AssertTrue(
            !source.Contains("assets/textures/ui/basic-ui/2/", StringComparison.Ordinal) &&
            !source.Contains("assets/textures/ui/basic-ui/3/", StringComparison.Ordinal) &&
            !source.Contains("assets/textures/ui/basic-ui/need-human/", StringComparison.Ordinal),
            $"strategic UI skin resource should not mix UI packs path={path}");
        AssertTrue(
            source.Contains("texture_margin_left", StringComparison.Ordinal) &&
            source.Contains("texture_margin_top", StringComparison.Ordinal) &&
            source.Contains("texture_margin_right", StringComparison.Ordinal) &&
            source.Contains("texture_margin_bottom", StringComparison.Ordinal),
            $"strategic UI skin resource should define nine-patch margins path={path}");
    }

    AssertTrue(File.Exists(themePath), $"strategic UI shared theme should exist path={themePath}");
    string themeSource = File.ReadAllText(themePath);
    AssertTrue(
        themeSource.Contains("[gd_resource type=\"Theme\"", StringComparison.Ordinal),
        $"strategic UI shared theme should be a Godot Theme resource path={themePath}");
    AssertTrue(
        themeSource.Contains("basic_ui_1_panel_sheet.tres", StringComparison.Ordinal) &&
        themeSource.Contains("basic_ui_1_button_action.tres", StringComparison.Ordinal) &&
        themeSource.Contains("basic_ui_1_button_action_hover.tres", StringComparison.Ordinal) &&
        themeSource.Contains("basic_ui_1_button_action_pressed.tres", StringComparison.Ordinal) &&
        themeSource.Contains("basic_ui_1_button_action_disabled.tres", StringComparison.Ordinal),
        $"strategic UI shared theme should centralize context sheet and action button skin resources path={themePath}");
    AssertTrue(
        themeSource.Contains("WorldContextSheet/base_type = &\"PanelContainer\"", StringComparison.Ordinal) &&
        themeSource.Contains("WorldContextSheet/styles/panel = ExtResource(", StringComparison.Ordinal) &&
        themeSource.Contains("WorldPrimaryActionButton/base_type = &\"Button\"", StringComparison.Ordinal) &&
        themeSource.Contains("WorldSecondaryActionButton/base_type = &\"Button\"", StringComparison.Ordinal),
        $"strategic UI shared theme should expose named type variations for authored UI roles path={themePath}");

    string[] targetScenes =
    {
        Path.Combine(root, "scenes", "world", "ui", "StrategicWorldHud.tscn"),
        Path.Combine(root, "scenes", "world", "ui", "WorldSitePeacetimeHud.tscn"),
        Path.Combine(root, "scenes", "world", "ui", "BattlePreparationRosterRow.tscn"),
        Path.Combine(root, "scenes", "world", "ui", "BattleRuntimeSkillSlot.tscn"),
        Path.Combine(root, "scenes", "world", "ui", "BattleRuntimeHeroSwitchButton.tscn"),
        Path.Combine(root, "scenes", "world", "ui", "BattlePreparationObjectiveThumbnail.tscn"),
        Path.Combine(root, "scenes", "world", "ui", "BattleObjectiveMapDialog.tscn"),
        Path.Combine(root, "scenes", "world", "ui", "WorldSiteHoverSummaryPanel.tscn"),
        Path.Combine(root, "scenes", "world", "ui", "WorldPrimaryActionButton.tscn"),
        Path.Combine(root, "scenes", "world", "ui", "WorldSecondaryActionButton.tscn")
    };

    foreach (string scenePath in targetScenes)
    {
        AssertTrue(File.Exists(scenePath), $"strategic UI scene should exist path={scenePath}");
        string scene = File.ReadAllText(scenePath);
        AssertTrue(
            scene.Contains("res://assets/themes/game-ui-skin/basic_ui_1_", StringComparison.Ordinal),
            $"strategic UI scene should use shared basic-ui/1 texture skin resources path={scenePath}");
        AssertTrue(
            !scene.Contains("StyleBoxFlat", StringComparison.Ordinal),
            $"strategic UI visual validation target should not use local StyleBoxFlat blocks path={scenePath}");
    }

    string strategicHud = File.ReadAllText(Path.Combine(root, "scenes", "world", "ui", "StrategicWorldHud.tscn"));
    AssertTrue(
        strategicHud.Contains("res://assets/themes/game-ui-skin/basic_ui_1_theme.tres", StringComparison.Ordinal),
        "strategic world HUD should attach the shared basic-ui/1 Theme instead of styling key panels independently");
    AssertTrue(
        strategicHud.Contains("button_pause.png", StringComparison.Ordinal) &&
        strategicHud.Contains("button_pause_hover.png", StringComparison.Ordinal) &&
        strategicHud.Contains("button_pause_pressed.png", StringComparison.Ordinal) &&
        strategicHud.Contains("button_quick.png", StringComparison.Ordinal) &&
        strategicHud.Contains("button_quick_hover.png", StringComparison.Ordinal) &&
        strategicHud.Contains("button_quick_pressed.png", StringComparison.Ordinal) &&
        strategicHud.Contains("button_reset.png", StringComparison.Ordinal) &&
        strategicHud.Contains("button_reset_hover.png", StringComparison.Ordinal) &&
        strategicHud.Contains("button_reset_pressed.png", StringComparison.Ordinal),
        "strategic world top bar controls should use matching authored icon TextureButtons with native hover/click states");
    AssertTrue(
        !strategicHud.Contains("[node name=\"TopResourceBar\"", StringComparison.Ordinal) &&
        !strategicHud.Contains("basic_ui_1_panel_topbar", StringComparison.Ordinal),
        "strategic world top UI should not draw a full-width framed panel behind the lightweight controls");

    string hoverSummaryScenePath = Path.Combine(root, "scenes", "world", "ui", "WorldSiteHoverSummaryPanel.tscn");
    string hoverSummary = File.ReadAllText(hoverSummaryScenePath);
    string hoverSummaryRootBlock = ExtractSceneNodeBlock(hoverSummary, "[node name=\"WorldSiteHoverSummaryPanel\"");
    AssertTrue(
        hoverSummary.Contains("res://assets/themes/game-ui-skin/basic_ui_1_theme.tres", StringComparison.Ordinal) &&
        hoverSummaryRootBlock.Contains("theme_type_variation = &\"WorldContextCard\"", StringComparison.Ordinal) &&
        !hoverSummaryRootBlock.Contains("theme_override_styles/panel", StringComparison.Ordinal),
        "strategic-world hover summary should use the shared context-card Theme variation instead of a local panel style");
    string gameUiSceneFactorySource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "Common", "GameUiSceneFactory.cs"));
    AssertTrue(
        gameUiSceneFactorySource.Contains("WorldSiteHoverSummaryPanelScenePath = \"res://scenes/world/ui/WorldSiteHoverSummaryPanel.tscn\"", StringComparison.Ordinal) &&
        gameUiSceneFactorySource.Contains("CreateWorldSiteHoverSummaryPanel", StringComparison.Ordinal) &&
        gameUiSceneFactorySource.Contains("Instantiate<WorldSiteHoverSummaryPanel>(WorldSiteHoverSummaryPanelScenePath", StringComparison.Ordinal),
        "strategic-world hover summary should remain an authored scene instantiated through GameUiSceneFactory");

    string objectiveThumbnail = File.ReadAllText(Path.Combine(root, "scenes", "world", "ui", "BattlePreparationObjectiveThumbnail.tscn"));
    string objectiveThumbnailBlock = ExtractSceneNodeBlock(objectiveThumbnail, "[node name=\"BattlePreparationObjectiveThumbnail\"");
    AssertTrue(
        objectiveThumbnailBlock.Contains("theme_type_variation = &\"WorldContextCard\"", StringComparison.Ordinal) &&
        !objectiveThumbnailBlock.Contains("theme_override_styles/panel", StringComparison.Ordinal),
        "battle-preparation objective thumbnail should use the shared context-card Theme variation instead of a local panel style");
    string siteHud = File.ReadAllText(Path.Combine(root, "scenes", "world", "ui", "WorldSitePeacetimeHud.tscn"));
    string activeObjectiveThumbnailBlock = ExtractSceneNodeBlock(siteHud, "[node name=\"BattlePreparationObjectiveThumbnail\" type=\"PanelContainer\" parent=\"MinimapHost/BattlePreparationObjectiveThumbnailDock\"");
    AssertTrue(
        siteHud.Contains("res://assets/themes/game-ui-skin/basic_ui_1_theme.tres", StringComparison.Ordinal) &&
        activeObjectiveThumbnailBlock.Contains("theme = ExtResource(\"14_theme\")", StringComparison.Ordinal) &&
        activeObjectiveThumbnailBlock.Contains("theme_type_variation = &\"WorldContextCard\"", StringComparison.Ordinal) &&
        !activeObjectiveThumbnailBlock.Contains("theme_override_styles/panel", StringComparison.Ordinal),
        "active world-site objective thumbnail should use the same shared context-card Theme variation as the standalone template");

    string objectiveDialog = File.ReadAllText(Path.Combine(root, "scenes", "world", "ui", "BattleObjectiveMapDialog.tscn"));
    string dialogPanelBlock = ExtractSceneNodeBlock(objectiveDialog, "[node name=\"DialogPanel\"");
    string companyPanelBlock = ExtractSceneNodeBlock(objectiveDialog, "[node name=\"CompanyPanel\"");
    string previewPanelBlock = ExtractSceneNodeBlock(objectiveDialog, "[node name=\"PreviewPanel\"");
    string closeButtonBlock = ExtractSceneNodeBlock(objectiveDialog, "[node name=\"CloseButton\"");
    string doneButtonBlock = ExtractSceneNodeBlock(objectiveDialog, "[node name=\"DoneButton\"");
    string mapPreviewBlock = ExtractSceneNodeBlock(objectiveDialog, "[node name=\"MapPreview\"");
    AssertTrue(
        dialogPanelBlock.Contains("theme_type_variation = &\"WorldContextSheet\"", StringComparison.Ordinal) &&
        !dialogPanelBlock.Contains("theme_override_styles/panel", StringComparison.Ordinal),
        "battle objective map dialog shell should use the shared context-sheet Theme variation");
    AssertTrue(
        companyPanelBlock.Contains("theme_type_variation = &\"WorldContextCard\"", StringComparison.Ordinal) &&
        previewPanelBlock.Contains("theme_type_variation = &\"WorldContextCard\"", StringComparison.Ordinal) &&
        !companyPanelBlock.Contains("theme_override_styles/panel", StringComparison.Ordinal) &&
        !previewPanelBlock.Contains("theme_override_styles/panel", StringComparison.Ordinal),
        "battle objective map dialog inner panels should use the shared context-card Theme variation");
    AssertTrue(
        closeButtonBlock.Contains("theme_type_variation = &\"WorldSecondaryActionButton\"", StringComparison.Ordinal) &&
        doneButtonBlock.Contains("theme_type_variation = &\"WorldPrimaryActionButton\"", StringComparison.Ordinal),
        "battle objective map dialog actions should use shared primary/secondary action button Theme variations");

    float dialogHeight = ReadResourceFloat(dialogPanelBlock, "offset_bottom") -
        ReadResourceFloat(dialogPanelBlock, "offset_top");
    (_, float mapPreviewHeight) = ReadSceneNodeMinimumSize(mapPreviewBlock, "MapPreview", "BattleObjectiveMapDialog.tscn");
    (_, float closeButtonHeight) = ReadSceneNodeMinimumSize(closeButtonBlock, "CloseButton", "BattleObjectiveMapDialog.tscn");
    (_, float doneButtonHeight) = ReadSceneNodeMinimumSize(doneButtonBlock, "DoneButton", "BattleObjectiveMapDialog.tscn");
    const float sheetThemeVerticalMargins = 36f;
    const float cardThemeVerticalMargins = 20f;
    const float explicitDialogMarginVertical = 32f;
    const float previewMarginVertical = 20f;
    const float dialogStackVerticalGaps = 36f;
    const float statusLabelBudget = 24f;
    float requiredDialogHeight =
        sheetThemeVerticalMargins +
        explicitDialogMarginVertical +
        closeButtonHeight +
        dialogStackVerticalGaps +
        cardThemeVerticalMargins +
        previewMarginVertical +
        mapPreviewHeight +
        statusLabelBudget +
        doneButtonHeight;
    AssertTrue(
        dialogHeight >= requiredDialogHeight,
        $"battle objective map dialog should budget enough height for the shared Theme margins and controls height={dialogHeight} required={requiredDialogHeight}");
}

internal static void StrategicWorldUiButtonSkinsFitAuthoredButtonSizes()
{
    string root = ProjectRoot();
    string skinDir = Path.Combine(root, "assets", "themes", "game-ui-skin");
    string[] compactStyles =
    {
        "basic_ui_1_button_compact.tres",
        "basic_ui_1_button_compact_hover.tres",
        "basic_ui_1_button_compact_pressed.tres",
        "basic_ui_1_button_compact_disabled.tres"
    };

    foreach (string fileName in compactStyles)
    {
        string path = Path.Combine(skinDir, fileName);
        AssertTrue(File.Exists(path), $"compact button texture skin resource should exist path={path}");
        string source = File.ReadAllText(path);
        AssertTrue(
            source.Contains("button_empty_2_compact.png", StringComparison.Ordinal),
            $"compact button skin should use the scaled compact derivative, not the full-size long button texture path={path}");

        float left = ReadResourceFloat(source, "texture_margin_left");
        float top = ReadResourceFloat(source, "texture_margin_top");
        float right = ReadResourceFloat(source, "texture_margin_right");
        float bottom = ReadResourceFloat(source, "texture_margin_bottom");
        AssertTrue(
            left == top && top == right && right == bottom,
            $"compact button skin margins should preserve the 45-degree corner geometry equally on all sides path={path}");
        AssertTrue(
            left == 18f,
            $"compact button skin should use the half-scale 45-degree corner projection margin path={path}");
    }

    float compactMargin = 18f;
    string[] actionStyles =
    {
        "basic_ui_1_button_action.tres",
        "basic_ui_1_button_action_hover.tres",
        "basic_ui_1_button_action_pressed.tres",
        "basic_ui_1_button_action_disabled.tres"
    };

    foreach (string fileName in actionStyles)
    {
        string path = Path.Combine(skinDir, fileName);
        AssertTrue(File.Exists(path), $"action button texture skin resource should exist path={path}");
        string source = File.ReadAllText(path);
        AssertTrue(
            source.Contains("normal_window.png", StringComparison.Ordinal) &&
            !source.Contains("button_empty_2.png", StringComparison.Ordinal),
            $"action buttons should use the regular bordered control skin, not the ornamental plaque button path={path}");
    }

    string strategicHudPath = Path.Combine(root, "scenes", "world", "ui", "StrategicWorldHud.tscn");
    AssertSceneTextureButtonUsesStateTextures(
        strategicHudPath,
        "PauseButton",
        "button_pause.png",
        "button_pause_hover.png",
        "button_pause_pressed.png",
        "top-bar pause button should use the authored pause icon TextureButton instead of a flat Button icon");
    AssertSceneTextureButtonUsesStateTextures(
        strategicHudPath,
        "QuickButton",
        "button_quick.png",
        "button_quick_hover.png",
        "button_quick_pressed.png",
        "top-bar speed button should use the authored fast-forward TextureButton instead of a flat Button icon");
    AssertSceneTextureButtonUsesStateTextures(
        strategicHudPath,
        "ResetButton",
        "button_reset.png",
        "button_reset_hover.png",
        "button_reset_pressed.png",
        "top-bar reset button should use an authored reset TextureButton instead of a flat Button icon");

    string siteHudPath = Path.Combine(root, "scenes", "world", "ui", "WorldSitePeacetimeHud.tscn");
    AssertSceneButtonUsesCompactStyle(siteHudPath, "BattleRuntimeRegroupButton", "runtime regroup button is narrower than the large ornamental button skin", compactMargin);
    AssertSceneButtonUsesCompactStyle(siteHudPath, "MoveFirstRuleButton", "battle-preparation rule button is a compact control", compactMargin);
    AssertSceneButtonUsesCompactStyle(siteHudPath, "AttackFirstRuleButton", "battle-preparation rule button is a compact control", compactMargin);
    AssertSceneButtonUsesCompactStyle(siteHudPath, "HoldRuleButton", "battle-preparation rule button is a compact control", compactMargin);
    AssertSceneButtonUsesCompactStyle(siteHudPath, "BattlePreparationStartButton", "battle-preparation start button is a compact control", compactMargin);
    AssertSceneButtonUsesCompactStyle(siteHudPath, "ReturnMapButton", "site top-bar return button is a compact control", compactMargin);

    AssertSceneButtonUsesCompactStyle(
        Path.Combine(root, "scenes", "world", "ui", "BattleRuntimeHeroSwitchButton.tscn"),
        "BattleRuntimeHeroSwitchButton",
        "battle runtime hero switch button is narrower than the large ornamental button skin",
        compactMargin);

    string worldClockSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "StrategicWorldRoot.WorldClock.cs"));
    AssertTrue(
        !worldClockSource.Contains("_worldClockToggleButton.Text = \"\";", StringComparison.Ordinal) &&
        !worldClockSource.Contains("_worldClockSpeedButton.Text = \"\";", StringComparison.Ordinal) &&
        !worldClockSource.Contains("_worldClockToggleButton.Icon", StringComparison.Ordinal) &&
        !worldClockSource.Contains("_worldClockSpeedButton.Icon", StringComparison.Ordinal) &&
        !worldClockSource.Contains("_worldClockToggleButton.Text = _worldClockPaused", StringComparison.Ordinal) &&
        !worldClockSource.Contains("_worldClockSpeedButton.Text = $", StringComparison.Ordinal),
        "world clock refresh should use TextureButton state textures rather than text or Button.Icon");
    AssertTrue(
        worldClockSource.Contains("button_pause.png", StringComparison.Ordinal) &&
        worldClockSource.Contains("button_pause_hover.png", StringComparison.Ordinal) &&
        worldClockSource.Contains("button_pause_pressed.png", StringComparison.Ordinal) &&
        worldClockSource.Contains("button_play.png", StringComparison.Ordinal) &&
        worldClockSource.Contains("button_play_hover.png", StringComparison.Ordinal) &&
        worldClockSource.Contains("button_play_pressed.png", StringComparison.Ordinal) &&
        worldClockSource.Contains("button_quick.png", StringComparison.Ordinal) &&
        worldClockSource.Contains("button_quick_hover.png", StringComparison.Ordinal) &&
        worldClockSource.Contains("button_quick_pressed.png", StringComparison.Ordinal),
        "world clock refresh should load authored basic-ui/1 state textures for pause/play/speed controls");
    AssertTrue(
        worldClockSource.Contains("ApplyTextureButtonStates(", StringComparison.Ordinal) &&
        worldClockSource.Contains("_worldClockToggleButton,", StringComparison.Ordinal) &&
        worldClockSource.Contains("_worldClockSpeedButton,", StringComparison.Ordinal) &&
        worldClockSource.Contains("button.TextureNormal = normal;", StringComparison.Ordinal) &&
        worldClockSource.Contains("button.TextureHover = hover;", StringComparison.Ordinal) &&
        worldClockSource.Contains("button.TexturePressed = pressed;", StringComparison.Ordinal),
        "world clock refresh should sync TextureButton normal/hover/pressed textures for runtime pause/play state changes");
    string worldRootSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "StrategicWorldRoot.cs"));
    string bootstrapSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "StrategicWorldRoot.UiBootstrap.cs"));
    AssertTrue(
        worldRootSource.Contains("TextureButton _worldClockToggleButton", StringComparison.Ordinal) &&
        worldRootSource.Contains("TextureButton _worldClockSpeedButton", StringComparison.Ordinal) &&
        bootstrapSource.Contains("GetRequiredNode<TextureButton>", StringComparison.Ordinal),
        "strategic world top controls should bind as TextureButton so Godot native texture states drive hover/click feedback");
}

internal static void StrategicWorldContextActionAreasUseConsistentButtonSizes()
{
    string root = ProjectRoot();
    string strategicHudPath = Path.Combine(root, "scenes", "world", "ui", "StrategicWorldHud.tscn");
    string strategicHud = File.ReadAllText(strategicHudPath);
    string controlsBlock = ExtractSceneNodeBlock(strategicHud, "[node name=\"TopRightControls\"");

    const float topRightButtonHeight = 54f;
    const float topRightButtonSeparation = 8f;
    (float width, float height) pauseSize = ReadSceneNodeMinimumSize(
        ExtractSceneNodeBlock(strategicHud, "[node name=\"PauseButton\""),
        "PauseButton",
        strategicHudPath);
    (float width, float height) quickSize = ReadSceneNodeMinimumSize(
        ExtractSceneNodeBlock(strategicHud, "[node name=\"QuickButton\""),
        "QuickButton",
        strategicHudPath);
    (float width, float height) resetSize = ReadSceneNodeMinimumSize(
        ExtractSceneNodeBlock(strategicHud, "[node name=\"ResetButton\""),
        "ResetButton",
        strategicHudPath);
    (float width, float height) controlsSize = ReadSceneNodeMinimumSize(controlsBlock, "TopRightControls", strategicHudPath);

    AssertTrue(
        pauseSize.width == topRightButtonHeight &&
        pauseSize.height == topRightButtonHeight &&
        quickSize.width == topRightButtonHeight &&
        quickSize.height == topRightButtonHeight &&
        resetSize.width == topRightButtonHeight &&
        resetSize.height == topRightButtonHeight,
        $"top-right controls should be one visual class: matching square icon buttons path={strategicHudPath}");
    AssertSceneTextureButtonUsesStateTextures(
        strategicHudPath,
        "ResetButton",
        "button_reset.png",
        "button_reset_hover.png",
        "button_reset_pressed.png",
        "top-bar reset button should use an authored reset TextureButton instead of a larger text button");

    float expectedControlsWidth = pauseSize.width + quickSize.width + resetSize.width + (topRightButtonSeparation * 2f);
    AssertTrue(
        controlsSize.width == expectedControlsWidth,
        $"top-right controls container should reserve exactly the authored button widths plus fixed gaps path={strategicHudPath} width={controlsSize.width} expected={expectedControlsWidth}");

    string primaryActionButtonPath = Path.Combine(root, "scenes", "world", "ui", "WorldPrimaryActionButton.tscn");
    string secondaryActionButtonPath = Path.Combine(root, "scenes", "world", "ui", "WorldSecondaryActionButton.tscn");
    (float width, float height) primarySize = ReadSceneNodeMinimumSize(
        ExtractSceneNodeBlock(File.ReadAllText(primaryActionButtonPath), "[node name=\"WorldPrimaryActionButton\""),
        "WorldPrimaryActionButton",
        primaryActionButtonPath);
    (float width, float height) secondarySize = ReadSceneNodeMinimumSize(
        ExtractSceneNodeBlock(File.ReadAllText(secondaryActionButtonPath), "[node name=\"WorldSecondaryActionButton\""),
        "WorldSecondaryActionButton",
        secondaryActionButtonPath);

    AssertTrue(
        primarySize.height == topRightButtonHeight &&
        secondarySize.height == topRightButtonHeight,
        $"city panel primary/secondary action buttons should share one operation-area height primary={primarySize.height} secondary={secondarySize.height}");
    AssertTrue(
        primarySize.width == 0f && secondarySize.width == 0f,
        "city panel action button templates should keep width container-driven while unifying height");
    AssertSceneButtonUsesActionStyle(
        primaryActionButtonPath,
        "WorldPrimaryActionButton",
        "city panel primary actions should use regular action button skin, not the ornamental plaque skin");
    AssertSceneButtonUsesActionStyle(
        secondaryActionButtonPath,
        "WorldSecondaryActionButton",
        "city panel secondary actions should use regular action button skin, not the ornamental plaque skin");

    string strategicDetailSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "StrategicWorldRoot.DetailHud.cs"));
    string strategicExpeditionSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "StrategicWorldRoot.ExpeditionHud.cs"));
    string siteFormattingSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "Sites", "WorldSiteRoot.SiteFormatting.cs"));
    AssertTrue(
        strategicDetailSource.Contains("button.Text = BuildActionButtonLabel(action);", StringComparison.Ordinal),
        "strategic location action buttons should use a one-line label and keep cost/failure details out of the button body");
    AssertTrue(
        strategicExpeditionSource.Contains("enterButton.Text = \"查看详情\";", StringComparison.Ordinal) &&
        strategicExpeditionSource.Contains("expeditionButton.Text = \"出征\";", StringComparison.Ordinal) &&
        strategicExpeditionSource.Contains("targetButton.Text = \"选择目的地\";", StringComparison.Ordinal) &&
        !strategicExpeditionSource.Contains("Text = \"出征\\n", StringComparison.Ordinal) &&
        !strategicExpeditionSource.Contains("Text = \"选择目的地\\n", StringComparison.Ordinal),
        "city panel expedition controls should keep action button labels short and single-line");
    AssertTrue(
        siteFormattingSource.Contains("BuildActionButtonLabel", StringComparison.Ordinal) &&
        siteFormattingSource.Contains("BuildActionTooltip", StringComparison.Ordinal) &&
        !siteFormattingSource.Contains("return $\"{action.DisplayName}\\n{suffix}\";", StringComparison.Ordinal),
        "site action formatting should keep button labels separate from tooltip detail text");
}

internal static void StrategicWorldTopBarControlsAreLayoutIndependent()
{
    string root = ProjectRoot();
    string scenePath = Path.Combine(root, "scenes", "world", "ui", "StrategicWorldHud.tscn");
    string scene = File.ReadAllText(scenePath);
    string controlsBlock = ExtractSceneNodeBlock(scene, "[node name=\"TopRightControls\"");

    AssertTrue(
        !scene.Contains("[node name=\"TopResourceRow\"", StringComparison.Ordinal) &&
        !scene.Contains("[node name=\"TopResourceBar\"", StringComparison.Ordinal),
        "top UI must not be a single resource bar or row; each top element should own its overlay placement");
    AssertTrue(
        controlsBlock.Contains("parent=\"TopBarHost\"", StringComparison.Ordinal) &&
        controlsBlock.Contains("layout_mode = 1", StringComparison.Ordinal) &&
        controlsBlock.Contains("anchor_left = 1.0", StringComparison.Ordinal) &&
        controlsBlock.Contains("anchor_right = 1.0", StringComparison.Ordinal),
        "top-bar controls should be independently anchored to the right edge of TopBarHost");

    string bootstrapSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "StrategicWorldRoot.UiBootstrap.cs"));
    AssertTrue(
        bootstrapSource.Contains("TopBarHost/TopRightControls/PauseButton", StringComparison.Ordinal) &&
        bootstrapSource.Contains("TopBarHost/TopRightControls/QuickButton", StringComparison.Ordinal) &&
        bootstrapSource.Contains("TopBarHost/TopRightControls/ResetButton", StringComparison.Ordinal),
        "strategic world code bindings should follow the independent top-bar controls path");
}

internal static void StrategicWorldHudUsesFullscreenOverlayContextLayout()
{
    string root = ProjectRoot();
    string scenePath = Path.Combine(root, "scenes", "world", "ui", "StrategicWorldHud.tscn");
    string scene = File.ReadAllText(scenePath);
    string panelBlock = ExtractSceneNodeBlock(scene, "[node name=\"SiteDetailPanel\"");
    string scrollBlock = ExtractSceneNodeBlock(scene, "[node name=\"Scroll\"");
    string contentBlock = ExtractSceneNodeBlock(scene, "[node name=\"Content\"");
    string bootstrapSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "StrategicWorldRoot.UiBootstrap.cs"));
    string detailSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "StrategicWorldRoot.DetailHud.cs"));

    AssertTrue(
        scene.Contains("[node name=\"TopLeftStatus\" type=\"VBoxContainer\" parent=\"TopBarHost\"]", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"NoticeLabel\" type=\"Label\" parent=\"TopBarHost\"]", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"WorldClockLabel\" type=\"Label\" parent=\"TopBarHost\"]", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"TopRightControls\" type=\"HBoxContainer\" parent=\"TopBarHost\"]", StringComparison.Ordinal),
        "strategic top HUD should be split into independently anchored overlay elements");

    AssertTrue(
        panelBlock.Contains("parent=\"OverlayHost\"", StringComparison.Ordinal) &&
        panelBlock.Contains("visible = false", StringComparison.Ordinal) &&
        panelBlock.Contains("anchor_left = 0.25", StringComparison.Ordinal) &&
        panelBlock.Contains("anchor_top = 0.75", StringComparison.Ordinal) &&
        panelBlock.Contains("anchor_right = 0.75", StringComparison.Ordinal) &&
        panelBlock.Contains("anchor_bottom = 1.0", StringComparison.Ordinal),
        "selected city detail should be a bottom-centered popup occupying half width and one quarter height");
    AssertTrue(
        contentBlock.Contains("type=\"HBoxContainer\"", StringComparison.Ordinal) &&
        contentBlock.Contains("parent=\"OverlayHost/SiteDetailPanel/Margin/Scroll\"", StringComparison.Ordinal),
        "selected city popup content should lay out horizontally, not as a left-side vertical menu panel");
    AssertTrue(
        scrollBlock.Contains("horizontal_scroll_mode = 0", StringComparison.Ordinal) &&
        !scrollBlock.Contains("horizontal_scroll_mode = 1", StringComparison.Ordinal),
        "selected city popup must not horizontally scroll or drift when the player uses the mouse wheel");
    AssertTrue(
        !scene.Contains("[node name=\"InfrastructureCard\"", StringComparison.Ordinal) &&
        !scene.Contains("[node name=\"DefenseCard\"", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"SummaryCard\"", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"ActionCard\"", StringComparison.Ordinal),
        "selected city popup should be a compact context/action sheet, not a four-column information board");
    AssertTrue(
        scene.Contains("res://assets/themes/game-ui-skin/basic_ui_1_theme.tres", StringComparison.Ordinal) &&
        panelBlock.Contains("theme_type_variation = &\"WorldContextSheet\"", StringComparison.Ordinal) &&
        !panelBlock.Contains("theme_override_styles/panel", StringComparison.Ordinal),
        "selected city detail popup should use the shared Theme's WorldContextSheet variation instead of an independent panel style override");

    AssertTrue(
        bootstrapSource.Contains("_siteDetailPanel", StringComparison.Ordinal) &&
        bootstrapSource.Contains("OverlayHost/SiteDetailPanel", StringComparison.Ordinal) &&
        !bootstrapSource.Contains("LeftPrimaryPanelHost/SiteDetailPanel", StringComparison.Ordinal),
        "strategic world bindings should target the bottom overlay popup, not the old left panel path");
    AssertTrue(
        detailSource.Contains("SiteDetailPanelOvershootPixels", StringComparison.Ordinal) &&
        detailSource.Contains("AnimateWorldDetailPanelIn", StringComparison.Ordinal) &&
        detailSource.Contains("AnimateWorldDetailPanelOut", StringComparison.Ordinal) &&
        detailSource.Contains("TweenProperty(_siteDetailPanel, \"position\"", StringComparison.Ordinal) &&
        detailSource.Contains("TweenProperty(_siteDetailPanel, \"scale\"", StringComparison.Ordinal) &&
        !detailSource.Contains("_siteDetailPanel.Visible = visible;", StringComparison.Ordinal),
        "selected city popup should use a bouncy overshoot tween instead of abruptly toggling visibility");
    AssertTrue(
        !bootstrapSource.Contains("_facilityList", StringComparison.Ordinal) &&
        !bootstrapSource.Contains("_garrisonList", StringComparison.Ordinal) &&
        !detailSource.Contains("AddStrategicFacilityLines", StringComparison.Ordinal) &&
        !detailSource.Contains("AddStrategicCorpsLines", StringComparison.Ordinal) &&
        !detailSource.Contains("BuildStrategicLocationBody", StringComparison.Ordinal),
        "selected city popup should bind only compact context and current actions instead of dumping facility and garrison lists");
}

static void AssertSceneTextureButtonUsesStateTextures(
    string scenePath,
    string buttonName,
    string normalTextureName,
    string hoverTextureName,
    string pressedTextureName,
    string reason)
{
    AssertTrue(File.Exists(scenePath), $"button scene should exist path={scenePath}");
    string scene = File.ReadAllText(scenePath);
    string nodeBlock = ExtractSceneNodeBlock(scene, $"[node name=\"{buttonName}\"");
    AssertTrue(
        nodeBlock.Contains($"[node name=\"{buttonName}\" type=\"TextureButton\"", StringComparison.Ordinal),
        $"{reason}; top-bar controls should be TextureButton nodes node={buttonName} path={scenePath}");
    AssertTrue(
        scene.Contains(normalTextureName, StringComparison.Ordinal) &&
        scene.Contains(hoverTextureName, StringComparison.Ordinal) &&
        scene.Contains(pressedTextureName, StringComparison.Ordinal),
        $"{reason}; scene should reference normal/hover/pressed textures node={buttonName} normal={normalTextureName} hover={hoverTextureName} pressed={pressedTextureName} path={scenePath}");
    AssertTrue(
        nodeBlock.Contains("texture_normal = ExtResource(\"", StringComparison.Ordinal) &&
        nodeBlock.Contains("texture_hover = ExtResource(\"", StringComparison.Ordinal) &&
        nodeBlock.Contains("texture_pressed = ExtResource(\"", StringComparison.Ordinal) &&
        nodeBlock.Contains("ignore_texture_size = true", StringComparison.Ordinal) &&
        nodeBlock.Contains("stretch_mode = 5", StringComparison.Ordinal),
        $"{reason}; top-bar TextureButtons should configure Godot native texture states and keep aspect centered inside the fixed button size node={buttonName} path={scenePath}");
    AssertTrue(
        !nodeBlock.Contains("icon = ExtResource(\"", StringComparison.Ordinal) &&
        !nodeBlock.Contains("expand_icon = true", StringComparison.Ordinal) &&
        !nodeBlock.Contains("flat = true", StringComparison.Ordinal) &&
        !nodeBlock.Contains("theme_override_styles/normal", StringComparison.Ordinal) &&
        !nodeBlock.Contains("button_compact", StringComparison.Ordinal),
        $"{reason}; top-bar TextureButtons should not fall back to Button icon or empty StyleBox skins node={buttonName} path={scenePath}");
    string text = ReadSceneNodeText(nodeBlock);
    AssertTrue(
        string.IsNullOrEmpty(text),
        $"{reason}; top-bar TextureButtons should not carry visible text node={buttonName} path={scenePath}");
}

static void AssertSceneButtonUsesCompactStyle(string scenePath, string buttonName, string reason, float textureMargin)
{
    AssertTrue(File.Exists(scenePath), $"button scene should exist path={scenePath}");
    string scene = File.ReadAllText(scenePath);
    string nodeBlock = ExtractSceneNodeBlock(scene, $"[node name=\"{buttonName}\"");
    AssertTrue(
        nodeBlock.Contains("theme_override_styles/normal = ExtResource(\"", StringComparison.Ordinal),
        $"button should define an authored normal style node={buttonName} path={scenePath}");
    AssertTrue(
        nodeBlock.Contains("button_compact", StringComparison.Ordinal),
        $"{reason}; small buttons should use compact scaled texture skin instead of the full-size primary button skin node={buttonName} path={scenePath}");

    (float width, float height) minimumSize = ReadSceneNodeMinimumSize(nodeBlock, buttonName, scenePath);
    string text = ReadSceneNodeText(nodeBlock);
    AssertTrue(
        !text.Contains('\n'),
        $"{reason}; compact text buttons should keep labels on one line node={buttonName} path={scenePath}");
    float requiredWidth = (textureMargin * 2f) + EstimateButtonTextWidth(text);
    float requiredHeight = (textureMargin * 2f) + EstimateButtonTextHeight(text);
    AssertTrue(
        minimumSize.width >= requiredWidth,
        $"{reason}; button width should fit both protected corner margins and text node={buttonName} path={scenePath} width={minimumSize.width} required={requiredWidth}");
    AssertTrue(
        minimumSize.height >= requiredHeight,
        $"{reason}; button height should fit both protected corner margins and text node={buttonName} path={scenePath} height={minimumSize.height} required={requiredHeight}");
}

static void AssertSceneButtonUsesActionStyle(string scenePath, string buttonName, string reason)
{
    AssertTrue(File.Exists(scenePath), $"button scene should exist path={scenePath}");
    string scene = File.ReadAllText(scenePath);
    string nodeBlock = ExtractSceneNodeBlock(scene, $"[node name=\"{buttonName}\"");
    AssertTrue(
        scene.Contains("basic_ui_1_theme.tres", StringComparison.Ordinal) &&
        nodeBlock.Contains("theme = ExtResource(\"1_theme\")", StringComparison.Ordinal) &&
        nodeBlock.Contains($"theme_type_variation = &\"{buttonName}\"", StringComparison.Ordinal),
        $"{reason}; action button templates should use shared Theme variation config node={buttonName} path={scenePath}");
    AssertTrue(
        !nodeBlock.Contains("theme_override_styles/normal", StringComparison.Ordinal) &&
        !nodeBlock.Contains("theme_override_styles/hover", StringComparison.Ordinal) &&
        !nodeBlock.Contains("theme_override_styles/pressed", StringComparison.Ordinal) &&
        !nodeBlock.Contains("theme_override_styles/disabled", StringComparison.Ordinal),
        $"{reason}; action button templates should not carry independent style overrides node={buttonName} path={scenePath}");
}

static float ReadResourceFloat(string resource, string key)
{
    foreach (string line in resource.Split('\n'))
    {
        string trimmed = line.Trim();
        if (!trimmed.StartsWith(key + " = ", StringComparison.Ordinal))
        {
            continue;
        }

        string value = trimmed[(key.Length + 3)..].Trim();
        return float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    throw new InvalidOperationException($"resource value missing key={key}");
}

static (float width, float height) ReadSceneNodeMinimumSize(string nodeBlock, string buttonName, string scenePath)
{
    foreach (string line in nodeBlock.Split('\n'))
    {
        string trimmed = line.Trim();
        if (!trimmed.StartsWith("custom_minimum_size = Vector2(", StringComparison.Ordinal))
        {
            continue;
        }

        string values = trimmed["custom_minimum_size = Vector2(".Length..].TrimEnd(')');
        string[] parts = values.Split(',', StringSplitOptions.TrimEntries);
        AssertTrue(parts.Length == 2, $"button minimum size should have width and height node={buttonName} path={scenePath}");
        return (
            float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
            float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
    }

    throw new InvalidOperationException($"button minimum size missing node={buttonName} path={scenePath}");
}

static string ReadSceneNodeText(string nodeBlock)
{
    foreach (string line in nodeBlock.Split('\n'))
    {
        string trimmed = line.Trim();
        if (!trimmed.StartsWith("text = \"", StringComparison.Ordinal) || !trimmed.EndsWith("\"", StringComparison.Ordinal))
        {
            continue;
        }

        string value = trimmed["text = \"".Length..^1];
        return value.Replace("\\n", "\n", StringComparison.Ordinal);
    }

    return string.Empty;
}

static float EstimateButtonTextWidth(string text)
{
    string[] lines = text.Split('\n');
    int maxChars = lines.Length == 0 ? 0 : lines.Max(line => line.Length);
    return maxChars * 16f;
}

static float EstimateButtonTextHeight(string text)
{
    int lineCount = string.IsNullOrEmpty(text) ? 1 : text.Split('\n').Length;
    return lineCount * 18f;
}
}
