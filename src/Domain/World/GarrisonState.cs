namespace Rpg.Domain.World;

public sealed class GarrisonState
{
    public string UnitTypeId { get; set; } = "";
    public int Count { get; set; }
    public string FactionId { get; set; } = "";
    public string SourceKind { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string StrategicParticipantId { get; set; } = "";
    public int Morale { get; set; } = 50;
    public int DamageLevel { get; set; }
}
