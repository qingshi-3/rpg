using System.Linq;
using Godot;
using Rpg.Application.Maps;
using Rpg.Definitions.Maps;
using Rpg.Presentation.Battle;

namespace Rpg.Presentation.Maps;

[Tool]
public abstract partial class SemanticMapMarker : Node2D, ISemanticMapMarkerSource
{
    private static readonly Color AnchorColor = new(1.0f, 0.94f, 0.42f, 0.98f);
    private const int MaxMarkerSize = 64;

    [ExportGroup("Marker Identity")]

    [Export]
    public string MarkerId { get; set; } = "";

    [ExportGroup("Marker Region")]

    [Export(PropertyHint.Range, "1,64,1")]
    public int Width { get; set; } = 1;

    [Export(PropertyHint.Range, "1,64,1")]
    public int Height { get; set; } = 1;

    [Export]
    public int CellHeight { get; set; }

    [Export]
    public string[] Tags { get; set; } = System.Array.Empty<string>();

    [ExportGroup("Editor Preview")]

    [Export]
    public bool SnapToGrid { get; set; } = true;

    [Export]
    public bool DrawEditorPreview { get; set; } = true;

    protected abstract SemanticMapMarkerType ResolvedMarkerType { get; }

    protected virtual SemanticDeploymentSide ResolvedDeploymentSide => SemanticDeploymentSide.Any;

    protected virtual string ResolvedObjectiveRole => "";

    protected virtual string ResolvedFactionId => "";

    protected virtual int ResolvedPriority => 0;

    protected virtual Color PreviewFillColor => new(0.42f, 0.62f, 1.0f, 0.18f);

    protected virtual Color PreviewBorderColor => new(0.58f, 0.78f, 1.0f, 0.92f);

    public override void _Ready()
    {
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (!Engine.IsEditorHint())
        {
            return;
        }

        if (SnapToGrid)
        {
            SnapGlobalPositionToCoordinateGrid();
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!DrawEditorPreview)
        {
            return;
        }

        int width = ResolveSafeSize(Width);
        int height = ResolveSafeSize(Height);

        DrawRegionFill(width, height, PreviewFillColor);
        DrawRegionOutline(width, height, PreviewBorderColor);

        DrawCircle(Vector2.Zero, 4.5f, AnchorColor);
        DrawCircle(Vector2.Zero, 8.0f, new Color(AnchorColor.R, AnchorColor.G, AnchorColor.B, 0.18f));
    }

    public bool TryResolveSemanticMarkerData(string mapId, out SemanticMapMarkerData data, out string failureReason)
    {
        data = null;
        failureReason = "";

        if (string.IsNullOrWhiteSpace(MarkerId))
        {
            failureReason = $"semantic_marker_missing_id source={GetPath()}";
            return false;
        }

        if (!TryResolveAnchorCell(out Vector2I anchorCell))
        {
            failureReason = $"semantic_marker_anchor_invalid id={MarkerId} source={GetPath()}";
            return false;
        }

        data = new SemanticMapMarkerData
        {
            MapId = mapId ?? "",
            MarkerId = MarkerId,
            MarkerType = ResolvedMarkerType,
            DeploymentSide = ResolvedDeploymentSide,
            ObjectiveRole = ResolvedObjectiveRole ?? "",
            AnchorCell = anchorCell,
            CellHeight = CellHeight,
            Width = ResolveSafeSize(Width),
            Height = ResolveSafeSize(Height),
            FactionId = ResolvedFactionId ?? "",
            Priority = ResolvedPriority,
            SourcePath = GetPath().ToString()
        };
        data.Tags.AddRange((Tags ?? System.Array.Empty<string>())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim()));
        return true;
    }

    private void SnapGlobalPositionToCoordinateGrid()
    {
        if (!TryResolveCoordinateLayer(out BattleMapLayer coordinateLayer) ||
            !TryResolveAnchorCell(coordinateLayer, out Vector2I anchorCell))
        {
            return;
        }

        Vector2 snappedLocal = coordinateLayer.MapToLocal(anchorCell);
        GlobalPosition = coordinateLayer.ToGlobal(snappedLocal);
    }

    private bool TryResolveAnchorCell(out Vector2I anchorCell)
    {
        anchorCell = default;
        return TryResolveCoordinateLayer(out BattleMapLayer coordinateLayer) &&
               TryResolveAnchorCell(coordinateLayer, out anchorCell);
    }

    private bool TryResolveAnchorCell(BattleMapLayer coordinateLayer, out Vector2I anchorCell)
    {
        anchorCell = default;
        if (coordinateLayer == null)
        {
            return false;
        }

        anchorCell = coordinateLayer.LocalToMap(coordinateLayer.ToLocal(GlobalPosition));
        return true;
    }

    private bool TryResolveCoordinateLayer(out BattleMapLayer coordinateLayer)
    {
        coordinateLayer = null;
        Node current = this;
        while (current != null)
        {
            if (current is BattleMapView battleMapView)
            {
                coordinateLayer = battleMapView.CoordinateLayer ?? BattleMapLayerQueries.FindCoordinateLayer(battleMapView);
                return coordinateLayer != null;
            }

            current = current.GetParent();
        }

        Node root = GetTree()?.EditedSceneRoot ?? GetTree()?.CurrentScene;
        if (root != null)
        {
            coordinateLayer = BattleMapLayerQueries.EnumerateBattleMapLayers(root).FirstOrDefault();
        }

        return coordinateLayer != null;
    }

    private Vector2[] BuildCellPolygonLocal(int offsetX, int offsetY)
    {
        if (!TryResolveAnchorCell(out Vector2I anchorCell) ||
            !TryResolveCoordinateLayer(out BattleMapLayer coordinateLayer))
        {
            return BuildFallbackCellPolygon(offsetX, offsetY);
        }

        Vector2I cell = new(anchorCell.X + offsetX, anchorCell.Y + offsetY);
        Vector2 center = coordinateLayer.MapToLocal(cell);
        Vector2 stepX = coordinateLayer.MapToLocal(new Vector2I(cell.X + 1, cell.Y)) - center;
        Vector2 stepY = coordinateLayer.MapToLocal(new Vector2I(cell.X, cell.Y + 1)) - center;

        Vector2[] localPoints =
        {
            center - (stepX + stepY) * 0.5f,
            center + (stepX - stepY) * 0.5f,
            center + (stepX + stepY) * 0.5f,
            center + (-stepX + stepY) * 0.5f
        };

        return localPoints
            .Select(point => ToLocal(coordinateLayer.ToGlobal(point)))
            .ToArray();
    }

    private void DrawRegionFill(int width, int height, Color fill)
    {
        DrawColoredPolygon(BuildRegionOutlineLocal(width, height), fill);
    }

    private void DrawRegionOutline(int width, int height, Color border)
    {
        // Semantic markers are authored regions; drawing only the outer border keeps large footprints readable in-editor.
        DrawPolyline(ClosePolygon(BuildRegionOutlineLocal(width, height)), border, 1.75f, true);
    }

    private Vector2[] BuildRegionOutlineLocal(int width, int height)
    {
        Vector2[] topLeft = BuildCellPolygonLocal(0, 0);
        Vector2[] topRight = BuildCellPolygonLocal(width - 1, 0);
        Vector2[] bottomRight = BuildCellPolygonLocal(width - 1, height - 1);
        Vector2[] bottomLeft = BuildCellPolygonLocal(0, height - 1);

        return new[]
        {
            topLeft[0],
            topRight[1],
            bottomRight[2],
            bottomLeft[3]
        };
    }

    private static Vector2[] BuildFallbackCellPolygon(int offsetX, int offsetY)
    {
        const float cellSize = 16.0f;
        Vector2 origin = new(offsetX * cellSize, offsetY * cellSize);
        return new[]
        {
            origin,
            origin + new Vector2(cellSize, 0.0f),
            origin + new Vector2(cellSize, cellSize),
            origin + new Vector2(0.0f, cellSize)
        };
    }

    private static Vector2[] ClosePolygon(Vector2[] polygon)
    {
        Vector2[] closed = new Vector2[polygon.Length + 1];
        polygon.CopyTo(closed, 0);
        closed[^1] = polygon[0];
        return closed;
    }

    private static int ResolveSafeSize(int value)
    {
        return System.Math.Clamp(value, 1, MaxMarkerSize);
    }
}
