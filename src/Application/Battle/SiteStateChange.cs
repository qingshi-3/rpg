using System.Collections.Generic;
using Rpg.Domain.World;

namespace Rpg.Application.Battle;

public sealed class SiteStateChange
{
    public string SiteId { get; set; } = "";
    public string OwnerFactionId { get; set; } = "";
    public SiteControlState? ControlState { get; set; }
    public int DamageLevelDelta { get; set; }
    public List<string> TagsAdded { get; set; } = new();
    public List<string> TagsRemoved { get; set; } = new();
}
