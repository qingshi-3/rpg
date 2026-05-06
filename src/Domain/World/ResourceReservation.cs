namespace Rpg.Domain.World;

public sealed class ResourceReservation
{
    public ResourceReservation()
    {
    }

    public ResourceReservation(string resourceId, int amount, string sourceId, string sourceKind)
    {
        ResourceId = resourceId ?? "";
        Amount = System.Math.Max(0, amount);
        SourceId = sourceId ?? "";
        SourceKind = sourceKind ?? "";
    }

    public string ResourceId { get; set; } = "";
    public int Amount { get; set; }
    public string SourceId { get; set; } = "";
    public string SourceKind { get; set; } = "";
}
