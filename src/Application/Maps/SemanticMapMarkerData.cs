using System.Collections.Generic;
using System.Linq;
using Godot;
using Rpg.Definitions.Maps;

namespace Rpg.Application.Maps;

public sealed class SemanticMapMarkerData
{
    public string MapId { get; set; } = "";
    public string MarkerId { get; set; } = "";
    public SemanticMapMarkerType MarkerType { get; set; } = SemanticMapMarkerType.BuildingSlot;
    public SemanticDeploymentSide DeploymentSide { get; set; } = SemanticDeploymentSide.Any;
    public string ObjectiveRole { get; set; } = "";
    public Vector2I AnchorCell { get; set; }
    public int CellHeight { get; set; }
    public int Width { get; set; } = 1;
    public int Height { get; set; } = 1;
    public string FactionId { get; set; } = "";
    public int Priority { get; set; }
    public List<string> Tags { get; } = new();
    public string SourcePath { get; set; } = "";

    public IReadOnlyList<Vector2I> CoveredCells => BuildCoveredCells();

    private IReadOnlyList<Vector2I> BuildCoveredCells()
    {
        int width = System.Math.Clamp(Width, 1, 64);
        int height = System.Math.Clamp(Height, 1, 64);
        return Enumerable.Range(0, height)
            .SelectMany(y => Enumerable.Range(0, width)
                .Select(x => new Vector2I(AnchorCell.X + x, AnchorCell.Y + y)))
            .ToArray();
    }
}
