using System;
using System.Collections.Generic;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle.Tactics;

internal static class BattleGroupActionZoneResolver
{
    internal static BattleGroupActionZoneSnapshot ResolveCombatJoinActionZone(
        BattleRuntimeActor actor,
        IReadOnlyDictionary<string, BattleGroupActionZoneSnapshot> actionZones)
    {
        if (actor == null ||
            actionZones == null ||
            string.IsNullOrWhiteSpace(actor.BattleGroupId) ||
            !actionZones.TryGetValue(actor.BattleGroupId, out BattleGroupActionZoneSnapshot actionZone) ||
            actionZone.Kind != BattleGroupActionZoneKind.CombatJoin ||
            string.IsNullOrWhiteSpace(actionZone.TargetCombatZoneId))
        {
            return null;
        }

        return actionZone;
    }

    internal static IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> FilterFactsToActionZone(
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> facts,
        BattleRuntimeTickStartActorFact actorFact,
        BattleGroupActionZoneSnapshot actionZone)
    {
        Dictionary<string, BattleRuntimeTickStartActorFact> filtered = new(StringComparer.Ordinal);
        foreach (BattleRuntimeTickStartActorFact fact in facts?.Values ?? Array.Empty<BattleRuntimeTickStartActorFact>())
        {
            if (string.Equals(fact.Actor.ActorId ?? "", actorFact.Actor.ActorId ?? "", StringComparison.Ordinal) ||
                BattleRuntimeTickResolver.SameFaction(fact.Actor, actorFact.Actor) ||
                IsInsideActionZone(fact.Actor, fact.Anchor, actionZone))
            {
                filtered[fact.Actor.ActorId ?? ""] = fact;
            }
        }

        return filtered;
    }

    internal static bool IsInsideActionZone(BattleRuntimeActor actor, BattleGridCoord anchor, BattleGroupActionZoneSnapshot actionZone)
    {
        if (actor == null ||
            actionZone == null ||
            anchor.Height != actionZone.CenterCellHeight)
        {
            return false;
        }

        foreach (BattleGridCoord cell in BattleActorFootprint.Enumerate(actor, anchor))
        {
            if (cell.X >= actionZone.MinCellX &&
                cell.X <= actionZone.MaxCellX &&
                cell.Y >= actionZone.MinCellY &&
                cell.Y <= actionZone.MaxCellY)
            {
                return true;
            }
        }

        return false;
    }
}
