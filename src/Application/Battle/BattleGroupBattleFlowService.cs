using System.Collections.Generic;
using Rpg.Application.Battle.Reports;
using Rpg.Application.Battle.Settlement;
using Rpg.Application.Battle.Snapshots;
using Rpg.Domain.BattleGroups;
using Rpg.Domain.Corps;
using Rpg.Domain.Heroes;
using Rpg.Runtime.Battle;

namespace Rpg.Application.Battle;

public sealed class BattleGroupBattleFlowResult
{
    public BattleStartSnapshot Snapshot { get; init; } = new();
    public BattleRuntimeSessionResult RuntimeResult { get; init; } = new();
    public SettlementPlan SettlementPlan { get; init; } = new();
    public BattleReportRecord Report { get; init; } = new();
}

public sealed class BattleGroupBattleFlowService
{
    private readonly BattleSnapshotBuilder _snapshotBuilder = new();
    private readonly BattleRuntimeSession _runtimeSession = new();
    private readonly BattleSettlementService _settlementService = new();
    private readonly BattleReportBuilder _reportBuilder = new();

    public BattleGroupBattleFlowResult RunMinimalBattle(
        string snapshotId,
        string battleId,
        string targetLocationId,
        IEnumerable<BattleGroupState> groups,
        IReadOnlyDictionary<string, HeroState> heroes,
        IReadOnlyDictionary<string, CorpsState> corps)
    {
        BattleStartSnapshot snapshot = _snapshotBuilder.Build(
            snapshotId,
            battleId,
            targetLocationId,
            groups,
            heroes,
            corps);

        BattleRuntimeSessionResult runtimeResult = _runtimeSession.RunMinimal(snapshot);
        SettlementPlan settlementPlan = _settlementService.BuildPlan(
            snapshot.SnapshotId,
            runtimeResult.Outcome,
            runtimeResult.EventStream);
        BattleReportRecord report = _reportBuilder.Build(
            runtimeResult.Outcome,
            runtimeResult.EventStream,
            settlementPlan);

        return new BattleGroupBattleFlowResult
        {
            Snapshot = snapshot,
            RuntimeResult = runtimeResult,
            SettlementPlan = settlementPlan,
            Report = report
        };
    }

    public BattleGroupBattleFlowResult RunSnapshot(BattleStartSnapshot snapshot)
    {
        BattleRuntimeSessionResult runtimeResult = _runtimeSession.RunMinimal(snapshot);
        SettlementPlan settlementPlan = _settlementService.BuildPlan(
            snapshot?.SnapshotId ?? "",
            runtimeResult.Outcome,
            runtimeResult.EventStream);
        BattleReportRecord report = _reportBuilder.Build(
            runtimeResult.Outcome,
            runtimeResult.EventStream,
            settlementPlan);

        return new BattleGroupBattleFlowResult
        {
            Snapshot = snapshot ?? new BattleStartSnapshot(),
            RuntimeResult = runtimeResult,
            SettlementPlan = settlementPlan,
            Report = report
        };
    }
}
