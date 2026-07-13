#nullable enable

using Godot;

namespace Rpg.Presentation.StrategicMap;

[GlobalClass]
public partial class StrategicMapPresentationConfig : Resource
{
    [ExportGroup("Package Selection")]
    [Export] public string SelectionPath { get; set; } = "res://config/world/strategic-map-selection.json";

    [ExportGroup("Chunk Residency")]
    [Export(PropertyHint.Range, "0,2048,1")] public float PreloadMargin { get; set; } = 256f;
    [Export(PropertyHint.Range, "1,8,1")] public int MaxConcurrentChunkLoads { get; set; } = 2;

    [ExportGroup("Initial Inspection")]
    [Export] public Vector2 InitialFocusRatio { get; set; } = new(0.5f, 0.4f);
    [Export(PropertyHint.Range, "0.1,4,0.01")] public float InitialZoom { get; set; } = 0.48f;

    [ExportGroup("Campaign Region Treatment")]
    [Export] public Color PlayerControlColor { get; set; } = new("4faddf");
    [Export] public Color EnemyControlColor { get; set; } = new("d9604c");
    [Export] public Color NeutralProvinceColor { get; set; } = new("b5a46f");
    [Export(PropertyHint.Range, "0,1,0.01")] public float RegionFillAlpha { get; set; } = 0.055f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float CityBoundaryAlpha { get; set; } = 0.24f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float ProvinceBoundaryAlpha { get; set; } = 0.68f;
}
