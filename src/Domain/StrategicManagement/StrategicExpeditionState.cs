using System.Collections.Generic;

namespace Rpg.Domain.StrategicManagement;

public sealed class StrategicExpeditionState
{
    public string ExpeditionId { get; set; } = "";
    public string FactionId { get; set; } = "";
    public string SourceLocationId { get; set; } = "";
    public string TargetLocationId { get; set; } = "";
    public StrategicExpeditionIntent Intent { get; set; } = StrategicExpeditionIntent.Unknown;
    public List<StrategicExpeditionParticipantState> Participants { get; set; } = new();
    public StrategicExpeditionStatus Status { get; set; } = StrategicExpeditionStatus.Unknown;
    public int CreatedElapsedWorldTimePulses { get; set; }
}

public sealed class StrategicExpeditionParticipantState
{
    public string HeroId { get; set; } = "";
    public string CorpsInstanceId { get; set; } = "";
    public string RollbackStationLocationId { get; set; } = "";
    public StrategicBattleParticipantRole BattleRole { get; set; } = StrategicBattleParticipantRole.Unknown;
}

public enum StrategicBattleParticipantRole
{
    Unknown = 0,
    Deployed = 1,
    Reserve = 2
}
