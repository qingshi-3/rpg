namespace Rpg.Application.Battle.Auto;

public sealed class AutoBattleEvent
{
	public int Tick { get; init; }
	public AutoBattleEventKind Kind { get; init; }
	public string ActorId { get; init; } = "";
	public string TargetId { get; init; } = "";
	public string ForceId { get; init; } = "";
	public string UnitDefinitionId { get; init; } = "";
	public int CellX { get; init; }
	public int CellY { get; init; }
	public int CellHeight { get; init; }
	public int Damage { get; init; }
	public int RemainingHealth { get; init; }
	public BattleOutcome Outcome { get; init; } = BattleOutcome.None;
}
