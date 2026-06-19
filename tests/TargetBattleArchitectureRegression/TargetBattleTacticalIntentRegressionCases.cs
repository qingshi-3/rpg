using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;

internal static class TargetBattleTacticalIntentRegressionCases
{
    public static void Register(Action<string, Action> run)
    {
        run("battle entry copies enemy tactical intent config by group key", BattleEntryCopiesEnemyTacticalIntentConfigByGroupKey);
    }

    private static void BattleEntryCopiesEnemyTacticalIntentConfigByGroupKey()
    {
        BattleStartRequest request = BuildBattleEntryRequest("entry_enemy_ai_intent");
        request.AttackerFactionId = "enemy";
        request.DefenderFactionId = "player";
        request.ObjectiveZones.Add(BuildObjectiveZone("player_deployment", "player_deployment", centerX: 0, centerY: 0, priority: 100));
        request.PlayerForces.Add(BuildForce("player_company", "player", count: 1, (0, 0)));
        request.EnemyForces.Add(BuildForce("enemy_raider", "enemy", count: 1, (8, 0)));
        request.EnemyTacticalIntentPlans["enemy_raider"] = new BattleTacticalIntentPlanSnapshot
        {
            IntentId = BattleTacticalIntentIds.AssaultTarget,
            PrimaryTargetSelector = BattleTargetSelectors.RuntimeObservedHostileCluster,
            RetargetPolicyId = BattleRetargetPolicyIds.AllowVolatileObservation,
            FallbackIntentId = BattleTacticalIntentIds.HoldLine
        };

        BattleGroupSessionProbeResult result = new BattleGroupSessionProbeService().PrepareSnapshot(request);

        AssertTrue(result.Success, "battle entry snapshot should prepare");
        BattleGroupSnapshot enemy = result.Snapshot.BattleGroups.Single(item => item.SourceForceId == "enemy_raider");
        AssertEqual(BattleTacticalIntentIds.AssaultTarget, enemy.TacticalIntentPlan.IntentId, "configured enemy intent id should enter snapshot");
        AssertEqual(BattleTargetSelectors.RuntimeObservedHostileCluster, enemy.TacticalIntentPlan.PrimaryTargetSelector, "configured selector should enter snapshot");
        AssertEqual(BattleRetargetPolicyIds.AllowVolatileObservation, enemy.TacticalIntentPlan.RetargetPolicyId, "configured retarget policy should enter snapshot");
        AssertEqual(BattleTacticalIntentPlanSources.ExplicitGroup, enemy.TacticalIntentPlan.IntentSource, "group-key config should be marked explicit");
    }

    private static BattleStartRequest BuildBattleEntryRequest(string id)
    {
        return new BattleStartRequest
        {
            RequestId = id,
            ContextId = id,
            TargetSiteId = "site_1",
            BattleKind = BattleKind.AssaultSite
        };
    }

    private static BattleForceRequest BuildForce(
        string forceId,
        string factionId,
        int count,
        params (int X, int Y)[] placements)
    {
        BattleForceRequest force = new()
        {
            ForceId = forceId,
            UnitDefinitionId = $"{forceId}_unit",
            FactionId = factionId,
            Count = count,
            MaxHitPoints = 80,
            AttackDamage = 1
        };

        foreach ((int x, int y) in placements)
        {
            force.PreferredPlacements.Add(new BattleForcePlacementRequest
            {
                CellX = x,
                CellY = y
            });
        }

        return force;
    }

    private static BattleObjectiveZoneSnapshot BuildObjectiveZone(
        string zoneId,
        string role,
        int centerX,
        int centerY,
        int priority,
        string deploymentSide = "Player")
    {
        return new BattleObjectiveZoneSnapshot
        {
            ObjectiveZoneId = zoneId,
            ObjectiveRole = role,
            DeploymentSide = deploymentSide,
            FactionId = deploymentSide.Equals("Player", StringComparison.OrdinalIgnoreCase) ? "player" : "enemy",
            Priority = priority,
            CellX = centerX,
            CellY = centerY,
            Width = 3,
            Height = 3
        };
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"{message}: expected={expected} actual={actual}");
        }
    }
}
