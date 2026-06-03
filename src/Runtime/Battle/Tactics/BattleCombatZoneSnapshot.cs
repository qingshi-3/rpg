using System;
using System.Collections.Generic;

namespace Rpg.Runtime.Battle.Tactics;

public sealed class BattleCombatZoneSnapshot
{
    public string CombatZoneId { get; init; } = "";
    public string OwnerBattleGroupId { get; init; } = "";
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
    public IReadOnlyList<string> ActorIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> BattleGroupIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> FactionIds { get; init; } = Array.Empty<string>();
}
