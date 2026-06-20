using Godot;
using Rpg.Definitions.Maps;

namespace Rpg.Presentation.Maps;

[Tool]
public partial class BridgeMapMarker : SemanticMapMarker
{
    private static readonly Color RiverFill = new(0.44f, 0.86f, 1.0f, 0.22f);
    private static readonly Color RiverBorder = new(0.66f, 0.94f, 1.0f, 0.96f);
    private static readonly Color HeightFill = new(0.84f, 0.68f, 1.0f, 0.24f);
    private static readonly Color HeightBorder = new(0.92f, 0.82f, 1.0f, 0.96f);

    [ExportGroup("Bridge Routing")]

    [Export]
    public SemanticBridgeKind BridgeKind { get; set; } = SemanticBridgeKind.RiverBridge;

    [Export]
    public string[] ConnectionIds { get; set; } = System.Array.Empty<string>();

    [Export]
    public int Priority { get; set; }

    protected override SemanticMapMarkerType ResolvedMarkerType => SemanticMapMarkerType.Bridge;

    protected override SemanticBridgeKind ResolvedBridgeKind => BridgeKind;

    protected override string[] ResolvedConnectionIds => ConnectionIds;

    protected override int ResolvedPriority => Priority;

    protected override Color PreviewFillColor => BridgeKind == SemanticBridgeKind.HeightBridge ? HeightFill : RiverFill;

    protected override Color PreviewBorderColor => BridgeKind == SemanticBridgeKind.HeightBridge ? HeightBorder : RiverBorder;
}
