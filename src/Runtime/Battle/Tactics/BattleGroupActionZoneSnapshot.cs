namespace Rpg.Runtime.Battle.Tactics;

public sealed class BattleGroupActionZoneSnapshot
{
    public string BattleGroupId { get; init; } = "";
    public BattleGroupActionZoneKind Kind { get; init; } = BattleGroupActionZoneKind.ObjectiveMove;
    public string TargetCombatZoneId { get; init; } = "";
    public string TargetRegionId { get; init; } = "";
    public string ReasonCode { get; init; } = "";
    public int Version { get; init; }
    public int LastBuiltRuntimeTick { get; init; }
    public int MinCellX { get; init; }
    public int MinCellY { get; init; }
    public int MaxCellX { get; init; }
    public int MaxCellY { get; init; }
    public int CenterCellX { get; init; }
    public int CenterCellY { get; init; }
    public int CenterCellHeight { get; init; }
}
