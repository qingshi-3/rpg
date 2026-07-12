using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Domain.Corps;
using Rpg.Domain.StrategicManagement;
using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle.Tactics;

namespace Rpg.Application.StrategicBattleBridge;

public sealed class StrategicBattleDraftSnapshotResult
{
    public bool Success { get; init; }
    public string FailureReason { get; init; } = "";
    public BattleStartSnapshot Snapshot { get; init; } = new();
    public IReadOnlyList<string> DeployedParticipantIds { get; init; } = Array.Empty<string>();

    public static StrategicBattleDraftSnapshotResult Ok(
        BattleStartSnapshot snapshot,
        IEnumerable<string> deployedParticipantIds)
    {
        return new StrategicBattleDraftSnapshotResult
        {
            Success = true,
            Snapshot = snapshot ?? new BattleStartSnapshot(),
            DeployedParticipantIds = (deployedParticipantIds ?? Enumerable.Empty<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.Ordinal)
                .ToArray()
        };
    }

    public static StrategicBattleDraftSnapshotResult Failed(string reason)
    {
        return new StrategicBattleDraftSnapshotResult
        {
            Success = false,
            FailureReason = string.IsNullOrWhiteSpace(reason)
                ? "strategic_battle_draft_snapshot_compile_failed"
                : reason
        };
    }
}

public sealed class StrategicBattleDraftSnapshotCompiler
{
    public const string DraftMissingReason = "strategic_battle_preparation_draft_missing";
    public const string DraftLineageMissingReason = "strategic_battle_draft_lineage_missing";
    public const string DraftSessionMismatchReason = "strategic_battle_draft_session_mismatch";
    public const string DraftLineageStaleReason = "strategic_battle_draft_lineage_stale";
    public const string FinalSnapshotAlreadyCompiledReason = "strategic_battle_final_snapshot_already_compiled";
    public const string ParticipantMappingMissingReason = "strategic_battle_launch_participant_mapping_missing";
    public const string ParticipantMappingAmbiguousReason = "strategic_battle_launch_participant_mapping_ambiguous";
    public const string ParticipantRoleMissingReason = "strategic_battle_launch_participant_role_missing";
    public const string ParticipantRoleDuplicateReason = "strategic_battle_launch_participant_role_duplicate";
    public const string ParticipantRoleAmbiguousReason = "strategic_battle_launch_participant_role_ambiguous";
    public const string ParticipantLineageMismatchReason = "strategic_battle_launch_participant_lineage_mismatch";
    public const string DeploymentMissingReason = "strategic_battle_launch_deployment_missing";
    public const string CombatStatsMissingReason = "strategic_battle_launch_combat_stats_missing";

    public StrategicBattleDraftSnapshotResult CompileAndCommitFinalSnapshot(
        StrategicBattleActiveContext activeContext)
    {
        StrategicBattlePreparationDraft draft = activeContext?.PreparationDraft;
        BattleStartRequest projection = StrategicBattlePreparationDraftAdapter.CreateCompatibilityProjection(draft);
        if (projection == null)
        {
            return StrategicBattleDraftSnapshotResult.Failed("strategic_battle_compatibility_projection_failed");
        }

        StrategicBattleDraftSnapshotResult result = CompileFinalSnapshot(activeContext, draft);
        if (!result.Success)
        {
            return result;
        }

        // Compilation validated every lineage and fact before this single commit.
        // The compatibility projection is a detached outbound copy.
        activeContext.Snapshot = result.Snapshot;
        activeContext.CompatibilityRequest = projection;
        activeContext.FinalizedDraftId = draft.DraftId;
        activeContext.FinalizedDraftRevision = draft.Revision;
        return result;
    }

    private StrategicBattleDraftSnapshotResult CompileFinalSnapshot(
        StrategicBattleActiveContext activeContext,
        StrategicBattlePreparationDraft draft)
    {
        BattleStartSnapshot preparationSeed = activeContext?.PreparationSeedSnapshot;
        StrategicBattleSession session = activeContext?.Session;
        if (activeContext == null ||
            session == null ||
            preparationSeed == null ||
            string.IsNullOrWhiteSpace(preparationSeed.SnapshotId))
        {
            return StrategicBattleDraftSnapshotResult.Failed("strategic_battle_active_context_snapshot_missing");
        }

        if (draft == null || !ReferenceEquals(draft, activeContext.PreparationDraft))
        {
            return StrategicBattleDraftSnapshotResult.Failed(DraftMissingReason);
        }

        if (!string.IsNullOrWhiteSpace(activeContext.FinalizedDraftId) ||
            !ReferenceEquals(activeContext.Snapshot, preparationSeed))
        {
            return StrategicBattleDraftSnapshotResult.Failed(FinalSnapshotAlreadyCompiledReason);
        }

        if (string.IsNullOrWhiteSpace(draft.DraftId) ||
            string.IsNullOrWhiteSpace(draft.SessionId) ||
            draft.Revision <= 0 ||
            string.IsNullOrWhiteSpace(activeContext.PreparationDraftId) ||
            activeContext.PreparationDraftRevision <= 0)
        {
            return StrategicBattleDraftSnapshotResult.Failed(DraftLineageMissingReason);
        }

        if (!string.Equals(draft.SessionId, session.SessionId ?? "", StringComparison.Ordinal) ||
            !string.Equals(draft.StrategicBattleSessionId ?? "", session.SessionId ?? "", StringComparison.Ordinal) ||
            !string.Equals(draft.ContextId ?? "", session.SessionId ?? "", StringComparison.Ordinal))
        {
            return StrategicBattleDraftSnapshotResult.Failed(DraftSessionMismatchReason);
        }

        if (!string.Equals(draft.DraftId, activeContext.PreparationDraftId, StringComparison.Ordinal) ||
            draft.Revision != activeContext.PreparationDraftRevision)
        {
            return StrategicBattleDraftSnapshotResult.Failed(DraftLineageStaleReason);
        }

        string battleId = !string.IsNullOrWhiteSpace(session.SessionId)
            ? session.SessionId
            : "";
        if (string.IsNullOrWhiteSpace(battleId) ||
            string.IsNullOrWhiteSpace(session.TargetLocationId) ||
            !string.Equals(activeContext.ContextId ?? "", battleId, StringComparison.Ordinal) ||
            !string.Equals(preparationSeed.BattleId ?? "", battleId, StringComparison.Ordinal) ||
            !string.Equals(preparationSeed.StrategicBattleSessionId ?? "", battleId, StringComparison.Ordinal))
        {
            return StrategicBattleDraftSnapshotResult.Failed("strategic_battle_active_context_snapshot_mismatch");
        }

        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = preparationSeed.SnapshotId,
            BattleId = battleId,
            StrategicBattleSessionId = session.SessionId,
            StrategicBattleDraftId = draft.DraftId,
            StrategicBattleDraftRevision = draft.Revision,
            TargetLocationId = session.TargetLocationId,
            LocationContext = new LocationBattleContext
            {
                LocationId = session.TargetLocationId
            }
        };
        CopySkills(preparationSeed, snapshot);
        CopyObjectiveZones(draft, snapshot);
        CopyNavigationContext(draft, snapshot);

        HashSet<string> deployedParticipantIds = new(StringComparer.Ordinal);
        StrategicBattleParticipantReference[] participants = session.Participants
            .Where(item => item != null)
            .ToArray();
        Dictionary<string, List<MappedPlayerForce>> playerForcesByParticipant = new(StringComparer.Ordinal);
        foreach (BattleForceRequest force in draft.PlayerForces ?? new List<BattleForceRequest>())
        {
            if (force == null || force.Count <= 0)
            {
                continue;
            }

            if (!HasRequiredCombatStats(force))
            {
                return RejectInvalidForce(activeContext, draft, force, CombatStatsMissingReason);
            }

            StrategicBattleParticipantReference participant = ResolveParticipant(participants, force, out string mappingFailureReason);
            if (participant == null)
            {
                return RejectInvalidForce(activeContext, draft, force, mappingFailureReason);
            }

            BattleGroupSnapshot[] activeGroups = preparationSeed.BattleGroups.Where(group =>
                string.Equals(group?.SourceForceId ?? "", participant.ParticipantId ?? "", StringComparison.Ordinal) ||
                string.Equals(group?.BattleGroupId ?? "", participant.ParticipantId ?? "", StringComparison.Ordinal))
                .ToArray();
            if (activeGroups.Length != 1)
            {
                return RejectInvalidForce(
                    activeContext,
                    draft,
                    force,
                    activeGroups.Length == 0 ? ParticipantMappingMissingReason : ParticipantMappingAmbiguousReason);
            }

            if (!TryResolvePlayerForceRole(force, activeGroups[0], out StrategicPlayerForceRole role, out string roleFailureReason))
            {
                return RejectInvalidForce(activeContext, draft, force, roleFailureReason);
            }

            string participantId = participant.ParticipantId ?? "";
            if (!playerForcesByParticipant.TryGetValue(participantId, out List<MappedPlayerForce> mappedForces))
            {
                mappedForces = new List<MappedPlayerForce>();
                playerForcesByParticipant.Add(participantId, mappedForces);
            }

            mappedForces.Add(new MappedPlayerForce(force, activeGroups[0], role));
        }

        foreach (StrategicBattleParticipantReference participant in participants)
        {
            string participantId = participant.ParticipantId ?? "";
            if (!playerForcesByParticipant.TryGetValue(participantId, out List<MappedPlayerForce> mappedForces))
            {
                continue;
            }

            MappedPlayerForce[] heroRows = mappedForces
                .Where(item => item.Role == StrategicPlayerForceRole.Hero)
                .ToArray();
            MappedPlayerForce[] corpsRows = mappedForces
                .Where(item => item.Role == StrategicPlayerForceRole.Corps)
                .ToArray();
            if (heroRows.Length == 0 || corpsRows.Length == 0)
            {
                return RejectInvalidForce(activeContext, draft, mappedForces[0].Force, ParticipantRoleMissingReason);
            }

            if (heroRows.Length != 1 || corpsRows.Length != 1 || mappedForces.Count != 2)
            {
                return RejectInvalidForce(activeContext, draft, mappedForces[0].Force, ParticipantRoleDuplicateReason);
            }

            if (HasContradictoryFaction(heroRows[0].Force, corpsRows[0].Force))
            {
                return RejectInvalidForce(activeContext, draft, mappedForces[0].Force, ParticipantRoleAmbiguousReason);
            }

            if (!HasExactParticipantLineage(heroRows[0].Force, participant) ||
                !HasExactParticipantLineage(corpsRows[0].Force, participant))
            {
                return RejectInvalidForce(activeContext, draft, mappedForces[0].Force, ParticipantLineageMismatchReason);
            }

            if (!TryResolveDraftPlacement(corpsRows[0].Force, out BattleForcePlacementRequest placement))
            {
                return RejectInvalidForce(activeContext, draft, corpsRows[0].Force, DeploymentMissingReason);
            }

            // Draft row counts describe visible force representation only. The
            // participant lineage owns exactly one Runtime group and commander.
            AddPlayerGroup(
                snapshot,
                draft,
                heroRows[0].Force,
                corpsRows[0].Force,
                participant,
                heroRows[0].ActiveGroup,
                placement);
            deployedParticipantIds.Add(participantId);
        }

        int enemyOrdinal = 0;
        foreach (BattleForceRequest force in draft.EnemyForces ?? new List<BattleForceRequest>())
        {
            if (force == null || force.Count <= 0)
            {
                continue;
            }

            if (!HasRequiredCombatStats(force))
            {
                return RejectInvalidForce(activeContext, draft, force, CombatStatsMissingReason);
            }

            if (string.IsNullOrWhiteSpace(force.UnitDefinitionId))
            {
                return StrategicBattleDraftSnapshotResult.Failed("strategic_battle_launch_enemy_force_invalid");
            }

            AddEnemyGroups(snapshot, draft, force, ref enemyOrdinal);
        }

        if (snapshot.BattleGroups.Count == 0 || deployedParticipantIds.Count == 0)
        {
            return StrategicBattleDraftSnapshotResult.Failed("strategic_battle_launch_snapshot_empty");
        }

        // Freeze the accepted deployment subset only after every Draft fact has
        // validated and the complete final Snapshot exists.
        foreach (StrategicBattleParticipantReference participant in participants)
        {
            participant.Role = deployedParticipantIds.Contains(participant.ParticipantId ?? "")
                ? StrategicBattleParticipantRole.Deployed
                : StrategicBattleParticipantRole.Reserve;
        }

        GameLog.Info(
            nameof(StrategicBattleDraftSnapshotCompiler),
            $"StrategicBattleDraftSnapshotCompiled context={activeContext.ContextId ?? ""} draft={draft.DraftId} revision={draft.Revision} snapshot={snapshot.SnapshotId} battle={snapshot.BattleId} groups={snapshot.BattleGroups.Count} seedSkills={preparationSeed.SkillDefinitions.Count} launchSkills={snapshot.SkillDefinitions.Count}");
        return StrategicBattleDraftSnapshotResult.Ok(snapshot, deployedParticipantIds);
    }

    private static StrategicBattleDraftSnapshotResult RejectInvalidForce(
        StrategicBattleActiveContext activeContext,
        StrategicBattlePreparationDraft draft,
        BattleForceRequest force,
        string reason)
    {
        GameLog.Warn(
            nameof(StrategicBattleDraftSnapshotCompiler),
            $"StrategicBattleDraftSnapshotRejected context={activeContext?.ContextId ?? ""} draft={draft?.DraftId ?? ""} force={force?.ForceId ?? ""} reason={reason ?? ""}");
        return StrategicBattleDraftSnapshotResult.Failed(reason);
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

    private static void AddPlayerGroup(
        BattleStartSnapshot snapshot,
        StrategicBattlePreparationDraft draft,
        BattleForceRequest heroForce,
        BattleForceRequest corpsForce,
        StrategicBattleParticipantReference participant,
        BattleGroupSnapshot activeGroup,
        BattleForcePlacementRequest placement)
    {
        snapshot.BattleGroups.Add(new BattleGroupSnapshot
        {
            BattleGroupId = corpsForce.StrategicParticipantId ?? "",
            RuntimeCommanderGroupId = corpsForce.StrategicParticipantId ?? "",
            FactionId = corpsForce.FactionId ?? "",
            SourceForceId = corpsForce.StrategicParticipantId ?? "",
            HeroId = heroForce.StrategicHeroId ?? "",
            HeroDefinitionId = heroForce.StrategicHeroDefinitionId ?? "",
            HeroLevel = activeGroup?.HeroLevel ?? 1,
            CorpsId = corpsForce.StrategicCorpsInstanceId ?? "",
            CorpsDefinitionId = corpsForce.StrategicCorpsDefinitionId ?? "",
            HeroBattleUnitId = FirstNonEmpty(heroForce.UnitDefinitionId, heroForce.StrategicHeroBattleUnitId, activeGroup?.HeroBattleUnitId),
            CorpsBattleUnitId = FirstNonEmpty(corpsForce.UnitDefinitionId, corpsForce.StrategicCorpsBattleUnitId, activeGroup?.CorpsBattleUnitId),
            CorpsLevel = Math.Max(1, participant.CorpsLevel),
            CorpsEquipmentLevel = Math.Max(0, participant.CorpsEquipmentLevel),
            CorpsStrength = CorpsStrengthPolicy.Clamp(corpsForce.StrategicPreBattleCorpsStrength),
            SourceLocationId = corpsForce.StrategicSourceLocationId ?? "",
            CellX = placement.CellX,
            CellY = placement.CellY,
            CellHeight = placement.CellHeight,
            FootprintWidth = corpsForce.FootprintWidth,
            FootprintHeight = corpsForce.FootprintHeight,
            MaxHitPoints = corpsForce.MaxHitPoints,
            AttackDamage = corpsForce.AttackDamage,
            AttackRange = corpsForce.AttackRange,
            AttackSpeed = corpsForce.AttackSpeed,
            MoveStepSeconds = corpsForce.MoveStepSeconds,
            AttackActionSeconds = corpsForce.AttackActionSeconds,
            AttackImpactDelaySeconds = corpsForce.AttackImpactDelaySeconds,
            InitialCorpsCommandId = draft.InitialCorpsCommandId ?? "",
            Plan = ResolveBattleGroupPlan(draft.PlayerBattleGroupPlans, draft.PlayerBattleGroupPlan, corpsForce, participant.ParticipantId),
            TacticalMode = BattleGroupTacticalMode.PlayerCommanded
        });
    }

    private static void AddEnemyGroups(
        BattleStartSnapshot snapshot,
        StrategicBattlePreparationDraft draft,
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
                SourceLocationId = string.IsNullOrWhiteSpace(draft.TargetSiteId)
                    ? draft.StrategicTargetLocationId ?? ""
                    : draft.TargetSiteId,
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
                Plan = ResolveBattleGroupPlan(draft.EnemyBattleGroupPlans, draft.EnemyBattleGroupPlan, force, commanderGroupId),
                TacticalIntentPlan = ResolveEnemyTacticalIntentPlan(draft, force, commanderGroupId),
                TacticalMode = BattleGroupTacticalMode.EnemyActiveDefense
            });
            enemyOrdinal++;
        }
    }

    private static StrategicBattleParticipantReference ResolveParticipant(
        IEnumerable<StrategicBattleParticipantReference> participants,
        BattleForceRequest force,
        out string failureReason)
    {
        failureReason = ParticipantMappingMissingReason;
        if (force == null)
        {
            return null;
        }

        StrategicBattleParticipantReference[] list = participants?.ToArray() ?? Array.Empty<StrategicBattleParticipantReference>();
        bool hasIdentity = !string.IsNullOrWhiteSpace(force.StrategicParticipantId) ||
                           !string.IsNullOrWhiteSpace(force.StrategicHeroId) ||
                           !string.IsNullOrWhiteSpace(force.StrategicCorpsInstanceId);
        StrategicBattleParticipantReference[] matches = list.Where(item =>
                (!string.IsNullOrWhiteSpace(force.StrategicParticipantId) &&
                 string.Equals(item.ParticipantId ?? "", force.StrategicParticipantId, StringComparison.Ordinal)) ||
                (!string.IsNullOrWhiteSpace(force.StrategicHeroId) &&
                 string.Equals(item.HeroId ?? "", force.StrategicHeroId, StringComparison.Ordinal)) ||
                (!string.IsNullOrWhiteSpace(force.StrategicCorpsInstanceId) &&
                 string.Equals(item.CorpsInstanceId ?? "", force.StrategicCorpsInstanceId, StringComparison.Ordinal)))
            .Distinct()
            .ToArray();
        if (!hasIdentity || matches.Length == 0)
        {
            return null;
        }

        StrategicBattleParticipantReference match = matches.Length == 1 ? matches[0] : null;
        if (match == null ||
            (!string.IsNullOrWhiteSpace(force.StrategicParticipantId) &&
             !string.Equals(match.ParticipantId ?? "", force.StrategicParticipantId, StringComparison.Ordinal)) ||
            (!string.IsNullOrWhiteSpace(force.StrategicHeroId) &&
             !string.Equals(match.HeroId ?? "", force.StrategicHeroId, StringComparison.Ordinal)) ||
            (!string.IsNullOrWhiteSpace(force.StrategicCorpsInstanceId) &&
             !string.Equals(match.CorpsInstanceId ?? "", force.StrategicCorpsInstanceId, StringComparison.Ordinal)))
        {
            failureReason = ParticipantMappingAmbiguousReason;
            return null;
        }

        failureReason = "";
        return match;
    }

    private static bool TryResolvePlayerForceRole(
        BattleForceRequest force,
        BattleGroupSnapshot activeGroup,
        out StrategicPlayerForceRole role,
        out string failureReason)
    {
        role = StrategicPlayerForceRole.Unknown;
        failureReason = ParticipantRoleMissingReason;
        string unitDefinitionId = force?.UnitDefinitionId ?? "";
        string heroBattleUnitId = FirstNonEmpty(force?.StrategicHeroBattleUnitId, activeGroup?.HeroBattleUnitId);
        string corpsBattleUnitId = FirstNonEmpty(force?.StrategicCorpsBattleUnitId, activeGroup?.CorpsBattleUnitId);
        bool isHero = !string.IsNullOrWhiteSpace(heroBattleUnitId) &&
                      string.Equals(unitDefinitionId, heroBattleUnitId, StringComparison.Ordinal);
        bool isCorps = !string.IsNullOrWhiteSpace(corpsBattleUnitId) &&
                       string.Equals(unitDefinitionId, corpsBattleUnitId, StringComparison.Ordinal);
        if (isHero == isCorps)
        {
            failureReason = isHero ? ParticipantRoleAmbiguousReason : ParticipantRoleMissingReason;
            return false;
        }

        role = isHero ? StrategicPlayerForceRole.Hero : StrategicPlayerForceRole.Corps;
        failureReason = "";
        return true;
    }

    private static bool HasContradictoryFaction(
        BattleForceRequest heroForce,
        BattleForceRequest corpsForce)
    {
        string[] factions = new[] { heroForce?.FactionId, corpsForce?.FactionId }
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return factions.Length > 1;
    }

    private static bool HasExactParticipantLineage(
        BattleForceRequest force,
        StrategicBattleParticipantReference participant)
    {
        return force != null &&
               participant != null &&
               string.Equals(force.StrategicParticipantId ?? "", participant.ParticipantId ?? "", StringComparison.Ordinal) &&
               string.Equals(force.StrategicHeroId ?? "", participant.HeroId ?? "", StringComparison.Ordinal) &&
               string.Equals(force.StrategicHeroDefinitionId ?? "", participant.HeroDefinitionId ?? "", StringComparison.Ordinal) &&
               string.Equals(force.StrategicCorpsInstanceId ?? "", participant.CorpsInstanceId ?? "", StringComparison.Ordinal) &&
               string.Equals(force.StrategicCorpsDefinitionId ?? "", participant.CorpsDefinitionId ?? "", StringComparison.Ordinal) &&
               string.Equals(force.StrategicSourceLocationId ?? "", participant.SourceLocationId ?? "", StringComparison.Ordinal) &&
               string.Equals(force.FactionId ?? "", participant.FactionId ?? "", StringComparison.Ordinal) &&
               force.StrategicPreBattleCorpsStrength == participant.PreBattleCorpsStrength;
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

    private static bool TryResolveDraftPlacement(
        BattleForceRequest force,
        out BattleForcePlacementRequest placement)
    {
        placement = force?.PreferredPlacements?.FirstOrDefault(item => item != null);
        return placement != null;
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
        StrategicBattlePreparationDraft draft,
        BattleForceRequest force,
        string commanderGroupId)
    {
        foreach (string key in ResolvePlanKeys(force, commanderGroupId))
        {
            if (draft?.EnemyTacticalIntentPlans != null &&
                draft.EnemyTacticalIntentPlans.TryGetValue(key, out BattleTacticalIntentPlanSnapshot explicitPlan) &&
                HasAuthoredTacticalIntentPlan(explicitPlan))
            {
                return CopyTacticalIntentPlan(explicitPlan, BattleTacticalIntentPlanSources.ExplicitGroup);
            }
        }

        if (HasAuthoredTacticalIntentPlan(force?.TacticalIntentPlan))
        {
            return CopyTacticalIntentPlan(force.TacticalIntentPlan, BattleTacticalIntentPlanSources.ForceDefault);
        }

        return HasAuthoredTacticalIntentPlan(draft?.EnemyTacticalIntentPlan)
            ? CopyTacticalIntentPlan(draft.EnemyTacticalIntentPlan, BattleTacticalIntentPlanSources.ScenarioDefault)
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
        StrategicBattlePreparationDraft draft,
        BattleStartSnapshot target)
    {
        foreach (BattleObjectiveZoneSnapshot zone in draft?.ObjectiveZones ?? Enumerable.Empty<BattleObjectiveZoneSnapshot>())
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
        StrategicBattlePreparationDraft draft,
        BattleStartSnapshot target)
    {
        BattleNavigationSnapshotBuilder.CopyRequestToLocationContext(draft, target.LocationContext);
    }

    private enum StrategicPlayerForceRole
    {
        Unknown = 0,
        Hero = 1,
        Corps = 2
    }

    private sealed record MappedPlayerForce(
        BattleForceRequest Force,
        BattleGroupSnapshot ActiveGroup,
        StrategicPlayerForceRole Role);
}
