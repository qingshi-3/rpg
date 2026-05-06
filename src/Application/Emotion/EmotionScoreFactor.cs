namespace Rpg.Application.Emotion;

public sealed class EmotionScoreFactor
{
    public EmotionScoreFactor(string id, int amount)
    {
        Id = id ?? "";
        Amount = amount;
    }

    public string Id { get; }
    public int Amount { get; }
}
