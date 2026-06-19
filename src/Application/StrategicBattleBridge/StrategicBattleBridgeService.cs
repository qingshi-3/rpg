using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Application.StrategicManagement;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.BattleGroups;
using Rpg.Domain.Corps;
using Rpg.Domain.Heroes;
using Rpg.Domain.StrategicManagement;
using Rpg.Infrastructure.Logging;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Results;

namespace Rpg.Application.StrategicBattleBridge;

public sealed class StrategicBattleBridgeService
{
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

    public StrategicBattleSnapshotResult CompileStartSnapshot(
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
        foreach (StrategicBattleParticipantReference participant in session.Participants)
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

        return StrategicBattleSnapshotResult.Ok(snapshot);
    }

    public StrategicBattleActiveContextResult CreateActiveContext(
        StrategicManagementState state,
        StrategicBattleSession session,
        BattleStartRequest compatibilityRequest)
    {
        if (state == null || session == null || compatibilityRequest == null)
        {
            return StrategicBattleActiveContextResult.Failed(StrategicFailureReasons.MissingBattleResultSummary);
        }

        AttachSessionToLegacyRequest(session, compatibilityRequest);
        StrategicBattleSnapshotResult snapshotResult = CompileStartSnapshot(state, session);
        if (!snapshotResult.Success)
        {
            return StrategicBattleActiveContextResult.Failed(snapshotResult.FailureReason);
        }

        if (string.IsNullOrWhiteSpace(compatibilityRequest.SiteScenePath))
        {
            compatibilityRequest.SiteScenePath = session.SiteScenePath;
        }

        if (string.IsNullOrWhiteSpace(compatibilityRequest.ReturnScenePath))
        {
            compatibilityRequest.ReturnScenePath = session.ReturnScenePath;
        }

        StrategicBattleActiveContext context = new()
        {
            ContextId = session.SessionId,
            ScenePath = string.IsNullOrWhiteSpace(session.SiteScenePath)
                ? compatibilityRequest.SiteScenePath
                : session.SiteScenePath,
            ReturnScenePath = string.IsNullOrWhiteSpace(session.ReturnScenePath)
                ? compatibilityRequest.ReturnScenePath
                : session.ReturnScenePath,
            Session = session,
            Snapshot = snapshotResult.Snapshot,
            CompatibilityRequest = compatibilityRequest
        };

        GameLog.Info(
            nameof(StrategicBattleBridgeService),
            $"StrategicBattleActiveContextCreated context={context.ContextId} expedition={session.ExpeditionId} target={session.TargetLocationId} snapshot={context.Snapshot.SnapshotId} scene={context.ScenePath}");
        return StrategicBattleActiveContextResult.Ok(context);
    }

    public void AttachSessionToLegacyRequest(StrategicBattleSession session, BattleStartRequest request)
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
        }
    }

    public StrategicBattleResultSummary BuildResultSummary(StrategicBattleActiveContext context)
    {
        StrategicBattleSession session = context?.Session;
        BattleStartSnapshot snapshot = context?.Snapshot;
        BattleRuntimeSessionResult runtimeResult = context?.RuntimeResult ?? context?.FlowResult?.RuntimeResult;
        BattleOutcomeResult outcome = runtimeResult?.Outcome;
        BattleOutcome mappedOutcome = MapRuntimeOutcome(outcome);
        StrategicBattleResultSummary summary = new()
        {
            SessionId = session?.SessionId ?? context?.ContextId ?? "",
            SnapshotId = snapshot?.SnapshotId ?? outcome?.SnapshotId ?? "",
            ExpeditionId = session?.ExpeditionId ?? "",
            TargetLocationId = session?.TargetLocationId ?? "",
            Outcome = mappedOutcome,
            ObjectiveSucceeded = mappedOutcome == BattleOutcome.Victory
        };

        if (!string.IsNullOrWhiteSpace(GetActiveContextFailureReason(context)))
        {
            return summary;
        }

        foreach (StrategicBattleParticipantReference participant in session.Participants)
        {
            if (participant == null || string.IsNullOrWhiteSpace(participant.CorpsInstanceId))
            {
                continue;
            }

            int initialCount = ResolveInitialParticipantCount(context.CompatibilityRequest, participant);
            int survivedCount = CountSurvivingRuntimeCorps(outcome, participant);
            int preBattleStrength = Math.Max(0, participant.PreBattleCorpsStrength);
            int remainingStrength;
            if (outcome.ActorOutcomes.Count == 0 || initialCount <= 0)
            {
                remainingStrength = mappedOutcome == BattleOutcome.Victory ? preBattleStrength : 0;
            }
            else
            {
                remainingStrength = (int)Math.Round(preBattleStrength * Math.Clamp(survivedCount / (double)initialCount, 0.0, 1.0));
            }

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
        if (context == null || context.Session == null || context.RuntimeResult?.Outcome == null)
        {
            return StrategicFailureReasons.MissingBattleResultSummary;
        }

        if (string.IsNullOrWhiteSpace(context.Session.SessionId) ||
            string.IsNullOrWhiteSpace(context.ContextId) ||
            !string.Equals(context.Session.SessionId, context.ContextId, StringComparison.Ordinal))
        {
            return StrategicFailureReasons.BattleResultMismatch;
        }

        if (!string.IsNullOrWhiteSpace(context.RuntimeResult.Outcome.BattleId) &&
            !string.Equals(context.RuntimeResult.Outcome.BattleId, context.Session.SessionId, StringComparison.Ordinal))
        {
            return StrategicFailureReasons.BattleResultMismatch;
        }

        if (!context.RuntimeResult.Outcome.IsComplete || context.Session.Participants.Count == 0)
        {
            return StrategicFailureReasons.MissingBattleResultSummary;
        }

        return "";
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

    private static int ResolveInitialParticipantCount(
        BattleStartRequest request,
        StrategicBattleParticipantReference participant)
    {
        return (request?.PlayerForces ?? new List<BattleForceRequest>())
            .Where(force =>
                string.Equals(force.StrategicParticipantId ?? "", participant.ParticipantId ?? "", StringComparison.Ordinal) ||
                string.Equals(force.StrategicCorpsInstanceId ?? "", participant.CorpsInstanceId ?? "", StringComparison.Ordinal))
            .Sum(force => Math.Max(0, force.Count));
    }

    private static int CountSurvivingRuntimeCorps(
        BattleOutcomeResult outcome,
        StrategicBattleParticipantReference participant)
    {
        return (outcome?.ActorOutcomes ?? new List<BattleActorOutcome>())
            .Count(actor =>
                actor.Kind == BattleRuntimeActorKind.Corps &&
                actor.Survived &&
                (string.Equals(actor.SourceForceId ?? "", participant.ParticipantId ?? "", StringComparison.Ordinal) ||
                 string.Equals(actor.BattleGroupId ?? "", participant.ParticipantId ?? "", StringComparison.Ordinal) ||
                 string.Equals(actor.SourceStateId ?? "", participant.CorpsInstanceId ?? "", StringComparison.Ordinal)));
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
