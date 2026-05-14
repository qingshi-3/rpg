using Godot;

namespace Rpg.Presentation.Battle.Feedback;

public readonly record struct BattleDamageNumberMotionSpec(
    Vector2 SpawnOffset,
    Vector2 FloatOffset)
{
    public static BattleDamageNumberMotionSpec Default => new(
        new Vector2(0f, -30f),
        new Vector2(9f, -34f));
}
