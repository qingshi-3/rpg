using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Commands;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;

namespace Rpg.Presentation.World.Sites;

internal static class BattleRuntimeSkillUsageResolver
{
    internal static WorldSiteRoot.BattleRuntimeSkillUsageState Resolve(
        BattleRuntimeCommandGroupView selected,
        string skillId,
        IReadOnlyList<BattleEvent> events,
        IReadOnlyList<BattleRuntimeSpatialMark> spatialMarks = null,
        double runtimeTimeSeconds = 0)
    {
        if (selected == null)
        {
            return WorldSiteRoot.BattleRuntimeSkillUsageState.Unavailable;
        }

        string normalizedSkillId = (skillId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(normalizedSkillId))
        {
            return WorldSiteRoot.BattleRuntimeSkillUsageState.Unavailable;
        }

        if (string.Equals(normalizedSkillId, HeroSkillCommandIds.ThunderMarkFoldSkillId, System.StringComparison.Ordinal) &&
            !HasLiveThunderMark(selected.GroupKey, spatialMarks, runtimeTimeSeconds))
        {
            return WorldSiteRoot.BattleRuntimeSkillUsageState.Unavailable;
        }

        if (events == null)
        {
            return WorldSiteRoot.BattleRuntimeSkillUsageState.Ready;
        }

        bool used = events.Any(item =>
            item != null &&
            item.Kind == BattleEventKind.SkillUsed &&
            string.Equals(item.BattleGroupId ?? "", selected.GroupKey, System.StringComparison.Ordinal) &&
            string.Equals(item.SourceDefinitionId ?? "", normalizedSkillId, System.StringComparison.Ordinal));
        if (used)
        {
            return WorldSiteRoot.BattleRuntimeSkillUsageState.Used;
        }

        HashSet<string> failedCommandIds = events
            .Where(item =>
                item != null &&
                item.Kind == BattleEventKind.CommandFailed &&
                string.Equals(item.BattleGroupId ?? "", selected.GroupKey, System.StringComparison.Ordinal) &&
                string.Equals(item.SourceDefinitionId ?? "", normalizedSkillId, System.StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(item.SourceCommandId))
            .Select(item => item.SourceCommandId)
            .ToHashSet(System.StringComparer.Ordinal);

        bool pending = events.Any(item =>
            item != null &&
            string.Equals(item.BattleGroupId ?? "", selected.GroupKey, System.StringComparison.Ordinal) &&
            string.Equals(item.SourceDefinitionId ?? "", normalizedSkillId, System.StringComparison.Ordinal) &&
            item.Kind == BattleEventKind.CommandAccepted &&
            !failedCommandIds.Contains(item.SourceCommandId ?? ""));
        return pending
            ? WorldSiteRoot.BattleRuntimeSkillUsageState.Pending
            : WorldSiteRoot.BattleRuntimeSkillUsageState.Ready;
    }

    private static bool HasLiveThunderMark(
        string ownerBattleGroupId,
        IReadOnlyList<BattleRuntimeSpatialMark> spatialMarks,
        double runtimeTimeSeconds) =>
        !string.IsNullOrWhiteSpace(ownerBattleGroupId) &&
        (spatialMarks ?? System.Array.Empty<BattleRuntimeSpatialMark>()).Any(item =>
            item != null &&
            item.ExpiresAtSeconds > runtimeTimeSeconds &&
            string.Equals(item.OwnerBattleGroupId ?? "", ownerBattleGroupId, System.StringComparison.Ordinal));
}
