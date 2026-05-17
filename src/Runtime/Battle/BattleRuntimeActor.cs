namespace Rpg.Runtime.Battle;

public sealed class BattleRuntimeActor
{
    public string ActorId { get; set; } = "";
    public string BattleGroupId { get; set; } = "";
    public string FactionId { get; set; } = "";
    public string SourceForceId { get; set; } = "";
    public string SourceStateId { get; set; } = "";
    public BattleRuntimeActorKind Kind { get; set; }
    public int HitPoints { get; set; } = 1;
    public int Position { get; set; }
    public int GridX { get; set; }
    public int GridY { get; set; }
    public int GridHeight { get; set; }
}
