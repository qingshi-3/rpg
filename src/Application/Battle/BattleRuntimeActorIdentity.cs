using System.Collections.Generic;
using System.Linq;

namespace Rpg.Application.Battle;

public static class BattleRuntimeActorIdentity
{
    public static IReadOnlyDictionary<string, string> BuildPresentationEntityToRuntimeActorMap(BattleStartRequest request)
    {
        return BuildPresentationEntityToRuntimeActorMap(request?.PlayerForces, request?.EnemyForces);
    }

    public static IReadOnlyDictionary<string, string> BuildPresentationEntityToRuntimeActorMap(
        IEnumerable<BattleForceRequest> playerForces,
        IEnumerable<BattleForceRequest> enemyForces)
    {
        var result = new Dictionary<string, string>(System.StringComparer.Ordinal);
        var sourceForceIndexes = new Dictionary<string, int>(System.StringComparer.Ordinal);
        AddMappings(result, sourceForceIndexes, playerForces);
        AddMappings(result, sourceForceIndexes, enemyForces);
        return result;
    }

    public static HashSet<string> BuildRuntimeCorpsActorIdSet(IEnumerable<BattleForceRequest> forces)
    {
        return BuildPresentationEntityToRuntimeActorMap(forces, Enumerable.Empty<BattleForceRequest>())
            .Values
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(System.StringComparer.Ordinal);
    }

    private static void AddMappings(
        IDictionary<string, string> result,
        IDictionary<string, int> sourceForceIndexes,
        IEnumerable<BattleForceRequest> forces)
    {
        foreach (BattleForceRequest force in forces ?? Enumerable.Empty<BattleForceRequest>())
        {
            int count = System.Math.Max(0, force?.Count ?? 0);
            if (count <= 0)
            {
                continue;
            }

            string sourceForceId = ResolveRuntimeSourceForceId(force);
            if (string.IsNullOrWhiteSpace(sourceForceId))
            {
                continue;
            }

            for (int index = 0; index < count; index++)
            {
                string presentationEntityId = BuildPresentationEntityId(force, index);
                if (string.IsNullOrWhiteSpace(presentationEntityId))
                {
                    continue;
                }

                sourceForceIndexes.TryGetValue(sourceForceId, out int previousCount);
                int oneBasedRuntimeIndex = previousCount + 1;
                sourceForceIndexes[sourceForceId] = oneBasedRuntimeIndex;
                result[presentationEntityId] = $"{sourceForceId}:{oneBasedRuntimeIndex}";
            }
        }
    }

    private static string ResolveRuntimeSourceForceId(BattleForceRequest force)
    {
        if (!string.IsNullOrWhiteSpace(force?.StrategicParticipantId))
        {
            return force.StrategicParticipantId;
        }

        if (!string.IsNullOrWhiteSpace(force?.ForceId))
        {
            return force.ForceId;
        }

        return force?.UnitDefinitionId ?? "";
    }

    private static string BuildPresentationEntityId(BattleForceRequest force, int forceIndex)
    {
        string source = string.IsNullOrWhiteSpace(force?.ForceId)
            ? force?.UnitDefinitionId
            : force.ForceId;
        return string.IsNullOrWhiteSpace(source)
            ? ""
            : $"{source}:{forceIndex + 1}";
    }
}
