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
        BattleGridCoord destination = new(targetGridX, targetGridY, targetGridHeight);
        return BattleDisplacementCommitBoundary.ValidateThunderMarkTeleportDestination(
            state,
            caster,
            selectedSpatialMarkId,
            destination,
            ResolveThunderMarkTeleportRadius(skill),
            runtimeTimeSeconds,
            navigationGraph,
            out _,
            out _,
            out reasonCode);
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
