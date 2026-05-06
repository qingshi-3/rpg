namespace Rpg.Domain.World;

public sealed class GarrisonState
{
    public string UnitTypeId { get; set; } = "";
    public int Count { get; set; }
    public string SourceFacilityId { get; set; } = "";
    public int Morale { get; set; } = 50;
    public int DamageLevel { get; set; }
}
