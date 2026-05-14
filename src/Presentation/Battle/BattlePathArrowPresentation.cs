namespace Rpg.Presentation.Battle;

public sealed class BattlePathArrowPresentation
{
    public static BattlePathArrowPresentation Default { get; } = new(showMovementPathArrows: false);

    private BattlePathArrowPresentation(bool showMovementPathArrows)
    {
        ShowMovementPathArrows = showMovementPathArrows;
    }

    public bool ShowMovementPathArrows { get; }
}
