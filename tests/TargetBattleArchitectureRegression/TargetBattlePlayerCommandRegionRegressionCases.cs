using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Tactics;

internal static class TargetBattlePlayerCommandRegionRegressionCases
{
    public static void Register(Action<string, Action> run)
    {
        run("battle entry seeds player objective as command owned region", BattleEntrySeedsPlayerObjectiveAsCommandOwnedRegion);
    }

    private static void BattleEntrySeedsPlayerObjectiveAsCommandOwnedRegion()
    {
        BattleStartRequest request = new()
        {
            RequestId = "entry_player_command_region",
            ContextId = "entry_player_command_region",
            TargetSiteId = "site_1",
            BattleKind = BattleKind.AssaultSite,
            AttackerFactionId = "player",
            DefenderFactionId = "enemy",
            PlayerBattleGroupPlan = new BattleGroupPlanSnapshot
            {
                ObjectiveZoneId = "enemy_lane",
                EngagementRule = BattleEngagementRule.AttackFirst
            }
        };
        request.ObjectiveZones.Add(new BattleObjectiveZoneSnapshot
        {
            ObjectiveZoneId = "enemy_lane",
            ObjectiveRole = "enemy_deployment",
            DeploymentSide = "Enemy",
            FactionId = "enemy",
            CellX = 6,
            CellY = 0,
            Width = 3,
            Height = 3
        });
        request.PlayerForces.Add(BuildForce("player_company", "player", 0));
        request.EnemyForces.Add(BuildForce("enemy_company", "enemy", 10));
        TargetBattleTestTopology.CompileRequestRect(request, -2, -2, 13, 4);

        BattleGroupSessionProbeResult result = new BattleGroupSessionProbeService().PrepareSnapshot(request);

        AssertTrue(result.Success, "battle entry snapshot should prepare");
        BattleGroupSnapshot player = result.Snapshot.BattleGroups.Single(item => item.SourceForceId == "player_company");
        string ownerGroupId = BattleCommanderGroupIdentity.Resolve(player);
        BattleTacticalRegionSnapshot commandRegion = player.InitialTacticalRegions.Single();
        AssertEqual("enemy_lane", commandRegion.RegionId, "player objective command region should keep the selected objective id");
        AssertEqual(ownerGroupId, commandRegion.OwnerBattleGroupId, "player command region owner");
        AssertEqual(BattleTacticalRegionKind.FixedTarget, commandRegion.Kind, "player objective command region kind");
        AssertEqual("enemy_lane", commandRegion.SourceRegionId, "player command region source objective");
        AssertEqual(7, commandRegion.CenterCellX, "player command region center x");
        AssertEqual(1, commandRegion.CenterCellY, "player command region center y");
        AssertEqual(3, commandRegion.Width, "player command region width");
        AssertEqual(3, commandRegion.Height, "player command region height");

        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(result.Snapshot);
        controller.AdvanceNextTick();
        BattleGroupTacticalState state = controller.State.TacticalStates[ownerGroupId];
        AssertEqual("enemy_lane", state.SelectedRegion?.RegionId, "runtime should preserve the selected player command region");
        AssertEqual(BattleGroupTacticalCommandSource.PlayerCommand, state.SelectedRegionCommandSource, "player command must outrank autonomous fallback targeting");
    }

    private static BattleForceRequest BuildForce(string forceId, string factionId, int x)
    {
        BattleForceRequest force = new()
        {
            ForceId = forceId,
            UnitDefinitionId = $"{forceId}_unit",
            FactionId = factionId,
            Count = 1,
            MaxHitPoints = 80,
            AttackDamage = 1
        };
        force.PreferredPlacements.Add(new BattleForcePlacementRequest { CellX = x, CellY = 0 });
        return force;
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
