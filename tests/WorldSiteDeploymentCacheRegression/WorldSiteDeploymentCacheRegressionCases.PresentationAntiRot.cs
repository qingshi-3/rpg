internal static partial class WorldSiteDeploymentCacheRegressionCases
{
internal static void StrategicWorldDefaultDefinitionUsesSandboxNaming()
{
    string root = ProjectRoot();
    string idsPath = Path.Combine(root, "src", "Application", "World", "StrategicWorldIds.cs");
    string factoryPath = Path.Combine(root, "src", "Application", "World", "StrategicWorldV1DefinitionFactory.cs");
    string idsSource = File.ReadAllText(idsPath);
    string factorySource = File.ReadAllText(factoryPath);

    var definition = Rpg.Application.World.StrategicWorldV1DefinitionFactory.Create(loadInitialStateConfig: false);

    AssertEqual("default_sandbox_strategic_v1", definition.Id, "default strategic world definition should use sandbox id naming");
    AssertEqual("沙盒大世界", definition.DisplayName, "default strategic world title should not present a chapter or demo target name");
    AssertTrue(
        idsSource.Contains("DefinitionDefaultSandbox", StringComparison.Ordinal) &&
        !idsSource.Contains("DefinitionChapter01", StringComparison.Ordinal) &&
        !idsSource.Contains("chapter_01", StringComparison.Ordinal),
        "strategic world definition id should be default sandbox naming, not chapter naming");

    foreach (string forbidden in new[] { "第一章", "章节", "chapter_01", "埋骨地攻防", "埋骨地" })
    {
        AssertTrue(
            !factorySource.Contains(forbidden, StringComparison.Ordinal),
            $"active strategic world definition should not expose chapter or discarded demo term={forbidden}");
    }
}

internal static void BattleGridHighlightOverlayDelegatesVectorRendering()
{
    string root = ProjectRoot();
    string overlayPath = Path.Combine(root, "src", "Presentation", "Battle", "BattleGridHighlightOverlay.cs");
    string rendererPath = Path.Combine(root, "src", "Presentation", "Battle", "BattleGridVectorHighlightRenderer.cs");

    AssertTrue(File.Exists(overlayPath), $"battle grid highlight overlay source should exist path={overlayPath}");
    AssertTrue(File.Exists(rendererPath), $"vector highlight renderer should exist path={rendererPath}");

    string overlaySource = File.ReadAllText(overlayPath);
    string rendererSource = File.ReadAllText(rendererPath);
    int overlayLines = File.ReadAllLines(overlayPath).Length;

    AssertTrue(
        overlayLines < 350,
        $"BattleGridHighlightOverlay should stay below 350 lines after renderer decomposition actual={overlayLines}");
    AssertTrue(
        overlaySource.Contains("BattleGridVectorHighlightRenderer", StringComparison.Ordinal),
        "BattleGridHighlightOverlay should delegate dynamic vector drawing to BattleGridVectorHighlightRenderer");
    AssertTrue(
        !overlaySource.Contains("new Polygon2D", StringComparison.Ordinal) &&
        !overlaySource.Contains("new Line2D", StringComparison.Ordinal),
        "BattleGridHighlightOverlay should not construct vector drawing nodes inline");

    foreach (string required in new[]
    {
        "internal sealed class BattleGridVectorHighlightRenderer",
        "new Polygon2D",
        "new Line2D",
        "AddSkillRangeDeploymentStyle",
        "AddPathArrows",
        "AddTargetLockRing",
        "AddHoverFrame"
    })
    {
        AssertTrue(rendererSource.Contains(required, StringComparison.Ordinal), $"vector highlight renderer should own renderer fragment={required}");
    }
}

internal static void BattleSiteMapLoadedSubscribersDisconnectOnExitTree()
{
    string root = ProjectRoot();
    string battlePresentationDir = Path.Combine(root, "src", "Presentation", "Battle");
    AssertTrue(Directory.Exists(battlePresentationDir), $"battle Presentation source directory should exist path={battlePresentationDir}");

    string[] subscriberFiles = Directory.GetFiles(battlePresentationDir, "*.cs", SearchOption.AllDirectories)
        .Where(file => File.ReadAllText(file).Contains(".SiteMapLoaded += OnSiteMapLoaded", StringComparison.Ordinal))
        .OrderBy(path => path)
        .ToArray();
    AssertTrue(
        subscriberFiles.Length >= 3,
        "battle Presentation should include the known SiteMapLoaded subscribers in the lifecycle guard");

    foreach (string file in subscriberFiles)
    {
        string source = File.ReadAllText(file);
        AssertTrue(
            source.Contains("public override void _ExitTree()", StringComparison.Ordinal) &&
            source.Contains(".SiteMapLoaded -= OnSiteMapLoaded", StringComparison.Ordinal),
            $"C# signal subscribers must disconnect from WorldSiteRoot.SiteMapLoaded in _ExitTree file={file}");
    }

    AssertTrue(
        subscriberFiles.Any(file => file.EndsWith("BattleDeploymentZoneOverlay.cs", StringComparison.Ordinal)) &&
        subscriberFiles.Any(file => file.EndsWith("BattleGridHighlightOverlay.cs", StringComparison.Ordinal)) &&
        subscriberFiles.Any(file => file.EndsWith("BattleDebugController.cs", StringComparison.Ordinal)),
        "battle SiteMapLoaded lifecycle guard should cover deployment zone overlay, grid highlight overlay, and debug controller");
}

internal static void DebugTogglesUseInputMapActions()
{
    string root = ProjectRoot();
    string projectConfig = File.ReadAllText(Path.Combine(root, "project.godot"));
    foreach (string action in new[]
    {
        "performance_debug_toggle",
        "battle_debug_toggle",
        "battle_guide_grid_toggle"
    })
    {
        AssertTrue(
            projectConfig.Contains(action + "={", StringComparison.Ordinal),
            $"debug toggle should be declared in the Project Input Map action={action}");
    }

    AssertTrue(
        projectConfig.Contains("\"physical_keycode\":4194338", StringComparison.Ordinal) &&
        projectConfig.Contains("\"physical_keycode\":4194339", StringComparison.Ordinal),
        "debug toggle Input Map actions should keep the default F3/F4 keyboard bindings");

    string[] debugInputFiles =
    {
        Path.Combine(root, "src", "Presentation", "Debug", "PerformanceDebugOverlay.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Debug", "BattleDebugController.cs"),
        Path.Combine(root, "src", "Presentation", "Battle", "Debug", "BattleGuideGridDebug.cs")
    };

    foreach (string file in debugInputFiles)
    {
        AssertTrue(File.Exists(file), $"debug input source should exist path={file}");
        string source = File.ReadAllText(file);
        AssertTrue(
            source.Contains(".IsActionPressed(", StringComparison.Ordinal),
            $"debug toggles should read Input Map actions instead of raw keycodes file={file}");

        foreach (string forbidden in new[] { "ToggleKey", "Key.F3", "Key.F4", "InputEventKey" })
        {
            AssertTrue(
                !source.Contains(forbidden, StringComparison.Ordinal),
                $"debug toggles should not use hardcoded keyboard checks fragment={forbidden} file={file}");
        }
    }
}

internal static void PresentationDoesNotConstructBattleRuntimeSession()
{
    string root = ProjectRoot();
    string presentationDir = Path.Combine(root, "src", "Presentation");
    AssertTrue(Directory.Exists(presentationDir), $"presentation source directory should exist path={presentationDir}");

    List<string> offendingFiles = Directory.GetFiles(presentationDir, "*.cs", SearchOption.AllDirectories)
        .Where(file => File.ReadAllText(file).Contains("new BattleRuntimeSession(", StringComparison.Ordinal))
        .Select(file => Path.GetRelativePath(root, file))
        .OrderBy(path => path)
        .ToList();

    AssertTrue(
        offendingFiles.Count == 0,
        $"Presentation must consume the runtime controller from the Application boundary, not construct BattleRuntimeSession itself files={string.Join(", ", offendingFiles)}");
}

internal static void StrategicWorldClockPresentationUsesWorldMapTimeTerminology()
{
    string root = ProjectRoot();
    string worldDir = Path.Combine(root, "src", "Presentation", "World");
    string[] files =
    {
        Path.Combine(worldDir, "StrategicWorldRoot.WorldClock.cs"),
        Path.Combine(worldDir, "StrategicWorldRoot.DetailHud.cs"),
        Path.Combine(worldDir, "StrategicWorldRoot.UiBootstrap.cs"),
        Path.Combine(worldDir, "StrategicWorldRoot.BattleEntry.cs")
    };

    foreach (string file in files)
    {
        AssertTrue(File.Exists(file), $"strategic world Presentation source file should exist path={file}");
    }

    string source = string.Join("\n", files.Select(File.ReadAllText));
    List<string> forbiddenHits = new();
    foreach (string forbidden in new[]
    {
        "世界步",
        "世界推进",
        "推进到",
        "推进已暂停",
        "继续推进",
        "暂停世界推进",
        "继续世界推进",
        "世界时钟"
    })
    {
        if (source.Contains(forbidden, StringComparison.Ordinal))
        {
            forbiddenHits.Add(forbidden);
        }
    }

    AssertTrue(
        forbiddenHits.Count == 0,
        $"strategic world player-facing clock text should use realtime world-map terminology, not step/advance wording hits={string.Join(", ", forbiddenHits)}");
    AssertTrue(
        source.Contains("大地图时间", StringComparison.Ordinal) &&
        source.Contains("大地图结算", StringComparison.Ordinal),
        "strategic world player-facing clock text should expose 大地图时间 and 大地图结算 terminology");
}

internal static void StrategicWorldClockSettlesStrategicManagementElapsedTimeThroughRuntime()
{
    string root = ProjectRoot();
    string worldClockPath = Path.Combine(root, "src", "Presentation", "World", "StrategicWorldRoot.WorldClock.cs");
    AssertTrue(File.Exists(worldClockPath), $"strategic world clock source should exist path={worldClockPath}");

    string source = File.ReadAllText(worldClockPath);
    string tickBody = ExtractMethodBody(source, "private void AdvanceWorldClockTick()");

    AssertTrue(
        source.Contains("using Rpg.Application.StrategicManagement;", StringComparison.Ordinal),
        "strategic world clock should import only the Strategic Management Application boundary it needs");
    AssertTrue(
        tickBody.Contains("StrategicManagementRuntime.SettleElapsedWorldTime(1)", StringComparison.Ordinal),
        "each large-map clock settlement should route exactly one elapsed pulse through StrategicManagementRuntime");
    AssertTrue(
        tickBody.IndexOf("_worldTickService.AdvanceWorldTick(State, Definition)", StringComparison.Ordinal) <
        tickBody.IndexOf("StrategicManagementRuntime.SettleElapsedWorldTime(1)", StringComparison.Ordinal),
        "legacy world tick should remain the local driver, then bridge to Strategic Management elapsed-time settlement");
    AssertTrue(
        !tickBody.Contains("SaveCurrentState", StringComparison.Ordinal) &&
        source.Contains("launch-session memory", StringComparison.Ordinal) &&
        source.Contains("save coordinator", StringComparison.Ordinal),
        "large-map elapsed-time settlement should mutate runtime Strategic Management memory only; save policy belongs to an explicit save coordinator/autosave boundary, not the tick loop");

    foreach (string forbidden in new[]
    {
        "new StrategicManagementCommandService",
        "StrategicManagementRuntime.Commands.",
        "StrategicManagementRuntime.State.SetResourceAmount",
        "StrategicManagementRuntime.State.AddResource",
        ".FactionResources"
    })
    {
        AssertTrue(
            !tickBody.Contains(forbidden, StringComparison.Ordinal),
            $"world-clock bridge must not bypass Strategic Management Runtime facade fragment={forbidden}");
    }
}

internal static void StrategicWorldResourceBarReadsStrategicManagementResources()
{
    string root = ProjectRoot();
    string uiBootstrapPath = Path.Combine(root, "src", "Presentation", "World", "StrategicWorldRoot.UiBootstrap.cs");
    AssertTrue(File.Exists(uiBootstrapPath), $"strategic world UI bootstrap source should exist path={uiBootstrapPath}");

    string source = File.ReadAllText(uiBootstrapPath);
    string refreshBody = ExtractMethodBody(source, "private void RefreshResources()");

    AssertTrue(
        source.Contains("using Rpg.Application.StrategicManagement;", StringComparison.Ordinal) &&
        source.Contains("using Rpg.Definitions.StrategicManagement;", StringComparison.Ordinal),
        "strategic world resource bar should import Strategic Management Application and definition ids");
    AssertTrue(
        refreshBody.Contains("StrategicManagementRuntime.BuildDashboard(", StringComparison.Ordinal) &&
        refreshBody.Contains(".Resources", StringComparison.Ordinal),
        "strategic world resource bar should read faction-shared resources from Strategic Management dashboard resources");
    AssertTrue(
        !refreshBody.Contains("大地图结算", StringComparison.Ordinal) &&
        !refreshBody.Contains("DisplayName} {resource.Amount}", StringComparison.Ordinal) &&
        refreshBody.Contains("foreach (StrategicResourceViewModel resource in dashboard.Resources)", StringComparison.Ordinal) &&
        refreshBody.Contains("ticker.SetText(resource.Amount.ToString(), animate);", StringComparison.Ordinal),
        "strategic world resource ticker should show resource amounts only; settlement state belongs to notice and world-clock UI");

    foreach (string forbidden in new[]
    {
        "ResourceStore resources = State.PlayerResources",
        "State.PlayerResources",
        "StrategicWorldDisplayNames.GetResourceLabel",
        "StrategicWorldIds.ResourcePopulation",
        "StrategicWorldIds.ResourceEconomy",
        "StrategicWorldIds.ResourceStone",
        "resources.GetAvailable(",
        "resources.GetAmount("
    })
    {
        AssertTrue(
            !refreshBody.Contains(forbidden, StringComparison.Ordinal),
            $"strategic world top resource bar must not read legacy world resources fragment={forbidden}");
    }
}

internal static void StrategicWorldResourceBarUsesRollingTicker()
{
    string root = ProjectRoot();
    string hudScenePath = Path.Combine(root, "scenes", "world", "ui", "StrategicWorldHud.tscn");
    string themePath = Path.Combine(root, "resource", "ui", "themes", "game-ui-skin", "basic_ui_1_theme.tres");
    string tickerSourcePath = Path.Combine(root, "src", "Presentation", "World", "WorldResourceTicker.cs");
    string uiBootstrapPath = Path.Combine(root, "src", "Presentation", "World", "StrategicWorldRoot.UiBootstrap.cs");
    AssertTrue(File.Exists(hudScenePath), $"strategic world HUD scene should exist path={hudScenePath}");
    AssertTrue(File.Exists(themePath), $"strategic world theme should exist path={themePath}");
    AssertTrue(File.Exists(tickerSourcePath), "top strategic resource bar rolling animation should live in a focused component");

    string scene = File.ReadAllText(hudScenePath);
    string theme = File.ReadAllText(themePath);
    string tickerSource = File.ReadAllText(tickerSourcePath);
    string uiBootstrapSource = File.ReadAllText(uiBootstrapPath);
    string refreshBody = ExtractMethodBody(uiBootstrapSource, "private void RefreshResources()");
    string topLeftStatusBlock = ExtractSceneNodeBlock(scene, "[node name=\"TopLeftStatus\"");
    string resourceStripBlock = ExtractSceneNodeBlock(scene, "[node name=\"ResourceStrip\"");
    string foodIconBlock = ExtractSceneNodeBlock(scene, "[node name=\"FoodIcon\"");
    string moneyIconBlock = ExtractSceneNodeBlock(scene, "[node name=\"MoneyIcon\"");
    string oreIconBlock = ExtractSceneNodeBlock(scene, "[node name=\"OreIcon\"");
    string woodIconBlock = ExtractSceneNodeBlock(scene, "[node name=\"WoodIcon\"");
    string foodTickerBlock = ExtractSceneNodeBlock(scene, "[node name=\"FoodAmountTicker\"");
    string moneyTickerBlock = ExtractSceneNodeBlock(scene, "[node name=\"MoneyAmountTicker\"");
    string oreTickerBlock = ExtractSceneNodeBlock(scene, "[node name=\"OreAmountTicker\"");
    string woodTickerBlock = ExtractSceneNodeBlock(scene, "[node name=\"WoodAmountTicker\"");

    AssertTrue(
        scene.Contains("WorldResourceTicker.cs", StringComparison.Ordinal) &&
        scene.Contains("resource_food_icon_ai.png", StringComparison.Ordinal) &&
        scene.Contains("resource_money_icon_ai.png", StringComparison.Ordinal) &&
        scene.Contains("resource_ore_icon_ai.png", StringComparison.Ordinal) &&
        scene.Contains("resource_wood_icon_ai.png", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"TopLeftStatus\" type=\"PanelContainer\" parent=\"TopBarHost\"]", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"Margin\" type=\"MarginContainer\" parent=\"TopBarHost/TopLeftStatus\"]", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"ResourceStrip\" type=\"HBoxContainer\" parent=\"TopBarHost/TopLeftStatus/Margin\"]", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"FoodIcon\" type=\"TextureRect\" parent=\"TopBarHost/TopLeftStatus/Margin/ResourceStrip/FoodSlot\"]", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"FoodAmountTicker\" type=\"Control\" parent=\"TopBarHost/TopLeftStatus/Margin/ResourceStrip/FoodSlot\"]", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"CurrentLabel\" type=\"Label\" parent=\"TopBarHost/TopLeftStatus/Margin/ResourceStrip/FoodSlot/FoodAmountTicker\"]", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"IncomingLabel\" type=\"Label\" parent=\"TopBarHost/TopLeftStatus/Margin/ResourceStrip/FoodSlot/FoodAmountTicker\"]", StringComparison.Ordinal),
        "strategic world resource bar should use authored AI resource icons and roll only amount labels");
    AssertTrue(
        topLeftStatusBlock.Contains("theme_type_variation = &\"WorldTopStatusPanel\"", StringComparison.Ordinal) &&
        theme.Contains("WorldTopStatusPanel/base_type = &\"PanelContainer\"", StringComparison.Ordinal) &&
        theme.Contains("basic_ui_1_panel_topbar.tres", StringComparison.Ordinal),
        "strategic world top-left resources should sit on a reusable window-frame panel background");
    AssertTrue(
        tickerSource.Contains("public partial class WorldResourceTicker : Control", StringComparison.Ordinal) &&
        tickerSource.Contains("public void SetText(string text, bool animate)", StringComparison.Ordinal) &&
        tickerSource.Contains("TweenProperty", StringComparison.Ordinal) &&
        tickerSource.Contains("ClipContents = true", StringComparison.Ordinal),
        "WorldResourceTicker should own the rolling label animation");
    AssertTrue(
        resourceStripBlock.Contains("theme_override_constants/separation = 14", StringComparison.Ordinal) &&
        foodIconBlock.Contains("texture = ExtResource(\"21_food_icon\")", StringComparison.Ordinal) &&
        moneyIconBlock.Contains("texture = ExtResource(\"22_money_icon\")", StringComparison.Ordinal) &&
        oreIconBlock.Contains("texture = ExtResource(\"23_ore_icon\")", StringComparison.Ordinal) &&
        woodIconBlock.Contains("texture = ExtResource(\"24_wood_icon\")", StringComparison.Ordinal) &&
        foodIconBlock.Contains("expand_mode = 1", StringComparison.Ordinal) &&
        foodIconBlock.Contains("stretch_mode = 5", StringComparison.Ordinal) &&
        foodTickerBlock.Contains("script = ExtResource(\"20_resource_ticker\")", StringComparison.Ordinal) &&
        moneyTickerBlock.Contains("script = ExtResource(\"20_resource_ticker\")", StringComparison.Ordinal) &&
        oreTickerBlock.Contains("script = ExtResource(\"20_resource_ticker\")", StringComparison.Ordinal) &&
        woodTickerBlock.Contains("script = ExtResource(\"20_resource_ticker\")", StringComparison.Ordinal) &&
        !scene.Contains("NameLabel\" type=\"Label\" parent=\"TopBarHost/TopLeftStatus/Margin/ResourceStrip", StringComparison.Ordinal) &&
        !scene.Contains("text = \"粮食\"", StringComparison.Ordinal) &&
        !scene.Contains("text = \"资金\"", StringComparison.Ordinal) &&
        !scene.Contains("text = \"矿石\"", StringComparison.Ordinal) &&
        !scene.Contains("text = \"木材\"", StringComparison.Ordinal) &&
        !tickerSource.Contains("TextServer.OverrunBehavior.TrimEllipsis", StringComparison.Ordinal),
        "strategic world resource HUD should use static icons for resource types and animate only numeric amount controls");
    AssertTrue(
        uiBootstrapSource.Contains("private readonly Dictionary<string, WorldResourceTicker> _resourceAmountTickers", StringComparison.Ordinal) &&
        refreshBody.Contains("_resourceAmountTickers.TryGetValue(resource.ResourceId, out WorldResourceTicker ticker)", StringComparison.Ordinal) &&
        refreshBody.Contains("ticker.SetText(resource.Amount.ToString(), animate);", StringComparison.Ordinal) &&
        refreshBody.Contains("_lastResourceTickerSignature", StringComparison.Ordinal),
        "strategic world refresh should bind amount-only tickers and animate only resource amount changes");
    AssertTrue(
        !refreshBody.Contains("_resourceLabel.Text =", StringComparison.Ordinal),
        "strategic world resource totals should not bypass the rolling ticker by setting a raw Label directly");
}

internal static void PresentationHudLayoutAvoidsStretchAnchorSizeWarnings()
{
    string root = ProjectRoot();
    string tickerSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "WorldResourceTicker.cs"));
    string siteHudSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "Sites", "WorldSiteRoot.SiteManagementHud.cs"));
    string prepareLabelBody = ExtractMethodBody(tickerSource, "private static void PrepareLabel(Label label)");
    string layoutLabelsBody = ExtractMethodBody(tickerSource, "private void LayoutLabels(bool resetPositions)");
    string applySiteHudFullRectBody = ExtractMethodBody(siteHudSource, "private void ApplySiteHudFullRect(string reason)");

    AssertTrue(
        prepareLabelBody.Contains("label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft)", StringComparison.Ordinal) &&
        layoutLabelsBody.Contains("_currentLabel.Size = labelSize", StringComparison.Ordinal) &&
        layoutLabelsBody.Contains("_incomingLabel.Size = labelSize", StringComparison.Ordinal),
        "resource ticker labels should use fixed top-left anchors before script-driven size and roll position updates");
    AssertTrue(
        !applySiteHudFullRectBody.Contains("_siteHudRoot.Size", StringComparison.Ordinal),
        "site HUD full-rect root should rely on anchors and offsets instead of writing Size after _ready");
}

internal static void PresentationUiFocusVisualsAreProjectHiddenByDefault()
{
    string root = ProjectRoot();
    string projectConfig = File.ReadAllText(Path.Combine(root, "project.godot"));
    string focusThemePath = Path.Combine(root, "resource", "ui", "themes", "game-ui-focus_defaults.tres");
    string skinSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "Common", "GameUiSkin.cs"));
    string factorySource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "Common", "GameUiSceneFactory.cs"));
    string instantiateBody = ExtractMethodBody(factorySource, "public static T Instantiate<T>(string scenePath, string ownerName) where T : Node");
    string applyButtonBody = ExtractMethodBody(skinSource, "public static void ApplyButton(Button button, GameUiButtonKind kind = GameUiButtonKind.Secondary)");

    AssertTrue(
        projectConfig.Contains("[gui]", StringComparison.Ordinal) &&
        projectConfig.Contains("theme/custom=\"res://resource/ui/themes/game-ui-focus_defaults.tres\"", StringComparison.Ordinal),
        "project UI should define hidden focus defaults once so new themes inherit the no-outline policy");
    AssertTrue(File.Exists(focusThemePath), "hidden focus default theme resource should exist");

    string focusTheme = File.Exists(focusThemePath) ? File.ReadAllText(focusThemePath) : "";
    AssertTrue(
        focusTheme.Contains("[sub_resource type=\"StyleBoxEmpty\"", StringComparison.Ordinal) &&
        focusTheme.Contains("Button/styles/focus = SubResource(", StringComparison.Ordinal),
        "project hidden focus theme should assign StyleBoxEmpty to Button/styles/focus");
    AssertTrue(
        skinSource.Contains("private static readonly StyleBoxEmpty HiddenFocusStyle = new();", StringComparison.Ordinal) &&
        skinSource.Contains("public static void ApplyProjectFocusStyle(Node root)", StringComparison.Ordinal) &&
        skinSource.Contains("button.AddThemeStyleboxOverride(\"focus\", HiddenFocusStyle)", StringComparison.Ordinal),
        "GameUiSkin should own a cached empty focus style and apply it to buttons without disabling focus navigation");
    AssertTrue(
        applyButtonBody.Contains("ApplyHiddenFocusStyle(button)", StringComparison.Ordinal) &&
        applyButtonBody.Contains("button.FocusMode = Control.FocusModeEnum.All", StringComparison.Ordinal),
        "direct button skinning should hide the focus visual while keeping keyboard/controller focus enabled");
    AssertTrue(
        instantiateBody.Contains("GameUiSkin.ApplyProjectFocusStyle(node)", StringComparison.Ordinal),
        "all resource-backed UI scenes instantiated through GameUiSceneFactory should receive the hidden focus style centrally");
}

internal static void StrategicWorldProductionFeedbackFloatsFromSettlementEvents()
{
    string root = ProjectRoot();
    string worldClockPath = Path.Combine(root, "src", "Presentation", "World", "StrategicWorldRoot.WorldClock.cs");
    string feedbackPath = Path.Combine(root, "src", "Presentation", "World", "StrategicWorldRoot.ResourceFeedback.cs");
    string floatTextPath = Path.Combine(root, "src", "Presentation", "World", "WorldResourceFloatText.cs");
    string floatTextScenePath = Path.Combine(root, "scenes", "world", "ui", "WorldResourceFloatText.tscn");
    string factoryPath = Path.Combine(root, "src", "Presentation", "Common", "GameUiSceneFactory.cs");
    string hudScenePath = Path.Combine(root, "scenes", "world", "ui", "StrategicWorldHud.tscn");
    AssertTrue(File.Exists(feedbackPath), "large-map resource production feedback should live in a focused StrategicWorldRoot partial");
    AssertTrue(File.Exists(floatTextPath), "resource production float text should have a focused script");
    AssertTrue(File.Exists(floatTextScenePath), "resource production float text should use an authored reusable scene");
    AssertTrue(File.Exists(hudScenePath), "strategic world HUD should own resource float overlay authoring");

    string worldClockSource = File.ReadAllText(worldClockPath);
    string feedbackSource = File.ReadAllText(feedbackPath);
    string floatTextSource = File.ReadAllText(floatTextPath);
    string floatTextScene = File.ReadAllText(floatTextScenePath);
    string factorySource = File.ReadAllText(factoryPath);
    string hudScene = File.ReadAllText(hudScenePath);
    string tickBody = ExtractMethodBody(worldClockSource, "private void AdvanceWorldClockTick()");
    string feedbackBody = ExtractMethodBody(feedbackSource, "private void ShowStrategicProductionFeedback(");

    AssertTrue(
        tickBody.Contains("ShowStrategicProductionFeedback(strategicSettlement)", StringComparison.Ordinal) &&
        tickBody.IndexOf("StrategicManagementRuntime.SettleElapsedWorldTime(1)", StringComparison.Ordinal) <
        tickBody.IndexOf("ShowStrategicProductionFeedback(strategicSettlement)", StringComparison.Ordinal),
        "large-map world clock should display production feedback from the Strategic Management settlement result");
    foreach (string required in new[]
    {
        "StrategicLocationProductionSettled",
        "StrategicCityBuildingProductionSettled",
        "TryParseStrategicResourceAmounts",
        "StrategicManagementRuntime.Definitions.Locations",
        "GetSiteLabelRect(",
        "GameUiSceneFactory.CreateWorldResourceFloatText",
        "_worldResourceFloatOverlay"
    })
    {
        AssertTrue(feedbackSource.Contains(required, StringComparison.Ordinal), $"production feedback should consume settlement events and map them to authored UI fragment={required}");
    }
    foreach (string forbidden in new[]
    {
        "StrategicManagementRuntime.Commands.",
        "StrategicManagementRuntime.State.AddResource",
        "_rules.GetLocationProduction",
        "_rules.GetCityBuildingProduction",
        "new Label",
        "new TextureRect"
    })
    {
        AssertTrue(!feedbackBody.Contains(forbidden, StringComparison.Ordinal), $"production feedback must not recalculate resources or build raw controls fragment={forbidden}");
    }
    AssertTrue(
        factorySource.Contains("WorldResourceFloatTextScenePath", StringComparison.Ordinal) &&
        factorySource.Contains("CreateWorldResourceFloatText", StringComparison.Ordinal),
        "GameUiSceneFactory should own resource float text scene loading");
    AssertTrue(
        floatTextSource.Contains("public partial class WorldResourceFloatText : Control", StringComparison.Ordinal) &&
        floatTextSource.Contains("public void Bind(", StringComparison.Ordinal) &&
        floatTextSource.Contains("public void Play(", StringComparison.Ordinal) &&
        floatTextSource.Contains("TweenProperty", StringComparison.Ordinal) &&
        floatTextScene.Contains("WorldResourceFloatText.cs", StringComparison.Ordinal),
        "resource float text should bind icon text plus +N and animate through its authored scene");
    AssertTrue(
        hudScene.Contains("[node name=\"WorldResourceFloatOverlay\" type=\"Control\" parent=\"OverlayHost\"]", StringComparison.Ordinal) &&
        feedbackSource.Contains("BindWorldResourceFloatOverlay(") &&
        feedbackSource.Contains("GameUiSceneFactory.GetRequiredNode<Control>(") &&
        !feedbackSource.Contains("new Control", StringComparison.Ordinal) &&
        !feedbackSource.Contains("AddChild(_worldResourceFloatOverlay)", StringComparison.Ordinal),
        "resource float overlay host should be authored in StrategicWorldHud.tscn and bound by Presentation instead of constructed at runtime");
}

internal static void StrategicWorldDetailReadsStrategicManagementLocationDashboard()
{
    string root = ProjectRoot();
    string detailPath = Path.Combine(root, "src", "Presentation", "World", "StrategicWorldRoot.DetailHud.cs");
    AssertTrue(File.Exists(detailPath), $"strategic world detail HUD source should exist path={detailPath}");

    string source = File.ReadAllText(detailPath);
    string refreshBody = ExtractMethodBody(source, "private void RefreshDetail(StrategicWorldDefinitionQueries queries)");

    foreach (string required in new[]
    {
        "using Rpg.Application.StrategicManagement;",
        "using Rpg.Definitions.StrategicManagement;",
        "using Rpg.Domain.StrategicManagement;",
        "StrategicManagementRuntime.LocationMappings.TryResolveLocationIdForMapSite(",
        "StrategicManagementRuntime.BuildLocationDashboard(",
        "StrategicLocationDashboardViewModel",
        "StrategicCityManagementViewModel",
        "ProductionDisplayText",
        "SourcePermissionDisplayText",
        "BuildStrategicLocationContextSummary(",
        "BuildCityCompactOperationSummary("
    })
    {
        AssertTrue(source.Contains(required, StringComparison.Ordinal), $"strategic world detail should consume Strategic Management dashboard fragment={required}");
    }

    foreach (string forbidden in new[]
    {
        "State.SiteStates.TryGetValue(_selectedSiteId, out WorldSiteState site)",
        "out WorldSiteState site",
        "site.Facilities",
        "site.Garrison",
        "AddSiteGarrisonLines(",
        "GetFacilityStateLabel(",
        "AddStrategicFacilityLines(",
        "AddStrategicCorpsLines(",
        "StrategicWorldDisplayNames.GetFactionLabel",
        "StrategicWorldDisplayNames.GetResourceLabel",
        "StrategicWorldIds.FacilityMine",
        "StrategicWorldIds.FacilityDefenseTower"
    })
    {
        AssertTrue(!refreshBody.Contains(forbidden, StringComparison.Ordinal), $"large-map detail must not read legacy site management facts fragment={forbidden}");
    }

    foreach (string forbiddenMethod in new[]
    {
        "private void AddSiteGarrisonLines(",
        "private bool TryRefreshStaleSiteDetail("
    })
    {
        AssertTrue(!source.Contains(forbiddenMethod, StringComparison.Ordinal), $"legacy strategic detail helper should be removed fragment={forbiddenMethod}");
    }
}

internal static void StrategicWorldHoverSummaryReadsStrategicManagementCityAssets()
{
    string root = ProjectRoot();
    string rootSource = ReadStrategicWorldRootSource();
    string hoverBody = ExtractMethodBody(rootSource, "private void RefreshSiteHoverSummary(");
    string presenterSource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "World", "WorldSiteHoverSummaryPresenter.cs"));

    AssertTrue(
        hoverBody.Contains("TryBuildStrategicLocationDashboardForMapSite(siteId", StringComparison.Ordinal) &&
        hoverBody.Contains("WorldSiteHoverSummaryPresenter.Build(definition, dashboard)", StringComparison.Ordinal),
        "strategic world hover summary should bind from Strategic Management location dashboard for the hovered map site");
    AssertTrue(
        presenterSource.Contains("StrategicManagementDashboardViewModel", StringComparison.Ordinal) &&
        presenterSource.Contains("ReserveForces", StringComparison.Ordinal) &&
        presenterSource.Contains("HeroCompanies", StringComparison.Ordinal),
        "hover summary presenter should format city assets, reserve forces, and current-city battle groups from Strategic Management");

    string[] forbiddenFragments =
    {
        "WorldSiteState",
        "ResourceStore",
        "ResourcePopulation",
        "ResourceEconomy",
        ".Garrison",
        "ActiveTags",
        "GetSiteArmyCount",
        "GetSiteHeroCount",
        "兵团"
    };
    foreach (string fragment in forbiddenFragments)
    {
        AssertTrue(
            !presenterSource.Contains(fragment, StringComparison.Ordinal),
            $"hover summary presenter must not read legacy world-site summary data fragment={fragment}");
    }
}

internal static void StrategicWorldHoverSummaryPanelDoesNotEllipsizeAssets()
{
    string root = ProjectRoot();
    string hoverScenePath = Path.Combine(root, "scenes", "world", "ui", "WorldSiteHoverSummaryPanel.tscn");
    AssertTrue(File.Exists(hoverScenePath), $"strategic world hover summary scene should exist path={hoverScenePath}");

    string scene = File.ReadAllText(hoverScenePath);
    string rootBlock = ExtractSceneNodeBlock(scene, "[node name=\"WorldSiteHoverSummaryPanel\"");
    string titleBlock = ExtractSceneNodeBlock(scene, "[node name=\"TitleLabel\"");
    string resourceBlock = ExtractSceneNodeBlock(scene, "[node name=\"ResourceLabel\"");
    string forceBlock = ExtractSceneNodeBlock(scene, "[node name=\"ForceLabel\"");
    (float width, _) = ReadSceneNodeMinimumSize(rootBlock, "WorldSiteHoverSummaryPanel", hoverScenePath);

    AssertTrue(
        width >= 260f,
        $"hover summary panel should leave enough baseline width for all foundation assets instead of the old narrow size width={width}");

    foreach (string block in new[] { titleBlock, resourceBlock, forceBlock })
    {
        AssertTrue(
            !block.Contains("text_overrun_behavior = 3", StringComparison.Ordinal) &&
            !block.Contains("clip_text = true", StringComparison.Ordinal),
            "hover summary labels must not ellipsize or clip strategic asset text; the panel should grow to fit the content");
    }

    foreach (string forbidden in new[] { "...", "ResourcePopulation", "ResourceEconomy", "兵团" })
    {
        AssertTrue(
            !scene.Contains(forbidden, StringComparison.Ordinal),
            $"hover summary authored defaults should not carry legacy or placeholder text fragment={forbidden}");
    }
}

internal static void StrategicWorldSelectedManagedCityActionsUseStrategicManagementAuthority()
{
    string root = ProjectRoot();
    string expeditionHudPath = Path.Combine(root, "src", "Presentation", "World", "StrategicWorldRoot.ExpeditionHud.cs");
    string siteEntryPath = Path.Combine(root, "src", "Presentation", "World", "StrategicWorldRoot.SiteEntry.cs");
    AssertTrue(File.Exists(expeditionHudPath), $"strategic world expedition HUD source should exist path={expeditionHudPath}");
    AssertTrue(File.Exists(siteEntryPath), $"strategic world site entry source should exist path={siteEntryPath}");

    string expeditionHudSource = File.ReadAllText(expeditionHudPath);
    string siteEntrySource = File.ReadAllText(siteEntryPath);
    string refreshControlsBody = ExtractMethodBody(expeditionHudSource, "private bool RefreshExpeditionControls()");
    string canEnterBody = ExtractMethodBody(siteEntrySource, "private bool CanEnterSelectedSiteDetail(");
    string canShowBody = ExtractMethodBody(siteEntrySource, "private bool CanShowSelectedSiteDetailEntry()");

    foreach (string required in new[]
    {
        "TryBuildSelectedStrategicLocationDashboard(",
        "StrategicManagementRuntime.LocationMappings.TryResolveLocationIdForMapSite(",
        "StrategicManagementRuntime.BuildLocationDashboard(",
        "dashboard.SelectedLocation.CanManageCity"
    })
    {
        AssertTrue(siteEntrySource.Contains(required, StringComparison.Ordinal), $"large-map selected city actions should use Strategic Management authority fragment={required}");
    }

    AssertTrue(
        refreshControlsBody.Contains("bool handledStrategicLocationActions", StringComparison.Ordinal) &&
        refreshControlsBody.Contains("CanShowSelectedSiteDetailEntry()", StringComparison.Ordinal) &&
        refreshControlsBody.Contains("return handledStrategicLocationActions;", StringComparison.Ordinal),
        "selected managed-city actions should be fully handled before legacy WorldActionResolver fallback can show global wait actions");
    AssertTrue(
        canShowBody.Contains("dashboard.SelectedLocation.CanManageCity", StringComparison.Ordinal),
        "selected-site detail entry visibility should follow the Strategic Management location dashboard");
    AssertTrue(
        canEnterBody.Contains("dashboard.SelectedLocation.CanManageCity", StringComparison.Ordinal),
        "selected-site detail entry validation should follow the Strategic Management location dashboard");

    foreach (string forbidden in new[]
    {
        "site.OwnerFactionId",
        "site.ControlState",
        "selectedSite?.OwnerFactionId",
        "selectedSite.ControlState",
        "State.SiteStates.TryGetValue(_selectedSiteId, out WorldSiteState site)"
    })
    {
        AssertTrue(!refreshControlsBody.Contains(forbidden, StringComparison.Ordinal), $"selected city action binding must not gate managed-city actions on legacy WorldSite state fragment={forbidden}");
        AssertTrue(!canEnterBody.Contains(forbidden, StringComparison.Ordinal), $"selected city entry validation must not gate managed-city actions on legacy WorldSite state fragment={forbidden}");
        AssertTrue(!canShowBody.Contains(forbidden, StringComparison.Ordinal), $"selected city detail visibility must not gate managed-city actions on legacy WorldSite state fragment={forbidden}");
    }
}

internal static void StrategicWorldResetAndDetailInitializeStrategicManagementRuntime()
{
    string root = ProjectRoot();
    string detailPath = Path.Combine(root, "src", "Presentation", "World", "StrategicWorldRoot.DetailHud.cs");
    string uiBootstrapPath = Path.Combine(root, "src", "Presentation", "World", "StrategicWorldRoot.UiBootstrap.cs");
    AssertTrue(File.Exists(detailPath), $"strategic world detail HUD source should exist path={detailPath}");
    AssertTrue(File.Exists(uiBootstrapPath), $"strategic world UI bootstrap source should exist path={uiBootstrapPath}");

    string detailSource = File.ReadAllText(detailPath);
    string uiBootstrapSource = File.ReadAllText(uiBootstrapPath);
    string refreshBody = ExtractMethodBody(detailSource, "private void RefreshDetail(StrategicWorldDefinitionQueries queries)");
    string resetBody = ExtractMethodBody(uiBootstrapSource, "private void ResetWorld()");

    AssertTrue(
        refreshBody.Contains("StrategicManagementRuntime.EnsureInitialized()", StringComparison.Ordinal) &&
        refreshBody.IndexOf("StrategicManagementRuntime.EnsureInitialized()", StringComparison.Ordinal) <
        refreshBody.IndexOf("StrategicManagementRuntime.LocationMappings.TryResolveLocationIdForMapSite", StringComparison.Ordinal),
        "large-map detail should initialize Strategic Management runtime before reading map-site mappings");
    AssertTrue(
        resetBody.Contains("StrategicManagementRuntime.Reset()", StringComparison.Ordinal),
        "strategic world reset should reset the Strategic Management runtime now that top resource and detail panels read it");
}

internal static void WorldSiteRootDelegatesSiteManagementHudLists()
{
    string siteRootDir = Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites");
    string rootSource = File.ReadAllText(Path.Combine(siteRootDir, "WorldSiteRoot.SiteManagementHud.cs"));
    string rootClassSource = File.ReadAllText(Path.Combine(siteRootDir, "WorldSiteRoot.cs"));
    string binderPath = Path.Combine(siteRootDir, "StrategicManagementDashboardPanelBinder.cs");
    string legacyBinderPath = Path.Combine(siteRootDir, "WorldSiteManagementPanelBinder.cs");
    AssertTrue(File.Exists(binderPath), "strategic management HUD binding should live in StrategicManagementDashboardPanelBinder");
    AssertTrue(!File.Exists(legacyBinderPath), "legacy world-site management binder should not remain as a second city-management display authority");

    string binderSource = File.ReadAllText(binderPath);
    AssertTrue(
        binderSource.Contains("internal sealed class StrategicManagementDashboardPanelBinder", StringComparison.Ordinal),
        "strategic management dashboard binder should be a focused internal Presentation collaborator");
    AssertTrue(
        rootClassSource.Contains("private StrategicManagementDashboardPanelBinder _strategicManagementDashboardPanelBinder", StringComparison.Ordinal),
        "WorldSiteRoot should own a focused strategic dashboard binder");

    string bindBody = ExtractMethodBody(rootSource, "private void BindSiteManagementPanel");
    foreach (string required in new[]
    {
        "StrategicManagementRuntime.BuildDashboard(",
        "StrategicManagementRuntime.BuildLocationDashboard(",
        "_strategicManagementDashboardPanelBinder.Bind(",
        "_strategicManagementDashboardPanelBinder.BindLocation("
    })
    {
        AssertTrue(bindBody.Contains(required, StringComparison.Ordinal), $"WorldSiteRoot site-management refresh should use strategic dashboard binding fragment={required}");
    }

    foreach (string rootMethod in new[]
    {
        "private string BuildResourceLine()",
        "private string BuildSiteOverview(",
        "_siteManagementPanelBinder.RefreshFacilityList(",
        "_siteManagementPanelBinder.RefreshFacilityBuildList(",
        "_siteManagementPanelBinder.RefreshGarrisonList(",
        "_siteManagementPanelBinder.RefreshActionList("
    })
    {
        AssertTrue(!rootSource.Contains(rootMethod, StringComparison.Ordinal), $"WorldSiteRoot should not retain legacy site-management display fragment {rootMethod}");
    }

    AssertTrue(
        binderSource.Contains("StrategicManagementDashboardViewModel", StringComparison.Ordinal) &&
        binderSource.Contains("StrategicLocationDashboardViewModel", StringComparison.Ordinal) &&
        binderSource.Contains("BindLocation(", StringComparison.Ordinal) &&
        binderSource.Contains("ProductionDisplayText", StringComparison.Ordinal) &&
        binderSource.Contains("ProductionPerWorldTimePulse", StringComparison.Ordinal) &&
        binderSource.Contains("StrategicBuildingOptionViewModel", StringComparison.Ordinal) &&
        binderSource.Contains("GameUiSceneFactory.CreateWorldMutedLine", StringComparison.Ordinal) &&
        binderSource.Contains("GameUiSceneFactory.CreateWorldBuildingOptionCard", StringComparison.Ordinal) &&
        binderSource.Contains("_selectBuildingForPlacement", StringComparison.Ordinal) &&
        binderSource.Contains("Selected +=", StringComparison.Ordinal) &&
        binderSource.Contains("ReserveRecoveryPerElapsedPulse", StringComparison.Ordinal),
        "strategic management dashboard binder should render building cards and the read-only reserve recovery fact");
    AssertTrue(
        !binderSource.Contains("GameUiSceneFactory.CreateWorldCorpsInstanceRow", StringComparison.Ordinal) &&
        !binderSource.Contains("GameUiSceneFactory.CreateWorldMilitaryHeroCard", StringComparison.Ordinal) &&
        !binderSource.Contains("_replenishCorps", StringComparison.Ordinal) &&
        !binderSource.Contains("_toggleHeroAssignment", StringComparison.Ordinal) &&
        !binderSource.Contains("ReplenishRequested +=", StringComparison.Ordinal),
        "site-management dashboard binder should not expose the removed corps tab, replenishment rows, or hero reassignment cards");

    AssertTrue(
        !binderSource.Contains("StrategicBattlePreparationOptionViewModel", StringComparison.Ordinal) &&
        !binderSource.Contains("BattlePreparations", StringComparison.Ordinal) &&
        !binderSource.Contains("_selectBattlePreparation", StringComparison.Ordinal),
        "strategic management dashboard binder should not render deleted strategic battle-preparation choices");

    foreach (string forbidden in new[]
    {
        "StrategicManagementRuntime",
        "StrategicManagementCommandService",
        "StrategicWorldRuntime",
        "WorldSiteState",
        "WorldSiteDefinition",
        "WorldActionResolver",
        "WorldActionViewModel",
        "Rpg.Domain.World",
        "Rpg.Application.World",
        ".Apply(",
        "BeginAndActivate",
        "RefreshSiteMapEntities",
        "EnsureSitePlacementsRespectTerrain"
    })
    {
        AssertTrue(
            !binderSource.Contains(forbidden, StringComparison.Ordinal),
            $"site management HUD binder should stay display/callback-only and must not use {forbidden}");
    }
}

internal static void WorldSiteRootRoutesStrategicManagementDashboardCommands()
{
    string siteRootDir = Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites");
    string rootSource = ReadWorldSiteRootSource();
    string siteHudSource = File.ReadAllText(Path.Combine(siteRootDir, "WorldSiteRoot.SiteManagementHud.cs"));

    string buildHudBody = ExtractMethodBody(siteHudSource, "private void BuildSiteHud()");
    AssertTrue(
        buildHudBody.Contains("OnStrategicBuildBuildingSelected", StringComparison.Ordinal),
        "WorldSiteRoot should pass the building command callback into the dashboard binder");
    AssertTrue(
        rootSource.Contains("StrategicManagementRuntime.Commands.BuildCityBuilding(", StringComparison.Ordinal),
        "WorldSiteRoot should route building placement through Strategic Management commands");

    AssertTrue(
        !siteHudSource.Contains("OnStrategicReplenishCorpsPressed", StringComparison.Ordinal) &&
        !siteHudSource.Contains("OnStrategicHeroAssignmentPressed", StringComparison.Ordinal) &&
        !siteHudSource.Contains("StrategicManagementRuntime.Commands.ReplenishCorps(", StringComparison.Ordinal) &&
        !rootSource.Contains("OnStrategicBattlePreparationPressed", StringComparison.Ordinal) &&
        !rootSource.Contains("StrategicManagementRuntime.Commands.SelectBattlePreparation(", StringComparison.Ordinal),
        "WorldSiteRoot should not route deleted corps-tab replenishment, old hero-assignment, or strategic battle-preparation commands");

    AssertTrue(
        rootSource.Contains("private void HandleStrategicManagementCommandResult(", StringComparison.Ordinal) &&
        rootSource.Contains("RefreshSiteManagementUi(", StringComparison.Ordinal),
        "WorldSiteRoot should refresh the strategic dashboard with a command-result notice after command submission");

    string buildBuildingBody = ExtractMethodBody(rootSource, "private void TrySubmitStrategicBuildingPlacement(");
    string recruitCorpsBody = ExtractMethodBody(rootSource, "private void OnStrategicRecruitCorpsForHeroPressed(");
    string commandBodies = buildBuildingBody + recruitCorpsBody;

    AssertTrue(
        rootSource.Contains("StrategicManagementRuntime.LocationMappings.TryResolveCityIdForMapSite(", StringComparison.Ordinal) &&
        rootSource.Contains("StrategicManagementRuntime.LocationMappings.TryResolveLocationIdForMapSite(", StringComparison.Ordinal) &&
        !rootSource.Contains("return StrategicManagementIds.LocationPlainsCity;", StringComparison.Ordinal),
        "WorldSiteRoot should use explicit Strategic Management map-site resolution instead of silently mapping every site to the first city");
    AssertTrue(
        rootSource.Contains("private static bool TryResolveStrategicManagementLocationId(", StringComparison.Ordinal),
        "WorldSiteRoot should resolve mapped non-city strategic locations separately from managed city resolution");
    AssertTrue(
        ExtractMethodBody(rootSource, "private void BindSiteManagementPanel(").Contains("TryResolveStrategicManagementLocationId(_siteHudSiteId, out string locationId)", StringComparison.Ordinal) &&
        ExtractMethodBody(rootSource, "private void BindSiteManagementPanel(").Contains("StrategicManagementRuntime.BuildLocationDashboard(", StringComparison.Ordinal),
        "WorldSiteRoot should route mapped non-city sites into a Strategic Management location dashboard");
    AssertTrue(
        buildBuildingBody.Contains("TryResolveStrategicManagementCityId(_siteHudSiteId, out string cityId)", StringComparison.Ordinal) &&
        recruitCorpsBody.Contains("TryResolveStrategicManagementCityId(_siteHudSiteId, out string cityId)", StringComparison.Ordinal),
        "city-management commands should guard command submission behind selected strategic city resolution");
    AssertTrue(
        siteHudSource.Contains("OpenStrategicMilitaryWorkbench", StringComparison.Ordinal) &&
        siteHudSource.Contains("OnStrategicMilitaryHeroSelected", StringComparison.Ordinal) &&
        siteHudSource.Contains("OnStrategicRecruitCorpsForHeroPressed", StringComparison.Ordinal) &&
        siteHudSource.Contains("StrategicManagementRuntime.Commands.RecruitCorpsForHero(", StringComparison.Ordinal) &&
        !siteHudSource.Contains("StrategicManagementRuntime.Commands.AssignCorpsToHero(", StringComparison.Ordinal) &&
        !siteHudSource.Contains("StrategicManagementRuntime.Commands.UnassignCorpsFromHero(", StringComparison.Ordinal),
        "hero reassignment should be routed through the recruitment workbench and hero-directed recruitment command");
    AssertTrue(
        !commandBodies.Contains("WorldActionResolver", StringComparison.Ordinal) &&
        !commandBodies.Contains("_worldActionResolver", StringComparison.Ordinal),
        "strategic dashboard command callbacks must not route through legacy world action authority");
}

internal static void WorldSiteRootShowsStrategicManagementPanelOnlyForPlayerCityManagement()
{
    string siteRootDir = Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites");
    string rootSource = File.ReadAllText(Path.Combine(siteRootDir, "WorldSiteRoot.SiteManagementHud.cs"));
    string visibilityBody = ExtractMethodBody(rootSource, "private void UpdateSiteManagementEntryVisibility(");

    AssertTrue(
        rootSource.Contains("private bool ShouldShowSiteManagementEntry(", StringComparison.Ordinal) &&
        visibilityBody.Contains("ShouldShowSiteManagementEntry()", StringComparison.Ordinal),
        "site management entry visibility should be delegated to a named helper that owns the player-city management gate");

    string helperBody = ExtractMethodBody(rootSource, "private bool ShouldShowSiteManagementEntry(");
    foreach (string required in new[]
    {
        "_siteHudRoot?.Visible == true",
        "_isBattlePreparationActive",
        "_battleRuntimeEnabled",
        "CanOpenManagedCityDetail(_siteHudSiteId)"
    })
    {
        AssertTrue(
            helperBody.Contains(required, StringComparison.Ordinal),
            $"site management entry should show only for player-owned city management fragment={required}");
    }

    string cityGateBody = ExtractMethodBody(rootSource, "private static bool CanOpenManagedCityDetail(");
    foreach (string required in new[]
    {
        "TryResolveStrategicManagementCityId(worldSiteId, out string cityId)",
        "StrategicManagementRuntime.State.Locations.TryGetValue(cityId, out StrategicLocationState location)",
        "location.OwnerFactionId == StrategicManagementIds.FactionPlayer",
        "location.ControlState == StrategicLocationControlState.PlayerHeld",
        "StrategicManagementRuntime.State.Cities.ContainsKey(cityId)"
    })
    {
        AssertTrue(
            cityGateBody.Contains(required, StringComparison.Ordinal),
            $"managed city panel gate should read Strategic Management city ownership fragment={required}");
    }

    AssertTrue(
        !helperBody.Contains("_selectedFacilitySlotId", StringComparison.Ordinal) &&
        !helperBody.Contains("_battleRuntimeCommandPauseActive", StringComparison.Ordinal) &&
        !helperBody.Contains("TryResolveStrategicManagementLocationId(_siteHudSiteId, out _)", StringComparison.Ordinal) &&
        !helperBody.Contains("CanOpenSiteDetail(ResolveSiteState(_siteHudSiteId))", StringComparison.Ordinal),
        "site management entry visibility must not depend on retired slots, battle pause, generic strategic-location mapping, or legacy WorldSite ownership");
}

internal static void WorldSiteManagementMapSuppressesLegacyUnitsForManagedCity()
{
    string siteRootDir = Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites");
    string mapSource = File.ReadAllText(Path.Combine(siteRootDir, "WorldSiteRoot.SiteMapPresentation.cs"));
    string hudSource = File.ReadAllText(Path.Combine(siteRootDir, "WorldSiteRoot.SiteManagementHud.cs"));
    string refreshBody = ExtractMethodBody(mapSource, "private void RefreshSiteMapEntities(");
    string renderGateBody = ExtractMethodBody(mapSource, "private bool ShouldRenderLegacySitePlacementUnits(");
    string bindBody = ExtractMethodBody(hudSource, "private void BindSiteManagementPanel(");
    string garrisonGateBody = ExtractMethodBody(hudSource, "private void EnsureLegacySiteGarrisonPlacementsForPresentation(");

    AssertTrue(
        refreshBody.Contains("ShouldRenderLegacySitePlacementUnits(site)", StringComparison.Ordinal) &&
        refreshBody.Contains("legacyPlacementsVisible", StringComparison.Ordinal),
        "site map presentation should gate legacy WorldSite unit placements before rendering city management entities");
    AssertTrue(
        renderGateBody.Contains("_isBattlePreparationActive", StringComparison.Ordinal) &&
        renderGateBody.Contains("_battleRuntimeEnabled", StringComparison.Ordinal) &&
        renderGateBody.Contains("CanOpenManagedCityDetail(_siteHudSiteId)", StringComparison.Ordinal),
        "legacy site unit placement rendering should remain available for battle modes but be suppressed for player managed-city peacetime");
    AssertTrue(
        bindBody.Contains("EnsureLegacySiteGarrisonPlacementsForPresentation(site, definition)", StringComparison.Ordinal) &&
        garrisonGateBody.Contains("CanOpenManagedCityDetail(_siteHudSiteId)", StringComparison.Ordinal),
        "managed city panel binding should not regenerate enemy legacy garrison placements from WorldSiteState before rendering Strategic Management city UI");
}

internal static void WorldSiteManagementHudUsesTabbedOperationLayout()
{
    string root = ProjectRoot();
    string scenePath = Path.Combine(root, "scenes", "world", "ui", "WorldSitePeacetimeHud.tscn");
    string refsPath = Path.Combine(root, "src", "Presentation", "World", "Sites", "WorldSitePeacetimeHudNodeRefs.cs");
    string binderPath = Path.Combine(root, "src", "Presentation", "World", "Sites", "StrategicManagementDashboardPanelBinder.cs");
    string siteHudPath = Path.Combine(root, "src", "Presentation", "World", "Sites", "WorldSiteRoot.SiteManagementHud.cs");
    string animationPath = Path.Combine(root, "src", "Presentation", "World", "Sites", "SiteManagementDrawerAnimator.cs");
    string resourceBarAnimatorPath = Path.Combine(root, "src", "Presentation", "World", "Sites", "SiteManagementResourceBarAnimator.cs");
    string sheetStylePath = Path.Combine(root, "resource", "ui", "themes", "game-ui-skin", "basic_ui_1_panel_sheet.tres");
    string tabStylePath = Path.Combine(root, "resource", "ui", "themes", "game-ui-skin", "site_management_tab_normal.tres");

    AssertTrue(File.Exists(scenePath), $"site management HUD scene should exist path={scenePath}");
    AssertTrue(File.Exists(sheetStylePath), $"site management HUD should reuse the shared outer sheet StyleBox resource path={sheetStylePath}");
    AssertTrue(File.Exists(tabStylePath), $"site management tab rail should use a dedicated ManaSoul tab StyleBox path={tabStylePath}");

    string scene = File.ReadAllText(scenePath);
    string refsSource = File.ReadAllText(refsPath);
    string binderSource = File.ReadAllText(binderPath);
    string siteHudSource = File.ReadAllText(siteHudPath);
    string animationSource = File.Exists(animationPath) ? File.ReadAllText(animationPath) : "";
    string resourceBarAnimatorSource = File.Exists(resourceBarAnimatorPath) ? File.ReadAllText(resourceBarAnimatorPath) : "";
    string sheetStyle = File.ReadAllText(sheetStylePath);
    string tabStyle = File.Exists(tabStylePath) ? File.ReadAllText(tabStylePath) : "";

    AssertTrue(
        sheetStyle.Contains("assets/textures/ui/tinyrpg_manasoulgui_v_1_0/20250420manaSoul9SlicesA-Sheet.png", StringComparison.Ordinal),
        "site management sheet StyleBox should use the shared ManaSoul outer panel texture");
    AssertTrue(
        scene.Contains("basic_ui_1_panel_sheet.tres", StringComparison.Ordinal) &&
        ExtractSceneNodeBlock(scene, "[node name=\"SitePeacetimePanel\"").Contains("theme_override_styles/panel = ExtResource(\"3_panel_sheet\")", StringComparison.Ordinal),
        "site management outer frame should use the shared ManaSoul sheet StyleBox resource");
    string peacetimePanelBlock = ExtractSceneNodeBlock(scene, "[node name=\"SitePeacetimePanel\"");
    AssertTrue(
        peacetimePanelBlock.Contains("[node name=\"SitePeacetimePanel\" type=\"PanelContainer\" parent=\"OverlayHost\"]", StringComparison.Ordinal) &&
        peacetimePanelBlock.Contains("anchor_left = 0.0", StringComparison.Ordinal) &&
        peacetimePanelBlock.Contains("anchor_top = 0.0", StringComparison.Ordinal) &&
        peacetimePanelBlock.Contains("anchor_right = 0.0", StringComparison.Ordinal) &&
        peacetimePanelBlock.Contains("anchor_bottom = 0.0", StringComparison.Ordinal) &&
        peacetimePanelBlock.Contains("offset_left = 0.0", StringComparison.Ordinal) &&
        peacetimePanelBlock.Contains("offset_top = 72.0", StringComparison.Ordinal) &&
        peacetimePanelBlock.Contains("offset_right = 640.0", StringComparison.Ordinal) &&
        peacetimePanelBlock.Contains("offset_bottom = 840.0", StringComparison.Ordinal) &&
        !siteHudSource.Contains("_sitePeacetimePanel.OffsetRight = 520.0f", StringComparison.Ordinal) &&
        !siteHudSource.Contains("_sitePeacetimePanel.CustomMinimumSize = new Vector2(520.0f", StringComparison.Ordinal),
        "site management panel should be a task-sized overlay and must not hardcode the old 520px split-screen strip");

    AssertTrue(
        !scene.Contains("[node name=\"SiteTopBar\"", StringComparison.Ordinal) &&
        !scene.Contains("SiteOperationHintLabel", StringComparison.Ordinal),
        "site management should not keep a top header strip or a right-side resource panel over the map");
    string topLeftStatusBlock = ExtractSceneNodeBlock(scene, "[node name=\"TopLeftStatus\"");
    string tabRailBlock = ExtractSceneNodeBlock(scene, "[node name=\"SiteManagementTabRail\"");
    AssertTrue(
        scene.Contains("[node name=\"SiteManagementTabRail\" type=\"Control\" parent=\"OverlayHost\"]", StringComparison.Ordinal) &&
        tabRailBlock.Contains("offset_left = 0.0", StringComparison.Ordinal) &&
        tabRailBlock.Contains("offset_right = 96.0", StringComparison.Ordinal) &&
        tabRailBlock.Contains("custom_minimum_size = Vector2(96, 208)", StringComparison.Ordinal) &&
        refsSource.Contains("OverlayHost/SiteManagementTabRail", StringComparison.Ordinal) &&
        !scene.Contains("[node name=\"SiteResourceLabel\" type=\"Label\" parent=\"OverlayHost/SitePeacetimePanel", StringComparison.Ordinal) &&
        !refsSource.Contains("OverlayHost/SitePeacetimePanel/Margin/SiteManagementStack/SiteManagementHeader/SiteResourceLabel", StringComparison.Ordinal),
        "the tab rail should remain a separate left-edge entry surface and resources must not live inside the opened function panel");

    AssertTrue(
        scene.Contains("[node name=\"TopLeftStatus\" type=\"PanelContainer\" parent=\"TopBarHost\"]", StringComparison.Ordinal) &&
        topLeftStatusBlock.Contains("offset_left = 12.0", StringComparison.Ordinal) &&
        topLeftStatusBlock.Contains("offset_top = 10.0", StringComparison.Ordinal) &&
        topLeftStatusBlock.Contains("mouse_filter = 2", StringComparison.Ordinal) &&
        topLeftStatusBlock.Contains("theme_type_variation = &\"WorldTopStatusPanel\"", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"SiteResourceLabel\" type=\"Label\" parent=\"TopBarHost/TopLeftStatus/Margin\"]", StringComparison.Ordinal) &&
        refsSource.Contains("SiteResourceBar = Get<Control>(root, \"TopBarHost/TopLeftStatus\"", StringComparison.Ordinal) &&
        refsSource.Contains("SiteResourceLabel = Get<Label>(root, \"TopBarHost/TopLeftStatus/Margin/SiteResourceLabel\"", StringComparison.Ordinal) &&
        binderSource.Contains("_resourceLabel.Text = BuildResourceLine(safeDashboard.Resources)", StringComparison.Ordinal),
        "site-management resources should use a persistent top-left bar, share the strategic-world placement pattern, and keep binding from Strategic Management resources");

    AssertTrue(
        File.Exists(resourceBarAnimatorPath) &&
        resourceBarAnimatorSource.Contains("internal sealed class SiteManagementResourceBarAnimator", StringComparison.Ordinal) &&
        siteHudSource.Contains("_siteResourceBarAnimator.Bind(hudRefs.SiteResourceBar)", StringComparison.Ordinal) &&
        siteHudSource.Contains("UpdateSiteResourceBarVisibility(", StringComparison.Ordinal) &&
        siteHudSource.Contains("_siteResourceBarAnimator.Update(", StringComparison.Ordinal) &&
        siteHudSource.Contains("placementActive || _battlePreparationHudRetreated", StringComparison.Ordinal) &&
        resourceBarAnimatorSource.Contains("new Vector2(0.0f, -70.0f)", StringComparison.Ordinal) &&
        resourceBarAnimatorSource.Contains("TweenProperty(_resourceBar, \"position\"", StringComparison.Ordinal) &&
        resourceBarAnimatorSource.Contains("TweenProperty(_resourceBar, \"modulate\"", StringComparison.Ordinal) &&
        siteHudSource.Contains("UpdateSiteResourceBarVisibility(\"battle_preparation_hud_retreat", StringComparison.Ordinal),
        "site-management resource bar should stay resident by default and delegate smooth upward retreat to a focused helper during building placement and defensive deployment placement");

    AssertTrue(
        tabStyle.Contains("[gd_resource type=\"StyleBoxTexture\"", StringComparison.Ordinal) &&
        tabStyle.Contains("assets/textures/ui/tinyrpg_manasoulgui_v_1_0/20250420manaTabD-Sheet.png", StringComparison.Ordinal) &&
        tabStyle.Contains("region = Rect2(10, 0, 86, 32)", StringComparison.Ordinal),
        "site management tab rail should trim the ManaSoul tab frame's transparent left edge so the visible tab sits on the screen edge");

    foreach (string[] tab in new[]
    {
        new[] { "BuildTabButton", "建造", "22_build_tab_icon" },
        new[] { "RecruitTabButton", "招兵", "24_recruit_tab_icon" },
        new[] { "OverviewTabButton", "总览", "25_overview_tab_icon" },
        new[] { "ReturnMapTabButton", "返回", "26_return_tab_icon" }
    })
    {
        string tabName = tab[0];
        string tabText = tab[1];
        string iconId = tab[2];
        string tabBlock = ExtractSceneNodeBlock(scene, $"[node name=\"{tabName}\"");
        AssertTrue(
            !string.IsNullOrWhiteSpace(tabBlock) &&
            tabBlock.Contains($"[node name=\"{tabName}\" type=\"Button\" parent=\"OverlayHost/SiteManagementTabRail\"]", StringComparison.Ordinal) &&
            tabBlock.Contains("custom_minimum_size = Vector2(96, 44)", StringComparison.Ordinal) &&
            tabBlock.Contains("layout_mode = 1", StringComparison.Ordinal) &&
            tabBlock.Contains("offset_left = -52.0", StringComparison.Ordinal) &&
            tabBlock.Contains("offset_right = 44.0", StringComparison.Ordinal) &&
            tabBlock.Contains("text = \"\"", StringComparison.Ordinal) &&
            tabBlock.Contains("tooltip_text = \"\"", StringComparison.Ordinal) &&
            tabBlock.Contains($"icon = ExtResource(\"{iconId}\")", StringComparison.Ordinal) &&
            tabBlock.Contains("icon_alignment = 2", StringComparison.Ordinal) &&
            tabBlock.Contains("theme_override_constants/icon_max_width = 22", StringComparison.Ordinal) &&
            tabBlock.Contains("theme_type_variation = &\"WorldSiteRailTabButton\"", StringComparison.Ordinal) &&
            tabBlock.Contains("toggle_mode = true", StringComparison.Ordinal) &&
            siteHudSource.Contains($"WireSiteManagementTabHover({_siteManagementTabField(tabName)}, \"{tabText}\")", StringComparison.Ordinal),
            $"site management tab should be a left-edge drawer tab with no external tooltip, a readable right-side icon, and Chinese hover text tab={tabName}");
    }

    AssertTrue(
        scene.Contains("UI_TravelBook_IconGear01a.png", StringComparison.Ordinal) &&
        scene.Contains("training_ground_icon.tres", StringComparison.Ordinal) &&
        scene.Contains("UI_TravelBook_IconHome01a.png", StringComparison.Ordinal) &&
        scene.Contains("UI_TravelBook_IconArrow01a.png", StringComparison.Ordinal) &&
        animationSource.Contains("ApplyTabDrawerState", StringComparison.Ordinal) &&
        animationSource.Contains("const float collapsedLeft = -52.0f", StringComparison.Ordinal) &&
        animationSource.Contains("const float collapsedRight = 44.0f", StringComparison.Ordinal) &&
        animationSource.Contains("const float expandedLeft = 0.0f", StringComparison.Ordinal) &&
        animationSource.Contains("const float expandedRight = 96.0f", StringComparison.Ordinal) &&
        animationSource.Contains("tab.TooltipText = \"\"", StringComparison.Ordinal) &&
        animationSource.Contains("tab.IconAlignment = HorizontalAlignment.Right", StringComparison.Ordinal) &&
        animationSource.Contains("expanded ? HorizontalAlignment.Left : HorizontalAlignment.Center", StringComparison.Ordinal),
        "site management tab hover should slide each drawer from icon-only side peek to full left-edge label with the icon on the right and no tooltip popup");

    AssertTrue(
        scene.Contains("[node name=\"ReturnMapTabButton\" type=\"Button\" parent=\"OverlayHost/SiteManagementTabRail\"]", StringComparison.Ordinal) &&
        refsSource.Contains("ReturnMapButton = Get<Button>(root, \"OverlayHost/SiteManagementTabRail/ReturnMapTabButton\"", StringComparison.Ordinal) &&
        siteHudSource.Contains("_returnMapButton.Pressed += () => ReturnToReturnScene(_siteHudReturnScenePath)", StringComparison.Ordinal) &&
        !scene.Contains("[node name=\"ReturnMapButton\" type=\"Button\" parent=\"OverlayHost/SitePeacetimePanel/Margin/SiteManagementStack/SiteManagementHeader\"]", StringComparison.Ordinal),
        "return-to-world should live as an outside left-edge tab instead of a button inside the opened management panel");

    AssertTrue(
        scene.Contains("[node name=\"SiteManagementHeader\" type=\"HBoxContainer\" parent=\"OverlayHost/SitePeacetimePanel/Margin/SiteManagementStack\"]", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"SiteHeaderSpacer\" type=\"Control\" parent=\"OverlayHost/SitePeacetimePanel/Margin/SiteManagementStack/SiteManagementHeader\"]", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"SitePanelCloseButton\" type=\"Button\" parent=\"OverlayHost/SitePeacetimePanel/Margin/SiteManagementStack/SiteManagementHeader\"]", StringComparison.Ordinal) &&
        !scene.Contains("[node name=\"SiteResourceLabel\" type=\"Label\" parent=\"OverlayHost/SitePeacetimePanel/Margin/SiteManagementStack/SiteManagementHeader\"]", StringComparison.Ordinal) &&
        !scene.Contains("[node name=\"ReturnMapButton\" type=\"Button\" parent=\"OverlayHost/SitePeacetimePanel/Margin/SiteManagementStack/SiteManagementHeader\"]", StringComparison.Ordinal),
        "opened site-management panel header should not own resources and should reserve the top-right slot for close");

    AssertTrue(
        File.Exists(animationPath) &&
        siteHudSource.Contains("OpenSiteManagementSectionWithBounce(") &&
        animationSource.Contains("ApplyTabDrawerState(button, expandedText, expanded: false, animated: true)") &&
        animationSource.Contains("BouncePanelIn(") &&
        animationSource.Contains("const float panelHiddenLeft = -640.0f") &&
        animationSource.Contains("const float panelOvershootLeft = 18.0f") &&
        animationSource.Contains("TweenProperty(panel, \"offset_left\", panelOvershootLeft") &&
        animationSource.Contains("TweenProperty(panel, \"offset_left\", panelFinalLeft"),
        "opening a management tab should first retract the tab then bounce the panel in from the left with a small overshoot");

    AssertTrue(
        siteHudSource.Contains("CloseSiteManagementPanelWithBounce(") &&
        animationSource.Contains("ClosePanelThenShowRail(") &&
        animationSource.Contains("const float panelCloseOvershootLeft = 18.0f") &&
        animationSource.Contains("const float panelClosedLeft = -640.0f") &&
        animationSource.Contains("TweenProperty(panel, \"offset_left\", panelCloseOvershootLeft") &&
        animationSource.Contains("TweenProperty(panel, \"offset_left\", panelClosedLeft") &&
        animationSource.Contains("AnimateRailTabsIn(") &&
        animationSource.Contains("const float tabHiddenLeft = -96.0f") &&
        animationSource.Contains("const float tabPopLeft = -44.0f") &&
        animationSource.Contains("TweenProperty(tab, \"offset_left\", tabPopLeft") &&
        animationSource.Contains("TweenProperty(tab, \"offset_left\", collapsedLeft"),
        "closing the management panel should bump right, retract left, then smoothly pop the left-edge tabs back out");

    AssertTrue(
        !scene.Contains("tooltip_text = \"建造建筑\"", StringComparison.Ordinal) &&
        !scene.Contains("tooltip_text = \"城市总览\"", StringComparison.Ordinal) &&
        scene.Contains("text = \"关闭\"", StringComparison.Ordinal) &&
        scene.Contains("tooltip_text = \"关闭面板\"", StringComparison.Ordinal) &&
        !scene.Contains("寤", StringComparison.Ordinal) &&
        !scene.Contains("鎷", StringComparison.Ordinal) &&
        !scene.Contains("鍏", StringComparison.Ordinal) &&
        !scene.Contains("鍩", StringComparison.Ordinal),
        "site management authored Chinese labels should stay readable");

    foreach (string required in new[]
    {
        "SiteManagementTabRail",
        "ManagementContentScroll",
        "SiteBuildSection",
        "SiteOverviewSection"
    })
    {
        AssertTrue(scene.Contains(required, StringComparison.Ordinal), $"tabbed site management scene should contain {required}");
        AssertTrue(refsSource.Contains(required, StringComparison.Ordinal), $"node refs should bind tabbed site management node {required}");
    }

    AssertTrue(
        !scene.Contains("CorpsTabButton", StringComparison.Ordinal) &&
        !scene.Contains("SiteCorpsSection", StringComparison.Ordinal) &&
        !refsSource.Contains("CorpsTabButton", StringComparison.Ordinal) &&
        !refsSource.Contains("SiteCorpsSection", StringComparison.Ordinal),
        "first-version site management should remove the obsolete corps tab and section; recruitment workbench owns hero main-corps reassignment");
    AssertTrue(
        siteHudSource.Contains("OpenSiteManagementSectionWithBounce(") &&
        siteHudSource.Contains("UpdateSiteManagementEntryVisibility(") &&
        siteHudSource.Contains("_siteManagementTabRail"),
        "WorldSiteRoot should open one site-management overlay function at a time and return to the tab rail");
    AssertTrue(
        !binderSource.Contains("_recruitList", StringComparison.Ordinal) &&
        !binderSource.Contains("_corpsList", StringComparison.Ordinal) &&
        !binderSource.Contains("BindCorpsAndHeroes(", StringComparison.Ordinal) &&
        !binderSource.Contains("GameUiSceneFactory.CreateWorldMusterOptionCard", StringComparison.Ordinal),
        "strategic management binder should keep recruitment and corps inventory out of the narrow site-management sidebar");
    AssertTrue(
        !binderSource.Contains("Conscription", StringComparison.Ordinal),
        "strategic management binder should not retain a dedicated conscription page");
}

internal static void WorldSiteOmitsDedicatedConscriptionSurface()
{
    string root = ProjectRoot();
    string siteRootDir = Path.Combine(root, "src", "Presentation", "World", "Sites");
    string scene = File.ReadAllText(Path.Combine(root, "scenes", "world", "ui", "WorldSitePeacetimeHud.tscn"));
    string refsSource = File.ReadAllText(Path.Combine(siteRootDir, "WorldSitePeacetimeHudNodeRefs.cs"));
    string rootSource = ReadWorldSiteRootSource();
    string binderSource = File.ReadAllText(Path.Combine(siteRootDir, "StrategicManagementDashboardPanelBinder.cs"));
    string factorySource = File.ReadAllText(Path.Combine(root, "src", "Presentation", "Common", "GameUiSceneFactory.cs"));

    foreach (string retiredFragment in new[]
    {
        "ConscriptionTabButton",
        "SiteConscriptionSection",
        "WorldConscriptionPanel",
        "ManualConscriptRequested",
        "AutoConscriptionIntensityRequested",
        "ManualConscriptReserveForces(",
        "SetAutoConscriptionIntensity("
    })
    {
        AssertTrue(
            !scene.Contains(retiredFragment, StringComparison.Ordinal) &&
            !refsSource.Contains(retiredFragment, StringComparison.Ordinal) &&
            !rootSource.Contains(retiredFragment, StringComparison.Ordinal) &&
            !binderSource.Contains(retiredFragment, StringComparison.Ordinal) &&
            !factorySource.Contains(retiredFragment, StringComparison.Ordinal),
            $"site management should not retain retired conscription fragment={retiredFragment}");
    }

    AssertTrue(
        binderSource.Contains("ReserveRecoveryPerElapsedPulse", StringComparison.Ordinal),
        "existing city summary should display the read-only passive reserve recovery rate");
}

internal static void WorldSiteRecruitmentUsesHeroFirstMilitaryWorkbench()
{
    string root = ProjectRoot();
    string siteRootDir = Path.Combine(root, "src", "Presentation", "World", "Sites");
    string scenePath = Path.Combine(root, "scenes", "world", "ui", "WorldSitePeacetimeHud.tscn");
    string heroCardScenePath = Path.Combine(root, "scenes", "world", "ui", "WorldMilitaryWorkbenchHeroCard.tscn");
    string heroCardSourcePath = Path.Combine(siteRootDir, "WorldMilitaryHeroCard.cs");
    string musterCardScenePath = Path.Combine(root, "scenes", "world", "ui", "WorldMusterOptionCard.tscn");
    string musterCardSourcePath = Path.Combine(siteRootDir, "WorldMusterOptionCard.cs");
    string animatedPreviewScenePath = Path.Combine(root, "scenes", "ui", "common", "BattleUnitAnimatedPreview.tscn");
    string animatedPreviewSourcePath = Path.Combine(root, "src", "Presentation", "Common", "BattleUnitAnimatedPreview.cs");
    string plinthPreviewScenePath = Path.Combine(root, "scenes", "ui", "common", "BattleUnitPlinthPreview.tscn");
    string plinthPreviewSourcePath = Path.Combine(root, "src", "Presentation", "Common", "BattleUnitPlinthPreview.cs");
    string musterTooltipScenePath = Path.Combine(root, "scenes", "world", "ui", "WorldMusterOptionTooltip.tscn");
    string musterTooltipSourcePath = Path.Combine(siteRootDir, "WorldMusterOptionTooltip.cs");
    string workbenchBinderPath = Path.Combine(siteRootDir, "StrategicMilitaryWorkbenchBinder.cs");
    string centeredModalAnimatorPath = Path.Combine(siteRootDir, "SiteManagementCenteredModalAnimator.cs");
    string resourceBarAnimatorPath = Path.Combine(siteRootDir, "SiteManagementResourceBarAnimator.cs");
    string refsPath = Path.Combine(siteRootDir, "WorldSitePeacetimeHudNodeRefs.cs");
    string siteHudPath = Path.Combine(siteRootDir, "WorldSiteRoot.SiteManagementHud.cs");
    string factoryPath = Path.Combine(root, "src", "Presentation", "Common", "GameUiSceneFactory.cs");
    string resolverPath = Path.Combine(root, "src", "Presentation", "Common", "BattleUnitPreviewResolver.cs");
    string dashboardBinderPath = Path.Combine(siteRootDir, "StrategicManagementDashboardPanelBinder.cs");
    string recruitmentThemeResourcePath = "res://resource/ui/themes/recruitment-ui-v1/recruitment_ui_v1_theme.tres";
    string recruitmentDividerResourcePath = "res://assets/textures/ui/recruitment-ui-v1/recruitment_divider_blue.png";
    string recruitmentPlinthResourcePath = "res://assets/textures/ui/recruitment-ui-v1/recruitment_unit_plinth_normal.png";
    string recruitmentSelectedPlinthResourcePath = "res://assets/textures/ui/recruitment-ui-v1/recruitment_unit_plinth_selected.png";

    AssertTrue(File.Exists(scenePath), $"world-site peacetime HUD scene should exist path={scenePath}");
    AssertTrue(File.Exists(heroCardScenePath), $"military workbench hero card should be an authored reusable scene path={heroCardScenePath}");
    AssertTrue(File.Exists(heroCardSourcePath), $"military workbench hero card should own its binding script path={heroCardSourcePath}");
    AssertTrue(File.Exists(musterCardScenePath), $"military workbench muster card should be an authored reusable scene path={musterCardScenePath}");
    AssertTrue(File.Exists(musterCardSourcePath), $"military workbench muster card should own its binding script path={musterCardSourcePath}");
    AssertTrue(File.Exists(animatedPreviewScenePath), $"battle-unit animated preview should be an authored common UI scene path={animatedPreviewScenePath}");
    AssertTrue(File.Exists(animatedPreviewSourcePath), $"battle-unit animated preview should own its binding script path={animatedPreviewSourcePath}");
    AssertTrue(File.Exists(plinthPreviewScenePath), $"battle-unit plinth preview should be an authored common UI scene path={plinthPreviewScenePath}");
    AssertTrue(File.Exists(plinthPreviewSourcePath), $"battle-unit plinth preview should own its binding script path={plinthPreviewSourcePath}");
    AssertTrue(File.Exists(musterTooltipScenePath), $"military muster hover detail should be an authored tooltip scene path={musterTooltipScenePath}");
    AssertTrue(File.Exists(musterTooltipSourcePath), $"military muster hover detail should own its binding script path={musterTooltipSourcePath}");
    AssertTrue(File.Exists(workbenchBinderPath), $"military workbench binding should live in a focused Presentation collaborator path={workbenchBinderPath}");
    AssertTrue(File.Exists(centeredModalAnimatorPath), $"centered recruitment modal animation should live in a focused Presentation collaborator path={centeredModalAnimatorPath}");
    AssertTrue(File.Exists(resourceBarAnimatorPath), $"site resource bar overlay behavior should live in a focused Presentation collaborator path={resourceBarAnimatorPath}");
    AssertTrue(File.Exists(resolverPath), $"battle unit idle-frame previews should be resolved by a shared Presentation helper path={resolverPath}");

    string scene = File.ReadAllText(scenePath);
    string heroCardScene = File.Exists(heroCardScenePath) ? File.ReadAllText(heroCardScenePath) : "";
    string heroCardSource = File.Exists(heroCardSourcePath) ? File.ReadAllText(heroCardSourcePath) : "";
    string musterCardScene = File.Exists(musterCardScenePath) ? File.ReadAllText(musterCardScenePath) : "";
    string musterCardSource = File.Exists(musterCardSourcePath) ? File.ReadAllText(musterCardSourcePath) : "";
    string animatedPreviewScene = File.Exists(animatedPreviewScenePath) ? File.ReadAllText(animatedPreviewScenePath) : "";
    string animatedPreviewSource = File.Exists(animatedPreviewSourcePath) ? File.ReadAllText(animatedPreviewSourcePath) : "";
    string plinthPreviewScene = File.Exists(plinthPreviewScenePath) ? File.ReadAllText(plinthPreviewScenePath) : "";
    string plinthPreviewSource = File.Exists(plinthPreviewSourcePath) ? File.ReadAllText(plinthPreviewSourcePath) : "";
    string musterTooltipScene = File.Exists(musterTooltipScenePath) ? File.ReadAllText(musterTooltipScenePath) : "";
    string musterTooltipSource = File.Exists(musterTooltipSourcePath) ? File.ReadAllText(musterTooltipSourcePath) : "";
    string workbenchBinderSource = File.Exists(workbenchBinderPath) ? File.ReadAllText(workbenchBinderPath) : "";
    string centeredModalAnimatorSource = File.Exists(centeredModalAnimatorPath) ? File.ReadAllText(centeredModalAnimatorPath) : "";
    string resourceBarAnimatorSource = File.Exists(resourceBarAnimatorPath) ? File.ReadAllText(resourceBarAnimatorPath) : "";
    string refsSource = File.ReadAllText(refsPath);
    string siteHudSource = File.ReadAllText(siteHudPath);
    string factorySource = File.ReadAllText(factoryPath);
    string resolverSource = File.Exists(resolverPath) ? File.ReadAllText(resolverPath) : "";
    string dashboardBinderSource = File.ReadAllText(dashboardBinderPath);
    string workbenchPanelBlock = ExtractSceneNodeBlock(scene, "[node name=\"MilitaryWorkbenchPanel\"");
    string workbenchMarginBlock = ExtractSceneNodeBlock(scene, "[node name=\"WorkbenchMargin\"");
    string workbenchStackBlock = ExtractSceneNodeBlock(scene, "[node name=\"WorkbenchStack\"");
    string workbenchBackdropBlock = ExtractSceneNodeBlock(scene, "[node name=\"MilitaryWorkbenchBackdrop\"");
    string militaryCloseButtonBlock = ExtractSceneNodeBlock(scene, "[node name=\"MilitaryCloseButton\"");
    string militaryHeroScrollBlock = ExtractSceneNodeBlock(scene, "[node name=\"MilitaryHeroScroll\"");
    string militaryHeaderDividerBlock = ExtractSceneNodeBlock(scene, "[node name=\"MilitaryHeaderDivider\"");
    string militaryMusterDividerBlock = ExtractSceneNodeBlock(scene, "[node name=\"MilitaryMusterDivider\"");
    string militaryBodyBlock = ExtractSceneNodeBlock(scene, "[node name=\"MilitaryBody\"");
    string selectedHeroMarginBlock = ExtractSceneNodeBlock(scene, "[node name=\"SelectedHeroMargin\"");
    string selectedHeroRowBlock = ExtractSceneNodeBlock(scene, "[node name=\"SelectedHeroRow\"");
    string selectedHeroAvatarFrameBlock = ExtractSceneNodeBlock(scene, "[node name=\"SelectedHeroAvatarFrame\"");
    string selectedHeroPanelBlock = ExtractSceneNodeBlock(scene, "[node name=\"SelectedHeroPanel\"");
    string selectedHeroPlinthBlock = ExtractSceneNodeBlock(scene, "[node name=\"SelectedHeroPlinthPreview\"");
    string militaryMusterScrollBlock = ExtractSceneNodeBlock(scene, "[node name=\"MilitaryMusterScroll\"");
    string musterGridBlock = ExtractSceneNodeBlock(scene, "[node name=\"MilitaryMusterGrid\"");
    string heroCardRootBlock = ExtractSceneNodeBlock(heroCardScene, "[node name=\"WorldMilitaryHeroCard\"");
    string heroCardPlinthBlock = ExtractSceneNodeBlock(heroCardScene, "[node name=\"PlinthPreview\"");
    string musterCardRootBlock = ExtractSceneNodeBlock(musterCardScene, "[node name=\"WorldMusterOptionCard\"");
    string musterCardPlinthBlock = ExtractSceneNodeBlock(musterCardScene, "[node name=\"PlinthPreview\"");
    string musterCardNameplateBlock = ExtractSceneNodeBlock(musterCardScene, "[node name=\"Nameplate\"");
    string musterApplyBody = ExtractMethodBody(musterCardSource, "private void ApplyBinding()");
    string musterCustomTooltipBody = ExtractMethodBody(musterCardSource, "public override Control _MakeCustomTooltip(");
    string musterPressedBody = ExtractMethodBody(musterCardSource, "private void OnPressed()");

    foreach (string requiredNode in new[]
    {
        "MilitaryWorkbenchBackdrop",
        "MilitaryWorkbenchPanel",
        "MilitaryHeroList",
        "MilitaryMusterGrid",
        "MilitaryHeroSummaryLabel",
        "MilitaryNoticeLabel",
        "MilitaryCloseButton"
    })
    {
        AssertTrue(scene.Contains(requiredNode, StringComparison.Ordinal), $"military workbench scene should author node={requiredNode}");
        AssertTrue(refsSource.Contains(requiredNode, StringComparison.Ordinal), $"military workbench node refs should bind node={requiredNode}");
    }

    AssertTrue(
        !scene.Contains("SiteRecruitList", StringComparison.Ordinal) &&
        !dashboardBinderSource.Contains("GameUiSceneFactory.CreateWorldMusterOptionCard", StringComparison.Ordinal) &&
        !scene.Contains("CorpsTabButton", StringComparison.Ordinal) &&
        !scene.Contains("SiteCorpsSection", StringComparison.Ordinal) &&
        !refsSource.Contains("CorpsTabButton", StringComparison.Ordinal) &&
        !refsSource.Contains("SiteCorpsSection", StringComparison.Ordinal),
        "recruitment should not render corps cards inside the narrow left sidebar and the obsolete corps tab should be removed");
    AssertTrue(
        !scene.Contains("MilitaryBackButton", StringComparison.Ordinal) &&
        !refsSource.Contains("MilitaryBackButton", StringComparison.Ordinal) &&
        !siteHudSource.Contains("_militaryBackButton", StringComparison.Ordinal) &&
        !workbenchBinderSource.Contains("MilitaryBackButton", StringComparison.Ordinal),
        "recruitment workbench should not keep a secondary back button; the modal header should expose only the close action");
    AssertTrue(
        scene.Contains(recruitmentThemeResourcePath, StringComparison.Ordinal) &&
        scene.Contains(recruitmentDividerResourcePath, StringComparison.Ordinal) &&
        workbenchPanelBlock.Contains("custom_minimum_size = Vector2(1500, 900)", StringComparison.Ordinal) &&
        workbenchPanelBlock.Contains("offset_left = -750.0", StringComparison.Ordinal) &&
        workbenchPanelBlock.Contains("offset_top = -450.0", StringComparison.Ordinal) &&
        workbenchPanelBlock.Contains("offset_right = 750.0", StringComparison.Ordinal) &&
        workbenchPanelBlock.Contains("offset_bottom = 450.0", StringComparison.Ordinal) &&
        workbenchPanelBlock.Contains("theme_type_variation = &\"RecruitmentModalPanel\"", StringComparison.Ordinal) &&
        workbenchMarginBlock.Contains("margin_left = 24", StringComparison.Ordinal) &&
        workbenchMarginBlock.Contains("margin_top = 20", StringComparison.Ordinal) &&
        workbenchMarginBlock.Contains("margin_right = 24", StringComparison.Ordinal) &&
        workbenchMarginBlock.Contains("margin_bottom = 20", StringComparison.Ordinal) &&
        workbenchStackBlock.Contains("theme_override_constants/separation = 10", StringComparison.Ordinal) &&
        militaryBodyBlock.Contains("theme_override_constants/separation = 14", StringComparison.Ordinal) &&
        militaryHeroScrollBlock.Contains("custom_minimum_size = Vector2(384, 0)", StringComparison.Ordinal) &&
        !workbenchPanelBlock.Contains("theme_override_styles/panel", StringComparison.Ordinal) &&
        workbenchBackdropBlock.Contains("type=\"ColorRect\"", StringComparison.Ordinal) &&
        workbenchBackdropBlock.Contains("visible = false", StringComparison.Ordinal) &&
        workbenchBackdropBlock.Contains("color = Color(0.03, 0.04, 0.08, 0.68)", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"SelectedHeroPlinthPreview\" parent=\"ModalHost/MilitaryWorkbenchPanel/WorkbenchMargin/WorkbenchStack/MilitaryBody/MilitaryDetailStack/SelectedHeroPanel/SelectedHeroMargin/SelectedHeroRow/SelectedHeroAvatarFrame\" instance=", StringComparison.Ordinal),
        "military workbench should use the recruitment modal theme, dim the map behind it, and keep enough room for a hero-first card grid");
    AssertTrue(
        scene.Contains("[node name=\"MilitaryHeaderDivider\" type=\"TextureRect\" parent=\"ModalHost/MilitaryWorkbenchPanel/WorkbenchMargin/WorkbenchStack\"]", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"MilitaryMusterDivider\" type=\"TextureRect\" parent=\"ModalHost/MilitaryWorkbenchPanel/WorkbenchMargin/WorkbenchStack/MilitaryBody/MilitaryDetailStack\"]", StringComparison.Ordinal) &&
        militaryHeaderDividerBlock.Contains("texture = ExtResource(", StringComparison.Ordinal) &&
        militaryMusterDividerBlock.Contains("texture = ExtResource(", StringComparison.Ordinal) &&
        militaryHeaderDividerBlock.Contains("custom_minimum_size = Vector2(1452, 32)", StringComparison.Ordinal) &&
        militaryHeaderDividerBlock.Contains("size_flags_horizontal = 3", StringComparison.Ordinal) &&
        militaryHeaderDividerBlock.Contains("stretch_mode = 0", StringComparison.Ordinal) &&
        militaryMusterDividerBlock.Contains("custom_minimum_size = Vector2(826, 28)", StringComparison.Ordinal) &&
        militaryMusterDividerBlock.Contains("size_flags_horizontal = 4", StringComparison.Ordinal) &&
        militaryMusterDividerBlock.Contains("stretch_mode = 0", StringComparison.Ordinal) &&
        militaryMusterScrollBlock.Contains("custom_minimum_size = Vector2(826, 0)", StringComparison.Ordinal) &&
        militaryMusterScrollBlock.Contains("size_flags_horizontal = 4", StringComparison.Ordinal) &&
        musterGridBlock.Contains("custom_minimum_size = Vector2(826, 0)", StringComparison.Ordinal) &&
        musterGridBlock.Contains("size_flags_horizontal = 4", StringComparison.Ordinal) &&
        militaryHeaderDividerBlock.Contains("mouse_filter = 2", StringComparison.Ordinal) &&
        militaryMusterDividerBlock.Contains("mouse_filter = 2", StringComparison.Ordinal),
        "recruitment workbench should stretch the authored blue divider texture to mark the full workbench range and the exact four-card troop-list range without extra box frames");
    AssertTrue(
        militaryCloseButtonBlock.Contains("theme_type_variation = &\"RecruitmentTextButton\"", StringComparison.Ordinal) &&
        selectedHeroPanelBlock.Contains("theme_type_variation = &\"RecruitmentSelectedCardPanel\"", StringComparison.Ordinal) &&
        selectedHeroPanelBlock.Contains("custom_minimum_size = Vector2(0, 128)", StringComparison.Ordinal) &&
        selectedHeroMarginBlock.Contains("margin_top = 10", StringComparison.Ordinal) &&
        selectedHeroMarginBlock.Contains("margin_bottom = 10", StringComparison.Ordinal) &&
        selectedHeroRowBlock.Contains("theme_override_constants/separation = 16", StringComparison.Ordinal) &&
        selectedHeroAvatarFrameBlock.Contains("custom_minimum_size = Vector2(176, 100)", StringComparison.Ordinal) &&
        scene.Contains(recruitmentSelectedPlinthResourcePath, StringComparison.Ordinal) &&
        selectedHeroPlinthBlock.Contains("PlinthTexture = ExtResource(", StringComparison.Ordinal) &&
        musterGridBlock.Contains("columns = 4", StringComparison.Ordinal),
        "military workbench controls should reuse recruitment button/card variations and present muster cards in a wide four-column panel");
    AssertTrue(
        resolverSource.Contains("BattleUnitPreviewResolver", StringComparison.Ordinal) &&
        animatedPreviewScene.Contains("res://src/Presentation/Common/BattleUnitAnimatedPreview.cs", StringComparison.Ordinal) &&
        animatedPreviewSource.Contains("public partial class BattleUnitAnimatedPreview", StringComparison.Ordinal) &&
        animatedPreviewSource.Contains("BattleUnitAnimatedPreviewLayoutMode", StringComparison.Ordinal) &&
        animatedPreviewSource.Contains("PreviewLayoutMode", StringComparison.Ordinal) &&
        animatedPreviewSource.Contains("ApplyFrameRectLayout", StringComparison.Ordinal) &&
        animatedPreviewSource.Contains("public void Bind(BattleUnitAnimatedPreviewModel preview)", StringComparison.Ordinal) &&
        animatedPreviewSource.Contains("AnimatedSprite2D", StringComparison.Ordinal) &&
        resolverSource.Contains("BattleUnitDefinitionIndexLoader.LoadDefaultPathIndex", StringComparison.Ordinal) &&
        resolverSource.Contains("GD.Load<BattleUnitDefinition>", StringComparison.Ordinal) &&
        resolverSource.Contains("definition.Visual", StringComparison.Ordinal) &&
        resolverSource.Contains("SpriteFrames", StringComparison.Ordinal) &&
        resolverSource.Contains("AnimationSet?.IdleAnimation", StringComparison.Ordinal) &&
        resolverSource.Contains("BattleUnitAnimatedPreviewModel", StringComparison.Ordinal) &&
        resolverSource.Contains("ResolveAnimatedPreview", StringComparison.Ordinal) &&
        resolverSource.Contains("GetFrameTexture(animationName, 0)", StringComparison.Ordinal) &&
        !resolverSource.Contains("AtlasTexture", StringComparison.Ordinal) &&
        !resolverSource.Contains(".png", StringComparison.OrdinalIgnoreCase),
        "shared unit preview resolver should load BattleUnitDefinition visual SpriteFrames, expose animated preview models, and keep first-frame extraction only as a legacy adapter");
    AssertTrue(
        plinthPreviewScene.Contains("res://src/Presentation/Common/BattleUnitPlinthPreview.cs", StringComparison.Ordinal) &&
        plinthPreviewScene.Contains("res://assets/textures/ui/recruitment-ui-v1/recruitment_unit_plinth_normal.png", StringComparison.Ordinal) &&
        plinthPreviewScene.Contains("res://scenes/ui/common/BattleUnitAnimatedPreview.tscn", StringComparison.Ordinal) &&
        plinthPreviewScene.Contains("[node name=\"Plinth\" type=\"Sprite2D\" parent=\".\"]", StringComparison.Ordinal) &&
        plinthPreviewScene.Contains("[node name=\"HeroPreview\" parent=\".\" instance=", StringComparison.Ordinal) &&
        plinthPreviewSource.Contains("public partial class BattleUnitPlinthPreview", StringComparison.Ordinal) &&
        plinthPreviewSource.Contains("public Texture2D PlinthTexture", StringComparison.Ordinal) &&
        plinthPreviewSource.Contains("private static readonly Vector2 PlinthSize = new(176f, 80f);", StringComparison.Ordinal) &&
        plinthPreviewSource.Contains("private static readonly Vector2 HeroOffset = new(0f, -39f);", StringComparison.Ordinal) &&
        plinthPreviewSource.Contains("private static readonly Vector2 HeroMaxSize = new(188f, 130f);", StringComparison.Ordinal) &&
        !plinthPreviewSource.Contains("[Export]\n    public Vector2 PlinthSize", StringComparison.Ordinal) &&
        !plinthPreviewSource.Contains("[Export]\n    public Vector2 HeroOffset", StringComparison.Ordinal) &&
        !plinthPreviewSource.Contains("[Export]\n    public Vector2 HeroMaxSize", StringComparison.Ordinal) &&
        plinthPreviewSource.Contains("public void Bind(BattleUnitAnimatedPreviewModel preview)", StringComparison.Ordinal),
        "shared unit plinth preview should own fixed recruitment plinth and animated hero alignment so UI surfaces only place or scale the whole display");
    AssertTrue(
        heroCardScene.Contains("WorldMilitaryHeroCard.cs", StringComparison.Ordinal) &&
        heroCardScene.Contains("res://scenes/ui/common/BattleUnitPlinthPreview.tscn", StringComparison.Ordinal) &&
        heroCardScene.Contains("[node name=\"PlinthPreview\" parent=\"Content/PreviewSlot\" instance=", StringComparison.Ordinal) &&
        heroCardScene.Contains("position = Vector2(63, 78)", StringComparison.Ordinal) &&
        heroCardScene.Contains("scale = Vector2(0.66, 0.66)", StringComparison.Ordinal) &&
        !heroCardScene.Contains("PlinthSize =", StringComparison.Ordinal) &&
        !heroCardScene.Contains("HeroOffset =", StringComparison.Ordinal) &&
        !heroCardScene.Contains("HeroMaxSize =", StringComparison.Ordinal) &&
        !heroCardScene.Contains("HeroPreviewLayoutMode =", StringComparison.Ordinal) &&
        heroCardScene.Contains(recruitmentThemeResourcePath, StringComparison.Ordinal) &&
        heroCardRootBlock.Contains("theme_type_variation = &\"RecruitmentSelectableCardButton\"", StringComparison.Ordinal) &&
        heroCardScene.Contains(recruitmentPlinthResourcePath, StringComparison.Ordinal) &&
        heroCardPlinthBlock.Contains("PlinthTexture = ExtResource(", StringComparison.Ordinal) &&
        !heroCardScene.Contains("[node name=\"Plinth\" type=\"TextureRect\" parent=\"Content/PreviewSlot\"]", StringComparison.Ordinal) &&
        !heroCardScene.Contains("[node name=\"AnimatedPreview\" parent=\"Content/PreviewSlot\" instance=", StringComparison.Ordinal) &&
        !heroCardScene.Contains("basic_ui_1_panel_slot.tres", StringComparison.Ordinal) &&
        heroCardSource.Contains("WorldMilitaryHeroCard : Button", StringComparison.Ordinal) &&
        heroCardSource.Contains("SelectedEventHandler", StringComparison.Ordinal) &&
        heroCardSource.Contains("BattleUnitAnimatedPreviewModel preview", StringComparison.Ordinal) &&
        heroCardSource.Contains("BattleUnitPlinthPreview", StringComparison.Ordinal) &&
        !heroCardSource.Contains("string iconPath", StringComparison.Ordinal) &&
        !heroCardSource.Contains("_iconPath", StringComparison.Ordinal) &&
        !heroCardSource.Contains("GD.Load<Texture2D>", StringComparison.Ordinal),
        "hero selection should use the recruitment card theme, plinth-backed animated preview, typed selection signal, and externally resolved battle-unit preview model");
    AssertTrue(
        factorySource.Contains("WorldMilitaryWorkbenchHeroCardScenePath", StringComparison.Ordinal) &&
        factorySource.Contains("CreateWorldMilitaryWorkbenchHeroCard", StringComparison.Ordinal),
        "UI scene factory should expose the recruitment-workbench military hero card template");
    AssertTrue(
        workbenchBinderSource.Contains("internal sealed class StrategicMilitaryWorkbenchBinder", StringComparison.Ordinal) &&
        workbenchBinderSource.Contains("BattleUnitPreviewResolver", StringComparison.Ordinal) &&
        workbenchBinderSource.Contains("ResolveAnimatedPreview", StringComparison.Ordinal) &&
        workbenchBinderSource.Contains("CreateWorldMilitaryWorkbenchHeroCard", StringComparison.Ordinal) &&
        workbenchBinderSource.Contains("CreateWorldMusterOptionCard", StringComparison.Ordinal) &&
        workbenchBinderSource.Contains("ResolveSelectedHeroId", StringComparison.Ordinal) &&
        workbenchBinderSource.Contains("hero.BattleUnitId", StringComparison.Ordinal) &&
        workbenchBinderSource.Contains("template.BattleUnitId", StringComparison.Ordinal) &&
        workbenchBinderSource.Contains("template.ReserveForceCost", StringComparison.Ordinal) &&
        workbenchBinderSource.Contains("template.CreationCost", StringComparison.Ordinal) &&
        !workbenchBinderSource.Contains("_panel.Visible = true", StringComparison.Ordinal) &&
        !workbenchBinderSource.Contains("_backdrop.Visible = true", StringComparison.Ordinal) &&
        !workbenchBinderSource.Contains("_panel.Visible = false", StringComparison.Ordinal) &&
        !workbenchBinderSource.Contains("_backdrop.Visible = false", StringComparison.Ordinal) &&
        !workbenchBinderSource.Contains("template.ReserveForceRefund", StringComparison.Ordinal) &&
        !workbenchBinderSource.Contains("template.NetReserveForceCost", StringComparison.Ordinal) &&
        !workbenchBinderSource.Contains("template.RefundCost", StringComparison.Ordinal) &&
        !workbenchBinderSource.Contains("template.NetCost", StringComparison.Ordinal) &&
        !workbenchBinderSource.Contains("refundText", StringComparison.Ordinal) &&
        !workbenchBinderSource.Contains("netText", StringComparison.Ordinal) &&
        !workbenchBinderSource.Contains("hero.IconPath", StringComparison.Ordinal) &&
        !workbenchBinderSource.Contains("template.IconPath", StringComparison.Ordinal) &&
        !workbenchBinderSource.Contains("GD.Load<Texture2D>(hero.IconPath)", StringComparison.Ordinal) &&
        workbenchBinderSource.Contains("BindHeroSelectionStep", StringComparison.Ordinal) &&
        workbenchBinderSource.Contains("BindCorpsAdjustmentStep", StringComparison.Ordinal),
        "military workbench binder should own hero-first content binding without directly showing or hiding the centered modal");
    AssertTrue(
        centeredModalAnimatorSource.Contains("internal static class SiteManagementCenteredModalAnimator", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("OpenCenteredModalAfterDelay(", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("OpenCenteredModal(", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("CloseCenteredModal(", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("ModalLeftEdgeNudgePixels", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("ResolveLeftEdgeStartPosition(restPosition, panel)", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("ModalPointScale", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("ModalOpenOvershootScale", StringComparison.Ordinal) &&
        !centeredModalAnimatorSource.Contains("ModalClosedScale", StringComparison.Ordinal) &&
        !centeredModalAnimatorSource.Contains("ModalCloseBumpPixels", StringComparison.Ordinal) &&
        !centeredModalAnimatorSource.Contains("ModalCloseBumpScale", StringComparison.Ordinal) &&
        !centeredModalAnimatorSource.Contains("ColorRect", StringComparison.Ordinal) &&
        !centeredModalAnimatorSource.Contains("ModalTrail", StringComparison.Ordinal) &&
        !centeredModalAnimatorSource.Contains("CreateModalTrailAfterimages", StringComparison.Ordinal) &&
        !centeredModalAnimatorSource.Contains("AnimateModalTrailAfterimages", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("new Vector2(ModalPointScale, ModalPointScale)", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("private const float ModalOpenOvershootViewportRatio = 0.60f;", StringComparison.Ordinal) &&
        !centeredModalAnimatorSource.Contains("ModalOpenCenterOvershootRatio", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("private const float ModalCenterArrivalScale = 0.54f;", StringComparison.Ordinal) &&
        !centeredModalAnimatorSource.Contains("ModalCenterReturnScale", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("private const float ModalUiLaunchOpacity = 0.24f;", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("private const float ModalUiCenterOpacity = 0.72f;", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("private const double TabRetractDelaySeconds = 0.10;", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("private const double ModalOpenBackdropFadeSeconds = 0.135;", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("private const double ModalUiLaunchSeconds = 0.27;", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("private const double ModalOpenReturnSeconds = 0.07;", StringComparison.Ordinal) &&
        !centeredModalAnimatorSource.Contains("ModalOpenOverscaleSeconds", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("private const double ModalOpenSettleSeconds = 0.045;", StringComparison.Ordinal) &&
        !centeredModalAnimatorSource.Contains("ModalCloseBumpSeconds", StringComparison.Ordinal) &&
        !centeredModalAnimatorSource.Contains("ModalCloseRetractSeconds", StringComparison.Ordinal) &&
        !centeredModalAnimatorSource.Contains("ModalClosePanelFadeSeconds", StringComparison.Ordinal) &&
        !centeredModalAnimatorSource.Contains("ModalCloseBackdropFadeSeconds", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("panel.Modulate = new Color(1.0f, 1.0f, 1.0f, ModalUiLaunchOpacity)", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("Vector2 centerOvershootPosition = ResolveCenterOvershootPosition(startPosition, restPosition, panel);", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("panel.GetParent() is Control parent", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("parentSize.X * ModalOpenOvershootViewportRatio", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("TweenProperty(panel, \"position\", centerOvershootPosition, ModalUiLaunchSeconds).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out)", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("TweenProperty(panel, \"scale\", new Vector2(ModalCenterArrivalScale, ModalCenterArrivalScale), ModalUiLaunchSeconds).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In)", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("TweenProperty(panel, \"modulate\", new Color(1.0f, 1.0f, 1.0f, ModalUiCenterOpacity), ModalUiLaunchSeconds)", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("TweenProperty(panel, \"position\", restPosition, ModalOpenReturnSeconds)", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("TweenProperty(panel, \"scale\", new Vector2(ModalOpenOvershootScale, ModalOpenOvershootScale), ModalOpenReturnSeconds)", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("TweenProperty(panel, \"modulate\", Opaque, ModalOpenReturnSeconds)", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("TweenProperty(panel, \"position\", restPosition, ModalOpenSettleSeconds)", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("TweenProperty(panel, \"scale\", new Vector2(ModalOpenOvershootScale, ModalOpenOvershootScale), ModalOpenSettleSeconds).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In)", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("TweenProperty(panel, \"position\", centerOvershootPosition, ModalOpenReturnSeconds).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In)", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("TweenProperty(panel, \"scale\", new Vector2(ModalCenterArrivalScale, ModalCenterArrivalScale), ModalOpenReturnSeconds)", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("TweenProperty(panel, \"modulate\", new Color(1.0f, 1.0f, 1.0f, ModalUiCenterOpacity), ModalOpenReturnSeconds)", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("TweenProperty(panel, \"position\", startPosition, ModalUiLaunchSeconds).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In)", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("TweenProperty(panel, \"scale\", new Vector2(ModalPointScale, ModalPointScale), ModalUiLaunchSeconds)", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("TweenProperty(panel, \"modulate\", Transparent, ModalUiLaunchSeconds)", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("TweenProperty(backdrop, \"modulate\", Transparent, ModalOpenBackdropFadeSeconds)", StringComparison.Ordinal) &&
        !centeredModalAnimatorSource.Contains("ModalUiLaunchStretchScaleX", StringComparison.Ordinal) &&
        !centeredModalAnimatorSource.Contains("ModalUiLaunchStretchScaleY", StringComparison.Ordinal) &&
        !centeredModalAnimatorSource.Contains("ModalOpenOvershootPixels", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("TweenProperty(panel, \"position\"", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("TweenProperty(panel, \"scale\"", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("TweenProperty(panel, \"modulate\"", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("TweenProperty(backdrop, \"modulate\"", StringComparison.Ordinal) &&
        siteHudSource.Contains("SiteManagementCenteredModalAnimator.OpenCenteredModalAfterDelay(", StringComparison.Ordinal) &&
        siteHudSource.Contains("SiteManagementCenteredModalAnimator.CloseCenteredModal(", StringComparison.Ordinal) &&
        siteHudSource.Contains("SiteManagementDrawerAnimator.AnimateRailTabsIn(this, _siteManagementTabRail)", StringComparison.Ordinal),
        "recruitment workbench should use the real centered modal UI as the q-bounce body: no gray ColorRect trail layer, faster speed, no redundant back button, tab retract, backdrop fade, point-like left-tab start, reach the 60% viewport marker while scaling and revealing, rebound to center while continuing into overscale, settle back to 100%, close by reversing the open path through the 60% marker back to the left edge, then rail return");
    AssertTrue(
        resourceBarAnimatorSource.Contains("private const int ModalOverlayBypassZIndex = 615;", StringComparison.Ordinal) &&
        resourceBarAnimatorSource.Contains("internal void SetModalOverlayBypass(bool active)", StringComparison.Ordinal) &&
        resourceBarAnimatorSource.Contains("_resourceBar.ZIndex = active ? ModalOverlayBypassZIndex : _restZIndex;", StringComparison.Ordinal) &&
        siteHudSource.Contains("_siteResourceBarAnimator.SetModalOverlayBypass(true);", StringComparison.Ordinal) &&
        siteHudSource.Contains("_siteResourceBarAnimator.SetModalOverlayBypass(false);", StringComparison.Ordinal),
        "opening the recruitment modal should dim the map through the backdrop while keeping the top-left resource bar above the backdrop and restoring its authored z-index after close");
    AssertTrue(
        centeredModalAnimatorSource.Contains("private sealed class ModalAnimationState", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("private static readonly Dictionary<ulong, ModalAnimationState> ActiveModalAnimations = new();", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("BeginAnimation(panel, restPosition)", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("CancelActiveTween(state);", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("IsCurrent(panel, generation)", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("GodotObject.IsInstanceValid(panel)", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("GodotObject.IsInstanceValid(backdrop)", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("state.RestPosition", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("FinishAnimation(panel, generation);", StringComparison.Ordinal) &&
        centeredModalAnimatorSource.Contains("CancelCenteredModal(Control panel, Control backdrop", StringComparison.Ordinal) &&
        siteHudSource.Contains("SiteManagementCenteredModalAnimator.CancelCenteredModal(_militaryWorkbenchPanel, _militaryWorkbenchBackdrop", StringComparison.Ordinal),
        "recruitment centered modal animator should own one tween chain per panel, cancel stale delayed/open/close callbacks, keep a stable rest position, and let immediate close cancel pending open without touching freed Godot wrappers");
    AssertTrue(
        musterCardScene.Contains("[node name=\"WorldMusterOptionCard\" type=\"Button\"", StringComparison.Ordinal) &&
        musterCardScene.Contains("res://scenes/ui/common/BattleUnitPlinthPreview.tscn", StringComparison.Ordinal) &&
        musterCardScene.Contains("[node name=\"PlinthPreview\" parent=\"PreviewLayer\" instance=", StringComparison.Ordinal) &&
        musterCardScene.Contains("position = Vector2(84, 99)", StringComparison.Ordinal) &&
        musterCardScene.Contains("scale = Vector2(0.84, 0.84)", StringComparison.Ordinal) &&
        musterCardScene.Contains("[node name=\"ResourceCostRow\" type=\"HBoxContainer\" parent=\".\"]", StringComparison.Ordinal) &&
        musterCardScene.Contains("[node name=\"ReserveCostSlot\" type=\"HBoxContainer\" parent=\"ResourceCostRow\"]", StringComparison.Ordinal) &&
        musterCardScene.Contains("[node name=\"MoneyCostSlot\" type=\"HBoxContainer\" parent=\"ResourceCostRow\"]", StringComparison.Ordinal) &&
        musterCardScene.Contains("[node name=\"FoodCostSlot\" type=\"HBoxContainer\" parent=\"ResourceCostRow\"]", StringComparison.Ordinal) &&
        musterCardScene.Contains("[node name=\"WoodCostSlot\" type=\"HBoxContainer\" parent=\"ResourceCostRow\"]", StringComparison.Ordinal) &&
        musterCardScene.Contains("[node name=\"OreCostSlot\" type=\"HBoxContainer\" parent=\"ResourceCostRow\"]", StringComparison.Ordinal) &&
        musterCardScene.Contains("res://assets/textures/ui/resource-icons/resource_money_icon_ai.png", StringComparison.Ordinal) &&
        musterCardScene.Contains("res://assets/textures/ui/resource-icons/resource_food_icon_ai.png", StringComparison.Ordinal) &&
        musterCardScene.Contains("res://assets/textures/ui/resource-icons/resource_wood_icon_ai.png", StringComparison.Ordinal) &&
        musterCardScene.Contains("res://assets/textures/ui/resource-icons/resource_ore_icon_ai.png", StringComparison.Ordinal) &&
        musterCardScene.Contains("res://assets/textures/ui/recruitment-ui-v1/recruitment_reserve_force_icon.png", StringComparison.Ordinal) &&
        !musterCardScene.Contains("recruitment_socket_circle_gold.png", StringComparison.Ordinal) &&
        !musterCardScene.Contains("ConsumeLabel", StringComparison.Ordinal) &&
        !musterCardScene.Contains("RefundLabel", StringComparison.Ordinal) &&
        !musterCardScene.Contains("NetLabel", StringComparison.Ordinal) &&
        !musterCardScene.Contains("PlinthSize =", StringComparison.Ordinal) &&
        !musterCardScene.Contains("HeroOffset =", StringComparison.Ordinal) &&
        !musterCardScene.Contains("HeroMaxSize =", StringComparison.Ordinal) &&
        !musterCardScene.Contains("HeroPreviewLayoutMode =", StringComparison.Ordinal) &&
        musterCardScene.Contains(recruitmentThemeResourcePath, StringComparison.Ordinal) &&
        musterCardRootBlock.Contains("theme_type_variation = &\"RecruitmentCardButton\"", StringComparison.Ordinal) &&
        musterCardScene.Contains(recruitmentPlinthResourcePath, StringComparison.Ordinal) &&
        musterCardPlinthBlock.Contains("PlinthTexture = ExtResource(", StringComparison.Ordinal) &&
        !musterCardScene.Contains("[node name=\"Plinth\" type=\"TextureRect\" parent=\"PreviewLayer\"]", StringComparison.Ordinal) &&
        !musterCardScene.Contains("[node name=\"AnimatedPreview\" parent=\"PreviewLayer\" instance=", StringComparison.Ordinal) &&
        musterCardNameplateBlock.Contains("theme_type_variation = &\"RecruitmentNameplatePanel\"", StringComparison.Ordinal) &&
        musterCardSource.Contains("_MakeCustomTooltip", StringComparison.Ordinal) &&
        musterCardSource.Contains("WorldMusterOptionTooltip", StringComparison.Ordinal) &&
        musterCardSource.Contains("BattleUnitAnimatedPreviewModel preview", StringComparison.Ordinal) &&
        musterCardSource.Contains("BattleUnitPlinthPreview", StringComparison.Ordinal) &&
        musterCardSource.Contains("StrategicResourceCostViewModel", StringComparison.Ordinal) &&
        musterCardSource.Contains("_reserveAmountLabel", StringComparison.Ordinal) &&
        musterCardSource.Contains("_moneyAmountLabel", StringComparison.Ordinal) &&
        musterCardSource.Contains("_foodAmountLabel", StringComparison.Ordinal) &&
        musterCardSource.Contains("_woodAmountLabel", StringComparison.Ordinal) &&
        musterCardSource.Contains("_oreAmountLabel", StringComparison.Ordinal) &&
        musterCardSource.Contains("ApplyResourceCost", StringComparison.Ordinal) &&
        !musterCardSource.Contains("_consumeLabel", StringComparison.Ordinal) &&
        !musterCardSource.Contains("_refundLabel", StringComparison.Ordinal) &&
        !musterCardSource.Contains("_netLabel", StringComparison.Ordinal) &&
        !musterCardSource.Contains("refundText", StringComparison.Ordinal) &&
        !musterCardSource.Contains("netText", StringComparison.Ordinal) &&
        !musterCardSource.Contains("string iconPath", StringComparison.Ordinal) &&
        !musterCardSource.Contains("_iconPath", StringComparison.Ordinal) &&
        !musterCardSource.Contains("GD.Load<Texture2D>", StringComparison.Ordinal) &&
        musterApplyBody.Contains("Disabled = false;", StringComparison.Ordinal) &&
        musterApplyBody.Contains("TooltipText = _displayName;", StringComparison.Ordinal) &&
        musterApplyBody.Contains("_reserveAmountLabel.Text = _reserveCost.ToString", StringComparison.Ordinal) &&
        musterApplyBody.Contains("ApplyResourceCost", StringComparison.Ordinal) &&
        !musterApplyBody.Contains("\\n", StringComparison.Ordinal) &&
        musterCustomTooltipBody.Contains("GameUiSceneFactory.CreateWorldMusterOptionTooltip", StringComparison.Ordinal) &&
        musterPressedBody.Contains("if (_selectable)", StringComparison.Ordinal),
        "muster option cards should use recruitment card resources, stay preview-only, bind externally resolved animated previews, display compact icon/amount reserve and resource requirements, remain hoverable when unavailable, and keep disabled reasons in an authored tooltip");
    AssertTrue(
        scene.Contains("[node name=\"SelectedHeroPlinthPreview\" parent=\"ModalHost/MilitaryWorkbenchPanel/WorkbenchMargin/WorkbenchStack/MilitaryBody/MilitaryDetailStack/SelectedHeroPanel/SelectedHeroMargin/SelectedHeroRow/SelectedHeroAvatarFrame\" instance=", StringComparison.Ordinal) &&
        scene.Contains("position = Vector2(88, 74)", StringComparison.Ordinal) &&
        scene.Contains("scale = Vector2(0.64, 0.64)", StringComparison.Ordinal) &&
        !scene.Contains("PlinthSize =", StringComparison.Ordinal) &&
        !scene.Contains("HeroOffset =", StringComparison.Ordinal) &&
        !scene.Contains("HeroMaxSize =", StringComparison.Ordinal) &&
        !scene.Contains("HeroPreviewLayoutMode =", StringComparison.Ordinal) &&
        !scene.Contains("[node name=\"SelectedHeroPlinth\" type=\"TextureRect\"", StringComparison.Ordinal) &&
        !scene.Contains("[node name=\"SelectedHeroPreview\" parent=\"ModalHost/MilitaryWorkbenchPanel/WorkbenchMargin/WorkbenchStack/MilitaryBody/MilitaryDetailStack/SelectedHeroPanel/SelectedHeroMargin/SelectedHeroRow/SelectedHeroAvatarFrame\" instance=", StringComparison.Ordinal),
        "selected hero workbench preview should use the fixed plinth-preview display component without overriding internal alignment.");
    AssertTrue(
        musterTooltipScene.Contains("[node name=\"WorldMusterOptionTooltip\" type=\"PanelContainer\"", StringComparison.Ordinal) &&
        musterTooltipScene.Contains("theme_type_variation = &\"WorldHoverInfoPanel\"", StringComparison.Ordinal) &&
        musterTooltipScene.Contains("[node name=\"TitleLabel\" type=\"Label\"", StringComparison.Ordinal) &&
        musterTooltipScene.Contains("[node name=\"ReserveLabel\" type=\"Label\"", StringComparison.Ordinal) &&
        musterTooltipScene.Contains("[node name=\"CostLabel\" type=\"Label\"", StringComparison.Ordinal) &&
        musterTooltipScene.Contains("[node name=\"DisabledReasonLabel\" type=\"Label\"", StringComparison.Ordinal) &&
        musterTooltipSource.Contains("public partial class WorldMusterOptionTooltip", StringComparison.Ordinal) &&
        musterTooltipSource.Contains("public void Bind(", StringComparison.Ordinal),
        "muster hover detail should be an authored PanelContainer scene with title, reserve cost, resource cost, and disabled reason labels");
    AssertTrue(
        factorySource.Contains("WorldMusterOptionTooltipScenePath = \"res://scenes/world/ui/WorldMusterOptionTooltip.tscn\"", StringComparison.Ordinal) &&
        factorySource.Contains("CreateWorldMusterOptionTooltip", StringComparison.Ordinal),
        "UI scene factory should expose the authored muster tooltip scene used by recruitment cards");
    AssertTrue(
        !factorySource.Contains("CreateWorldCorpsInstanceRow", StringComparison.Ordinal) &&
        !workbenchBinderSource.Contains("GameUiSceneFactory.CreateWorldCorpsInstanceRow", StringComparison.Ordinal) &&
        !workbenchBinderSource.Contains("ReplenishRequested +=", StringComparison.Ordinal),
        "removed first-version corps tab should not keep active corps-row creation paths in recruitment binding");
    AssertTrue(
        siteHudSource.Contains("StrategicManagementRuntime.BuildHeroCorpsWorkbenchDashboard(", StringComparison.Ordinal) &&
        siteHudSource.Contains("OpenStrategicMilitaryWorkbench", StringComparison.Ordinal) &&
        siteHudSource.Contains("BindStrategicMilitaryWorkbench", StringComparison.Ordinal) &&
        siteHudSource.Contains("OnStrategicMilitaryHeroSelected", StringComparison.Ordinal) &&
        siteHudSource.Contains("OnStrategicRecruitCorpsForHeroPressed", StringComparison.Ordinal) &&
        siteHudSource.Contains("StrategicManagementRuntime.Commands.RecruitCorpsForHero(", StringComparison.Ordinal),
        "WorldSiteRoot should open the workbench from recruitment, build hero-scoped replacement projections, and submit hero-directed recruitment through Strategic Management commands");
}

internal static void UnitIdlePreviewsReachExpeditionBattleGateAndDeploymentSurfaces()
{
    string root = ProjectRoot();
    string commonDir = Path.Combine(root, "src", "Presentation", "Common");
    string worldDir = Path.Combine(root, "src", "Presentation", "World");
    string siteRootDir = Path.Combine(worldDir, "Sites");
    string factoryPath = Path.Combine(commonDir, "GameUiSceneFactory.cs");
    string resolverPath = Path.Combine(commonDir, "BattleUnitPreviewResolver.cs");
    string commandGroupPath = Path.Combine(siteRootDir, "BattleRuntimeCommandGroupView.cs");
    string commandHudModelPath = Path.Combine(siteRootDir, "BattleRuntimeCommandHudModel.cs");
    string preparationBinderPath = Path.Combine(siteRootDir, "BattlePreparationHudBinder.cs");
    string rosterRowSourcePath = Path.Combine(siteRootDir, "BattlePreparationRosterRow.cs");
    string rosterRowScenePath = Path.Combine(root, "scenes", "world", "ui", "BattlePreparationRosterRow.tscn");
    string expeditionHudPath = Path.Combine(worldDir, "StrategicWorldRoot.ExpeditionHud.cs");
    string expeditionRowSourcePath = Path.Combine(worldDir, "WorldExpeditionCountRow.cs");
    string expeditionRowScenePath = Path.Combine(root, "scenes", "world", "ui", "WorldExpeditionCountRow.tscn");
    string battleGateDialogPath = Path.Combine(worldDir, "StrategicBattleGateDialog.cs");
    string battleGateCardSourcePath = Path.Combine(worldDir, "StrategicBattleGateForcePreviewCard.cs");
    string battleGateScenePath = Path.Combine(root, "scenes", "world", "ui", "PreBattleDialog.tscn");
    string battleGateCardScenePath = Path.Combine(root, "scenes", "world", "ui", "StrategicBattleGateForcePreviewCard.tscn");
    string battleEntryPath = Path.Combine(worldDir, "StrategicWorldRoot.BattleEntry.cs");

    foreach (string path in new[]
    {
        factoryPath,
        resolverPath,
        commandGroupPath,
        commandHudModelPath,
        preparationBinderPath,
        rosterRowSourcePath,
        rosterRowScenePath,
        expeditionHudPath,
        expeditionRowSourcePath,
        expeditionRowScenePath,
        battleGateDialogPath,
        battleGateCardSourcePath,
        battleGateScenePath,
        battleGateCardScenePath,
        battleEntryPath
    })
    {
        AssertTrue(File.Exists(path), $"idle preview presentation artifact should exist path={path}");
    }

    string factorySource = File.ReadAllText(factoryPath);
    string commandGroupSource = File.ReadAllText(commandGroupPath);
    string commandHudModelSource = File.ReadAllText(commandHudModelPath);
    string preparationBinderSource = File.ReadAllText(preparationBinderPath);
    string rosterRowSource = File.ReadAllText(rosterRowSourcePath);
    string rosterRowScene = File.ReadAllText(rosterRowScenePath);
    string expeditionHudSource = File.ReadAllText(expeditionHudPath);
    string expeditionRowSource = File.ReadAllText(expeditionRowSourcePath);
    string expeditionRowScene = File.ReadAllText(expeditionRowScenePath);
    string battleGateDialogSource = File.ReadAllText(battleGateDialogPath);
    string battleGateCardSource = File.ReadAllText(battleGateCardSourcePath);
    string battleGateScene = File.ReadAllText(battleGateScenePath);
    string battleGateCardScene = File.ReadAllText(battleGateCardScenePath);
    string battleEntrySource = File.ReadAllText(battleEntryPath);
    string resolverSource = File.ReadAllText(resolverPath);

    AssertTrue(
        commandGroupSource.Contains("HeroBattleUnitId", StringComparison.Ordinal) &&
        commandGroupSource.Contains("CorpsBattleUnitId", StringComparison.Ordinal) &&
        commandHudModelSource.Contains("ResolveHeroBattleUnitId", StringComparison.Ordinal) &&
        commandHudModelSource.Contains("ResolveCorpsBattleUnitId", StringComparison.Ordinal) &&
        commandHudModelSource.Contains("StrategicHeroBattleUnitId", StringComparison.Ordinal) &&
        commandHudModelSource.Contains("StrategicCorpsBattleUnitId", StringComparison.Ordinal),
        "battle command group view should carry hero and corps battle unit ids for all later UI preview surfaces");
    AssertTrue(
        rosterRowScene.Contains("[node name=\"Avatar\" type=\"TextureRect\"", StringComparison.Ordinal) &&
        !rosterRowScene.Contains("[node name=\"Avatar\" type=\"ColorRect\"", StringComparison.Ordinal) &&
        rosterRowSource.Contains("Texture2D previewTexture", StringComparison.Ordinal) &&
        rosterRowSource.Contains("_avatar.Texture = _previewTexture", StringComparison.Ordinal) &&
        preparationBinderSource.Contains("BattleUnitPreviewResolver.ResolvePreviewTexture(group.HeroBattleUnitId)", StringComparison.Ordinal) &&
        !rosterRowSource.Contains("ColorRect", StringComparison.Ordinal),
        "battle-preparation roster rows should show the selected hero idle first frame instead of a color swatch");
    AssertTrue(
        expeditionRowScene.Contains("WorldExpeditionCountRow.cs", StringComparison.Ordinal) &&
        expeditionRowScene.Contains("[node name=\"HeroPreview\" type=\"TextureRect\"", StringComparison.Ordinal) &&
        expeditionRowScene.Contains("[node name=\"CorpsPreview\" type=\"TextureRect\"", StringComparison.Ordinal) &&
        expeditionRowSource.Contains("public partial class WorldExpeditionCountRow : HBoxContainer", StringComparison.Ordinal) &&
        expeditionRowSource.Contains("Texture2D heroPreviewTexture", StringComparison.Ordinal) &&
        expeditionRowSource.Contains("Texture2D corpsPreviewTexture", StringComparison.Ordinal) &&
        expeditionHudSource.Contains("BattleUnitPreviewResolver.ResolvePreviewTexture(company.HeroBattleUnitId)", StringComparison.Ordinal) &&
        expeditionHudSource.Contains("BattleUnitPreviewResolver.ResolvePreviewTexture(company.CorpsBattleUnitId)", StringComparison.Ordinal) &&
        factorySource.Contains("public static WorldExpeditionCountRow CreateWorldExpeditionCountRow", StringComparison.Ordinal),
        "strategic-world expedition draft rows should preview both hero and corps idle frames through the shared resolver");
    AssertTrue(
        battleGateScene.Contains("BriefPreviewList", StringComparison.Ordinal) &&
        battleGateScene.Contains("DetailPreviewList", StringComparison.Ordinal) &&
        battleGateCardScene.Contains("StrategicBattleGateForcePreviewCard.cs", StringComparison.Ordinal) &&
        battleGateCardScene.Contains("[node name=\"Preview\" type=\"TextureRect\"", StringComparison.Ordinal) &&
        battleGateCardSource.Contains("Texture2D previewTexture", StringComparison.Ordinal) &&
        battleGateDialogSource.Contains("StrategicBattleGateForcePreviewData", StringComparison.Ordinal) &&
        battleGateDialogSource.Contains("BattleUnitPreviewResolver.ResolvePreviewTexture(item.BattleUnitId)", StringComparison.Ordinal) &&
        battleEntrySource.Contains("BuildPreBattleForcePreviewData", StringComparison.Ordinal) &&
        battleEntrySource.Contains("StrategicHeroBattleUnitId", StringComparison.Ordinal) &&
        battleEntrySource.Contains("StrategicCorpsBattleUnitId", StringComparison.Ordinal) &&
        factorySource.Contains("CreateStrategicBattleGateForcePreviewCard", StringComparison.Ordinal),
        "battle trigger brief/detail modal should show authored force preview cards instead of only multiline text");
    AssertTrue(
        resolverSource.Contains("GetFrameTexture(animationName, 0)", StringComparison.Ordinal) &&
        !expeditionHudSource.Contains("GD.Load<Texture2D>", StringComparison.Ordinal) &&
        !preparationBinderSource.Contains("GD.Load<Texture2D>", StringComparison.Ordinal) &&
        !battleGateDialogSource.Contains("GD.Load<Texture2D>", StringComparison.Ordinal),
        "expedition, battle gate, and deployment surfaces should share the idle-frame resolver and must not load raw spritesheet textures directly");
}

internal static void WorldSiteBuildPickerUsesIconCardsAndMapPlacement()
{
    string root = ProjectRoot();
    string siteRootDir = Path.Combine(root, "src", "Presentation", "World", "Sites");
    string scenePath = Path.Combine(root, "scenes", "world", "ui", "WorldSitePeacetimeHud.tscn");
    string cardScenePath = Path.Combine(root, "scenes", "world", "ui", "WorldBuildingOptionCard.tscn");
    string tooltipScenePath = Path.Combine(root, "scenes", "world", "ui", "WorldBuildingOptionTooltip.tscn");
    string cardThemePath = Path.Combine(root, "resource", "ui", "themes", "game-ui-skin", "build_inventory_preview_theme.tres");
    string cardSourcePath = Path.Combine(siteRootDir, "WorldBuildingOptionCard.cs");
    string tooltipSourcePath = Path.Combine(siteRootDir, "WorldBuildingOptionTooltip.cs");
    string binderPath = Path.Combine(siteRootDir, "StrategicManagementDashboardPanelBinder.cs");
    string nodeRefsPath = Path.Combine(siteRootDir, "WorldSitePeacetimeHudNodeRefs.cs");
    string factoryPath = Path.Combine(root, "src", "Presentation", "Common", "GameUiSceneFactory.cs");
    string siteHudPath = Path.Combine(siteRootDir, "WorldSiteRoot.SiteManagementHud.cs");

    AssertTrue(File.Exists(scenePath), $"world-site peacetime HUD scene should exist path={scenePath}");
    AssertTrue(File.Exists(cardScenePath), $"build picker card should be an authored reusable scene path={cardScenePath}");
    AssertTrue(File.Exists(tooltipScenePath), $"build picker hover detail should be an authored reusable tooltip scene path={tooltipScenePath}");
    AssertTrue(File.Exists(cardThemePath), $"build picker card theme should exist path={cardThemePath}");
    AssertTrue(File.Exists(cardSourcePath), $"build picker card should own its binding script path={cardSourcePath}");
    AssertTrue(File.Exists(tooltipSourcePath), $"build picker hover detail should own its binding script path={tooltipSourcePath}");

    string scene = File.ReadAllText(scenePath);
    string cardScene = File.ReadAllText(cardScenePath);
    string tooltipScene = File.ReadAllText(tooltipScenePath);
    string cardTheme = File.ReadAllText(cardThemePath);
    string cardSource = File.ReadAllText(cardSourcePath);
    string tooltipSource = File.ReadAllText(tooltipSourcePath);
    string binderSource = File.ReadAllText(binderPath);
    string nodeRefsSource = File.ReadAllText(nodeRefsPath);
    string factorySource = File.ReadAllText(factoryPath);
    string siteHudSource = File.ReadAllText(siteHudPath);
    string rootSource = ReadWorldSiteRootSource();
    string buildListBlock = ExtractSceneNodeBlock(scene, "[node name=\"SiteBuildingOptionGrid\"");
    string cardApplyBody = ExtractMethodBody(cardSource, "private void ApplyBinding()");
    string cardCustomTooltipBody = ExtractMethodBody(cardSource, "public override Control _MakeCustomTooltip(");
    string cardPressedBody = ExtractMethodBody(cardSource, "private void OnPressed()");

    AssertTrue(
        buildListBlock.Contains("type=\"GridContainer\"", StringComparison.Ordinal) &&
        buildListBlock.Contains("columns = 4", StringComparison.Ordinal),
        "site-management build picker should be a compact inventory-style GridContainer, not a long text-button VBox list");
    AssertTrue(
        nodeRefsSource.Contains("internal GridContainer SiteBuildingOptionGrid", StringComparison.Ordinal) &&
        nodeRefsSource.Contains("Get<GridContainer>(root, \"OverlayHost/SitePeacetimePanel/Margin/SiteManagementStack/ManagementContentScroll/ManagementContent/SiteBuildSection/SiteBuildingOptionGrid\"", StringComparison.Ordinal),
        "world-site HUD node refs should bind the build picker as GridContainer");

    AssertTrue(
        cardScene.Contains("[node name=\"WorldBuildingOptionCard\" type=\"Button\"", StringComparison.Ordinal) &&
        cardScene.Contains("res://src/Presentation/World/Sites/WorldBuildingOptionCard.cs", StringComparison.Ordinal) &&
        cardScene.Contains("[node name=\"IconSlot\" type=\"CenterContainer\" parent=\"Content\"]", StringComparison.Ordinal) &&
        cardScene.Contains("[node name=\"Icon\" type=\"TextureRect\"", StringComparison.Ordinal) &&
        cardScene.Contains("[node name=\"NameLabel\" type=\"Label\"", StringComparison.Ordinal) &&
        cardScene.Contains("theme_override_colors/font_color = Color(0.96, 0.78, 0.62, 1)", StringComparison.Ordinal),
        "building option card should be a focused authored Button scene with icon and a readable bottom name label for the dark ManaSoul panel");
    AssertTrue(
        cardSource.Contains("public void Bind(") &&
        cardSource.Contains("TextureRect") &&
        cardSource.Contains("TooltipText") &&
        cardSource.Contains("_MakeCustomTooltip", StringComparison.Ordinal) &&
        cardSource.Contains("WorldBuildingOptionTooltip", StringComparison.Ordinal) &&
        cardSource.Contains("占地") &&
        cardSource.Contains("成本") &&
        !cardSource.Contains("DefaultRegion", StringComparison.Ordinal) &&
        !cardSource.Contains("CategoryId", StringComparison.Ordinal),
        "building option card binding should expose only icon/name on the card plus a custom footprint and cost tooltip");
    AssertTrue(
        cardApplyBody.Contains("TooltipText = _displayName;", StringComparison.Ordinal) &&
        !cardApplyBody.Contains("\\n占地", StringComparison.Ordinal) &&
        !cardApplyBody.Contains("\\n成本", StringComparison.Ordinal) &&
        !cardApplyBody.Contains("不可建造", StringComparison.Ordinal) &&
        cardCustomTooltipBody.Contains("GameUiSceneFactory.CreateWorldBuildingOptionTooltip", StringComparison.Ordinal) &&
        cardCustomTooltipBody.Contains("占地", StringComparison.Ordinal) &&
        cardCustomTooltipBody.Contains("成本", StringComparison.Ordinal) &&
        cardCustomTooltipBody.Contains("_disabledReason", StringComparison.Ordinal),
        "building option card TooltipText should be a short trigger only; footprint, cost, and disabled reason belong to the authored custom tooltip scene");
    AssertTrue(
        cardApplyBody.Contains("Disabled = false;", StringComparison.Ordinal) &&
        !cardApplyBody.Contains("Disabled = !_selectable;", StringComparison.Ordinal) &&
        cardPressedBody.Contains("if (_selectable)", StringComparison.Ordinal),
        "building option cards must remain hoverable when unavailable; click authority stays gated by _selectable instead of BaseButton.Disabled");
    AssertTrue(
        cardScene.Contains("[node name=\"IconSlot\" type=\"CenterContainer\" parent=\"Content\"]", StringComparison.Ordinal) &&
        cardScene.Contains("custom_minimum_size = Vector2(96, 70)", StringComparison.Ordinal) &&
        cardScene.Contains("[node name=\"Icon\" type=\"TextureRect\" parent=\"Content/IconSlot\"]", StringComparison.Ordinal) &&
        cardScene.Contains("stretch_mode = 0", StringComparison.Ordinal) &&
        !cardScene.Contains("stretch_mode = 5", StringComparison.Ordinal) &&
        !cardScene.Contains("expand_mode = 1", StringComparison.Ordinal) &&
        cardSource.Contains("ApplyIconTexture(Texture2D texture)", StringComparison.Ordinal) &&
        cardSource.Contains("CalculateIntegerIconScale", StringComparison.Ordinal) &&
        cardSource.Contains("TextureFilter = TextureFilterEnum.Nearest", StringComparison.Ordinal),
        "building option card previews should center an integer-scaled texture in a fixed slot instead of using arbitrary TextureRect aspect scaling");
    AssertTrue(
        tooltipScene.Contains("[node name=\"WorldBuildingOptionTooltip\" type=\"PanelContainer\"", StringComparison.Ordinal) &&
        tooltipScene.Contains("res://src/Presentation/World/Sites/WorldBuildingOptionTooltip.cs", StringComparison.Ordinal) &&
        tooltipScene.Contains("theme_type_variation = &\"WorldHoverInfoPanel\"", StringComparison.Ordinal) &&
        tooltipScene.Contains("[node name=\"TitleLabel\" type=\"Label\"", StringComparison.Ordinal) &&
        tooltipScene.Contains("[node name=\"FootprintLabel\" type=\"Label\"", StringComparison.Ordinal) &&
        tooltipScene.Contains("[node name=\"CostLabel\" type=\"Label\"", StringComparison.Ordinal) &&
        tooltipScene.Contains("[node name=\"DisabledReasonLabel\" type=\"Label\"", StringComparison.Ordinal),
        "building option hover detail should be an authored PanelContainer scene with name, footprint, cost, and disabled reason labels");
    AssertTrue(
        cardTheme.Contains("[sub_resource type=\"StyleBoxEmpty\" id=\"StyleBoxEmpty_popup_panel\"]", StringComparison.Ordinal) &&
        cardTheme.Contains("PopupPanel/styles/panel = SubResource(\"StyleBoxEmpty_popup_panel\")", StringComparison.Ordinal) &&
        cardTheme.Contains("TooltipPanel/styles/panel = SubResource(\"StyleBoxEmpty_popup_panel\")", StringComparison.Ordinal) &&
        !cardTheme.Contains("PopupPanel/base_type", StringComparison.Ordinal),
        "building option native tooltip popup shell should stay transparent in the card theme for reversible preview-skin use");
    string sharedThemePath = Path.Combine(root, "resource", "ui", "themes", "game-ui-skin", "basic_ui_1_theme.tres");
    string sharedTheme = File.ReadAllText(sharedThemePath);
    AssertTrue(
        sharedTheme.Contains("[sub_resource type=\"StyleBoxEmpty\" id=\"StyleBoxEmpty_popup_panel\"]", StringComparison.Ordinal) &&
        sharedTheme.Contains("PopupPanel/styles/panel = SubResource(\"StyleBoxEmpty_popup_panel\")", StringComparison.Ordinal) &&
        sharedTheme.Contains("TooltipPanel/styles/panel = SubResource(\"StyleBoxEmpty_popup_panel\")", StringComparison.Ordinal) &&
        sharedTheme.Contains("WorldHoverInfoPanel/styles/panel = ExtResource(\"1_panel_sheet\")", StringComparison.Ordinal),
        "shared custom tooltip popup shell should be transparent so only the authored WorldHoverInfoPanel background is visible");
    AssertTrue(
        tooltipSource.Contains("public void Bind(", StringComparison.Ordinal) &&
        tooltipSource.Contains("TitleLabel", StringComparison.Ordinal) &&
        tooltipSource.Contains("FootprintLabel", StringComparison.Ordinal) &&
        tooltipSource.Contains("CostLabel", StringComparison.Ordinal) &&
        tooltipSource.Contains("DisabledReasonLabel", StringComparison.Ordinal),
        "building option hover detail script should own label binding for the authored tooltip scene");

    AssertTrue(
        factorySource.Contains("WorldBuildingOptionCardScenePath = \"res://scenes/world/ui/WorldBuildingOptionCard.tscn\"", StringComparison.Ordinal) &&
        factorySource.Contains("CreateWorldBuildingOptionCard", StringComparison.Ordinal) &&
        factorySource.Contains("Instantiate<WorldBuildingOptionCard>(WorldBuildingOptionCardScenePath", StringComparison.Ordinal) &&
        factorySource.Contains("WorldBuildingOptionTooltipScenePath = \"res://scenes/world/ui/WorldBuildingOptionTooltip.tscn\"", StringComparison.Ordinal) &&
        factorySource.Contains("CreateWorldBuildingOptionTooltip", StringComparison.Ordinal) &&
        factorySource.Contains("Instantiate<WorldBuildingOptionTooltip>(WorldBuildingOptionTooltipScenePath", StringComparison.Ordinal),
        "build option cards and their hover detail should be instantiated through GameUiSceneFactory");

    string bindOptionsBody = ExtractMethodBody(binderSource, "private void BindBuildingOptions(");
    AssertTrue(
        bindOptionsBody.Contains("GameUiSceneFactory.CreateWorldBuildingOptionCard", StringComparison.Ordinal) &&
        bindOptionsBody.Contains("_selectBuildingForPlacement?.Invoke(buildingDefinitionId)", StringComparison.Ordinal) &&
        bindOptionsBody.Contains("FormatReasonsForPresentation(option.DisabledReason)", StringComparison.Ordinal),
        "strategic dashboard binder should create building option cards, pass disabled reason to the tooltip, and submit building selection, not direct placement");
    foreach (string forbidden in new[]
    {
        "CreateWorldPrimaryActionButton",
        "DefaultRegionId",
        "DefaultGridX",
        "DefaultGridY",
        "FormatCategory("
    })
    {
        AssertTrue(
            !bindOptionsBody.Contains(forbidden, StringComparison.Ordinal),
            $"building picker binding must keep placement/debug detail out of the card body fragment={forbidden}");
    }

    string inputBody = ExtractMethodBody(rootSource, "public override void _Input(InputEvent @event)");
    AssertTrue(
        inputBody.Contains("TryHandleStrategicBuildingPlacementInput(@event)", StringComparison.Ordinal) &&
        !inputBody.Contains("TryHandleFacilitySlotInput(@event)", StringComparison.Ordinal),
        "selected strategic building placement should consume map clicks without legacy facility-slot compatibility input");

    string selectBody = ExtractMethodBody(siteHudSource, "private void OnStrategicBuildBuildingSelected(");
    AssertTrue(
        !selectBody.Contains("BuildCityBuilding", StringComparison.Ordinal) &&
        selectBody.Contains("_selectedStrategicBuildingDefinitionId", StringComparison.Ordinal),
        "pressing a building card should enter placement mode instead of building at a default coordinate");
    string placementBody = ExtractMethodBody(rootSource, "private bool TryHandleStrategicBuildingPlacementInput(");
    AssertTrue(
        placementBody.Contains("TrySubmitStrategicBuildingPlacement(", StringComparison.Ordinal) &&
        placementBody.Contains("GetViewport().SetInputAsHandled()", StringComparison.Ordinal),
        "strategic building placement input should consume the map click and route submission through the focused placement command method");
    AssertTrue(
        rootSource.Contains("BuildCityBuilding", StringComparison.Ordinal) &&
        rootSource.Contains("TryResolveStrategicBuildingPlacement", StringComparison.Ordinal),
        "strategic building placement should resolve a map click to command-validated placement");
    AssertTrue(
        rootSource.Contains("StrategicBuildingPlacementResolver", StringComparison.Ordinal),
        "map-click construction placement resolution should live in a focused Presentation collaborator, not the legacy facility-slot path");
}

internal static void WorldSiteStrategicBuildingPlacementUsesMarkerBackedPreview()
{
    string root = ProjectRoot();
    string siteRootDir = Path.Combine(root, "src", "Presentation", "World", "Sites");
    string rootSource = ReadWorldSiteRootSource();
    string placementSourcePath = Path.Combine(siteRootDir, "WorldSiteRoot.StrategicBuildingPlacement.cs");
    string mapPresentationPath = Path.Combine(siteRootDir, "WorldSiteRoot.SiteMapPresentation.cs");
    string resolverPath = Path.Combine(siteRootDir, "StrategicBuildingPlacementResolver.cs");
    string previewPath = Path.Combine(siteRootDir, "StrategicBuildingPlacementPreview.cs");
    string buildingEntityPath = Path.Combine(siteRootDir, "StrategicCityBuildingMapEntity.cs");
    string scenePath = Path.Combine(root, "scenes", "world", "sites", "WorldSiteRoot.tscn");
    string buildingEntityScenePath = Path.Combine(root, "scenes", "world", "sites", "StrategicCityBuildingMapEntity.tscn");

    AssertTrue(File.Exists(previewPath), "strategic building placement preview should live in a focused Presentation Node2D collaborator");
    AssertTrue(File.Exists(buildingEntityPath), "confirmed strategic buildings should render through a focused map entity collaborator");
    AssertTrue(File.Exists(buildingEntityScenePath), "confirmed strategic buildings should use an authored reusable scene");

    string placementSource = File.ReadAllText(placementSourcePath);
    string mapPresentationSource = File.ReadAllText(mapPresentationPath);
    string resolverSource = File.ReadAllText(resolverPath);
    string previewSource = File.Exists(previewPath) ? File.ReadAllText(previewPath) : "";
    string buildingEntitySource = File.Exists(buildingEntityPath) ? File.ReadAllText(buildingEntityPath) : "";
    string buildingEntityScene = File.Exists(buildingEntityScenePath) ? File.ReadAllText(buildingEntityScenePath) : "";
    string scene = File.ReadAllText(scenePath);

    AssertTrue(
        mapPresentationSource.Contains("ResolveSemanticConstructionRegionMarkers", StringComparison.Ordinal) &&
        mapPresentationSource.Contains("SemanticMapMarkerType.ConstructionRegion", StringComparison.Ordinal),
        "world-site map presentation should filter marker-backed construction regions separately from legacy building slots");
    AssertTrue(
        resolverSource.Contains("IReadOnlyList<SemanticMapMarkerData> constructionRegionMarkers", StringComparison.Ordinal) &&
        resolverSource.Contains("marker.MarkerType == SemanticMapMarkerType.ConstructionRegion", StringComparison.Ordinal) &&
        resolverSource.Contains("marker.MarkerId", StringComparison.Ordinal),
        "strategic building placement resolver should use marker-backed construction region ids before command validation");
    AssertTrue(
        placementSource.Contains("@event is InputEventMouseMotion", StringComparison.Ordinal) &&
        placementSource.Contains("UpdateStrategicBuildingPlacementPreview()", StringComparison.Ordinal),
        "strategic building placement should refresh realtime legality preview on mouse motion");
    AssertTrue(
        placementSource.Contains("SetStrategicBuildingPlacementPreview", StringComparison.Ordinal) &&
        placementSource.Contains("ClearStrategicBuildingPlacementPreview", StringComparison.Ordinal),
        "strategic building placement should have explicit preview set and clear lifecycle");
    AssertTrue(
        placementSource.Contains("SuppressStrategicBuildingAutoHover", StringComparison.Ordinal) &&
        placementSource.Contains("RestoreStrategicBuildingAutoHover", StringComparison.Ordinal) &&
        placementSource.Contains("BattleGridHighlightKind.Hover", StringComparison.Ordinal),
        "strategic building placement should suppress the generic 1x1 map hover so only the building footprint frame is visible");
    AssertTrue(
        previewSource.Contains("StrategicBuildingPlacementPreview : Node2D", StringComparison.Ordinal) &&
        previewSource.Contains("SetPreview(") &&
        previewSource.Contains("Texture2D", StringComparison.Ordinal) &&
        previewSource.Contains("DrawTextureRect", StringComparison.Ordinal),
        "placement preview should be a viewport Node2D that renders the selected building texture");
    AssertTrue(
        previewSource.Contains("TryBuildFootprintFramePolygon", StringComparison.Ordinal) &&
        previewSource.Contains("DrawPlacementFootprintFrame", StringComparison.Ordinal) &&
        previewSource.Contains("DrawPlacementCornerSegment", StringComparison.Ordinal) &&
        previewSource.Contains("DrawLine", StringComparison.Ordinal),
        "placement preview should draw the hover corner frame from the whole building footprint, not a fixed 1x1 cell");
    AssertTrue(
        !previewSource.Contains("DrawColoredPolygon", StringComparison.Ordinal) &&
        !previewSource.Contains("DrawPolyline", StringComparison.Ordinal) &&
        !previewSource.Contains("BuildableFill", StringComparison.Ordinal) &&
        !previewSource.Contains("BlockedFill", StringComparison.Ordinal),
        "placement preview should not draw the old n*m footprint grid over the selected building texture");
    AssertTrue(
        placementSource.Contains("GD.Load<Texture2D>(building.IconPath)", StringComparison.Ordinal) &&
        placementSource.Contains("SetStrategicBuildingPlacementPreview(footprintCells, buildable, previewTexture)", StringComparison.Ordinal),
        "strategic building placement should pass the selected building texture into the mouse-follow preview");
    AssertTrue(
        placementSource.Contains("StrategicManagementRuntime.SaveCurrentState()", StringComparison.Ordinal),
        "successful strategic building placement should persist the Strategic Management state through the runtime save boundary");
    AssertTrue(
        rootSource.Contains("_strategicBuildingPlacementPreview", StringComparison.Ordinal) &&
        scene.Contains("StrategicBuildingPlacementPreview.cs", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"StrategicBuildingPlacementPreview\" type=\"Node2D\" parent=\"MainWorldViewportHost/MainWorldViewport/OverlayRoot\"]", StringComparison.Ordinal),
        "WorldSiteRoot should author and bind the strategic building placement preview under the world viewport overlay");
    AssertTrue(
        rootSource.Contains("CityBuildingRootPath", StringComparison.Ordinal) &&
        rootSource.Contains("_cityBuildingRoot", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"CityBuildingRoot\" type=\"Node2D\" parent=\"MainWorldViewportHost/MainWorldViewport\"]", StringComparison.Ordinal),
        "WorldSiteRoot should author a dedicated map container for confirmed city building entities");
    AssertTrue(
        mapPresentationSource.Contains("RefreshStrategicCityBuildingEntities", StringComparison.Ordinal) &&
        mapPresentationSource.Contains("StrategicManagementRuntime.State.Cities", StringComparison.Ordinal) &&
        mapPresentationSource.Contains("city.Buildings", StringComparison.Ordinal) &&
        mapPresentationSource.Contains("StrategicCityBuildingMapEntity", StringComparison.Ordinal),
        "world-site map presentation should rebuild confirmed building map entities from Strategic Management city building state");
    AssertTrue(
        buildingEntitySource.Contains("StrategicCityBuildingMapEntity : Node2D", StringComparison.Ordinal) &&
        buildingEntitySource.Contains("SetBuilding(", StringComparison.Ordinal) &&
        buildingEntitySource.Contains("DrawTexture(_texture", StringComparison.Ordinal) &&
        buildingEntitySource.Contains("ResolveNativeDrawPosition", StringComparison.Ordinal) &&
        buildingEntityScene.Contains("StrategicCityBuildingMapEntity.cs", StringComparison.Ordinal),
        "confirmed building map entity should render the native building texture through an authored Node2D scene");
    AssertTrue(
        !placementSource.Contains("BuildFacility", StringComparison.Ordinal) &&
        !resolverSource.Contains("FacilitySlotDefinition", StringComparison.Ordinal) &&
        !resolverSource.Contains("WorldActionResolver", StringComparison.Ordinal),
        "strategic building placement preview and resolution must not route through legacy facility-slot authority");
}

internal static void StrategicCityBuildingMapEntityDrawsCleanSpriteOnly()
{
    string entitySource = File.ReadAllText(Path.Combine(
        ProjectRoot(),
        "src",
        "Presentation",
        "World",
        "Sites",
        "StrategicCityBuildingMapEntity.cs"));
    string drawBody = ExtractMethodBody(entitySource, "public override void _Draw()");

    AssertTrue(
        drawBody.Contains("DrawTexture(_texture", StringComparison.Ordinal) &&
        entitySource.Contains("ResolveNativeDrawPosition", StringComparison.Ordinal) &&
        entitySource.Contains("_texture.GetWidth()", StringComparison.Ordinal) &&
        entitySource.Contains("_texture.GetHeight()", StringComparison.Ordinal),
        "confirmed strategic city building entity should draw the selected building texture at native pixel size");
    AssertTrue(
        !entitySource.Contains("FootprintShadowColor", StringComparison.Ordinal) &&
        !drawBody.Contains("DrawTextureRect(_texture, _drawRect", StringComparison.Ordinal) &&
        !drawBody.Contains("Grow(", StringComparison.Ordinal) &&
        !drawBody.Contains("DrawRect(_drawRect", StringComparison.Ordinal),
        "confirmed strategic city building entity should not stretch sprites or paint footprint backgrounds behind clean sprites");
}

static string _siteManagementTabField(string tabName)
{
    return tabName switch
    {
        "BuildTabButton" => "_siteBuildTabButton",
        "RecruitTabButton" => "_siteRecruitTabButton",
        "OverviewTabButton" => "_siteOverviewTabButton",
        "ReturnMapTabButton" => "_returnMapButton",
        _ => tabName
    };
}

internal static void WorldSiteRootDelegatesBattlePreparationMapDrag()
{
    string siteRootDir = Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites");
    string rootSource = ReadWorldSiteRootSource();
    string interactionSource = File.ReadAllText(Path.Combine(siteRootDir, "WorldSiteRoot.SiteInteraction.cs"));
    string controllerPath = Path.Combine(siteRootDir, "BattlePreparationDeploymentDragController.cs");
    AssertTrue(File.Exists(controllerPath), "battle-preparation map drag request/placement rules should live in BattlePreparationDeploymentDragController");

    string controllerSource = File.ReadAllText(controllerPath);
    AssertTrue(
        controllerSource.Contains("internal sealed class BattlePreparationDeploymentDragController", StringComparison.Ordinal),
        "battle-preparation deployment drag controller should be a focused internal Presentation collaborator");
    AssertTrue(
        rootSource.Contains("private readonly BattlePreparationDeploymentDragController _battlePreparationDeploymentDragController", StringComparison.Ordinal),
        "WorldSiteRoot should own a focused battle-preparation deployment drag collaborator");

    foreach (string delegatedCall in new[]
    {
        "_battlePreparationDeploymentDragController.TryResolveDragContext(",
        "_battlePreparationDeploymentDragController.TryMovePlacement(",
        "_battlePreparationDeploymentDragController.SyncRequestPlacement("
    })
    {
        AssertTrue(interactionSource.Contains(delegatedCall, StringComparison.Ordinal), $"site interaction should delegate battle-preparation map drag through {delegatedCall}");
    }
    AssertTrue(
        interactionSource.Contains("dragContext != null") &&
        interactionSource.Contains("_battlePreparationDeploymentDragController.BuildFootprintCells(dragContext, gridPosition)") &&
        interactionSource.Contains("BuildSitePlacementFootprintCells(placement, gridPosition)"),
        "peacetime drag footprint fallback should remain in WorldSiteRoot; only battle-preparation drag contexts should delegate footprint cells to the controller");

    foreach (string rootMethod in new[]
    {
        "private bool TryResolveBattlePreparationDragContext(",
        "private static bool TryResolveBattlePreparationRequestPlacement(",
        "private bool TryMoveBattlePreparationPlacement(",
        "private void SyncBattlePreparationGridOccupant("
    })
    {
        AssertTrue(!rootSource.Contains(rootMethod, StringComparison.Ordinal), $"WorldSiteRoot should not own battle-preparation map drag method {rootMethod}");
    }

    AssertTrue(
        controllerSource.Contains("dragContext.RequestPlacement == null", StringComparison.Ordinal) &&
        controllerSource.IndexOf("dragContext.RequestPlacement == null", StringComparison.Ordinal) <
        controllerSource.IndexOf("_deploymentTargetEvaluator.TryMoveToGridCell", StringComparison.Ordinal),
        "controller must confirm request-backed placement authority before mutating mirrored site placement state");
    AssertTrue(
        !controllerSource.Contains("WorldSiteUnitPlacement placement", StringComparison.Ordinal) &&
        !controllerSource.Contains("_resolveUnitFootprintSize(placement?.UnitTypeId)", StringComparison.Ordinal),
        "battle-preparation drag controller should not own peacetime site-placement footprint fallback");
}

internal static void WorldSiteRootDelegatesBattleRuntimeHeroFrame()
{
    string siteRootDir = Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites");
    string rootSource = ReadWorldSiteRootSource();
    string commandHudSource = File.ReadAllText(Path.Combine(siteRootDir, "WorldSiteRoot.BattleRuntimeCommandHud.cs"));
    string presenterPath = Path.Combine(siteRootDir, "BattleRuntimeHeroFramePresenter.cs");
    AssertTrue(File.Exists(presenterPath), "battle runtime hero frame binding should live in BattleRuntimeHeroFramePresenter");

    string presenterSource = File.ReadAllText(presenterPath);
    AssertTrue(
        presenterSource.Contains("internal sealed class BattleRuntimeHeroFramePresenter", StringComparison.Ordinal),
        "battle runtime hero frame presenter should be a focused internal Presentation collaborator");
    AssertTrue(
        rootSource.Contains("private BattleRuntimeHeroFramePresenter _battleRuntimeHeroFramePresenter", StringComparison.Ordinal),
        "WorldSiteRoot should own a focused runtime hero frame presenter");

    string refreshBody = ExtractMethodBody(commandHudSource, "private void RefreshBattleRuntimeHeroFrame()");
    AssertTrue(
        refreshBody.Contains("_battleRuntimeHeroFramePresenter.Refresh(", StringComparison.Ordinal),
        "WorldSiteRoot runtime hero frame refresh should delegate binding to the presenter");
    AssertTrue(
        !commandHudSource.Contains("private void RefreshBattleRuntimeSkillList(", StringComparison.Ordinal),
        "WorldSiteRoot should not own battle-runtime skill slot list binding");
    AssertTrue(
        !refreshBody.Contains("BattleRuntimeCommandHudPresentation.SetProgressBar", StringComparison.Ordinal) &&
        !commandHudSource.Contains("GameUiSceneFactory.CreateBattleRuntimeSkillSlot", StringComparison.Ordinal),
        "WorldSiteRoot should not bind progress bars or instantiate skill slots directly");
    AssertTrue(
        presenterSource.Contains("BattleRuntimeCommandHudPresentation.SetProgressBar", StringComparison.Ordinal) &&
        presenterSource.Contains("using Rpg.Presentation.Common;", StringComparison.Ordinal) &&
        presenterSource.Contains("GameUiSceneFactory.CreateBattleRuntimeSkillSlot", StringComparison.Ordinal) &&
        !presenterSource.Contains("_regroupButton", StringComparison.Ordinal),
        "runtime hero frame presenter should own progress-bar and skill-slot binding");
}

internal static void WorldSiteRootCentralizesWorldSiteHudNodeRefs()
{
    string siteRootDir = Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites");
    string siteManagementPath = Path.Combine(siteRootDir, "WorldSiteRoot.SiteManagementHud.cs");
    string nodeRefsPath = Path.Combine(siteRootDir, "WorldSitePeacetimeHudNodeRefs.cs");
    AssertTrue(File.Exists(nodeRefsPath), "world-site HUD scene node lookups should live in WorldSitePeacetimeHudNodeRefs");

    string siteManagementSource = File.ReadAllText(siteManagementPath);
    string nodeRefsSource = File.ReadAllText(nodeRefsPath);
    string buildHudBody = ExtractMethodBody(siteManagementSource, "private void BuildSiteHud()");

    AssertTrue(
        buildHudBody.Contains("WorldSitePeacetimeHudNodeRefs.Resolve", StringComparison.Ordinal) &&
        !buildHudBody.Contains("GameUiSceneFactory.GetRequiredNode<", StringComparison.Ordinal),
        "BuildSiteHud should instantiate the HUD and wire callbacks, while a focused node refs class owns deep scene paths");
    foreach (string required in new[]
    {
        "internal sealed class WorldSitePeacetimeHudNodeRefs",
        "Resolve(Control root, string ownerName)",
        "GameUiSceneFactory.GetRequiredNode",
        "TopBarHost/TopLeftStatus",
        "TopBarHost/TopLeftStatus/Margin/SiteResourceLabel",
        "OverlayHost/SiteManagementTabRail",
        "OverlayHost/SitePeacetimePanel",
        "BottomCommandHost/BattleRuntimePauseDetailPanel/PauseDetailMargin/PauseDetailStack/PauseDetailBody/BattleRuntimeHeroFramePanel/HeroFrameMargin/BattleRuntimeHeroFrame/BattleRuntimeHeroInfoStack/BattleRuntimeHeroNameLabel",
        "OverlayHost/BattlePreparationRosterDock",
        "MinimapHost/BattlePreparationObjectiveThumbnailDock/BattlePreparationObjectiveThumbnail"
    })
    {
        AssertTrue(nodeRefsSource.Contains(required, StringComparison.Ordinal), $"world-site HUD node refs should own binding fragment={required}");
    }

    AssertTrue(
        !nodeRefsSource.Contains("Rpg.Application", StringComparison.Ordinal) &&
        !nodeRefsSource.Contains("Rpg.Runtime", StringComparison.Ordinal),
        "world-site HUD node refs should only locate Presentation scene nodes and must not gain Application or Runtime authority");
}

internal static void WorldSiteRootDelegatesBattleObjectivePlanningHudBinding()
{
    string siteRootDir = Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites");
    string rootSource = ReadWorldSiteRootSource();
    string planningSource = File.ReadAllText(Path.Combine(siteRootDir, "WorldSiteRoot.BattleObjectivePlanningHud.cs"));
    string preparationHudSource = File.ReadAllText(Path.Combine(siteRootDir, "WorldSiteRoot.BattlePreparationHud.cs"));
    string binderPath = Path.Combine(siteRootDir, "BattleObjectivePlanningHudBinder.cs");
    AssertTrue(File.Exists(binderPath), "battle objective planning HUD binding should live in BattleObjectivePlanningHudBinder");

    string binderSource = File.ReadAllText(binderPath);
    AssertTrue(
        binderSource.Contains("internal sealed class BattleObjectivePlanningHudBinder", StringComparison.Ordinal),
        "battle objective planning HUD binder should be a focused internal Presentation collaborator");
    AssertTrue(
        rootSource.Contains("private readonly BattleObjectivePlanningHudBinder _battleObjectivePlanningHudBinder", StringComparison.Ordinal),
        "WorldSiteRoot should own a focused battle objective planning HUD binder");

    string dialogBody = ExtractMethodBody(planningSource, "private void BindBattleObjectiveMapDialog()");
    string thumbnailBody = ExtractMethodBody(preparationHudSource, "private void BindBattlePreparationObjectiveThumbnail()");
    AssertTrue(
        dialogBody.Contains("_battleObjectivePlanningHudBinder.BindDialog(", StringComparison.Ordinal),
        "battle objective dialog binding should delegate to the objective planning HUD binder");
    AssertTrue(
        thumbnailBody.Contains("_battleObjectivePlanningHudBinder.BindThumbnail(", StringComparison.Ordinal),
        "battle preparation objective thumbnail binding should reuse the objective planning HUD binder");

    foreach (string rootMethod in new[]
    {
        "private IReadOnlyList<BattleObjectiveCompanyOption> BuildBattleObjectiveCompanyOptions(",
        "private string ResolveBattlePreparationPlanObjectiveLabel(",
        "private IReadOnlyList<BattleObjectiveMapCell> BuildBattleObjectiveMapCells(",
        "private IReadOnlyList<BattleObjectiveMapRegion> BuildBattleObjectiveMapRegions(",
        "private static BattleObjectiveMapRegion ToBattleObjectiveMapRegion("
    })
    {
        AssertTrue(!rootSource.Contains(rootMethod, StringComparison.Ordinal), $"WorldSiteRoot should not own battle objective planning binder method {rootMethod}");
    }

    foreach (string required in new[]
    {
        "BuildBattleObjectiveCompanyOptions",
        "BuildBattleObjectiveMapCells",
        "BuildBattleObjectiveMapRegions",
        "BattleGridTerrainQueries.IsWater",
        "Selectable = selectable"
    })
    {
        AssertTrue(binderSource.Contains(required, StringComparison.Ordinal), $"objective planning HUD binder should retain binding behavior fragment={required}");
    }

    foreach (string forbidden in new[]
    {
        "StrategicWorldRuntime",
        "WorldActionResolver",
        "WorldFacilitySlot",
        "BattleRuntimeSessionController",
        "AdvanceFixedTick",
        "ApplyBattleResultToWorld"
    })
    {
        AssertTrue(!binderSource.Contains(forbidden, StringComparison.Ordinal), $"objective planning HUD binder must stay read-only Presentation binding and must not use {forbidden}");
    }
}

}
