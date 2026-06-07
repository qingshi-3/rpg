using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Application.Battle;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.World.Sites;

namespace Rpg.Presentation.Battle;

public partial class BattleGridHighlightOverlay : Node2D
{
    private const string PerceptionRangeShaderPath = "res://assets/battle/shaders/perception_range_highlight.gdshader";
    private const string PerceptionPulseSpeedParameter = "pulse_speed";
    private const string PerceptionPulseStrengthParameter = "pulse_strength";
    private const string PerceptionEdgeGlowParameter = "edge_glow";
    private const string PerceptionEdgeAlphaBoostParameter = "edge_alpha_boost";
    private const string PerceptionScanlineStrengthParameter = "scanline_strength";
    private const string PerceptionScanlineScaleParameter = "scanline_scale";

    [ExportGroup("Layering")]
    [Export]
    public int OverlayZIndex { get; set; } = 600;

    [ExportGroup("Hover")]

    [Export]
    public bool HoverEnabled { get; set; } = true;

    [Export]
    public Color HoverFillColor { get; set; } = new(1f, 1f, 1f, 0f);

    [Export]
    public Color HoverBorderColor { get; set; } = new(1f, 1f, 1f, 0.9f);

    [Export]
    public float HoverBorderWidth { get; set; } = 2.5f;

    [Export]
    public float HoverCornerLengthRatio { get; set; } = 0.22f;

    [ExportGroup("Range Colors")]

    [Export]
    public Color MoveColor { get; set; } = new(0.04f, 0.42f, 1f, 0.34f);

    [Export]
    public Color PathColor { get; set; } = new(1f, 0.82f, 0.18f, 0.34f);

    [Export]
    public Color ThreatColor { get; set; } = new(1f, 0.26f, 0.08f, 0.38f);

    [Export]
    // Attack range is a low-alpha planning background; target locks and unit focus carry the stronger confirmation.
    public Color AttackColor { get; set; } = new(1f, 0.08f, 0.04f, 0.18f);

    [Export]
    // Skill range uses the deployment-zone visual language: a quiet area fill plus one outer luminous boundary.
    public Color SkillRangeFillColor { get; set; } = new(0.10f, 0.82f, 1.0f, 0.08f);

    [Export]
    public Color SkillRangeBorderColor { get; set; } = new(0.42f, 0.96f, 1.0f, 0.88f);

    [Export(PropertyHint.Range, "1,16,0.5")]
    public float SkillRangeBorderWidth { get; set; } = 2.5f;

    [Export(PropertyHint.Range, "2,32,0.5")]
    public float SkillRangeGlowWidth { get; set; } = 11.0f;

    [Export]
    // Friendly hover uses green for mobility so it does not read as enemy intent.
    public Color FriendlyMoveColor { get; set; } = new(0.08f, 0.82f, 0.28f, 0.34f);

    [Export]
    // Enemy deployment zones are authoring constraints, not active threat telegraphs.
    public Color EnemyDeploymentColor { get; set; } = new(1f, 0.62f, 0.12f, 0.26f);

    [Export]
    // Debug-only local sensing range for player-side units.
    // Perception is tuning information, so it stays pale and below combat-threat contrast.
    public Color FriendlyPerceptionColor { get; set; } = new(0.62f, 0.88f, 1f, 0.095f);

    [Export]
    // Debug-only local sensing range for enemy-side units.
    public Color EnemyPerceptionColor { get; set; } = new(1f, 0.58f, 0.52f, 0.09f);

    [Export]
    // Friendly hover attack range is yellow to separate planning information from hostile threat red.
    public Color FriendlyAttackColor { get; set; } = new(1f, 0.82f, 0.12f, 0.28f);

    [Export]
    public Color SelectedColor { get; set; } = new(0.35f, 1f, 0.55f, 0.22f);

    [Export]
    public Color InvalidColor { get; set; } = new(1f, 0.04f, 0.02f, 0.38f);

    [Export]
    public float RangeBorderWidth { get; set; } = 1.5f;

    [ExportGroup("Perception Range Style")]

    [Export(PropertyHint.Range, "0,6,0.1")]
    public float PerceptionPulseSpeed { get; set; } = 1.15f;

    [Export(PropertyHint.Range, "0,0.45,0.01")]
    public float PerceptionPulseStrength { get; set; } = 0.12f;

    [Export(PropertyHint.Range, "0,2,0.01")]
    public float PerceptionEdgeGlow { get; set; } = 0.72f;

    [Export(PropertyHint.Range, "0,0.35,0.01")]
    public float PerceptionEdgeAlphaBoost { get; set; } = 0.08f;

    [Export(PropertyHint.Range, "0,0.5,0.01")]
    public float PerceptionScanlineStrength { get; set; } = 0.14f;

    [Export(PropertyHint.Range, "2,40,0.5")]
    public float PerceptionScanlineScale { get; set; } = 14f;

    [ExportGroup("Dynamic Range Style")]

    [Export]
    public bool EnableDynamicRangeStyle { get; set; } = true;

    [Export]
    public bool PulseThreatHighlights { get; set; } = true;

    [Export]
    public bool PulseAttackHighlights { get; set; } = true;

    [Export]
    public bool PulseTargetHighlights { get; set; } = true;

    [Export]
    public bool PulseSkillHighlights { get; set; } = true;

    [Export(PropertyHint.Range, "0.2,2.5,0.05")]
    public double DynamicPulseSeconds { get; set; } = 0.85;

    [Export(PropertyHint.Range, "0.1,1,0.05")]
    public float DynamicPulseMinAlphaMultiplier { get; set; } = 0.72f;

    [ExportGroup("Target Lock Style")]

    [Export]
    public bool ShowTargetLockRing { get; set; } = true;

    [Export]
    // Skill targeting reads as a unit focus, so the ground hint is one quiet footprint ring instead of per-cell arrows.
    public Color TargetLockRingColor { get; set; } = new(1f, 0.26f, 0.12f, 0.82f);

    [Export]
    public Color TargetLockGlowColor { get; set; } = new(1f, 0.12f, 0.06f, 0.24f);

    [Export(PropertyHint.Range, "1,12,0.25")]
    public float TargetLockRingWidth { get; set; } = 2.25f;

    [Export(PropertyHint.Range, "2,28,0.5")]
    public float TargetLockGlowWidth { get; set; } = 8.5f;

    [Export(PropertyHint.Range, "0,0.45,0.01")]
    public float TargetLockPaddingRatio { get; set; } = 0.12f;

    [Export(PropertyHint.Range, "0.2,0.8,0.01")]
    public float TargetLockVerticalScale { get; set; } = 0.38f;

    [ExportGroup("Path Arrows")]

    [Export]
    // Movement paths keep tile highlights, but direction arrows are off by default to reduce visual noise.
    public bool ShowPathArrows { get; set; } = BattlePathArrowPresentation.Default.ShowMovementPathArrows;

    [Export]
    public Color PathArrowColor { get; set; } = new(1f, 0.94f, 0.35f, 0.86f);

    [Export]
    public float PathArrowWidth { get; set; } = 2.2f;

    [Export]
    public float PathArrowHeadLength { get; set; } = 6f;

    [Export]
    public float PathArrowHeadAngleDegrees { get; set; } = 34f;

    [Export]
    public float PathArrowCellPaddingRatio { get; set; } = 0.28f;

    private readonly Dictionary<BattleGridHighlightKind, HashSet<GridPosition>> _cellsByKind = new();
    private readonly List<GridPosition> _pathCells = new();
    private readonly BattleGridHighlightTileLayerRenderer _tileLayerRenderer = new();

    private WorldSiteRoot _siteRoot;
    private BattleMapView _battleMapView;
    private BattleGridMap _gridMap;
    private BattleMapLayer _coordinateLayer;
    private Node2D _vectorOverlayRoot;
    private readonly HashSet<GridPosition> _hoverCells = new();
    private bool _hoverOverrideActive;
    private bool _tacticalPauseVisualsStatic;

    public override void _Ready()
    {
        ZIndex = OverlayZIndex;
        SetProcess(HoverEnabled);
        EnsureVectorOverlayRoot();

        _siteRoot = FindWorldSiteRoot();
        if (_siteRoot == null)
        {
            GD.PushWarning("BattleGridHighlightOverlay could not find WorldSiteRoot.");
            return;
        }

        _siteRoot.SiteMapLoaded += OnSiteMapLoaded;

        if (_siteRoot.ActiveSiteMap != null)
        {
            OnSiteMapLoaded(_siteRoot.ActiveSiteMap);
        }
    }

    public override void _Process(double delta)
    {
        if (!HoverEnabled || _battleMapView == null || _gridMap == null || _coordinateLayer == null)
        {
            if (!_hoverOverrideActive)
            {
                SetAutoHoverCell(null);
            }

            return;
        }

        if (_hoverOverrideActive)
        {
            return;
        }

        Vector2 mouseGlobal = _battleMapView.GetGlobalMousePosition();
        Vector2I tilePosition = _coordinateLayer.LocalToMap(_coordinateLayer.ToLocal(mouseGlobal));
        var position = new GridPosition(tilePosition.X, tilePosition.Y);

        if (TryResolveHoveredEntityFootprint(position, out IReadOnlyList<GridPosition> footprintCells))
        {
            SetHoverCells(footprintCells, overrideActive: false);
            return;
        }

        SetAutoHoverCell(_gridMap.TryGetCell(position, out _) ? position : null);
    }

    public void SetCells(BattleGridHighlightKind kind, IEnumerable<GridPosition> cells)
    {
        if (kind == BattleGridHighlightKind.Hover)
        {
            SetHoverCells(cells, overrideActive: true);
            return;
        }

        if (kind == BattleGridHighlightKind.Path)
        {
            SetPath(cells);
            return;
        }

        HashSet<GridPosition> nextCells = cells?.ToHashSet() ?? new HashSet<GridPosition>();
        _cellsByKind[kind] = nextCells;
        if (UsesTileLayer(kind))
        {
            _tileLayerRenderer.SetCells(kind, nextCells);
        }

        RebuildDynamicOverlay();
    }

    public void SetCellsBatch(params (BattleGridHighlightKind Kind, IEnumerable<GridPosition> Cells)[] updates)
    {
        if (updates == null || updates.Length == 0)
        {
            return;
        }

        // Hover previews can touch large ranges; batch updates avoid rebuilding thousands of overlay nodes per layer.
        foreach ((BattleGridHighlightKind kind, IEnumerable<GridPosition> cells) in updates)
        {
            if (kind == BattleGridHighlightKind.Hover)
            {
                SetHoverCellsState(cells, overrideActive: true);
                continue;
            }

            if (kind == BattleGridHighlightKind.Path)
            {
                GridPosition[] orderedCells = cells?.ToArray() ?? System.Array.Empty<GridPosition>();
                SetPathState(orderedCells);
                continue;
            }

            HashSet<GridPosition> nextCells = cells?.ToHashSet() ?? new HashSet<GridPosition>();
            _cellsByKind[kind] = nextCells;
            if (UsesTileLayer(kind))
            {
                _tileLayerRenderer.SetCells(kind, nextCells);
            }
        }

        RebuildDynamicOverlay();
    }

    public void SetPath(IEnumerable<GridPosition> cells)
    {
        GridPosition[] orderedCells = cells?.ToArray() ?? System.Array.Empty<GridPosition>();
        SetPathState(orderedCells);
        RebuildDynamicOverlay();
    }

    public void ClearCells(BattleGridHighlightKind kind)
    {
        if (kind == BattleGridHighlightKind.Hover)
        {
            SetHoverCells(System.Array.Empty<GridPosition>(), overrideActive: false);
            return;
        }

        if (kind == BattleGridHighlightKind.Path)
        {
            _pathCells.Clear();
        }
        bool removed = _cellsByKind.Remove(kind);
        if (UsesTileLayer(kind))
        {
            _tileLayerRenderer.ClearCells(kind);
        }

        if (removed || kind == BattleGridHighlightKind.Path || kind == BattleGridHighlightKind.Target)
        {
            RebuildDynamicOverlay();
        }
    }

    public void ClearAll()
    {
        _cellsByKind.Clear();
        _pathCells.Clear();
        _hoverCells.Clear();
        _hoverOverrideActive = false;
        _tileLayerRenderer.ClearAll();
        ClearDynamicOverlay();
    }

    public void SetTacticalPauseVisualsStatic(bool staticVisuals)
    {
        if (_tacticalPauseVisualsStatic == staticVisuals)
        {
            return;
        }

        _tacticalPauseVisualsStatic = staticVisuals;
        ConfigureTileLayers();
        ApplyAllCellLayers();
        RebuildDynamicOverlay();
    }

    private void OnSiteMapLoaded(Node activeSiteMap)
    {
        _battleMapView = activeSiteMap as BattleMapView;
        _battleMapView?.EnsureRuntimeData();
        _gridMap = _siteRoot?.ActiveGridMap ?? _battleMapView?.GridMap;
        _coordinateLayer = _battleMapView?.CoordinateLayer;
        _hoverCells.Clear();
        _hoverOverrideActive = false;
        _pathCells.Clear();
        _cellsByKind.Remove(BattleGridHighlightKind.Path);
        ConfigureTileLayers();
        ApplyAllCellLayers();
        RebuildDynamicOverlay();
    }

    private void SetAutoHoverCell(GridPosition? position)
    {
        SetHoverCells(
            position.HasValue ? new[] { position.Value } : System.Array.Empty<GridPosition>(),
            overrideActive: false);
    }

    private bool TryResolveHoveredEntityFootprint(GridPosition position, out IReadOnlyList<GridPosition> footprintCells)
    {
        footprintCells = System.Array.Empty<GridPosition>();
        BattleEntity entity = _siteRoot?.FindEntityAt(position);
        GridOccupantComponent gridOccupant = entity?.GetComponent<GridOccupantComponent>();
        if (gridOccupant == null)
        {
            return false;
        }

        footprintCells = BattleFootprintCells.Enumerate(
            gridOccupant.Position,
            gridOccupant.FootprintWidth,
            gridOccupant.FootprintHeight);
        return footprintCells.Count > 0;
    }

    private void SetHoverCells(IEnumerable<GridPosition> cells, bool overrideActive)
    {
        if (SetHoverCellsState(cells, overrideActive))
        {
            RebuildDynamicOverlay();
        }
    }

    private bool SetHoverCellsState(IEnumerable<GridPosition> cells, bool overrideActive)
    {
        HashSet<GridPosition> nextCells = cells?.ToHashSet() ?? new HashSet<GridPosition>();
        if (_hoverOverrideActive == overrideActive && _hoverCells.SetEquals(nextCells))
        {
            return false;
        }

        _hoverOverrideActive = overrideActive;
        _hoverCells.Clear();
        foreach (GridPosition cell in nextCells)
        {
            _hoverCells.Add(cell);
        }

        return true;
    }

    private void SetPathState(IReadOnlyList<GridPosition> orderedCells)
    {
        _pathCells.Clear();
        _pathCells.AddRange(orderedCells);
        HashSet<GridPosition> pathCells = orderedCells.Skip(1).ToHashSet();
        _cellsByKind[BattleGridHighlightKind.Path] = pathCells;
        _tileLayerRenderer.SetCells(BattleGridHighlightKind.Path, pathCells);
    }

    private void ConfigureTileLayers()
    {
        if (_coordinateLayer?.TileSet == null)
        {
            _tileLayerRenderer.Configure(
                this,
                null,
                null,
                System.Array.Empty<BattleGridHighlightKind>(),
                null);
            return;
        }

        BattleGridHighlightKind[] drawOrder = GetTileLayerDrawOrder().ToArray();
        BattleGridHighlightTileSetSpec tileSetSpec = BattleGridHighlightTileSetFactory.Create(
            _coordinateLayer.TileSet,
            BuildTileStyles(drawOrder),
            drawOrder);
        _tileLayerRenderer.Configure(
            this,
            _coordinateLayer,
            tileSetSpec,
            drawOrder,
            ConfigureHighlightLayer);
    }

    private void ConfigureHighlightLayer(TileMapLayer layer, BattleGridHighlightKind kind)
    {
        if (IsPerceptionKind(kind))
        {
            ApplyPerceptionRangeShader(layer);
            return;
        }

        ApplyDynamicRangeStyle(layer, kind);
    }

    private void ApplyPerceptionRangeShader(TileMapLayer layer)
    {
        if (layer == null)
        {
            return;
        }

        if (_tacticalPauseVisualsStatic)
        {
            layer.Material = null;
            return;
        }

        Shader shader = GD.Load<Shader>(PerceptionRangeShaderPath);
        if (shader == null)
        {
            GD.PushWarning($"BattleGridHighlightOverlay could not load perception shader: {PerceptionRangeShaderPath}.");
            return;
        }

        var material = new ShaderMaterial
        {
            Shader = shader
        };
        material.SetShaderParameter(PerceptionPulseSpeedParameter, PerceptionPulseSpeed);
        material.SetShaderParameter(PerceptionPulseStrengthParameter, PerceptionPulseStrength);
        material.SetShaderParameter(PerceptionEdgeGlowParameter, PerceptionEdgeGlow);
        material.SetShaderParameter(PerceptionEdgeAlphaBoostParameter, PerceptionEdgeAlphaBoost);
        material.SetShaderParameter(PerceptionScanlineStrengthParameter, PerceptionScanlineStrength);
        material.SetShaderParameter(PerceptionScanlineScaleParameter, PerceptionScanlineScale);
        layer.Material = material;
    }

    private void ApplyAllCellLayers()
    {
        foreach (BattleGridHighlightKind kind in GetTileLayerDrawOrder())
        {
            if (!_cellsByKind.TryGetValue(kind, out HashSet<GridPosition> cells))
            {
                continue;
            }

            _tileLayerRenderer.SetCells(kind, cells);
        }
    }

    private void RebuildDynamicOverlay()
    {
        ClearDynamicOverlay();

        if (_coordinateLayer == null)
        {
            return;
        }

        AddSkillRangeDeploymentStyle();
        AddPathArrows();
        AddTargetLockRing();

        if (_hoverCells.Count > 0)
        {
            AddHoverFrame(BuildHoverFramePolygon(_hoverCells));
        }
    }

    private void ClearDynamicOverlay()
    {
        EnsureVectorOverlayRoot();
        foreach (Node child in _vectorOverlayRoot.GetChildren())
        {
            _vectorOverlayRoot.RemoveChild(child);
            child.QueueFree();
        }
    }

    private void EnsureVectorOverlayRoot()
    {
        if (_vectorOverlayRoot != null && GodotObject.IsInstanceValid(_vectorOverlayRoot))
        {
            return;
        }

        _vectorOverlayRoot = GetNodeOrNull<Node2D>("RuntimeVectorOverlay");
        if (_vectorOverlayRoot != null)
        {
            return;
        }

        _vectorOverlayRoot = new Node2D
        {
            Name = "RuntimeVectorOverlay",
            // Child z-indices align with highlight kinds; a neutral root keeps range fills below target locks and hover frames.
            ZIndex = 0
        };
        AddChild(_vectorOverlayRoot);
    }

    private void AddSkillRangeDeploymentStyle()
    {
        if (!_cellsByKind.TryGetValue(BattleGridHighlightKind.Skill, out HashSet<GridPosition> cells) ||
            cells.Count == 0)
        {
            return;
        }

        int fillZIndex = (int)BattleGridHighlightKind.Skill * 2;
        int borderZIndex = fillZIndex + 1;
        Color glowColor = WithAlpha(
            SkillRangeBorderColor,
            Mathf.Clamp(SkillRangeBorderColor.A * 0.24f, 0.0f, 0.42f));

        foreach (GridPosition cell in cells.OrderBy(cell => cell.Y).ThenBy(cell => cell.X))
        {
            var fillNode = new Polygon2D
            {
                Polygon = BuildCellPolygon(cell),
                Color = SkillRangeFillColor,
                ZIndex = fillZIndex
            };
            _vectorOverlayRoot.AddChild(fillNode);
            ApplyDynamicRangeStyle(fillNode, BattleGridHighlightKind.Skill);
        }

        foreach ((Vector2 start, Vector2 end) in BuildBoundarySegments(cells))
        {
            var glowLine = new Line2D
            {
                Points = new[] { start, end },
                Width = SkillRangeGlowWidth,
                DefaultColor = glowColor,
                ZIndex = borderZIndex
            };
            _vectorOverlayRoot.AddChild(glowLine);
            ApplyDynamicRangeStyle(glowLine, BattleGridHighlightKind.Skill);

            var borderLine = new Line2D
            {
                Points = new[] { start, end },
                Width = SkillRangeBorderWidth,
                DefaultColor = SkillRangeBorderColor,
                ZIndex = borderZIndex + 1
            };
            _vectorOverlayRoot.AddChild(borderLine);
            ApplyDynamicRangeStyle(borderLine, BattleGridHighlightKind.Skill);
        }
    }

    private void AddCellHighlight(BattleGridHighlightKind kind, GridPosition cell)
    {
        Vector2[] polygon = BuildCellPolygon(cell);

        if (kind == BattleGridHighlightKind.Hover)
        {
            AddHoverFrame(polygon);
            return;
        }

        (Color fill, Color border, float borderWidth) = GetStyle(kind);

        var fillNode = new Polygon2D
        {
            Polygon = polygon,
            Color = fill,
            ZIndex = (int)kind * 2
        };
        _vectorOverlayRoot.AddChild(fillNode);
        ApplyDynamicRangeStyle(fillNode, kind);

        var borderNode = new Line2D
        {
            Points = ClosePolygon(polygon),
            Width = borderWidth,
            DefaultColor = border,
            Closed = true,
            ZIndex = (int)kind * 2 + 1
        };
        _vectorOverlayRoot.AddChild(borderNode);
        ApplyDynamicRangeStyle(borderNode, kind);

    }

    private void ApplyDynamicRangeStyle(CanvasItem item, BattleGridHighlightKind kind)
    {
        if (!ShouldAnimateOverlay(kind) || item == null || !IsInsideTree())
        {
            return;
        }

        float minAlpha = Mathf.Clamp(DynamicPulseMinAlphaMultiplier, 0.1f, 1f);
        double pulseSeconds = System.Math.Max(0.2, DynamicPulseSeconds);
        item.Modulate = new Color(1f, 1f, 1f, 1f);

        Tween tween = CreateTween();
        tween.BindNode(item);
        tween.SetLoops();
        tween.SetTrans(Tween.TransitionType.Sine);
        tween.SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(item, "modulate", new Color(1f, 1f, 1f, minAlpha), pulseSeconds);
        tween.TweenProperty(item, "modulate", Colors.White, pulseSeconds);
    }

    private void AddHoverFrame(Vector2[] polygon)
    {
        if (HoverFillColor.A > 0f)
        {
            _vectorOverlayRoot.AddChild(new Polygon2D
            {
                Polygon = polygon,
                Color = HoverFillColor,
                ZIndex = (int)BattleGridHighlightKind.Hover * 2
            });
        }

        float cornerLengthRatio = Mathf.Clamp(HoverCornerLengthRatio, 0.02f, 0.45f);

        for (int index = 0; index < polygon.Length; index++)
        {
            Vector2 corner = polygon[index];
            Vector2 previous = polygon[(index - 1 + polygon.Length) % polygon.Length];
            Vector2 next = polygon[(index + 1) % polygon.Length];

            AddHoverCornerSegment(corner, corner.Lerp(previous, cornerLengthRatio));
            AddHoverCornerSegment(corner, corner.Lerp(next, cornerLengthRatio));
        }
    }

    private void AddHoverCornerSegment(Vector2 start, Vector2 end)
    {
        _vectorOverlayRoot.AddChild(new Line2D
        {
            Points = new[] { start, end },
            Width = HoverBorderWidth,
            DefaultColor = HoverBorderColor,
            ZIndex = (int)BattleGridHighlightKind.Hover * 2 + 1
        });
    }

    private void AddTargetLockRing()
    {
        if (!ShowTargetLockRing ||
            !_cellsByKind.TryGetValue(BattleGridHighlightKind.Target, out HashSet<GridPosition> cells) ||
            cells.Count == 0)
        {
            return;
        }

        Vector2[] ringPoints = BuildTargetLockRingPoints(cells);
        if (ringPoints.Length < 8)
        {
            return;
        }

        int zIndex = (int)BattleGridHighlightKind.Target * 2 + 1;
        var glow = new Line2D
        {
            Points = ringPoints,
            Closed = true,
            Width = TargetLockGlowWidth,
            DefaultColor = TargetLockGlowColor,
            ZIndex = zIndex
        };
        _vectorOverlayRoot.AddChild(glow);
        ApplyDynamicRangeStyle(glow, BattleGridHighlightKind.Target);

        var ring = new Line2D
        {
            Points = ringPoints,
            Closed = true,
            Width = TargetLockRingWidth,
            DefaultColor = TargetLockRingColor,
            ZIndex = zIndex + 1
        };
        _vectorOverlayRoot.AddChild(ring);
        ApplyDynamicRangeStyle(ring, BattleGridHighlightKind.Target);
    }

    private Vector2[] BuildTargetLockRingPoints(HashSet<GridPosition> cells)
    {
        GridPosition[] targetCells = cells?.Distinct().ToArray() ?? System.Array.Empty<GridPosition>();
        if (targetCells.Length == 0)
        {
            return System.Array.Empty<Vector2>();
        }

        int minX = targetCells.Min(cell => cell.X);
        int maxX = targetCells.Max(cell => cell.X);
        int minY = targetCells.Min(cell => cell.Y);
        int maxY = targetCells.Max(cell => cell.Y);
        Vector2[] footprintPolygon = BuildCellBlockPolygon(minX, minY, maxX, maxY);
        float left = footprintPolygon.Min(point => point.X);
        float right = footprintPolygon.Max(point => point.X);
        float top = footprintPolygon.Min(point => point.Y);
        float bottom = footprintPolygon.Max(point => point.Y);
        Vector2 center = new((left + right) * 0.5f, (top + bottom) * 0.5f);
        float padding = Mathf.Min(right - left, bottom - top) * Mathf.Clamp(TargetLockPaddingRatio, 0f, 0.45f);
        float radiusX = Mathf.Max(6f, (right - left) * 0.5f + padding);
        float radiusY = Mathf.Max(4f, (bottom - top) * Mathf.Clamp(TargetLockVerticalScale, 0.2f, 0.8f));

        const int PointCount = 48;
        Vector2[] points = new Vector2[PointCount];
        for (int index = 0; index < PointCount; index++)
        {
            float angle = Mathf.Tau * index / PointCount;
            points[index] = center + new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
        }

        return points;
    }

    private void AddPathArrows()
    {
        if (!ShowPathArrows)
        {
            return;
        }

        if (_pathCells.Count < 2)
        {
            return;
        }

        for (int index = 0; index < _pathCells.Count - 1; index++)
        {
            AddPathArrow(_pathCells[index], _pathCells[index + 1]);
        }
    }

    private void AddPathArrow(GridPosition from, GridPosition to)
    {
        Vector2 fromCenter = BuildCellCenter(from);
        Vector2 toCenter = BuildCellCenter(to);
        Vector2 delta = toCenter - fromCenter;
        float length = delta.Length();
        if (length <= 0.01f)
        {
            return;
        }

        Vector2 direction = delta / length;
        float padding = Mathf.Min(
            length * 0.35f,
            GetCellHalfExtent(from) * Mathf.Clamp(PathArrowCellPaddingRatio, 0f, 0.45f));
        Vector2 start = fromCenter + direction * padding;
        Vector2 end = toCenter - direction * padding;

        if ((end - start).Length() <= 1f)
        {
            start = fromCenter;
            end = toCenter;
        }

        int zIndex = (int)BattleGridHighlightKind.Hover * 2 - 1;
        _vectorOverlayRoot.AddChild(new Line2D
        {
            Points = new[] { start, end },
            Width = PathArrowWidth,
            DefaultColor = PathArrowColor,
            ZIndex = zIndex
        });

        float headLength = Mathf.Min(PathArrowHeadLength, length * 0.32f);
        float headAngle = Mathf.DegToRad(PathArrowHeadAngleDegrees);
        Vector2 back = -direction;
        AddPathArrowHeadSegment(end, end + back.Rotated(headAngle) * headLength, zIndex);
        AddPathArrowHeadSegment(end, end + back.Rotated(-headAngle) * headLength, zIndex);
    }

    private void AddPathArrowHeadSegment(Vector2 start, Vector2 end, int zIndex)
    {
        _vectorOverlayRoot.AddChild(new Line2D
        {
            Points = new[] { start, end },
            Width = PathArrowWidth,
            DefaultColor = PathArrowColor,
            ZIndex = zIndex
        });
    }

    private Vector2 BuildCellCenter(GridPosition cell)
    {
        var origin = new Vector2I(cell.X, cell.Y);
        return ToLocal(_coordinateLayer.ToGlobal(_coordinateLayer.MapToLocal(origin)));
    }

    private float GetCellHalfExtent(GridPosition cell)
    {
        Vector2 center = BuildCellCenter(cell);
        Vector2 right = BuildCellCenter(new GridPosition(cell.X + 1, cell.Y));
        Vector2 down = BuildCellCenter(new GridPosition(cell.X, cell.Y + 1));
        return Mathf.Min((right - center).Length(), (down - center).Length()) * 0.5f;
    }

    private Vector2[] BuildCellPolygon(GridPosition cell)
    {
        var origin = new Vector2I(cell.X, cell.Y);
        Vector2 center = _coordinateLayer.MapToLocal(origin);
        Vector2 stepX = _coordinateLayer.MapToLocal(new Vector2I(cell.X + 1, cell.Y)) - center;
        Vector2 stepY = _coordinateLayer.MapToLocal(new Vector2I(cell.X, cell.Y + 1)) - center;

        Vector2[] localPoints =
        {
            center - (stepX + stepY) * 0.5f,
            center + (stepX - stepY) * 0.5f,
            center + (stepX + stepY) * 0.5f,
            center + (-stepX + stepY) * 0.5f
        };

        return localPoints
            .Select(point => ToLocal(_coordinateLayer.ToGlobal(point)))
            .ToArray();
    }

    private IEnumerable<(Vector2 Start, Vector2 End)> BuildBoundarySegments(HashSet<GridPosition> cells)
    {
        foreach (GridPosition cell in cells.OrderBy(cell => cell.Y).ThenBy(cell => cell.X))
        {
            Vector2[] polygon = BuildCellPolygon(cell);

            if (!cells.Contains(new GridPosition(cell.X, cell.Y - 1)))
            {
                yield return (polygon[0], polygon[1]);
            }

            if (!cells.Contains(new GridPosition(cell.X + 1, cell.Y)))
            {
                yield return (polygon[1], polygon[2]);
            }

            if (!cells.Contains(new GridPosition(cell.X, cell.Y + 1)))
            {
                yield return (polygon[2], polygon[3]);
            }

            if (!cells.Contains(new GridPosition(cell.X - 1, cell.Y)))
            {
                yield return (polygon[3], polygon[0]);
            }
        }
    }

    private Vector2[] BuildHoverFramePolygon(IEnumerable<GridPosition> cells)
    {
        GridPosition[] hoverCells = cells?.Distinct().ToArray() ?? System.Array.Empty<GridPosition>();
        if (hoverCells.Length == 0)
        {
            return System.Array.Empty<Vector2>();
        }

        if (hoverCells.Length == 1)
        {
            return BuildCellPolygon(hoverCells[0]);
        }

        // Explicit hover is used by rectangular deployment footprints; one outer frame preserves the existing hover language without tile fills.
        int minX = hoverCells.Min(cell => cell.X);
        int maxX = hoverCells.Max(cell => cell.X);
        int minY = hoverCells.Min(cell => cell.Y);
        int maxY = hoverCells.Max(cell => cell.Y);
        return BuildCellBlockPolygon(minX, minY, maxX, maxY);
    }

    private Vector2[] BuildCellBlockPolygon(int minX, int minY, int maxX, int maxY)
    {
        var topLeftOrigin = new Vector2I(minX, minY);
        Vector2 topLeftCenter = _coordinateLayer.MapToLocal(topLeftOrigin);
        Vector2 topRightCenter = _coordinateLayer.MapToLocal(new Vector2I(maxX, minY));
        Vector2 bottomRightCenter = _coordinateLayer.MapToLocal(new Vector2I(maxX, maxY));
        Vector2 bottomLeftCenter = _coordinateLayer.MapToLocal(new Vector2I(minX, maxY));
        Vector2 stepX = _coordinateLayer.MapToLocal(new Vector2I(minX + 1, minY)) - topLeftCenter;
        Vector2 stepY = _coordinateLayer.MapToLocal(new Vector2I(minX, minY + 1)) - topLeftCenter;

        Vector2[] localPoints =
        {
            topLeftCenter - (stepX + stepY) * 0.5f,
            topRightCenter + (stepX - stepY) * 0.5f,
            bottomRightCenter + (stepX + stepY) * 0.5f,
            bottomLeftCenter + (-stepX + stepY) * 0.5f
        };

        return localPoints
            .Select(point => ToLocal(_coordinateLayer.ToGlobal(point)))
            .ToArray();
    }

    private static Vector2[] ClosePolygon(Vector2[] polygon)
    {
        Vector2[] closed = new Vector2[polygon.Length + 1];
        polygon.CopyTo(closed, 0);
        closed[^1] = polygon[0];
        return closed;
    }

    private (Color fill, Color border, float borderWidth) GetStyle(BattleGridHighlightKind kind)
    {
        return kind switch
        {
            BattleGridHighlightKind.Move => (MoveColor, WithAlpha(MoveColor, 0.76f), RangeBorderWidth),
            BattleGridHighlightKind.Path => (PathColor, WithAlpha(PathColor, 0.72f), RangeBorderWidth + 0.35f),
            BattleGridHighlightKind.Threat => (ThreatColor, WithAlpha(ThreatColor, 0.82f), RangeBorderWidth + 0.2f),
            BattleGridHighlightKind.Attack => (AttackColor, WithAlpha(AttackColor, 0.42f), RangeBorderWidth),
            BattleGridHighlightKind.Skill => (SkillRangeFillColor, SkillRangeBorderColor, SkillRangeBorderWidth),
            BattleGridHighlightKind.Target => (WithAlpha(TargetLockRingColor, 0f), TargetLockRingColor, TargetLockRingWidth),
            BattleGridHighlightKind.FriendlyMove => (FriendlyMoveColor, WithAlpha(FriendlyMoveColor, 0.78f), RangeBorderWidth),
            BattleGridHighlightKind.EnemyDeployment => (EnemyDeploymentColor, WithAlpha(EnemyDeploymentColor, 0.76f), RangeBorderWidth),
            BattleGridHighlightKind.FriendlyPerception => (FriendlyPerceptionColor, WithAlpha(FriendlyPerceptionColor, 0.18f), 0f),
            BattleGridHighlightKind.EnemyPerception => (EnemyPerceptionColor, WithAlpha(EnemyPerceptionColor, 0.17f), 0f),
            BattleGridHighlightKind.FriendlyAttack => (FriendlyAttackColor, WithAlpha(FriendlyAttackColor, 0.84f), RangeBorderWidth + 0.2f),
            BattleGridHighlightKind.Selected => (SelectedColor, WithAlpha(SelectedColor, 0.62f), RangeBorderWidth),
            BattleGridHighlightKind.Invalid => (InvalidColor, WithAlpha(InvalidColor, 0.45f), RangeBorderWidth),
            BattleGridHighlightKind.Hover => (HoverFillColor, HoverBorderColor, HoverBorderWidth),
            _ => (HoverFillColor, HoverBorderColor, HoverBorderWidth)
        };
    }

    private Dictionary<BattleGridHighlightKind, BattleGridHighlightStyle> BuildTileStyles(IEnumerable<BattleGridHighlightKind> kinds)
    {
        Dictionary<BattleGridHighlightKind, BattleGridHighlightStyle> styles = new();
        foreach (BattleGridHighlightKind kind in kinds)
        {
            (Color fill, Color border, float borderWidth) = GetStyle(kind);
            BattleGridHighlightTileShape shape = kind switch
            {
                BattleGridHighlightKind.FriendlyMove or BattleGridHighlightKind.EnemyDeployment => BattleGridHighlightTileShape.Square,
                BattleGridHighlightKind.FriendlyPerception or BattleGridHighlightKind.EnemyPerception => BattleGridHighlightTileShape.SoftAura,
                _ => BattleGridHighlightTileShape.Diamond
            };
            styles[kind] = new BattleGridHighlightStyle(fill, border, borderWidth, shape);
        }

        return styles;
    }

    private static bool IsPerceptionKind(BattleGridHighlightKind kind)
    {
        return kind is BattleGridHighlightKind.FriendlyPerception or BattleGridHighlightKind.EnemyPerception;
    }

    private bool ShouldPulse(BattleGridHighlightKind kind)
    {
        return EnableDynamicRangeStyle &&
               ((kind == BattleGridHighlightKind.Threat && PulseThreatHighlights) ||
                (kind == BattleGridHighlightKind.Attack && PulseAttackHighlights) ||
                (kind == BattleGridHighlightKind.FriendlyAttack && PulseAttackHighlights) ||
                (kind == BattleGridHighlightKind.Target && PulseTargetHighlights) ||
                (kind == BattleGridHighlightKind.Skill && PulseSkillHighlights));
    }

    private bool ShouldAnimateOverlay(BattleGridHighlightKind kind)
    {
        // Tactical pause keeps overlay data responsive to player input but makes
        // every battlefield-space hint static; pause readability belongs to UI
        // state or a later pause filter, not battle-time animation.
        return !_tacticalPauseVisualsStatic && ShouldPulse(kind);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        return new Color(color.R, color.G, color.B, alpha);
    }

    private static bool UsesTileLayer(BattleGridHighlightKind kind)
    {
        return kind is not BattleGridHighlightKind.Skill and not BattleGridHighlightKind.Target;
    }

    private static IEnumerable<BattleGridHighlightKind> GetTileLayerDrawOrder()
    {
        yield return BattleGridHighlightKind.Move;
        yield return BattleGridHighlightKind.Path;
        yield return BattleGridHighlightKind.Threat;
        yield return BattleGridHighlightKind.Attack;
        yield return BattleGridHighlightKind.FriendlyMove;
        yield return BattleGridHighlightKind.EnemyDeployment;
        yield return BattleGridHighlightKind.FriendlyPerception;
        yield return BattleGridHighlightKind.EnemyPerception;
        yield return BattleGridHighlightKind.FriendlyAttack;
        yield return BattleGridHighlightKind.Invalid;
    }

    private WorldSiteRoot FindWorldSiteRoot()
    {
        Node current = this;

        while (current != null)
        {
            if (current is WorldSiteRoot siteRoot)
            {
                return siteRoot;
            }

            current = current.GetParent();
        }

        return null;
    }
}
