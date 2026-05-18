using System.Collections.Generic;

namespace Rpg.Application.Battle;

public sealed class BattleForceRequest
{
    public string ForceId { get; set; } = "";
    public string SourceKind { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string UnitDefinitionId { get; set; } = "";
    public int Count { get; set; }
    public int FootprintWidth { get; set; } = 1;
    public int FootprintHeight { get; set; } = 1;
    public double AttackSpeed { get; set; } = 1.0;
    public string FactionId { get; set; } = "";
    public string PreferredEntranceId { get; set; } = "";
    public List<BattleForcePlacementRequest> PreferredPlacements { get; set; } = new();
}
