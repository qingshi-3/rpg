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
        siteScene.Contains("BattleRuntimeRegroupButton", StringComparison.Ordinal),
        "battle runtime HUD should author a persistent hero frame with HP, mana, a skill list, and regroup controls");
    AssertTrue(
        File.Exists(Path.Combine(root, "scenes", "world", "ui", "BattleRuntimeSkillSlot.tscn")) &&
        File.Exists(Path.Combine(root, "src", "Presentation", "World", "Sites", "BattleRuntimeSkillSlot.cs")) &&
        File.Exists(Path.Combine(root, "scenes", "world", "ui", "BattleRuntimeHeroSwitchButton.tscn")) &&
        File.Exists(Path.Combine(root, "src", "Presentation", "World", "Sites", "BattleRuntimeHeroSwitchButton.cs")),
        "runtime hero and skill controls should use reusable authored scenes instead of hardcoded single buttons");
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
        siteManagementSource.Contains("_battleRuntimeRegroupButton", StringComparison.Ordinal),
        "WorldSiteRoot should bind the authored runtime hero frame controls");
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

internal static void BattleRuntimeTargetPickingLeavesCommandHudClickable()
{
    string rootSource = ReadWorldSiteRootSource();
    string presentationSource = ReadWorldSitePresentationSource();
    string targetInputBody = ExtractMethodBody(rootSource, "private bool TryHandleBattleRuntimeHeroSkillTargetInput(InputEvent inputEvent)");
    int gateIndex = targetInputBody.IndexOf(
        "BattleRuntimeCommandHudPointerGate.ContainsPointer(_battleRuntimeCommandBar, mouseButton.Position)",
        StringComparison.Ordinal);
    int rightClickIndex = targetInputBody.IndexOf("mouseButton.ButtonIndex == MouseButton.Right", StringComparison.Ordinal);
    int handledIndex = targetInputBody.IndexOf("GetViewport()?.SetInputAsHandled()", StringComparison.Ordinal);

    AssertTrue(
        gateIndex >= 0 &&
        rightClickIndex >= 0 &&
        handledIndex >= 0 &&
        gateIndex < rightClickIndex &&
        gateIndex < handledIndex,
        "target-picking input should ignore command HUD pointer clicks before treating them as battlefield cancel or target clicks");
    AssertTrue(
        presentationSource.Contains("internal static class BattleRuntimeCommandHudPointerGate", StringComparison.Ordinal) &&
        presentationSource.Contains("control.GetGlobalRect().HasPoint(globalPosition)", StringComparison.Ordinal),
        "HUD pointer gating should live in a focused presentation helper instead of growing WorldSiteRoot");
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
        pausePresentationBody.IndexOf("_battleRuntimeCommandBar.Visible = false", StringComparison.Ordinal) <
        pausePresentationBody.IndexOf("_battleRuntimeCommandBar.Visible = true", StringComparison.Ordinal),
        "turning tactical pause off should hide the hero switch and skill command bar instead of leaving it on screen");
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
    string panelVisibilityBody = ExtractMethodBody(siteManagementSource, "private void UpdateSitePeacetimePanelVisibility(");

    AssertTrue(
        resolveRectBody.Contains("_battleRuntimeEnabled", StringComparison.Ordinal) &&
        resolveRectBody.Contains("new Rect2(Vector2.Zero, rootViewportSize)", StringComparison.Ordinal),
        "battle runtime should resolve the world viewport to fullscreen instead of reserving left or bottom HUD workspace");
    AssertTrue(
        pausePresentationBody.Contains("_sitePeacetimePanel.Visible = false", StringComparison.Ordinal) &&
        !pausePresentationBody.Contains("UpdateSitePeacetimePanelVisibility(\"battle_runtime_command_pause\")", StringComparison.Ordinal),
        "tactical pause should keep the left primary panel hidden and not re-run management panel visibility");
    AssertTrue(
        panelVisibilityBody.Contains("IsBattleRuntimeHudActive()", StringComparison.Ordinal) &&
        panelVisibilityBody.Contains("_sitePeacetimePanel.Visible = false", StringComparison.Ordinal) &&
        panelVisibilityBody.IndexOf("IsBattleRuntimeHudActive()", StringComparison.Ordinal) <
        panelVisibilityBody.IndexOf("bool shouldShow", StringComparison.Ordinal),
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
        destinationInputBody.Contains("BattleRuntimeCommandHudPointerGate.ContainsPointer", StringComparison.Ordinal) &&
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
