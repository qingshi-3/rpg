internal static partial class WorldSiteDeploymentCacheRegressionCases
{
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
        siteScene.Contains("BattleRuntimeHeroNameLabel", StringComparison.Ordinal) &&
        siteScene.Contains("BattleRuntimeHeroStateLabel", StringComparison.Ordinal) &&
        siteScene.Contains("BattleRuntimeHeroHealthBar", StringComparison.Ordinal) &&
        siteScene.Contains("BattleRuntimeHeroManaBar", StringComparison.Ordinal) &&
        siteScene.Contains("BattleRuntimeHeroSkillList", StringComparison.Ordinal) &&
        siteScene.Contains("BattleRuntimeRegroupButton", StringComparison.Ordinal),
        "battle runtime HUD should author a persistent hero frame with HP, mana, a skill list, and regroup controls");
    AssertTrue(
        File.Exists(Path.Combine(root, "scenes", "world", "ui", "BattleRuntimeSkillSlot.tscn")) &&
        File.Exists(Path.Combine(root, "src", "Presentation", "World", "Sites", "BattleRuntimeSkillSlot.cs")),
        "runtime skills should use a reusable authored skill-slot scene instead of a hardcoded single button");
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
        siteManagementSource.Contains("_battleRuntimeHeroNameLabel", StringComparison.Ordinal) &&
        siteManagementSource.Contains("_battleRuntimeHeroHealthBar", StringComparison.Ordinal) &&
        siteManagementSource.Contains("_battleRuntimeHeroManaBar", StringComparison.Ordinal) &&
        siteManagementSource.Contains("_battleRuntimeHeroSkillList", StringComparison.Ordinal) &&
        siteManagementSource.Contains("_battleRuntimeRegroupButton", StringComparison.Ordinal),
        "WorldSiteRoot should bind the authored runtime hero frame controls");
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
    string skillDefinitions = File.ReadAllText(Path.Combine(
        root,
        "src",
        "Definitions",
        "Battle",
        "Skills",
        "FirstSliceBattleSkillDefinitions.cs"));

    AssertTrue(
        sceneFactorySource.Contains("BattleRuntimeSkillSlotScenePath", StringComparison.Ordinal) &&
        sceneFactorySource.Contains("CreateBattleRuntimeSkillSlot", StringComparison.Ordinal),
        "runtime skill list should instantiate reusable authored skill-slot resources");
    AssertTrue(
        rootSource.Contains("RefreshBattleRuntimeSkillList", StringComparison.Ordinal) &&
        rootSource.Contains("CreateBattleRuntimeSkillSlot", StringComparison.Ordinal) &&
        rootSource.Contains("skill.DisplayName", StringComparison.Ordinal) &&
        !rootSource.Contains("_battleRuntimeHeroSkillButton", StringComparison.Ordinal),
        "battle runtime HUD should populate skill slots from skill snapshots instead of hardcoding one '技' button");
    AssertTrue(
        slotSource.Contains("public void Bind(", StringComparison.Ordinal) &&
        slotSource.Contains("StatusText", StringComparison.Ordinal) &&
        slotSource.Contains("CooldownRemainingSeconds", StringComparison.Ordinal) &&
        slotSource.Contains("EmitSignal(SignalName.Pressed", StringComparison.Ordinal),
        "runtime skill slot should expose bindable state for future cooldown text and current lock status");
    AssertTrue(
        skillDefinitions.Contains("DisplayName = \"破阵\"", StringComparison.Ordinal),
        "first-slice skill display name should be readable Chinese in the runtime skill list");
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
