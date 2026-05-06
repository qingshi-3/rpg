using Godot;
using Rpg.Definitions.Maps;

namespace Rpg.Presentation.Battle;

[GlobalClass]
public partial class BattleMapConnectionConfig : Node
{
    [Export]
    public Godot.Collections.Array<BattleMapConnection> Connections { get; set; } = new();
}
