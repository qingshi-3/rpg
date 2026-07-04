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
    private readonly BattleGridVectorHighlightRenderer _vectorHighlightRenderer = new();
    private readonly BattleGridHighlightGeometry _highlightGeometry;

    private WorldSiteRoot _siteRoot;
    private BattleMapView _battleMapView;
    private BattleGridMap _gridMap;
    private BattleMapLayer _coordinateLayer;
    private Node2D _vectorOverlayRoot;
    private readonly HashSet<GridPosition> _hoverCells = new();
    private bool _hoverOverrideActive;
    private bool _tacticalPauseVisualsStatic;

    public BattleGridHighlightOverlay()
    {
        _highlightGeometry = new BattleGridHighlightGeometry(this, () => _coordinateLayer);
    }

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

    public override void _ExitTree()
    {
        _siteRoot?.SetHoveredBattleRuntimeEntity("");
        if (_siteRoot != null)
        {
            _siteRoot.SiteMapLoaded -= OnSiteMapLoaded;
        }
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


}
