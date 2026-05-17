namespace Rpg.Application.Battle.Commands;

public sealed class CommandRequest
{
    public string CommandId { get; set; } = "";
    public string BattleId { get; set; } = "";
    public string BattleGroupId { get; set; } = "";
    public CommandChannel Channel { get; set; }
    public CommandKind Kind { get; set; }
    public string TargetActorId { get; set; } = "";
    public string SkillId { get; set; } = "";
}
