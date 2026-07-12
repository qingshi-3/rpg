using System.Linq;
using Rpg.Domain.StrategicManagement;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.StrategicManagement;

public sealed class StrategicManagementStateInvariantService
{
    public int RepairAll(StrategicManagementState state)
    {
        return RepairResolvedCapturedCityCompanyStations(state);
    }

    public int RepairResolvedCapturedCityCompanyStations(StrategicManagementState state)
    {
        if (state?.Expeditions == null || state.CorpsInstances == null)
        {
            return 0;
        }

        int repaired = 0;
        foreach (StrategicExpeditionState expedition in state.Expeditions.Values.Where(item => item != null))
        {
            if (!TryResolveCapturedCity(state, expedition, out string capturedCityId))
            {
                continue;
            }

            foreach (StrategicExpeditionParticipantState participant in EnumerateParticipants(expedition))
            {
                if (!state.CorpsInstances.TryGetValue(participant?.CorpsInstanceId ?? "", out StrategicCorpsInstanceState corps) ||
                    corps == null ||
                    participant.BattleRole == StrategicBattleParticipantRole.Reserve ||
                    corps.Strength <= 0 ||
                    corps.Status == StrategicCorpsInstanceStatus.Routed ||
                    !string.Equals(corps.FactionId, expedition.FactionId, System.StringComparison.Ordinal) ||
                    (!string.IsNullOrWhiteSpace(corps.CurrentExpeditionId) &&
                     !string.Equals(corps.CurrentExpeditionId, expedition.ExpeditionId, System.StringComparison.Ordinal)) ||
                    string.Equals(corps.HomeCityId, capturedCityId, System.StringComparison.Ordinal))
                {
                    continue;
                }

                corps.HomeCityId = capturedCityId;
                repaired++;
            }
        }

        if (repaired > 0)
        {
            GameLog.Warn(
                nameof(StrategicManagementStateInvariantService),
                $"StrategicManagementStateInvariantRepaired capturedCityCompanyStations={repaired}");
        }

        return repaired;
    }

    private static bool TryResolveCapturedCity(
        StrategicManagementState state,
        StrategicExpeditionState expedition,
        out string capturedCityId)
    {
        capturedCityId = "";
        if (state == null ||
            expedition == null ||
            expedition.Status != StrategicExpeditionStatus.Resolved ||
            string.IsNullOrWhiteSpace(expedition.TargetLocationId) ||
            !state.Cities.ContainsKey(expedition.TargetLocationId) ||
            !state.Locations.TryGetValue(expedition.TargetLocationId, out StrategicLocationState location) ||
            !string.Equals(location.OwnerFactionId, expedition.FactionId, System.StringComparison.Ordinal))
        {
            return false;
        }

        if (!TryGetVictoryFeedback(state, expedition.ExpeditionId))
        {
            return false;
        }

        capturedCityId = expedition.TargetLocationId;
        return true;
    }

    private static bool TryGetVictoryFeedback(StrategicManagementState state, string expeditionId)
    {
        if (state?.BattleFeedbackRecordIdsByExpedition == null ||
            state.BattleFeedbackRecords == null ||
            !state.BattleFeedbackRecordIdsByExpedition.TryGetValue(expeditionId ?? "", out string feedbackId) ||
            !state.BattleFeedbackRecords.TryGetValue(feedbackId ?? "", out StrategicBattleFeedbackRecord feedback))
        {
            return false;
        }

        return feedback?.Victory == true;
    }

    private static System.Collections.Generic.IReadOnlyList<StrategicExpeditionParticipantState> EnumerateParticipants(
        StrategicExpeditionState expedition)
    {
        return expedition?.Participants is { } participants
            ? participants
            : System.Array.Empty<StrategicExpeditionParticipantState>();
    }
}
