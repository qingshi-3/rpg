namespace Rpg.Definitions.StrategicManagement;

public sealed class StrategicResourceAmount
{
    public StrategicResourceAmount()
    {
    }

    public StrategicResourceAmount(string resourceId, int amount)
    {
        ResourceId = resourceId ?? "";
        Amount = amount;
    }

    public string ResourceId { get; set; } = "";
    public int Amount { get; set; }
}
