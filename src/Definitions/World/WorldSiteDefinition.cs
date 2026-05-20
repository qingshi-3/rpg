using System.Collections.Generic;
using Godot;
using Rpg.Domain.World;

namespace Rpg.Definitions.World;

public sealed class WorldSiteDefinition
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public WorldSiteKind SiteKind { get; set; } = WorldSiteKind.ResourceSite;
    public string Description { get; set; } = "";
    public string SiteScenePath { get; set; } = "";
    public WorldSiteIntelDefinition Intel { get; set; }
    public Vector2 MapPosition { get; set; }
    public string InitialOwnerFactionId { get; set; } = "";
    public SiteControlState InitialControlState { get; set; } = SiteControlState.Unknown;
    public List<FacilitySlotDefinition> FacilitySlots { get; set; } = new();
    public string DefaultGarrisonZoneId { get; set; } = "";
    public List<SiteDeploymentZoneDefinition> DeploymentZones { get; set; } = new();
    public List<string> InitialFacilities { get; set; } = new();
    public List<GarrisonDefinition> InitialGarrison { get; set; } = new();
    public List<SiteAutoGarrisonProductionDefinition> AutoGarrisonProductions { get; set; } = new();
    public List<string> BattleAnchors { get; set; } = new();
    public List<BattleEntranceDefinition> EntranceDefinitions { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}
