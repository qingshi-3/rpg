namespace Rpg.Application.Battle.Auto;

public enum AutoBattleEventKind
{
	BattleStarted = 1,
	UnitSpawned = 2,
	TargetAcquired = 3,
	MovementStarted = 4,
	MovementCompleted = 5,
	AttackResolved = 6,
	UnitDefeated = 7,
	BattleEnded = 8
}
