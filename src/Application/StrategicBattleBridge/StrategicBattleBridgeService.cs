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

        request.PlayerForces ??= new List<BattleForceRequest>();
        if (request.PlayerForces.Count == 0)
        {
            // The compatibility rows are compiled from Bridge participant identity.
            // WorldArmy is only a movement carrier and must not seed roster facts.
            request.PlayerForces.AddRange(BuildParticipantForceProjection(session, request.SourceArmyId));
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

    private IEnumerable<BattleForceRequest> BuildParticipantForceProjection(
        StrategicBattleSession session,
        string sourceArmyId)
    {
        foreach (StrategicBattleParticipantReference participant in session.Participants.Where(item => item != null))
        {
            string heroBattleUnitId = GetHeroBattleUnitId(participant);
            string corpsBattleUnitId = GetCorpsBattleUnitId(participant);
            int corpsCount = _definitions.Corps.TryGetValue(
                participant.CorpsDefinitionId ?? "",
                out StrategicCorpsDefinition corpsDefinition)
                ? Math.Max(1, corpsDefinition.BattleUnitCount)
                : 1;
            string sourceId = string.IsNullOrWhiteSpace(sourceArmyId)
                ? participant.ParticipantId
                : sourceArmyId;

            yield return CreateParticipantForce(
                participant,
                sourceId,
                $"{participant.ParticipantId}:hero",
                heroBattleUnitId,
                1,
                heroBattleUnitId,
                corpsBattleUnitId);
            yield return CreateParticipantForce(
                participant,
                sourceId,
                $"{participant.ParticipantId}:corps",
                corpsBattleUnitId,
                corpsCount,
                heroBattleUnitId,
                corpsBattleUnitId);
        }
    }

    private static BattleForceRequest CreateParticipantForce(
        StrategicBattleParticipantReference participant,
        string sourceId,
        string forceId,
        string unitDefinitionId,
        int count,
        string heroBattleUnitId,
        string corpsBattleUnitId)
    {
        return new BattleForceRequest
        {
            ForceId = forceId,
            CommandGroupId = participant.ParticipantId,
            SourceKind = "PlayerArmy",
            SourceId = sourceId ?? "",
            UnitDefinitionId = unitDefinitionId ?? "",
            StrategicParticipantId = participant.ParticipantId,
            StrategicHeroId = participant.HeroId,
            StrategicHeroDefinitionId = participant.HeroDefinitionId,
            StrategicHeroBattleUnitId = heroBattleUnitId ?? "",
            StrategicCorpsInstanceId = participant.CorpsInstanceId,
            StrategicCorpsDefinitionId = participant.CorpsDefinitionId,
            StrategicCorpsBattleUnitId = corpsBattleUnitId ?? "",
            StrategicSourceLocationId = participant.SourceLocationId,
            StrategicPreBattleCorpsStrength = participant.PreBattleCorpsStrength,
            Count = count,
            FactionId = participant.FactionId
        };
    }

    public StrategicBattleResultSummary BuildResultSummary(StrategicBattleActiveContext context)
    {
        StrategicBattleSession session = context?.Session;
        BattleStartSnapshot snapshot = context?.Snapshot;
        StrategicBattleResultEnvelope resultEnvelope = context?.ResultEnvelope;
        BattleRuntimeSessionResult runtimeResult = resultEnvelope?.RuntimeResult;
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
            TerminationReason = outcome?.TerminationReason ?? BattleTerminationReason.None,
            ObjectiveId = session?.BattleObjectiveId ?? "",
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

        PopulateEnvelopeFacts(summary, runtimeResult, resultEnvelope.SettlementPlan, resultEnvelope.Report);
        if (!TryCompileTargetConsequences(summary, resultEnvelope.Report, out string consequenceFailureReason))
        {
            summary.Outcome = BattleOutcome.None;
            summary.TerminationReason = BattleTerminationReason.None;
            GameLog.Warn(
                nameof(StrategicBattleBridgeService),
                $"StrategicBattleResultSummaryRejected session={summary.SessionId} snapshot={summary.SnapshotId} reason={consequenceFailureReason}");
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
                    outcome,
                    out BattleGroupSnapshot group,
                    out BattleActorOutcome heroOutcome,
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
            int strengthLoss = Math.Max(0, preBattleStrength - remainingStrength);
            bool retreated = outcome.TerminationReason == BattleTerminationReason.PlayerRetreat;
            bool routed = remainingStrength == 0;

            summary.Participants.Add(new StrategicBattleParticipantResult
            {
                ParticipantId = participant.ParticipantId,
                HeroId = participant.HeroId,
                CorpsInstanceId = participant.CorpsInstanceId,
                HeroState = retreated && heroOutcome.Survived
                    ? StrategicHeroBattleState.Retreated
                    : heroOutcome.Survived
                        ? StrategicHeroBattleState.Survived
                        : StrategicHeroBattleState.Defeated,
                PreBattleCorpsStrength = preBattleStrength,
                RemainingCorpsStrength = remainingStrength,
                StrengthLoss = strengthLoss,
                CorpsEquipmentLevel = participant.CorpsEquipmentLevel,
                Routed = routed,
                Retreated = retreated,
                RequiresRecovery = routed || retreated || strengthLoss > 0,
                RecoveryLocationId = routed || retreated || strengthLoss > 0
                    ? participant.RollbackStationLocationId
                    : ""
            });
        }

        // This flag is an acceptance boundary, not an optional enhancement. Strategic
        // Management may consume only summaries that completed every source mapping.
        summary.HasConsequenceFacts = true;
        return summary;
    }

    public static string GetActiveContextFailureReason(StrategicBattleActiveContext context)
    {
        StrategicBattleResultEnvelope resultEnvelope = context?.ResultEnvelope;
        return resultEnvelope == null
            ? StrategicFailureReasons.MissingBattleResultSummary
            : GetResultEnvelopeFailureReason(context, resultEnvelope);
    }

    public static string GetResultEnvelopeFailureReason(
        StrategicBattleActiveContext context,
        StrategicBattleResultEnvelope resultEnvelope)
    {
        BattleRuntimeSessionResult runtimeResult = resultEnvelope?.RuntimeResult;
        BattleOutcomeResult outcome = runtimeResult?.Outcome;
        BattleEventStream eventStream = runtimeResult?.EventStream;
        SettlementPlan settlementPlan = resultEnvelope?.SettlementPlan;
        BattleReportRecord report = resultEnvelope?.Report;
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

        if (string.IsNullOrWhiteSpace(outcome.BattleId) ||
            !string.Equals(outcome.BattleId, context.Session.SessionId, StringComparison.Ordinal) ||
            !string.Equals(resultEnvelope.SessionId, context.Session.SessionId, StringComparison.Ordinal))
        {
            return StrategicFailureReasons.BattleResultMismatch;
        }

        if (context.Snapshot == null ||
            string.IsNullOrWhiteSpace(context.Snapshot.SnapshotId) ||
            !string.Equals(outcome.SnapshotId ?? "", context.Snapshot.SnapshotId, StringComparison.Ordinal) ||
            !string.Equals(resultEnvelope.SnapshotId, context.Snapshot.SnapshotId, StringComparison.Ordinal))
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

        if (!HasConsistentSourceEvents(outcome, eventStream, settlementPlan, report))
        {
            return StrategicFailureReasons.BattleResultMismatch;
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

        if ((settlementPlan.Deltas?.ChangedLocationIds ?? new List<string>()).Any(id =>
                !string.Equals(id ?? "", context.Session.TargetLocationId ?? "", StringComparison.Ordinal)))
        {
            return StrategicFailureReasons.BattleResultMismatch;
        }

        StrategicBattleParticipantReference[] carriedParticipants = context.Session.Participants
            .Where(participant => participant != null)
            .ToArray();
        if (carriedParticipants.Length != context.Session.Participants.Count ||
            HasDuplicateParticipantIdentity(carriedParticipants))
        {
            return ParticipantActorMappingAmbiguousReason;
        }

        StrategicBattleParticipantReference[] deployedParticipants = carriedParticipants
            .Where(participant => participant?.Role == StrategicBattleParticipantRole.Deployed)
            .ToArray();
        if (deployedParticipants.Length == 0)
        {
            return StrategicFailureReasons.MissingBattleResultSummary;
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
                    outcome,
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

    private static bool HasDuplicateParticipantIdentity(
        IEnumerable<StrategicBattleParticipantReference> participants)
    {
        StrategicBattleParticipantReference[] list = participants?.Where(item => item != null).ToArray() ??
                                                    Array.Empty<StrategicBattleParticipantReference>();
        return list.Select(item => item.ParticipantId ?? "").Distinct(StringComparer.Ordinal).Count() != list.Length ||
               list.Select(item => item.HeroId ?? "").Distinct(StringComparer.Ordinal).Count() != list.Length ||
               list.Select(item => item.CorpsInstanceId ?? "").Distinct(StringComparer.Ordinal).Count() != list.Length;
    }

    private static bool HasConsistentSourceEvents(
        BattleOutcomeResult outcome,
        BattleEventStream eventStream,
        SettlementPlan settlementPlan,
        BattleReportRecord report)
    {
        if (outcome == null || eventStream == null || settlementPlan?.Deltas == null || report == null)
        {
            return false;
        }

        string[] eventIds = eventStream.Events.Select(item => item?.EventId ?? "").ToArray();
        if (eventIds.Any(string.IsNullOrWhiteSpace) ||
            eventIds.Distinct(StringComparer.Ordinal).Count() != eventIds.Length ||
            eventStream.Events.Any(item =>
                item == null ||
                !string.Equals(item.BattleId ?? "", outcome.BattleId ?? "", StringComparison.Ordinal)))
        {
            return false;
        }

        if (!(settlementPlan.SourceEventIds ?? new List<string>()).SequenceEqual(eventIds, StringComparer.Ordinal) ||
            !(report.SourceEventIds ?? new List<string>()).SequenceEqual(eventIds, StringComparer.Ordinal) ||
            !string.Equals(report.OutcomeSummary ?? "", outcome.TerminationReason.ToString(), StringComparison.Ordinal))
        {
            return false;
        }

        HashSet<string> heroIds = outcome.ActorOutcomes
            .Where(item => item?.Kind == BattleRuntimeActorKind.Hero)
            .Select(item => item.SourceStateId ?? "")
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> corpsIds = outcome.ActorOutcomes
            .Where(item => item?.Kind == BattleRuntimeActorKind.Corps)
            .Select(item => item.SourceStateId ?? "")
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> groupIds = outcome.ActorOutcomes
            .Where(item => item != null)
            .Select(item => item.BattleGroupId ?? "")
            .ToHashSet(StringComparer.Ordinal);

        return HasUniqueNonEmptyValues(settlementPlan.Deltas?.ChangedHeroIds) &&
               settlementPlan.Deltas.ChangedHeroIds.All(heroIds.Contains) &&
               HasUniqueNonEmptyValues(settlementPlan.Deltas?.ChangedCorpsIds) &&
               settlementPlan.Deltas.ChangedCorpsIds.All(corpsIds.Contains) &&
               HasUniqueNonEmptyValues(settlementPlan.Deltas?.ChangedBattleGroupIds) &&
               settlementPlan.Deltas.ChangedBattleGroupIds.All(groupIds.Contains) &&
               HasUniqueNonEmptyValues(settlementPlan.Deltas?.ChangedLocationIds) &&
               HasUniqueValues(report.FailureCandidates) &&
               HasReportSkillSourceConsistency(eventStream, report);
    }

    private static bool HasUniqueNonEmptyValues(IEnumerable<string> values)
    {
        string[] list = (values ?? Enumerable.Empty<string>()).ToArray();
        return list.All(value => !string.IsNullOrWhiteSpace(value)) &&
               list.Distinct(StringComparer.Ordinal).Count() == list.Length;
    }

    private static bool HasUniqueValues(IEnumerable<string> values)
    {
        string[] list = (values ?? Enumerable.Empty<string>()).ToArray();
        return list.All(value => value != null) &&
               list.Distinct(StringComparer.Ordinal).Count() == list.Length;
    }

    private static bool HasReportSkillSourceConsistency(
        BattleEventStream eventStream,
        BattleReportRecord report)
    {
        string[] expectedUses = eventStream.Events
            .Where(item => item?.Kind == BattleEventKind.SkillUsed)
            .Select(item => $"{item.ReasonCode}:{item.ActorId}->{item.TargetId}")
            .ToArray();
        if (!(report.HeroSkillUses ?? new List<string>()).SequenceEqual(expectedUses, StringComparer.Ordinal))
        {
            return false;
        }

        foreach (BattleReportSkillEffectFact fact in report.HeroSkillEffects ?? new List<BattleReportSkillEffectFact>())
        {
            if (fact == null || !eventStream.Events.Any(item =>
                    item.Kind == BattleEventKind.EffectApplied &&
                    string.Equals(item.SourceCommandId ?? "", fact.SourceCommandId ?? "", StringComparison.Ordinal) &&
                    string.Equals(item.SourceActionId ?? "", fact.SourceActionId ?? "", StringComparison.Ordinal) &&
                    string.Equals(item.SourceDefinitionId ?? "", fact.SourceDefinitionId ?? "", StringComparison.Ordinal) &&
                    string.Equals(item.ActorId ?? "", fact.ActorId ?? "", StringComparison.Ordinal) &&
                    string.Equals(item.TargetId ?? "", fact.TargetId ?? "", StringComparison.Ordinal) &&
                    string.Equals(item.ReasonCode ?? "", fact.ReasonCode ?? "", StringComparison.Ordinal) &&
                    item.CorpsStrengthDelta == fact.CorpsStrengthDelta &&
                    item.RuntimeTick == fact.RuntimeTick))
            {
                return false;
            }
        }

        foreach (BattleReportSkillFailureFact fact in report.HeroSkillFailures ?? new List<BattleReportSkillFailureFact>())
        {
            if (fact == null || !eventStream.Events.Any(item =>
                    item.Kind == BattleEventKind.CommandFailed &&
                    string.Equals(item.SourceCommandId ?? "", fact.SourceCommandId ?? "", StringComparison.Ordinal) &&
                    string.Equals(item.SourceDefinitionId ?? "", fact.SourceDefinitionId ?? "", StringComparison.Ordinal) &&
                    string.Equals(item.ActorId ?? "", fact.ActorId ?? "", StringComparison.Ordinal) &&
                    string.Equals(item.TargetId ?? "", fact.TargetId ?? "", StringComparison.Ordinal) &&
                    string.Equals(item.ReasonCode ?? "", fact.ReasonCode ?? "", StringComparison.Ordinal) &&
                    item.RuntimeTick == fact.RuntimeTick))
            {
                return false;
            }
        }

        return true;
    }

    private static void PopulateEnvelopeFacts(
        StrategicBattleResultSummary summary,
        BattleRuntimeSessionResult runtimeResult,
        SettlementPlan settlementPlan,
        BattleReportRecord report)
    {
        summary.ReportId = report.ReportId ?? "";
        summary.ReportOutcomeSummary = report.OutcomeSummary ?? "";
        summary.SettlementSourceEventIds.AddRange(settlementPlan.SourceEventIds ?? new List<string>());
        summary.ReportSourceEventIds.AddRange(report.SourceEventIds ?? new List<string>());
        summary.ChangedHeroIds.AddRange(settlementPlan.Deltas?.ChangedHeroIds ?? new List<string>());
        summary.ChangedCorpsIds.AddRange(settlementPlan.Deltas?.ChangedCorpsIds ?? new List<string>());
        summary.ChangedBattleGroupIds.AddRange(settlementPlan.Deltas?.ChangedBattleGroupIds ?? new List<string>());
        summary.ChangedLocationIds.AddRange(settlementPlan.Deltas?.ChangedLocationIds ?? new List<string>());
        summary.FailureCandidates.AddRange(report.FailureCandidates ?? new List<string>());
        summary.HeroSkillUses.AddRange(report.HeroSkillUses ?? new List<string>());

        foreach (BattleEvent item in runtimeResult.EventStream.Events.Where(IsContributionEvent))
        {
            summary.CommandAndSkillContributions.Add(new StrategicBattleContributionFact
            {
                EventId = item.EventId ?? "",
                EventKind = item.Kind.ToString(),
                BattleGroupId = item.BattleGroupId ?? "",
                ActorId = item.ActorId ?? "",
                SourceCommandId = item.SourceCommandId ?? "",
                SourceActionId = item.SourceActionId ?? "",
                SourceDefinitionId = item.SourceDefinitionId ?? "",
                TargetId = item.TargetId ?? "",
                EffectKind = item.EffectKind ?? "",
                ReasonCode = item.ReasonCode ?? "",
                CorpsStrengthDelta = item.CorpsStrengthDelta,
                RuntimeTick = item.RuntimeTick
            });
        }

        foreach (BattleReportSkillEffectFact fact in report.HeroSkillEffects ?? new List<BattleReportSkillEffectFact>())
        {
            summary.HeroSkillEffects.Add(new StrategicBattleSkillEffectFact
            {
                SourceCommandId = fact.SourceCommandId ?? "",
                SourceActionId = fact.SourceActionId ?? "",
                SourceDefinitionId = fact.SourceDefinitionId ?? "",
                EffectKind = fact.EffectKind ?? "",
                ActorId = fact.ActorId ?? "",
                TargetId = fact.TargetId ?? "",
                ReasonCode = fact.ReasonCode ?? "",
                CorpsStrengthDelta = fact.CorpsStrengthDelta,
                RuntimeTick = fact.RuntimeTick,
                RuntimeTimeSeconds = fact.RuntimeTimeSeconds
            });
        }

        foreach (BattleReportSkillFailureFact fact in report.HeroSkillFailures ?? new List<BattleReportSkillFailureFact>())
        {
            summary.HeroSkillFailures.Add(new StrategicBattleSkillFailureFact
            {
                SourceCommandId = fact.SourceCommandId ?? "",
                SourceDefinitionId = fact.SourceDefinitionId ?? "",
                ActorId = fact.ActorId ?? "",
                TargetId = fact.TargetId ?? "",
                ReasonCode = fact.ReasonCode ?? "",
                RuntimeTick = fact.RuntimeTick,
                RuntimeTimeSeconds = fact.RuntimeTimeSeconds
            });
        }
    }

    private static bool IsContributionEvent(BattleEvent item)
    {
        return item != null &&
               (!string.IsNullOrWhiteSpace(item.SourceCommandId) ||
                item.Kind == BattleEventKind.SkillUsed ||
                item.Kind == BattleEventKind.EffectApplied ||
                item.Kind == BattleEventKind.CommandFailed ||
                item.Kind == BattleEventKind.CommandInterrupted ||
                item.Kind == BattleEventKind.CommandCompleted);
    }

    private bool TryCompileTargetConsequences(
        StrategicBattleResultSummary summary,
        BattleReportRecord report,
        out string failureReason)
    {
        failureReason = "";
        _definitions.Locations.TryGetValue(summary.TargetLocationId ?? "", out StrategicLocationDefinition target);
        summary.TargetDisplayName = target?.DisplayName ?? "";
        summary.OutcomeText = summary.Outcome switch
        {
            BattleOutcome.Victory => "\u80dc\u5229",
            BattleOutcome.Defeat => "\u5931\u8d25",
            BattleOutcome.Withdraw => "\u64a4\u9000",
            _ => "\u672a\u77e5"
        };

        StrategicBattleRewardDefinition[] matches = _definitions.BattleRewards.Values
            .Where(item => item != null &&
                           string.Equals(item.TargetLocationId ?? "", summary.TargetLocationId ?? "", StringComparison.Ordinal))
            .ToArray();
        if (matches.Length > 1)
        {
            failureReason = StrategicFailureReasons.BattleResultMismatch;
            return false;
        }

        StrategicBattleRewardDefinition reward = matches.SingleOrDefault();
        if (reward == null)
        {
            summary.FailureReasonText = summary.Outcome == BattleOutcome.Victory
                ? ""
                : string.Join("\uff1b", report.FailureCandidates ?? new List<string>());
            return true;
        }

        if (string.IsNullOrWhiteSpace(reward.RewardId) ||
            !HasUniqueNonEmptyValues(reward.EquipmentSampleIds) ||
            reward.VictoryResourceRewards.Any(item =>
                item == null || item.Amount <= 0 || string.IsNullOrWhiteSpace(item.ResourceId)) ||
            reward.VictoryResourceRewards.Select(item => item.ResourceId).Distinct(StringComparer.Ordinal).Count() != reward.VictoryResourceRewards.Count ||
            reward.EquipmentSampleIds.Any(id => !_definitions.EquipmentSamples.ContainsKey(id)) ||
            reward.VictoryResourceRewards.Any(item => !_definitions.Resources.ContainsKey(item.ResourceId)) ||
            !string.IsNullOrWhiteSpace(reward.RewardEquipmentSampleId) &&
            !reward.EquipmentSampleIds.Contains(reward.RewardEquipmentSampleId, StringComparer.Ordinal))
        {
            failureReason = StrategicFailureReasons.BattleResultMismatch;
            return false;
        }

        summary.EquipmentSampleIds.AddRange(reward.EquipmentSampleIds);
        bool victory = summary.Outcome == BattleOutcome.Victory && summary.ObjectiveSucceeded;
        summary.WorldChangeText = victory ? reward.VictorySummaryText ?? "" : reward.DefeatSummaryText ?? "";
        summary.ProgressionText = victory ? reward.VictoryProgressionText ?? "" : reward.DefeatProgressionText ?? "";
        summary.FailureReasonText = victory
            ? ""
            : string.Join("\uff1b", report.FailureCandidates ?? new List<string>());
        if (!victory)
        {
            if (!string.IsNullOrWhiteSpace(reward.DisplayName))
            {
                AddSummaryLine(summary.RewardLines, $"\u672a\u83b7\u5f97\uff1a{reward.DisplayName}");
            }

            return true;
        }

        summary.RewardClaimId = reward.RewardId;
        foreach (StrategicResourceAmount amount in reward.VictoryResourceRewards)
        {
            summary.ResourceRewards.Add(new StrategicResourceAmount(amount.ResourceId, amount.Amount));
        }

        if (!string.IsNullOrWhiteSpace(reward.RewardEquipmentSampleId))
        {
            summary.RewardEquipmentSampleIds.Add(reward.RewardEquipmentSampleId);
        }

        AddSummaryLine(summary.RewardLines, reward.UnlockText);
        foreach (StrategicResourceAmount amount in summary.ResourceRewards)
        {
            _definitions.Resources.TryGetValue(amount.ResourceId, out StrategicResourceDefinition resource);
            AddSummaryLine(summary.RewardLines, $"\u83b7\u5f97\uff1a{resource?.DisplayName ?? amount.ResourceId} +{amount.Amount}");
        }

        if (_definitions.EquipmentSamples.TryGetValue(
                reward.RewardEquipmentSampleId ?? "",
                out StrategicEquipmentSampleDefinition equipment))
        {
            AddSummaryLine(summary.RewardLines, $"\u83b7\u5f97\u88c5\u5907\uff1a{equipment.DisplayName}");
        }

        return true;
    }

    private static void AddSummaryLine(ICollection<string> lines, string line)
    {
        if (!string.IsNullOrWhiteSpace(line) && !lines.Contains(line))
        {
            lines.Add(line);
        }
    }

    private static bool TryResolveParticipantRuntimeOutcome(
        StrategicBattleActiveContext context,
        StrategicBattleParticipantReference participant,
        BattleOutcomeResult runtimeOutcome,
        out BattleGroupSnapshot group,
        out BattleActorOutcome heroOutcome,
        out BattleActorOutcome corpsOutcome,
        out string failureReason)
    {
        group = null;
        heroOutcome = null;
        corpsOutcome = null;
        failureReason = ParticipantActorMappingMissingReason;
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
            !string.Equals(group.CorpsId ?? "", participant.CorpsInstanceId ?? "", StringComparison.Ordinal) ||
            group.CorpsStrength != ClampStrength(participant.PreBattleCorpsStrength) ||
            group.CorpsEquipmentLevel != participant.CorpsEquipmentLevel)
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
            heroOutcome.RemainingHitPoints < 0 ||
            heroOutcome.Survived != (heroOutcome.RemainingHitPoints > 0) ||
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
            if (participant == null ||
                !state.Heroes.TryGetValue(participant.HeroId ?? "", out StrategicHeroState hero) ||
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
        return expedition?.Participants is { } participants
            ? participants
            : Array.Empty<StrategicExpeditionParticipantState>();
    }

    private static string GetMissingParticipantFailureReason(
        StrategicManagementState state,
        StrategicExpeditionState expedition)
    {
        foreach (StrategicExpeditionParticipantState participant in EnumerateExpeditionParticipants(expedition))
        {
            if (participant == null)
            {
                return StrategicFailureReasons.InvalidExpeditionParticipants;
            }

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
