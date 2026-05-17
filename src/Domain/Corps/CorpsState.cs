namespace Rpg.Domain.Corps;

public sealed class CorpsState
{
    public string CorpsId { get; set; } = "";
    public string CorpsDefinitionId { get; set; } = "";
    public int Level { get; set; } = 1;
    public int EquipmentLevel { get; set; } = 1;
    public int CorpsStrength { get; set; } = CorpsStrengthPolicy.MaxStrength;
    public int TrainingProgress { get; set; }

    public void ClampStrength()
    {
        CorpsStrength = CorpsStrengthPolicy.Clamp(CorpsStrength);
    }
}
