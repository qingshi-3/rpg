namespace Rpg.Runtime.Battle.Events;

public enum BattleEventKind
{
    BattleStarted = 0,
    CommandAccepted = 1,
    CommandRejected = 2,
    RuntimeActorSpawned = 3,
    MovementCompleted = 4,
    DamageApplied = 5,
    CorpsStrengthChanged = 6,
    BattleEnded = 7,
    MovementStarted = 8,
    BattleGroupPlanAccepted = 9,
    BattleGroupPlanStateChanged = 10,
    BattleGroupTacticalRegionSelected = 11,
    BattleGroupTacticalRegionRejected = 12,
    BattleGroupEngagementStateChanged = 13,
    BattleGroupTemporaryRegionSelected = 14,
    BattleGroupLocalCombatRegionChanged = 15
}
