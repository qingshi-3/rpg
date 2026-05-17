namespace Rpg.Domain.Equipment;

public sealed class EquipmentInstance
{
    public string EquipmentInstanceId { get; set; } = "";
    public string EquipmentDefinitionId { get; set; } = "";
    public string OwnerFactionId { get; set; } = "player";
    public int Level { get; set; } = 1;
}
