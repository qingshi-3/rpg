using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Snapshots;

namespace Rpg.Presentation.World.Sites;

internal static class BattleRuntimeSkillTargetingRules
{
    internal static BattleSkillSnapshot ResolveSkillSnapshot(
        IReadOnlyList<BattleSkillSnapshot> selectedSkills,
        IEnumerable<BattleSkillSnapshot> runtimeSkills,
        string skillDefinitionId)
    {
        string normalizedSkillDefinitionId = (skillDefinitionId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(normalizedSkillDefinitionId))
        {
            return null;
        }

        return (selectedSkills ?? System.Array.Empty<BattleSkillSnapshot>())
            .FirstOrDefault(item => string.Equals(ResolveSkillDefinitionId(item), normalizedSkillDefinitionId, System.StringComparison.Ordinal))
            ?? (runtimeSkills ?? System.Array.Empty<BattleSkillSnapshot>())
                .FirstOrDefault(item => string.Equals(ResolveSkillDefinitionId(item), normalizedSkillDefinitionId, System.StringComparison.Ordinal));
    }

    internal static string ResolveSkillDefinitionId(BattleSkillSnapshot skill) =>
        skill?.SkillDefinitionId?.Trim() ?? "";

    internal static BattleSkillTargetingSnapshot ResolveTargeting(BattleSkillSnapshot skill) =>
        skill?.Targeting ?? new BattleSkillTargetingSnapshot();

    internal static bool IsImmediateSelfSkill(BattleSkillSnapshot skill) =>
        ResolveTargeting(skill).InputFlow == BattleSkillInputFlow.ImmediateSelf ||
        skill?.TargetingMode == BattleSkillTargetingMode.None;

    internal static bool UsesMarkThenLandingFlow(BattleSkillSnapshot skill) =>
        ResolveTargeting(skill).InputFlow == BattleSkillInputFlow.SelectMarkThenLandingCell ||
        ResolveTargeting(skill).RequiresSelectedMark ||
        skill?.Effects?.OfType<TeleportToMarkSkillEffectSnapshot>().Any() == true;

    internal static bool UsesDirectionAreaFlow(BattleSkillSnapshot skill) =>
        ResolveTargeting(skill).InputFlow == BattleSkillInputFlow.SelectDirectionArea;

    internal static int ResolveSkillRange(BattleSkillSnapshot skill)
    {
        int range = ResolveTargeting(skill).Range;
        return range > 0 ? range : System.Math.Max(0, skill?.Range ?? 0);
    }

    internal static int ResolveMarkLandingRadius(BattleSkillSnapshot skill)
    {
        int targetingRadius = ResolveTargeting(skill).LandingRadius;
        int effectRadius = skill?.Effects?
            .OfType<TeleportToMarkSkillEffectSnapshot>()
            .Select(effect => effect.LandingRadius)
            .DefaultIfEmpty(0)
            .Max() ?? 0;
        return System.Math.Max(1, targetingRadius > 0 ? targetingRadius : effectRadius);
    }
}
