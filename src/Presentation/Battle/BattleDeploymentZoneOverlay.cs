using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.World.Sites;

namespace Rpg.Presentation.Battle;

public partial class BattleDeploymentZoneOverlay : Node2D
{
    private const string DeploymentZoneShaderPath = "res://assets/battle/shaders/deployment_zone_highlight.gdshader";
    private static readonly StringName PulseSpeedParameter = "pulse_speed";
    private static readonly StringName PulseStrengthParameter = "pulse_strength";
    private static readonly StringName GlowBoostParameter = "glow_boost";

    [Export]
    public int OverlayZIndex { get; set; } = 520;

    [Export]
    public Color PlayerFillColor { get; set; } = new(0.10f, 0.95f, 0.34f, 0.08f);

    [Export]
    public Color PlayerBorderColor { get; set; } = new(0.30f, 1.0f, 0.46f, 0.92f);

    [Export]
    public Color EnemyFillColor { get; set; } = new(1.0f, 0.42f, 0.12f, 0.06f);

    [Export]
    public Color EnemyBorderColor { get; set; } = new(1.0f, 0.62f, 0.22f, 0.86f);

    [Export(PropertyHint.Range, "1,16,0.5")]
    public float BorderWidth { get; set; } = 2.5f;

    [Export(PropertyHint.Range, "2,32,0.5")]
    public float GlowWidth { get; set; } = 11.0f;

    [Export(PropertyHint.Range, "0,8,0.1")]
    public float PulseSpeed { get; set; } = 3.2f;

    [Export(PropertyHint.Range, "0,0.75,0.01")]
    public float PulseStrength { get; set; } = 0.34f;

    private readonly HashSet<GridPosition> _playerCells = new();
    private readonly HashSet<GridPosition> _enemyCells = new();
    private WorldSiteRoot _siteRoot;
    private BattleMapLayer _coordinateLayer;

    public override void _Ready()
    {
        ZIndex = OverlayZIndex;
        Visible = false;
        SetProcess(true);
        ConfigureShaderMaterial();

        _siteRoot = FindWorldSiteRoot();
        if (_siteRoot != null)
        {
            _siteRoot.SiteMapLoaded += OnSiteMapLoaded;
            if (_siteRoot.ActiveSiteMap != null)
            {
                OnSiteMapLoaded(_siteRoot.ActiveSiteMap);
            }
        }
    }

    public override void _ExitTree()
    {
        if (_siteRoot != null)
        {
            _siteRoot.SiteMapLoaded -= OnSiteMapLoaded;
        }
    }

    public override void _Process(double delta)
    {
        if (Visible && (_playerCells.Count > 0 || _enemyCells.Count > 0))
        {
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        if (_coordinateLayer == null)
        {
            return;
        }

        DrawDeploymentZone(_playerCells, PlayerFillColor, PlayerBorderColor);
        DrawDeploymentZone(_enemyCells, EnemyFillColor, EnemyBorderColor);
    }

    public void SetZones(IEnumerable<GridPosition> playerCells, IEnumerable<GridPosition> enemyCells)
    {
        ReplaceCells(_playerCells, playerCells);
        ReplaceCells(_enemyCells, enemyCells);
        Visible = _playerCells.Count > 0 || _enemyCells.Count > 0;
        QueueRedraw();
    }

    public void ClearZones()
    {
        _playerCells.Clear();
        _enemyCells.Clear();
        Visible = false;
        QueueRedraw();
    }

    private void ConfigureShaderMaterial()
    {
        Shader shader = GD.Load<Shader>(DeploymentZoneShaderPath);
        if (shader == null)
        {
            GD.PushWarning($"BattleDeploymentZoneOverlay could not load shader: {DeploymentZoneShaderPath}.");
            return;
        }

        var material = new ShaderMaterial
        {
            Shader = shader
        };
        material.SetShaderParameter(PulseSpeedParameter, PulseSpeed);
        material.SetShaderParameter(PulseStrengthParameter, PulseStrength);
        material.SetShaderParameter(GlowBoostParameter, 0.28f);
        Material = material;
    }

    private void OnSiteMapLoaded(Node activeSiteMap)
    {
        BattleMapView mapView = activeSiteMap as BattleMapView;
        mapView?.EnsureRuntimeData();
        _coordinateLayer = mapView?.CoordinateLayer;
        QueueRedraw();
    }

    private void DrawDeploymentZone(HashSet<GridPosition> cells, Color fill, Color border)
    {
        if (cells.Count == 0)
        {
            return;
        }

        float pulse = 1.0f + Mathf.Sin(Time.GetTicksMsec() / 1000.0f * PulseSpeed) * PulseStrength;
        Color fillColor = WithAlpha(fill, Mathf.Clamp(fill.A * pulse, 0.0f, 0.18f));
        Color glowColor = WithAlpha(border, Mathf.Clamp(border.A * 0.24f * pulse, 0.0f, 0.42f));
        Color borderColor = WithAlpha(border, Mathf.Clamp(border.A * pulse, 0.0f, 1.0f));

        foreach (GridPosition cell in cells.OrderBy(cell => cell.Y).ThenBy(cell => cell.X))
        {
            DrawColoredPolygon(BuildCellPolygon(cell), fillColor);
        }

        foreach ((Vector2 start, Vector2 end) in BuildBoundarySegments(cells))
        {
            DrawLine(start, end, glowColor, GlowWidth, true);
            DrawLine(start, end, borderColor, BorderWidth, true);
        }
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

    private static void ReplaceCells(HashSet<GridPosition> target, IEnumerable<GridPosition> cells)
    {
        target.Clear();
        foreach (GridPosition cell in cells?.Distinct() ?? System.Array.Empty<GridPosition>())
        {
            target.Add(cell);
        }
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        return new Color(color.R, color.G, color.B, alpha);
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
