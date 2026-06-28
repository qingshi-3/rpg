using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Navigation;
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
        AttachHeadlessDiagnosticCombatStats(snapshot);
        AttachHeadlessDiagnosticTopology(snapshot);

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

    private static void AttachHeadlessDiagnosticCombatStats(BattleStartSnapshot snapshot)
    {
        foreach (BattleGroupSnapshot group in snapshot?.BattleGroups ?? Enumerable.Empty<BattleGroupSnapshot>())
        {
            if (group == null)
            {
                continue;
            }

            // RunMinimalBattle is a diagnostic vertical-slice helper over domain-only
            // groups. Strategic production launch must provide authored combat stats.
            group.MaxHitPoints = group.MaxHitPoints > 0
                ? group.MaxHitPoints
                : System.Math.Max(1, group.CorpsStrength);
            group.AttackDamage = group.AttackDamage > 0 ? group.AttackDamage : 1;
            group.AttackRange = group.AttackRange > 0 ? group.AttackRange : 1;
            group.AttackSpeed = double.IsNaN(group.AttackSpeed) || double.IsInfinity(group.AttackSpeed) || group.AttackSpeed <= 0
                ? BattleAttackSpeedPolicy.DefaultAttackSpeed
                : group.AttackSpeed;
            group.MoveStepSeconds = double.IsNaN(group.MoveStepSeconds) || double.IsInfinity(group.MoveStepSeconds) || group.MoveStepSeconds <= 0
                ? BattleActionTimingPolicy.DefaultMoveStepSeconds
                : group.MoveStepSeconds;
        }
    }

    private static void AttachHeadlessDiagnosticTopology(BattleStartSnapshot snapshot)
    {
        if (snapshot?.LocationContext?.NavigationTopology?.HasNodes == true ||
            snapshot?.BattleGroups == null ||
            snapshot.BattleGroups.Count == 0)
        {
            return;
        }

        // RunMinimalBattle is a headless diagnostic helper. Production launch
        // paths must compile authored map topology before calling Runtime.
        int minX = snapshot.BattleGroups.Min(group => group.CellX) - 2;
        int minY = snapshot.BattleGroups.Min(group => group.CellY) - 2;
        int maxX = snapshot.BattleGroups.Max(group => group.CellX + System.Math.Max(1, group.FootprintWidth) - 1) + 2;
        int maxY = snapshot.BattleGroups.Max(group => group.CellY + System.Math.Max(1, group.FootprintHeight) - 1) + 2;
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                snapshot.LocationContext.NavigationSurfaces.Add(new BattleNavigationSurfaceSnapshot
                {
                    X = x,
                    Y = y,
                    Height = 0,
                    MoveCost = 1
                });
            }
        }

        snapshot.LocationContext.NavigationTopology = BattleNavigationTopologyCompiler.Compile(
            snapshot.LocationContext.NavigationSurfaces,
            snapshot.LocationContext.NavigationConnections);
    }
}
