namespace Rpg.Definitions.World;

public sealed class ResourceDefinition
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public ResourceCategory Category { get; set; } = ResourceCategory.Material;
    public bool IsReservable { get; set; }
    public int MinValue { get; set; }
    public int? MaxValue { get; set; }
    public string IconKey { get; set; } = "";
}
