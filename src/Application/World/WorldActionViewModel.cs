using System.Collections.Generic;

namespace Rpg.Application.World;

public sealed class WorldActionViewModel
{
    public string ActionId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsEnabled { get; set; }
    public string DisabledReason { get; set; } = "";
    public List<string> CostLines { get; set; } = new();
    public List<string> EffectLines { get; set; } = new();
    public List<string> WarningLines { get; set; } = new();
    public string TargetSiteId { get; set; } = "";
}
