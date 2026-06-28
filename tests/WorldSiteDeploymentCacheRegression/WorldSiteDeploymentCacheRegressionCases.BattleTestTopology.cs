using Rpg.Application.Battle;
using Rpg.Application.Battle.Navigation;
using Rpg.Application.Battle.Snapshots;

internal static partial class WorldSiteDeploymentCacheRegressionCases
{
internal static void AttachFlatRequestTopology(
    BattleStartRequest request,
    int minX = -2,
    int minY = -2,
    int maxX = 10,
    int maxY = 6)
{
    if (request == null)
    {
        return;
    }

    request.NavigationSurfaces.Clear();
    request.NavigationConnections.Clear();
    for (int y = minY; y <= maxY; y++)
    {
        for (int x = minX; x <= maxX; x++)
        {
            request.NavigationSurfaces.Add(new BattleNavigationSurfaceSnapshot
            {
                X = x,
                Y = y,
                Height = 0,
                MoveCost = 1
            });
        }
    }

    request.NavigationTopology = BattleNavigationTopologyCompiler.Compile(
        request.NavigationSurfaces,
        request.NavigationConnections);
}

internal static void AttachFlatSnapshotTopology(
    BattleStartSnapshot snapshot,
    int minX = -2,
    int minY = -2,
    int maxX = 10,
    int maxY = 6)
{
    if (snapshot?.LocationContext == null)
    {
        return;
    }

    snapshot.LocationContext.NavigationSurfaces.Clear();
    snapshot.LocationContext.NavigationConnections.Clear();
    for (int y = minY; y <= maxY; y++)
    {
        for (int x = minX; x <= maxX; x++)
        {
            snapshot.LocationContext.NavigationSurfaces.Add(new BattleNavigationSurfaceSnapshot
            {
                X = x,
                Y = y,
                Height = 0,
                MoveCost = 1
            });
        }
    }

    snapshot.LocationContext.NavigationTopology = BattleNavigationTopologyCompiler.Compile(
        snapshot.LocationContext.NavigationSurfaces,
        snapshot.LocationContext.NavigationConnections);
}
}
