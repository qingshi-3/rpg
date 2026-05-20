using Rpg.Application.Battle.Navigation;
using Rpg.Application.Battle.Snapshots;

internal static class BattleNavigationTestTopology
{
    public static void Compile(LocationBattleContext context)
    {
        if (context == null)
        {
            return;
        }

        context.NavigationTopology = BattleNavigationTopologyCompiler.Compile(
            context.NavigationSurfaces,
            context.NavigationConnections);
    }
}
