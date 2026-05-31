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

    files.Add(Path.Combine(root, "src", "Presentation", "Battle", "Entities", "BattleUnitRoot.cs"));
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
    string battlePreparationHudSource = File.ReadAllText(battlePreparationHudPath);
    AssertTrue(
        battlePreparationHudSource.Contains("GameUiSceneFactory", StringComparison.Ordinal),
        $"battle preparation dynamic UI rows should be created through GameUiSceneFactory file={battlePreparationHudPath}");
}
}
