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
    BattleGroupLocalCombatRegionChanged = 15,
    SkillUsed = 16,
    CommandFailed = 17,
    CommandInterrupted = 18,
    EffectApplied = 19,
    ThunderMarkCreated = 20,
    ThunderMarkTeleported = 21,
    CommandCompleted = 22
}
