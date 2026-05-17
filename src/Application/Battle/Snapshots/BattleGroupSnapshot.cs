namespace Rpg.Application.Battle.Snapshots;

public sealed class BattleGroupSnapshot
{
    public string BattleGroupId { get; set; } = "";
    public string HeroId { get; set; } = "";
    public string HeroDefinitionId { get; set; } = "";
    public int HeroLevel { get; set; }
    public string CorpsId { get; set; } = "";
    public string CorpsDefinitionId { get; set; } = "";
    public int CorpsLevel { get; set; }
    public int CorpsEquipmentLevel { get; set; }
    public int CorpsStrength { get; set; }
    public string SourceLocationId { get; set; } = "";
}
