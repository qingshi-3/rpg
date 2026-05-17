namespace Rpg.Runtime.Battle;

public enum BattleTerminationReason
{
    None = 0,
    NormalVictory = 1,
    NormalDefeat = 2,
    PlayerRetreat = 3,
    Interrupted = 4,
    RuntimeException = 5
}
