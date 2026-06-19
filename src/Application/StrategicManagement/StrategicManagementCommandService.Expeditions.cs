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
        StrategicCorpsInstanceState leadCorps = participants[0].Corps;
        string expeditionId = state.AllocateExpeditionId();
        StrategicExpeditionState expedition = new()
        {
            ExpeditionId = expeditionId,
            FactionId = leadHero.FactionId,
            SourceLocationId = sourceLocationId ?? "",
            TargetLocationId = targetLocationId ?? "",
            Intent = intent,
            HeroId = leadHero.HeroId,
            CorpsInstanceId = leadCorps.CorpsInstanceId,
            Status = StrategicExpeditionStatus.Moving,
            CreatedElapsedWorldTimePulses = state.ElapsedWorldTimePulses
        };
        foreach ((StrategicHeroState hero, StrategicCorpsInstanceState corps) in participants)
        {
            expedition.Participants.Add(new StrategicExpeditionParticipantState
            {
                HeroId = hero.HeroId,
                CorpsInstanceId = corps.CorpsInstanceId
            });
        }

        state.Expeditions[expedition.ExpeditionId] = expedition;
        foreach ((StrategicHeroState hero, StrategicCorpsInstanceState corps) in participants)
        {
            hero.CurrentExpeditionId = expedition.ExpeditionId;
            corps.CurrentExpeditionId = expedition.ExpeditionId;
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
        if (state == null || !state.Expeditions.TryGetValue(expeditionId ?? "", out StrategicExpeditionState expedition))
        {
            return Reject("CancelExpedition", expeditionId, StrategicFailureReasons.MissingExpedition);
        }

        expedition.Status = StrategicExpeditionStatus.Cancelled;
        foreach (StrategicExpeditionParticipantState participant in EnumerateExpeditionParticipants(expedition))
        {
            UnlockExpeditionParticipant(state, expedition, participant, null);
        }

        StrategicCommandResult result = StrategicCommandResult.Ok(expedition.ExpeditionId);
        result.Events.Add(Event(
            "StrategicExpeditionCancelled",
            expedition.ExpeditionId,
            ("reason", reason ?? "")));
        Accept("CancelExpedition", expedition.ExpeditionId, result);
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
        StrategicBattleParticipantResult participant)
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
        if (expedition?.Participants?.Count > 0)
        {
            return expedition.Participants;
        }

        if (expedition == null ||
            string.IsNullOrWhiteSpace(expedition.HeroId) ||
            string.IsNullOrWhiteSpace(expedition.CorpsInstanceId))
        {
            return System.Array.Empty<StrategicExpeditionParticipantState>();
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
