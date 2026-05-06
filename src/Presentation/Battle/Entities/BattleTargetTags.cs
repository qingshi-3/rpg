using System;

namespace Rpg.Presentation.Battle.Entities;

[Flags]
public enum BattleTargetTags
{
    None = 0,
    Unit = 1 << 0,
    Object = 1 << 1,
    Obstacle = 1 << 2,
    Ally = 1 << 3,
    Enemy = 1 << 4,
    Destructible = 1 << 5
}
