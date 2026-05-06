using Rpg.Definitions.Emotion;

namespace Rpg.Application.Emotion;

public sealed class EmotionAxisWeight
{
    public EmotionAxisWeight(EmotionAxis axis, int weight)
    {
        Axis = axis;
        Weight = weight;
    }

    public EmotionAxis Axis { get; }
    public int Weight { get; }
}
