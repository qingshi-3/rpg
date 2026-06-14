namespace Rpg.Runtime.Battle;

internal sealed class BattleRuntimeActiveChannel
{
    public string ChannelId { get; init; } = "";
    public string ActorId { get; init; } = "";
    public string SourceCommandId { get; init; } = "";
    public string SourceActionId { get; init; } = "";
    public string SourceDefinitionId { get; init; } = "";
    public double StartedAtSeconds { get; init; }
    public double EndsAtSeconds { get; init; }
    public double NextTickAtSeconds { get; set; }
    public double TickIntervalSeconds { get; init; }
    public int DamageAmount { get; init; }
    public int Radius { get; init; }
}
