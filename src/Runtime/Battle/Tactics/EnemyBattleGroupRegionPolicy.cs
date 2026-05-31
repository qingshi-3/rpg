using Rpg.Application.Battle.Snapshots;

namespace Rpg.Runtime.Battle.Tactics;

public static class EnemyBattleGroupRegionPolicy
{
    public static BattleRegionMovementGoal ResolveMovementGoal(BattleGroupTacticalState state)
    {
        if (state == null ||
            state.EngagementState != BattleGroupEngagementState.NotEngaged ||
            !IsEnemyRegionMovementMode(state.TacticalMode) ||
            state.SelectedRegion is not { } region ||
            region.Kind is not (BattleTacticalRegionKind.FixedTarget or BattleTacticalRegionKind.TemporaryTarget))
        {
            return null;
        }

        return new BattleRegionMovementGoal
        {
            RegionId = region.RegionId ?? "",
            OwnerBattleGroupId = region.OwnerBattleGroupId ?? "",
            Kind = region.Kind,
            CenterCellX = region.CenterCellX,
            CenterCellY = region.CenterCellY,
            CenterCellHeight = region.CenterCellHeight,
            Width = System.Math.Max(1, region.Width),
            Height = System.Math.Max(1, region.Height),
            SourceRegionId = region.SourceRegionId ?? "",
            ReasonCode = region.Kind == BattleTacticalRegionKind.FixedTarget
                ? BattleGroupTacticalReasonCode.RegionFixedAdvance
                : BattleGroupTacticalReasonCode.RegionTemporaryAdvance
        };
    }

    private static bool IsEnemyRegionMovementMode(BattleGroupTacticalMode mode)
    {
        return mode is BattleGroupTacticalMode.EnemyOffense or BattleGroupTacticalMode.EnemyActiveDefense;
    }
}
