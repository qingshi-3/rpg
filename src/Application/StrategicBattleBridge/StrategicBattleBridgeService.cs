using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Reports;
using Rpg.Application.Battle.Settlement;
using Rpg.Application.Battle.Snapshots;
using Rpg.Application.StrategicManagement;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.BattleGroups;
using Rpg.Domain.Corps;
using Rpg.Domain.Heroes;
using Rpg.Domain.StrategicManagement;
using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Results;

namespace Rpg.Application.StrategicBattleBridge;

public sealed class StrategicBattleBridgeService
{
    public const string ParticipantActorMappingMissingReason = "strategic_battle_participant_actor_mapping_missing";
    public const string ParticipantActorMappingAmbiguousReason = "strategic_battle_participant_actor_mapping_ambiguous";
    public const string ParticipantCasualtyBasisInvalidReason = "strategic_battle_participant_casualty_basis_invalid";

    private readonly StrategicManagementDefinitionSet _definitions;
    private readonly BattleSnapshotBuilder _snapshotBuilder = new();

    public StrategicBattleBridgeService(StrategicManagementDefinitionSet definitions)
    {
        _definitions = definitions ?? new StrategicManagementDefinitionSet();
    }

    public StrategicBattleSessionResult CreateSession(
        StrategicManagementState state,
        string expeditionId,
        string returnScenePath,
        string fallbackSiteScenePath)
    {
        if (state == null ||
            !state.Expeditions.TryGetValue(expeditionId ?? "", out StrategicExpeditionState expedition))
        {
            return StrategicBattleSessionResult.Failed(StrategicFailureReasons.MissingExpedition);
        }

        if (expedition.Status != StrategicExpeditionStatus.Moving)
        {
            return StrategicBattleSessionResult.Failed(StrategicFailureReasons.ExpeditionNotCommandable);
        }

        if (expedition.Intent != StrategicExpeditionIntent.AssaultLocation)
        {
            return StrategicBattleSessionResult.Failed(StrategicFailureReasons.UnsupportedExpeditionIntent);
        }

        if (!_definitions.Locations.TryGetValue(expedition.TargetLocationId ?? "", out StrategicLocationDefinition targetDefinition) ||
            !state.Locations.TryGetValue(expedition.TargetLocationId ?? "", out StrategicLocationState targetLocation))
        {
            return StrategicBattleSessionResult.Failed(StrategicFailureReasons.MissingLocation);
        }

        if (string.IsNullOrWhiteSpace(targetDefinition.BattleMapDefinitionId) ||
            string.IsNullOrWhiteSpace(targetDefinition.BattleEncounterId))
        {
            return StrategicBattleSessionResult.Failed(StrategicFailureReasons.MissingBattleEntryMetadata);
        }

        List<StrategicBattleParticipantReference> participants = BuildParticipantReferences(state, expedition);
        if (participants.Count == 0)
        {
            return StrategicBattleSessionResult.Failed(GetMissingParticipantFailureReason(state, expedition));
        }

        StrategicBattleSession session = new()
        {
            SessionId = $"strategic_battle:{expedition.ExpeditionId}:{Guid.NewGuid():N}",
            ExpeditionId = expedition.ExpeditionId,
            SourceLocationId = expedition.SourceLocationId,
            TargetLocationId = expedition.TargetLocationId,
            AttackerFactionId = expedition.FactionId,
            DefenderFactionId = targetLocation.OwnerFactionId,
            BattleKind = BattleKind.AssaultSite,
            EncounterId = targetDefinition.BattleEncounterId,
            MapDefinitionId = targetDefinition.BattleMapDefinitionId,
            BattleObjectiveId = targetDefinition.BattleObjectiveId,
            ReturnScenePath = returnScenePath ?? "",
            SiteScenePath = string.IsNullOrWhiteSpace(targetDefinition.BattleScenePath)
                ? fallbackSiteScenePath ?? ""
                : targetDefinition.BattleScenePath
        };
        session.Participants.AddRange(participants);

        GameLog.Info(
            nameof(StrategicBattleBridgeService),
            $"StrategicBattleSessionCreated session={session.SessionId} expedition={session.ExpeditionId} target={session.TargetLocationId} participants={session.Participants.Count}");
        return StrategicBattleSessionResult.Ok(session);
    }

    public StrategicBattleSnapshotResult CompilePreparationSeedSnapshot(
        StrategicManagementState state,
        StrategicBattleSession session)
    {
        if (state == null || session == null || session.Participants.Count == 0)
        {
            return StrategicBattleSnapshotResult.Failed(StrategicFailureReasons.MissingBattleResultSummary);
        }

        List<BattleGroupState> groups = new();
        Dictionary<string, HeroState> heroes = new(StringComparer.Ordinal);
        Dictionary<string, CorpsState> corps = new(StringComparer.Ordinal);
        foreach (StrategicBattleParticipantReference participant in session.Participants.Where(item => item?.Role != StrategicBattleParticipantRole.Reserve))
        {
            if (participant == null ||
                string.IsNullOrWhiteSpace(participant.HeroId) ||
                string.IsNullOrWhiteSpace(participant.CorpsInstanceId))
            {
                return StrategicBattleSnapshotResult.Failed(StrategicFailureReasons.MissingBattleResultSummary);
            }

            groups.Add(new BattleGroupState
            {
                BattleGroupId = participant.ParticipantId,
                HeroId = participant.HeroId,
                CorpsId = participant.CorpsInstanceId,
                CurrentLocationId = participant.SourceLocationId,
                Status = BattleGroupStatus.Stationed
            });
            heroes[participant.HeroId] = new HeroState
            {
                HeroId = participant.HeroId,
                HeroDefinitionId = participant.HeroDefinitionId,
                OwnerFactionId = participant.FactionId,
                Level = 1
            };
            corps[participant.CorpsInstanceId] = new CorpsState
            {
                CorpsId = participant.CorpsInstanceId,
                CorpsDefinitionId = participant.CorpsDefinitionId,
                Level = Math.Max(1, participant.CorpsLevel),
                EquipmentLevel = Math.Max(0, participant.CorpsEquipmentLevel),
                CorpsStrength = ClampStrength(participant.PreBattleCorpsStrength)
            };
        }

        BattleStartSnapshot snapshot = _snapshotBuilder.Build(
            $"snapshot:{session.SessionId}",
            session.SessionId,
            session.TargetLocationId,
            groups,
            heroes,
            corps);
        snapshot.StrategicBattleSessionId = session.SessionId;
        foreach (BattleGroupSnapshot group in snapshot.BattleGroups)
        {
            StrategicBattleParticipantReference participant = session.Participants.FirstOrDefault(item =>
                string.Equals(item.ParticipantId, group.BattleGroupId, StringComparison.Ordinal));
            if (participant == null)
            {
                continue;
            }

            group.FactionId = participant.FactionId;
            group.SourceForceId = participant.ParticipantId;
            group.RuntimeCommanderGroupId = participant.ParticipantId;
            group.HeroBattleUnitId = GetHeroBattleUnitId(participant);
            group.CorpsBattleUnitId = GetCorpsBattleUnitId(participant);
        }

        _snapshotBuilder.RecompileSkillDefinitions(snapshot);
        GameLog.Info(
            nameof(StrategicBattleBridgeService),
            $"StrategicBattleSkillSnapshotCompiled session={session.SessionId} snapshot={snapshot.SnapshotId} groups={snapshot.BattleGroups.Count} skills={snapshot.SkillDefinitions.Count}");
        return StrategicBattleSnapshotResult.Ok(snapshot);
    }

    public StrategicBattleActiveContextResult CreateActiveContext(
        StrategicManagementState state,
        StrategicBattleSession session,
        BattleStartRequest preparationSeed)
    {
        if (state == null || session == null || preparationSeed == null)
        {
            return StrategicBattleActiveContextResult.Failed(StrategicFailureReasons.MissingBattleResultSummary);
        }

        StrategicBattlePreparationDraft draft = StrategicBattlePreparationDraftAdapter.Create(session, preparationSeed);
        if (draft == null)
        {
            return StrategicBattleActiveContextResult.Failed("strategic_battle_preparation_draft_missing");
        }

        AttachSessionToDraft(session, draft);
        StrategicBattleSnapshotResult snapshotResult = CompilePreparationSeedSnapshot(state, session);
        if (!snapshotResult.Success)
        {
            return StrategicBattleActiveContextResult.Failed(snapshotResult.FailureReason);
        }

        if (string.IsNullOrWhiteSpace(draft.SiteScenePath))
        {
            draft.SiteScenePath = session.SiteScenePath;
        }

        if (string.IsNullOrWhiteSpace(draft.ReturnScenePath))
        {
            draft.ReturnScenePath = session.ReturnScenePath;
        }

        StrategicBattleActiveContext context = new()
        {
            ContextId = session.SessionId,
            ScenePath = string.IsNullOrWhiteSpace(session.SiteScenePath)
                ? draft.SiteScenePath
                : session.SiteScenePath,
            ReturnScenePath = string.IsNullOrWhiteSpace(session.ReturnScenePath)
                ? draft.ReturnScenePath
                : session.ReturnScenePath,
            Session = session,
            PreparationDraft = draft,
            PreparationSeedSnapshot = snapshotResult.Snapshot,
            PreparationDraftId = draft.DraftId,
            PreparationDraftRevision = draft.Revision,
            Snapshot = snapshotResult.Snapshot
        };

        GameLog.Info(
            nameof(StrategicBattleBridgeService),
            $"StrategicBattleActiveContextCreated context={context.ContextId} expedition={session.ExpeditionId} target={session.TargetLocationId} snapshot={context.Snapshot.SnapshotId} skills={context.Snapshot.SkillDefinitions.Count} scene={context.ScenePath}");
        return StrategicBattleActiveContextResult.Ok(context);
    }

    public void AttachSessionToLegacyRequest(StrategicBattleSession session, BattleStartRequest request)
    {
        AttachSessionProjection(session, request);
    }

    private void AttachSessionToDraft(StrategicBattleSession session, StrategicBattlePreparationDraft draft)
    {
        AttachSessionProjection(session, draft);
        // Draft lineage is Bridge-owned; a legacy seed cannot retain a competing context identity.
        draft.ContextId = session?.SessionId ?? "";
    }

    private void AttachSessionProjection(StrategicBattleSession session, BattleStartRequest request)
    {
        if (session == null || request == null)
        {
            return;
        }

        request.StrategicBattleSessionId = session.SessionId;
        request.StrategicExpeditionId = session.ExpeditionId;
        request.StrategicSourceLocationId = session.SourceLocationId;
        request.StrategicTargetLocationId = session.TargetLocationId;
        request.ContextId = string.IsNullOrWhiteSpace(request.ContextId) ? session.SessionId : request.ContextId;
        request.EncounterId = string.IsNullOrWhiteSpace(request.EncounterId) ? session.EncounterId : request.EncounterId;
        request.MapDefinitionId = string.IsNullOrWhiteSpace(request.MapDefinitionId) ? session.MapDefinitionId : request.MapDefinitionId;
        if (!string.IsNullOrWhiteSpace(session.BattleObjectiveId) && !request.ObjectiveIds.Contains(session.BattleObjectiveId))
        {
            request.ObjectiveIds.Add(session.BattleObjectiveId);
        }

        if (session.Participants.Count == 0)
        {
            return;
        }

        foreach (BattleForceRequest force in request.PlayerForces ?? new List<BattleForceRequest>())
        {
            StrategicBattleParticipantReference participant = ResolveParticipantForForce(session, force);
            if (participant == null)
            {
                continue;
            }

            force.StrategicParticipantId = participant.ParticipantId;
            force.StrategicHeroId = participant.HeroId;
            force.StrategicHeroDefinitionId = participant.HeroDefinitionId;
            force.StrategicHeroBattleUnitId = GetHeroBattleUnitId(participant);
            force.StrategicCorpsInstanceId = participant.CorpsInstanceId;
            force.StrategicCorpsDefinitionId = participant.CorpsDefinitionId;
            force.StrategicCorpsBattleUnitId = GetCorpsBattleUnitId(participant);
            force.StrategicSourceLocationId = participant.SourceLocationId;
            force.StrategicPreBattleCorpsStrength = participant.PreBattleCorpsStrength;
            force.FactionId = string.IsNullOrWhiteSpace(force.FactionId)
                ? participant.FactionId
                : force.FactionId;
        }
    }

    public StrategicBattleResultSummary BuildResultSummary(StrategicBattleActiveContext context)
    {
        StrategicBattleSession session = context?.Session;
        BattleStartSnapshot snapshot = context?.Snapshot;
        BattleRuntimeSessionResult runtimeResult = ResolveRuntimeResult(context);
        BattleOutcomeResult outcome = runtimeResult?.Outcome;
        string failureReason = GetActiveContextFailureReason(context);
        BattleOutcome mappedOutcome = string.IsNullOrWhiteSpace(failureReason)
            ? MapRuntimeOutcome(outcome)
            : BattleOutcome.None;
        StrategicBattleResultSummary summary = new()
        {
            SessionId = session?.SessionId ?? context?.ContextId ?? "",
            SnapshotId = snapshot?.SnapshotId ?? outcome?.SnapshotId ?? "",
            ExpeditionId = session?.ExpeditionId ?? "",
            TargetLocationId = session?.TargetLocationId ?? "",
            Outcome = mappedOutcome,
            ObjectiveSucceeded = mappedOutcome == BattleOutcome.Victory
        };

        foreach (StrategicBattleParticipantReference participant in session?.Participants ?? new List<StrategicBattleParticipantReference>())
        {
            if (participant == null)
            {
                continue;
            }

            summary.ParticipantDispositions.Add(new StrategicBattleParticipantDisposition
            {
                ParticipantId = participant.ParticipantId,
                HeroId = participant.HeroId,
                CorpsInstanceId = participant.CorpsInstanceId,
                RollbackStationLocationId = participant.RollbackStationLocationId,
                Role = participant.Role
            });
        }

        if (!string.IsNullOrWhiteSpace(failureReason))
        {
            GameLog.Warn(
                nameof(StrategicBattleBridgeService),
                $"StrategicBattleResultSummaryRejected session={summary.SessionId} snapshot={summary.SnapshotId} reason={failureReason}");
            return summary;
        }

        foreach (StrategicBattleParticipantReference participant in session.Participants.Where(item => item?.Role == StrategicBattleParticipantRole.Deployed))
        {
            if (participant == null || string.IsNullOrWhiteSpace(participant.CorpsInstanceId))
            {
                continue;
            }

            if (!TryResolveParticipantRuntimeOutcome(
                    context,
                    participant,
                    out BattleGroupSnapshot group,
                    out _,
                    out BattleActorOutcome corpsOutcome,
                    out string mappingFailureReason))
            {
                GameLog.Warn(
                    nameof(StrategicBattleBridgeService),
                    $"StrategicBattleParticipantSummarySkipped session={summary.SessionId} participant={participant.ParticipantId} reason={mappingFailureReason}");
                continue;
            }

            int preBattleStrength = ClampStrength(participant.PreBattleCorpsStrength);
            // Strategic casualties scale the frozen 0-100 corps basis from the
            // authoritative corps actor only; hero rows and visible counts never enter.
            double survivalFraction = Math.Clamp(
                corpsOutcome.RemainingHitPoints / (double)group.MaxHitPoints,
                0.0,
                1.0);
            int remainingStrength = (int)Math.Round(preBattleStrength * survivalFraction);

            summary.Participants.Add(new StrategicBattleParticipantResult
            {
                HeroId = participant.HeroId,
                CorpsInstanceId = participant.CorpsInstanceId,
                RemainingCorpsStrength = remainingStrength
            });
        }

        return summary;
    }

    public static string GetActiveContextFailureReason(StrategicBattleActiveContext context)
    {
        BattleRuntimeSessionResult runtimeResult = ResolveRuntimeResult(context);
        BattleOutcomeResult outcome = runtimeResult?.Outcome;
        BattleEventStream eventStream = runtimeResult?.EventStream;
        SettlementPlan settlementPlan = ResolveSettlementPlan(context);
        BattleReportRecord report = ResolveReport(context);
        if (context == null || context.Session == null || outcome == null)
        {
            return StrategicFailureReasons.MissingBattleResultSummary;
        }

        if (string.IsNullOrWhiteSpace(context.Session.SessionId) ||
            string.IsNullOrWhiteSpace(context.ContextId) ||
            !string.Equals(context.Session.SessionId, context.ContextId, StringComparison.Ordinal))
        {
            return StrategicFailureReasons.BattleResultMismatch;
        }

        if (!string.IsNullOrWhiteSpace(outcome.BattleId) &&
            !string.Equals(outcome.BattleId, context.Session.SessionId, StringComparison.Ordinal))
        {
            return StrategicFailureReasons.BattleResultMismatch;
        }

        if (context.Snapshot == null ||
            string.IsNullOrWhiteSpace(context.Snapshot.SnapshotId) ||
            !string.Equals(outcome.SnapshotId ?? "", context.Snapshot.SnapshotId, StringComparison.Ordinal))
        {
            return string.IsNullOrWhiteSpace(outcome.SnapshotId)
                ? StrategicFailureReasons.MissingBattleResultSummary
                : StrategicFailureReasons.BattleResultMismatch;
        }

        if (!outcome.IsComplete ||
            outcome.ActorOutcomes == null ||
            outcome.ActorOutcomes.Count == 0 ||
            context.Session.Participants.Count == 0)
        {
            return StrategicFailureReasons.MissingBattleResultSummary;
        }

        if (eventStream == null ||
            eventStream.Events.Count == 0 ||
            !eventStream.Events.Any(item =>
                item.Kind == BattleEventKind.BattleEnded &&
                string.Equals(item.BattleId ?? "", outcome.BattleId ?? "", StringComparison.Ordinal)))
        {
            return StrategicFailureReasons.MissingBattleResultSummary;
        }

        if (settlementPlan?.Accepted != true)
        {
            return StrategicFailureReasons.MissingBattleResultSummary;
        }

        if (!string.Equals(settlementPlan.SnapshotId ?? "", outcome.SnapshotId ?? "", StringComparison.Ordinal) ||
            !string.Equals(settlementPlan.BattleId ?? "", outcome.BattleId ?? "", StringComparison.Ordinal))
        {
            return StrategicFailureReasons.BattleResultMismatch;
        }

        if (report == null ||
            string.IsNullOrWhiteSpace(report.ReportId) ||
            string.IsNullOrWhiteSpace(report.SnapshotId) ||
            string.IsNullOrWhiteSpace(report.BattleId))
        {
            return StrategicFailureReasons.MissingBattleResultSummary;
        }

        if (!string.Equals(report.SnapshotId ?? "", outcome.SnapshotId ?? "", StringComparison.Ordinal) ||
            !string.Equals(report.BattleId ?? "", outcome.BattleId ?? "", StringComparison.Ordinal))
        {
            return StrategicFailureReasons.BattleResultMismatch;
        }

        StrategicBattleParticipantReference[] deployedParticipants = context.Session.Participants
            .Where(participant => participant?.Role == StrategicBattleParticipantRole.Deployed)
            .ToArray();
        if (deployedParticipants.Length == 0)
        {
            return StrategicFailureReasons.MissingBattleResultSummary;
        }

        if (HasDuplicateParticipantIdentity(deployedParticipants))
        {
            return ParticipantActorMappingAmbiguousReason;
        }

        HashSet<string> actorIds = new(StringComparer.Ordinal);
        if ((outcome.ActorOutcomes ?? new List<BattleActorOutcome>()).Any(actor =>
                actor == null ||
                string.IsNullOrWhiteSpace(actor.ActorId) ||
                !actorIds.Add(actor.ActorId)))
        {
            return ParticipantActorMappingAmbiguousReason;
        }

        foreach (StrategicBattleParticipantReference participant in deployedParticipants)
        {
            if (participant == null ||
                string.IsNullOrWhiteSpace(participant.ParticipantId) ||
                string.IsNullOrWhiteSpace(participant.HeroId) ||
                string.IsNullOrWhiteSpace(participant.CorpsInstanceId))
            {
                return ParticipantActorMappingMissingReason;
            }

            if (!TryResolveParticipantRuntimeOutcome(
                    context,
                    participant,
                    out _,
                    out _,
                    out _,
                    out string mappingFailureReason))
            {
                return mappingFailureReason;
            }
        }

        return "";
    }

    private static BattleRuntimeSessionResult ResolveRuntimeResult(StrategicBattleActiveContext context)
    {
        return context?.RuntimeResult ?? context?.FlowResult?.RuntimeResult;
    }

    private static SettlementPlan ResolveSettlementPlan(StrategicBattleActiveContext context)
    {
        return context?.SettlementPlan ?? context?.FlowResult?.SettlementPlan;
    }

    private static BattleReportRecord ResolveReport(StrategicBattleActiveContext context)
    {
        return context?.Report ?? context?.FlowResult?.Report;
    }

    private static bool HasDuplicateParticipantIdentity(
        IEnumerable<StrategicBattleParticipantReference> participants)
    {
        StrategicBattleParticipantReference[] list = participants?.Where(item => item != null).ToArray() ??
                                                    Array.Empty<StrategicBattleParticipantReference>();
        return list.Select(item => item.ParticipantId ?? "").Distinct(StringComparer.Ordinal).Count() != list.Length ||
               list.Select(item => item.HeroId ?? "").Distinct(StringComparer.Ordinal).Count() != list.Length ||
               list.Select(item => item.CorpsInstanceId ?? "").Distinct(StringComparer.Ordinal).Count() != list.Length;
    }

    private static bool TryResolveParticipantRuntimeOutcome(
        StrategicBattleActiveContext context,
        StrategicBattleParticipantReference participant,
        out BattleGroupSnapshot group,
        out BattleActorOutcome heroOutcome,
        out BattleActorOutcome corpsOutcome,
        out string failureReason)
    {
        group = null;
        heroOutcome = null;
        corpsOutcome = null;
        failureReason = ParticipantActorMappingMissingReason;
        BattleOutcomeResult runtimeOutcome = ResolveRuntimeResult(context)?.Outcome;
        if (context?.Snapshot?.BattleGroups == null ||
            runtimeOutcome?.ActorOutcomes == null ||
            participant == null)
        {
            return false;
        }

        BattleGroupSnapshot[] matchingGroups = context.Snapshot.BattleGroups
            .Where(candidate =>
                candidate != null &&
                string.Equals(candidate.SourceForceId ?? "", participant.ParticipantId ?? "", StringComparison.Ordinal))
            .ToArray();
        if (matchingGroups.Length != 1)
        {
            failureReason = matchingGroups.Length == 0
                ? ParticipantActorMappingMissingReason
                : ParticipantActorMappingAmbiguousReason;
            return false;
        }

        group = matchingGroups[0];
        string commanderGroupId = BattleCommanderGroupIdentity.Resolve(group);
        if (!string.Equals(group.BattleGroupId ?? "", participant.ParticipantId ?? "", StringComparison.Ordinal) ||
            !string.Equals(commanderGroupId, participant.ParticipantId ?? "", StringComparison.Ordinal) ||
            !string.Equals(group.HeroId ?? "", participant.HeroId ?? "", StringComparison.Ordinal) ||
            !string.Equals(group.CorpsId ?? "", participant.CorpsInstanceId ?? "", StringComparison.Ordinal))
        {
            failureReason = ParticipantActorMappingAmbiguousReason;
            return false;
        }

        BattleActorOutcome[] associatedOutcomes = runtimeOutcome.ActorOutcomes
            .Where(actor =>
                actor != null &&
                (string.Equals(actor.SourceForceId ?? "", participant.ParticipantId ?? "", StringComparison.Ordinal) ||
                 string.Equals(actor.BattleGroupId ?? "", commanderGroupId, StringComparison.Ordinal) ||
                 string.Equals(actor.SourceStateId ?? "", participant.HeroId ?? "", StringComparison.Ordinal) ||
                 string.Equals(actor.SourceStateId ?? "", participant.CorpsInstanceId ?? "", StringComparison.Ordinal)))
            .ToArray();
        BattleActorOutcome[] heroOutcomes = associatedOutcomes
            .Where(actor => actor.Kind == BattleRuntimeActorKind.Hero)
            .ToArray();
        BattleActorOutcome[] corpsOutcomes = associatedOutcomes
            .Where(actor => actor.Kind == BattleRuntimeActorKind.Corps)
            .ToArray();
        if (heroOutcomes.Length == 0 || corpsOutcomes.Length == 0)
        {
            return false;
        }

        if (heroOutcomes.Length != 1 || corpsOutcomes.Length != 1 || associatedOutcomes.Length != 2)
        {
            failureReason = ParticipantActorMappingAmbiguousReason;
            return false;
        }

        heroOutcome = heroOutcomes[0];
        corpsOutcome = corpsOutcomes[0];
        if (!IsExactActorMapping(heroOutcome, participant.ParticipantId, participant.HeroId, commanderGroupId) ||
            !IsExactActorMapping(corpsOutcome, participant.ParticipantId, participant.CorpsInstanceId, commanderGroupId))
        {
            failureReason = ParticipantActorMappingAmbiguousReason;
            return false;
        }

        if (group.MaxHitPoints <= 0 ||
            corpsOutcome.RemainingHitPoints < 0 ||
            corpsOutcome.RemainingHitPoints > group.MaxHitPoints ||
            corpsOutcome.Survived != (corpsOutcome.RemainingHitPoints > 0))
        {
            failureReason = ParticipantCasualtyBasisInvalidReason;
            return false;
        }

        failureReason = "";
        return true;
    }

    private static bool IsExactActorMapping(
        BattleActorOutcome actor,
        string participantId,
        string sourceStateId,
        string commanderGroupId)
    {
        return actor != null &&
               string.Equals(actor.SourceForceId ?? "", participantId ?? "", StringComparison.Ordinal) &&
               string.Equals(actor.SourceStateId ?? "", sourceStateId ?? "", StringComparison.Ordinal) &&
               string.Equals(actor.BattleGroupId ?? "", commanderGroupId ?? "", StringComparison.Ordinal);
    }

    public StrategicBattleResultSummary BuildResultSummary(BattleStartRequest request, BattleResult result)
    {
        StrategicBattleResultSummary summary = new()
        {
            SessionId = request?.StrategicBattleSessionId ?? "",
            SnapshotId = result?.ContextId ?? request?.ContextId ?? "",
            ExpeditionId = request?.StrategicExpeditionId ?? "",
            TargetLocationId = request?.StrategicTargetLocationId ?? "",
            Outcome = result?.Outcome ?? BattleOutcome.None,
            ObjectiveSucceeded = IsAnyObjectiveSucceeded(result)
        };
        if (!string.IsNullOrWhiteSpace(GetLegacyResultFailureReason(request, result)))
        {
            return summary;
        }

        if (IsStrategicCompatibilityRequest(request))
        {
            GameLog.Warn(
                nameof(StrategicBattleBridgeService),
                $"StrategicBattleLegacyResultSummaryRejected session={request?.StrategicBattleSessionId ?? ""} expedition={request?.StrategicExpeditionId ?? ""} request={request?.RequestId ?? ""}");
            return summary;
        }

        foreach (IGrouping<string, BattleForceRequest> group in (request?.PlayerForces ?? new List<BattleForceRequest>())
                     .Where(force => !string.IsNullOrWhiteSpace(force.StrategicCorpsInstanceId))
                     .GroupBy(force => force.StrategicCorpsInstanceId))
        {
            BattleForceRequest first = group.First();
            BattleForceResult[] forceResults = group
                .Select(force => FindForceResult(result, force))
                .ToArray();
            if (forceResults.Any(item => item == null))
            {
                continue;
            }

            int preBattleStrength = group.Max(force => Math.Max(0, force.StrategicPreBattleCorpsStrength));
            int initialCount = group.Sum(force => Math.Max(0, force.Count));
            int survivedCount = forceResults.Sum(forceResult => Math.Max(0, forceResult.SurvivedCount));
            int remainingStrength = initialCount <= 0
                ? (summary.Outcome == BattleOutcome.Victory ? preBattleStrength : 0)
                : (int)Math.Round(preBattleStrength * Math.Clamp(survivedCount / (double)initialCount, 0.0, 1.0));
            summary.Participants.Add(new StrategicBattleParticipantResult
            {
                HeroId = first.StrategicHeroId,
                CorpsInstanceId = first.StrategicCorpsInstanceId,
                RemainingCorpsStrength = remainingStrength
            });
        }

        return summary;
    }

    private static bool IsStrategicCompatibilityRequest(BattleStartRequest request)
    {
        return !string.IsNullOrWhiteSpace(request?.StrategicBattleSessionId) ||
               !string.IsNullOrWhiteSpace(request?.StrategicExpeditionId) ||
               !string.IsNullOrWhiteSpace(request?.StrategicTargetLocationId);
    }

    public static string GetLegacyResultFailureReason(BattleStartRequest request, BattleResult result)
    {
        if (request == null || result == null)
        {
            return StrategicFailureReasons.MissingBattleResultSummary;
        }

        if (!string.Equals(request.RequestId ?? "", result.RequestId ?? "", StringComparison.Ordinal) ||
            request.BattleKind != result.BattleKind)
        {
            return StrategicFailureReasons.BattleResultMismatch;
        }

        if (!string.IsNullOrWhiteSpace(request.ContextId) &&
            !string.IsNullOrWhiteSpace(result.ContextId) &&
            !string.Equals(request.ContextId, result.ContextId, StringComparison.Ordinal))
        {
            return StrategicFailureReasons.BattleResultMismatch;
        }

        if (request.BattleKind == BattleKind.Unknown || result.Outcome == BattleOutcome.None)
        {
            return StrategicFailureReasons.MissingBattleResultSummary;
        }

        return "";
    }

    private static string BuildParticipantId(string expeditionId, string heroId, string corpsInstanceId)
    {
        return $"strategic_participant:{expeditionId}:{heroId}:{corpsInstanceId}";
    }

    private List<StrategicBattleParticipantReference> BuildParticipantReferences(
        StrategicManagementState state,
        StrategicExpeditionState expedition)
    {
        List<StrategicBattleParticipantReference> participants = new();
        foreach (StrategicExpeditionParticipantState participant in EnumerateExpeditionParticipants(expedition))
        {
            if (!state.Heroes.TryGetValue(participant.HeroId ?? "", out StrategicHeroState hero) ||
                !state.CorpsInstances.TryGetValue(participant.CorpsInstanceId ?? "", out StrategicCorpsInstanceState corps))
            {
                return new List<StrategicBattleParticipantReference>();
            }

            _definitions.Heroes.TryGetValue(hero.HeroDefinitionId ?? "", out StrategicHeroDefinition heroDefinition);
            _definitions.Corps.TryGetValue(corps.CorpsDefinitionId ?? "", out StrategicCorpsDefinition corpsDefinition);
            participants.Add(new StrategicBattleParticipantReference
            {
                ParticipantId = BuildParticipantId(expedition.ExpeditionId, hero.HeroId, corps.CorpsInstanceId),
                HeroId = hero.HeroId,
                HeroDefinitionId = heroDefinition?.HeroDefinitionId ?? hero.HeroDefinitionId,
                CorpsInstanceId = corps.CorpsInstanceId,
                CorpsDefinitionId = corpsDefinition?.CorpsDefinitionId ?? corps.CorpsDefinitionId,
                FactionId = expedition.FactionId,
                SourceLocationId = expedition.SourceLocationId,
                RollbackStationLocationId = participant.RollbackStationLocationId,
                PreBattleCorpsStrength = ClampStrength(corps.Strength),
                CorpsLevel = Math.Max(1, corps.Level),
                CorpsEquipmentLevel = Math.Max(0, corps.EquipmentLevel)
            });
        }

        return participants;
    }

    private static IEnumerable<StrategicExpeditionParticipantState> EnumerateExpeditionParticipants(
        StrategicExpeditionState expedition)
    {
        if (expedition?.Participants?.Count > 0)
        {
            return expedition.Participants;
        }

        if (expedition == null ||
            string.IsNullOrWhiteSpace(expedition.HeroId) ||
            string.IsNullOrWhiteSpace(expedition.CorpsInstanceId))
        {
            return Array.Empty<StrategicExpeditionParticipantState>();
        }

        return new[]
        {
            new StrategicExpeditionParticipantState
            {
                HeroId = expedition.HeroId,
                CorpsInstanceId = expedition.CorpsInstanceId
            }
        };
    }

    private static string GetMissingParticipantFailureReason(
        StrategicManagementState state,
        StrategicExpeditionState expedition)
    {
        foreach (StrategicExpeditionParticipantState participant in EnumerateExpeditionParticipants(expedition))
        {
            if (!state.Heroes.ContainsKey(participant.HeroId ?? ""))
            {
                return StrategicFailureReasons.MissingHero;
            }

            if (!state.CorpsInstances.ContainsKey(participant.CorpsInstanceId ?? ""))
            {
                return StrategicFailureReasons.MissingCorpsInstance;
            }
        }

        return StrategicFailureReasons.InvalidExpeditionParticipants;
    }

    private StrategicBattleParticipantReference ResolveParticipantForForce(
        StrategicBattleSession session,
        BattleForceRequest force)
    {
        if (session == null || force == null)
        {
            return null;
        }

        StrategicBattleParticipantReference participant = session.Participants.FirstOrDefault(item =>
            !string.IsNullOrWhiteSpace(force.StrategicParticipantId) &&
            string.Equals(item.ParticipantId, force.StrategicParticipantId, StringComparison.Ordinal));
        if (participant != null)
        {
            return participant;
        }

        List<StrategicBattleParticipantReference> unitMatches = session.Participants
            .Where(item =>
                string.Equals(GetHeroBattleUnitId(item), force.UnitDefinitionId ?? "", StringComparison.Ordinal) ||
                string.Equals(GetCorpsBattleUnitId(item), force.UnitDefinitionId ?? "", StringComparison.Ordinal))
            .ToList();
        if (unitMatches.Count == 1)
        {
            return unitMatches[0];
        }

        return session.Participants.Count == 1 ? session.Participants[0] : null;
    }

    private string GetHeroBattleUnitId(StrategicBattleParticipantReference participant)
    {
        return participant != null &&
               _definitions.Heroes.TryGetValue(participant.HeroDefinitionId ?? "", out StrategicHeroDefinition definition)
            ? definition.BattleUnitId ?? ""
            : "";
    }

    private string GetCorpsBattleUnitId(StrategicBattleParticipantReference participant)
    {
        return participant != null &&
               _definitions.Corps.TryGetValue(participant.CorpsDefinitionId ?? "", out StrategicCorpsDefinition definition)
            ? definition.BattleUnitId ?? ""
            : "";
    }

    private static bool IsAnyObjectiveSucceeded(BattleResult result)
    {
        return result != null &&
               (result.ObjectiveResults.Count == 0 && result.Outcome == BattleOutcome.Victory ||
                result.ObjectiveResults.Any(item => item.State == BattleObjectiveState.Succeeded));
    }

    private static BattleForceResult FindForceResult(BattleResult result, BattleForceRequest force)
    {
        BattleForceResult forceResult = result?.ForceResults?.FirstOrDefault(item =>
            !string.IsNullOrWhiteSpace(force.ForceId) &&
            string.Equals(item.ForceId, force.ForceId, StringComparison.Ordinal));
        if (forceResult == null)
        {
            forceResult = result?.ForceResults?.FirstOrDefault(item =>
                string.Equals(item.SourceKind, force.SourceKind, StringComparison.Ordinal) &&
                string.Equals(item.SourceId, force.SourceId, StringComparison.Ordinal) &&
                string.Equals(item.UnitDefinitionId, force.UnitDefinitionId, StringComparison.Ordinal));
        }

        return forceResult;
    }

    private static int ClampStrength(int value)
    {
        return Math.Clamp(value, 0, 100);
    }

    private static BattleOutcome MapRuntimeOutcome(BattleOutcomeResult outcome)
    {
        if (outcome == null || !outcome.IsComplete)
        {
            return BattleOutcome.None;
        }

        return outcome.TerminationReason switch
        {
            BattleTerminationReason.NormalVictory => BattleOutcome.Victory,
            BattleTerminationReason.NormalDefeat => BattleOutcome.Defeat,
            BattleTerminationReason.PlayerRetreat => BattleOutcome.Withdraw,
            BattleTerminationReason.Interrupted => BattleOutcome.Disaster,
            BattleTerminationReason.RuntimeException => BattleOutcome.Disaster,
            _ => BattleOutcome.Disaster
        };
    }
}
