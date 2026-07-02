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
        siteManagementSource.Contains("_battleRuntimeHeroSelectorPresenter", StringComparison.Ordinal) &&
        siteManagementSource.Contains("_battleRuntimeHeroNameLabel", StringComparison.Ordinal) &&
        siteManagementSource.Contains("_battleRuntimeHeroHealthBar", StringComparison.Ordinal) &&
        siteManagementSource.Contains("_battleRuntimeHeroManaBar", StringComparison.Ordinal) &&
        siteManagementSource.Contains("_battleRuntimeHeroSkillList", StringComparison.Ordinal) &&
        siteManagementSource.Contains("_battleRuntimeRegroupButton", StringComparison.Ordinal),
        "WorldSiteRoot should bind the authored runtime hero frame controls");
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
        File.ReadAllText(Path.Combine(root, "assets", "battle", "skills", "skill_shield_barrier.tres")),
        File.ReadAllText(Path.Combine(root, "assets", "battle", "skills", "skill_sun_piercer.tres")),
        File.ReadAllText(Path.Combine(root, "assets", "battle", "skills", "skill_thunder_tag_throw.tres")),
        File.ReadAllText(Path.Combine(root, "assets", "battle", "skills", "skill_thunder_mark_fold.tres")),
        File.ReadAllText(Path.Combine(root, "assets", "battle", "skills", "skill_thunder_spiral_break.tres"))
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
}
