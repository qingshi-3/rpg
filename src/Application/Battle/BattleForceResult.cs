namespace Rpg.Application.Battle;

public sealed class BattleForceResult
{
	public string ForceId { get; set; } = "";
	public string SourceKind { get; set; } = "";
	public string SourceId { get; set; } = "";
	public string UnitDefinitionId { get; set; } = "";
	public int InitialCount { get; set; }
	public int SurvivedCount { get; set; }
	public int DefeatedCount { get; set; }
}
