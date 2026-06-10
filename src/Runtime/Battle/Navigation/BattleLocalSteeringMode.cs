namespace Rpg.Runtime.Battle.Navigation;

public enum BattleLocalSteeringMode
{
    SeekGoal = 0,
    FollowObstacle = 1,
    RejoinSeek = 2,
    QueueOrHold = 3,
    StuckRecovery = 4
}
