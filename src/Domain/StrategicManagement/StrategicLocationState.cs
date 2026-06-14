namespace Rpg.Domain.StrategicManagement;

public sealed class StrategicLocationState
{
    public string LocationId { get; set; } = "";
    public string OwnerFactionId { get; set; } = "";
    public StrategicLocationControlState ControlState { get; set; } = StrategicLocationControlState.Neutral;
}
