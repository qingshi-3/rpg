using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle.Navigation;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Runtime.Battle;

internal static class BattleTacticalAreaDiagnosticLogger
{
    internal static void LogAreaSnapshot(
        BattleRuntimeState state,
        IReadOnlyCollection<BattleRuntimeActor> livingCorps,
        int runtimeTick,
        string reasonCode)
    {
        if (state == null)
        {
            return;
        }

        IReadOnlyCollection<BattleRuntimeActor> units = livingCorps ?? System.Array.Empty<BattleRuntimeActor>();
        GameLog.Info(
            "BattleAreaSnapshot",
            $"BattleAreaSnapshot battle={state.BattleId ?? ""} tick={runtimeTick} reason={reasonCode ?? ""} combatZones={state.CombatZones.Count} deploymentZones={state.ObjectiveZones.Count} groupActionZones={state.GroupActionZones.Count} units={units.Count}");

        foreach (BattleCombatZoneSnapshot zone in state.CombatZones.Values.OrderBy(item => item.CombatZoneId, System.StringComparer.Ordinal))
        {
            GameLog.Info(
                "BattleCombatZoneSnapshot",
                $"BattleCombatZoneSnapshot battle={state.BattleId ?? ""} tick={runtimeTick} zone={zone.CombatZoneId ?? ""} bounds=({zone.MinCellX},{zone.MinCellY})-({zone.MaxCellX},{zone.MaxCellY}) center=({zone.CenterCellX},{zone.CenterCellY},{zone.CenterCellHeight}) owner={zone.OwnerBattleGroupId ?? ""} units={string.Join(",", zone.ActorIds)} groups={string.Join(",", zone.BattleGroupIds)} reason={zone.ReasonCode ?? ""}");
        }

        foreach (BattleObjectiveZoneSnapshot zone in state.ObjectiveZones.OrderBy(item => item.ObjectiveZoneId, System.StringComparer.Ordinal))
        {
            int maxX = zone.CellX + System.Math.Max(1, zone.Width) - 1;
            int maxY = zone.CellY + System.Math.Max(1, zone.Height) - 1;
            GameLog.Info(
                "BattleDeploymentZoneSnapshot",
                $"BattleDeploymentZoneSnapshot battle={state.BattleId ?? ""} tick={runtimeTick} zone={zone.ObjectiveZoneId ?? ""} kind={zone.ObjectiveRole ?? ""} side={zone.DeploymentSide ?? ""} faction={zone.FactionId ?? ""} bounds=({zone.CellX},{zone.CellY})-({maxX},{maxY}) height={zone.CellHeight}");
        }

        foreach (BattleGroupActionZoneSnapshot zone in state.GroupActionZones.Values.OrderBy(item => item.BattleGroupId, System.StringComparer.Ordinal))
        {
            GameLog.Info(
                "BattleGroupActionZoneSnapshot",
                $"BattleGroupActionZoneSnapshot battle={state.BattleId ?? ""} tick={runtimeTick} group={zone.BattleGroupId ?? ""} kind={zone.Kind} bounds=({zone.MinCellX},{zone.MinCellY})-({zone.MaxCellX},{zone.MaxCellY}) center=({zone.CenterCellX},{zone.CenterCellY},{zone.CenterCellHeight}) targetCombatZone={zone.TargetCombatZoneId ?? ""} targetRegion={zone.TargetRegionId ?? ""} reason={zone.ReasonCode ?? ""}");
        }

        foreach (BattleRuntimeActor actor in units.OrderBy(item => item.ActorId, System.StringComparer.Ordinal))
        {
            BattleGridCoord anchor = new(actor.GridX, actor.GridY, actor.GridHeight);
            string combatZoneId = state.CombatZones.Values
                .FirstOrDefault(zone => zone.ActorIds.Contains(actor.ActorId ?? "", System.StringComparer.Ordinal))
                ?.CombatZoneId ?? "";
            string actionZoneId = state.GroupActionZones.TryGetValue(actor.BattleGroupId ?? "", out BattleGroupActionZoneSnapshot actionZone) &&
                                  BattleGroupActionZoneResolver.IsInsideActionZone(actor, anchor, actionZone)
                ? actionZone.BattleGroupId
                : "";
            GameLog.Info(
                "BattleUnitPositionSnapshot",
                $"BattleUnitPositionSnapshot battle={state.BattleId ?? ""} tick={runtimeTick} actor={actor.ActorId ?? ""} group={actor.BattleGroupId ?? ""} faction={actor.FactionId ?? ""} cell=({actor.GridX},{actor.GridY},{actor.GridHeight}) footprint={BattleActorFootprint.NormalizeSize(actor.FootprintWidth)}x{BattleActorFootprint.NormalizeSize(actor.FootprintHeight)} combatZone={combatZoneId} groupActionZone={actionZoneId} planState={actor.PlanState}");
        }
    }
}
