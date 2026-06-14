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
            EnumerateExpeditionParticipants(expedition).ToList();
        if (participants.Count == 0)
        {
            return null;
        }

        StrategicExpeditionParticipantState leadParticipant = participants[0];
        if (!state.Heroes.TryGetValue(leadParticipant.HeroId ?? "", out StrategicHeroState leadHero) ||
            !state.CorpsInstances.TryGetValue(leadParticipant.CorpsInstanceId ?? "", out StrategicCorpsInstanceState leadCorps))
        {
            return null;
        }

        WorldArmyState army = new()
        {
            ArmyId = $"strategic:{expedition.ExpeditionId}",
            StrategicExpeditionId = expedition.ExpeditionId,
            StrategicHeroId = leadHero.HeroId,
            StrategicCorpsInstanceId = leadCorps.CorpsInstanceId,
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
        foreach (StrategicExpeditionParticipantState participant in participants)
        {
            if (!state.Heroes.TryGetValue(participant.HeroId ?? "", out StrategicHeroState hero) ||
                !state.CorpsInstances.TryGetValue(participant.CorpsInstanceId ?? "", out StrategicCorpsInstanceState corps) ||
                !definitions.Heroes.TryGetValue(hero.HeroDefinitionId ?? "", out StrategicHeroDefinition heroDefinition) ||
                !definitions.Corps.TryGetValue(corps.CorpsDefinitionId ?? "", out StrategicCorpsDefinition corpsDefinition) ||
                string.IsNullOrWhiteSpace(heroDefinition.BattleUnitId) ||
                string.IsNullOrWhiteSpace(corpsDefinition.BattleUnitId))
            {
                return null;
            }

            string participantId = BuildParticipantId(expedition.ExpeditionId, hero.HeroId, corps.CorpsInstanceId);
            army.GarrisonUnits.Add(new GarrisonState
            {
                UnitTypeId = heroDefinition.BattleUnitId,
                Count = 1,
                FactionId = army.OwnerFactionId,
                SourceKind = "StrategicExpeditionParticipant",
                SourceId = participantId,
                StrategicParticipantId = participantId,
                Morale = 80
            });
            army.GarrisonUnits.Add(new GarrisonState
            {
                UnitTypeId = corpsDefinition.BattleUnitId,
                Count = System.Math.Max(1, corpsDefinition.BattleUnitCount),
                FactionId = army.OwnerFactionId,
                SourceKind = "StrategicExpeditionParticipant",
                SourceId = participantId,
                StrategicParticipantId = participantId,
                Morale = 70
            });
        }

        GameLog.Info(
            nameof(StrategicExpeditionWorldArmyAdapter),
            $"StrategicExpeditionWorldArmyCreated expedition={expedition.ExpeditionId} participants={FormatParticipants(participants)} army={army.ArmyId} intent={worldIntent}");
        return army;
    }

    private static System.Collections.Generic.IEnumerable<StrategicExpeditionParticipantState> EnumerateExpeditionParticipants(
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

    private static string FormatParticipants(System.Collections.Generic.IReadOnlyList<StrategicExpeditionParticipantState> participants)
    {
        return string.Join(",", participants.Select(item => $"{item.HeroId}:{item.CorpsInstanceId}"));
    }

    private static string BuildParticipantId(string expeditionId, string heroId, string corpsInstanceId)
    {
        return $"strategic_participant:{expeditionId}:{heroId}:{corpsInstanceId}";
    }

    private static string ToWorldFactionId(string factionId)
    {
        return string.Equals(factionId, StrategicManagementIds.FactionPlayer, System.StringComparison.Ordinal)
            ? StrategicWorldIds.FactionPlayer
            : StrategicWorldIds.FactionUndead;
    }
}
