namespace Rpg.Runtime.Battle;

public sealed class BattleRuntimeActor
{
    public string ActorId { get; set; } = "";
    public string BattleGroupId { get; set; } = "";
    public BattleRuntimeActorKind Kind { get; set; }
    public int HitPoints { get; set; } = 1;
}
