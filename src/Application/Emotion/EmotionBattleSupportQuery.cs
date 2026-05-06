namespace Rpg.Application.Emotion;

public sealed class EmotionBattleSupportQuery
{
    public EmotionBattleSupportQuery(string actorId, string supportedFactionId, int battleRisk = 0, int supportCost = 0)
    {
        ActorId = actorId ?? "";
        SupportedFactionId = supportedFactionId ?? "";
        BattleRisk = battleRisk;
        SupportCost = supportCost;
    }

    public string ActorId { get; }
    public string SupportedFactionId { get; }
    public int BattleRisk { get; }
    public int SupportCost { get; }
}
