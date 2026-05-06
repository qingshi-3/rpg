using Godot;

namespace Rpg.Definitions.Battle.Encounters;

[GlobalClass]
public partial class EncounterDefinition : Resource
{
    [Export]
    public string Id { get; set; } = "";

    [Export]
    public string DisplayName { get; set; } = "战斗遭遇";

    [Export]
    public PackedScene BattleScene { get; set; }
}
