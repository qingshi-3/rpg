using System.Text.Json;
using Godot;
using Rpg.Presentation.World.Preview;

namespace StrategicRegionPreviewRegression;

internal static class PreviewRegressionCases
{
    private const string PreviewScenePath = "scenes/world/preview/StrategicRegionPreview.tscn";
    private const string OverlayScenePath = "scenes/world/preview/StrategicRegionOverlayChunk.tscn";
    private const string OverlayShaderPath = "resource/world/preview/strategic_region_overlay.gdshader";
    private const string CityScenePath = "scenes/world/preview/StrategicCityAnchorVisual.tscn";
    private const string HudScenePath = "scenes/world/preview/StrategicRegionPreviewHud.tscn";
    private const string ConfigPath = "resource/world/preview/strategic_region_preview_config.tres";

    public static void Run(string name, Action test)
    {
        try
        {
            test();
            Console.WriteLine($"PASS: {name}");
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"FAIL: {name}\n{exception}");
            System.Environment.ExitCode = 1;
            throw;
        }
    }

    public static void PreviewGeographyDefinesIrregularFiveAndSixRegionTopologies(string projectRoot)
    {
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(projectRoot, "config", "world", "maps", "mock_qinghe_chiyan", "source", "geography.json")));
        JsonElement root = document.RootElement;
        JsonElement locations = root.GetProperty("strategicLocations").GetProperty("features");
        JsonElement regions = root.GetProperty("locationGeometries").GetProperty("features");

        string[] cityIds = locations.EnumerateArray()
            .Where(feature => feature.GetProperty("properties").GetProperty("locationType").GetString() == "main-city")
            .Select(feature => feature.GetProperty("properties").GetProperty("provinceId").GetString() ?? "")
            .ToArray();

        AssertEqual(2, cityIds.Length, "preview geography must contain exactly two cities");
        AssertEqual(11, regions.GetArrayLength(), "preview geography must contain exactly eleven regions");
        AssertEqual(5, CountRegions(regions, "qinghe"), "Qinghe must own exactly five regions");
        AssertEqual(6, CountRegions(regions, "chiyan"), "Chiyan must own exactly six regions");

        AssertTopology(regions, "qinghe", [1, 2, 3, 3, 3]);
        AssertTopology(regions, "chiyan", [1, 1, 2, 2, 2, 2]);

        string outlinesText = ReadRequired(projectRoot, "assets/textures/world/masks/territory/region_outlines.json");
        using JsonDocument outlines = JsonDocument.Parse(outlinesText);
        JsonElement cities = outlines.RootElement.GetProperty("provinces");
        AssertTrue(CountReflexVertices(FindCityRing(cities, "qinghe")) >= 2, "Qinghe outline must remain visibly concave");
        AssertTrue(CountReflexVertices(FindCityRing(cities, "chiyan")) >= 2, "Chiyan outline must preserve its horseshoe concavity");
    }

    public static void PreviewSceneIsIndependentlyRunnableAndIsolated(string projectRoot)
    {
        string previewScene = ReadRequired(projectRoot, PreviewScenePath);
        string projectSettings = ReadRequired(projectRoot, "project.godot");
        string formalRoot = ReadRequired(projectRoot, "scenes/world/StrategicWorldRoot.tscn");

        AssertContains(previewScene, "StrategicRegionPreviewRoot", "preview scene must own its standalone root script");
        AssertContains(previewScene, "MapCameraController", "preview scene must reuse the map camera controller");
        AssertNotContains(projectSettings, PreviewScenePath.Replace('/', '\\'), "preview scene must not become the project main scene");
        AssertNotContains(projectSettings, PreviewScenePath, "preview scene must not become an autoload or main scene");
        AssertNotContains(formalRoot, "StrategicRegionPreview", "formal strategic root must not instance the preview");
    }

    public static void PreviewUsesReusableAuthoredScenesAndPresentationResources(string projectRoot)
    {
        string previewScene = ReadRequired(projectRoot, PreviewScenePath);
        ReadRequired(projectRoot, OverlayScenePath);
        ReadRequired(projectRoot, CityScenePath);
        ReadRequired(projectRoot, HudScenePath);
        ReadRequired(projectRoot, ConfigPath);

        AssertContains(previewScene, "StrategicRegionOverlayChunk.tscn", "preview must instance a reusable chunk overlay scene");
        AssertContains(previewScene, "StrategicCityAnchorVisual.tscn", "preview must instance a reusable city anchor scene");
        AssertContains(previewScene, "StrategicRegionPreviewHud.tscn", "preview must instance an authored HUD scene");
        AssertContains(previewScene, "strategic_region_preview_config.tres", "preview must use a presentation config resource");
    }

    public static void PreviewLoaderPreservesCanonicalCoordinates(string projectRoot)
    {
        StrategicRegionPreviewData data = StrategicRegionPreviewDataLoader.LoadFromProjectRoot(projectRoot);

        AssertEqual(2, data.Cities.Count, "loader must return two cities");
        AssertEqual(11, data.Regions.Count, "loader must return eleven regions");
        AssertEqual(11, data.Regions.Select(region => region.MaskId).Distinct().Count(), "every region must retain a unique mask id");
        AssertEqual(6, data.Chunks.Count, "preview bounds must intersect exactly six chunks");

        StrategicRegionPreviewCity firstCity = data.Cities.OrderBy(city => city.CityId).First();
        AssertTrue(firstCity.WorldPosition.X >= data.PreviewBounds.Position.X, "city x must stay in canonical preview bounds");
        AssertTrue(firstCity.WorldPosition.Y >= data.PreviewBounds.Position.Y, "city y must stay in canonical preview bounds");
        AssertTrue(data.Regions.All(region => region.PolygonParts.Count > 0), "every region must retain polygon geometry");
    }

    public static void PreviewUsesChunkMaskOverlaysWithoutRegionPolygons(string projectRoot)
    {
        string preview = ReadRequired(projectRoot, PreviewScenePath);
        string overlay = ReadRequired(projectRoot, OverlayScenePath);
        string shader = ReadRequired(projectRoot, OverlayShaderPath);
        string rootCode = ReadRequired(projectRoot, "src/Presentation/World/Preview/StrategicRegionPreviewRoot.cs");

        AssertNotContains(preview, "StrategicRegionVisual.tscn", "preview must not instance per-region visuals");
        AssertNotContains(preview, "StrategicCityTerritoryVisual.tscn", "preview must not instance polygon territory visuals");
        AssertNotContains(overlay, "Polygon2D", "chunk overlay must use one rectangular sprite, not polygon triangulation");
        AssertContains(overlay, "strategic_region_overlay_material.tres", "chunk overlay must author one mask material");
        AssertContains(shader, "region_id", "overlay shader must decode region ids from the territory mask");
        AssertContains(shader, "mask_pixel_size", "overlay shader must derive soft borders from mask neighbours");
        AssertContains(shader, "hover_gate", "hover emphasis must be gated by the center region id");
        AssertContains(shader, "faction_color", "border color must derive from the center region faction");
        AssertNotContains(shader, "frost_color", "neutral frost color must not tint territory borders");
        AssertNotContains(shader, "hint_screen_texture", "overlay must not depend on the screen back buffer");
        AssertContains(rootCode, "GetPixel", "preview picking must resolve region ids from the mask image");
        AssertNotContains(preview, "BackBufferCopy", "mask overlay must not require a screen back buffer");
    }

    public static void PreviewUsesMaskMetadataLookup(string projectRoot)
    {
        string shader = ReadRequired(projectRoot, OverlayShaderPath);
        string rootCode = ReadRequired(projectRoot, "src/Presentation/World/Preview/StrategicRegionPreviewRoot.cs");
        string overlayCode = ReadRequired(projectRoot, "src/Presentation/World/Preview/StrategicRegionOverlayChunk.cs");

        AssertContains(shader, "region_metadata", "shader must resolve faction and city membership from metadata indexed by mask id");
        AssertContains(shader, "metadata_for_id", "shader must use one categorical metadata lookup path");
        AssertContains(shader, "context_city_id", "context membership must compare against data-driven city metadata");
        AssertNotContains(shader, "player_region_ids", "shader must not retain four-id player membership");
        AssertNotContains(shader, "hostile_region_ids", "shader must not retain four-id hostile membership");
        AssertNotContains(shader, "context_region_ids", "shader must not retain four-id context membership");
        AssertContains(rootCode, "MaskMetadataWidth = 256", "metadata must address every categorical mask id from 0 through 255");
        AssertContains(rootCode, "BuildRegionMetadataTexture", "preview must build metadata from loaded region and city data");
        AssertNotContains(rootCode, "BuildMaskIdVector", "preview must not retain a per-city four-region builder");
        AssertContains(overlayCode, "Texture2D regionMetadata", "every overlay chunk must receive the shared metadata lookup texture");
    }

    private static int CountRegions(JsonElement regions, string cityId)
    {
        return regions.EnumerateArray().Count(feature =>
            feature.GetProperty("properties").GetProperty("provinceId").GetString() == cityId);
    }

    private static void AssertTopology(JsonElement regions, string cityId, int[] expectedDegrees)
    {
        JsonElement[] cityRegions = regions.EnumerateArray()
            .Where(feature => feature.GetProperty("properties").GetProperty("provinceId").GetString() == cityId)
            .ToArray();
        Dictionary<string, List<int>> ownersByEdge = new(StringComparer.Ordinal);
        for (int regionIndex = 0; regionIndex < cityRegions.Length; regionIndex++)
        {
            JsonElement[] ring = cityRegions[regionIndex].GetProperty("geometry").GetProperty("coordinates")[0]
                .EnumerateArray().ToArray();
            AssertTrue(ring.Length >= 5, $"region {regionIndex} in {cityId} must be an authored irregular polygon");
            for (int pointIndex = 0; pointIndex < ring.Length - 1; pointIndex++)
            {
                string edge = CanonicalEdge(ring[pointIndex], ring[pointIndex + 1]);
                if (ownersByEdge.TryGetValue(edge, out List<int>? owners))
                {
                    owners.Add(regionIndex);
                }
                else
                {
                    ownersByEdge.Add(edge, new List<int> { regionIndex });
                }
            }
        }

        int[] degrees = Enumerable.Range(0, cityRegions.Length)
            .Select(index => ownersByEdge.Values.Count(owners => owners.Count == 2 && owners.Contains(index)))
            .OrderBy(value => value)
            .ToArray();
        AssertTrue(degrees.SequenceEqual(expectedDegrees), $"{cityId} must preserve its distinct exact-boundary connection pattern");
        AssertTrue(ownersByEdge.Values.All(owners => owners.Count <= 2), $"{cityId} must not have duplicate or ambiguous shared edges");
    }

    private static string CanonicalEdge(JsonElement left, JsonElement right)
    {
        string a = $"{left[0].GetDouble():R},{left[1].GetDouble():R}";
        string b = $"{right[0].GetDouble():R},{right[1].GetDouble():R}";
        return string.CompareOrdinal(a, b) <= 0 ? $"{a}|{b}" : $"{b}|{a}";
    }

    private static JsonElement[] FindCityRing(JsonElement cities, string cityId)
    {
        JsonElement city = cities.EnumerateArray().Single(element => element.GetProperty("provinceId").GetString() == cityId);
        return city.GetProperty("geometry").GetProperty("coordinates")[0].EnumerateArray().ToArray();
    }

    private static int CountReflexVertices(JsonElement[] closedRing)
    {
        double orientation = 0;
        for (int index = 0; index < closedRing.Length - 1; index++)
        {
            orientation += closedRing[index][0].GetDouble() * closedRing[index + 1][1].GetDouble()
                - closedRing[index + 1][0].GetDouble() * closedRing[index][1].GetDouble();
        }

        int reflex = 0;
        int count = closedRing.Length - 1;
        for (int index = 0; index < count; index++)
        {
            JsonElement previous = closedRing[(index + count - 1) % count];
            JsonElement current = closedRing[index];
            JsonElement next = closedRing[(index + 1) % count];
            double cross = (current[0].GetDouble() - previous[0].GetDouble()) * (next[1].GetDouble() - current[1].GetDouble())
                - (current[1].GetDouble() - previous[1].GetDouble()) * (next[0].GetDouble() - current[0].GetDouble());
            if (cross * orientation < 0) reflex++;
        }
        return reflex;
    }

    private static string ReadRequired(string projectRoot, string relativePath)
    {
        string path = Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Required preview file is missing path={relativePath}");
        }

        return File.ReadAllText(path);
    }

    private static void AssertContains(string text, string expected, string message)
    {
        if (!text.Contains(expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{message}; missing={expected}");
        }
    }

    private static void AssertNotContains(string text, string rejected, string message)
    {
        if (text.Contains(rejected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{message}; rejected={rejected}");
        }
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message}; expected={expected} actual={actual}");
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
