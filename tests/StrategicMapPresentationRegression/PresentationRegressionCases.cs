using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Rpg.Application.StrategicMap;
using Rpg.Definitions.StrategicMap;

namespace StrategicMapPresentationRegression;

internal static class PresentationRegressionCases
{
    private const string GodotUidAlphabet = "abcdefghijklmnopqrstuvwxy012345678";
    private static readonly Regex ImportUidPattern = new("^uid://[a-y0-8]{1,13}$", RegexOptions.CultureInvariant);

    private static readonly string[] ForbiddenTokens =
    {
        "StrategicWorldRuntime",
        "StrategicWorldState",
        "WorldArmyState",
        "StrategicWorldRoot",
        "Presentation.World.Preview",
        "scenes/world/preview",
        "resource/world/preview",
        "player_camp",
        "bonefield",
        "TileMapLayer"
    };

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

    public static void ProductionVisualBindingsAreComplete(string projectRoot)
    {
        StrategicMapChunkManifest manifest = LoadSelected(projectRoot).Canonical.ChunkManifest;
        StrategicMapPresentationContract.ThrowIfVisualBindingsInvalid(manifest, projectRoot);
        AssertEqual(35, manifest.Chunks.Count, "canonical production chunk count");
        AssertEqual(35, manifest.Chunks.Select(chunk => chunk.VisualTexturePath).Distinct(StringComparer.Ordinal).Count(), "unique visual bindings");

        foreach (StrategicMapChunkDefinition chunk in manifest.Chunks)
        {
            string visualPath = StrategicMapPackageLoader.ResolveProjectPath(projectRoot, chunk.VisualTexturePath!);
            string sourcePath = Path.Combine(projectRoot, "assets", "textures", "world", "visual-chunks", $"{chunk.ChunkId}.png");
            AssertEqual(FileHash(sourcePath), FileHash(visualPath), $"published visual snapshot identity chunkId={chunk.ChunkId}");
        }

        StrategicMapChunkDefinition first = manifest.Chunks[0];
        StrategicMapChunkManifest broken = manifest with
        {
            Chunks = new[] { first with { VisualTexturePath = null } }.Concat(manifest.Chunks.Skip(1)).ToArray()
        };
        InvalidOperationException failure = AssertThrows<InvalidOperationException>(() =>
            StrategicMapPresentationContract.ThrowIfVisualBindingsInvalid(broken, projectRoot));
        AssertContains(failure.Message, $"chunkId={first.ChunkId}", "missing binding failure includes chunk id");
        AssertContains(failure.Message, "path=<empty>", "missing binding failure includes path");
    }

    public static void ProductionVisualImportsAreReproducible(string projectRoot)
    {
        StrategicMapChunkManifest manifest = LoadSelected(projectRoot).Canonical.ChunkManifest;
        string visualDirectory = Path.GetDirectoryName(StrategicMapPackageLoader.ResolveProjectPath(projectRoot, manifest.Chunks[0].VisualTexturePath!))!;
        string[] pngs = Directory.GetFiles(visualDirectory, "*.png", SearchOption.TopDirectoryOnly);
        string[] sidecars = Directory.GetFiles(visualDirectory, "*.png.import", SearchOption.TopDirectoryOnly);
        AssertEqual(35, pngs.Length, "production PNG count");
        AssertEqual(35, sidecars.Length, "production PNG import sidecar count");

        HashSet<string> productionUids = new(StringComparer.Ordinal);
        foreach (string png in pngs.OrderBy(path => path, StringComparer.Ordinal))
        {
            string sidecarPath = png + ".import";
            AssertTrue(File.Exists(sidecarPath), $"missing production sidecar path={sidecarPath}");
            string sourcePath = $"res://{Path.GetRelativePath(projectRoot, png).Replace('\\', '/')}";
            string cacheHash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(sourcePath))).ToLowerInvariant();
            string cachePath = $"res://.godot/imported/{Path.GetFileName(png)}-{cacheHash}.ctex";
            string import = File.ReadAllText(sidecarPath);
            string uid = ReadQuotedImportValue(import, "uid");

            AssertEqual(ExpectedImportUid(sourcePath), uid, $"deterministic path-derived uid source={sourcePath}");
            AssertTrue(ImportUidPattern.IsMatch(uid), $"illegal Godot UID source={sourcePath} uid={uid}");
            AssertTrue(productionUids.Add(uid), $"duplicate production UID source={sourcePath} uid={uid}");
            AssertContains(import, $"source_file=\"{sourcePath}\"", $"source path source={sourcePath}");
            AssertContains(import, $"path=\"{cachePath}\"", $"cache path source={sourcePath}");
            AssertContains(import, $"dest_files=[\"{cachePath}\"]", $"cache destination source={sourcePath}");
            AssertContains(import, "compress/mode=0", $"lossless import source={sourcePath}");
            AssertContains(import, "mipmaps/generate=true", $"mipmap import source={sourcePath}");
            AssertNotContains(import, "reference/", $"reference path source={sourcePath}");
            AssertNotContains(import, "C:/Users/qs/asset", $"external source path={sourcePath}");
            AssertNotContains(import, "C:\\Users\\qs\\asset", $"external source path={sourcePath}");
        }

        string[] committedSidecars = ReadCommittedImportSidecars(projectRoot);
        AssertTrue(committedSidecars.Length > 0, "committed import sidecars must be discoverable");
        Dictionary<string, List<string>> uidOwners = committedSidecars
            .Concat(sidecars)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => new { Path = path, Uid = ReadQuotedImportValue(File.ReadAllText(path), "uid") })
            .GroupBy(item => item.Uid, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Path).ToList(), StringComparer.Ordinal);
        foreach ((string uid, List<string> owners) in uidOwners)
        {
            AssertEqual(1, owners.Count, $"repository import UID collision uid={uid} paths={string.Join(',', owners)}");
        }
    }

    public static void WorldQueriesResolveCanonicalChunks(string projectRoot)
    {
        StrategicMapChunkManifest manifest = LoadSelected(projectRoot).Canonical.ChunkManifest;
        AssertEqual("chunk_0_0", StrategicMapWorldQuery.ResolveChunkAtWorldPosition(manifest, new StrategicMapPoint(0, 0))!.ChunkId, "world origin query");
        AssertEqual("chunk_1_0", StrategicMapWorldQuery.ResolveChunkAtWorldPosition(manifest, new StrategicMapPoint(1024, 512))!.ChunkId, "chunk boundary query");
        AssertEqual("chunk_6_4", StrategicMapWorldQuery.ResolveChunkAtWorldPosition(manifest, new StrategicMapPoint(7167.99, 5119.99))!.ChunkId, "world end query");
        AssertTrue(StrategicMapWorldQuery.ResolveChunkAtWorldPosition(manifest, new StrategicMapPoint(7168, 5120)) == null, "exclusive world end");
        AssertTrue(StrategicMapWorldQuery.ResolveChunkAtWorldPosition(manifest, new StrategicMapPoint(-1, 0)) == null, "negative world position");

        IReadOnlyList<StrategicMapChunkDefinition> visible = StrategicMapWorldQuery.SelectVisibleChunks(
            manifest,
            new StrategicMapWorldRect(900, 900, 300, 300),
            0);
        AssertSequence(new[] { "chunk_0_0", "chunk_1_0", "chunk_0_1", "chunk_1_1" }, visible.Select(chunk => chunk.ChunkId), "visible chunk ordering");
        AssertTrue(StrategicMapWorldQuery.SelectVisibleChunks(manifest, new StrategicMapWorldRect(8000, 8000, 100, 100), 0).Count == 0, "outside view selects no chunks");
    }

    public static void ThreadedResidencySchedulingIsBounded(string projectRoot)
    {
        StrategicMapChunkManifest manifest = LoadSelected(projectRoot).Canonical.ChunkManifest;
        StrategicMapChunkLoadRequest[] requests = manifest.Chunks.Take(4)
            .Select(chunk => new StrategicMapChunkLoadRequest(
                chunk,
                StrategicMapPresentationContract.ResolveVisualTextureResourcePath(chunk)))
            .ToArray();
        StrategicMapChunkLoadScheduler scheduler = new(2);

        AssertTrue(scheduler.SetDesired(requests), "initial desired set changes scheduler");
        IReadOnlyList<StrategicMapChunkLoadRequest> initial = scheduler.ReserveAvailableRequests();
        AssertSequence(requests.Take(2).Select(request => request.Chunk.ChunkId), initial.Select(request => request.Chunk.ChunkId), "canonical request ordering");
        AssertEqual(2, scheduler.ActiveCount, "bounded active request count");
        AssertEqual(0, scheduler.ReserveAvailableRequests().Count, "cap prevents additional native request");

        scheduler.SetDesired(new[] { requests[2] });
        AssertEqual(StrategicMapChunkLoadCompletion.Stale, scheduler.Complete(requests[0], true), "offscreen completion is stale");
        AssertEqual(0, scheduler.ResidentCount, "stale completion is not resident");
        AssertSequence(new[] { requests[2].Chunk.ChunkId }, scheduler.ReserveAvailableRequests().Select(request => request.Chunk.ChunkId), "freed slot schedules current canonical request");

        AssertEqual(StrategicMapChunkLoadCompletion.Failed, scheduler.Complete(requests[1], false), "terminal failure state");
        scheduler.SetDesired(new[] { requests[1], requests[2] });
        AssertEqual(0, scheduler.ReserveAvailableRequests().Count, "failed chunk never retries after camera churn");
        AssertEqual(StrategicMapChunkLoadCompletion.Resident, scheduler.Complete(requests[2], true), "desired completion becomes resident");
        scheduler.SetDesired(Array.Empty<StrategicMapChunkLoadRequest>());
        AssertSequence(new[] { requests[2].Chunk.ChunkId }, scheduler.GetChunkIdsToUnload(), "deterministic resident unload");

        AssertThrows<InvalidOperationException>(() => scheduler.SetDesired(new[] { requests[0], requests[0] }));
        StrategicMapChunkLoadRequest duplicatePath = requests[1] with { ResourcePath = requests[0].ResourcePath };
        AssertThrows<InvalidOperationException>(() => scheduler.SetDesired(new[] { requests[0], duplicatePath }));
    }

    public static void RegionInputsMatchCanonicalGeography(string projectRoot)
    {
        StrategicMapLoadedContext context = LoadSelected(projectRoot);
        StrategicMapCanonicalDefinition canonical = context.Canonical;
        string lookupPath = StrategicMapPackageLoader.ResolveProjectPath(projectRoot, context.Package.RegionLookupPath);
        StrategicMapRegionLookupDefinition lookup = StrategicMapRegionLookupLoader.Load(lookupPath);
        StrategicMapRegionLookupLoader.ValidateAgainstCanonical(lookup, canonical.Geography);
        AssertEqual(11, lookup.Entries.Count, "region lookup count");
        AssertEqual(5, lookup.Entries.Count(entry => entry.ProvinceId == "qinghe"), "Qinghe lookup count");
        AssertEqual(6, lookup.Entries.Count(entry => entry.ProvinceId == "chiyan"), "Chiyan lookup count");

        string outlinesPath = Path.Combine(Path.GetDirectoryName(lookupPath)!, "region_outlines.json");
        using JsonDocument outlines = JsonDocument.Parse(File.ReadAllText(outlinesPath));
        AssertEqual(2, outlines.RootElement.GetProperty("version").GetInt32(), "region outlines version");
        AssertEqual(11, outlines.RootElement.GetProperty("locationGeometries").GetArrayLength(), "region outlines location count");
    }

    public static void AuthoredProductionSceneIsIsolated(string projectRoot)
    {
        string scene = Read(projectRoot, "scenes/world/strategic_map/StrategicMap.tscn");
        AssertContains(scene, "src/Presentation/StrategicMap/StrategicMapRoot.cs", "production root binding");
        AssertContains(scene, "src/Presentation/Common/MapCameraController.cs", "shared camera binding");
        AssertNotContains(scene, "7168", "production scene must not author current-map width");
        AssertNotContains(scene, "5120", "production scene must not author current-map height");
        AssertContains(scene, "StrategicMapChunkVisual.tscn", "authored reusable chunk scene");
        AssertContains(scene, "StrategicMapRegionOverlay.tscn", "authored static region overlay");
        AssertContains(Read(projectRoot, "project.godot"), "run/main_scene=\"res://scenes/world/StrategicWorldRoot.tscn\"", "legacy main remains unchanged during Stage 1");
        AssertNotContains(scene, "TileMap", "production scene must not render through TileMap");
        AssertNotContains(scene, "/preview/", "production scene must not depend on Preview resources");
    }

    public static void CampaignRegionTreatmentConsumesReadOnlyStrategicManagementPort(string projectRoot)
    {
        string root = Read(projectRoot, "src/Presentation/StrategicMap/StrategicMapRoot.cs");
        string port = Read(projectRoot, "src/Application/StrategicMap/StrategicMapCampaignPresentationPort.cs");
        string adapter = Read(projectRoot, "src/Application/StrategicManagement/StrategicManagementCampaignPresentationPort.cs");
        string shader = Read(projectRoot, "resource/world/strategic_map/strategic_map_region_overlay.gdshader");

        AssertContains(port, "interface IStrategicMapCampaignPresentationPort", "pure read-only campaign port contract");
        AssertContains(port, "IReadOnlyList<StrategicMapLocationControlView>", "immutable location view collection");
        AssertContains(adapter, "StrategicManagementGeographyInvariantService", "adapter validates the complete canonical identity graph");
        AssertContains(root, "IStrategicMapCampaignPresentationPort campaignPresentation", "production root consumes the campaign port abstraction");
        AssertContains(root, "new StrategicManagementCampaignPresentationPort", "production composition binds Strategic Management adapter");
        AssertContains(root, "campaignPresentation.Read()", "production region metadata reads immutable campaign state");
        AssertContains(root, "location.Control switch", "region treatment uses port control views");
        AssertNotContains(root, "CampaignRole", "production region refresh must not infer mutable control from canonical campaign role");
        AssertNotContains(root, "ProvinceCampaignRole", "production region refresh must not branch on static province role");
        AssertNotContains(root, "StrategicWorld", "replacement presentation must not fall back to legacy world state");
        AssertNotContains(root, "fallback", "missing campaign state must fail instead of selecting a fallback source");
        AssertContains(shader, "player_control_color", "shader uses player control treatment naming");
        AssertContains(shader, "enemy_control_color", "shader uses enemy control treatment naming");
        AssertNotContains(shader, "player_start_color", "shader no longer encodes canonical campaign roles as mutable state");
        AssertNotContains(shader, "first_hostile_color", "shader no longer encodes canonical campaign roles as mutable state");
    }

    public static void ProductionSourceExcludesForbiddenDependencies(string projectRoot)
    {
        string[] roots =
        {
            Path.Combine(projectRoot, "src", "Definitions", "StrategicMap"),
            Path.Combine(projectRoot, "src", "Application", "StrategicMap"),
            Path.Combine(projectRoot, "src", "Presentation", "StrategicMap"),
            Path.Combine(projectRoot, "scenes", "world", "strategic_map"),
            Path.Combine(projectRoot, "resource", "world", "strategic_map")
        };

        foreach (string path in roots.SelectMany(root => Directory.GetFiles(root, "*", SearchOption.AllDirectories)))
        {
            string text = File.ReadAllText(path);
            foreach (string token in ForbiddenTokens)
            {
                AssertNotContains(text, token, $"forbidden production dependency token={token} path={path}");
            }
        }

        string chunkVisual = Read(projectRoot, "src/Presentation/StrategicMap/StrategicMapChunkVisual.cs");
        AssertNotContains(chunkVisual, "GD.Load", "chunk visual must bind completed texture without synchronous loading");

        string root = Read(projectRoot, "src/Presentation/StrategicMap/StrategicMapRoot.cs");
        AssertContains(root, "ResourceLoader.LoadThreadedRequest", "native threaded request API");
        AssertContains(root, "ResourceLoader.LoadThreadedGetStatus", "cross-frame threaded polling API");
        AssertContains(root, "ResourceLoader.LoadThreadedGet", "terminal threaded result collection API");
        int loadedGate = root.IndexOf("if (status != ResourceLoader.ThreadLoadStatus.Loaded)", StringComparison.Ordinal);
        int threadedGet = root.IndexOf("ResourceLoader.LoadThreadedGet(request.ResourcePath)", StringComparison.Ordinal);
        AssertTrue(loadedGate >= 0 && threadedGet > loadedGate, "threaded get must remain behind the Loaded status gate");
        string beforeThreadedGet = root[..threadedGet];
        int lastContinue = beforeThreadedGet.LastIndexOf("continue;", StringComparison.Ordinal);
        AssertTrue(lastContinue > loadedGate, "non-Loaded statuses must continue before threaded get");
        AssertContains(root, "chunkId={request.Chunk.ChunkId}", "chunk failure includes stable id");
        AssertContains(root, "path={request.ResourcePath}", "chunk failure includes current resource path");
        AssertNotContains(root, "GD.Load<Texture2D>(request.ResourcePath", "chunk residency must not synchronously load textures");

        string config = Read(projectRoot, "src/Presentation/StrategicMap/StrategicMapPresentationConfig.cs");
        AssertContains(config, "MaxConcurrentChunkLoads", "authored concurrent load setting");
        AssertContains(Read(projectRoot, "resource/world/strategic_map/strategic_map_presentation_config.tres"), "MaxConcurrentChunkLoads = 2", "authored concurrent load default");
    }

    public static void BothPublishedPackagesLoadThroughSameContracts(string projectRoot)
    {
        StrategicMapLoadedContext mock = LoadSelected(projectRoot);
        StrategicMapLoadedContext fixture = LoadSelected(projectRoot, "res://config/world/strategic-map-selection-fixture.json");
        AssertEqual(35, mock.Canonical.ChunkManifest.Chunks.Count, "mock chunk count");
        AssertEqual(2, fixture.Canonical.ChunkManifest.Chunks.Count, "fixture chunk count");
        AssertEqual(7168d, mock.Canonical.ChunkManifest.WorldWidth, "mock width");
        AssertEqual(2048d, fixture.Canonical.ChunkManifest.WorldWidth, "fixture width");
        AssertEqual(11, mock.RegionLookup.Entries.Count, "mock region count");
        AssertEqual(2, fixture.RegionLookup.Entries.Count, "fixture region count");
        StrategicMapPresentationContract.ThrowIfVisualBindingsInvalid(mock.Canonical.ChunkManifest, projectRoot);
        StrategicMapPresentationContract.ThrowIfVisualBindingsInvalid(fixture.Canonical.ChunkManifest, projectRoot);
    }

    private static StrategicMapLoadedContext LoadSelected(
        string projectRoot,
        string selectionPath = "res://config/world/strategic-map-selection.json")
    {
        StrategicMapPackageSelection selection = StrategicMapPackageLoader.LoadSelection(projectRoot, selectionPath);
        return StrategicMapPackageLoader.LoadSelected(projectRoot, selection);
    }

    private static string ExpectedImportUid(string sourcePath)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"StrategicMapPackageImportUID\0{sourcePath}"));
        ulong value = ((ulong)hash[0] << 56) |
            ((ulong)hash[1] << 48) |
            ((ulong)hash[2] << 40) |
            ((ulong)hash[3] << 32) |
            ((ulong)hash[4] << 24) |
            ((ulong)hash[5] << 16) |
            ((ulong)hash[6] << 8) |
            hash[7];
        value &= long.MaxValue;

        Span<char> reversed = stackalloc char[13];
        int length = 0;
        do
        {
            reversed[length++] = GodotUidAlphabet[(int)(value % (ulong)GodotUidAlphabet.Length)];
            value /= (ulong)GodotUidAlphabet.Length;
        }
        while (value > 0);

        char[] encoded = new char[length];
        for (int index = 0; index < length; index++)
        {
            encoded[index] = reversed[length - index - 1];
        }
        return $"uid://{new string(encoded)}";
    }

    private static string ReadQuotedImportValue(string import, string key)
    {
        Match match = Regex.Match(import, $"(?m)^{Regex.Escape(key)}=\\\"([^\\\"]+)\\\"$");
        if (!match.Success)
        {
            throw new InvalidOperationException($"Import sidecar is missing quoted value key={key}");
        }
        return match.Groups[1].Value;
    }

    private static string[] ReadCommittedImportSidecars(string projectRoot)
    {
        ProcessStartInfo startInfo = new("git")
        {
            WorkingDirectory = projectRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("ls-files");
        startInfo.ArgumentList.Add("-z");
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add("*.import");
        using Process process = Process.Start(startInfo) ??
            throw new InvalidOperationException("Failed to start git for committed import-sidecar discovery.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git ls-files failed exit={process.ExitCode} error={error.Trim()}");
        }

        return output
            .Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Select(path => Path.GetFullPath(Path.Combine(projectRoot, path.Replace('/', Path.DirectorySeparatorChar))))
            .ToArray();
    }

    private static string FileHash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static string Read(string projectRoot, string relativePath) => File.ReadAllText(Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static void AssertSequence(IEnumerable<string> expected, IEnumerable<string> actual, string message)
    {
        string[] expectedArray = expected.ToArray();
        string[] actualArray = actual.ToArray();
        if (!expectedArray.SequenceEqual(actualArray, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"{message}; expected={string.Join(',', expectedArray)} actual={string.Join(',', actualArray)}");
        }
    }

    private static void AssertContains(string text, string value, string message)
    {
        if (!text.Contains(value, StringComparison.Ordinal)) throw new InvalidOperationException($"{message}; missing={value}");
    }

    private static void AssertNotContains(string text, string value, string message)
    {
        if (text.Contains(value, StringComparison.Ordinal)) throw new InvalidOperationException($"{message}; forbidden={value}");
    }

    private static TException AssertThrows<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }

        throw new InvalidOperationException($"Expected exception type={typeof(TException).Name}");
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
