#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.StrategicManagement;
using Rpg.Application.StrategicMap;
using Rpg.Definitions.StrategicManagement;
using Rpg.Definitions.StrategicMap;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.StrategicMap;

public partial class StrategicMapRoot : Node2D
{
    [Export] public StrategicMapPresentationConfig Config { get; set; } = null!;
    [Export] public PackedScene ChunkVisualScene { get; set; } = null!;
    [Export] public PackedScene RegionOverlayScene { get; set; } = null!;

    private readonly Dictionary<string, StrategicMapChunkVisual> _loadedChunks = new(StringComparer.Ordinal);
    private Node2D _chunkVisuals = null!;
    private Node2D _regionPresentation = null!;
    private MapCameraController _camera = null!;
    private Label _statusLabel = null!;
    private StrategicMapCanonicalDefinition _canonical = null!;
    private StrategicMapLoadedContext _loadedContext = null!;
    private StrategicMapChunkLoadScheduler _chunkLoadScheduler = null!;

    public override void _Ready()
    {
        _chunkVisuals = GetNode<Node2D>("ChunkVisuals");
        _regionPresentation = GetNode<Node2D>("RegionPresentation");
        _camera = GetNode<MapCameraController>("MapCamera");
        _statusLabel = GetNode<Label>("%StatusLabel");

        try
        {
            ValidateAuthoredDependencies();
            string projectRoot = ProjectSettings.GlobalizePath("res://");
            StrategicMapPackageSelection selection = StrategicMapPackageLoader.LoadSelection(projectRoot, Config.SelectionPath);
            _loadedContext = StrategicMapPackageLoader.LoadSelected(projectRoot, selection);
            _canonical = _loadedContext.Canonical;
            StrategicManagementScenarioDefinition scenario = StrategicManagementScenarioLoader.LoadSelected(
                projectRoot,
                selection.ScenarioPath,
                _loadedContext.Package,
                _canonical);
            StrategicManagementDefinitionSet managementDefinitions = FirstStrategicManagementDefinitions.Create(
                _canonical,
                scenario,
                new StrategicManagementContentIdentity(
                    _loadedContext.Package.MapId,
                    scenario.ScenarioId,
                    _loadedContext.Package.CompatibilityRevision,
                    scenario.ScenarioContentRevision));
            StrategicMapPresentationContract.ThrowIfVisualBindingsInvalid(_canonical.ChunkManifest, projectRoot);
            _chunkLoadScheduler = new StrategicMapChunkLoadScheduler(Config.MaxConcurrentChunkLoads);
            StrategicManagementRuntime.EnsureInitialized(managementDefinitions);
            IStrategicMapCampaignPresentationPort campaignPresentation =
                new StrategicManagementCampaignPresentationPort(
                    StrategicManagementRuntime.Definitions,
                    StrategicManagementRuntime.State);

            BuildRegionPresentation(_loadedContext.RegionLookup, campaignPresentation.Read(), projectRoot);
            ConfigureCamera();
            UpdateChunkResidency();
            SetProcess(true);
            ShowStatus($"战略地图已加载 · {_canonical.ChunkManifest.Chunks.Count} 个区块", false);
            GameLog.Info(nameof(StrategicMapRoot), $"StrategicMapLoaded MapId={_loadedContext.Package.MapId} revision={_loadedContext.Package.Revision} chunks={_canonical.ChunkManifest.Chunks.Count} provinces={_canonical.Geography.Provinces.Count} bounds={_canonical.ChunkManifest.WorldWidth}x{_canonical.ChunkManifest.WorldHeight}");
        }
        catch (Exception exception)
        {
            ReportFatalLoadFailure(exception);
        }
    }

    public override void _Process(double delta)
    {
        _ = delta;
        if (_canonical != null)
        {
            UpdateChunkResidency();
        }
    }

    public StrategicMapChunkDefinition? ResolveChunkAtWorldPosition(Vector2 worldPosition) =>
        _canonical == null
            ? null
            : StrategicMapWorldQuery.ResolveChunkAtWorldPosition(
                _canonical.ChunkManifest,
                new StrategicMapPoint(worldPosition.X, worldPosition.Y));

    private void ValidateAuthoredDependencies()
    {
        if (Config == null || ChunkVisualScene == null || RegionOverlayScene == null)
        {
            throw new InvalidOperationException("Strategic map scene is missing an authored presentation dependency.");
        }
        if (string.IsNullOrWhiteSpace(Config.SelectionPath))
        {
            throw new InvalidOperationException("Strategic map package selection path is missing.");
        }
        if (Config.MaxConcurrentChunkLoads is < StrategicMapChunkLoadScheduler.MinimumConcurrentLoads or > StrategicMapChunkLoadScheduler.MaximumConcurrentLoads)
        {
            throw new InvalidOperationException(
                $"Strategic map MaxConcurrentChunkLoads must be in range {StrategicMapChunkLoadScheduler.MinimumConcurrentLoads}-{StrategicMapChunkLoadScheduler.MaximumConcurrentLoads}.");
        }
    }

    private void BuildRegionPresentation(
        StrategicMapRegionLookupDefinition lookup,
        StrategicMapCampaignPresentationView campaign,
        string projectRoot)
    {
        Texture2D metadata = BuildRegionMetadataTexture(lookup, campaign);
        foreach (StrategicMapRegionArtifactChunk regionChunk in _loadedContext.Package.RegionChunks)
        {
            string maskPath = regionChunk.MaskTexturePath;
            _ = StrategicMapPackageLoader.ResolveProjectPath(projectRoot, maskPath);
            Texture2D? territoryMask = GD.Load<Texture2D>(maskPath);
            if (territoryMask == null)
            {
                throw new InvalidOperationException($"Strategic map region mask failed to load MapId={_loadedContext.Package.MapId} chunkId={regionChunk.ChunkId} path={maskPath}");
            }
            StrategicMapRegionOverlay overlay = RegionOverlayScene.Instantiate<StrategicMapRegionOverlay>();
            _regionPresentation.AddChild(overlay);
            overlay.Bind(
                new Vector2((float)regionChunk.WorldOrigin.X, (float)regionChunk.WorldOrigin.Y),
                new Vector2((float)regionChunk.WorldWidth, (float)regionChunk.WorldHeight),
                territoryMask,
                metadata,
                Config);
        }
    }

    private Texture2D BuildRegionMetadataTexture(
        StrategicMapRegionLookupDefinition lookup,
        StrategicMapCampaignPresentationView campaign)
    {
        Dictionary<string, StrategicMapLocationControlView> locations = campaign.Locations
            .ToDictionary(location => location.LocationId, StringComparer.Ordinal);
        Dictionary<string, int> provinceCodes = campaign.Provinces
            .OrderBy(province => province.ProvinceId, StringComparer.Ordinal)
            .Select((province, index) => new { province.ProvinceId, Code = index + 1 })
            .ToDictionary(item => item.ProvinceId, item => item.Code, StringComparer.Ordinal);
        int metadataWidth = Math.Max(1, lookup.Entries.Max(entry => entry.MaskId) + 1);
        Image metadata = Image.CreateEmpty(metadataWidth, 1, false, Image.Format.Rgba8);
        // The mask id remains derived lookup data. Control comes only from the read-only
        // Strategic Management port; canonical geography supplies identity lineage only.
        foreach (StrategicMapRegionLookupEntry entry in lookup.Entries)
        {
            if (!locations.TryGetValue(entry.LocationId, out StrategicMapLocationControlView? location) ||
                !string.Equals(location.ProvinceId, entry.ProvinceId, StringComparison.Ordinal) ||
                !_canonical.Geography.Provinces.Any(province =>
                    string.Equals(province.ProvinceId, location.ProvinceId, StringComparison.Ordinal) &&
                    string.Equals(province.LayoutId, location.LayoutId, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Strategic map campaign view identity mismatch ProvinceId={entry.ProvinceId} LocationId={entry.LocationId} LayoutId={location?.LayoutId ?? "<missing>"}");
            }

            int presentationCode = location.Control switch
            {
                StrategicMapCampaignControl.Player => 1,
                StrategicMapCampaignControl.Enemy => 2,
                _ => 3
            };
            float shade = 0.38f + entry.MaskId % 5 * 0.055f;
            metadata.SetPixel(entry.MaskId, 0, new Color(
                presentationCode / 255f,
                provinceCodes[entry.ProvinceId] / 255f,
                shade,
                1f));
        }

        ImageTexture texture = ImageTexture.CreateFromImage(metadata);
        metadata.Dispose();
        return texture;
    }

    private void ConfigureCamera()
    {
        _camera.SetMapBounds(new Rect2(
            Vector2.Zero,
            new Vector2((float)_canonical.ChunkManifest.WorldWidth, (float)_canonical.ChunkManifest.WorldHeight)));
        Vector2 focusRatio = new(
            Mathf.Clamp(Config.InitialFocusRatio.X, 0f, 1f),
            Mathf.Clamp(Config.InitialFocusRatio.Y, 0f, 1f));
        _camera.FocusOn(new Vector2(
            (float)_canonical.ChunkManifest.WorldWidth * focusRatio.X,
            (float)_canonical.ChunkManifest.WorldHeight * focusRatio.Y));
        _camera.SetZoomScalar(Config.InitialZoom);
    }

    private void UpdateChunkResidency()
    {
        IReadOnlyList<StrategicMapChunkDefinition> desired = StrategicMapWorldQuery.SelectVisibleChunks(
            _canonical.ChunkManifest,
            GetVisibleWorldRect(),
            Config.PreloadMargin);
        StrategicMapChunkLoadRequest[] desiredRequests = desired
            .Select(chunk => new StrategicMapChunkLoadRequest(
                chunk,
                StrategicMapPresentationContract.ResolveVisualTextureResourcePath(chunk)))
            .ToArray();
        bool desiredChanged = _chunkLoadScheduler.SetDesired(desiredRequests);

        foreach (string chunkId in _chunkLoadScheduler.GetChunkIdsToUnload())
        {
            if (_loadedChunks.Remove(chunkId, out StrategicMapChunkVisual? visual))
            {
                visual.QueueFree();
            }
            _chunkLoadScheduler.MarkUnloaded(chunkId);
        }

        PollThreadedChunkLoads();
        foreach (StrategicMapChunkLoadRequest request in _chunkLoadScheduler.ReserveAvailableRequests())
        {
            Error error = ResourceLoader.LoadThreadedRequest(request.ResourcePath, "Texture2D", false);
            if (error != Error.Ok)
            {
                _chunkLoadScheduler.Complete(request, false);
                ReportChunkLoadFailure(request, $"threaded request failed error={error}");
            }
        }

        if (desiredChanged)
        {
            GameLog.Info(nameof(StrategicMapRoot), $"StrategicMapResidencyChanged desired={desiredRequests.Length} loaded={_loadedChunks.Count} loading={_chunkLoadScheduler.ActiveCount} failed={_chunkLoadScheduler.FailedCount}");
        }
    }

    private void PollThreadedChunkLoads()
    {
        foreach (StrategicMapChunkLoadRequest request in _chunkLoadScheduler.ActiveRequests.ToArray())
        {
            ResourceLoader.ThreadLoadStatus status = ResourceLoader.LoadThreadedGetStatus(request.ResourcePath);
            if (status == ResourceLoader.ThreadLoadStatus.InProgress)
            {
                continue;
            }

            if (status != ResourceLoader.ThreadLoadStatus.Loaded)
            {
                _chunkLoadScheduler.Complete(request, false);
                ReportChunkLoadFailure(request, $"threaded load status={status}");
                continue;
            }

            // Godot documents LoadThreadedGet as blocking before Loaded. Keep the status gate
            // adjacent so failed/invalid requests can never stall the presentation thread.
            Resource? resource = ResourceLoader.LoadThreadedGet(request.ResourcePath);
            if (resource is not Texture2D texture)
            {
                _chunkLoadScheduler.Complete(request, false);
                string resourceType = resource?.GetType().Name ?? "null";
                ReportChunkLoadFailure(request, $"threaded result type={resourceType} expected=Texture2D");
                continue;
            }

            Vector2 textureSize = texture.GetSize();
            if (!float.IsFinite(textureSize.X) || !float.IsFinite(textureSize.Y) || textureSize.X <= 0f || textureSize.Y <= 0f)
            {
                _chunkLoadScheduler.Complete(request, false);
                ReportChunkLoadFailure(request, $"texture is invalid or empty size={textureSize}");
                continue;
            }

            if (!_chunkLoadScheduler.IsDesired(request))
            {
                // A completed offscreen request must still be collected, but its resource is not instantiated.
                _chunkLoadScheduler.Complete(request, true);
                continue;
            }

            TryBindCompletedChunkVisual(request, texture);
        }
    }

    private void TryBindCompletedChunkVisual(StrategicMapChunkLoadRequest request, Texture2D texture)
    {
        StrategicMapChunkVisual? visual = null;
        try
        {
            visual = ChunkVisualScene.Instantiate<StrategicMapChunkVisual>();
            _chunkVisuals.AddChild(visual);
            visual.Bind(
                request.Chunk,
                new Vector2(
                    (float)_canonical.ChunkManifest.ChunkWidth,
                    (float)_canonical.ChunkManifest.ChunkHeight),
                texture,
                request.ResourcePath);
            _loadedChunks.Add(request.Chunk.ChunkId, visual);
            _chunkLoadScheduler.Complete(request, true);
        }
        catch (Exception exception)
        {
            visual?.QueueFree();
            _chunkLoadScheduler.Complete(request, false);
            ReportChunkLoadFailure(request, $"completed texture binding failed reason={exception.Message}");
        }
    }

    private void ReportChunkLoadFailure(StrategicMapChunkLoadRequest request, string reason)
    {
        string message = $"区块加载失败：chunkId={request.Chunk.ChunkId} path={request.ResourcePath} reason={reason}";
        GameLog.Error(nameof(StrategicMapRoot), message);
        GD.PushError(message);
        ShowStatus(message, true);
    }

    private StrategicMapWorldRect GetVisibleWorldRect()
    {
        Vector2 viewportSize = GetViewportRect().Size;
        float zoom = _camera.GetZoomScalar();
        Vector2 worldSize = viewportSize / Mathf.Max(zoom, 0.001f);
        Vector2 topLeft = _camera.GlobalPosition - worldSize * 0.5f;
        return new StrategicMapWorldRect(topLeft.X, topLeft.Y, worldSize.X, worldSize.Y);
    }

    private void ReportFatalLoadFailure(Exception exception)
    {
        SetProcess(false);
        string message = $"战略地图加载失败：{exception.Message}";
        GameLog.Error(nameof(StrategicMapRoot), message);
        GD.PushError(message);
        ShowStatus(message, true);
    }

    private void ShowStatus(string message, bool isError)
    {
        _statusLabel.Text = message;
        _statusLabel.Modulate = isError ? new Color("ffc1b8") : new Color("d9e6dc");
    }
}
