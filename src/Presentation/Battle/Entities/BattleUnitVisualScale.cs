namespace Rpg.Presentation.Battle.Entities;

public sealed class BattleUnitVisualScale
{
    public static BattleUnitVisualScale Default { get; } = new(spriteScaleMultiplier: 0.8f);

    private BattleUnitVisualScale(float spriteScaleMultiplier)
    {
        SpriteScaleMultiplier = spriteScaleMultiplier;
    }

    public float SpriteScaleMultiplier { get; }
}
