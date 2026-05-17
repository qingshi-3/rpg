using Godot;

namespace Rpg.Definitions.Corps;

[GlobalClass]
public partial class CorpsDefinition : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "兵团";
    [Export] public CorpsCombatClass CombatClass { get; set; } = CorpsCombatClass.Infantry;
    [Export] public string FormId { get; set; } = "";
    [Export] public int MaxVisibleSoldiers { get; set; } = 5;
    [Export] public Godot.Collections.Array<string> Tags { get; set; } = new();
    [Export] public Godot.Collections.Array<string> AbilityIds { get; set; } = new();
}
