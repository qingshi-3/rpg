namespace Rpg.Domain.StrategicManagement;

public sealed class StrategicHeroState
{
    public string HeroId { get; set; } = "";
    public string HeroDefinitionId { get; set; } = "";
    public string FactionId { get; set; } = "";
    public string AssignedCorpsInstanceId { get; set; } = "";
    public string CurrentExpeditionId { get; set; } = "";
}
