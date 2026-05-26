using Godot;
using Rpg.Definitions.Maps;

namespace Rpg.Presentation.Maps;

[Tool]
public partial class ObjectiveZoneMapMarker : SemanticMapMarker
{
    private static readonly Color ObjectiveFill = new(1.0f, 0.64f, 0.30f, 0.22f);
    private static readonly Color ObjectiveBorder = new(1.0f, 0.82f, 0.42f, 0.96f);

    [ExportGroup("Objective Routing")]

    [Export]
    public string ObjectiveRole { get; set; } = "";

    [Export]
    public SemanticDeploymentSide DeploymentSide { get; set; } = SemanticDeploymentSide.Any;

    [Export]
    public string FactionId { get; set; } = "";

    [Export]
    public int Priority { get; set; }

    protected override SemanticMapMarkerType ResolvedMarkerType => SemanticMapMarkerType.ObjectiveZone;

    protected override string ResolvedObjectiveRole => ObjectiveRole;

    protected override SemanticDeploymentSide ResolvedDeploymentSide => DeploymentSide;

    protected override string ResolvedFactionId => FactionId;

    protected override int ResolvedPriority => Priority;

    protected override Color PreviewFillColor => ObjectiveFill;

    protected override Color PreviewBorderColor => ObjectiveBorder;
}
