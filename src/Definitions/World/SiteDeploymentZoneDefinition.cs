using System.Collections.Generic;
using Godot;

namespace Rpg.Definitions.World;

public sealed class SiteDeploymentZoneDefinition
{
    public string ZoneId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public SiteDeploymentZoneKind ZoneKind { get; set; } = SiteDeploymentZoneKind.DefaultGarrison;
    public int Capacity { get; set; }
    public List<Vector2I> Cells { get; set; } = new();
}
