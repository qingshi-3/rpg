using Rpg.Domain.World;

namespace Rpg.Application.Battle;

public sealed class FacilityStateChange
{
    public string SiteId { get; set; } = "";
    public string FacilityInstanceId { get; set; } = "";
    public string FacilityId { get; set; } = "";
    public FacilityState? State { get; set; }
    public int DamageDelta { get; set; }
}
