using System.Collections.Generic;

namespace Rpg.Domain.World;

public sealed class WorldBattleState
{
    public string BattleId { get; set; } = "";
    public string ThreatId { get; set; } = "";
    public string SourceSiteId { get; set; } = "";
    public string TargetSiteId { get; set; } = "";
    public string AttackerFactionId { get; set; } = "";
    public string DefenderFactionId { get; set; } = "";
    public string AttackerArmyId { get; set; } = "";
    public int StartedTick { get; set; }
    public int LastAdvancedTick { get; set; }
    public int ResolvedTick { get; set; } = -1;
    public int TotalDurationTicks { get; set; } = 4;
    public WorldBattlePhase CurrentPhase { get; set; } = WorldBattlePhase.Opening;
    public WorldBattleOutcome ProjectedOutcome { get; set; } = WorldBattleOutcome.Unknown;
    public WorldBattleOutcome ResolvedOutcome { get; set; } = WorldBattleOutcome.Unknown;
    public string ProjectedWinnerFactionId { get; set; } = "";
    public string ResolvedWinnerFactionId { get; set; } = "";
    public int AttackerPower { get; set; }
    public int DefenderPower { get; set; }
    public int AttackerLossEstimate { get; set; }
    public int DefenderLossEstimate { get; set; }
    public bool PlayerIntervened { get; set; }
    public bool IsResolved { get; set; }
    public string ResolutionReason { get; set; } = "";
    public List<GarrisonState> AttackerForces { get; set; } = new();
    public List<GarrisonState> DefenderForces { get; set; } = new();
}
