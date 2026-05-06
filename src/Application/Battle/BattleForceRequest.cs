using System.Collections.Generic;

namespace Rpg.Application.Battle;

public sealed class BattleForceRequest
{
    public string ForceId { get; set; } = "";
    public string SourceKind { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string UnitDefinitionId { get; set; } = "";
    public int Count { get; set; }
    public string FactionId { get; set; } = "";
    public string PreferredEntranceId { get; set; } = "";
    public List<BattleForcePlacementRequest> PreferredPlacements { get; set; } = new();
}
