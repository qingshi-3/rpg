using Godot;
using Rpg.Definitions.Maps;

namespace Rpg.Presentation.Maps;

[Tool]
public partial class ConstructionRegionMapMarker : SemanticMapMarker
{
    private static readonly Color RegionFill = new(0.34f, 0.92f, 0.82f, 0.16f);
    private static readonly Color RegionBorder = new(0.48f, 1.0f, 0.92f, 0.88f);

    [ExportGroup("Construction Region")]

    [Export]
    public int Priority { get; set; }

    protected override SemanticMapMarkerType ResolvedMarkerType => SemanticMapMarkerType.ConstructionRegion;

    protected override int ResolvedPriority => Priority;

    protected override Color PreviewFillColor => RegionFill;

    protected override Color PreviewBorderColor => RegionBorder;
}
