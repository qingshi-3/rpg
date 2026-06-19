using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle.Events;

namespace Rpg.Runtime.Battle.Tactics;

public sealed class BattleGroupTacticalStateStore
{
    private readonly Dictionary<string, BattleGroupTacticalState> _states = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<BattleGroupTacticalRegionMutationResult>> _initializationResults = new(StringComparer.Ordinal);
    private readonly string _battleId;
    private int _nextEventSequence;

    private BattleGroupTacticalStateStore(string battleId = "")
    {
        _battleId = battleId ?? "";
    }

    public IReadOnlyDictionary<string, BattleGroupTacticalState> States => CaptureSnapshots();

    public static BattleGroupTacticalStateStore Empty()
    {
        return new BattleGroupTacticalStateStore();
    }

    public static BattleGroupTacticalStateStore FromBattleGroups(IEnumerable<BattleGroupSnapshot> groups)
    {
        return FromBattleGroups(groups, battleId: "");
    }

    public static BattleGroupTacticalStateStore FromBattleGroups(IEnumerable<BattleGroupSnapshot> groups, string battleId)
    {
        return FromBattleGroups(groups, battleId, objectiveZones: null);
    }

    public static BattleGroupTacticalStateStore FromBattleGroups(
        IEnumerable<BattleGroupSnapshot> groups,
        string battleId,
        IReadOnlyList<BattleObjectiveZoneSnapshot> objectiveZones)
    {
        BattleGroupTacticalStateStore store = new(battleId);
        foreach (BattleGroupSnapshot group in groups ?? Enumerable.Empty<BattleGroupSnapshot>())
        {
            string commanderGroupId = BattleCommanderGroupIdentity.Resolve(group);
            if (string.IsNullOrWhiteSpace(commanderGroupId))
            {
                continue;
            }

            if (store._states.ContainsKey(commanderGroupId))
            {
                if (string.IsNullOrWhiteSpace(group.RuntimeCommanderGroupId))
                {
                    store.RecordDuplicateGroupInitializationResults(group);
                }

                continue;
            }

            BattleGroupTacticalState state = new()
            {
                BattleGroupId = commanderGroupId,
                TacticalMode = group.TacticalMode,
                TacticalIntentPlan = BattleTacticalIntentPolicy.NormalizeForRuntime(group),
                AllowPlayerScopedEngagement = !string.IsNullOrWhiteSpace(group.RuntimeCommanderGroupId),
                AllowAutonomousFallbackTargeting = !string.IsNullOrWhiteSpace(group.RuntimeCommanderGroupId),
                EngagementState = BattleGroupEngagementState.NotEngaged
            };
            store._states[commanderGroupId] = state;

            bool hasAuthoredInitialRegion = false;
            foreach (BattleTacticalRegionSnapshot initialRegion in group.InitialTacticalRegions ?? Enumerable.Empty<BattleTacticalRegionSnapshot>())
            {
                hasAuthoredInitialRegion = true;
                BattleGroupTacticalRegionMutationResult result = store.TrySetRegion(commanderGroupId, initialRegion, isEnemyPolicyMutation: false);
                store.RecordInitializationResult(commanderGroupId, result);
            }

            if (!hasAuthoredInitialRegion &&
                group.TacticalMode == BattleGroupTacticalMode.PlayerCommanded)
            {
                BattleTacticalRegionSnapshot playerPlanRegion = BuildPlayerPlanRegionSeed(
                    group,
                    commanderGroupId,
                    objectiveZones);
                if (playerPlanRegion != null)
                {
                    BattleGroupTacticalRegionMutationResult result = store.TrySetRegion(
                        commanderGroupId,
                        playerPlanRegion,
                        isEnemyPolicyMutation: false);
                    store.RecordInitializationResult(commanderGroupId, result);
                }
            }
        }

        return store;
    }

    public BattleGroupTacticalState GetRequiredSnapshot(string battleGroupId)
    {
        return _states.TryGetValue(battleGroupId ?? "", out BattleGroupTacticalState state)
            ? CloneState(state)
            : throw new KeyNotFoundException($"battle group tactical state not found: {battleGroupId}");
    }

    public IReadOnlyDictionary<string, BattleGroupTacticalState> CaptureSnapshots()
    {
        return new ReadOnlyDictionary<string, BattleGroupTacticalState>(_states.ToDictionary(
            item => item.Key,
            item => CloneState(item.Value),
            StringComparer.Ordinal));
    }

    public IReadOnlyDictionary<string, IReadOnlyList<BattleGroupTacticalRegionMutationResult>> CaptureInitializationResults()
    {
        Dictionary<string, IReadOnlyList<BattleGroupTacticalRegionMutationResult>> snapshots = _initializationResults.ToDictionary(
            item => item.Key,
            item => (IReadOnlyList<BattleGroupTacticalRegionMutationResult>)item.Value.Select(result => result.Clone()).ToArray(),
            StringComparer.Ordinal);
        return new ReadOnlyDictionary<string, IReadOnlyList<BattleGroupTacticalRegionMutationResult>>(snapshots);
    }

    public BattleGroupTacticalRegionMutationResult TrySetRegion(
        string battleGroupId,
        BattleTacticalRegionSnapshot proposedRegion,
        bool isEnemyPolicyMutation)
    {
        return TrySetRegionCore(
            battleGroupId,
            proposedRegion,
            isEnemyPolicyMutation,
            BattleGroupTacticalCommandSource.None,
            BattleEventKind.BattleGroupTacticalRegionSelected,
            BattleGroupTacticalReasonCode.RegionAccepted);
    }

    internal BattleGroupTacticalRegionMutationResult TrySetTemporaryRegion(
        string battleGroupId,
        BattleTacticalRegionSnapshot proposedRegion,
        int runtimeTick)
    {
        BattleGroupTacticalRegionMutationResult result = TrySetRegionCore(
            battleGroupId,
            proposedRegion,
            isEnemyPolicyMutation: true,
            BattleGroupTacticalCommandSource.EnemyPolicy,
            BattleEventKind.BattleGroupTemporaryRegionSelected,
            string.IsNullOrWhiteSpace(proposedRegion?.ReasonCode)
                ? BattleGroupTacticalReasonCode.TemporaryRegionCreatedCluster
                : proposedRegion.ReasonCode);
        if (result.Accepted &&
            _states.TryGetValue(battleGroupId ?? "", out BattleGroupTacticalState state))
        {
            state.LastTemporaryRegionRefreshTick = runtimeTick;
        }

        return result;
    }

    internal BattleGroupTacticalRegionMutationResult TrySetPlayerAutonomousTemporaryRegion(
        string battleGroupId,
        BattleTacticalRegionSnapshot proposedRegion,
        int runtimeTick)
    {
        BattleGroupTacticalRegionMutationResult result = TrySetRegionCore(
            battleGroupId,
            proposedRegion,
            isEnemyPolicyMutation: false,
            BattleGroupTacticalCommandSource.SelfCalculated,
            BattleEventKind.BattleGroupTemporaryRegionSelected,
            string.IsNullOrWhiteSpace(proposedRegion?.ReasonCode)
                ? BattleGroupTacticalReasonCode.PlayerAutonomousTemporaryRegionCreatedCluster
                : proposedRegion.ReasonCode);
        if (result.Accepted &&
            _states.TryGetValue(battleGroupId ?? "", out BattleGroupTacticalState state))
        {
            state.LastTemporaryRegionRefreshTick = runtimeTick;
        }

        return result;
    }

    internal bool TryClearSelectedRegion(
        string battleGroupId,
        BattleGroupTacticalCommandSource requiredSource)
    {
        string normalizedGroupId = battleGroupId ?? "";
        if (!_states.TryGetValue(normalizedGroupId, out BattleGroupTacticalState state) ||
            state.SelectedRegion == null ||
            requiredSource != BattleGroupTacticalCommandSource.None &&
            state.SelectedRegionCommandSource != requiredSource)
        {
            return false;
        }

        // Clearing command-owned region state is a lifecycle transition, not a
        // new tactical selection. Runtime action events continue to describe the
        // current execution command for Presentation and reports.
        state.SelectedRegion = null;
        state.SelectedRegionCommandSource = BattleGroupTacticalCommandSource.None;
        state.LastTemporaryRegionRefreshTick = -1;
        state.Version++;
        return true;
    }

    internal BattleGroupTacticalRegionMutationResult TrySetLocalCombatRegion(
        string battleGroupId,
        BattleTacticalRegionSnapshot proposedRegion)
    {
        string normalizedGroupId = battleGroupId ?? "";
        BattleGroupTacticalRegionMutationResult validation = ValidateRegionMutation(
            normalizedGroupId,
            proposedRegion,
            isEnemyPolicyMutation: false,
            BattleGroupTacticalCommandSource.None,
            out BattleGroupTacticalState state);
        if (!validation.Accepted)
        {
            return validation;
        }

        if (proposedRegion.Kind != BattleTacticalRegionKind.LocalCombat)
        {
            return Rejected(normalizedGroupId, proposedRegion, BattleGroupTacticalReasonCode.RegionRejectedInvalidRegion);
        }

        if (IsEquivalentRegion(state.LocalCombatRegion, proposedRegion))
        {
            return new BattleGroupTacticalRegionMutationResult
            {
                Accepted = true,
                ReasonCode = proposedRegion.ReasonCode ?? "",
                Event = null
            };
        }

        BattleTacticalRegionSnapshot accepted = CloneRegion(proposedRegion);
        accepted.Version = state.Version + 1;
        state.LocalCombatRegion = accepted;
        state.Version++;
        return new BattleGroupTacticalRegionMutationResult
        {
            Accepted = true,
            ReasonCode = accepted.ReasonCode,
            Event = BuildEvent(
                normalizedGroupId,
                accepted,
                BattleEventKind.BattleGroupLocalCombatRegionChanged,
                accepted.ReasonCode)
        };
    }

    private BattleGroupTacticalRegionMutationResult TrySetRegionCore(
        string battleGroupId,
        BattleTacticalRegionSnapshot proposedRegion,
        bool isEnemyPolicyMutation,
        BattleGroupTacticalCommandSource requestedCommandSource,
        BattleEventKind acceptedEventKind,
        string acceptedReasonCode)
    {
        string normalizedGroupId = battleGroupId ?? "";
        BattleGroupTacticalRegionMutationResult validation = ValidateRegionMutation(
            normalizedGroupId,
            proposedRegion,
            isEnemyPolicyMutation,
            requestedCommandSource,
            out BattleGroupTacticalState state);
        if (!validation.Accepted)
        {
            return validation;
        }

        BattleTacticalRegionSnapshot accepted = CloneRegion(proposedRegion);
        accepted.Version = state.Version + 1;
        state.SelectedRegion = accepted;
        state.SelectedRegionCommandSource = ResolveCommandSource(state, isEnemyPolicyMutation, requestedCommandSource);
        state.Version++;
        return new BattleGroupTacticalRegionMutationResult
        {
            Accepted = true,
            ReasonCode = acceptedReasonCode ?? BattleGroupTacticalReasonCode.RegionAccepted,
            Event = BuildEvent(normalizedGroupId, accepted, acceptedEventKind, acceptedReasonCode ?? BattleGroupTacticalReasonCode.RegionAccepted)
        };
    }

    private BattleGroupTacticalRegionMutationResult ValidateRegionMutation(
        string normalizedGroupId,
        BattleTacticalRegionSnapshot proposedRegion,
        bool isEnemyPolicyMutation,
        BattleGroupTacticalCommandSource requestedCommandSource,
        out BattleGroupTacticalState state)
    {
        state = null;
        if (!_states.TryGetValue(normalizedGroupId, out state))
        {
            return Rejected(normalizedGroupId, proposedRegion, BattleGroupTacticalReasonCode.RegionRejectedMissingGroup);
        }

        if (proposedRegion == null)
        {
            return Rejected(normalizedGroupId, proposedRegion, BattleGroupTacticalReasonCode.RegionRejectedInvalidRegion);
        }

        string ownerBattleGroupId = proposedRegion.OwnerBattleGroupId ?? "";
        if (string.IsNullOrWhiteSpace(ownerBattleGroupId))
        {
            return Rejected(normalizedGroupId, proposedRegion, BattleGroupTacticalReasonCode.RegionRejectedMissingOwner);
        }

        if (!string.Equals(ownerBattleGroupId, normalizedGroupId, StringComparison.Ordinal))
        {
            return Rejected(normalizedGroupId, proposedRegion, BattleGroupTacticalReasonCode.RegionRejectedOwnerMismatch);
        }

        if (isEnemyPolicyMutation && state.TacticalMode == BattleGroupTacticalMode.PlayerCommanded)
        {
            // Enemy policy can read player tactical facts, but player-commanded intent remains owned by player plan/commands.
            return Rejected(normalizedGroupId, proposedRegion, BattleGroupTacticalReasonCode.RegionRejectedPlayerPolicyOverwrite);
        }

        if (requestedCommandSource == BattleGroupTacticalCommandSource.SelfCalculated)
        {
            if (state.TacticalMode != BattleGroupTacticalMode.PlayerCommanded ||
                !state.AllowAutonomousFallbackTargeting)
            {
                return Rejected(normalizedGroupId, proposedRegion, BattleGroupTacticalReasonCode.RegionRejectedPlayerPolicyOverwrite);
            }

            if (state.SelectedRegion != null &&
                state.SelectedRegionCommandSource == BattleGroupTacticalCommandSource.PlayerCommand)
            {
                return Rejected(normalizedGroupId, proposedRegion, BattleGroupTacticalReasonCode.RegionRejectedPlayerPolicyOverwrite);
            }
        }

        if (proposedRegion.Width < BattleGroupTacticalPolicySettings.MinimumRegionWidth ||
            proposedRegion.Height < BattleGroupTacticalPolicySettings.MinimumRegionHeight)
        {
            return Rejected(normalizedGroupId, proposedRegion, BattleGroupTacticalReasonCode.RegionRejectedInvalidSize);
        }

        return new BattleGroupTacticalRegionMutationResult
        {
            Accepted = true,
            ReasonCode = BattleGroupTacticalReasonCode.RegionAccepted,
            Event = null
        };
    }

    private static BattleGroupTacticalCommandSource ResolveCommandSource(
        BattleGroupTacticalState state,
        bool isEnemyPolicyMutation,
        BattleGroupTacticalCommandSource requestedCommandSource)
    {
        if (requestedCommandSource != BattleGroupTacticalCommandSource.None)
        {
            return requestedCommandSource;
        }

        if (isEnemyPolicyMutation ||
            state?.TacticalMode is BattleGroupTacticalMode.EnemyOffense
                or BattleGroupTacticalMode.EnemyActiveDefense
                or BattleGroupTacticalMode.EnemyHoldDefense)
        {
            return BattleGroupTacticalCommandSource.EnemyPolicy;
        }

        return BattleGroupTacticalCommandSource.PlayerCommand;
    }

    internal bool TryApplyEngagementState(
        string battleGroupId,
        BattleGroupEngagementState engagementState,
        BattleGroupTacticalMode tacticalMode)
    {
        string normalizedGroupId = battleGroupId ?? "";
        if (!_states.TryGetValue(normalizedGroupId, out BattleGroupTacticalState state))
        {
            return false;
        }

        if (state.TacticalMode == BattleGroupTacticalMode.PlayerCommanded &&
            tacticalMode != BattleGroupTacticalMode.PlayerCommanded)
        {
            // Player groups may consume local combat response, but enemy policy
            // must never convert their command-owned posture into enemy assault.
            return false;
        }

        if (state.EngagementState == engagementState && state.TacticalMode == tacticalMode)
        {
            return false;
        }

        state.EngagementState = engagementState;
        state.TacticalMode = tacticalMode;
        state.NoPerceivedHostileTicks = 0;
        state.Version++;
        return true;
    }

    internal int RecordNoPerceivedHostileTick(string battleGroupId)
    {
        string normalizedGroupId = battleGroupId ?? "";
        if (!_states.TryGetValue(normalizedGroupId, out BattleGroupTacticalState state))
        {
            return 0;
        }

        state.NoPerceivedHostileTicks++;
        return state.NoPerceivedHostileTicks;
    }

    internal void ResetNoPerceivedHostileTicks(string battleGroupId)
    {
        string normalizedGroupId = battleGroupId ?? "";
        if (_states.TryGetValue(normalizedGroupId, out BattleGroupTacticalState state))
        {
            state.NoPerceivedHostileTicks = 0;
        }
    }

    internal void RecordMemberDamageTriggerTick(string battleGroupId, int runtimeTick)
    {
        string normalizedGroupId = battleGroupId ?? "";
        if (_states.TryGetValue(normalizedGroupId, out BattleGroupTacticalState state) &&
            state.LastMemberDamageTriggerTick != runtimeTick)
        {
            state.LastMemberDamageTriggerTick = runtimeTick;
            state.Version++;
        }
    }

    internal void RecordMemberAttackTriggerTick(string battleGroupId, int runtimeTick)
    {
        string normalizedGroupId = battleGroupId ?? "";
        if (_states.TryGetValue(normalizedGroupId, out BattleGroupTacticalState state) &&
            state.LastMemberAttackTriggerTick != runtimeTick)
        {
            state.LastMemberAttackTriggerTick = runtimeTick;
            state.Version++;
        }
    }

    internal static BattleGroupTacticalState CloneState(BattleGroupTacticalState state)
    {
        return new BattleGroupTacticalState
        {
            BattleGroupId = state?.BattleGroupId ?? "",
            TacticalMode = state?.TacticalMode ?? BattleGroupTacticalMode.PlayerCommanded,
            TacticalIntentPlan = BattleTacticalIntentPolicy.CopyIntentPlan(state?.TacticalIntentPlan),
            AllowPlayerScopedEngagement = state?.AllowPlayerScopedEngagement ?? false,
            AllowAutonomousFallbackTargeting = state?.AllowAutonomousFallbackTargeting ?? false,
            EngagementState = state?.EngagementState ?? BattleGroupEngagementState.NotEngaged,
            SelectedRegion = CloneRegion(state?.SelectedRegion),
            SelectedRegionCommandSource = state?.SelectedRegionCommandSource ?? BattleGroupTacticalCommandSource.None,
            LocalCombatRegion = CloneRegion(state?.LocalCombatRegion),
            Version = state?.Version ?? 0,
            LastTemporaryRegionRefreshTick = state?.LastTemporaryRegionRefreshTick ?? -1,
            NoPerceivedHostileTicks = state?.NoPerceivedHostileTicks ?? 0,
            LastMemberDamageTriggerTick = state?.LastMemberDamageTriggerTick ?? -1,
            LastMemberAttackTriggerTick = state?.LastMemberAttackTriggerTick ?? -1
        };
    }

    internal static BattleTacticalRegionSnapshot CloneRegion(BattleTacticalRegionSnapshot region)
    {
        if (region == null)
        {
            return null;
        }

        return new BattleTacticalRegionSnapshot
        {
            RegionId = region.RegionId ?? "",
            OwnerBattleGroupId = region.OwnerBattleGroupId ?? "",
            Kind = region.Kind,
            SourceRegionId = region.SourceRegionId ?? "",
            ReasonCode = region.ReasonCode ?? "",
            CenterCellX = region.CenterCellX,
            CenterCellY = region.CenterCellY,
            CenterCellHeight = region.CenterCellHeight,
            Width = region.Width,
            Height = region.Height,
            Version = region.Version
        };
    }

    private void RecordInitializationResult(string battleGroupId, BattleGroupTacticalRegionMutationResult result)
    {
        string key = battleGroupId ?? "";
        if (!_initializationResults.TryGetValue(key, out List<BattleGroupTacticalRegionMutationResult> results))
        {
            results = new List<BattleGroupTacticalRegionMutationResult>();
            _initializationResults[key] = results;
        }

        results.Add(result.Clone());
    }

    private void RecordDuplicateGroupInitializationResults(BattleGroupSnapshot group)
    {
        BattleTacticalRegionSnapshot region = group.InitialTacticalRegions?.FirstOrDefault();
        RecordInitializationResult(
            group.BattleGroupId,
            Rejected(group.BattleGroupId, region, BattleGroupTacticalReasonCode.RegionRejectedDuplicateGroup));
    }

    private BattleGroupTacticalRegionMutationResult Rejected(
        string battleGroupId,
        BattleTacticalRegionSnapshot region,
        string reasonCode)
    {
        return new BattleGroupTacticalRegionMutationResult
        {
            Accepted = false,
            ReasonCode = reasonCode,
            Event = BuildEvent(battleGroupId, region, BattleEventKind.BattleGroupTacticalRegionRejected, reasonCode)
        };
    }

    private static bool IsEquivalentRegion(
        BattleTacticalRegionSnapshot current,
        BattleTacticalRegionSnapshot proposed)
    {
        return current != null &&
               proposed != null &&
               string.Equals(current.OwnerBattleGroupId ?? "", proposed.OwnerBattleGroupId ?? "", StringComparison.Ordinal) &&
               current.Kind == proposed.Kind &&
               current.CenterCellX == proposed.CenterCellX &&
               current.CenterCellY == proposed.CenterCellY &&
               current.CenterCellHeight == proposed.CenterCellHeight &&
               current.Width == proposed.Width &&
               current.Height == proposed.Height &&
               string.Equals(current.SourceRegionId ?? "", proposed.SourceRegionId ?? "", StringComparison.Ordinal) &&
               string.Equals(current.ReasonCode ?? "", proposed.ReasonCode ?? "", StringComparison.Ordinal);
    }

    private static BattleTacticalRegionSnapshot BuildPlayerPlanRegionSeed(
        BattleGroupSnapshot group,
        string commanderGroupId,
        IReadOnlyList<BattleObjectiveZoneSnapshot> objectiveZones)
    {
        BattleGroupPlanSnapshot plan = group?.Plan;
        if (plan == null || string.IsNullOrWhiteSpace(commanderGroupId))
        {
            return null;
        }

        BattleObjectiveZoneSnapshot zone = (objectiveZones ?? Array.Empty<BattleObjectiveZoneSnapshot>())
            .FirstOrDefault(candidate => string.Equals(
                candidate?.ObjectiveZoneId ?? "",
                plan.ObjectiveZoneId ?? "",
                StringComparison.Ordinal));
        if (zone != null)
        {
            int width = Math.Max(1, zone.Width);
            int height = Math.Max(1, zone.Height);
            return BuildPlanRegion(
                zone.ObjectiveZoneId,
                commanderGroupId,
                zone.ObjectiveZoneId,
                zone.CellX,
                zone.CellY,
                zone.CellHeight,
                width,
                height);
        }

        if (!plan.HasObjectiveAnchor)
        {
            return null;
        }

        int planWidth = Math.Max(1, plan.ObjectiveWidth);
        int planHeight = Math.Max(1, plan.ObjectiveHeight);
        string regionId = string.IsNullOrWhiteSpace(plan.ObjectiveZoneId)
            ? $"{commanderGroupId}:player_command:{plan.ObjectiveCellX}:{plan.ObjectiveCellY}:{plan.ObjectiveCellHeight}"
            : plan.ObjectiveZoneId;
        return BuildPlanRegion(
            regionId,
            commanderGroupId,
            plan.ObjectiveZoneId ?? "",
            plan.ObjectiveCellX,
            plan.ObjectiveCellY,
            plan.ObjectiveCellHeight,
            planWidth,
            planHeight);
    }

    private static BattleTacticalRegionSnapshot BuildPlanRegion(
        string regionId,
        string commanderGroupId,
        string sourceRegionId,
        int cellX,
        int cellY,
        int cellHeight,
        int width,
        int height)
    {
        return new BattleTacticalRegionSnapshot
        {
            RegionId = regionId ?? "",
            OwnerBattleGroupId = commanderGroupId ?? "",
            Kind = BattleTacticalRegionKind.FixedTarget,
            SourceRegionId = sourceRegionId ?? "",
            ReasonCode = BattleGroupTacticalReasonCode.RegionAccepted,
            CenterCellX = cellX + (Math.Max(1, width) - 1) / 2,
            CenterCellY = cellY + (Math.Max(1, height) - 1) / 2,
            CenterCellHeight = cellHeight,
            Width = Math.Max(1, width),
            Height = Math.Max(1, height)
        };
    }

    private BattleEvent BuildEvent(
        string battleGroupId,
        BattleTacticalRegionSnapshot region,
        BattleEventKind kind,
        string reasonCode)
    {
        int eventSequence = ++_nextEventSequence;
        string regionId = region?.RegionId ?? "region";
        return new BattleEvent
        {
            EventId = $"{_battleId}:{battleGroupId}:{regionId}:{reasonCode}:{eventSequence}",
            BattleId = _battleId,
            BattleGroupId = battleGroupId ?? "",
            Kind = kind,
            ReasonCode = reasonCode ?? "",
            TacticalRegionId = region?.RegionId ?? "",
            TacticalRegionKind = region?.Kind.ToString() ?? "",
            TacticalRegionVersion = region?.Version ?? 0
        };
    }
}
