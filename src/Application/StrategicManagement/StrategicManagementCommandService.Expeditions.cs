using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.StrategicBattleBridge;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.StrategicManagement;
public sealed partial class StrategicManagementCommandService
{
    public StrategicCommandResult CreateExpedition(
        StrategicManagementState state,
        string sourceLocationId,
        string targetLocationId,
        StrategicExpeditionIntent intent,
        string heroId)
    {
        return CreateExpedition(
            state,
            sourceLocationId,
            targetLocationId,
            intent,
            string.IsNullOrWhiteSpace(heroId) ? System.Array.Empty<string>() : new[] { heroId });
    }

    public StrategicCommandResult CreateExpedition(
        StrategicManagementState state,
        string sourceLocationId,
        string targetLocationId,
        StrategicExpeditionIntent intent,
        System.Collections.Generic.IReadOnlyCollection<string> heroIds)
    {
        string failureReason = _rules.GetExpeditionCreationFailureReason(
            state,
            sourceLocationId,
            targetLocationId,
            intent,
            heroIds);
        if (!string.IsNullOrWhiteSpace(failureReason))
        {
            return Reject("CreateExpedition", string.Join(",", heroIds ?? System.Array.Empty<string>()), failureReason);
        }

        System.Collections.Generic.List<(StrategicHeroState Hero, StrategicCorpsInstanceState Corps)> participants =
            BuildExpeditionParticipants(state, heroIds);
        StrategicHeroState leadHero = participants[0].Hero;
        string expeditionId = state.AllocateExpeditionId();
        StrategicExpeditionState expedition = new()
        {
            ExpeditionId = expeditionId,
            FactionId = leadHero.FactionId,
            SourceLocationId = sourceLocationId ?? "",
            TargetLocationId = targetLocationId ?? "",
            Intent = intent,
            Status = StrategicExpeditionStatus.Moving,
            CreatedElapsedWorldTimePulses = state.ElapsedWorldTimePulses
        };
        foreach ((StrategicHeroState hero, StrategicCorpsInstanceState corps) in participants)
        {
            expedition.Participants.Add(new StrategicExpeditionParticipantState
            {
                HeroId = hero.HeroId,
                CorpsInstanceId = corps.CorpsInstanceId,
                // Capture the authoritative station before dispatch clears HomeCityId.
                RollbackStationLocationId = corps.HomeCityId
            });
        }

        state.Expeditions[expedition.ExpeditionId] = expedition;
        foreach ((StrategicHeroState hero, StrategicCorpsInstanceState corps) in participants)
        {
            hero.CurrentExpeditionId = expedition.ExpeditionId;
            corps.CurrentExpeditionId = expedition.ExpeditionId;
            // A moving expedition owns its companies until arrival or battle settlement.
            // SourceLocationId is a departure record, not a continuing city station.
            corps.HomeCityId = "";
            corps.Status = StrategicCorpsInstanceStatus.Expedition;
        }

        StrategicCommandResult result = StrategicCommandResult.Ok(
            participants.SelectMany(item => new[] { item.Hero.HeroId, item.Corps.CorpsInstanceId })
                .Append(expedition.ExpeditionId)
                .ToArray());
        result.CreatedEntityId = expedition.ExpeditionId;
        result.Events.Add(Event(
            "StrategicExpeditionCreated",
            expedition.ExpeditionId,
            ("participants", FormatExpeditionParticipants(expedition)),
            ("source", expedition.SourceLocationId),
            ("target", expedition.TargetLocationId),
            ("intent", expedition.Intent.ToString())));
        Accept("CreateExpedition", expedition.ExpeditionId, result);
        return result;
    }

    public StrategicCommandResult CancelExpedition(
        StrategicManagementState state,
        string expeditionId,
        string reason = "")
    {
        string failureReason = GetExpeditionCancellationFailureReason(state, expeditionId);
        if (!string.IsNullOrWhiteSpace(failureReason))
        {
            return Reject("CancelExpedition", expeditionId, failureReason);
        }

        StrategicExpeditionState expedition = state.Expeditions[expeditionId ?? ""];
        // The entire restoration plan is valid before the first participant is mutated.
        expedition.Status = StrategicExpeditionStatus.Cancelled;
        foreach (StrategicExpeditionParticipantState participant in EnumerateExpeditionParticipants(expedition))
        {
            UnlockExpeditionParticipant(state, expedition, participant, null, participant.RollbackStationLocationId);
        }

        StrategicCommandResult result = StrategicCommandResult.Ok(expedition.ExpeditionId);
        result.Events.Add(Event(
            "StrategicExpeditionCancelled",
            expedition.ExpeditionId,
            ("reason", reason ?? "")));
        Accept("CancelExpedition", expedition.ExpeditionId, result);
        return result;
    }

    public string GetExpeditionCancellationFailureReason(StrategicManagementState state, string expeditionId)
    {
        if (state == null || !state.Expeditions.TryGetValue(expeditionId ?? "", out StrategicExpeditionState expedition))
        {
            return StrategicFailureReasons.MissingExpedition;
        }

        System.Collections.Generic.HashSet<string> heroIds = new(System.StringComparer.Ordinal);
        System.Collections.Generic.HashSet<string> corpsIds = new(System.StringComparer.Ordinal);
        foreach (StrategicExpeditionParticipantState participant in EnumerateExpeditionParticipants(expedition))
        {
            string stationId = participant?.RollbackStationLocationId ?? "";
            if (participant == null ||
                !heroIds.Add(participant.HeroId ?? "") ||
                !corpsIds.Add(participant.CorpsInstanceId ?? "") ||
                !state.Heroes.TryGetValue(participant.HeroId ?? "", out StrategicHeroState hero) ||
                !state.CorpsInstances.TryGetValue(participant.CorpsInstanceId ?? "", out StrategicCorpsInstanceState corps) ||
                !string.Equals(hero.CurrentExpeditionId, expedition.ExpeditionId, System.StringComparison.Ordinal) ||
                !string.Equals(corps.CurrentExpeditionId, expedition.ExpeditionId, System.StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(stationId) ||
                !state.Cities.ContainsKey(stationId) ||
                !_definitions.Locations.TryGetValue(stationId, out StrategicLocationDefinition stationDefinition) ||
                stationDefinition.Kind != StrategicLocationKind.City ||
                !state.Locations.TryGetValue(stationId, out StrategicLocationState stationState) ||
                !string.Equals(stationState.OwnerFactionId, expedition.FactionId, System.StringComparison.Ordinal))
            {
                return StrategicFailureReasons.InvalidExpeditionRollbackPlan;
            }
        }

        return heroIds.Count == 0 ? StrategicFailureReasons.InvalidExpeditionRollbackPlan : "";
    }

    public StrategicCommandResult CompleteExpeditionArrival(
        StrategicManagementState state,
        string expeditionId)
    {
        string failureReason = _rules.GetExpeditionArrivalFailureReason(state, expeditionId);
        if (!string.IsNullOrWhiteSpace(failureReason))
        {
            return Reject("CompleteExpeditionArrival", expeditionId, failureReason);
        }

        StrategicExpeditionState expedition = state.Expeditions[expeditionId ?? ""];
        expedition.Status = StrategicExpeditionStatus.Resolved;
        foreach (StrategicExpeditionParticipantState participant in EnumerateExpeditionParticipants(expedition))
        {
            UnlockExpeditionParticipant(state, expedition, participant, null, expedition.TargetLocationId);
        }

        StrategicCommandResult result = StrategicCommandResult.Ok(
            EnumerateExpeditionParticipants(expedition)
                .SelectMany(item => new[] { item.HeroId, item.CorpsInstanceId })
                .Append(expedition.ExpeditionId)
                .ToArray());
        result.CreatedEntityId = expedition.ExpeditionId;
        result.Events.Add(Event(
            "StrategicExpeditionArrived",
            expedition.ExpeditionId,
            ("target", expedition.TargetLocationId),
            ("intent", expedition.Intent.ToString())));
        Accept("CompleteExpeditionArrival", expedition.ExpeditionId, result);
        return result;
    }

    public StrategicCommandResult RetargetExpedition(
        StrategicManagementState state,
        string expeditionId,
        string targetLocationId,
        StrategicExpeditionIntent intent)
    {
        string failureReason = _rules.GetExpeditionRetargetFailureReason(
            state,
            expeditionId,
            targetLocationId,
            intent);
        if (!string.IsNullOrWhiteSpace(failureReason))
        {
            return Reject("RetargetExpedition", expeditionId, failureReason);
        }

        StrategicExpeditionState expedition = state.Expeditions[expeditionId ?? ""];
        expedition.TargetLocationId = intent == StrategicExpeditionIntent.MoveToPosition
            ? ""
            : targetLocationId ?? "";
        expedition.Intent = intent;
        expedition.Status = StrategicExpeditionStatus.Moving;

        StrategicCommandResult result = StrategicCommandResult.Ok(
            expedition.ExpeditionId,
            expedition.TargetLocationId,
            expedition.Intent.ToString());
        result.Events.Add(Event(
            "StrategicExpeditionRetargeted",
            expedition.ExpeditionId,
            ("target", expedition.TargetLocationId),
            ("intent", expedition.Intent.ToString())));
        Accept("RetargetExpedition", expedition.ExpeditionId, result);
        return result;
    }

    private static void UnlockExpeditionParticipant(
        StrategicManagementState state,
        StrategicExpeditionState expedition,
        StrategicExpeditionParticipantState expeditionParticipant,
        StrategicBattleParticipantResult participant,
        string stationCityId = "")
    {
        if (state.Heroes.TryGetValue(expeditionParticipant?.HeroId ?? "", out StrategicHeroState hero) &&
            string.Equals(hero.CurrentExpeditionId, expedition.ExpeditionId, System.StringComparison.Ordinal))
        {
            hero.CurrentExpeditionId = "";
        }

        if (!state.CorpsInstances.TryGetValue(expeditionParticipant?.CorpsInstanceId ?? "", out StrategicCorpsInstanceState corps))
        {
            return;
        }

        int remainingStrength = participant == null
            ? corps.Strength
            : System.Math.Clamp(participant.RemainingCorpsStrength, 0, 100);
        corps.Strength = remainingStrength;
        if (string.Equals(corps.CurrentExpeditionId, expedition.ExpeditionId, System.StringComparison.Ordinal))
        {
            corps.CurrentExpeditionId = "";
        }

        // Expedition settlement is the only place that gives the company a new station.
        // Moving companies keep HomeCityId empty so source cities cannot manage them.
        if (remainingStrength > 0 && !string.IsNullOrWhiteSpace(stationCityId))
        {
            corps.HomeCityId = stationCityId;
        }

        corps.Status = remainingStrength <= 0
            ? StrategicCorpsInstanceStatus.Routed
            : string.IsNullOrWhiteSpace(corps.AssignedHeroId)
                ? StrategicCorpsInstanceStatus.Garrisoned
                : StrategicCorpsInstanceStatus.AssignedToHero;
    }

    private static System.Collections.Generic.List<(StrategicHeroState Hero, StrategicCorpsInstanceState Corps)> BuildExpeditionParticipants(
        StrategicManagementState state,
        System.Collections.Generic.IReadOnlyCollection<string> heroIds)
    {
        System.Collections.Generic.List<(StrategicHeroState Hero, StrategicCorpsInstanceState Corps)> participants = new();
        foreach (string heroId in NormalizeHeroIds(heroIds))
        {
            StrategicHeroState hero = state.Heroes[heroId];
            StrategicCorpsInstanceState corps = state.CorpsInstances[hero.AssignedCorpsInstanceId];
            participants.Add((hero, corps));
        }

        return participants;
    }

    private static System.Collections.Generic.IReadOnlyList<StrategicExpeditionParticipantState> EnumerateExpeditionParticipants(
        StrategicExpeditionState expedition)
    {
        return expedition?.Participants is { } participants
            ? participants
            : System.Array.Empty<StrategicExpeditionParticipantState>();
    }

    private static string[] NormalizeHeroIds(System.Collections.Generic.IReadOnlyCollection<string> heroIds)
    {
        return (heroIds ?? System.Array.Empty<string>())
            .Where(heroId => !string.IsNullOrWhiteSpace(heroId))
            .Distinct(System.StringComparer.Ordinal)
            .ToArray();
    }

    private static string FormatExpeditionParticipants(StrategicExpeditionState expedition)
    {
        return string.Join(
            ",",
            EnumerateExpeditionParticipants(expedition)
                .Select(item => $"{item.HeroId}:{item.CorpsInstanceId}"));
    }
}
