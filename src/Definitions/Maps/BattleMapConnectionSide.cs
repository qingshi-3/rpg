using Godot;

namespace Rpg.Definitions.Maps;

[GlobalClass]
public partial class BattleMapConnectionSide : Resource
{
    [Export]
    public Godot.Collections.Array<BattleMapConnectionPoint> Points { get; set; } = new();
}
