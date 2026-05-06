namespace Rpg.Definitions.World;

public sealed class BattleEntranceDefinition
{
    public string EntranceId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string FactionId { get; set; } = "";
    public int Capacity { get; set; } = 4;
    public string BattleAnchorId { get; set; } = "";
    public string Source { get; set; } = "Default";
}
