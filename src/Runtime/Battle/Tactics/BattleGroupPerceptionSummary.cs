using System;
using System.Collections.Generic;

namespace Rpg.Runtime.Battle.Tactics;

public sealed class BattleGroupPerceptionMemberCoverage
{
    public string ActorId { get; init; } = "";
    public int AnchorCellX { get; init; }
    public int AnchorCellY { get; init; }
    public int AnchorCellHeight { get; init; }
    public IReadOnlyList<string> PerceivedHostileActorIds { get; init; } = Array.Empty<string>();
}

public sealed class BattleGroupPerceptionSummary
{
    public string BattleGroupId { get; init; } = "";
    public string FactionId { get; init; } = "";
    public int LastBuiltRuntimeTick { get; init; }
    public int MinAnchorCellX { get; init; }
    public int MaxAnchorCellX { get; init; }
    public int MinAnchorCellY { get; init; }
    public int MaxAnchorCellY { get; init; }
    public int MinAnchorCellHeight { get; init; }
    public int MaxAnchorCellHeight { get; init; }
    public IReadOnlyList<string> PerceivedHostileActorIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<BattleGroupPerceptionMemberCoverage> MemberCoverages { get; init; } =
        Array.Empty<BattleGroupPerceptionMemberCoverage>();
}
