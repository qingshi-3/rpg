namespace Rpg.Definitions.World;

public sealed class ResourceAmountDefinition
{
    public ResourceAmountDefinition()
    {
    }

    public ResourceAmountDefinition(string resourceId, int amount)
    {
        ResourceId = resourceId ?? "";
        Amount = amount;
    }

    public string ResourceId { get; set; } = "";
    public int Amount { get; set; }
}
