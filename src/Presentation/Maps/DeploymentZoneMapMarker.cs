using Godot;
using Rpg.Definitions.Maps;

namespace Rpg.Presentation.Maps;

[Tool]
public partial class DeploymentZoneMapMarker : SemanticMapMarker
{
    private static readonly Color PlayerFill = new(0.46f, 1.0f, 0.56f, 0.20f);
    private static readonly Color PlayerBorder = new(0.62f, 1.0f, 0.70f, 0.95f);
    private static readonly Color EnemyFill = new(1.0f, 0.44f, 0.44f, 0.20f);
    private static readonly Color EnemyBorder = new(1.0f, 0.62f, 0.62f, 0.95f);
    private static readonly Color SharedFill = new(0.42f, 0.62f, 1.0f, 0.18f);
    private static readonly Color SharedBorder = new(0.58f, 0.78f, 1.0f, 0.92f);

    [ExportGroup("Deployment Routing")]

    [Export]
    public SemanticDeploymentSide DeploymentSide { get; set; } = SemanticDeploymentSide.Any;

    [Export]
    public string FactionId { get; set; } = "";

    [Export]
    public int Priority { get; set; }

    protected override SemanticMapMarkerType ResolvedMarkerType => SemanticMapMarkerType.DeploymentZone;

    protected override SemanticDeploymentSide ResolvedDeploymentSide => DeploymentSide;

    protected override string ResolvedFactionId => FactionId;

    protected override int ResolvedPriority => Priority;

    protected override Color PreviewFillColor => DeploymentSide switch
    {
        SemanticDeploymentSide.Player => PlayerFill,
        SemanticDeploymentSide.Enemy => EnemyFill,
        _ => SharedFill
    };

    protected override Color PreviewBorderColor => DeploymentSide switch
    {
        SemanticDeploymentSide.Player => PlayerBorder,
        SemanticDeploymentSide.Enemy => EnemyBorder,
        _ => SharedBorder
    };
}
