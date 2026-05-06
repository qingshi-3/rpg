using Godot;

namespace Rpg.Presentation.Battle.Entities;

public partial class FactionComponent : BattleEntityComponent
{
    [Export]
    public BattleFaction Faction { get; set; } = BattleFaction.Neutral;
}
