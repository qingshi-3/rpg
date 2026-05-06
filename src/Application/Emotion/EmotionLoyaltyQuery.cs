namespace Rpg.Application.Emotion;

public sealed class EmotionLoyaltyQuery
{
    public EmotionLoyaltyQuery(string actorId, string factionId, int pressure = 0)
    {
        ActorId = actorId ?? "";
        FactionId = factionId ?? "";
        Pressure = pressure;
    }

    public string ActorId { get; }
    public string FactionId { get; }
    public int Pressure { get; }
}
