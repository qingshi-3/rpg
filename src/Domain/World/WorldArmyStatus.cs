namespace Rpg.Domain.World;

public enum WorldArmyStatus
{
    Idle = 0,
    Garrisoned = 1,
    Moving = 2,
    Attacking = 3,
    Retreating = 4,
    Defeated = 5,
    NavigationBlocked = 6
}
