internal static partial class WorldSiteDeploymentCacheRegressionCases
{
internal static void ResourceDirectoryTaxonomyKeepsAssetsRawAndResourcesAuthored()
{
    string root = ProjectRoot();
    string resourceRoot = Path.Combine(root, "resource");
    AssertTrue(Directory.Exists(resourceRoot), $"authored resource root should exist path={resourceRoot}");

    string projectConfig = File.ReadAllText(Path.Combine(root, "project.godot"));
    AssertTrue(
        projectConfig.Contains("\"res://resource/\": \"orange\"", StringComparison.Ordinal),
        "project folder colors should register res://resource/ as the authored-resource root");

    string assetsRoot = Path.Combine(root, "assets");
    AssertTrue(Directory.Exists(assetsRoot), $"raw asset root should exist path={assetsRoot}");

    Dictionary<string, int> expectedLegacyBuckets = new()
    {
        ["LimboAI behavior trees"] = 2,
        ["Battle skill definitions"] = 5,
        ["UI themes/styleboxes"] = 23,
        ["TileSets"] = 3,
        ["Shaders"] = 5,
        ["Building AtlasTexture icons"] = 8,
        ["Unit definitions"] = 697,
        ["Unit visual definitions"] = 697,
        ["Unit audio definitions"] = 2,
        ["Legacy unit visual support resources"] = 3,
        ["SpriteFrames preview packages"] = 904
    };
    Dictionary<string, int> actualLegacyBuckets = expectedLegacyBuckets.Keys.ToDictionary(key => key, _ => 0);
    List<string> violations = new();

    foreach (string file in Directory.EnumerateFiles(assetsRoot, "*", SearchOption.AllDirectories))
    {
        string extension = Path.GetExtension(file);
        if (!string.Equals(extension, ".tres", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(extension, ".gdshader", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(extension, ".tscn", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
        string bucket = ClassifyLegacyAuthoredAssetResource(relative);
        if (!actualLegacyBuckets.ContainsKey(bucket))
        {
            violations.Add(relative);
            continue;
        }

        actualLegacyBuckets[bucket]++;
    }

    AssertTrue(
        violations.Count == 0,
        $"assets/ should contain only raw media plus explicitly inventoried legacy authored resources: {string.Join(", ", violations)}");
    foreach ((string bucket, int expectedCount) in expectedLegacyBuckets)
    {
        AssertTrue(
            actualLegacyBuckets[bucket] == expectedCount,
            $"legacy authored asset bucket changed before its migration batch bucket={bucket} expected={expectedCount} actual={actualLegacyBuckets[bucket]}");
    }
}

static string ClassifyLegacyAuthoredAssetResource(string relative)
{
    if (relative.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase))
    {
        return "Scenes are not assets";
    }

    string[] parts = relative.Split('/');
    if (relative.EndsWith("/frames.tres", StringComparison.OrdinalIgnoreCase))
    {
        return "SpriteFrames preview packages";
    }

    if (relative.EndsWith(".gdshader", StringComparison.OrdinalIgnoreCase))
    {
        return IsUnder(relative, "assets/battle/shaders/") || IsUnder(relative, "assets/world/shaders/")
            ? "Shaders"
            : "Unknown shader";
    }

    if (!relative.EndsWith(".tres", StringComparison.OrdinalIgnoreCase))
    {
        return "Raw asset";
    }

    if (parts.Length == 4 && parts[0] == "assets" && parts[1] == "ai" && parts[2] == "battle")
    {
        return "LimboAI behavior trees";
    }

    if (parts.Length == 4 && parts[0] == "assets" && parts[1] == "battle" && parts[2] == "skills")
    {
        return "Battle skill definitions";
    }

    if (parts.Length == 4 && parts[0] == "assets" && parts[1] == "themes" && parts[2] == "game-ui-skin")
    {
        return "UI themes/styleboxes";
    }

    if (parts.Length >= 4 && parts[0] == "assets" && parts[1] == "tilesets")
    {
        return "TileSets";
    }

    if (parts.Length == 6 &&
        parts[0] == "assets" &&
        parts[1] == "textures" &&
        parts[2] == "world" &&
        parts[3] == "Buildings" &&
        parts[4] == "Foundation" &&
        parts[5].EndsWith("_icon.tres", StringComparison.OrdinalIgnoreCase))
    {
        return "Building AtlasTexture icons";
    }

    if ((parts.Length == 4 || parts.Length == 5) &&
        parts[0] == "assets" &&
        parts[1] == "battle" &&
        parts[2] == "unit_visuals")
    {
        return "Legacy unit visual support resources";
    }

    if (parts.Length == 6 &&
        parts[0] == "assets" &&
        parts[1] == "battle" &&
        parts[2] == "units" &&
        parts[5] == "unit.tres")
    {
        return "Unit definitions";
    }

    if (parts.Length == 6 &&
        parts[0] == "assets" &&
        parts[1] == "battle" &&
        parts[2] == "units" &&
        parts[5] == "visual.tres")
    {
        return "Unit visual definitions";
    }

    if (parts.Length == 7 &&
        parts[0] == "assets" &&
        parts[1] == "battle" &&
        parts[2] == "units" &&
        parts[5] == "audio" &&
        parts[6] == "audio.tres")
    {
        return "Unit audio definitions";
    }

    return "Unknown authored resource";
}

static bool IsUnder(string relative, string prefix)
{
    return relative.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
}
}
