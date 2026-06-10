using Rpg.Application.Battle.Snapshots;

namespace Rpg.Application.Battle;

public static class BattleCommanderGroupIdentity
{
    public static string Resolve(BattleGroupSnapshot group)
    {
        if (!string.IsNullOrWhiteSpace(group?.RuntimeCommanderGroupId))
        {
            return group.RuntimeCommanderGroupId;
        }

        return group?.BattleGroupId ?? "";
    }

    public static string BuildProbeCommanderGroupId(BattleForceRequest force, string fallbackForceId)
    {
        string key = ResolveForceCommandKey(force, fallbackForceId);
        return string.IsNullOrWhiteSpace(key)
            ? ""
            : $"probe_group_{key}";
    }

    public static string ResolveForceCommandKey(BattleForceRequest force, string fallbackForceId = "")
    {
        if (force == null)
        {
            return fallbackForceId ?? "";
        }

        if (!string.IsNullOrWhiteSpace(force.CommandGroupId))
        {
            return force.CommandGroupId;
        }

        if (!string.IsNullOrWhiteSpace(force.SourceKind) && !string.IsNullOrWhiteSpace(force.SourceId))
        {
            return $"{force.SourceKind}:{force.SourceId}";
        }

        if (!string.IsNullOrWhiteSpace(force.SourceId))
        {
            return force.SourceId;
        }

        if (!string.IsNullOrWhiteSpace(force.ForceId))
        {
            return force.ForceId;
        }

        return fallbackForceId ?? "";
    }
}
