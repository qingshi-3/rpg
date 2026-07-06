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

internal static void StrategicWorldUiUsesManaSoulGuiSkin()
{
    string root = ProjectRoot();
    string skinDir = Path.Combine(root, "resource", "ui", "themes", "game-ui-skin");
    string themePath = Path.Combine(skinDir, "basic_ui_1_theme.tres");
    (string fileName, string textureName)[] panelStyleResources =
    {
        ("basic_ui_1_panel_large.tres", "20250420manaSoul9SlicesC-Sheet.png"),
        ("basic_ui_1_panel_sheet.tres", "20250420manaSoul9SlicesA-Sheet.png"),
        ("basic_ui_1_panel_topbar.tres", "20250420manaSoul9SlicesE-Sheet.png"),
        ("basic_ui_1_panel_card.tres", "20250420manaSoul9SlicesB-Sheet.png")
    };

    foreach ((string fileName, string textureName) in panelStyleResources)
    {
        string path = Path.Combine(skinDir, fileName);
        AssertTrue(File.Exists(path), $"strategic UI panel skin resource should exist path={path}");
        string source = File.ReadAllText(path);
        AssertTrue(
            source.Contains("[gd_resource type=\"StyleBoxTexture\"", StringComparison.Ordinal),
            $"strategic UI panel skin resource should be a StyleBoxTexture path={path}");
        AssertTrue(
            source.Contains($"assets/textures/ui/tinyrpg_manasoulgui_v_1_0/{textureName}", StringComparison.Ordinal) &&
            source.Contains("region = Rect2(0, 0, 96, 96)", StringComparison.Ordinal),
            $"strategic UI outer panel skin resource should use a ManaSoul 9-slice texture={textureName} path={path}");
        AssertTrue(
            !source.Contains("assets/textures/ui/basic-ui/2/", StringComparison.Ordinal) &&
            !source.Contains("assets/textures/ui/basic-ui/3/", StringComparison.Ordinal) &&
            !source.Contains("assets/textures/ui/basic-ui/need-human/", StringComparison.Ordinal) &&
            !source.Contains("assets/textures/ui/travel-book-lite/", StringComparison.Ordinal),
            $"strategic UI outer panel skin resource should not mix legacy UI packs path={path}");
        AssertTrue(
            source.Contains("texture_margin_left", StringComparison.Ordinal) &&
            source.Contains("texture_margin_top", StringComparison.Ordinal) &&
            source.Contains("texture_margin_right", StringComparison.Ordinal) &&
            source.Contains("texture_margin_bottom", StringComparison.Ordinal),
            $"strategic UI panel skin resource should define nine-patch margins path={path}");
    }

    string slotPath = Path.Combine(skinDir, "basic_ui_1_panel_slot.tres");
    AssertTrue(File.Exists(slotPath), $"strategic UI inner slot skin resource should exist path={slotPath}");
    string slotSource = File.ReadAllText(slotPath);
    AssertTrue(
        slotSource.Contains("assets/textures/ui/basic-ui/1/", StringComparison.Ordinal) &&
        !slotSource.Contains("assets/textures/ui/tinyrpg_manasoulgui_v_1_0/20250420manaSoul9Slices", StringComparison.Ordinal),
        $"strategic UI inner slot skin can stay on the prior compact slot resource while outer panels move to ManaSoul path={slotPath}");

    (string fileName, string sheetName, float regionX)[] buttonStyleResources =
    {
        ("basic_ui_1_button_primary.tres", "20250421manaSoulButtonB-Sheet.png", 0f),
        ("basic_ui_1_button_primary_hover.tres", "20250421manaSoulButtonB-Sheet.png", 96f),
        ("basic_ui_1_button_primary_pressed.tres", "20250421manaSoulButtonB-Sheet.png", 192f),
        ("basic_ui_1_button_disabled.tres", "20250421manaSoulButtonB-Sheet.png", 288f),
        ("basic_ui_1_button_action.tres", "20250421manaSoulButtonB-Sheet.png", 0f),
        ("basic_ui_1_button_action_hover.tres", "20250421manaSoulButtonB-Sheet.png", 96f),
        ("basic_ui_1_button_action_pressed.tres", "20250421manaSoulButtonB-Sheet.png", 192f),
        ("basic_ui_1_button_action_disabled.tres", "20250421manaSoulButtonB-Sheet.png", 288f),
        ("basic_ui_1_button_compact.tres", "20250421manaSoulButtonA-Sheet.png", 0f),
        ("basic_ui_1_button_compact_hover.tres", "20250421manaSoulButtonA-Sheet.png", 96f),
        ("basic_ui_1_button_compact_pressed.tres", "20250421manaSoulButtonA-Sheet.png", 192f),
        ("basic_ui_1_button_compact_disabled.tres", "20250421manaSoulButtonA-Sheet.png", 288f)
    };

    foreach ((string fileName, string sheetName, float regionX) in buttonStyleResources)
    {
        string path = Path.Combine(skinDir, fileName);
        AssertManaSoulButtonStyleResource(path, sheetName, regionX, "strategic UI button skin");
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
        themeSource.Contains("basic_ui_1_button_action_disabled.tres", StringComparison.Ordinal) &&
        themeSource.Contains("basic_ui_1_button_compact.tres", StringComparison.Ordinal) &&
        themeSource.Contains("basic_ui_1_button_compact_hover.tres", StringComparison.Ordinal) &&
        themeSource.Contains("basic_ui_1_button_compact_pressed.tres", StringComparison.Ordinal) &&
        themeSource.Contains("basic_ui_1_button_compact_disabled.tres", StringComparison.Ordinal),
        $"strategic UI shared theme should centralize context sheet and action button skin resources path={themePath}");
    AssertTrue(
        themeSource.Contains("WorldContextSheet/base_type = &\"PanelContainer\"", StringComparison.Ordinal) &&
        themeSource.Contains("WorldContextSheet/styles/panel = ExtResource(", StringComparison.Ordinal) &&
        themeSource.Contains("WorldHoverInfoPanel/base_type = &\"PanelContainer\"", StringComparison.Ordinal) &&
        themeSource.Contains("WorldHoverInfoPanel/styles/panel = ExtResource(\"1_panel_sheet\")", StringComparison.Ordinal) &&
        themeSource.Contains("WorldPrimaryActionButton/base_type = &\"Button\"", StringComparison.Ordinal) &&
        themeSource.Contains("WorldSecondaryActionButton/base_type = &\"Button\"", StringComparison.Ordinal) &&
        themeSource.Contains("WorldCompactActionButton/base_type = &\"Button\"", StringComparison.Ordinal) &&
        themeSource.Contains("WorldCompactTabButton/base_type = &\"Button\"", StringComparison.Ordinal),
        $"strategic UI shared theme should expose named type variations for authored UI roles path={themePath}");

    string[] targetScenes =
    {
        Path.Combine(root, "scenes", "world", "ui", "StrategicWorldHud.tscn"),
        Path.Combine(root, "scenes", "world", "ui", "WorldSitePeacetimeHud.tscn"),
        Path.Combine(root, "scenes", "world", "ui", "BattlePreparationRosterRow.tscn"),
        Path.Combine(root, "scenes", "world", "ui", "BattleRuntimeSkillSlot.tscn"),
        Path.Combine(root, "scenes", "world", "ui", "BattleRuntimeHeroSwitchButton.tscn"),
        Path.Combine(root, "scenes", "world", "ui", "BattlePreparationObjectiveThumbnail.tscn"),
        Path.Combine(root, "scenes", "world", "ui", "WorldSiteHoverSummaryPanel.tscn"),
        Path.Combine(root, "scenes", "world", "ui", "WorldPrimaryActionButton.tscn"),
        Path.Combine(root, "scenes", "world", "ui", "WorldSecondaryActionButton.tscn"),
        Path.Combine(root, "scenes", "world", "ui", "WorldCompactMarkerButton.tscn"),
        Path.Combine(root, "scenes", "world", "ui", "WorldDeploymentMarkerButton.tscn"),
        Path.Combine(root, "scenes", "world", "ui", "WorldExpeditionCountRow.tscn"),
        Path.Combine(root, "scenes", "world", "ui", "WorldOpportunityDetailPanel.tscn")
    };

    foreach (string scenePath in targetScenes)
    {
        AssertTrue(File.Exists(scenePath), $"strategic UI scene should exist path={scenePath}");
        string scene = File.ReadAllText(scenePath);
        AssertTrue(
            scene.Contains("res://resource/ui/themes/game-ui-skin/basic_ui_1_", StringComparison.Ordinal),
            $"strategic UI scene should use shared game UI skin resources path={scenePath}");
        AssertTrue(
            !scene.Contains("StyleBoxFlat", StringComparison.Ordinal),
            $"strategic UI visual validation target should not use local StyleBoxFlat blocks path={scenePath}");
    }

    string strategicHud = File.ReadAllText(Path.Combine(root, "scenes", "world", "ui", "StrategicWorldHud.tscn"));
    AssertTrue(
        strategicHud.Contains("res://resource/ui/themes/game-ui-skin/basic_ui_1_theme.tres", StringComparison.Ordinal),
        "strategic world HUD should attach the shared world UI Theme instead of styling key panels independently");
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
        hoverSummary.Contains("res://resource/ui/themes/game-ui-skin/basic_ui_1_theme.tres", StringComparison.Ordinal) &&
        hoverSummaryRootBlock.Contains("theme_type_variation = &\"WorldHoverInfoPanel\"", StringComparison.Ordinal) &&
        !hoverSummaryRootBlock.Contains("theme_override_styles/panel", StringComparison.Ordinal),
        "strategic-world hover summary should use the shared hover info Theme variation instead of a local panel style");
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
        siteHud.Contains("res://resource/ui/themes/game-ui-skin/basic_ui_1_theme.tres", StringComparison.Ordinal) &&
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
        objectiveDialog.Contains("res://resource/ui/themes/game-ui-skin/basic_ui_1_theme.tres", StringComparison.Ordinal),
        "battle objective map dialog should use the shared game UI Theme while keeping the same authored role variations");
    AssertTrue(
        dialogPanelBlock.Contains("theme_type_variation = &\"WorldContextSheet\"", StringComparison.Ordinal) &&
        !dialogPanelBlock.Contains("theme_override_styles/panel", StringComparison.Ordinal),
        "battle objective map dialog shell should use the context-sheet Theme variation");
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
        $"battle objective map dialog should budget enough height for the modal Theme margins and controls height={dialogHeight} required={requiredDialogHeight}");
}

internal static void UiThemesKeepPopupShellsTransparent()
{
    string root = ProjectRoot();
    string themesRoot = Path.Combine(root, "resource", "ui", "themes");
    AssertTrue(Directory.Exists(themesRoot), $"UI theme root should exist path={themesRoot}");

    List<string> themePaths = Directory
        .GetFiles(themesRoot, "*.tres", SearchOption.AllDirectories)
        .Where(path => File.ReadAllText(path).Contains("[gd_resource type=\"Theme\"", StringComparison.Ordinal))
        .OrderBy(path => path)
        .ToList();
    AssertTrue(themePaths.Count > 0, $"UI theme scan should find Godot Theme resources root={themesRoot}");

    foreach (string themePath in themePaths)
    {
        string source = File.ReadAllText(themePath);
        string relativePath = Path.GetRelativePath(root, themePath);
        AssertTrue(
            source.Contains("[sub_resource type=\"StyleBoxEmpty\" id=\"StyleBoxEmpty_popup_panel\"]", StringComparison.Ordinal) &&
            source.Contains("PopupPanel/styles/panel = SubResource(\"StyleBoxEmpty_popup_panel\")", StringComparison.Ordinal) &&
            source.Contains("TooltipPanel/styles/panel = SubResource(\"StyleBoxEmpty_popup_panel\")", StringComparison.Ordinal),
            $"UI Theme should keep native popup and tooltip shells transparent so authored panels provide the only visible frame path={relativePath}");
        AssertThemeUsesUnifiedScrollbarSkin(source, relativePath);
    }
}

internal static void RecruitmentUiV1AssetsAreGeneratedAsResourceBackedThemeCandidates()
{
    string root = ProjectRoot();
    string textureDir = Path.Combine(root, "assets", "textures", "ui", "recruitment-ui-v1");
    string themeDir = Path.Combine(root, "resource", "ui", "themes", "recruitment-ui-v1");
    string themePath = Path.Combine(themeDir, "recruitment_ui_v1_theme.tres");

    AssertTrue(Directory.Exists(textureDir), $"recruitment UI v1 texture directory should exist path={textureDir}");
    AssertTrue(Directory.Exists(themeDir), $"recruitment UI v1 theme directory should exist path={themeDir}");

    string[] textureNames =
    {
        "recruitment_modal_panel.png",
        "recruitment_card_normal.png",
        "recruitment_card_hover.png",
        "recruitment_card_pressed.png",
        "recruitment_card_selected.png",
        "recruitment_card_disabled.png",
        "recruitment_unit_plinth_normal.png",
        "recruitment_unit_plinth_hover.png",
        "recruitment_unit_plinth_pressed.png",
        "recruitment_unit_plinth_selected.png",
        "recruitment_unit_plinth_disabled.png",
        "recruitment_nameplate_normal.png",
        "recruitment_nameplate_selected.png",
        "recruitment_nameplate_pressed.png",
        "recruitment_nameplate_disabled.png",
        "recruitment_text_button_normal.png",
        "recruitment_text_button_hover.png",
        "recruitment_text_button_pressed.png",
        "recruitment_text_button_disabled.png",
        "recruitment_icon_button_normal.png",
        "recruitment_icon_button_hover.png",
        "recruitment_icon_button_pressed.png",
        "recruitment_icon_button_disabled.png",
        "recruitment_tooltip_panel.png",
        "recruitment_divider_purple.png",
        "recruitment_divider_blue.png",
        "recruitment_resource_chip_normal.png",
        "recruitment_resource_chip_selected.png",
        "recruitment_resource_chip_pressed.png",
        "recruitment_resource_chip_disabled.png",
        "recruitment_socket_circle_gold.png",
        "recruitment_socket_diamond_gold.png",
        "recruitment_scroll_track_bar_b.png",
        "recruitment_scroll_grabber_normal.png",
        "recruitment_scroll_grabber_hover.png",
        "recruitment_scroll_grabber_pressed.png",
        "recruitment_scroll_track_bar_b_horizontal.png",
        "recruitment_scroll_grabber_horizontal_normal.png",
        "recruitment_scroll_grabber_horizontal_hover.png",
        "recruitment_scroll_grabber_horizontal_pressed.png"
    };

    foreach (string textureName in textureNames)
    {
        string path = Path.Combine(textureDir, textureName);
        AssertTrue(File.Exists(path), $"recruitment UI v1 texture should exist texture={textureName} path={path}");
        AssertTrue(
            new FileInfo(path).Length > 0,
            $"recruitment UI v1 texture should not be empty texture={textureName} path={path}");
        if (textureName.StartsWith("recruitment_scroll_", StringComparison.Ordinal))
        {
            (int width, int height) = ReadPngDimensions(path);
            bool isHorizontal = textureName.Contains("_horizontal", StringComparison.Ordinal);
            if (isHorizontal)
            {
                (int minY, int maxY) = ReadPngAlphaYBounds(path);
                int visibleHeight = maxY - minY + 1;
                AssertTrue(
                    height >= 28,
                    $"recruitment UI v1 horizontal scrollbar textures should have a tall transparent gutter for layout and input texture={textureName} height={height} path={path}");
                AssertTrue(
                    visibleHeight <= 18,
                    $"recruitment UI v1 horizontal scrollbar textures should keep the ManaSoul visible art narrow instead of stretching it texture={textureName} visibleHeight={visibleHeight} path={path}");
                AssertTrue(
                    minY >= 4 && maxY <= height - 5,
                    $"recruitment UI v1 horizontal scrollbar visible art should sit inside the transparent gutter texture={textureName} minY={minY} maxY={maxY} height={height} path={path}");
                continue;
            }

            (int minX, int maxX) = ReadPngAlphaXBounds(path);
            int visibleWidth = maxX - minX + 1;
            AssertTrue(
                width >= 28,
                $"recruitment UI v1 scrollbar textures should have a wide transparent gutter for layout and input texture={textureName} width={width} path={path}");
            AssertTrue(
                visibleWidth <= 18,
                $"recruitment UI v1 scrollbar textures should keep the ManaSoul visible art narrow instead of stretching it texture={textureName} visibleWidth={visibleWidth} path={path}");
            AssertTrue(
                minX >= 4 && maxX <= width - 5,
                $"recruitment UI v1 scrollbar visible art should sit inside the transparent gutter texture={textureName} minX={minX} maxX={maxX} width={width} path={path}");
            if (textureName.StartsWith("recruitment_scroll_grabber_", StringComparison.Ordinal))
            {
                bool hasUpperLeftAccent = PngRegionContainsColor(
                    path,
                    minX,
                    minX + 1,
                    Math.Min(8, height - 1),
                    Math.Min(12, height - 1),
                    IsBrightScrollbarEdgeAccent);
                bool hasLowerRightAccent = PngRegionContainsColor(
                    path,
                    maxX - 1,
                    maxX,
                    Math.Max(0, height - 12),
                    Math.Max(0, height - 7),
                    IsOrangeScrollbarEdgeAccent);
                AssertTrue(
                    !hasUpperLeftAccent && !hasLowerRightAccent,
                    $"recruitment UI v1 scrollbar grabber should keep accent pixels inside the thumb body instead of on the outer edge texture={textureName} path={path}");
            }
        }
    }

    string[] styleResourceNames =
    {
        "recruitment_modal_panel.tres",
        "recruitment_card_normal.tres",
        "recruitment_card_hover.tres",
        "recruitment_card_pressed.tres",
        "recruitment_card_selected.tres",
        "recruitment_card_disabled.tres",
        "recruitment_nameplate_normal.tres",
        "recruitment_nameplate_selected.tres",
        "recruitment_nameplate_pressed.tres",
        "recruitment_nameplate_disabled.tres",
        "recruitment_text_button_normal.tres",
        "recruitment_text_button_hover.tres",
        "recruitment_text_button_pressed.tres",
        "recruitment_text_button_disabled.tres",
        "recruitment_icon_button_normal.tres",
        "recruitment_icon_button_hover.tres",
        "recruitment_icon_button_pressed.tres",
        "recruitment_icon_button_disabled.tres",
        "recruitment_tooltip_panel.tres",
        "recruitment_scroll_track.tres",
        "recruitment_scroll_grabber_normal.tres",
        "recruitment_scroll_grabber_hover.tres",
        "recruitment_scroll_grabber_pressed.tres",
        "recruitment_scroll_track_horizontal.tres",
        "recruitment_scroll_grabber_horizontal_normal.tres",
        "recruitment_scroll_grabber_horizontal_hover.tres",
        "recruitment_scroll_grabber_horizontal_pressed.tres"
    };

    foreach (string resourceName in styleResourceNames)
    {
        string path = Path.Combine(themeDir, resourceName);
        AssertTrue(File.Exists(path), $"recruitment UI v1 StyleBox resource should exist resource={resourceName} path={path}");
        string source = File.ReadAllText(path);
        AssertTrue(
            source.Contains("[gd_resource type=\"StyleBoxTexture\"", StringComparison.Ordinal) &&
            source.Contains("assets/textures/ui/recruitment-ui-v1/", StringComparison.Ordinal) &&
            source.Contains("texture_margin_left", StringComparison.Ordinal) &&
            source.Contains("content_margin_left", StringComparison.Ordinal),
            $"recruitment UI v1 StyleBox resource should be texture-backed with margins resource={resourceName} path={path}");
        AssertTrue(
            !source.Contains("assets/textures/ui/basic-ui/", StringComparison.Ordinal) &&
            !source.Contains("assets/textures/ui/tinyrpg_manasoulgui_v_1_0/", StringComparison.Ordinal) &&
            !source.Contains("assets/textures/ui/travel-book-lite/", StringComparison.Ordinal),
            $"recruitment UI v1 StyleBox resource should stay isolated from older UI packs resource={resourceName} path={path}");
        if (resourceName.StartsWith("recruitment_scroll_", StringComparison.Ordinal))
        {
            bool isHorizontal = resourceName.Contains("_horizontal", StringComparison.Ordinal);
            if (isHorizontal)
            {
                float topMargin = ReadResourceFloat(source, "content_margin_top");
                float bottomMargin = ReadResourceFloat(source, "content_margin_bottom");
                AssertTrue(
                    topMargin + bottomMargin >= 24f,
                    $"recruitment UI v1 horizontal scrollbar StyleBox should reserve a usable bar height resource={resourceName} height={topMargin + bottomMargin} path={path}");
            }
            else
            {
                float leftMargin = ReadResourceFloat(source, "content_margin_left");
                float rightMargin = ReadResourceFloat(source, "content_margin_right");
                AssertTrue(
                    leftMargin + rightMargin >= 24f,
                    $"recruitment UI v1 scrollbar StyleBox should reserve a usable vertical bar width resource={resourceName} width={leftMargin + rightMargin} path={path}");
            }
        }
    }

    AssertTrue(File.Exists(themePath), $"recruitment UI v1 Theme resource should exist path={themePath}");
    string themeSource = File.ReadAllText(themePath);
    AssertTrue(
        themeSource.Contains("[gd_resource type=\"Theme\"", StringComparison.Ordinal) &&
        themeSource.Contains("PopupPanel/styles/panel = SubResource(\"StyleBoxEmpty_popup_panel\")", StringComparison.Ordinal) &&
        themeSource.Contains("TooltipPanel/styles/panel = SubResource(\"StyleBoxEmpty_popup_panel\")", StringComparison.Ordinal),
        $"recruitment UI v1 Theme should be a Godot Theme and keep popup shells transparent path={themePath}");
    AssertTrue(
        themeSource.Contains("RecruitmentModalPanel/base_type = &\"PanelContainer\"", StringComparison.Ordinal) &&
        themeSource.Contains("RecruitmentCardButton/base_type = &\"Button\"", StringComparison.Ordinal) &&
        themeSource.Contains("RecruitmentSelectableCardButton/base_type = &\"Button\"", StringComparison.Ordinal) &&
        themeSource.Contains("RecruitmentTextButton/base_type = &\"Button\"", StringComparison.Ordinal) &&
        themeSource.Contains("RecruitmentIconButton/base_type = &\"Button\"", StringComparison.Ordinal) &&
        themeSource.Contains("RecruitmentNameplatePanel/base_type = &\"PanelContainer\"", StringComparison.Ordinal) &&
        themeSource.Contains("RecruitmentTooltipPanel/base_type = &\"PanelContainer\"", StringComparison.Ordinal),
        $"recruitment UI v1 Theme should expose reusable type variations for the candidate recruitment UI kit path={themePath}");
    AssertTrue(
        themeSource.Contains("RecruitmentCardButton/styles/hover_pressed = ExtResource(\"4_card_pressed\")", StringComparison.Ordinal) &&
        themeSource.Contains("RecruitmentCardButton/styles/normal = ExtResource(\"3_card_hover\")", StringComparison.Ordinal) &&
        themeSource.Contains("RecruitmentCardButton/styles/hover = ExtResource(\"2_card_normal\")", StringComparison.Ordinal) &&
        themeSource.Contains("RecruitmentCardButton/styles/pressed = ExtResource(\"4_card_pressed\")", StringComparison.Ordinal) &&
        themeSource.Contains("RecruitmentSelectableCardButton/styles/normal = ExtResource(\"3_card_hover\")", StringComparison.Ordinal) &&
        themeSource.Contains("RecruitmentSelectableCardButton/styles/hover = ExtResource(\"2_card_normal\")", StringComparison.Ordinal) &&
        themeSource.Contains("RecruitmentSelectableCardButton/styles/pressed = ExtResource(\"5_card_selected\")", StringComparison.Ordinal) &&
        themeSource.Contains("RecruitmentSelectableCardButton/styles/hover_pressed = ExtResource(\"5_card_selected\")", StringComparison.Ordinal) &&
        !themeSource.Contains("RecruitmentSelectableCardButton/styles/hover_pressed = ExtResource(\"4_card_pressed\")", StringComparison.Ordinal),
        $"recruitment card themes should swap normal/hover visuals while toggleable hero cards keep selected-hover on the selected frame instead of the dark click frame path={themePath}");
    AssertTrue(
        themeSource.Contains("VScrollBar/styles/scroll = ExtResource(\"20_scroll_track\")", StringComparison.Ordinal) &&
        themeSource.Contains("VScrollBar/styles/scroll_focus = ExtResource(\"20_scroll_track\")", StringComparison.Ordinal) &&
        themeSource.Contains("VScrollBar/styles/grabber = ExtResource(\"21_scroll_grabber_normal\")", StringComparison.Ordinal) &&
        themeSource.Contains("VScrollBar/styles/grabber_highlight = ExtResource(\"22_scroll_grabber_hover\")", StringComparison.Ordinal) &&
        themeSource.Contains("VScrollBar/styles/grabber_pressed = ExtResource(\"23_scroll_grabber_pressed\")", StringComparison.Ordinal),
        $"recruitment modal scroll containers should use the ManaSoul-compatible vertical scrollbar skin instead of Godot defaults path={themePath}");

    string worldSiteHudScenePath = Path.Combine(root, "scenes", "world", "ui", "WorldSitePeacetimeHud.tscn");
    string worldSiteHudScene = File.ReadAllText(worldSiteHudScenePath);
    string heroScrollBlock = ExtractSceneNodeBlock(worldSiteHudScene, "[node name=\"MilitaryHeroScroll\"");
    string musterScrollBlock = ExtractSceneNodeBlock(worldSiteHudScene, "[node name=\"MilitaryMusterScroll\"");
    (float heroScrollWidth, _) = ReadSceneNodeMinimumSize(heroScrollBlock, "MilitaryHeroScroll", worldSiteHudScenePath);
    AssertTrue(
        heroScrollWidth >= 384f,
        $"recruitment hero list should reserve enough layout width for full-size hero cards plus the themed scrollbar path={worldSiteHudScenePath}");
    AssertTrue(
        heroScrollBlock.Contains("vertical_scroll_mode = 4", StringComparison.Ordinal) &&
        musterScrollBlock.Contains("vertical_scroll_mode = 4", StringComparison.Ordinal),
        $"recruitment scroll containers should reserve vertical scrollbar space so content and the visible bar do not fight for the same pixels path={worldSiteHudScenePath}");
}

internal static void StrategicWorldUiButtonSkinsFitAuthoredButtonSizes()
{
    string root = ProjectRoot();
    string skinDir = Path.Combine(root, "resource", "ui", "themes", "game-ui-skin");
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
        float regionX = fileName switch
        {
            "basic_ui_1_button_compact_hover.tres" => 96f,
            "basic_ui_1_button_compact_pressed.tres" => 192f,
            "basic_ui_1_button_compact_disabled.tres" => 288f,
            _ => 0f
        };
        string source = AssertManaSoulButtonStyleResource(path, "20250421manaSoulButtonA-Sheet.png", regionX, "compact button skin");

        float left = ReadResourceFloat(source, "texture_margin_left");
        float top = ReadResourceFloat(source, "texture_margin_top");
        float right = ReadResourceFloat(source, "texture_margin_right");
        float bottom = ReadResourceFloat(source, "texture_margin_bottom");
        AssertTrue(
            left == 18f && right == 18f,
            $"compact button skin should preserve ManaSoul ButtonA horizontal end caps path={path}");
        AssertTrue(
            top == 0f && bottom == 0f,
            $"compact button skin should use ManaSoul ButtonA as a horizontal three-slice path={path}");
    }

    float compactHorizontalPadding = 18f;
    float compactVerticalPadding = 5f;
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
        float regionX = fileName switch
        {
            "basic_ui_1_button_action_hover.tres" => 96f,
            "basic_ui_1_button_action_pressed.tres" => 192f,
            "basic_ui_1_button_action_disabled.tres" => 288f,
            _ => 0f
        };
        AssertManaSoulButtonStyleResource(path, "20250421manaSoulButtonB-Sheet.png", regionX, "action button skin");
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
    string themeSource = File.ReadAllText(Path.Combine(skinDir, "basic_ui_1_theme.tres"));
    AssertTrue(
        themeSource.Contains("WorldCompactActionButton/styles/normal = ExtResource(\"8_button_compact\")", StringComparison.Ordinal) &&
        themeSource.Contains("WorldCompactActionButton/styles/hover = ExtResource(\"9_button_compact_hover\")", StringComparison.Ordinal) &&
        themeSource.Contains("WorldCompactActionButton/styles/hover_pressed = ExtResource(\"10_button_compact_pressed\")", StringComparison.Ordinal) &&
        themeSource.Contains("WorldCompactActionButton/styles/pressed = ExtResource(\"10_button_compact_pressed\")", StringComparison.Ordinal) &&
        themeSource.Contains("WorldCompactActionButton/styles/disabled = ExtResource(\"11_button_compact_disabled\")", StringComparison.Ordinal),
        "compact action buttons should get all state skins from the shared Theme variation");
    AssertTrue(
        themeSource.Contains("WorldCompactTabButton/styles/normal = ExtResource(\"8_button_compact\")", StringComparison.Ordinal) &&
        themeSource.Contains("WorldCompactTabButton/styles/hover = ExtResource(\"9_button_compact_hover\")", StringComparison.Ordinal) &&
        themeSource.Contains("WorldCompactTabButton/styles/hover_pressed = ExtResource(\"9_button_compact_hover\")", StringComparison.Ordinal) &&
        themeSource.Contains("WorldCompactTabButton/styles/pressed = ExtResource(\"8_button_compact\")", StringComparison.Ordinal) &&
        themeSource.Contains("WorldCompactTabButton/styles/disabled = ExtResource(\"11_button_compact_disabled\")", StringComparison.Ordinal),
        "compact tab buttons should use a reusable Theme variation whose selected state keeps the ManaSoul normal frame");

    AssertSceneButtonUsesCompactStyle(siteHudPath, "BattleRuntimeRegroupButton", "runtime regroup button is narrower than the large ornamental button skin", compactHorizontalPadding, compactVerticalPadding, "WorldCompactActionButton");
    AssertSceneButtonUsesCompactStyle(siteHudPath, "MoveFirstRuleButton", "battle-preparation rule button is a compact control", compactHorizontalPadding, compactVerticalPadding, "WorldCompactActionButton");
    AssertSceneButtonUsesCompactStyle(siteHudPath, "AttackFirstRuleButton", "battle-preparation rule button is a compact control", compactHorizontalPadding, compactVerticalPadding, "WorldCompactActionButton");
    AssertSceneButtonUsesCompactStyle(siteHudPath, "HoldRuleButton", "battle-preparation rule button is a compact control", compactHorizontalPadding, compactVerticalPadding, "WorldCompactActionButton");
    AssertSceneButtonUsesCompactStyle(siteHudPath, "BattlePreparationStartButton", "battle-preparation start button is a compact control", compactHorizontalPadding, compactVerticalPadding, "WorldCompactActionButton");
    AssertSceneButtonUsesRecruitmentStyle(siteHudPath, "MilitaryBackButton", "RecruitmentTextButton", "military workbench back button belongs to the focused recruitment modal skin");
    AssertSceneButtonUsesRecruitmentStyle(siteHudPath, "MilitaryCloseButton", "RecruitmentTextButton", "military workbench close button belongs to the focused recruitment modal skin");
    AssertSceneButtonUsesCompactStyle(siteHudPath, "ReturnMapButton", "site top-bar return button is a compact control", compactHorizontalPadding, compactVerticalPadding, "WorldCompactActionButton");
    AssertSceneButtonUsesCompactStyle(siteHudPath, "BuildTabButton", "site management tab is a compact toggle control", compactHorizontalPadding, compactVerticalPadding, "WorldCompactTabButton");
    AssertSceneButtonUsesCompactStyle(siteHudPath, "ConscriptionTabButton", "site management tab is a compact toggle control", compactHorizontalPadding, compactVerticalPadding, "WorldCompactTabButton");
    AssertSceneButtonUsesCompactStyle(siteHudPath, "RecruitTabButton", "site management tab is a compact toggle control", compactHorizontalPadding, compactVerticalPadding, "WorldCompactTabButton");
    AssertSceneButtonUsesCompactStyle(siteHudPath, "CorpsTabButton", "site management tab is a compact toggle control", compactHorizontalPadding, compactVerticalPadding, "WorldCompactTabButton");
    AssertSceneButtonUsesCompactStyle(siteHudPath, "OverviewTabButton", "site management tab is a compact toggle control", compactHorizontalPadding, compactVerticalPadding, "WorldCompactTabButton");

    AssertSceneButtonUsesCompactStyle(
        Path.Combine(root, "scenes", "world", "ui", "BattleRuntimeHeroSwitchButton.tscn"),
        "BattleRuntimeHeroSwitchButton",
        "battle runtime hero switch button is narrower than the large ornamental button skin",
        compactHorizontalPadding,
        compactVerticalPadding,
        "WorldCompactActionButton");

    AssertSceneButtonUsesRecruitmentStyle(
        Path.Combine(root, "scenes", "world", "ui", "WorldMusterOptionCard.tscn"),
        "WorldMusterOptionCard",
        "RecruitmentCardButton",
        "muster option card belongs to the focused recruitment modal skin");
    AssertSceneButtonUsesSharedCardSkin(
        Path.Combine(root, "scenes", "world", "ui", "WorldMilitaryHeroCard.tscn"),
        "WorldMilitaryHeroCard",
        "military hero card can still be reused by non-modal management tabs without adopting the recruitment modal skin");
    AssertSceneButtonUsesRecruitmentStyle(
        Path.Combine(root, "scenes", "world", "ui", "WorldMilitaryWorkbenchHeroCard.tscn"),
        "WorldMilitaryHeroCard",
        "RecruitmentSelectableCardButton",
        "military workbench hero card belongs to the focused recruitment modal skin");
    AssertSceneButtonUsesThemeVariation(
        Path.Combine(root, "scenes", "world", "ui", "WorldCompactMarkerButton.tscn"),
        "WorldCompactMarkerButton",
        "WorldTravelBookCardButton",
        "compact world marker is a visible map UI button");
    AssertSceneButtonUsesThemeVariation(
        Path.Combine(root, "scenes", "world", "ui", "WorldDeploymentMarkerButton.tscn"),
        "WorldDeploymentMarkerButton",
        "WorldTravelBookCardButton",
        "deployment marker is a visible map UI button");
    AssertSceneButtonUsesThemeVariation(
        Path.Combine(root, "scenes", "world", "ui", "WorldExpeditionCountRow.tscn"),
        "MinusButton",
        "WorldTravelBookCardButton",
        "expedition count minus control is a visible UI button");
    AssertSceneButtonUsesThemeVariation(
        Path.Combine(root, "scenes", "world", "ui", "WorldExpeditionCountRow.tscn"),
        "PlusButton",
        "WorldTravelBookCardButton",
        "expedition count plus control is a visible UI button");
    AssertSceneButtonUsesThemeVariation(
        Path.Combine(root, "scenes", "world", "ui", "WorldOpportunityDetailPanel.tscn"),
        "CompleteButton",
        "WorldPrimaryActionButton",
        "opportunity completion action is a visible UI button");
    AssertSceneButtonStaysFlatInputHotspot(
        Path.Combine(root, "scenes", "world", "ui", "WorldSiteHitButton.tscn"),
        "WorldSiteHitButton",
        "world site hit button is an invisible input hotspot, not a visible UI button");

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
    string topLeftStatusBlock = ExtractSceneNodeBlock(scene, "[node name=\"TopLeftStatus\"");
    string controlsBlock = ExtractSceneNodeBlock(scene, "[node name=\"TopRightControls\"");
    string bootstrapSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "StrategicWorldRoot.UiBootstrap.cs"));

    AssertTrue(
        !scene.Contains("[node name=\"TopResourceRow\"", StringComparison.Ordinal) &&
        !scene.Contains("[node name=\"TopResourceBar\"", StringComparison.Ordinal),
        "top UI must not be a single resource bar or row; each top element should own its overlay placement");
    AssertTrue(
        !scene.Contains("[node name=\"Title\" type=\"Label\" parent=\"TopBarHost/TopLeftStatus\"]", StringComparison.Ordinal) &&
        !bootstrapSource.Contains("TopBarHost/TopLeftStatus/Title", StringComparison.Ordinal),
        "top-left strategic HUD should not show the sandbox world title; it should only carry current-context status");
    AssertTrue(
        topLeftStatusBlock.Contains("type=\"PanelContainer\"", StringComparison.Ordinal) &&
        topLeftStatusBlock.Contains("theme_type_variation = &\"WorldTopStatusPanel\"", StringComparison.Ordinal) &&
        bootstrapSource.Contains("TopBarHost/TopLeftStatus/Margin/ResourceStrip/FoodSlot/FoodAmountTicker", StringComparison.Ordinal),
        "top-left resource status should be an independent window-backed overlay, not bare text on the world map");
    AssertTrue(
        controlsBlock.Contains("parent=\"TopBarHost\"", StringComparison.Ordinal) &&
        controlsBlock.Contains("layout_mode = 1", StringComparison.Ordinal) &&
        controlsBlock.Contains("anchor_left = 1.0", StringComparison.Ordinal) &&
        controlsBlock.Contains("anchor_right = 1.0", StringComparison.Ordinal),
        "top-bar controls should be independently anchored to the right edge of TopBarHost");
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
    string bodyScrollBlock = ExtractSceneNodeBlock(scene, "[node name=\"BodyScroll\"");
    string bodyContentBlock = ExtractSceneNodeBlock(scene, "[node name=\"BodyContent\"");
    string bootstrapSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "StrategicWorldRoot.UiBootstrap.cs"));
    string detailSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "StrategicWorldRoot.DetailHud.cs"));

    AssertTrue(
        scene.Contains("[node name=\"TopLeftStatus\" type=\"PanelContainer\" parent=\"TopBarHost\"]", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"ResourceStrip\" type=\"HBoxContainer\" parent=\"TopBarHost/TopLeftStatus/Margin\"]", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"FoodAmountTicker\" type=\"Control\" parent=\"TopBarHost/TopLeftStatus/Margin/ResourceStrip/FoodSlot\"]", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"NoticeLabel\" type=\"Label\" parent=\"TopBarHost\"]", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"WorldClockLabel\" type=\"Label\" parent=\"TopBarHost\"]", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"TopRightControls\" type=\"HBoxContainer\" parent=\"TopBarHost\"]", StringComparison.Ordinal),
        "strategic top HUD should be split into independently anchored overlay elements");

    AssertTrue(
        panelBlock.Contains("parent=\"OverlayHost\"", StringComparison.Ordinal) &&
        panelBlock.Contains("visible = false", StringComparison.Ordinal) &&
        panelBlock.Contains("anchor_left = 0.5", StringComparison.Ordinal) &&
        panelBlock.Contains("anchor_top = 1.0", StringComparison.Ordinal) &&
        panelBlock.Contains("anchor_right = 0.5", StringComparison.Ordinal) &&
        panelBlock.Contains("anchor_bottom = 1.0", StringComparison.Ordinal),
        "selected city detail should be a bottom-centered popup sized by responsive layout code");
    AssertTrue(
        bodyContentBlock.Contains("type=\"HBoxContainer\"", StringComparison.Ordinal) &&
        bodyContentBlock.Contains("parent=\"OverlayHost/SiteDetailPanel/Margin/SheetContent/BodyScroll\"", StringComparison.Ordinal),
        "selected city popup content should lay out horizontally, not as a left-side vertical menu panel");
    AssertTrue(
        bodyScrollBlock.Contains("horizontal_scroll_mode = 0", StringComparison.Ordinal) &&
        bodyScrollBlock.Contains("vertical_scroll_mode = 1", StringComparison.Ordinal),
        "selected city popup body must not horizontally scroll and should vertically scroll only when details overflow");
    AssertTrue(
        !scene.Contains("[node name=\"InfrastructureCard\"", StringComparison.Ordinal) &&
        !scene.Contains("[node name=\"DefenseCard\"", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"SummaryCard\"", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"ActionCard\"", StringComparison.Ordinal),
        "selected city popup should be a compact context/action sheet, not a four-column information board");
    AssertTrue(
        scene.Contains("res://resource/ui/themes/game-ui-skin/basic_ui_1_theme.tres", StringComparison.Ordinal) &&
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

internal static void StrategicWorldDetailSheetKeepsActionsVisibleWhenContentOverflows()
{
    string root = ProjectRoot();
    string scenePath = Path.Combine(root, "scenes", "world", "ui", "StrategicWorldHud.tscn");
    string scene = File.ReadAllText(scenePath);
    string panelBlock = ExtractSceneNodeBlock(scene, "[node name=\"SiteDetailPanel\"");
    string sheetContentBlock = ExtractSceneNodeBlock(scene, "[node name=\"SheetContent\"");
    string bodyScrollBlock = ExtractSceneNodeBlock(scene, "[node name=\"BodyScroll\"");
    string bodyContentBlock = ExtractSceneNodeBlock(scene, "[node name=\"BodyContent\"");
    string actionCardBlock = ExtractSceneNodeBlock(scene, "[node name=\"ActionCard\"");
    string actionScrollBlock = ExtractSceneNodeBlock(scene, "[node name=\"ActionScroll\"");
    string bootstrapSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "StrategicWorldRoot.UiBootstrap.cs"));
    string detailSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "StrategicWorldRoot.DetailHud.cs"));

    AssertTrue(
        panelBlock.Contains("anchor_left = 0.5", StringComparison.Ordinal) &&
        panelBlock.Contains("anchor_top = 1.0", StringComparison.Ordinal) &&
        panelBlock.Contains("anchor_right = 0.5", StringComparison.Ordinal) &&
        panelBlock.Contains("anchor_bottom = 1.0", StringComparison.Ordinal) &&
        panelBlock.Contains("custom_minimum_size = Vector2(720, 168)", StringComparison.Ordinal) &&
        panelBlock.Contains("offset_left = -480.0", StringComparison.Ordinal) &&
        panelBlock.Contains("offset_top = -420.0", StringComparison.Ordinal) &&
        panelBlock.Contains("offset_right = 480.0", StringComparison.Ordinal) &&
        panelBlock.Contains("offset_bottom = -28.0", StringComparison.Ordinal) &&
        !panelBlock.Contains("anchor_left = 0.25", StringComparison.Ordinal) &&
        !panelBlock.Contains("anchor_top = 0.75", StringComparison.Ordinal) &&
        !panelBlock.Contains("anchor_right = 0.75", StringComparison.Ordinal),
        "selected city detail sheet should define its bottom-center responsive layout values on the authored Godot node");
    AssertTrue(
        sheetContentBlock.Contains("type=\"HBoxContainer\"", StringComparison.Ordinal) &&
        sheetContentBlock.Contains("parent=\"OverlayHost/SiteDetailPanel/Margin\"", StringComparison.Ordinal),
        "selected city detail sheet should use a horizontal content row so the fixed action area stays beside the scrollable body");
    AssertTrue(
        bodyScrollBlock.Contains("parent=\"OverlayHost/SiteDetailPanel/Margin/SheetContent\"", StringComparison.Ordinal) &&
        bodyScrollBlock.Contains("horizontal_scroll_mode = 0", StringComparison.Ordinal) &&
        bodyScrollBlock.Contains("vertical_scroll_mode = 1", StringComparison.Ordinal),
        "selected city detail body should disable horizontal scroll and enable vertical auto-scroll when content exceeds the safe height");
    AssertTrue(
        bodyContentBlock.Contains("type=\"HBoxContainer\"", StringComparison.Ordinal) &&
        bodyContentBlock.Contains("parent=\"OverlayHost/SiteDetailPanel/Margin/SheetContent/BodyScroll\"", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"SummaryCard\" type=\"PanelContainer\" parent=\"OverlayHost/SiteDetailPanel/Margin/SheetContent/BodyScroll/BodyContent\"]", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"OpportunityCard\" type=\"PanelContainer\" parent=\"OverlayHost/SiteDetailPanel/Margin/SheetContent/BodyScroll/BodyContent\"]", StringComparison.Ordinal),
        "selected city detail summary and opportunity content should be the only scrollable sheet body content");
    AssertTrue(
        actionCardBlock.Contains("parent=\"OverlayHost/SiteDetailPanel/Margin/SheetContent\"", StringComparison.Ordinal) &&
        !actionCardBlock.Contains("parent=\"OverlayHost/SiteDetailPanel/Margin/SheetContent/BodyScroll", StringComparison.Ordinal),
        "selected city actions should stay outside the scrollable body so primary actions remain visible");
    AssertTrue(
        actionScrollBlock.Contains("parent=\"OverlayHost/SiteDetailPanel/Margin/SheetContent/ActionCard/ActionMargin/ActionStack\"", StringComparison.Ordinal) &&
        actionScrollBlock.Contains("horizontal_scroll_mode = 0", StringComparison.Ordinal) &&
        actionScrollBlock.Contains("vertical_scroll_mode = 1", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"ActionList\" type=\"VBoxContainer\" parent=\"OverlayHost/SiteDetailPanel/Margin/SheetContent/ActionCard/ActionMargin/ActionStack/ActionScroll\"]", StringComparison.Ordinal),
        "selected city action card should clip its variable action list internally instead of letting expedition rows leak below the sheet");
    AssertTrue(
        bootstrapSource.Contains("OverlayHost/SiteDetailPanel/Margin/SheetContent/BodyScroll/BodyContent/SummaryCard", StringComparison.Ordinal) &&
        bootstrapSource.Contains("OverlayHost/SiteDetailPanel/Margin/SheetContent/ActionCard", StringComparison.Ordinal) &&
        bootstrapSource.Contains("OverlayHost/SiteDetailPanel/Margin/SheetContent/ActionCard/ActionMargin/ActionStack/ActionScroll/ActionList", StringComparison.Ordinal),
        "strategic world code bindings should follow the split scroll-body and fixed-action sheet structure");
    AssertTrue(
        detailSource.Contains("ApplyWorldDetailPanelResponsiveLayout", StringComparison.Ordinal) &&
        detailSource.Contains("CaptureWorldDetailPanelAuthoredLayout", StringComparison.Ordinal) &&
        detailSource.Contains("_siteDetailPanelAuthoredMinimumSize", StringComparison.Ordinal) &&
        detailSource.Contains("_siteDetailPanelAuthoredOffsetBottom", StringComparison.Ordinal) &&
        detailSource.Contains("CustomMinimumSize", StringComparison.Ordinal) &&
        detailSource.Contains("OffsetLeft", StringComparison.Ordinal) &&
        detailSource.Contains("GetCombinedMinimumSize()", StringComparison.Ordinal) &&
        detailSource.Contains("preferredWidth", StringComparison.Ordinal) &&
        detailSource.Contains("preferredHeight", StringComparison.Ordinal) &&
        detailSource.Contains("NotificationResized", StringComparison.Ordinal) &&
        !detailSource.Contains("SiteDetailPanelHorizontalMargin", StringComparison.Ordinal) &&
        !detailSource.Contains("SiteDetailPanelTopSafeMargin", StringComparison.Ordinal) &&
        !detailSource.Contains("SiteDetailPanelBottomMargin", StringComparison.Ordinal) &&
        !detailSource.Contains("SiteDetailPanelMinWidth", StringComparison.Ordinal) &&
        !detailSource.Contains("SiteDetailPanelMaxWidth", StringComparison.Ordinal) &&
        !detailSource.Contains("SiteDetailPanelMinHeight", StringComparison.Ordinal) &&
        !detailSource.Contains("SiteDetailPanelMaxHeightRatio", StringComparison.Ordinal) &&
        !detailSource.Contains("_siteDetailPanelRestPosition", StringComparison.Ordinal),
        "selected city detail sheet should read authored node layout values instead of keeping UI tunables or cached positions in StrategicWorldRoot");
}

internal static void StrategicWorldWheelInputPartitionsHudAndMap()
{
    string root = ProjectRoot();
    string strategicRoot = ReadStrategicWorldRootSource();
    string uiBootstrapSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "StrategicWorldRoot.UiBootstrap.cs"));
    string strategicHudScene = File.ReadAllText(Path.Combine(root, "scenes", "world", "ui", "StrategicWorldHud.tscn"));
    string strategicHudRootBlock = ExtractSceneNodeBlock(strategicHudScene, "[node name=\"StrategicWorldHud\"");
    string mapInputGateBody = ExtractMethodBody(strategicRoot, "private bool IsRootScreenMapInput(InputEvent @event)");
    string nonMapUiBody = ExtractMethodBody(strategicRoot, "private bool IsPointerOverNonMapUi()");

    AssertTrue(
        strategicRoot.Contains("private Control _strategicHudRoot;", StringComparison.Ordinal) &&
        uiBootstrapSource.Contains("_strategicHudRoot = hud;", StringComparison.Ordinal),
        "strategic world should keep the authored HUD root as the UI input boundary instead of treating the full-screen map rect as the only pointer gate");
    AssertTrue(
        strategicHudRootBlock.Contains("mouse_force_pass_scroll_events = false", StringComparison.Ordinal) &&
        !uiBootstrapSource.Contains("MouseForcePassScrollEvents = false", StringComparison.Ordinal),
        "strategic world HUD root should author scroll-pass behavior on the Godot scene node instead of setting UI node configuration in root code");
    AssertTrue(
        mapInputGateBody.Contains("IsPointerOverNonMapUi()", StringComparison.Ordinal) &&
        mapInputGateBody.IndexOf("IsPointerOverNonMapUi()", StringComparison.Ordinal) <
        mapInputGateBody.IndexOf("ResolveMainWorldViewportRect().HasPoint(screenPosition)", StringComparison.Ordinal),
        "strategic world root should reject pointer input over non-map HUD controls before checking the full-screen map viewport rect");
    AssertTrue(
        mapInputGateBody.Contains("_isArmyBoxSelecting", StringComparison.Ordinal) &&
        mapInputGateBody.Contains("_worldCamera?.IsPointerNavigationActive == true", StringComparison.Ordinal) &&
        mapInputGateBody.IndexOf("_worldCamera?.IsPointerNavigationActive == true", StringComparison.Ordinal) <
        mapInputGateBody.IndexOf("IsPointerOverNonMapUi()", StringComparison.Ordinal),
        "map-origin selection and camera drag gestures should keep receiving release/motion events even if the pointer crosses UI");
    AssertTrue(
        strategicRoot.Contains("GuiGetHoveredControl()", StringComparison.Ordinal) &&
        nonMapUiBody.Contains("_strategicHudRoot", StringComparison.Ordinal) &&
        nonMapUiBody.Contains("_mainWorldViewportHost", StringComparison.Ordinal) &&
        nonMapUiBody.Contains("_worldMapOverlay", StringComparison.Ordinal) &&
        strategicRoot.Contains("IsAncestorOf", StringComparison.Ordinal),
        "strategic world input partition should use Godot's hovered Control tree to distinguish HUD controls from map viewport controls");
    AssertTrue(
        nonMapUiBody.Contains("hoveredControl == _strategicHudRoot", StringComparison.Ordinal) &&
        nonMapUiBody.Contains("return false;", StringComparison.Ordinal),
        "the full-screen HUD root itself is pass-through and must not block empty-map wheel zoom");
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

static string AssertManaSoulButtonStyleResource(string path, string sheetName, float regionX, string reason)
{
    AssertTrue(File.Exists(path), $"{reason} resource should exist path={path}");
    string source = File.ReadAllText(path);
    AssertTrue(
        source.Contains("[gd_resource type=\"StyleBoxTexture\"", StringComparison.Ordinal),
        $"{reason} should be a StyleBoxTexture path={path}");
    AssertTrue(
        source.Contains($"assets/textures/ui/tinyrpg_manasoulgui_v_1_0/{sheetName}", StringComparison.Ordinal) &&
        source.Contains($"region = Rect2({regionX:0}, 0, 96, 22)", StringComparison.Ordinal),
        $"{reason} should reference ManaSoul state sheet={sheetName} regionX={regionX} path={path}");
    AssertTrue(
        !source.Contains("assets/textures/ui/basic-ui/", StringComparison.Ordinal) &&
        !source.Contains("assets/textures/ui/travel-book-lite/", StringComparison.Ordinal) &&
        !source.Contains("normal_window.png", StringComparison.Ordinal) &&
        !source.Contains("button_empty_2", StringComparison.Ordinal),
        $"{reason} should not keep previous basic-ui or TravelBookLite button textures path={path}");
    AssertTrue(
        source.Contains("texture_margin_left", StringComparison.Ordinal) &&
        source.Contains("texture_margin_top", StringComparison.Ordinal) &&
        source.Contains("texture_margin_right", StringComparison.Ordinal) &&
        source.Contains("texture_margin_bottom", StringComparison.Ordinal),
        $"{reason} should define nine-patch margins path={path}");
    return source;
}

static void AssertThemeUsesUnifiedScrollbarSkin(string source, string relativePath)
{
    string[] requiredResources =
    {
        "res://resource/ui/themes/recruitment-ui-v1/recruitment_scroll_track.tres",
        "res://resource/ui/themes/recruitment-ui-v1/recruitment_scroll_grabber_normal.tres",
        "res://resource/ui/themes/recruitment-ui-v1/recruitment_scroll_grabber_hover.tres",
        "res://resource/ui/themes/recruitment-ui-v1/recruitment_scroll_grabber_pressed.tres",
        "res://resource/ui/themes/recruitment-ui-v1/recruitment_scroll_track_horizontal.tres",
        "res://resource/ui/themes/recruitment-ui-v1/recruitment_scroll_grabber_horizontal_normal.tres",
        "res://resource/ui/themes/recruitment-ui-v1/recruitment_scroll_grabber_horizontal_hover.tres",
        "res://resource/ui/themes/recruitment-ui-v1/recruitment_scroll_grabber_horizontal_pressed.tres"
    };

    foreach (string requiredResource in requiredResources)
    {
        AssertTrue(
            source.Contains(requiredResource, StringComparison.Ordinal),
            $"UI Theme should reuse the shared recruitment scrollbar StyleBoxes instead of owning a separate scrollbar skin resource={requiredResource} path={relativePath}");
    }

    string[] requiredAssignments =
    {
        "HScrollBar/styles/scroll = ExtResource(",
        "HScrollBar/styles/scroll_focus = ExtResource(",
        "HScrollBar/styles/grabber = ExtResource(",
        "HScrollBar/styles/grabber_highlight = ExtResource(",
        "HScrollBar/styles/grabber_pressed = ExtResource(",
        "VScrollBar/styles/scroll = ExtResource(",
        "VScrollBar/styles/scroll_focus = ExtResource(",
        "VScrollBar/styles/grabber = ExtResource(",
        "VScrollBar/styles/grabber_highlight = ExtResource(",
        "VScrollBar/styles/grabber_pressed = ExtResource("
    };

    foreach (string requiredAssignment in requiredAssignments)
    {
        AssertTrue(
            source.Contains(requiredAssignment, StringComparison.Ordinal),
            $"UI Theme should explicitly style every scrollbar state assignment={requiredAssignment} path={relativePath}");
    }
}

static void AssertSceneButtonUsesSharedCardSkin(string scenePath, string buttonName, string reason)
{
    AssertTrue(File.Exists(scenePath), $"button card scene should exist path={scenePath}");
    string scene = File.ReadAllText(scenePath);
    string nodeBlock = ExtractSceneNodeBlock(scene, $"[node name=\"{buttonName}\"");
    AssertTrue(
        nodeBlock.Contains($"[node name=\"{buttonName}\" type=\"Button\"", StringComparison.Ordinal),
        $"{reason}; card root should remain a Button node={buttonName} path={scenePath}");
    AssertTrue(
        scene.Contains("res://resource/ui/themes/game-ui-skin/basic_ui_1_theme.tres", StringComparison.Ordinal) &&
        nodeBlock.Contains("theme = ExtResource(\"1_theme\")", StringComparison.Ordinal) &&
        nodeBlock.Contains("theme_type_variation = &\"WorldTravelBookCardButton\"", StringComparison.Ordinal),
        $"{reason}; card should use the shared world UI Theme variation node={buttonName} path={scenePath}");
    AssertTrue(
        !scene.Contains("res://resource/ui/themes/game-ui-skin/build_inventory_preview_theme.tres", StringComparison.Ordinal) &&
        !nodeBlock.Contains("theme_type_variation = &\"WorldBuildingOptionCard\"", StringComparison.Ordinal),
        $"{reason}; card should not reuse the excluded building picker skin node={buttonName} path={scenePath}");
    AssertTrue(
        !nodeBlock.Contains("theme_override_styles/normal", StringComparison.Ordinal) &&
        !nodeBlock.Contains("theme_override_styles/hover", StringComparison.Ordinal) &&
        !nodeBlock.Contains("theme_override_styles/pressed", StringComparison.Ordinal) &&
        !nodeBlock.Contains("theme_override_styles/disabled", StringComparison.Ordinal),
        $"{reason}; card should stay theme-driven instead of carrying local style overrides node={buttonName} path={scenePath}");
}

static void AssertSceneButtonUsesRecruitmentStyle(string scenePath, string buttonName, string variationName, string reason)
{
    AssertTrue(File.Exists(scenePath), $"recruitment button scene should exist path={scenePath}");
    string scene = File.ReadAllText(scenePath);
    string nodeBlock = ExtractSceneNodeBlock(scene, $"[node name=\"{buttonName}\"");
    AssertTrue(
        nodeBlock.Contains($"[node name=\"{buttonName}\" type=\"Button\"", StringComparison.Ordinal),
        $"{reason}; node should remain a Button node={buttonName} path={scenePath}");
    AssertTrue(
        scene.Contains("res://resource/ui/themes/recruitment-ui-v1/recruitment_ui_v1_theme.tres", StringComparison.Ordinal) &&
        nodeBlock.Contains($"theme_type_variation = &\"{variationName}\"", StringComparison.Ordinal),
        $"{reason}; node should use recruitment Theme variation={variationName} node={buttonName} path={scenePath}");
    AssertTrue(
        !nodeBlock.Contains("theme_override_styles/normal", StringComparison.Ordinal) &&
        !nodeBlock.Contains("theme_override_styles/hover", StringComparison.Ordinal) &&
        !nodeBlock.Contains("theme_override_styles/pressed", StringComparison.Ordinal) &&
        !nodeBlock.Contains("theme_override_styles/disabled", StringComparison.Ordinal),
        $"{reason}; node should inherit recruitment state styles from the Theme node={buttonName} path={scenePath}");
}

static void AssertSceneButtonUsesThemeVariation(string scenePath, string buttonName, string variationName, string reason)
{
    AssertTrue(File.Exists(scenePath), $"button scene should exist path={scenePath}");
    string scene = File.ReadAllText(scenePath);
    string nodeBlock = ExtractSceneNodeBlock(scene, $"[node name=\"{buttonName}\"");
    AssertTrue(
        nodeBlock.Contains($"[node name=\"{buttonName}\" type=\"Button\"", StringComparison.Ordinal),
        $"{reason}; node should remain a Button node={buttonName} path={scenePath}");
    AssertTrue(
        scene.Contains("res://resource/ui/themes/game-ui-skin/basic_ui_1_theme.tres", StringComparison.Ordinal) &&
        nodeBlock.Contains($"theme_type_variation = &\"{variationName}\"", StringComparison.Ordinal),
        $"{reason}; visible button should use shared ManaSoul-backed Theme variation={variationName} node={buttonName} path={scenePath}");
    AssertTrue(
        !scene.Contains("res://resource/ui/themes/game-ui-skin/build_inventory_preview_theme.tres", StringComparison.Ordinal),
        $"{reason}; visible button should not reuse the excluded building picker skin node={buttonName} path={scenePath}");
    AssertTrue(
        !nodeBlock.Contains("theme_override_styles/normal", StringComparison.Ordinal) &&
        !nodeBlock.Contains("theme_override_styles/hover", StringComparison.Ordinal) &&
        !nodeBlock.Contains("theme_override_styles/pressed", StringComparison.Ordinal) &&
        !nodeBlock.Contains("theme_override_styles/disabled", StringComparison.Ordinal),
        $"{reason}; visible button should inherit state styles from the shared Theme node={buttonName} path={scenePath}");
}

static void AssertSceneButtonStaysFlatInputHotspot(string scenePath, string buttonName, string reason)
{
    AssertTrue(File.Exists(scenePath), $"button scene should exist path={scenePath}");
    string scene = File.ReadAllText(scenePath);
    string nodeBlock = ExtractSceneNodeBlock(scene, $"[node name=\"{buttonName}\"");
    AssertTrue(
        nodeBlock.Contains($"[node name=\"{buttonName}\" type=\"Button\"", StringComparison.Ordinal) &&
        nodeBlock.Contains("flat = true", StringComparison.Ordinal) &&
        nodeBlock.Contains("text = \"\"", StringComparison.Ordinal),
        $"{reason}; hotspot should remain a flat empty Button node={buttonName} path={scenePath}");
    AssertTrue(
        !scene.Contains("basic_ui_1_theme.tres", StringComparison.Ordinal) &&
        !scene.Contains("build_inventory_preview_theme.tres", StringComparison.Ordinal) &&
        !nodeBlock.Contains("theme_type_variation", StringComparison.Ordinal),
        $"{reason}; hotspot should not draw a themed visual skin node={buttonName} path={scenePath}");
}

static void AssertSceneButtonUsesCompactStyle(string scenePath, string buttonName, string reason, float horizontalPadding, float verticalPadding, string variationName)
{
    AssertTrue(File.Exists(scenePath), $"button scene should exist path={scenePath}");
    string scene = File.ReadAllText(scenePath);
    string nodeBlock = ExtractSceneNodeBlock(scene, $"[node name=\"{buttonName}\"");
    AssertTrue(
        nodeBlock.Contains($"[node name=\"{buttonName}\" type=\"Button\"", StringComparison.Ordinal),
        $"{reason}; compact control should remain a Button node={buttonName} path={scenePath}");
    AssertTrue(
        scene.Contains("res://resource/ui/themes/game-ui-skin/basic_ui_1_theme.tres", StringComparison.Ordinal) &&
        nodeBlock.Contains($"theme_type_variation = &\"{variationName}\"", StringComparison.Ordinal),
        $"{reason}; compact buttons should use the shared Theme variation={variationName} instead of local state StyleBoxes node={buttonName} path={scenePath}");
    AssertTrue(
        !nodeBlock.Contains("theme_override_styles/normal", StringComparison.Ordinal) &&
        !nodeBlock.Contains("theme_override_styles/hover", StringComparison.Ordinal) &&
        !nodeBlock.Contains("theme_override_styles/pressed", StringComparison.Ordinal) &&
        !nodeBlock.Contains("theme_override_styles/disabled", StringComparison.Ordinal) &&
        !nodeBlock.Contains("button_compact", StringComparison.Ordinal),
        $"{reason}; compact buttons should inherit state styles from the shared Theme node={buttonName} path={scenePath}");

    (float width, float height) minimumSize = ReadSceneNodeMinimumSize(nodeBlock, buttonName, scenePath);
    string text = ReadSceneNodeText(nodeBlock);
    AssertTrue(
        !text.Contains('\n'),
        $"{reason}; compact text buttons should keep labels on one line node={buttonName} path={scenePath}");
    float requiredWidth = (horizontalPadding * 2f) + EstimateButtonTextWidth(text);
    float requiredHeight = (verticalPadding * 2f) + EstimateButtonTextHeight(text);
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

static (int width, int height) ReadPngDimensions(string path)
{
    using FileStream stream = File.OpenRead(path);
    Span<byte> header = stackalloc byte[24];
    int bytesRead = stream.Read(header);
    AssertTrue(bytesRead == header.Length, $"PNG should contain a full header path={path}");
    AssertTrue(
        header[0] == 0x89 &&
        header[1] == 0x50 &&
        header[2] == 0x4E &&
        header[3] == 0x47,
        $"file should be a PNG path={path}");

    int width =
        (header[16] << 24) |
        (header[17] << 16) |
        (header[18] << 8) |
        header[19];
    int height =
        (header[20] << 24) |
        (header[21] << 16) |
        (header[22] << 8) |
        header[23];
    return (width, height);
}

static (int minX, int maxX) ReadPngAlphaXBounds(string path)
{
    (int width, int height, int bytesPerPixel, byte[] pixels) = ReadPngPixels(path);
    int stride = width * bytesPerPixel;
    int minX = width;
    int maxX = -1;
    for (int y = 0; y < height; y++)
    {
        for (int x = 0; x < width; x++)
        {
            int index = (y * stride) + (x * bytesPerPixel);
            byte alpha = bytesPerPixel == 4 ? pixels[index + 3] : byte.MaxValue;
            if (alpha == 0)
            {
                continue;
            }

            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
        }
    }

    AssertTrue(maxX >= minX, $"PNG should contain visible pixels path={path}");
    return (minX, maxX);
}

static (int minY, int maxY) ReadPngAlphaYBounds(string path)
{
    (int width, int height, int bytesPerPixel, byte[] pixels) = ReadPngPixels(path);
    int stride = width * bytesPerPixel;
    int minY = height;
    int maxY = -1;
    for (int y = 0; y < height; y++)
    {
        for (int x = 0; x < width; x++)
        {
            int index = (y * stride) + (x * bytesPerPixel);
            byte alpha = bytesPerPixel == 4 ? pixels[index + 3] : byte.MaxValue;
            if (alpha == 0)
            {
                continue;
            }

            minY = Math.Min(minY, y);
            maxY = Math.Max(maxY, y);
        }
    }

    AssertTrue(maxY >= minY, $"PNG should contain visible pixels path={path}");
    return (minY, maxY);
}

static bool PngRegionContainsColor(
    string path,
    int minX,
    int maxX,
    int minY,
    int maxY,
    Func<byte, byte, byte, byte, bool> predicate)
{
    (int width, int height, int bytesPerPixel, byte[] pixels) = ReadPngPixels(path);
    int startX = Math.Clamp(minX, 0, width - 1);
    int endX = Math.Clamp(maxX, 0, width - 1);
    int startY = Math.Clamp(minY, 0, height - 1);
    int endY = Math.Clamp(maxY, 0, height - 1);
    if (startX > endX || startY > endY)
    {
        return false;
    }

    int stride = width * bytesPerPixel;
    for (int y = startY; y <= endY; y++)
    {
        for (int x = startX; x <= endX; x++)
        {
            int index = (y * stride) + (x * bytesPerPixel);
            byte red = pixels[index];
            byte green = pixels[index + 1];
            byte blue = pixels[index + 2];
            byte alpha = bytesPerPixel == 4 ? pixels[index + 3] : byte.MaxValue;
            if (predicate(red, green, blue, alpha))
            {
                return true;
            }
        }
    }

    return false;
}

static bool IsBrightScrollbarEdgeAccent(byte red, byte green, byte blue, byte alpha)
{
    return alpha > 0 && red >= 240 && green >= 190 && blue <= 150;
}

static bool IsOrangeScrollbarEdgeAccent(byte red, byte green, byte blue, byte alpha)
{
    return alpha > 0 && red >= 180 && green >= 80 && green <= 160 && blue <= 90;
}

static (int width, int height, int bytesPerPixel, byte[] pixels) ReadPngPixels(string path)
{
    byte[] png = File.ReadAllBytes(path);
    AssertTrue(png.Length >= 24, $"PNG should contain a full header path={path}");
    AssertTrue(
        png[0] == 0x89 &&
        png[1] == 0x50 &&
        png[2] == 0x4E &&
        png[3] == 0x47,
        $"file should be a PNG path={path}");

    int width = ReadBigEndianInt32(png, 16);
    int height = ReadBigEndianInt32(png, 20);
    int bitDepth = png[24];
    int colorType = png[25];
    AssertTrue(bitDepth == 8, $"scrollbar PNG alpha scan expects 8-bit channels path={path}");

    int bytesPerPixel = colorType switch
    {
        6 => 4,
        2 => 3,
        _ => throw new InvalidOperationException($"unsupported PNG color type for alpha scan path={path} colorType={colorType}")
    };

    using var idatStream = new MemoryStream();
    int offset = 8;
    while (offset + 12 <= png.Length)
    {
        int chunkLength = ReadBigEndianInt32(png, offset);
        string chunkType = System.Text.Encoding.ASCII.GetString(png, offset + 4, 4);
        int chunkDataOffset = offset + 8;
        if (chunkType == "IDAT")
        {
            idatStream.Write(png, chunkDataOffset, chunkLength);
        }
        else if (chunkType == "IEND")
        {
            break;
        }

        offset = chunkDataOffset + chunkLength + 4;
    }

    using var compressed = new MemoryStream(idatStream.ToArray());
    using var zlib = new System.IO.Compression.ZLibStream(compressed, System.IO.Compression.CompressionMode.Decompress);
    using var rawStream = new MemoryStream();
    zlib.CopyTo(rawStream);
    byte[] raw = rawStream.ToArray();

    int stride = width * bytesPerPixel;
    byte[] previous = new byte[stride];
    byte[] current = new byte[stride];
    byte[] pixels = new byte[stride * height];
    int rawOffset = 0;
    for (int y = 0; y < height; y++)
    {
        int filterType = raw[rawOffset++];
        Array.Copy(raw, rawOffset, current, 0, stride);
        rawOffset += stride;
        UnfilterPngScanline(current, previous, bytesPerPixel, filterType);
        Array.Copy(current, 0, pixels, y * stride, stride);

        (previous, current) = (current, previous);
    }

    return (width, height, bytesPerPixel, pixels);
}

static int ReadBigEndianInt32(byte[] bytes, int offset)
{
    return
        (bytes[offset] << 24) |
        (bytes[offset + 1] << 16) |
        (bytes[offset + 2] << 8) |
        bytes[offset + 3];
}

static void UnfilterPngScanline(byte[] current, byte[] previous, int bytesPerPixel, int filterType)
{
    for (int i = 0; i < current.Length; i++)
    {
        int left = i >= bytesPerPixel ? current[i - bytesPerPixel] : 0;
        int up = previous[i];
        int upLeft = i >= bytesPerPixel ? previous[i - bytesPerPixel] : 0;
        int predictor = filterType switch
        {
            0 => 0,
            1 => left,
            2 => up,
            3 => (left + up) / 2,
            4 => PngPaethPredictor(left, up, upLeft),
            _ => throw new InvalidOperationException($"unsupported PNG filter type={filterType}")
        };

        current[i] = unchecked((byte)(current[i] + predictor));
    }
}

static int PngPaethPredictor(int left, int up, int upLeft)
{
    int p = left + up - upLeft;
    int pa = Math.Abs(p - left);
    int pb = Math.Abs(p - up);
    int pc = Math.Abs(p - upLeft);
    if (pa <= pb && pa <= pc)
    {
        return left;
    }

    return pb <= pc ? up : upLeft;
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
