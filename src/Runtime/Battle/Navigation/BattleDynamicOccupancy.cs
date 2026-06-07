using System.Collections.Generic;

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
        if (actors == null)
        {
            return occupancy;
        }

        foreach (BattleRuntimeActor actor in actors)
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
        foreach (string occupantId in actorIds)
        {
            if (!string.Equals(occupantId, actorId, System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public IReadOnlyCollection<string> GetOtherOccupants(BattleRuntimeActor actor, BattleGridCoord cell)
    {
        if (!_tickStartOccupants.TryGetValue(cell, out HashSet<string> actorIds))
        {
            return System.Array.Empty<string>();
        }

        string actorId = actor?.ActorId ?? "";
        List<string> otherOccupants = new();
        foreach (string occupantId in actorIds)
        {
            if (!string.Equals(occupantId, actorId, System.StringComparison.Ordinal))
            {
                otherOccupants.Add(occupantId);
            }
        }

        return otherOccupants.Count == 0
            ? System.Array.Empty<string>()
            : otherOccupants.ToArray();
    }
}
