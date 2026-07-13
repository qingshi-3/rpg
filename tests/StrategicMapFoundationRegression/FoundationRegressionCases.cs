using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Rpg.Application.StrategicMap;
using Rpg.Definitions.StrategicMap;

namespace StrategicMapFoundationRegression;

internal static class FoundationRegressionCases
{
    private const string AcceptedGeometryFingerprint = "03222CF71EF3315DF430C4627F8DADAD1C620E6BD9749D63D6099F396290C07B";

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
            Environment.ExitCode = 1;
            throw;
        }
    }

    public static void CanonicalGeographyAndChunkManifestLoad(string projectRoot)
    {
        StrategicMapCanonicalDefinition definition = LoadSelected(projectRoot);
        AssertEqual(3, definition.Geography.Version, "geography schema version");
        AssertEqual(2, definition.ChunkManifest.Version, "chunk manifest version");
        AssertEqual(35, definition.ChunkManifest.Chunks.Count, "canonical chunk count");
        AssertEqual(7168d, definition.ChunkManifest.WorldWidth, "world width");
        AssertEqual(5120d, definition.ChunkManifest.WorldHeight, "world height");
        AssertTrue(StrategicMapValidator.Validate(definition).Count == 0, "canonical definitions must validate without issues");
    }

    public static void CanonicalProvinceAndCityGeometryContract(string projectRoot)
    {
        StrategicMapCanonicalDefinition definition = LoadSelected(projectRoot);
        StrategicMapGeographyDefinition geography = definition.Geography;
        AssertEqual(2, geography.Provinces.Count, "province count");
        ProvinceDefinition qinghe = geography.Provinces.Single(province => province.ProvinceId == "qinghe");
        ProvinceDefinition chiyan = geography.Provinces.Single(province => province.ProvinceId == "chiyan");
        AssertEqual("qinghe_layout", qinghe.LayoutId, "Qinghe layout ownership");
        AssertEqual("chiyan_layout", chiyan.LayoutId, "Chiyan layout ownership");
        AssertTrue(typeof(ProvinceDefinition).GetProperty("CampaignRole") == null, "static geography must not own campaign roles");

        StrategicLocationDefinition[] cities = geography.Locations.Where(location =>
            location.LocationType is StrategicLocationType.MainCity or StrategicLocationType.AuxiliaryCity).ToArray();
        AssertEqual(11, cities.Length, "city location count");
        AssertEqual(5, cities.Count(location => location.ProvinceId == "qinghe"), "Qinghe member count");
        AssertEqual(6, cities.Count(location => location.ProvinceId == "chiyan"), "Chiyan member count");
        AssertEqual("qinghe_core", cities.Single(location => location.ProvinceId == "qinghe" && location.LocationType == StrategicLocationType.MainCity).LocationId, "Qinghe main city");
        AssertEqual("chiyan_high_basin", cities.Single(location => location.ProvinceId == "chiyan" && location.LocationType == StrategicLocationType.MainCity).LocationId, "Chiyan main city");
        AssertEqual(11, geography.LocationGeometries.Select(geometry => geometry.LocationId).Distinct(StringComparer.Ordinal).Count(), "one geometry per city");
        AssertEqual(AcceptedGeometryFingerprint, GeometryFingerprint(geography.LocationGeometries), "accepted geometry fingerprint");
    }

    public static void ValidatorRejectsBrokenContracts(string projectRoot)
    {
        StrategicMapCanonicalDefinition canonical = LoadSelected(projectRoot);
        StrategicMapChunkDefinition first = canonical.ChunkManifest.Chunks[0];
        StrategicMapChunkDefinition brokenChunk = first with { WorldOrigin = new StrategicMapPoint(1, 0) };
        StrategicMapChunkManifest brokenManifest = canonical.ChunkManifest with
        {
            Chunks = new[] { brokenChunk }.Concat(canonical.ChunkManifest.Chunks.Skip(1)).ToArray()
        };
        StrategicLocationDefinition firstCity = canonical.Geography.Locations.First(location =>
            location.LocationType is StrategicLocationType.MainCity or StrategicLocationType.AuxiliaryCity);
        StrategicMapGeographyDefinition brokenGeography = canonical.Geography with
        {
            Locations = canonical.Geography.Locations.Select(location =>
                location.LocationId == firstCity.LocationId ? location with { ProvinceId = "missing" } : location).ToArray()
        };

        string[] codes = StrategicMapValidator.Validate(new StrategicMapCanonicalDefinition(brokenManifest, brokenGeography))
            .Select(issue => issue.Code)
            .ToArray();
        AssertTrue(codes.Contains("CHUNK_ORIGIN_MISMATCH", StringComparer.Ordinal), "validator must reject chunk origin drift");
        AssertTrue(codes.Contains("LOCATION_PROVINCE_UNKNOWN", StringComparer.Ordinal), "validator must reject unknown province membership");
        AssertTrue(codes.Contains("LOCATION_GEOMETRY_PROVINCE_MISMATCH", StringComparer.Ordinal), "validator must reject geometry membership mismatch");
    }

    public static void PackageLoaderRejectsTamperingAndCrossRevisionReferences(string projectRoot)
    {
        string validRoot = CopyFixturePackage(projectRoot);
        try
        {
            StrategicMapPackageSelection validSelection = FixtureSelection(validRoot);
            _ = StrategicMapPackageLoader.LoadSelected(validRoot, validSelection);
        }
        finally
        {
            Directory.Delete(validRoot, recursive: true);
        }

        AssertFixtureRejected(projectRoot, (root, package, _) =>
        {
            string geographyPath = ResolveResource(root, package["geographyPath"]!.GetValue<string>());
            File.AppendAllText(geographyPath, " ");
        }, "artifact hash mismatch");
        AssertFixtureRejected(projectRoot, (root, package, chunks) =>
        {
            string visualPath = ResolveResource(root, chunks["chunks"]![0]!["visualTexturePath"]!.GetValue<string>());
            using FileStream stream = File.Open(visualPath, FileMode.Append, FileAccess.Write);
            stream.WriteByte(0);
        }, "artifact hash mismatch");
        AssertFixtureRejected(projectRoot, (_, package, _) =>
        {
            package["contentHash"] = new string('0', 64);
        }, "content hash mismatch");
        AssertFixtureRejected(projectRoot, (_, package, _) =>
        {
            string revision = package["revision"]!.GetValue<string>();
            package["geographyPath"] = package["geographyPath"]!.GetValue<string>().Replace(revision, "r-cross-revision", StringComparison.Ordinal);
        }, "outside immutable revision");
        AssertFixtureRejected(projectRoot, (_, package, chunks) =>
        {
            string revision = package["revision"]!.GetValue<string>();
            chunks["chunks"]![0]!["visualTexturePath"] = chunks["chunks"]![0]!["visualTexturePath"]!
                .GetValue<string>().Replace(revision, "r-cross-revision", StringComparison.Ordinal);
        }, "outside immutable revision");
    }

    private static StrategicMapCanonicalDefinition LoadSelected(string projectRoot)
    {
        StrategicMapPackageSelection selection = StrategicMapPackageLoader.LoadSelection(
            projectRoot,
            "res://config/world/strategic-map-selection.json");
        return StrategicMapPackageLoader.LoadSelected(projectRoot, selection).Canonical;
    }

    private static void AssertFixtureRejected(
        string projectRoot,
        Action<string, JsonObject, JsonObject> mutate,
        string expectedMessage)
    {
        string isolatedRoot = CopyFixturePackage(projectRoot);
        try
        {
            StrategicMapPackageSelection selection = FixtureSelection(isolatedRoot);
            string packagePath = ResolveResource(isolatedRoot, selection.PackageManifestPath);
            JsonObject originalPackage = JsonNode.Parse(File.ReadAllText(packagePath))!.AsObject();
            JsonObject package = originalPackage.DeepClone().AsObject();
            string chunkPath = ResolveResource(isolatedRoot, package["chunkManifestPath"]!.GetValue<string>());
            JsonObject originalChunks = JsonNode.Parse(File.ReadAllText(chunkPath))!.AsObject();
            JsonObject chunks = originalChunks.DeepClone().AsObject();
            mutate(isolatedRoot, package, chunks);
            System.Text.Json.JsonSerializerOptions jsonOptions = new() { WriteIndented = true };
            if (!JsonNode.DeepEquals(package, originalPackage))
            {
                File.WriteAllText(packagePath, package.ToJsonString(jsonOptions));
            }
            if (!JsonNode.DeepEquals(chunks, originalChunks))
            {
                File.WriteAllText(chunkPath, chunks.ToJsonString(jsonOptions));
            }

            try
            {
                _ = StrategicMapPackageLoader.LoadSelected(isolatedRoot, selection);
            }
            catch (Exception exception) when (exception.Message.Contains(expectedMessage, StringComparison.Ordinal))
            {
                return;
            }
            throw new InvalidOperationException($"package mutation should be rejected with '{expectedMessage}'");
        }
        finally
        {
            Directory.Delete(isolatedRoot, recursive: true);
        }
    }

    private static string CopyFixturePackage(string projectRoot)
    {
        string isolatedRoot = Path.Combine(Path.GetTempPath(), $"rpg-map-package-integrity-{Guid.NewGuid():N}");
        StrategicMapPackageSelection selection = StrategicMapPackageLoader.LoadSelection(
            projectRoot,
            "res://config/world/strategic-map-selection-fixture.json");
        string packagePath = StrategicMapPackageLoader.ResolveProjectPath(projectRoot, selection.PackageManifestPath);
        JsonObject package = JsonNode.Parse(File.ReadAllText(packagePath))!.AsObject();
        string mapId = package["mapId"]!.GetValue<string>();
        string revision = package["revision"]!.GetValue<string>();
        CopyDirectory(
            Path.GetDirectoryName(packagePath)!,
            Path.Combine(isolatedRoot, "config", "world", "published", mapId, revision));
        CopyDirectory(
            Path.Combine(projectRoot, "assets", "textures", "world", "maps", mapId, revision),
            Path.Combine(isolatedRoot, "assets", "textures", "world", "maps", mapId, revision));
        return isolatedRoot;
    }

    private static StrategicMapPackageSelection FixtureSelection(string projectRoot)
    {
        string packagePath = Directory.GetFiles(
            Path.Combine(projectRoot, "config", "world", "published"),
            "package.json",
            SearchOption.AllDirectories).Single();
        string resourcePath = $"res://{Path.GetRelativePath(projectRoot, packagePath).Replace('\\', '/')}";
        return new StrategicMapPackageSelection(1, resourcePath, "res://unused-scenario.json");
    }

    private static string ResolveResource(string projectRoot, string resourcePath) =>
        Path.Combine(projectRoot, resourcePath["res://".Length..].Replace('/', Path.DirectorySeparatorChar));

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    public static void ModuleSourceStaysWithinPureFoundationLayers(string projectRoot)
    {
        string[] roots =
        {
            Path.Combine(projectRoot, "src", "Definitions", "StrategicMap"),
            Path.Combine(projectRoot, "src", "Application", "StrategicMap")
        };
        foreach (string path in roots.SelectMany(root => Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)))
        {
            foreach (string line in File.ReadLines(path).Select(line => line.Trim()))
            {
                if (!line.StartsWith("using ", StringComparison.Ordinal) || line.Count(character => character == ' ') != 1) continue;
                AssertTrue(
                    line.StartsWith("using System", StringComparison.Ordinal) ||
                    line == "using Rpg.Definitions.StrategicMap;",
                    $"module has an unexpected dependency path={path} using={line}");
            }
        }
    }

    private static string GeometryFingerprint(IReadOnlyList<LocationGeometryDefinition> geometries)
    {
        StringBuilder canonical = new();
        foreach (LocationGeometryDefinition geometry in geometries)
        {
            canonical.Append(geometry.LocationId).Append('|').Append(geometry.ProvinceId).Append('|').Append(geometry.Direction);
            foreach (StrategicMapPolygon polygon in geometry.Geometry.Polygons)
            {
                canonical.Append("|P");
                foreach (IReadOnlyList<StrategicMapPoint> ring in polygon.Rings)
                {
                    canonical.Append("|R");
                    foreach (StrategicMapPoint point in ring)
                    {
                        canonical.Append('|')
                            .Append(point.X.ToString("R", CultureInfo.InvariantCulture))
                            .Append(',')
                            .Append(point.Y.ToString("R", CultureInfo.InvariantCulture));
                    }
                }
            }
            canonical.AppendLine();
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
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
        if (!condition) throw new InvalidOperationException(message);
    }
}
