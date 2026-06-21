internal static partial class WorldSiteDeploymentCacheRegressionCases
{
internal static void WorldSiteRootPartialSetStaysBelowAntiRotLineBudget()
{
    string siteRootDir = Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites");
    AssertTrue(Directory.Exists(siteRootDir), $"presentation source directory should exist path={siteRootDir}");

    List<string> files = Directory.GetFiles(siteRootDir, "WorldSiteRoot*.cs")
        .OrderBy(path => path)
        .ToList();
    AssertTrue(files.Count > 0, $"presentation source scan should include WorldSiteRoot partials dir={siteRootDir}");

    int totalLines = files.Sum(file => File.ReadAllLines(file).Length);
    AssertTrue(
        totalLines < 8200,
        $"WorldSiteRoot total line count should stay below 8200 actual={totalLines}. WorldSiteRoot is a known god-node pending UI redesign; do not grow it further\u2014extract into focused components/scenes instead");
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
        refreshBody.Contains("大地图结算", StringComparison.Ordinal),
        "strategic world resource bar should keep the transitional large-map settlement counter display");

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
        binderSource.Contains("StrategicMusterTemplateViewModel", StringComparison.Ordinal) &&
        binderSource.Contains("GameUiSceneFactory.CreateWorldMutedLine", StringComparison.Ordinal) &&
        binderSource.Contains("GameUiSceneFactory.CreateWorldPrimaryActionButton", StringComparison.Ordinal) &&
        binderSource.Contains("_selectBuildingForPlacement", StringComparison.Ordinal) &&
        binderSource.Contains("_createCorps", StringComparison.Ordinal) &&
        binderSource.Contains("_replenishCorps", StringComparison.Ordinal) &&
        binderSource.Contains("_toggleHeroAssignment", StringComparison.Ordinal) &&
        binderSource.Contains("Pressed +=", StringComparison.Ordinal),
        "strategic management dashboard binder should render strategic dashboard rows and command buttons through authored resources and callbacks");

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
    foreach (string requiredCallback in new[]
    {
        "OnStrategicBuildBuildingSelected",
        "OnStrategicCreateCorpsPressed",
        "OnStrategicReplenishCorpsPressed",
        "OnStrategicHeroAssignmentPressed"
    })
    {
        AssertTrue(
            buildHudBody.Contains(requiredCallback, StringComparison.Ordinal),
            $"WorldSiteRoot should pass strategic command callbacks into the dashboard binder callback={requiredCallback}");
    }

    foreach (string requiredCommand in new[]
    {
        "StrategicManagementRuntime.Commands.BuildCityBuilding(",
        "StrategicManagementRuntime.Commands.CreateCorps(",
        "StrategicManagementRuntime.Commands.ReplenishCorps(",
        "StrategicManagementRuntime.Commands.AssignCorpsToHero(",
        "StrategicManagementRuntime.Commands.UnassignCorpsFromHero("
    })
    {
        AssertTrue(
            rootSource.Contains(requiredCommand, StringComparison.Ordinal),
            $"WorldSiteRoot should route strategic dashboard intent through command service call={requiredCommand}");
    }

    AssertTrue(
        !rootSource.Contains("OnStrategicBattlePreparationPressed", StringComparison.Ordinal) &&
        !rootSource.Contains("StrategicManagementRuntime.Commands.SelectBattlePreparation(", StringComparison.Ordinal),
        "WorldSiteRoot should not route deleted strategic battle-preparation commands");

    AssertTrue(
        rootSource.Contains("private void HandleStrategicManagementCommandResult(", StringComparison.Ordinal) &&
        rootSource.Contains("RefreshSiteManagementUi(", StringComparison.Ordinal),
        "WorldSiteRoot should refresh the strategic dashboard with a command-result notice after command submission");

    string buildBuildingBody = ExtractMethodBody(rootSource, "private void TrySubmitStrategicBuildingPlacement(");
    string createCorpsBody = ExtractMethodBody(rootSource, "private void OnStrategicCreateCorpsPressed(");
    string replenishCorpsBody = ExtractMethodBody(rootSource, "private void OnStrategicReplenishCorpsPressed(");
    string heroAssignmentBody = ExtractMethodBody(rootSource, "private void OnStrategicHeroAssignmentPressed(");
    string commandBodies = buildBuildingBody + createCorpsBody + replenishCorpsBody + heroAssignmentBody;

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
        createCorpsBody.Contains("TryResolveStrategicManagementCityId(_siteHudSiteId, out string cityId)", StringComparison.Ordinal) &&
        replenishCorpsBody.Contains("TryResolveStrategicManagementCityId(_siteHudSiteId, out string cityId)", StringComparison.Ordinal),
        "building placement, corps creation, and replenishment commands should guard command submission behind selected strategic city resolution");
    AssertTrue(
        heroAssignmentBody.Contains("dashboard.SelectedCity.CorpsInstances", StringComparison.Ordinal) &&
        heroAssignmentBody.Contains("TryResolveStrategicManagementCityId(_siteHudSiteId, out string cityId)", StringComparison.Ordinal) &&
        heroAssignmentBody.Contains("StrategicCorpsInstanceStatus.Garrisoned", StringComparison.Ordinal),
        "hero assignment should resolve a managed city and pick an available garrisoned corps from the current strategic dashboard");
    AssertTrue(
        !commandBodies.Contains("WorldActionResolver", StringComparison.Ordinal) &&
        !commandBodies.Contains("_worldActionResolver", StringComparison.Ordinal),
        "strategic dashboard command callbacks must not route through legacy world action authority");
}

internal static void WorldSiteRootShowsStrategicManagementPanelOnlyForPlayerCityManagement()
{
    string siteRootDir = Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites");
    string rootSource = File.ReadAllText(Path.Combine(siteRootDir, "WorldSiteRoot.SiteManagementHud.cs"));
    string visibilityBody = ExtractMethodBody(rootSource, "private void UpdateSitePeacetimePanelVisibility(");

    AssertTrue(
        rootSource.Contains("private bool ShouldShowSitePeacetimePanel(", StringComparison.Ordinal) &&
        visibilityBody.Contains("ShouldShowSitePeacetimePanel()", StringComparison.Ordinal),
        "site peacetime panel visibility should be delegated to a named helper that owns the player-city management gate");

    string helperBody = ExtractMethodBody(rootSource, "private bool ShouldShowSitePeacetimePanel(");
    foreach (string required in new[]
    {
        "_siteHudRoot?.Visible == true",
        "_isBattlePreparationActive",
        "_battleRuntimeEnabled",
        "CanOpenSiteDetail(ResolveSiteState(_siteHudSiteId))",
        "TryResolveStrategicManagementCityId(_siteHudSiteId, out _)"
    })
    {
        AssertTrue(
            helperBody.Contains(required, StringComparison.Ordinal),
            $"site peacetime panel should show only for player-owned city management fragment={required}");
    }
    AssertTrue(
        !helperBody.Contains("_selectedFacilitySlotId", StringComparison.Ordinal) &&
        !helperBody.Contains("_battleRuntimeCommandPauseActive", StringComparison.Ordinal) &&
        !helperBody.Contains("TryResolveStrategicManagementLocationId(_siteHudSiteId, out _)", StringComparison.Ordinal),
        "site peacetime panel visibility must not depend on retired slots, battle pause, or generic strategic-location mapping");
}

internal static void WorldSiteManagementHudUsesTabbedOperationLayout()
{
    string root = ProjectRoot();
    string scenePath = Path.Combine(root, "scenes", "world", "ui", "WorldSitePeacetimeHud.tscn");
    string refsPath = Path.Combine(root, "src", "Presentation", "World", "Sites", "WorldSitePeacetimeHudNodeRefs.cs");
    string binderPath = Path.Combine(root, "src", "Presentation", "World", "Sites", "StrategicManagementDashboardPanelBinder.cs");
    string siteHudPath = Path.Combine(root, "src", "Presentation", "World", "Sites", "WorldSiteRoot.SiteManagementHud.cs");
    string sheetStylePath = Path.Combine(root, "assets", "themes", "game-ui-skin", "basic_ui_1_panel_sheet.tres");

    AssertTrue(File.Exists(scenePath), $"site management HUD scene should exist path={scenePath}");
    AssertTrue(File.Exists(sheetStylePath), $"site management HUD should reuse the compact s_table.png StyleBox resource path={sheetStylePath}");

    string scene = File.ReadAllText(scenePath);
    string refsSource = File.ReadAllText(refsPath);
    string binderSource = File.ReadAllText(binderPath);
    string siteHudSource = File.ReadAllText(siteHudPath);
    string sheetStyle = File.ReadAllText(sheetStylePath);

    AssertTrue(
        sheetStyle.Contains("assets/textures/ui/basic-ui/1/s_table.png", StringComparison.Ordinal),
        "site management sheet StyleBox should use the compact s_table.png texture");
    AssertTrue(
        scene.Contains("basic_ui_1_panel_sheet.tres", StringComparison.Ordinal) &&
        ExtractSceneNodeBlock(scene, "[node name=\"SitePeacetimePanel\"").Contains("theme_override_styles/panel = ExtResource(\"3_panel_sheet\")", StringComparison.Ordinal),
        "site management outer frame should use the compact s_table.png StyleBox resource");
    string peacetimePanelBlock = ExtractSceneNodeBlock(scene, "[node name=\"SitePeacetimePanel\"");
    AssertTrue(
        peacetimePanelBlock.Contains("offset_left = 0.0", StringComparison.Ordinal) &&
        peacetimePanelBlock.Contains("offset_top = 0.0", StringComparison.Ordinal) &&
        peacetimePanelBlock.Contains("offset_right = 520.0", StringComparison.Ordinal) &&
        peacetimePanelBlock.Contains("offset_bottom = 0.0", StringComparison.Ordinal) &&
        siteHudSource.Contains("_sitePeacetimePanel.OffsetLeft = 0.0f", StringComparison.Ordinal) &&
        siteHudSource.Contains("_sitePeacetimePanel.OffsetTop = 0.0f", StringComparison.Ordinal) &&
        siteHudSource.Contains("_sitePeacetimePanel.OffsetRight = 520.0f", StringComparison.Ordinal) &&
        siteHudSource.Contains("_sitePeacetimePanel.OffsetBottom = 0.0f", StringComparison.Ordinal),
        "site management panel should fill the full left screen edge without outer gray gutters");

    AssertTrue(
        !scene.Contains("[node name=\"SiteTopBar\"", StringComparison.Ordinal) &&
        !scene.Contains("SiteOperationHintLabel", StringComparison.Ordinal),
        "site management should not keep a top header strip or a right-side resource panel over the map");
    AssertTrue(
        scene.Contains("[node name=\"SiteResourceLabel\" type=\"Label\" parent=\"LeftPrimaryPanelHost/SitePeacetimePanel/Margin/SiteManagementStack\"]", StringComparison.Ordinal) &&
        refsSource.Contains("LeftPrimaryPanelHost/SitePeacetimePanel/Margin/SiteManagementStack/SiteResourceLabel", StringComparison.Ordinal),
        "resources should be presented at the top of the left site-management workspace");

    foreach (string[] tab in new[]
    {
        new[] { "BuildTabButton", "建造" },
        new[] { "RecruitTabButton", "招兵" },
        new[] { "CorpsTabButton", "编制" },
        new[] { "OverviewTabButton", "总览" }
    })
    {
        string tabName = tab[0];
        string tabText = tab[1];
        string tabBlock = ExtractSceneNodeBlock(scene, $"[node name=\"{tabName}\"");
        AssertTrue(
            !string.IsNullOrWhiteSpace(tabBlock) &&
            tabBlock.Contains("custom_minimum_size = Vector2(76, 48)", StringComparison.Ordinal) &&
            tabBlock.Contains($"text = \"{tabText}\"", StringComparison.Ordinal) &&
            tabBlock.Contains("toggle_mode = true", StringComparison.Ordinal),
            $"site management tab should be a clear two-character toggle button tab={tabName}");
    }

    foreach (string required in new[]
    {
        "SiteManagementTabBar",
        "ManagementContentScroll",
        "SiteBuildSection",
        "SiteRecruitSection",
        "SiteCorpsSection",
        "SiteOverviewSection"
    })
    {
        AssertTrue(scene.Contains(required, StringComparison.Ordinal), $"tabbed site management scene should contain {required}");
        AssertTrue(refsSource.Contains(required, StringComparison.Ordinal), $"node refs should bind tabbed site management node {required}");
    }

    AssertTrue(
        siteHudSource.Contains("SelectSiteManagementSection(") &&
        siteHudSource.Contains("ApplySiteManagementSectionVisibility("),
        "WorldSiteRoot should switch visible site-management sections from tab buttons");
    AssertTrue(
        binderSource.Contains("_recruitList", StringComparison.Ordinal) &&
        binderSource.Contains("_corpsList", StringComparison.Ordinal) &&
        binderSource.IndexOf("ClearChildren(_recruitList)", StringComparison.Ordinal) <
        binderSource.IndexOf("foreach (StrategicMusterTemplateViewModel template", StringComparison.Ordinal) &&
        binderSource.Contains("AddMutedLine(_corpsList, \"英雄编制\")", StringComparison.Ordinal),
        "strategic management binder should separate recruitment from corps/hero configuration instead of stacking everything into one action list");
}

internal static void WorldSiteBuildPickerUsesIconCardsAndMapPlacement()
{
    string root = ProjectRoot();
    string siteRootDir = Path.Combine(root, "src", "Presentation", "World", "Sites");
    string scenePath = Path.Combine(root, "scenes", "world", "ui", "WorldSitePeacetimeHud.tscn");
    string cardScenePath = Path.Combine(root, "scenes", "world", "ui", "WorldBuildingOptionCard.tscn");
    string cardSourcePath = Path.Combine(siteRootDir, "WorldBuildingOptionCard.cs");
    string binderPath = Path.Combine(siteRootDir, "StrategicManagementDashboardPanelBinder.cs");
    string nodeRefsPath = Path.Combine(siteRootDir, "WorldSitePeacetimeHudNodeRefs.cs");
    string factoryPath = Path.Combine(root, "src", "Presentation", "Common", "GameUiSceneFactory.cs");
    string siteHudPath = Path.Combine(siteRootDir, "WorldSiteRoot.SiteManagementHud.cs");

    AssertTrue(File.Exists(scenePath), $"world-site peacetime HUD scene should exist path={scenePath}");
    AssertTrue(File.Exists(cardScenePath), $"build picker card should be an authored reusable scene path={cardScenePath}");
    AssertTrue(File.Exists(cardSourcePath), $"build picker card should own its binding script path={cardSourcePath}");

    string scene = File.ReadAllText(scenePath);
    string cardScene = File.ReadAllText(cardScenePath);
    string cardSource = File.ReadAllText(cardSourcePath);
    string binderSource = File.ReadAllText(binderPath);
    string nodeRefsSource = File.ReadAllText(nodeRefsPath);
    string factorySource = File.ReadAllText(factoryPath);
    string siteHudSource = File.ReadAllText(siteHudPath);
    string rootSource = ReadWorldSiteRootSource();
    string buildListBlock = ExtractSceneNodeBlock(scene, "[node name=\"SiteBuildingOptionGrid\"");

    AssertTrue(
        buildListBlock.Contains("type=\"GridContainer\"", StringComparison.Ordinal) &&
        buildListBlock.Contains("columns = 4", StringComparison.Ordinal),
        "site-management build picker should be a compact inventory-style GridContainer, not a long text-button VBox list");
    AssertTrue(
        nodeRefsSource.Contains("internal GridContainer SiteBuildingOptionGrid", StringComparison.Ordinal) &&
        nodeRefsSource.Contains("Get<GridContainer>(root, \"LeftPrimaryPanelHost/SitePeacetimePanel/Margin/SiteManagementStack/ManagementContentScroll/ManagementContent/SiteBuildSection/SiteBuildingOptionGrid\"", StringComparison.Ordinal),
        "world-site HUD node refs should bind the build picker as GridContainer");

    AssertTrue(
        cardScene.Contains("[node name=\"WorldBuildingOptionCard\" type=\"Button\"", StringComparison.Ordinal) &&
        cardScene.Contains("res://src/Presentation/World/Sites/WorldBuildingOptionCard.cs", StringComparison.Ordinal) &&
        cardScene.Contains("[node name=\"Icon\" type=\"TextureRect\"", StringComparison.Ordinal) &&
        cardScene.Contains("[node name=\"NameLabel\" type=\"Label\"", StringComparison.Ordinal),
        "building option card should be a focused authored Button scene with icon and bottom name label");
    AssertTrue(
        cardSource.Contains("public void Bind(") &&
        cardSource.Contains("TextureRect") &&
        cardSource.Contains("TooltipText") &&
        cardSource.Contains("占地") &&
        cardSource.Contains("成本") &&
        !cardSource.Contains("DefaultRegion", StringComparison.Ordinal) &&
        !cardSource.Contains("DisabledReason", StringComparison.Ordinal) &&
        !cardSource.Contains("CategoryId", StringComparison.Ordinal),
        "building option card binding should expose only icon/name plus footprint and cost tooltip");

    AssertTrue(
        factorySource.Contains("WorldBuildingOptionCardScenePath = \"res://scenes/world/ui/WorldBuildingOptionCard.tscn\"", StringComparison.Ordinal) &&
        factorySource.Contains("CreateWorldBuildingOptionCard", StringComparison.Ordinal) &&
        factorySource.Contains("Instantiate<WorldBuildingOptionCard>(WorldBuildingOptionCardScenePath", StringComparison.Ordinal),
        "build option cards should be instantiated through GameUiSceneFactory");

    string bindOptionsBody = ExtractMethodBody(binderSource, "private void BindBuildingOptions(");
    AssertTrue(
        bindOptionsBody.Contains("GameUiSceneFactory.CreateWorldBuildingOptionCard", StringComparison.Ordinal) &&
        bindOptionsBody.Contains("_selectBuildingForPlacement?.Invoke(buildingDefinitionId)", StringComparison.Ordinal),
        "strategic dashboard binder should create building option cards and submit building selection, not direct placement");
    foreach (string forbidden in new[]
    {
        "CreateWorldPrimaryActionButton",
        "DefaultRegionId",
        "DefaultGridX",
        "DefaultGridY",
        "DisabledReason",
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
    string scenePath = Path.Combine(root, "scenes", "world", "sites", "WorldSiteRoot.tscn");

    AssertTrue(File.Exists(previewPath), "strategic building placement preview should live in a focused Presentation Node2D collaborator");

    string placementSource = File.ReadAllText(placementSourcePath);
    string mapPresentationSource = File.ReadAllText(mapPresentationPath);
    string resolverSource = File.ReadAllText(resolverPath);
    string previewSource = File.Exists(previewPath) ? File.ReadAllText(previewPath) : "";
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
        previewSource.Contains("StrategicBuildingPlacementPreview : Node2D", StringComparison.Ordinal) &&
        previewSource.Contains("SetPreview(") &&
        previewSource.Contains("Texture2D", StringComparison.Ordinal) &&
        previewSource.Contains("DrawTextureRect", StringComparison.Ordinal),
        "placement preview should be a viewport Node2D that renders the selected building texture");
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
        rootSource.Contains("_strategicBuildingPlacementPreview", StringComparison.Ordinal) &&
        scene.Contains("StrategicBuildingPlacementPreview.cs", StringComparison.Ordinal) &&
        scene.Contains("[node name=\"StrategicBuildingPlacementPreview\" type=\"Node2D\" parent=\"MainWorldViewportHost/MainWorldViewport/OverlayRoot\"]", StringComparison.Ordinal),
        "WorldSiteRoot should author and bind the strategic building placement preview under the world viewport overlay");
    AssertTrue(
        !placementSource.Contains("BuildFacility", StringComparison.Ordinal) &&
        !resolverSource.Contains("FacilitySlotDefinition", StringComparison.Ordinal) &&
        !resolverSource.Contains("WorldActionResolver", StringComparison.Ordinal),
        "strategic building placement preview and resolution must not route through legacy facility-slot authority");
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
        presenterSource.Contains("GameUiSceneFactory.CreateBattleRuntimeSkillSlot", StringComparison.Ordinal),
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
        "LeftPrimaryPanelHost/SitePeacetimePanel/Margin/SiteManagementStack/SiteResourceLabel",
        "LeftPrimaryPanelHost/SitePeacetimePanel",
        "BottomCommandHost/BattleRuntimeCommandBar/CommandMargin/BattleRuntimeHeroFrame/BattleRuntimeHeroInfoStack/BattleRuntimeHeroNameLabel",
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

internal static void WorldSiteRootDelegatesBattleRuntimeLivePresentationObservation()
{
    string siteRootDir = Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites");
    string rootSource = ReadWorldSiteRootSource();
    string incrementalSource = File.ReadAllText(Path.Combine(siteRootDir, "WorldSiteRoot.BattleRuntimeIncremental.cs"));
    string observerPath = Path.Combine(siteRootDir, "BattleRuntimeLivePresentationObserver.cs");
    AssertTrue(File.Exists(observerPath), "live runtime event observation should live in BattleRuntimeLivePresentationObserver");

    string observerSource = File.ReadAllText(observerPath);
    AssertTrue(
        observerSource.Contains("internal sealed class BattleRuntimeLivePresentationObserver", StringComparison.Ordinal),
        "runtime live presentation observer should be a focused internal Presentation collaborator");
    AssertTrue(
        rootSource.Contains("private readonly BattleRuntimeLivePresentationObserver _battleRuntimeLivePresentationObserver", StringComparison.Ordinal),
        "WorldSiteRoot should own a focused runtime live presentation observer");

    string advanceBody = ExtractMethodBody(incrementalSource, "private async Task AdvanceBattleGroupRuntimeOnLiveClockAsync(");
    AssertTrue(
        advanceBody.Contains("controller.AdvanceFixedTick(tickSeconds)", StringComparison.Ordinal) &&
        advanceBody.Contains("_battleRuntimeLivePresentationObserver.ObserveAsync(", StringComparison.Ordinal),
        "WorldSiteRoot should keep Runtime clock ownership while delegating emitted event observation");
    AssertTrue(
        advanceBody.Contains("new(_battleRuntimeLivePresentationObserver.BuildRuntimePlaybackEntityMap())", StringComparison.Ordinal),
        "WorldSiteRoot should create live presentation state from the observer-built entity map");

    foreach (string rootMethod in new[]
    {
        "private Task ObserveRuntimeEventsOnPresentationAsync(",
        "private double ObserveRuntimeMovementEvent(",
        "private async Task<double> ObserveRuntimeSkillUsedEventCoreAsync(",
        "private async Task<double> PlayRuntimeDamageFeedbackEventAsync(",
        "private async Task ApplyRuntimeDamageEventAsync(",
        "private Dictionary<string, BattleEntity> BuildRuntimePlaybackEntityMap("
    })
    {
        AssertTrue(!rootSource.Contains(rootMethod, StringComparison.Ordinal), $"WorldSiteRoot should not own runtime event observation method {rootMethod}");
    }

    foreach (string required in new[]
    {
        "BattleEventKind.ThunderMarkTeleported",
        "presentationState.ObserveActorTeleportNow",
        "BattleEventKind.MovementStarted",
        "restartMoveAnimation: false",
        "returnToIdleOnComplete: true",
        "TrackTargetDamage",
        "previousTargetDamageTail"
    })
    {
        AssertTrue(observerSource.Contains(required, StringComparison.Ordinal), $"runtime live presentation observer should retain event behavior fragment={required}");
    }

    foreach (string forbidden in new[]
    {
        "BattleRuntimeSessionController",
        "AdvanceFixedTick",
        "CompleteResolvedBattle",
        "ApplyBattleResultToWorld",
        "StrategicWorldRuntime"
    })
    {
        AssertTrue(!observerSource.Contains(forbidden, StringComparison.Ordinal), $"runtime live presentation observer must not own Runtime lifecycle or settlement fragment={forbidden}");
    }
}

internal static void WorldSiteRootDelegatesBattlePreparationHudBinding()
{
    string siteRootDir = Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites");
    string rootSource = ReadWorldSiteRootSource();
    string hudSource = File.ReadAllText(Path.Combine(siteRootDir, "WorldSiteRoot.BattlePreparationHud.cs"));
    string refreshSource = File.ReadAllText(Path.Combine(siteRootDir, "BattlePreparationRefresh.cs"));
    string binderPath = Path.Combine(siteRootDir, "BattlePreparationHudBinder.cs");
    AssertTrue(File.Exists(binderPath), "battle-preparation roster and plan-control binding should live in BattlePreparationHudBinder");

    string binderSource = File.ReadAllText(binderPath);
    AssertTrue(
        binderSource.Contains("internal sealed class BattlePreparationHudBinder", StringComparison.Ordinal),
        "battle-preparation HUD binder should be a focused internal Presentation collaborator");
    AssertTrue(
        rootSource.Contains("private readonly BattlePreparationHudBinder _battlePreparationHudBinder", StringComparison.Ordinal),
        "WorldSiteRoot should own a focused battle-preparation HUD binder");

    string rosterBody = ExtractMethodBody(hudSource, "private void BindBattlePreparationCompanyRoster()");
    string controlsBody = ExtractMethodBody(hudSource, "private void BindBattlePreparationCompactPlanControls()");
    AssertTrue(
        rosterBody.Contains("_battlePreparationHudBinder.BindCompanyRoster(", StringComparison.Ordinal),
        "battle-preparation company roster binding should delegate to the HUD binder");
    AssertTrue(
        controlsBody.Contains("_battlePreparationHudBinder.BindCompactPlanControls(", StringComparison.Ordinal),
        "battle-preparation compact plan controls should delegate to the HUD binder");
    AssertTrue(
        refreshSource.Contains("BindBattlePreparationCompanyRoster()", StringComparison.Ordinal) &&
        refreshSource.Contains("BindBattlePreparationCompactPlanControls()", StringComparison.Ordinal) &&
        !ExtractMethodBody(refreshSource, "private void RefreshBattlePreparationPlanUi(").Contains("RefreshBattlePreparationMapEntities", StringComparison.Ordinal),
        "plan-only refresh should keep using lightweight root routing while binding details live in the HUD binder");

    foreach (string rootMethod in new[]
    {
        "private void BindBattlePreparationRuleButton(",
        "private BattlePreparationCompanyPlanStatus ResolveBattlePreparationCompanyPlanStatus("
    })
    {
        AssertTrue(!rootSource.Contains(rootMethod, StringComparison.Ordinal), $"WorldSiteRoot should not own battle-preparation HUD binder method {rootMethod}");
    }

    foreach (string required in new[]
    {
        "GameUiSceneFactory.CreateBattlePreparationRosterRow",
        "row.Selected +=",
        "row.DragStarted +=",
        "BattlePreparationPlanUiModel.ResolveCompanyPlanStatus",
        "BattlePreparationPlanUiModel.ResolveObjectiveText",
        "CanLaunchPreparedBattle"
    })
    {
        AssertTrue(binderSource.Contains(required, StringComparison.Ordinal), $"battle-preparation HUD binder should retain binding behavior fragment={required}");
    }

    foreach (string forbidden in new[]
    {
        "ActivateBattleRuntime",
        "ExcludeUndeployedBattlePreparationReserveGroups",
        "SyncBattlePreparationRequestPlacements",
        "WorldActionResolver",
        "StrategicWorldRuntime"
    })
    {
        AssertTrue(!binderSource.Contains(forbidden, StringComparison.Ordinal), $"battle-preparation HUD binder must not own launch, settlement, or strategic authority fragment={forbidden}");
    }
}
}
