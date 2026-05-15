using System.Collections.Generic;

namespace Rpg.Domain.World;

public sealed class WorldSiteExplorationState
{
    public int CurrentCellX { get; set; }
    public int CurrentCellY { get; set; }
    public int CurrentCellHeight { get; set; }
    public int PartyActionPoints { get; set; }
    public bool IsSimulationPaused { get; set; } = true;
    public string PauseReason { get; set; } = "";
    public int AlertLevel { get; set; }
    public string ActiveAlertPatrolId { get; set; } = "";
    public string PendingInteractionPointId { get; set; } = "";
    public List<string> PendingPathCellKeys { get; set; } = new();
    public List<string> RevealedCellKeys { get; set; } = new();
    public List<string> VisitedCellKeys { get; set; } = new();
    public List<string> RevealedPointIds { get; set; } = new();
    public List<string> ResolvedPointIds { get; set; } = new();
    public List<SiteExplorationPatrolState> PatrolUnits { get; set; } = new();
}
