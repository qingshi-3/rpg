using System.Linq;
using Godot;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.World;

public sealed class StrategicExpeditionWorldArmyAdapter
{
    // Temporary map-movement adapter: Strategic Management owns expedition facts;
    // WorldArmyState remains only the current large-map movement and legacy battle-entry carrier.
    public WorldArmyState CreateWorldArmy(
        StrategicManagementDefinitionSet definitions,
        StrategicManagementState state,
        StrategicExpeditionState expedition,
        string sourceMapSiteId,
        string targetMapSiteId,
        Vector2 sourcePosition,
        Vector2 destination,
        WorldArmyIntent worldIntent,
        int createdTick)
    {
        if (definitions == null ||
            state == null ||
            expedition == null)
        {
            return null;
        }

        System.Collections.Generic.List<StrategicExpeditionParticipantState> participants =
            (expedition.Participants ?? new()).ToList();
        if (participants.Count == 0)
        {
            return null;
        }

        if (participants.Any(participant =>
                participant == null ||
                !state.Heroes.ContainsKey(participant.HeroId ?? "") ||
                !state.CorpsInstances.ContainsKey(participant.CorpsInstanceId ?? "")))
        {
            return null;
        }

        WorldArmyState army = new()
        {
            ArmyId = $"strategic:{expedition.ExpeditionId}",
            StrategicExpeditionId = expedition.ExpeditionId,
            OwnerFactionId = ToWorldFactionId(expedition.FactionId),
            SourceSiteId = sourceMapSiteId ?? "",
            TargetSiteId = targetMapSiteId ?? "",
            MoveSpeed = 56.0f,
            Radius = 16.0f,
            Status = WorldArmyStatus.Moving,
            Intent = worldIntent,
            CreatedTick = createdTick
        };
        army.WorldPosition = sourcePosition;
        army.Destination = destination;
        army.ClearNavigationPath();

        GameLog.Info(
            nameof(StrategicExpeditionWorldArmyAdapter),
            $"StrategicExpeditionWorldArmyCreated expedition={expedition.ExpeditionId} participants={FormatParticipants(participants)} army={army.ArmyId} intent={worldIntent}");
        return army;
    }

    private static string FormatParticipants(System.Collections.Generic.IReadOnlyList<StrategicExpeditionParticipantState> participants)
    {
        return string.Join(",", participants.Select(item => $"{item.HeroId}:{item.CorpsInstanceId}"));
    }

    private static string ToWorldFactionId(string factionId)
    {
        return string.Equals(factionId, StrategicManagementIds.FactionPlayer, System.StringComparison.Ordinal)
            ? StrategicWorldIds.FactionPlayer
            : StrategicWorldIds.FactionUndead;
    }
}
