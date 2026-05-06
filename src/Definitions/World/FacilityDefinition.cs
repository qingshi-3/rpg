using System.Collections.Generic;

namespace Rpg.Definitions.World;

public sealed class FacilityDefinition
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public FacilityType FacilityType { get; set; } = FacilityType.Support;
    public List<ResourceAmountDefinition> BuildCosts { get; set; } = new();
    public int BuildTimeTicks { get; set; }
    public List<string> RequiredSlotTags { get; set; } = new();
    public int MaxLevel { get; set; } = 1;
    public List<string> PassiveEffects { get; set; } = new();
    public List<string> Actions { get; set; } = new();
    public List<BattleModifierDefinition> BattleModifiers { get; set; } = new();
}
