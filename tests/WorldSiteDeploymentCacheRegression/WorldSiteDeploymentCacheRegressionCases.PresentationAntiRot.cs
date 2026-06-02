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
}
