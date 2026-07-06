internal static partial class WorldSiteDeploymentCacheRegressionCases
{
internal static void WorldBuildingOptionCardUsesReversibleInventoryPreviewSkin()
{
    string root = ProjectRoot();
    string cardScenePath = Path.Combine(root, "scenes", "world", "ui", "WorldBuildingOptionCard.tscn");
    string hudScenePath = Path.Combine(root, "scenes", "world", "ui", "WorldSitePeacetimeHud.tscn");
    string themePath = Path.Combine(root, "resource", "ui", "themes", "game-ui-skin", "build_inventory_preview_theme.tres");
    string basicThemePath = Path.Combine(root, "resource", "ui", "themes", "game-ui-skin", "basic_ui_1_theme.tres");
    string textureAtlasPath = Path.Combine(root, "assets", "textures", "ui", "build-inventory-preview", "bestiary_details.png");
    string textureAtlasImportPath = textureAtlasPath + ".import";
    string[] styleResources =
    {
        "build_inventory_preview_card_normal.tres",
        "build_inventory_preview_card_hover.tres",
        "build_inventory_preview_card_pressed.tres",
        "build_inventory_preview_card_disabled.tres"
    };

    AssertTrue(File.Exists(cardScenePath), $"building card scene should exist path={cardScenePath}");
    AssertTrue(File.Exists(hudScenePath), $"site peacetime hud scene should exist path={hudScenePath}");
    AssertTrue(File.Exists(themePath), $"reversible building inventory preview theme should exist path={themePath}");
    AssertTrue(File.Exists(basicThemePath), $"existing basic UI theme should remain path={basicThemePath}");
    AssertTrue(File.Exists(textureAtlasPath), $"building inventory preview should copy the bestiary Details atlas into the project path={textureAtlasPath}");
    AssertTrue(File.Exists(textureAtlasImportPath), $"building inventory preview atlas should keep Godot import settings path={textureAtlasImportPath}");

    string cardScene = File.ReadAllText(cardScenePath);
    string rootBlock = ExtractSceneNodeBlock(cardScene, "[node name=\"WorldBuildingOptionCard\"");
    AssertTrue(
        rootBlock.Contains("[node name=\"WorldBuildingOptionCard\" type=\"Button\"", StringComparison.Ordinal) &&
        rootBlock.Contains("theme = ExtResource(\"1_theme\")", StringComparison.Ordinal) &&
        rootBlock.Contains("theme_type_variation = &\"WorldBuildingOptionCard\"", StringComparison.Ordinal) &&
        cardScene.Contains("res://resource/ui/themes/game-ui-skin/build_inventory_preview_theme.tres", StringComparison.Ordinal),
        "building option card should swap only its theme resource and named variation");
    AssertTrue(
        rootBlock.Contains("custom_minimum_size = Vector2(116, 112)", StringComparison.Ordinal) &&
        rootBlock.Contains("size_flags_horizontal = 1", StringComparison.Ordinal),
        "building option card should read as one inventory slot instead of a wide expanding action card");
    AssertTrue(
        cardScene.Contains("[node name=\"Content\" type=\"VBoxContainer\" parent=\".\"]", StringComparison.Ordinal) &&
        cardScene.Contains("[node name=\"Icon\" type=\"TextureRect\" parent=\"Content\"]", StringComparison.Ordinal) &&
        cardScene.Contains("[node name=\"NameLabel\" type=\"Label\" parent=\"Content\"]", StringComparison.Ordinal),
        "building option card preview skin must preserve the authored icon-card node structure");
    AssertTrue(
        cardScene.Contains("offset_left = 8.0", StringComparison.Ordinal) &&
        cardScene.Contains("offset_top = 6.0", StringComparison.Ordinal) &&
        cardScene.Contains("offset_right = -8.0", StringComparison.Ordinal) &&
        cardScene.Contains("offset_bottom = -6.0", StringComparison.Ordinal) &&
        cardScene.Contains("custom_minimum_size = Vector2(96, 70)", StringComparison.Ordinal) &&
        cardScene.Contains("custom_minimum_size = Vector2(96, 18)", StringComparison.Ordinal) &&
        cardScene.Contains("theme_override_colors/font_color = Color(0.96, 0.78, 0.62, 1)", StringComparison.Ordinal),
        "building option card content should be proportioned and keep readable text on the dark ManaSoul panel");
    AssertTrue(
        !rootBlock.Contains("theme_override_styles/normal", StringComparison.Ordinal) &&
        !rootBlock.Contains("theme_override_styles/hover", StringComparison.Ordinal) &&
        !rootBlock.Contains("theme_override_styles/pressed", StringComparison.Ordinal) &&
        !rootBlock.Contains("theme_override_styles/disabled", StringComparison.Ordinal),
        "building option card should stay theme-driven instead of carrying local style overrides");

    string themeSource = File.ReadAllText(themePath);
    AssertTrue(
        themeSource.Contains("[gd_resource type=\"Theme\"", StringComparison.Ordinal) &&
        themeSource.Contains("WorldBuildingOptionCard/base_type = &\"Button\"", StringComparison.Ordinal) &&
        themeSource.Contains("WorldBuildingOptionCard/styles/normal = ExtResource(\"2_hover\")", StringComparison.Ordinal) &&
        themeSource.Contains("WorldBuildingOptionCard/styles/hover = ExtResource(\"1_normal\")", StringComparison.Ordinal) &&
        themeSource.Contains("WorldBuildingOptionCard/styles/pressed = ExtResource(", StringComparison.Ordinal) &&
        themeSource.Contains("WorldBuildingOptionCard/styles/disabled = ExtResource(", StringComparison.Ordinal),
        "building inventory preview theme should define one reversible Button type variation and intentionally swap normal/hover visuals");

    foreach (string fileName in styleResources)
    {
        string path = Path.Combine(root, "resource", "ui", "themes", "game-ui-skin", fileName);
        AssertTrue(File.Exists(path), $"building inventory preview StyleBoxTexture should exist path={path}");
        string source = File.ReadAllText(path);
        AssertTrue(
            source.Contains("[gd_resource type=\"StyleBoxTexture\"", StringComparison.Ordinal) &&
            source.Contains("assets/textures/ui/build-inventory-preview/bestiary_details.png", StringComparison.Ordinal) &&
            source.Contains("region_rect = Rect2(258, 33, 31, 31)", StringComparison.Ordinal) &&
            source.Contains("modulate_color", StringComparison.Ordinal) &&
            source.Contains("texture_margin_left", StringComparison.Ordinal) &&
            source.Contains("texture_margin_top", StringComparison.Ordinal) &&
            source.Contains("texture_margin_right", StringComparison.Ordinal) &&
            source.Contains("texture_margin_bottom", StringComparison.Ordinal),
            $"building inventory preview style should use the bestiary Details inventory slot atlas region path={path}");
        AssertTrue(
            !source.Contains("build_inventory_card_", StringComparison.Ordinal),
            $"building inventory preview style should not use the rejected menu-card crop path={path}");
    }

    string hudScene = File.ReadAllText(hudScenePath);
    string buildListBlock = ExtractSceneNodeBlock(hudScene, "[node name=\"SiteBuildingOptionGrid\"");
    AssertTrue(
        buildListBlock.Contains("[node name=\"SiteBuildingOptionGrid\" type=\"GridContainer\"", StringComparison.Ordinal) &&
        buildListBlock.Contains("columns = 4", StringComparison.Ordinal),
        "building picker should present building options as a compact inventory-style grid");

    string basicTheme = File.ReadAllText(basicThemePath);
    AssertTrue(
        !basicTheme.Contains("build_inventory_preview", StringComparison.Ordinal) &&
        !basicTheme.Contains("WorldBuildingOptionCard", StringComparison.Ordinal) &&
        basicTheme.Contains("[sub_resource type=\"StyleBoxEmpty\" id=\"StyleBoxEmpty_popup_panel\"]", StringComparison.Ordinal) &&
        basicTheme.Contains("PopupPanel/styles/panel = SubResource(\"StyleBoxEmpty_popup_panel\")", StringComparison.Ordinal) &&
        basicTheme.Contains("TooltipPanel/styles/panel = SubResource(\"StyleBoxEmpty_popup_panel\")", StringComparison.Ordinal),
        "preview skin should be removable by changing the card scene back to the existing basic UI theme while shared custom tooltip shells stay transparent");
}
}
