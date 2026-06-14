using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle;

internal static partial class BattleRuntimeHeroSkillCommandResolver
{
    private static bool UsesThunderMarkTeleport(BattleSkillSnapshot skill) =>
        skill?.Effects?.Any(effect => effect?.Kind == BattleSkillEffectKind.TeleportToThunderMark) == true;

    private static bool ValidateThunderMarkTeleportDestination(
        BattleRuntimeState state,
        BattleRuntimeActor caster,
        BattleSkillSnapshot skill,
        string selectedSpatialMarkId,
        int targetGridX,
        int targetGridY,
        int targetGridHeight,
        double runtimeTimeSeconds,
        BattleNavigationGraph navigationGraph,
        out string reasonCode)
    {
        reasonCode = "";
        if (!BattleRuntimeThunderMarkQueries.TryResolveLiveMarkAnchorById(
                state,
                selectedSpatialMarkId,
                caster?.BattleGroupId ?? "",
                runtimeTimeSeconds,
                out _,
                out BattleGridCoord markAnchor))
        {
            reasonCode = "thunder_mark_missing";
            return false;
        }

        BattleGridCoord destination = new(targetGridX, targetGridY, targetGridHeight);
        int radius = ResolveThunderMarkTeleportRadius(skill);
        if (destination.Height != markAnchor.Height ||
            System.Math.Max(System.Math.Abs(destination.X - markAnchor.X), System.Math.Abs(destination.Y - markAnchor.Y)) > radius)
        {
            reasonCode = "thunder_mark_destination_not_near_mark";
            return false;
        }

        if (navigationGraph?.CanPlaceFootprint(caster, destination) != true)
        {
            reasonCode = "thunder_mark_destination_invalid";
            return false;
        }

        BattleDynamicOccupancy occupancy = BattleDynamicOccupancy.FromActors(state?.Actors);
        if (!occupancy.CanPlaceFootprint(caster, destination))
        {
            reasonCode = "thunder_mark_destination_occupied";
            return false;
        }

        return true;
    }

    private static int ResolveThunderMarkTeleportRadius(BattleSkillSnapshot skill)
    {
        int radius = skill?.Effects?
            .Where(effect => effect?.Kind == BattleSkillEffectKind.TeleportToThunderMark)
            .Select(effect => effect.Amount)
            .DefaultIfEmpty(1)
            .Max() ?? 1;
        return System.Math.Max(1, radius);
    }
}
