namespace Rpg.Runtime.Battle;

public enum BattleGroupPlanRuntimeState
{
    Deploying = 0,
    AdvancingToObjective = 1,
    SensingContact = 2,
    TargetLocked = 3,
    MovingToAttackSlot = 4,
    Attacking = 5,
    RegroupingOrReturningToObjective = 6,
    Retreating = 7,
    Routed = 8,
    Defeated = 9
}

