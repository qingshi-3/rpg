namespace Rpg.Runtime.Battle.AI;

public enum BattleRuntimeAiActionKind
{
    Hold = 0,
    AdvanceTowardTarget = 1,
    WaitForAttackCharge = 2,
    AttackTarget = 3,
    AdvanceTowardObjective = 4,
    JoinLocalCombat = 5,
    HoldSupport = 6,
    ReturnToObjective = 7,
    AdvanceTowardRegion = 8
}
