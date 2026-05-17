namespace Rpg.Presentation.Battle.Entities;

public sealed class BattleUnitVisualScale
{
    public static BattleUnitVisualScale Default { get; } = new(
        spriteScaleMultiplier: 0.8f,
        footprintScaleStepMultiplier: 0.35f);

    private BattleUnitVisualScale(float spriteScaleMultiplier, float footprintScaleStepMultiplier)
    {
        SpriteScaleMultiplier = spriteScaleMultiplier;
        FootprintScaleStepMultiplier = footprintScaleStepMultiplier;
    }

    public float SpriteScaleMultiplier { get; }

    // Footprint visuals should suggest body size without stretching art or mapping every occupied cell to a full sprite-scale step.
    public float FootprintScaleStepMultiplier { get; }
}
