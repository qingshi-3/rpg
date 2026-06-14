using System;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle;

internal static class BattleRuntimeThunderMarkQueries
{
    internal static bool HasLiveMark(
        BattleRuntimeState state,
        string ownerBattleGroupId,
        double runtimeTimeSeconds) =>
        TryResolveLiveMarkAnchor(state, ownerBattleGroupId, runtimeTimeSeconds, out _, out _);

    internal static bool TryResolveLiveMarkAnchor(
        BattleRuntimeState state,
        string ownerBattleGroupId,
        double runtimeTimeSeconds,
        out BattleRuntimeSpatialMark mark,
        out BattleGridCoord anchor)
    {
        mark = null;
        anchor = default;
        if (state?.SpatialMarks == null || string.IsNullOrWhiteSpace(ownerBattleGroupId))
        {
            return false;
        }

        state.SpatialMarks.RemoveAll(item => item.ExpiresAtSeconds <= runtimeTimeSeconds);
        for (int i = state.SpatialMarks.Count - 1; i >= 0; i--)
        {
            BattleRuntimeSpatialMark candidate = state.SpatialMarks[i];
            if (!string.Equals(candidate.OwnerBattleGroupId, ownerBattleGroupId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(candidate.AttachedActorId))
            {
                BattleRuntimeActor attached = state.Actors?.Find(item =>
                    string.Equals(item.ActorId, candidate.AttachedActorId, StringComparison.Ordinal));
                if (attached == null || attached.HitPoints <= 0)
                {
                    continue;
                }

                mark = candidate;
                anchor = new BattleGridCoord(attached.GridX, attached.GridY, attached.GridHeight);
                return true;
            }

            if (candidate.HasGroundAnchor)
            {
                mark = candidate;
                anchor = new BattleGridCoord(candidate.GridX, candidate.GridY, candidate.GridHeight);
                return true;
            }
        }

        return false;
    }

    internal static bool TryResolveLiveMarkAnchorById(
        BattleRuntimeState state,
        string selectedSpatialMarkId,
        string ownerBattleGroupId,
        double runtimeTimeSeconds,
        out BattleRuntimeSpatialMark mark,
        out BattleGridCoord anchor)
    {
        mark = null;
        anchor = default;
        if (state?.SpatialMarks == null ||
            string.IsNullOrWhiteSpace(selectedSpatialMarkId) ||
            string.IsNullOrWhiteSpace(ownerBattleGroupId))
        {
            return false;
        }

        state.SpatialMarks.RemoveAll(item => item.ExpiresAtSeconds <= runtimeTimeSeconds);
        BattleRuntimeSpatialMark candidate = state.SpatialMarks.Find(item =>
            string.Equals(item?.MarkId ?? "", selectedSpatialMarkId.Trim(), StringComparison.Ordinal));
        if (candidate == null ||
            !string.Equals(candidate.OwnerBattleGroupId ?? "", ownerBattleGroupId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(candidate.AttachedActorId))
        {
            BattleRuntimeActor attached = state.Actors?.Find(item =>
                string.Equals(item.ActorId, candidate.AttachedActorId, StringComparison.Ordinal));
            if (attached == null || attached.HitPoints <= 0)
            {
                return false;
            }

            mark = candidate;
            anchor = new BattleGridCoord(attached.GridX, attached.GridY, attached.GridHeight);
            return true;
        }

        if (candidate.HasGroundAnchor)
        {
            mark = candidate;
            anchor = new BattleGridCoord(candidate.GridX, candidate.GridY, candidate.GridHeight);
            return true;
        }

        return false;
    }
}
