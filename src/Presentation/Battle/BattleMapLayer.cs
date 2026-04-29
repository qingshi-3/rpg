using Godot;
using Rpg.Domain.Battle.Grid;

namespace Rpg.Presentation.Battle;

public partial class BattleMapLayer : TileMapLayer
{
    [ExportGroup("图层语义")]

    [Export]
    public LayerRole Role { get; set; } = LayerRole.Foundation;

    [Export]
    public int Height { get; set; }

    [ExportGroup("战斗逻辑影响")]

    [Export]
    public bool AffectsWalkability { get; set; }

    [Export]
    public bool AffectsLineOfSight { get; set; }

    [Export]
    public bool IsHeightTransitionLayer { get; set; }

    [Export]
    public bool IsVisualOnly { get; set; }
}
