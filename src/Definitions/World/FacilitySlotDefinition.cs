using System.Collections.Generic;

namespace Rpg.Definitions.World;

public sealed class FacilitySlotDefinition
{
    public string SlotId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public List<string> AllowedFacilityIds { get; set; } = new();
    public string InitialFacilityId { get; set; } = "";
    public string BattleAnchorId { get; set; } = "";
}
