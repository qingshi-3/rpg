namespace Rpg.Runtime.Battle.Events;

public enum BattleEventKind
{
    BattleStarted = 0,
    CommandAccepted = 1,
    CommandRejected = 2,
    MovementCompleted = 3,
    DamageApplied = 4,
    CorpsStrengthChanged = 5,
    BattleEnded = 6
}
