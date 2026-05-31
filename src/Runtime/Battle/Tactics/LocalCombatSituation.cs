using System.Collections.Generic;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle.Tactics;

internal sealed class LocalCombatSituation
{
    public string SituationId { get; init; } = "";
    public string OwnerBattleGroupId { get; init; } = "";
    public string RegionId { get; init; } = "";
    public BattleGridCoord Center { get; init; }
    public string TargetActorId { get; init; } = "";
    public string DirtyReason { get; init; } = "";
    public string ReasonCode { get; init; } = "";
    public int Version { get; init; }
    public int Width { get; init; } = 1;
    public int Height { get; init; } = 1;
    public double LastBuiltRuntimeTimeSeconds { get; init; }
    public int NearbyFriendlyCount { get; init; }
    public int NearbyHostileCount { get; init; }
    public int OpenAttackSlotCount { get; init; }
    public int OccupiedAttackSlotCount { get; init; }
    public bool BlocksObjectiveRoute { get; init; }
    public bool InsideLeash { get; init; } = true;
    public bool HasReachableAttackSlot { get; init; }
    public bool HasReachableSupportSlot { get; init; }
    public LocalCombatSupportSlotRole PreferredSupportRole { get; init; }
    public BattleGridCoord PreferredSupportAnchor { get; init; }
    public IReadOnlyList<string> ParticipantActorIds { get; init; } = new List<string>();
    public IReadOnlyList<string> NearbyCandidateActorIds { get; init; } = new List<string>();
}
