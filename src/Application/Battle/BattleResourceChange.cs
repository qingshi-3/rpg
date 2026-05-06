namespace Rpg.Application.Battle;

public sealed class BattleResourceChange
{
    public string ResourceId { get; set; } = "";
    public int Amount { get; set; }
    public string Reason { get; set; } = "";
}
