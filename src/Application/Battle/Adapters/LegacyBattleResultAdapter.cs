using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Results;
using System.Collections.Generic;
using System.Linq;

namespace Rpg.Application.Battle.Adapters;

public sealed class LegacyBattleResultAdapter
{
    public BattleResult ToLegacyResult(BattleStartRequest request, BattleOutcomeResult outcome)
    {
        BattleResult result = ToLegacyResult(
            request?.RequestId ?? "",
            request?.BattleKind ?? BattleKind.Unknown,
            outcome);
        AddForceResults(result, request, outcome);
        return result;
    }

    public BattleResult ToLegacyResult(string requestId, BattleKind battleKind, BattleOutcomeResult outcome)
    {
        BattleOutcome legacyOutcome = MapOutcome(outcome);
        string reason = outcome == null
            ? "null_outcome"
            : outcome.IsComplete
                ? outcome.TerminationReason.ToString()
                : $"incomplete_{outcome.TerminationReason}";
        GameLog.Info(nameof(LegacyBattleResultAdapter), $"Converted runtime outcome to legacy result request={requestId ?? ""} battle={outcome?.BattleId ?? ""} reason={reason} legacyOutcome={legacyOutcome}");

        return new BattleResult
        {
            RequestId = requestId ?? "",
            ContextId = outcome?.BattleId ?? "",
            BattleKind = battleKind,
            Outcome = legacyOutcome
        };
    }

    private static void AddForceResults(
        BattleResult result,
        BattleStartRequest request,
        BattleOutcomeResult outcome)
    {
        if (result == null || request == null)
        {
            return;
        }

        IReadOnlyCollection<BattleActorOutcome> actorOutcomes = outcome?.ActorOutcomes ?? new List<BattleActorOutcome>();
        foreach (BattleForceRequest force in EnumerateForces(request))
        {
            if (force == null)
            {
                continue;
            }

            int initial = System.Math.Max(0, force.Count);
            int survived = outcome?.IsComplete == true
                ? actorOutcomes.Count(actor =>
                    actor.Kind == BattleRuntimeActorKind.Corps &&
                    actor.Survived &&
                    string.Equals(actor.SourceForceId, force.ForceId, System.StringComparison.Ordinal))
                : 0;

            result.ForceResults.Add(new BattleForceResult
            {
                ForceId = force.ForceId ?? "",
                SourceKind = force.SourceKind ?? "",
                SourceId = force.SourceId ?? "",
                UnitDefinitionId = force.UnitDefinitionId ?? "",
                InitialCount = initial,
                SurvivedCount = System.Math.Min(initial, survived),
                DefeatedCount = System.Math.Max(0, initial - survived)
            });
        }
    }

    private static IEnumerable<BattleForceRequest> EnumerateForces(BattleStartRequest request)
    {
        foreach (BattleForceRequest force in request.PlayerForces ?? new List<BattleForceRequest>())
        {
            yield return force;
        }

        foreach (BattleForceRequest force in request.EnemyForces ?? new List<BattleForceRequest>())
        {
            yield return force;
        }
    }

    private static BattleOutcome MapOutcome(BattleOutcomeResult outcome)
    {
        if (outcome == null || !outcome.IsComplete)
        {
            return BattleOutcome.Disaster;
        }

        return outcome.TerminationReason switch
        {
            BattleTerminationReason.NormalVictory => BattleOutcome.Victory,
            BattleTerminationReason.NormalDefeat => BattleOutcome.Defeat,
            BattleTerminationReason.PlayerRetreat => BattleOutcome.Withdraw,
            BattleTerminationReason.RuntimeException => BattleOutcome.Disaster,
            BattleTerminationReason.Interrupted => BattleOutcome.Disaster,
            BattleTerminationReason.None => BattleOutcome.Disaster,
            _ => BattleOutcome.Disaster
        };
    }
}
