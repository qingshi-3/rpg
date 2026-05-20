namespace Rpg.Runtime.Battle;

public enum BattleRuntimeActorPhase
{
    AnchoredDecision = 0,
    Moving = 1,
    AttackWindup = 2,
    AttackRecovery = 3,
    WaitingForCharge = 4,
    Holding = 5,
    Defeated = 6
}
