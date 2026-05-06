namespace Rpg.Definitions.World;

public sealed class GarrisonDefinition
{
    public string UnitTypeId { get; set; } = "";
    public int Count { get; set; }
    public int Morale { get; set; } = 50;
}
