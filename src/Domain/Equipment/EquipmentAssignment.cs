namespace Rpg.Domain.Equipment;

public sealed class EquipmentAssignment
{
    public string OwnerHeroId { get; set; } = "";
    public string WeaponInstanceId { get; set; } = "";
    public string ArmorInstanceId { get; set; } = "";
    public string TokenInstanceId { get; set; } = "";
}
