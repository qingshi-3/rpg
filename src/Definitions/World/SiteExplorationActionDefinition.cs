namespace Rpg.Definitions.World;

public sealed class SiteExplorationActionDefinition
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public int AlertDelta { get; set; }
    public bool ConsumesWorldTick { get; set; }
    public bool ResolvesPoint { get; set; }
    public bool StartsBattle { get; set; }
    public string BattleEncounterId { get; set; } = "";
    public string[] RevealsPointIds { get; set; } = System.Array.Empty<string>();
    public string[] RevealsEntranceIds { get; set; } = System.Array.Empty<string>();
    public string[] UnlocksFacilitySlotIds { get; set; } = System.Array.Empty<string>();
    public string[] ClearsHazardIds { get; set; } = System.Array.Empty<string>();
    public string[] AddsKnownTacticalTags { get; set; } = System.Array.Empty<string>();
    public string[] AddsExplorationAdvantageTags { get; set; } = System.Array.Empty<string>();
}
