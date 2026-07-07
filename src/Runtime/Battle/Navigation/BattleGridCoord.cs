namespace Rpg.Runtime.Battle.Navigation;

public readonly record struct BattleGridCoord(int X, int Y, int Height = 0)
{
    public override string ToString()
    {
        return $"({X}, {Y}, h={Height})";
    }
}
