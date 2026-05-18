using Godot;
using Rpg.Definitions.Maps;

namespace Rpg.Presentation.Maps;

[Tool]
public partial class BuildingSlotMapMarker : SemanticMapMarker
{
    private static readonly Color BuildingFill = new(0.95f, 0.76f, 0.34f, 0.18f);
    private static readonly Color BuildingBorder = new(1.0f, 0.86f, 0.46f, 0.92f);

    protected override SemanticMapMarkerType ResolvedMarkerType => SemanticMapMarkerType.BuildingSlot;

    protected override Color PreviewFillColor => BuildingFill;

    protected override Color PreviewBorderColor => BuildingBorder;
}
