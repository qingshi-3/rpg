using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle;

internal static partial class BattleRuntimeHeroSkillCommandResolver
{
    private static bool UsesMarkTeleport(BattleSkillSnapshot skill) =>
        skill?.Effects?.Any(effect => effect is TeleportToMarkSkillEffectSnapshot) == true;

    private static bool ValidateMarkTeleportDestination(
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
        BattleGridCoord destination = new(targetGridX, targetGridY, targetGridHeight);
        return BattleDisplacementCommitBoundary.ValidateMarkTeleportDestination(
            state,
            caster,
            selectedSpatialMarkId,
            destination,
            ResolveMarkTeleportRadius(skill),
            runtimeTimeSeconds,
            navigationGraph,
            out _,
            out _,
            out reasonCode);
    }

    private static int ResolveMarkTeleportRadius(BattleSkillSnapshot skill)
    {
        int radius = skill?.Effects?
            .OfType<TeleportToMarkSkillEffectSnapshot>()
            .Select(effect => effect.LandingRadius)
            .DefaultIfEmpty(1)
            .Max() ?? 1;
        return System.Math.Max(1, radius);
    }
}
