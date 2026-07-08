using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Domain.Corps;
using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Application.StrategicBattleBridge;

public sealed class StrategicBattleLaunchSnapshotSyncResult
{
    public bool Success { get; init; }
    public string FailureReason { get; init; } = "";
    public BattleStartSnapshot Snapshot { get; init; } = new();

    public static StrategicBattleLaunchSnapshotSyncResult Ok(BattleStartSnapshot snapshot)
    {
        return new StrategicBattleLaunchSnapshotSyncResult
        {
            Success = true,
            Snapshot = snapshot ?? new BattleStartSnapshot()
        };
    }

    public static StrategicBattleLaunchSnapshotSyncResult Failed(string reason)
    {
        return new StrategicBattleLaunchSnapshotSyncResult
        {
            Success = false,
            FailureReason = string.IsNullOrWhiteSpace(reason)
                ? "strategic_battle_launch_snapshot_sync_failed"
                : reason
        };
    }
}

public sealed class StrategicBattleLaunchSnapshotSyncService
{
    public const string ParticipantMappingMissingReason = "strategic_battle_launch_participant_mapping_missing";
    public const string CombatStatsMissingReason = "strategic_battle_launch_combat_stats_missing";

    public StrategicBattleLaunchSnapshotSyncResult Sync(
        StrategicBattleActiveContext activeContext,
        BattleStartRequest compatibilityRequest)
    {
        BattleStartSnapshot activeSnapshot = activeContext?.Snapshot;
        StrategicBattleSession session = activeContext?.Session;
        if (activeContext == null ||
            session == null ||
            compatibilityRequest == null ||
            activeSnapshot == null ||
            string.IsNullOrWhiteSpace(activeSnapshot.SnapshotId))
        {
            return StrategicBattleLaunchSnapshotSyncResult.Failed("strategic_battle_active_context_snapshot_missing");
        }

        string battleId = !string.IsNullOrWhiteSpace(session.SessionId)
            ? session.SessionId
            : activeContext.ContextId ?? "";
        if (string.IsNullOrWhiteSpace(battleId))
        {
            return StrategicBattleLaunchSnapshotSyncResult.Failed("strategic_battle_active_context_snapshot_mismatch");
        }

        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = activeSnapshot.SnapshotId,
            BattleId = battleId,
            TargetLocationId = !string.IsNullOrWhiteSpace(session.TargetLocationId)
                ? session.TargetLocationId
                : activeSnapshot.TargetLocationId ?? "",
            LocationContext = new LocationBattleContext
            {
                LocationId = !string.IsNullOrWhiteSpace(session.TargetLocationId)
                    ? session.TargetLocationId
                    : activeSnapshot.LocationContext?.LocationId ?? ""
            }
        };
        CopySkills(activeSnapshot, snapshot);
        CopyObjectiveZones(compatibilityRequest, activeSnapshot, snapshot);
        CopyNavigationContext(compatibilityRequest, activeSnapshot, snapshot);

        int groupOrdinal = 0;
        StrategicBattleParticipantReference[] participants = session.Participants
            .Where(item => item != null)
            .ToArray();
        foreach (BattleForceRequest force in compatibilityRequest.PlayerForces ?? new List<BattleForceRequest>())
        {
            if (force == null || force.Count <= 0)
            {
                continue;
            }

            if (!HasRequiredCombatStats(force))
            {
                return RejectInvalidForce(activeContext, compatibilityRequest, force, CombatStatsMissingReason);
            }

            StrategicBattleParticipantReference participant = ResolveParticipant(participants, force);
            if (participant == null)
            {
                return RejectInvalidForce(activeContext, compatibilityRequest, force, ParticipantMappingMissingReason);
            }

            BattleGroupSnapshot activeGroup = activeSnapshot.BattleGroups.FirstOrDefault(group =>
                string.Equals(group?.SourceForceId ?? "", participant.ParticipantId ?? "", StringComparison.Ordinal) ||
                string.Equals(group?.BattleGroupId ?? "", participant.ParticipantId ?? "", StringComparison.Ordinal));
            AddPlayerGroups(snapshot, compatibilityRequest, force, participant, activeGroup, ref groupOrdinal);
        }

        int enemyOrdinal = 0;
        foreach (BattleForceRequest force in compatibilityRequest.EnemyForces ?? new List<BattleForceRequest>())
        {
            if (force == null || force.Count <= 0)
            {
                continue;
            }

            if (!HasRequiredCombatStats(force))
            {
                return RejectInvalidForce(activeContext, compatibilityRequest, force, CombatStatsMissingReason);
            }

            if (string.IsNullOrWhiteSpace(force.UnitDefinitionId))
            {
                return StrategicBattleLaunchSnapshotSyncResult.Failed("strategic_battle_launch_enemy_force_invalid");
            }

            AddEnemyGroups(snapshot, compatibilityRequest, force, ref enemyOrdinal);
        }

        if (snapshot.BattleGroups.Count == 0)
        {
            return StrategicBattleLaunchSnapshotSyncResult.Failed("strategic_battle_launch_snapshot_empty");
        }

        GameLog.Info(
            nameof(StrategicBattleLaunchSnapshotSyncService),
            $"StrategicBattleLaunchSnapshotSynced context={activeContext.ContextId ?? ""} request={compatibilityRequest.RequestId ?? ""} snapshot={snapshot.SnapshotId} battle={snapshot.BattleId} groups={snapshot.BattleGroups.Count} activeSkills={activeSnapshot.SkillDefinitions.Count} launchSkills={snapshot.SkillDefinitions.Count}");
        return StrategicBattleLaunchSnapshotSyncResult.Ok(snapshot);
    }

    private static StrategicBattleLaunchSnapshotSyncResult RejectInvalidForce(
        StrategicBattleActiveContext activeContext,
        BattleStartRequest request,
        BattleForceRequest force,
        string reason)
    {
        GameLog.Warn(
            nameof(StrategicBattleLaunchSnapshotSyncService),
            $"StrategicBattleLaunchSnapshotRejected context={activeContext?.ContextId ?? ""} request={request?.RequestId ?? ""} force={force?.ForceId ?? ""} reason={reason ?? ""}");
        return StrategicBattleLaunchSnapshotSyncResult.Failed(reason);
    }

    private static bool HasRequiredCombatStats(BattleForceRequest force)
    {
        return force != null &&
               force.MaxHitPoints > 0 &&
               force.AttackDamage > 0 &&
               force.AttackRange > 0 &&
               IsPositiveFinite(force.AttackSpeed) &&
               IsPositiveFinite(force.MoveStepSeconds) &&
               IsPositiveFinite(force.AttackActionSeconds) &&
               double.IsFinite(force.AttackImpactDelaySeconds) &&
               force.AttackImpactDelaySeconds >= 0;
    }

    private static bool IsPositiveFinite(double value)
    {
        return double.IsFinite(value) && value > 0;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values?.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
    }

    private static void AddPlayerGroups(
        BattleStartSnapshot snapshot,
        BattleStartRequest request,
        BattleForceRequest force,
        StrategicBattleParticipantReference participant,
        BattleGroupSnapshot activeGroup,
        ref int groupOrdinal)
    {
        string forceId = string.IsNullOrWhiteSpace(force.ForceId)
            ? $"player_force_{groupOrdinal}"
            : force.ForceId;
        for (int index = 0; index < force.Count; index++)
        {
            BattleForcePlacementRequest placement = ResolvePlacement(force, index, activeGroup, index);
            string battleGroupId = $"{participant.ParticipantId}:{forceId}:{index}";
            snapshot.BattleGroups.Add(new BattleGroupSnapshot
            {
                BattleGroupId = battleGroupId,
                RuntimeCommanderGroupId = participant.ParticipantId,
                FactionId = string.IsNullOrWhiteSpace(force.FactionId) ? participant.FactionId ?? "" : force.FactionId,
                SourceForceId = participant.ParticipantId,
                HeroId = participant.HeroId ?? "",
                HeroDefinitionId = participant.HeroDefinitionId ?? "",
                HeroLevel = activeGroup?.HeroLevel ?? 1,
                CorpsId = participant.CorpsInstanceId ?? "",
                CorpsDefinitionId = participant.CorpsDefinitionId ?? "",
                HeroBattleUnitId = FirstNonEmpty(force.StrategicHeroBattleUnitId, activeGroup?.HeroBattleUnitId),
                CorpsBattleUnitId = FirstNonEmpty(force.StrategicCorpsBattleUnitId, activeGroup?.CorpsBattleUnitId, force.UnitDefinitionId),
                CorpsLevel = Math.Max(1, participant.CorpsLevel),
                CorpsEquipmentLevel = Math.Max(0, participant.CorpsEquipmentLevel),
                CorpsStrength = CorpsStrengthPolicy.Clamp(participant.PreBattleCorpsStrength),
                SourceLocationId = participant.SourceLocationId ?? "",
                CellX = placement.CellX,
                CellY = placement.CellY,
                CellHeight = placement.CellHeight,
                FootprintWidth = force.FootprintWidth,
                FootprintHeight = force.FootprintHeight,
                MaxHitPoints = force.MaxHitPoints,
                AttackDamage = force.AttackDamage,
                AttackRange = force.AttackRange,
                AttackSpeed = force.AttackSpeed,
                MoveStepSeconds = force.MoveStepSeconds,
                AttackActionSeconds = force.AttackActionSeconds,
                AttackImpactDelaySeconds = force.AttackImpactDelaySeconds,
                InitialCorpsCommandId = request.InitialCorpsCommandId ?? "",
                Plan = ResolveBattleGroupPlan(request.PlayerBattleGroupPlans, request.PlayerBattleGroupPlan, force, participant.ParticipantId),
                TacticalMode = BattleGroupTacticalMode.PlayerCommanded
            });
            groupOrdinal++;
        }
    }

    private static void AddEnemyGroups(
        BattleStartSnapshot snapshot,
        BattleStartRequest request,
        BattleForceRequest force,
        ref int enemyOrdinal)
    {
        string forceId = string.IsNullOrWhiteSpace(force.ForceId)
            ? $"enemy_force_{enemyOrdinal}"
            : force.ForceId;
        string sourceForceId = string.IsNullOrWhiteSpace(force.SourceId)
            ? forceId
            : force.SourceId;
        string commanderGroupId = ResolveCommanderGroupId(force, forceId);
        for (int index = 0; index < force.Count; index++)
        {
            BattleForcePlacementRequest placement = ResolvePlacement(force, index, null, index);
            string battleGroupId = $"{sourceForceId}:{forceId}:{index}";
            snapshot.BattleGroups.Add(new BattleGroupSnapshot
            {
                BattleGroupId = battleGroupId,
                RuntimeCommanderGroupId = commanderGroupId,
                FactionId = string.IsNullOrWhiteSpace(force.FactionId) ? "enemy" : force.FactionId,
                SourceForceId = sourceForceId,
                HeroId = $"{sourceForceId}:hero:{index}",
                HeroDefinitionId = force.UnitDefinitionId ?? "",
                HeroLevel = 1,
                CorpsId = $"{sourceForceId}:corps:{index}",
                CorpsDefinitionId = force.UnitDefinitionId ?? "",
                HeroBattleUnitId = force.StrategicHeroBattleUnitId ?? "",
                CorpsBattleUnitId = string.IsNullOrWhiteSpace(force.StrategicCorpsBattleUnitId)
                    ? force.UnitDefinitionId ?? ""
                    : force.StrategicCorpsBattleUnitId,
                CorpsLevel = 1,
                CorpsEquipmentLevel = 0,
                CorpsStrength = force.StrategicPreBattleCorpsStrength > 0
                    ? CorpsStrengthPolicy.Clamp(force.StrategicPreBattleCorpsStrength)
                    : CorpsStrengthPolicy.MaxStrength,
                SourceLocationId = string.IsNullOrWhiteSpace(request.TargetSiteId)
                    ? request.StrategicTargetLocationId ?? ""
                    : request.TargetSiteId,
                CellX = placement.CellX,
                CellY = placement.CellY,
                CellHeight = placement.CellHeight,
                FootprintWidth = force.FootprintWidth,
                FootprintHeight = force.FootprintHeight,
                MaxHitPoints = force.MaxHitPoints,
                AttackDamage = force.AttackDamage,
                AttackRange = force.AttackRange,
                AttackSpeed = force.AttackSpeed,
                MoveStepSeconds = force.MoveStepSeconds,
                AttackActionSeconds = force.AttackActionSeconds,
                AttackImpactDelaySeconds = force.AttackImpactDelaySeconds,
                Plan = ResolveBattleGroupPlan(request.EnemyBattleGroupPlans, request.EnemyBattleGroupPlan, force, commanderGroupId),
                TacticalIntentPlan = ResolveEnemyTacticalIntentPlan(request, force, commanderGroupId),
                TacticalMode = BattleGroupTacticalMode.EnemyActiveDefense
            });
            enemyOrdinal++;
        }
    }

    private static StrategicBattleParticipantReference ResolveParticipant(
        IEnumerable<StrategicBattleParticipantReference> participants,
        BattleForceRequest force)
    {
        if (force == null)
        {
            return null;
        }

        StrategicBattleParticipantReference[] list = participants?.ToArray() ?? Array.Empty<StrategicBattleParticipantReference>();
        StrategicBattleParticipantReference explicitMatch = list.FirstOrDefault(item =>
            !string.IsNullOrWhiteSpace(force.StrategicParticipantId) &&
            string.Equals(item.ParticipantId ?? "", force.StrategicParticipantId, StringComparison.Ordinal));
        if (explicitMatch != null)
        {
            return explicitMatch;
        }

        return list.FirstOrDefault(item =>
            (!string.IsNullOrWhiteSpace(force.StrategicHeroId) &&
             string.Equals(item.HeroId ?? "", force.StrategicHeroId, StringComparison.Ordinal)) ||
            (!string.IsNullOrWhiteSpace(force.StrategicCorpsInstanceId) &&
             string.Equals(item.CorpsInstanceId ?? "", force.StrategicCorpsInstanceId, StringComparison.Ordinal)));
    }

    private static BattleForcePlacementRequest ResolvePlacement(
        BattleForceRequest force,
        int index,
        BattleGroupSnapshot activeGroup,
        int defaultOffset)
    {
        if (force != null && index < force.PreferredPlacements.Count && force.PreferredPlacements[index] != null)
        {
            return force.PreferredPlacements[index];
        }

        return new BattleForcePlacementRequest
        {
            CellX = activeGroup?.CellX ?? 0,
            CellY = activeGroup?.CellY ?? defaultOffset,
            CellHeight = activeGroup?.CellHeight ?? 0
        };
    }

    private static BattleGroupPlanSnapshot ResolveBattleGroupPlan(
        Dictionary<string, BattleGroupPlanSnapshot> keyedPlans,
        BattleGroupPlanSnapshot fallbackPlan,
        BattleForceRequest force,
        string primaryKey)
    {
        foreach (string key in ResolvePlanKeys(force, primaryKey))
        {
            if (keyedPlans != null &&
                keyedPlans.TryGetValue(key, out BattleGroupPlanSnapshot plan) &&
                HasAuthoredPlan(plan))
            {
                return CopyPlanForGroup(primaryKey, plan);
            }
        }

        return HasAuthoredPlan(fallbackPlan)
            ? CopyPlanForGroup(primaryKey, fallbackPlan)
            : new BattleGroupPlanSnapshot { BattleGroupId = primaryKey ?? "" };
    }

    private static BattleTacticalIntentPlanSnapshot ResolveEnemyTacticalIntentPlan(
        BattleStartRequest request,
        BattleForceRequest force,
        string commanderGroupId)
    {
        foreach (string key in ResolvePlanKeys(force, commanderGroupId))
        {
            if (request?.EnemyTacticalIntentPlans != null &&
                request.EnemyTacticalIntentPlans.TryGetValue(key, out BattleTacticalIntentPlanSnapshot explicitPlan) &&
                HasAuthoredTacticalIntentPlan(explicitPlan))
            {
                return CopyTacticalIntentPlan(explicitPlan, BattleTacticalIntentPlanSources.ExplicitGroup);
            }
        }

        if (HasAuthoredTacticalIntentPlan(force?.TacticalIntentPlan))
        {
            return CopyTacticalIntentPlan(force.TacticalIntentPlan, BattleTacticalIntentPlanSources.ForceDefault);
        }

        return HasAuthoredTacticalIntentPlan(request?.EnemyTacticalIntentPlan)
            ? CopyTacticalIntentPlan(request.EnemyTacticalIntentPlan, BattleTacticalIntentPlanSources.ScenarioDefault)
            : new BattleTacticalIntentPlanSnapshot();
    }

    private static IEnumerable<string> ResolvePlanKeys(BattleForceRequest force, string primaryKey)
    {
        foreach (string key in new[]
                 {
                     primaryKey ?? "",
                     force?.CommandGroupId ?? "",
                     force != null ? BattleCommanderGroupIdentity.ResolveForceCommandKey(force) : "",
                     force?.ForceId ?? "",
                     !string.IsNullOrWhiteSpace(force?.SourceKind) && !string.IsNullOrWhiteSpace(force?.SourceId)
                         ? $"{force.SourceKind}:{force.SourceId}"
                         : "",
                     force?.SourceId ?? ""
                 })
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                yield return key;
            }
        }
    }

    private static string ResolveCommanderGroupId(BattleForceRequest force, string forceId)
    {
        string key = BattleCommanderGroupIdentity.ResolveForceCommandKey(force, forceId);
        return string.IsNullOrWhiteSpace(key)
            ? forceId ?? ""
            : key;
    }

    private static bool HasAuthoredPlan(BattleGroupPlanSnapshot plan)
    {
        return plan != null &&
               (!string.IsNullOrWhiteSpace(plan.ObjectiveZoneId) ||
                !string.IsNullOrWhiteSpace(plan.InitialFormationId) ||
                plan.HasObjectiveAnchor ||
                plan.HasInitialDestinationBeacon ||
                plan.EngagementRule != BattleEngagementRule.AttackFirst);
    }

    private static BattleGroupPlanSnapshot CopyPlanForGroup(string battleGroupId, BattleGroupPlanSnapshot source)
    {
        if (source == null)
        {
            return new BattleGroupPlanSnapshot { BattleGroupId = battleGroupId ?? "" };
        }

        return new BattleGroupPlanSnapshot
        {
            BattleGroupId = battleGroupId ?? "",
            ObjectiveZoneId = source.ObjectiveZoneId ?? "",
            EngagementRule = Enum.IsDefined(typeof(BattleEngagementRule), source.EngagementRule)
                ? source.EngagementRule
                : BattleEngagementRule.AttackFirst,
            InitialFormationId = source.InitialFormationId ?? "",
            HasObjectiveAnchor = source.HasObjectiveAnchor,
            ObjectiveCellX = source.ObjectiveCellX,
            ObjectiveCellY = source.ObjectiveCellY,
            ObjectiveCellHeight = source.ObjectiveCellHeight,
            ObjectiveWidth = source.ObjectiveWidth,
            ObjectiveHeight = source.ObjectiveHeight,
            HasInitialDestinationBeacon = source.HasInitialDestinationBeacon,
            InitialDestinationCellX = source.InitialDestinationCellX,
            InitialDestinationCellY = source.InitialDestinationCellY,
            InitialDestinationCellHeight = source.InitialDestinationCellHeight
        };
    }

    private static bool HasAuthoredTacticalIntentPlan(BattleTacticalIntentPlanSnapshot plan)
    {
        return plan != null &&
               (!string.IsNullOrWhiteSpace(plan.IntentId) ||
                !string.IsNullOrWhiteSpace(plan.PrimaryTargetSelector) ||
                (plan.SecondaryTargetSelectors?.Count ?? 0) > 0 ||
                !string.IsNullOrWhiteSpace(plan.StyleProfileId) ||
                !string.IsNullOrWhiteSpace(plan.LeashSelector) ||
                !string.IsNullOrWhiteSpace(plan.RetargetPolicyId) ||
                !string.IsNullOrWhiteSpace(plan.EngagementPolicyId) ||
                !string.IsNullOrWhiteSpace(plan.FallbackIntentId));
    }

    private static BattleTacticalIntentPlanSnapshot CopyTacticalIntentPlan(
        BattleTacticalIntentPlanSnapshot source,
        string intentSource)
    {
        if (source == null)
        {
            return new BattleTacticalIntentPlanSnapshot();
        }

        return new BattleTacticalIntentPlanSnapshot
        {
            IntentId = source.IntentId ?? "",
            PrimaryTargetSelector = source.PrimaryTargetSelector ?? "",
            SecondaryTargetSelectors = (source.SecondaryTargetSelectors ?? new List<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToList(),
            StyleProfileId = source.StyleProfileId ?? "",
            LeashSelector = source.LeashSelector ?? "",
            RetargetPolicyId = source.RetargetPolicyId ?? "",
            EngagementPolicyId = source.EngagementPolicyId ?? "",
            FallbackIntentId = source.FallbackIntentId ?? "",
            IntentSource = string.IsNullOrWhiteSpace(intentSource)
                ? source.IntentSource ?? ""
                : intentSource
        };
    }

    private static void CopySkills(BattleStartSnapshot source, BattleStartSnapshot target)
    {
        foreach (BattleSkillSnapshot skill in source?.SkillDefinitions ?? Enumerable.Empty<BattleSkillSnapshot>())
        {
            string skillDefinitionId = ResolveSkillDefinitionId(skill);
            if (skill == null || string.IsNullOrWhiteSpace(skillDefinitionId))
            {
                continue;
            }

            BattleSkillSnapshot copy = new()
            {
                SkillDefinitionId = skillDefinitionId,
                GrantedSkillId = skill.GrantedSkillId ?? "",
                LoadoutSlotId = skill.LoadoutSlotId ?? "",
                OwnerHeroId = skill.OwnerHeroId ?? "",
                OwnerBattleGroupId = skill.OwnerBattleGroupId ?? "",
                RuntimeCommanderGroupId = skill.RuntimeCommanderGroupId ?? "",
                DisplayName = skill.DisplayName ?? "",
                IconText = skill.IconText ?? "",
                IconPath = skill.IconPath ?? "",
                Tags = (skill.Tags ?? new List<string>())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => item.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .ToList(),
                CommandChannel = skill.CommandChannel,
                SkillType = skill.SkillType,
                Targeting = CloneTargeting(skill.Targeting),
                Timing = CloneTiming(skill.Timing),
                InterruptPolicy = CloneInterruptPolicy(skill.InterruptPolicy),
                Costs = CloneCosts(skill.Costs),
                Cooldown = CloneCooldown(skill.Cooldown),
                Presentation = ClonePresentation(skill.Presentation),
                TargetingMode = skill.TargetingMode,
                Range = skill.Range,
                CastSeconds = skill.CastSeconds,
                ImpactDelaySeconds = skill.ImpactDelaySeconds,
                RecoverySeconds = skill.RecoverySeconds,
                HasInterruptPolicy = skill.HasInterruptPolicy,
                CanInterruptBasicAttackWindup = skill.CanInterruptBasicAttackWindup,
                CanCancelBasicAttackRecovery = skill.CanCancelBasicAttackRecovery,
                ReleasesWithoutOccupyingCaster = skill.ReleasesWithoutOccupyingCaster
            };
            copy.CasterUnitIds.AddRange(skill.CasterUnitIds ?? Enumerable.Empty<string>());
            copy.Effects.AddRange(CloneEffects(skill.Effects));

            target.SkillDefinitions.Add(copy);
        }
    }

    private static string ResolveSkillDefinitionId(BattleSkillSnapshot skill)
    {
        return skill?.SkillDefinitionId?.Trim() ?? "";
    }

    private static BattleSkillTargetingSnapshot CloneTargeting(BattleSkillTargetingSnapshot source)
    {
        return source == null
            ? new BattleSkillTargetingSnapshot()
            : new BattleSkillTargetingSnapshot
            {
                InputFlow = source.InputFlow,
                TargetKind = source.TargetKind,
                Range = source.Range,
                RangeMetric = source.RangeMetric,
                AreaShape = source.AreaShape,
                AreaRadius = source.AreaRadius,
                DirectionMode = source.DirectionMode,
                RequiresSelectedMark = source.RequiresSelectedMark,
                RequiredMarkKind = source.RequiredMarkKind,
                LandingRadius = source.LandingRadius,
                PreviewProfileId = source.PreviewProfileId ?? ""
            };
    }

    private static BattleSkillTimingSnapshot CloneTiming(BattleSkillTimingSnapshot source)
    {
        return source == null
            ? new BattleSkillTimingSnapshot()
            : new BattleSkillTimingSnapshot
            {
                CastSeconds = source.CastSeconds,
                ImpactDelaySeconds = source.ImpactDelaySeconds,
                RecoverySeconds = source.RecoverySeconds
            };
    }

    private static BattleSkillInterruptPolicySnapshot CloneInterruptPolicy(BattleSkillInterruptPolicySnapshot source)
    {
        return source == null
            ? new BattleSkillInterruptPolicySnapshot()
            : new BattleSkillInterruptPolicySnapshot
            {
                CanInterruptBasicAttackWindup = source.CanInterruptBasicAttackWindup,
                CanCancelBasicAttackRecovery = source.CanCancelBasicAttackRecovery,
                ReleasesWithoutOccupyingCaster = source.ReleasesWithoutOccupyingCaster,
                CanInterruptActiveChannel = source.CanInterruptActiveChannel
            };
    }

    private static BattleSkillPresentationSnapshot ClonePresentation(BattleSkillPresentationSnapshot source)
    {
        return source == null
            ? new BattleSkillPresentationSnapshot()
            : new BattleSkillPresentationSnapshot
            {
                ProfileId = source.ProfileId ?? "",
                CastFxProfileId = source.CastFxProfileId ?? "",
                ImpactFxProfileId = source.ImpactFxProfileId ?? "",
                MarkFxProfileId = source.MarkFxProfileId ?? "",
                AreaFxProfileId = source.AreaFxProfileId ?? "",
                SuppressActorCastFx = source.SuppressActorCastFx,
                HoldCastAnimationDuringAction = source.HoldCastAnimationDuringAction
            };
    }

    private static List<BattleSkillCostSnapshot> CloneCosts(IEnumerable<BattleSkillCostSnapshot> costs)
    {
        return (costs ?? Enumerable.Empty<BattleSkillCostSnapshot>())
            .Select(CloneCost)
            .Where(item => item != null)
            .ToList();
    }

    private static BattleSkillCostSnapshot CloneCost(BattleSkillCostSnapshot cost)
    {
        return cost switch
        {
            ManaCostSkillCostSnapshot mana => new ManaCostSkillCostSnapshot
            {
                PoolId = mana.PoolId ?? "",
                Amount = mana.Amount,
                PayTiming = mana.PayTiming,
                RefundPolicy = mana.RefundPolicy
            },
            LimitedUseSkillCostSnapshot limitedUse => new LimitedUseSkillCostSnapshot
            {
                MaxUses = limitedUse.MaxUses,
                ConsumeTiming = limitedUse.ConsumeTiming,
                RefundPolicy = limitedUse.RefundPolicy
            },
            NoCostSkillCostSnapshot => new NoCostSkillCostSnapshot(),
            null => null,
            _ => null
        };
    }

    private static BattleSkillCooldownSnapshot CloneCooldown(BattleSkillCooldownSnapshot cooldown)
    {
        return cooldown switch
        {
            PerGrantCooldownSkillCooldownSnapshot perGrant => new PerGrantCooldownSkillCooldownSnapshot
            {
                DurationSeconds = perGrant.DurationSeconds,
                StartsOn = perGrant.StartsOn,
                SharedCooldownGroupId = perGrant.SharedCooldownGroupId ?? ""
            },
            ChargeCooldownSkillCooldownSnapshot charge => new ChargeCooldownSkillCooldownSnapshot
            {
                MaxCharges = charge.MaxCharges,
                RechargeSeconds = charge.RechargeSeconds,
                StartsFull = charge.StartsFull
            },
            NoCooldownSkillCooldownSnapshot => new NoCooldownSkillCooldownSnapshot(),
            _ => new NoCooldownSkillCooldownSnapshot()
        };
    }

    private static List<BattleSkillEffectSnapshot> CloneEffects(IEnumerable<BattleSkillEffectSnapshot> effects)
    {
        return (effects ?? Enumerable.Empty<BattleSkillEffectSnapshot>())
            .Select(CloneEffect)
            .Where(item => item != null)
            .ToList();
    }

    private static BattleSkillEffectSnapshot CloneEffect(BattleSkillEffectSnapshot effect)
    {
        BattleSkillEffectSnapshot clone = effect switch
        {
            DamageSkillEffectSnapshot damage => new DamageSkillEffectSnapshot
            {
                BaseDamage = damage.BaseDamage,
                DamageType = damage.DamageType,
                CanHitActors = damage.CanHitActors,
                CanHitWorldObjects = damage.CanHitWorldObjects
            },
            CreateMarkSkillEffectSnapshot mark => new CreateMarkSkillEffectSnapshot
            {
                MarkKind = mark.MarkKind,
                LifetimeSeconds = mark.LifetimeSeconds,
                AttachToActorWhenTargeted = mark.AttachToActorWhenTargeted,
                ReplaceExistingOwnedMark = mark.ReplaceExistingOwnedMark
            },
            TeleportToMarkSkillEffectSnapshot teleport => new TeleportToMarkSkillEffectSnapshot
            {
                RequiredMarkKind = teleport.RequiredMarkKind,
                LandingRadius = teleport.LandingRadius,
                ConsumesMark = teleport.ConsumesMark
            },
            ChanneledAreaDamageSkillEffectSnapshot channel => new ChanneledAreaDamageSkillEffectSnapshot
            {
                BaseDamage = channel.BaseDamage,
                DamageType = channel.DamageType,
                DurationSeconds = channel.DurationSeconds,
                TickIntervalSeconds = channel.TickIntervalSeconds,
                AreaShape = channel.AreaShape,
                Radius = channel.Radius,
                FollowsCaster = channel.FollowsCaster,
                UsesTargetOffset = channel.UsesTargetOffset
            },
            _ => null
        };
        if (clone != null)
        {
            clone.EffectInstancePolicy = effect.EffectInstancePolicy;
            clone.PresentationProfileId = effect.PresentationProfileId ?? "";
        }

        return clone;
    }

    private static void CopyObjectiveZones(
        BattleStartRequest request,
        BattleStartSnapshot activeSnapshot,
        BattleStartSnapshot target)
    {
        IEnumerable<BattleObjectiveZoneSnapshot> source = (request?.ObjectiveZones?.Count ?? 0) > 0
            ? request.ObjectiveZones
            : activeSnapshot?.ObjectiveZones ?? Enumerable.Empty<BattleObjectiveZoneSnapshot>();
        foreach (BattleObjectiveZoneSnapshot zone in source)
        {
            if (zone == null || string.IsNullOrWhiteSpace(zone.ObjectiveZoneId))
            {
                continue;
            }

            target.ObjectiveZones.Add(new BattleObjectiveZoneSnapshot
            {
                ObjectiveZoneId = zone.ObjectiveZoneId ?? "",
                DisplayName = zone.DisplayName ?? "",
                ObjectiveRole = zone.ObjectiveRole ?? "",
                DeploymentSide = zone.DeploymentSide ?? "",
                FactionId = zone.FactionId ?? "",
                Priority = zone.Priority,
                CellX = zone.CellX,
                CellY = zone.CellY,
                CellHeight = zone.CellHeight,
                Width = zone.Width,
                Height = zone.Height
            });
        }
    }

    private static void CopyNavigationContext(
        BattleStartRequest request,
        BattleStartSnapshot activeSnapshot,
        BattleStartSnapshot target)
    {
        BattleNavigationSnapshotBuilder.CopyRequestToLocationContext(request, target.LocationContext);
        if (target.LocationContext.NavigationTopology?.HasNodes == true ||
            activeSnapshot?.LocationContext?.NavigationTopology?.HasNodes != true)
        {
            return;
        }

        foreach (BattleNavigationSurfaceSnapshot surface in activeSnapshot.LocationContext.NavigationSurfaces)
        {
            if (surface == null)
            {
                continue;
            }

            target.LocationContext.NavigationSurfaces.Add(new BattleNavigationSurfaceSnapshot
            {
                X = surface.X,
                Y = surface.Y,
                Height = surface.Height,
                MoveCost = surface.MoveCost
            });
        }

        foreach (BattleNavigationConnectionSnapshot connection in activeSnapshot.LocationContext.NavigationConnections)
        {
            if (connection == null)
            {
                continue;
            }

            target.LocationContext.NavigationConnections.Add(new BattleNavigationConnectionSnapshot
            {
                FromX = connection.FromX,
                FromY = connection.FromY,
                FromHeight = connection.FromHeight,
                ToX = connection.ToX,
                ToY = connection.ToY,
                ToHeight = connection.ToHeight,
                MoveCost = connection.MoveCost
            });
        }

        target.LocationContext.NavigationTopology = activeSnapshot.LocationContext.NavigationTopology.Clone();
    }
}
