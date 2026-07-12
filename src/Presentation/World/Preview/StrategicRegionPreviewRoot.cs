using System;
using System.Collections.Generic;
using Godot;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Common;

namespace Rpg.Presentation.World.Preview;

public partial class StrategicRegionPreviewRoot : Node2D
{
    private const int MaskMetadataWidth = 256;
    private const int PlayerFactionCode = 1;
    private const int HostileFactionCode = 2;

    [Export]
    public StrategicRegionPreviewConfig Config { get; set; } = null!;

    [Export]
    public PackedScene ReferenceChunkScene { get; set; } = null!;

    [Export]
    public PackedScene RegionOverlayScene { get; set; } = null!;

    [Export]
    public PackedScene CityAnchorScene { get; set; } = null!;

    private Node2D _referenceChunks = null!;
    private Node2D _regionOverlays = null!;
    private Node2D _cities = null!;
    private MapCameraController _camera = null!;
    private StrategicRegionPreviewHud _hud = null!;
    private StrategicRegionPreviewData _data = null!;
    private Image _territoryMaskImage = null!;
    private Texture2D _territoryMaskTexture = null!;
    private Texture2D _regionMetadataTexture = null!;

    private readonly List<StrategicRegionOverlayChunk> _overlayVisuals = new();
    private readonly List<StrategicCityAnchorVisual> _cityVisuals = new();
    private readonly Dictionary<string, StrategicRegionPreviewCity> _cityById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StrategicRegionPreviewRegion> _regionById = new(StringComparer.Ordinal);
    private readonly Dictionary<int, StrategicRegionPreviewRegion> _regionByMaskId = new();
    private readonly Dictionary<string, int> _cityMetadataIdByCityId = new(StringComparer.Ordinal);

    private string _hoveredRegionId = "";
    private string _selectedRegionId = "";
    private string _hoveredCityId = "";
    private string _selectedCityId = "";

    public override void _Ready()
    {
        _referenceChunks = GetNode<Node2D>("ReferenceChunks");
        _regionOverlays = GetNode<Node2D>("RegionOverlays");
        _cities = GetNode<Node2D>("Cities");
        _camera = GetNode<MapCameraController>("PreviewCamera");
        _hud = GetNode<StrategicRegionPreviewHud>("PreviewHud");
        _hud.ResetViewRequested += OnResetViewRequested;
        _hud.ClearSelectionRequested += OnClearSelectionRequested;

        try
        {
            ValidateAuthoredDependencies();
            string projectRoot = ProjectSettings.GlobalizePath("res://");
            _data = StrategicRegionPreviewDataLoader.LoadFromProjectRoot(projectRoot, Config.PreviewBounds);
            LoadTerritoryMask();
            BuildPreview();
            ResetView();
            UpdatePresentation();
            GameLog.Info(
                nameof(StrategicRegionPreviewRoot),
                $"StrategicRegionPreviewLoaded chunks={_data.Chunks.Count} cities={_data.Cities.Count} regions={_data.Regions.Count} renderer=chunk-mask-overlay bounds={_data.PreviewBounds}");
        }
        catch (Exception exception)
        {
            string message = $"独立区域预览加载失败：{exception.Message}";
            GameLog.Error(nameof(StrategicRegionPreviewRoot), message);
            GD.PushError(message);
            _hud.ShowError(message);
        }
    }

    public override void _ExitTree()
    {
        if (_hud != null)
        {
            _hud.ResetViewRequested -= OnResetViewRequested;
            _hud.ClearSelectionRequested -= OnClearSelectionRequested;
        }

        foreach (StrategicCityAnchorVisual visual in _cityVisuals)
        {
            visual.CityHoverChanged -= OnCityHoverChanged;
            visual.CitySelected -= OnCitySelected;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_data == null || _territoryMaskImage == null)
        {
            return;
        }

        if (@event is InputEventMouseMotion)
        {
            SetHoveredRegion(ResolveRegionAtWorldPosition(GetGlobalMousePosition()));
            return;
        }

        if (@event is InputEventMouseButton mouseButton &&
            mouseButton.Pressed &&
            mouseButton.ButtonIndex == MouseButton.Left)
        {
            StrategicRegionPreviewRegion region = ResolveRegionAtWorldPosition(GetGlobalMousePosition());
            if (region != null)
            {
                OnRegionSelected(region.RegionId);
                GetViewport().SetInputAsHandled();
            }
        }
    }

    private void ValidateAuthoredDependencies()
    {
        if (Config == null ||
            ReferenceChunkScene == null ||
            RegionOverlayScene == null ||
            CityAnchorScene == null ||
            string.IsNullOrWhiteSpace(Config.TerritoryMaskPath) ||
            Config.TerritoryMaskScale <= 0f)
        {
            throw new InvalidOperationException("Preview scene is missing an authored mask-overlay dependency.");
        }
    }

    private void LoadTerritoryMask()
    {
        _territoryMaskTexture = GD.Load<Texture2D>(Config.TerritoryMaskPath);
        if (_territoryMaskTexture == null)
        {
            throw new InvalidOperationException($"Territory mask failed to load path={Config.TerritoryMaskPath}");
        }

        _territoryMaskImage = _territoryMaskTexture.GetImage();
        if (_territoryMaskImage == null || _territoryMaskImage.IsEmpty())
        {
            throw new InvalidOperationException($"Territory mask image is empty path={Config.TerritoryMaskPath}");
        }
    }

    private void BuildPreview()
    {
        foreach (StrategicRegionPreviewCity city in _data.Cities)
        {
            _cityById.Add(city.CityId, city);
        }

        foreach (StrategicRegionPreviewRegion region in _data.Regions)
        {
            _regionById.Add(region.RegionId, region);
            _regionByMaskId.Add(region.MaskId, region);
        }

        _regionMetadataTexture = BuildRegionMetadataTexture();
        foreach (StrategicRegionPreviewChunk chunk in _data.Chunks)
        {
            StrategicReferenceChunkVisual reference = ReferenceChunkScene.Instantiate<StrategicReferenceChunkVisual>();
            _referenceChunks.AddChild(reference);
            reference.Bind(chunk, Config.ReferenceOpacity);

            StrategicRegionOverlayChunk overlay = RegionOverlayScene.Instantiate<StrategicRegionOverlayChunk>();
            _regionOverlays.AddChild(overlay);
            overlay.Bind(
                chunk,
                _territoryMaskTexture,
                _regionMetadataTexture,
                Config.TerritoryMaskScale,
                Config);
            _overlayVisuals.Add(overlay);
        }

        foreach (StrategicRegionPreviewCity city in _data.Cities)
        {
            StrategicCityAnchorVisual anchor = CityAnchorScene.Instantiate<StrategicCityAnchorVisual>();
            _cities.AddChild(anchor);
            anchor.Bind(city, Config.ResolveCityColor(city.CityId));
            anchor.CityHoverChanged += OnCityHoverChanged;
            anchor.CitySelected += OnCitySelected;
            _cityVisuals.Add(anchor);
        }
    }

    private StrategicRegionPreviewRegion ResolveRegionAtWorldPosition(Vector2 worldPosition)
    {
        if (!_data.PreviewBounds.HasPoint(worldPosition))
        {
            return null;
        }

        int maskX = Mathf.FloorToInt(worldPosition.X * Config.TerritoryMaskScale);
        int maskY = Mathf.FloorToInt(worldPosition.Y * Config.TerritoryMaskScale);
        if (maskX < 0 || maskY < 0 || maskX >= _territoryMaskImage.GetWidth() || maskY >= _territoryMaskImage.GetHeight())
        {
            return null;
        }

        int maskId = Mathf.RoundToInt(_territoryMaskImage.GetPixel(maskX, maskY).R * 255f);
        return _regionByMaskId.GetValueOrDefault(maskId);
    }

    private void SetHoveredRegion(StrategicRegionPreviewRegion region)
    {
        string regionId = region?.RegionId ?? "";
        if (_hoveredRegionId == regionId)
        {
            return;
        }

        _hoveredRegionId = regionId;
        UpdatePresentation();
    }

    private void OnRegionSelected(string regionId)
    {
        if (!_regionById.TryGetValue(regionId, out StrategicRegionPreviewRegion region) || _selectedRegionId == regionId)
        {
            return;
        }

        _selectedRegionId = regionId;
        _selectedCityId = region.CityId;
        GameLog.Info(nameof(StrategicRegionPreviewRoot), $"StrategicRegionPreviewSelectionChanged city={_selectedCityId} region={_selectedRegionId}");
        UpdatePresentation();
    }

    private void OnCityHoverChanged(string cityId, bool hovered)
    {
        if (hovered)
        {
            _hoveredCityId = cityId;
        }
        else if (_hoveredCityId == cityId)
        {
            _hoveredCityId = "";
        }

        UpdatePresentation();
    }

    private void OnCitySelected(string cityId)
    {
        if (!_cityById.ContainsKey(cityId) || (_selectedCityId == cityId && string.IsNullOrEmpty(_selectedRegionId)))
        {
            return;
        }

        _selectedCityId = cityId;
        _selectedRegionId = "";
        GameLog.Info(nameof(StrategicRegionPreviewRoot), $"StrategicRegionPreviewSelectionChanged city={_selectedCityId} region=");
        UpdatePresentation();
    }

    private void OnResetViewRequested()
    {
        ResetView();
    }

    private void OnClearSelectionRequested()
    {
        if (string.IsNullOrEmpty(_selectedCityId) && string.IsNullOrEmpty(_selectedRegionId))
        {
            return;
        }

        _selectedCityId = "";
        _selectedRegionId = "";
        GameLog.Info(nameof(StrategicRegionPreviewRoot), "StrategicRegionPreviewSelectionCleared");
        UpdatePresentation();
    }

    private void ResetView()
    {
        _camera.SetMapBounds(_data.PreviewBounds);
        _camera.FocusOn(_data.PreviewBounds.GetCenter());
        _camera.SetZoomScalar(Config.InitialZoom);
    }

    private void UpdatePresentation()
    {
        StrategicRegionPreviewRegion contextRegion = ResolveContextRegion();
        string contextCityId = ResolveContextCityId(contextRegion);
        float hoveredMaskId = ResolveMaskId(_hoveredRegionId);
        float selectedMaskId = ResolveMaskId(_selectedRegionId);
        float contextCityMetadataId = string.IsNullOrEmpty(contextCityId)
            ? 0f
            : _cityMetadataIdByCityId.GetValueOrDefault(contextCityId);

        foreach (StrategicRegionOverlayChunk overlay in _overlayVisuals)
        {
            overlay.SetPresentation(hoveredMaskId, selectedMaskId, contextCityMetadataId);
        }

        foreach (StrategicCityAnchorVisual city in _cityVisuals)
        {
            city.ApplyState(
                city.CityId == _hoveredCityId || city.CityId == contextCityId && string.IsNullOrEmpty(_selectedCityId),
                city.CityId == _selectedCityId);
        }

        StrategicRegionPreviewCity contextCity = string.IsNullOrEmpty(contextCityId)
            ? null
            : _cityById.GetValueOrDefault(contextCityId);
        bool locked = !string.IsNullOrEmpty(_selectedCityId) || !string.IsNullOrEmpty(_selectedRegionId);
        _hud.ShowContext(contextCity, contextRegion, locked);
    }

    private StrategicRegionPreviewRegion ResolveContextRegion()
    {
        if (!string.IsNullOrEmpty(_selectedRegionId))
        {
            return _regionById.GetValueOrDefault(_selectedRegionId);
        }

        return string.IsNullOrEmpty(_hoveredRegionId)
            ? null
            : _regionById.GetValueOrDefault(_hoveredRegionId);
    }

    private string ResolveContextCityId(StrategicRegionPreviewRegion contextRegion)
    {
        if (!string.IsNullOrEmpty(_selectedCityId))
        {
            return _selectedCityId;
        }

        if (contextRegion != null)
        {
            return contextRegion.CityId;
        }

        return _hoveredCityId;
    }

    private float ResolveMaskId(string regionId)
    {
        return !string.IsNullOrEmpty(regionId) && _regionById.TryGetValue(regionId, out StrategicRegionPreviewRegion region)
            ? region.MaskId
            : 0f;
    }

    private Texture2D BuildRegionMetadataTexture()
    {
        if (_data.Cities.Count > 255)
        {
            throw new InvalidOperationException($"Preview metadata supports at most 255 cities actual={_data.Cities.Count}");
        }

        Image metadata = Image.Create(MaskMetadataWidth, 1, false, Image.Format.Rgba8);
        for (int index = 0; index < _data.Cities.Count; index++)
        {
            _cityMetadataIdByCityId.Add(_data.Cities[index].CityId, index + 1);
        }

        // Each mask id owns one categorical metadata texel: faction code and city id. This keeps
        // membership data-driven for the complete byte mask range without a per-city region cap.
        foreach (StrategicRegionPreviewRegion region in _data.Regions)
        {
            int factionCode = region.CityId == Config.PlayerCityId
                ? PlayerFactionCode
                : region.CityId == Config.HostileCityId
                    ? HostileFactionCode
                    : 0;
            int cityMetadataId = _cityMetadataIdByCityId.GetValueOrDefault(region.CityId);
            metadata.SetPixel(
                region.MaskId,
                0,
                new Color(factionCode / 255f, cityMetadataId / 255f, 0f, 1f));
        }

        ImageTexture texture = ImageTexture.CreateFromImage(metadata);
        metadata.Dispose();
        return texture;
    }
}
