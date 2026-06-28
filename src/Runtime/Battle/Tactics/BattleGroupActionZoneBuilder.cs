using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle.Tactics;

internal static class BattleGroupActionZoneBuilder
{
    internal static IReadOnlyDictionary<string, BattleGroupActionZoneSnapshot> Build(
        IReadOnlyDictionary<string, BattleGroupTacticalState> tacticalStates,
        IEnumerable<BattleRuntimeActor> livingCorps,
        IReadOnlyDictionary<string, BattleCombatZoneSnapshot> combatZones,
        IReadOnlyDictionary<string, BattleGroupActionZoneSnapshot> previousActionZones,
        int runtimeTick)
    {
        BattleRuntimeActor[] actors = (livingCorps ?? Enumerable.Empty<BattleRuntimeActor>())
            .Where(item => item?.Kind == BattleRuntimeActorKind.Corps && item.HitPoints > 0)
            .OrderBy(item => item.ActorId, StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, BattleGroupActionZoneSnapshot> zones = new(StringComparer.Ordinal);
        foreach (BattleGroupTacticalState state in (tacticalStates?.Values ?? Enumerable.Empty<BattleGroupTacticalState>())
                     .OrderBy(item => item.BattleGroupId, StringComparer.Ordinal))
        {
            BattleRuntimeActor[] members = actors
                .Where(item => string.Equals(item.BattleGroupId ?? "", state.BattleGroupId ?? "", StringComparison.Ordinal))
                .ToArray();
            if (members.Length == 0)
            {
                continue;
            }

            BattleCombatZoneSnapshot selectedCombatZone = state.EngagementState == BattleGroupEngagementState.Engaged
                ? SelectCombatZone(state.BattleGroupId, members, combatZones, previousActionZones)
                : null;
            BattleGroupActionZoneSnapshot actionZone = selectedCombatZone != null
                ? FromCombatZone(state.BattleGroupId, selectedCombatZone, runtimeTick)
                : FromRegionOrMembers(state, members, runtimeTick);
            zones[actionZone.BattleGroupId] = actionZone;
        }

        return new ReadOnlyDictionary<string, BattleGroupActionZoneSnapshot>(zones);
    }

    internal static BattleTacticalRegionSnapshot ToLocalCombatRegion(BattleGroupActionZoneSnapshot actionZone)
    {
        if (actionZone == null ||
            actionZone.Kind != BattleGroupActionZoneKind.CombatJoin ||
            string.IsNullOrWhiteSpace(actionZone.BattleGroupId))
        {
            return null;
        }

        return new BattleTacticalRegionSnapshot
        {
            RegionId = $"{actionZone.BattleGroupId}:combat_join:{actionZone.TargetCombatZoneId}",
            OwnerBattleGroupId = actionZone.BattleGroupId,
            Kind = BattleTacticalRegionKind.LocalCombat,
            SourceRegionId = actionZone.TargetCombatZoneId ?? "",
            ReasonCode = actionZone.ReasonCode ?? "",
            CenterCellX = actionZone.CenterCellX,
            CenterCellY = actionZone.CenterCellY,
            CenterCellHeight = actionZone.CenterCellHeight,
            Width = Math.Max(1, actionZone.MaxCellX - actionZone.MinCellX + 1),
            Height = Math.Max(1, actionZone.MaxCellY - actionZone.MinCellY + 1),
            Version = actionZone.Version
        };
    }

    private static BattleCombatZoneSnapshot SelectCombatZone(
        string battleGroupId,
        IReadOnlyList<BattleRuntimeActor> members,
        IReadOnlyDictionary<string, BattleCombatZoneSnapshot> combatZones,
        IReadOnlyDictionary<string, BattleGroupActionZoneSnapshot> previousActionZones)
    {
        if (combatZones == null || combatZones.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(battleGroupId) &&
            previousActionZones != null &&
            previousActionZones.TryGetValue(battleGroupId, out BattleGroupActionZoneSnapshot previousActionZone) &&
            previousActionZone?.Kind == BattleGroupActionZoneKind.CombatJoin &&
            !string.IsNullOrWhiteSpace(previousActionZone.TargetCombatZoneId) &&
            combatZones.TryGetValue(previousActionZone.TargetCombatZoneId, out BattleCombatZoneSnapshot retainedCombatZone))
        {
            // Refresh computes the next selected combat scope before old action
            // state is discarded. This prevents a one-tick objective fallback
            // when allied participants die while this group is already joining
            // the still-live fight.
            return retainedCombatZone;
        }

        HashSet<string> memberIds = members.Select(item => item.ActorId ?? "").ToHashSet(StringComparer.Ordinal);
        return combatZones.Values
            .Where(zone => zone?.ActorIds?.Any(memberIds.Contains) == true)
            .OrderByDescending(zone => zone.ActorIds.Count)
            .ThenBy(zone => zone.CombatZoneId, StringComparer.Ordinal)
            .FirstOrDefault()
            ?? combatZones.Values
                .OrderBy(zone => DistanceToZone(members, zone))
                .ThenBy(zone => zone.CombatZoneId, StringComparer.Ordinal)
                .FirstOrDefault();
    }

    internal static BattleGroupActionZoneSnapshot FromCombatZone(
        string battleGroupId,
        BattleCombatZoneSnapshot combatZone,
        int runtimeTick)
    {
        return new BattleGroupActionZoneSnapshot
        {
            BattleGroupId = battleGroupId ?? "",
            Kind = BattleGroupActionZoneKind.CombatJoin,
            TargetCombatZoneId = combatZone?.CombatZoneId ?? "",
            TargetRegionId = "",
            ReasonCode = "group_action_combat_join",
            Version = runtimeTick + 1,
            LastBuiltRuntimeTick = runtimeTick,
            MinCellX = combatZone?.MinCellX ?? 0,
            MinCellY = combatZone?.MinCellY ?? 0,
            MaxCellX = combatZone?.MaxCellX ?? 0,
            MaxCellY = combatZone?.MaxCellY ?? 0,
            CenterCellX = combatZone?.CenterCellX ?? 0,
            CenterCellY = combatZone?.CenterCellY ?? 0,
            CenterCellHeight = combatZone?.CenterCellHeight ?? 0
        };
    }

    private static BattleGroupActionZoneSnapshot FromRegionOrMembers(
        BattleGroupTacticalState state,
        IReadOnlyList<BattleRuntimeActor> members,
        int runtimeTick)
    {
        BattleTacticalRegionSnapshot selected = state?.SelectedRegion;
        if (selected != null)
        {
            int selectedWidth = Math.Max(1, selected.Width);
            int selectedHeight = Math.Max(1, selected.Height);
            int selectedMinX = selected.CenterCellX - (selectedWidth - 1) / 2;
            int selectedMinY = selected.CenterCellY - (selectedHeight - 1) / 2;
            return new BattleGroupActionZoneSnapshot
            {
                BattleGroupId = state.BattleGroupId ?? "",
                Kind = BattleGroupActionZoneKind.ObjectiveMove,
                TargetRegionId = selected.RegionId ?? "",
                ReasonCode = selected.Kind == BattleTacticalRegionKind.TemporaryTarget
                    ? BattleGroupTacticalReasonCode.RegionTemporaryAdvance
                    : BattleGroupTacticalReasonCode.RegionFixedAdvance,
                Version = runtimeTick + 1,
                LastBuiltRuntimeTick = runtimeTick,
                MinCellX = selectedMinX,
                MinCellY = selectedMinY,
                MaxCellX = selectedMinX + selectedWidth - 1,
                MaxCellY = selectedMinY + selectedHeight - 1,
                CenterCellX = selected.CenterCellX,
                CenterCellY = selected.CenterCellY,
                CenterCellHeight = selected.CenterCellHeight
            };
        }

        BattleRuntimeActor representative = members.FirstOrDefault(item => item.HasObjectiveAnchor) ?? members[0];
        if (representative.HasObjectiveAnchor)
        {
            return new BattleGroupActionZoneSnapshot
            {
                BattleGroupId = state?.BattleGroupId ?? representative.BattleGroupId ?? "",
                Kind = BattleGroupActionZoneKind.ObjectiveMove,
                TargetRegionId = representative.ObjectiveZoneId ?? "",
                ReasonCode = "group_action_objective_move",
                Version = runtimeTick + 1,
                LastBuiltRuntimeTick = runtimeTick,
                MinCellX = representative.ObjectiveGridX,
                MinCellY = representative.ObjectiveGridY,
                MaxCellX = representative.ObjectiveGridX + Math.Max(1, representative.ObjectiveWidth) - 1,
                MaxCellY = representative.ObjectiveGridY + Math.Max(1, representative.ObjectiveHeight) - 1,
                CenterCellX = representative.ObjectiveGridX,
                CenterCellY = representative.ObjectiveGridY,
                CenterCellHeight = representative.ObjectiveGridHeight
            };
        }

        int minX = members.Min(item => item.GridX);
        int minY = members.Min(item => item.GridY);
        int maxX = members.Max(item => item.GridX + BattleActorFootprint.NormalizeSize(item.FootprintWidth) - 1);
        int maxY = members.Max(item => item.GridY + BattleActorFootprint.NormalizeSize(item.FootprintHeight) - 1);
        return new BattleGroupActionZoneSnapshot
        {
            BattleGroupId = state?.BattleGroupId ?? representative.BattleGroupId ?? "",
            Kind = BattleGroupActionZoneKind.Hold,
            ReasonCode = "group_action_hold_current_area",
            Version = runtimeTick + 1,
            LastBuiltRuntimeTick = runtimeTick,
            MinCellX = minX,
            MinCellY = minY,
            MaxCellX = maxX,
            MaxCellY = maxY,
            CenterCellX = minX + (maxX - minX) / 2,
            CenterCellY = minY + (maxY - minY) / 2,
            CenterCellHeight = representative.GridHeight
        };
    }

    private static int DistanceToZone(
        IReadOnlyList<BattleRuntimeActor> members,
        BattleCombatZoneSnapshot zone)
    {
        return members
            .Select(member =>
                Math.Abs(member.GridX - (zone?.CenterCellX ?? 0)) +
                Math.Abs(member.GridY - (zone?.CenterCellY ?? 0)) +
                Math.Abs(member.GridHeight - (zone?.CenterCellHeight ?? 0)))
            .DefaultIfEmpty(int.MaxValue)
            .Min();
    }
}
