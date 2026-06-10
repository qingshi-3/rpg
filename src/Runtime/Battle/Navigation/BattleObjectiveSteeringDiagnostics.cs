using System.Collections.Generic;
using Rpg.Infrastructure.Logging;

namespace Rpg.Runtime.Battle.Navigation;

internal static class BattleObjectiveSteeringDiagnostics
{
    private static readonly object Sync = new();
    private static readonly HashSet<string> LoggedKeys = new();

    internal static void Log(
        string battleId,
        int tick,
        BattleRuntimeActor actor,
        BattleGridCoord start,
        BattleGridCoord objectiveAnchor,
        string source,
        BattleRouteHint routeHint,
        IReadOnlyList<BattleGridCoord> candidates)
    {
        string selected = candidates?.Count > 0 ? candidates[0].ToString() : "none";
        string routeAnchor = routeHint.Anchor.ToString();
        string candidateSample = FormatCandidateSample(candidates);
        string key = $"{battleId}|{actor?.ActorId}|{actor?.BattleGroupId}|{actor?.ObjectiveZoneId}|{start}|{objectiveAnchor}|{source}|{routeAnchor}|{selected}|{candidateSample}";
        lock (Sync)
        {
            if (!LoggedKeys.Add(key))
            {
                return;
            }
        }

        GameLog.Info(
            nameof(BattleObjectiveSteeringDiagnostics),
            $"BattleRuntimeObjectiveSteering battle={battleId ?? ""} tick={tick} actor={actor?.ActorId ?? ""} group={actor?.BattleGroupId ?? ""} objective={actor?.ObjectiveZoneId ?? ""} source={source ?? ""} start={start} objectiveAnchor={objectiveAnchor} routeAnchor={routeAnchor} routeSourceRegion={routeHint.SourceRegionId ?? ""} routeTargetRegion={routeHint.TargetRegionId ?? ""} corridor={routeHint.CorridorId ?? ""} selected={selected} candidates={candidateSample} steeringMode={(actor == null ? "" : actor.MovementSteeringMode.ToString())} steeringKey={actor?.MovementSteeringIntentKey ?? ""}");
    }

    private static string FormatCandidateSample(IReadOnlyList<BattleGridCoord> candidates)
    {
        if (candidates == null || candidates.Count == 0)
        {
            return "none";
        }

        List<string> sample = new();
        int count = System.Math.Min(4, candidates.Count);
        for (int i = 0; i < count; i++)
        {
            sample.Add(candidates[i].ToString());
        }

        return string.Join("|", sample);
    }
}
