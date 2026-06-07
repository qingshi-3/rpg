namespace Rpg.Runtime.Battle.Effects;

internal sealed class BattleEffectExecutionContext
{
    public string BattleId { get; set; } = "";
    public int RuntimeTick { get; set; } = -1;
    public double RuntimeTimeSeconds { get; set; }
    public string SourceActionId { get; set; } = "";
    public string SourceCommandId { get; set; } = "";
    public string SourceDefinitionId { get; set; } = "";
    public BattleRuntimeActor Actor { get; set; } = new();
    public BattleRuntimeActor Target { get; set; } = new();
}
