namespace Rpg.Domain.StrategicManagement;

public sealed class StrategicCorpsInstanceState
{
    public string CorpsInstanceId { get; set; } = "";
    public string CorpsDefinitionId { get; set; } = "";
    public string HomeCityId { get; set; } = "";
    public string FactionId { get; set; } = "";
    public int Strength { get; set; } = 100;
    public int Level { get; set; } = 1;
    public int EquipmentLevel { get; set; }
    public int Experience { get; set; }
    public StrategicCorpsInstanceStatus Status { get; set; } = StrategicCorpsInstanceStatus.Garrisoned;
    public string AssignedHeroId { get; set; } = "";
    public string CurrentExpeditionId { get; set; } = "";
}
