using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Navigation;
using Rpg.Application.Battle.Snapshots;

internal static class TargetBattleTestTopology
{
    public static void CompileAroundGroups(BattleStartSnapshot snapshot, int margin = 2)
    {
        PrepareRuntimeFixtureFacts(snapshot);
        if (snapshot?.LocationContext?.NavigationTopology?.HasNodes == true)
        {
            return;
        }

        BattleGroupSnapshot[] groups = snapshot?.BattleGroups?
            .Where(group => group != null)
            .ToArray() ?? System.Array.Empty<BattleGroupSnapshot>();
        if (snapshot == null || groups.Length == 0)
        {
            return;
        }

        int minX = groups.Min(group => group.CellX) - margin;
        int minY = groups.Min(group => group.CellY) - margin;
        int maxX = groups.Max(group => group.CellX + System.Math.Max(1, group.FootprintWidth) - 1) + margin;
        int maxY = groups.Max(group => group.CellY + System.Math.Max(1, group.FootprintHeight) - 1) + margin;
        CompileRect(snapshot, minX, minY, maxX, maxY);
    }

    public static void CompileRect(BattleStartSnapshot snapshot, int minX, int minY, int maxX, int maxY)
    {
        PrepareRuntimeFixtureFacts(snapshot);
        if (snapshot == null)
        {
            return;
        }

        snapshot.LocationContext.NavigationSurfaces.Clear();
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

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
    }

    public static void CompileRequestRect(BattleStartRequest request, int minX, int minY, int maxX, int maxY)
    {
        if (request == null)
        {
            return;
        }

        PrepareRuntimeFixtureFacts(request.PlayerForces);
        PrepareRuntimeFixtureFacts(request.EnemyForces);
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

    private static void PrepareRuntimeFixtureFacts(BattleStartSnapshot snapshot)
    {
        foreach (BattleGroupSnapshot group in snapshot?.BattleGroups ?? Enumerable.Empty<BattleGroupSnapshot>())
        {
            if (group == null)
            {
                continue;
            }

            group.MaxHitPoints = group.MaxHitPoints > 0
                ? group.MaxHitPoints
                : System.Math.Max(1, group.CorpsStrength);
            group.AttackDamage = group.AttackDamage > 0 ? group.AttackDamage : 1;
            group.AttackRange = group.AttackRange > 0 ? group.AttackRange : 1;
            group.AttackSpeed = double.IsNaN(group.AttackSpeed) || double.IsInfinity(group.AttackSpeed) || group.AttackSpeed <= 0
                ? BattleAttackSpeedPolicy.DefaultAttackSpeed
                : group.AttackSpeed;
            group.MoveStepSeconds = double.IsNaN(group.MoveStepSeconds) || double.IsInfinity(group.MoveStepSeconds) || group.MoveStepSeconds <= 0
                ? BattleActionTimingPolicy.DefaultMoveStepSeconds
                : group.MoveStepSeconds;
        }
    }

    private static void PrepareRuntimeFixtureFacts(IEnumerable<BattleForceRequest> forces)
    {
        foreach (BattleForceRequest force in forces ?? Enumerable.Empty<BattleForceRequest>())
        {
            if (force == null)
            {
                continue;
            }

            force.MaxHitPoints = force.MaxHitPoints > 0
                ? force.MaxHitPoints
                : System.Math.Max(1, force.Count * 40);
            force.AttackDamage = force.AttackDamage > 0 ? force.AttackDamage : 1;
            force.AttackRange = force.AttackRange > 0 ? force.AttackRange : 1;
            force.AttackSpeed = double.IsNaN(force.AttackSpeed) || double.IsInfinity(force.AttackSpeed) || force.AttackSpeed <= 0
                ? BattleAttackSpeedPolicy.DefaultAttackSpeed
                : force.AttackSpeed;
            force.MoveStepSeconds = double.IsNaN(force.MoveStepSeconds) || double.IsInfinity(force.MoveStepSeconds) || force.MoveStepSeconds <= 0
                ? BattleActionTimingPolicy.DefaultMoveStepSeconds
                : force.MoveStepSeconds;
        }
    }
}
