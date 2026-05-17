using Godot;

namespace Rpg.Definitions.Equipment;

[GlobalClass]
public partial class EquipmentDefinition : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "装备";
    [Export] public string Slot { get; set; } = "";
    [Export] public string Grade { get; set; } = "common";
    [Export] public Godot.Collections.Array<string> Tags { get; set; } = new();
}
