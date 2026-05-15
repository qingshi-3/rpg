using System.Collections.Generic;

namespace Rpg.Definitions.World;

public sealed class SiteExplorationPointDefinition
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public int CellX { get; set; }
    public int CellY { get; set; }
    public int CellHeight { get; set; }
    public int InteractionRange { get; set; } = 1;
    public bool InitiallyRevealed { get; set; } = true;
    public List<SiteExplorationActionDefinition> Actions { get; set; } = new();
}
