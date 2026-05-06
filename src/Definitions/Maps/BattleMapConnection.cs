using Godot;

namespace Rpg.Definitions.Maps;

[GlobalClass]
public partial class BattleMapConnection : Resource
{
    [Export]
    public string Id { get; set; } = "";

    [Export]
    public BattleMapConnectionType Type { get; set; } = BattleMapConnectionType.Stair;

    [Export]
    public BattleMapConnectionSide SideA { get; set; } = new();

    [Export]
    public BattleMapConnectionSide SideB { get; set; } = new();

    [Export]
    public int MoveCost { get; set; } = 1;

    [Export]
    public bool Bidirectional { get; set; } = true;
}
