using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Adapters;
using Rpg.Application.Battle.Snapshots;
using Rpg.Domain.BattleGroups;
using Rpg.Domain.Corps;
using Rpg.Domain.Heroes;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.Battle;

public sealed class BattleGroupSessionProbeResult
{
    public bool Success { get; init; }
    public string FailureReason { get; init; } = "";
    public BattleStartSnapshot Snapshot { get; init; } = new();
    public BattleGroupBattleFlowResult FlowResult { get; init; } = new();
}

public sealed class BattleGroupSessionProbeService
{
    private readonly LegacyBattleStartSnapshotAdapter _snapshotAdapter = new();
    private readonly BattleGroupBattleFlowService _flowService = new();

    // Phase-2 migration uses this as a diagnostic side channel: it proves live
    // launch data can enter the target architecture without touching legacy handoff.
    public BattleGroupSessionProbeResult Probe(BattleStartRequest request)
    {
        if (request == null)
        {
            return Reject("missing_battle_request", null, null);
        }

        ProbeSeed seed = BuildSeed(request);
        BattleStartSnapshot snapshot = _snapshotAdapter.ToSnapshot(
            request,
            seed.Groups,
            seed.Heroes,
            seed.Corps);
        BattleGroupBattleFlowResult flowResult = _flowService.RunSnapshot(snapshot);
        if (!flowResult.SettlementPlan.Accepted)
        {
            string reason = string.IsNullOrWhiteSpace(flowResult.SettlementPlan.RejectionReason)
                ? "battle_group_probe_rejected"
                : flowResult.SettlementPlan.RejectionReason;
            GameLog.Warn(
                nameof(BattleGroupSessionProbeService),
                $"Battle group session probe rejected request={request.RequestId} snapshot={snapshot.SnapshotId} reason={reason}");
            return Reject(reason, snapshot, flowResult);
        }

        GameLog.Info(
            nameof(BattleGroupSessionProbeService),
            $"Battle group session probe succeeded request={request.RequestId} snapshot={snapshot.SnapshotId} groups={snapshot.BattleGroups.Count}");
        return new BattleGroupSessionProbeResult
        {
            Success = true,
            Snapshot = snapshot,
            FlowResult = flowResult
        };
    }

    private static BattleGroupSessionProbeResult Reject(
        string reason,
        BattleStartSnapshot snapshot,
        BattleGroupBattleFlowResult flowResult)
    {
        return new BattleGroupSessionProbeResult
        {
            Success = false,
            FailureReason = reason ?? "",
            Snapshot = snapshot ?? new BattleStartSnapshot(),
            FlowResult = flowResult ?? new BattleGroupBattleFlowResult()
        };
    }

    private static ProbeSeed BuildSeed(BattleStartRequest request)
    {
        ProbeSeed seed = new();
        string sourceLocationId = !string.IsNullOrWhiteSpace(request.SourceSiteId)
            ? request.SourceSiteId
            : request.TargetSiteId ?? "";
        int groupIndex = 0;
        foreach (BattleForceRequest force in request.PlayerForces ?? Enumerable.Empty<BattleForceRequest>())
        {
            int count = force?.Count ?? 0;
            for (int index = 0; index < count; index++)
            {
                string unitDefinitionId = string.IsNullOrWhiteSpace(force.UnitDefinitionId)
                    ? "legacy_unknown_corps"
                    : force.UnitDefinitionId;
                string forceId = string.IsNullOrWhiteSpace(force.ForceId)
                    ? $"force_{groupIndex}"
                    : force.ForceId;
                string heroId = $"probe_hero_{groupIndex}";
                string corpsId = $"probe_corps_{groupIndex}";

                BattleGroupState group = new()
                {
                    BattleGroupId = $"probe_group_{forceId}_{index}",
                    HeroId = heroId,
                    CorpsId = corpsId,
                    CurrentLocationId = sourceLocationId,
                    Status = BattleGroupStatus.Stationed
                };
                HeroState hero = new()
                {
                    HeroId = heroId,
                    HeroDefinitionId = $"probe_hero_definition_{groupIndex}",
                    Level = 1
                };
                CorpsState corps = new()
                {
                    CorpsId = corpsId,
                    CorpsDefinitionId = unitDefinitionId,
                    Level = 1,
                    CorpsStrength = CorpsStrengthPolicy.MaxStrength
                };

                seed.Groups.Add(group);
                seed.Heroes[hero.HeroId] = hero;
                seed.Corps[corps.CorpsId] = corps;
                groupIndex++;
            }
        }

        return seed;
    }

    private sealed class ProbeSeed
    {
        public List<BattleGroupState> Groups { get; } = new();
        public Dictionary<string, HeroState> Heroes { get; } = new();
        public Dictionary<string, CorpsState> Corps { get; } = new();
    }
}
