using Godot;
namespace Rpg.Definitions.Maps;

[GlobalClass]
public partial class BattleMapDefinition : Resource
{
	[Export]
	public Vector2I Size { get; set; } = new(6, 6);

	[Export]
	public PackedScene MapScene { get; set; }
}
