using System.Collections.Generic;
using System.Linq;

namespace Rpg.Runtime.Battle.Navigation;

internal sealed class BattleDynamicOccupancy
{
    private readonly Dictionary<BattleGridCoord, HashSet<string>> _tickStartOccupants = new();

    private BattleDynamicOccupancy()
    {
    }

    public static BattleDynamicOccupancy FromActors(IEnumerable<BattleRuntimeActor> actors)
    {
        BattleDynamicOccupancy occupancy = new();
        foreach (BattleRuntimeActor actor in actors ?? Enumerable.Empty<BattleRuntimeActor>())
        {
            if (actor?.Kind != BattleRuntimeActorKind.Corps || actor.HitPoints <= 0)
            {
                continue;
            }

            foreach (BattleGridCoord cell in BattleActorFootprint.Enumerate(actor))
            {
                occupancy.AddOccupant(actor, cell);
            }

            if (actor.HasReservedGridCell)
            {
                BattleGridCoord reservedAnchor = new(
                    actor.ReservedGridX,
                    actor.ReservedGridY,
                    actor.ReservedGridHeight);
                foreach (BattleGridCoord cell in BattleActorFootprint.Enumerate(actor, reservedAnchor))
                {
                    occupancy.AddOccupant(actor, cell);
                }
            }
        }

        return occupancy;
    }

    private void AddOccupant(BattleRuntimeActor actor, BattleGridCoord cell)
    {
        if (!_tickStartOccupants.TryGetValue(cell, out HashSet<string> actorIds))
        {
            actorIds = new HashSet<string>(System.StringComparer.Ordinal);
            _tickStartOccupants[cell] = actorIds;
        }

        actorIds.Add(actor?.ActorId ?? "");
    }

    public bool CanPlaceFootprint(BattleRuntimeActor actor, BattleGridCoord anchor)
    {
        foreach (BattleGridCoord cell in BattleActorFootprint.Enumerate(actor, anchor))
        {
            if (IsOccupiedByOther(actor, cell))
            {
                return false;
            }
        }

        return true;
    }

    public int CountOtherOccupiedCells(BattleRuntimeActor actor, BattleGridCoord anchor)
    {
        int count = 0;
        foreach (BattleGridCoord cell in BattleActorFootprint.Enumerate(actor, anchor))
        {
            if (IsOccupiedByOther(actor, cell))
            {
                count++;
            }
        }

        return count;
    }

    public bool IsOccupiedByOther(BattleRuntimeActor actor, BattleGridCoord cell)
    {
        if (!_tickStartOccupants.TryGetValue(cell, out HashSet<string> actorIds))
        {
            return false;
        }

        string actorId = actor?.ActorId ?? "";
        return actorIds.Any(item => !string.Equals(item, actorId, System.StringComparison.Ordinal));
    }

    public IReadOnlyCollection<string> GetOtherOccupants(BattleRuntimeActor actor, BattleGridCoord cell)
    {
        if (!_tickStartOccupants.TryGetValue(cell, out HashSet<string> actorIds))
        {
            return System.Array.Empty<string>();
        }

        string actorId = actor?.ActorId ?? "";
        return actorIds
            .Where(item => !string.Equals(item, actorId, System.StringComparison.Ordinal))
            .ToArray();
    }
}
