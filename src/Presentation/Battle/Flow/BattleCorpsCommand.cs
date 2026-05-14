namespace Rpg.Presentation.Battle.Flow;

public enum BattleCorpsCommand
{
    Assault = 0,
    FocusFire = 1,
    HoldLine = 2
}

public static class BattleCorpsCommandLabels
{
    public static BattleCorpsCommand Next(BattleCorpsCommand command)
    {
        return command switch
        {
            BattleCorpsCommand.Assault => BattleCorpsCommand.FocusFire,
            BattleCorpsCommand.FocusFire => BattleCorpsCommand.HoldLine,
            _ => BattleCorpsCommand.Assault
        };
    }

    public static string ToDisplayText(BattleCorpsCommand command)
    {
        return command switch
        {
            BattleCorpsCommand.Assault => "突击",
            BattleCorpsCommand.FocusFire => "集火",
            BattleCorpsCommand.HoldLine => "坚守",
            _ => "突击"
        };
    }
}
