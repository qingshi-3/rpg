using Godot;

namespace Rpg.Presentation.Battle.Entities;

public partial class MovementComponent : BattleEntityComponent
{
    [Export]
    public int MoveRange { get; set; } = 4;

    [ExportGroup("地形")]

    [Export]
    public bool CanEnterWater { get; set; }

}
