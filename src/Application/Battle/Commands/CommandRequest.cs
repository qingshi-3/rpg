namespace Rpg.Application.Battle.Commands;

public sealed class CommandRequest
{
	public string CommandId { get; set; } = "";
	public string BattleId { get; set; } = "";
	public string BattleGroupId { get; set; } = "";
	public string SourceActorId { get; set; } = "";
	public CommandChannel Channel { get; set; }
	public CommandKind Kind { get; set; }
	public string TargetActorId { get; set; } = "";
	public bool HasTargetGrid { get; set; }
	public int TargetGridX { get; set; }
	public int TargetGridY { get; set; }
	public int TargetGridHeight { get; set; }
	public string SelectedSpatialMarkId { get; set; } = "";
	public string SkillId { get; set; } = "";
}
