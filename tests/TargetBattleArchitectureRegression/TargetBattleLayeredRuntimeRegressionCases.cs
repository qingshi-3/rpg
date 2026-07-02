using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Tactics;

internal static class TargetBattleLayeredRuntimeRegressionCases
{
    public static void Register(Action<string, Action> run)
    {
        run("probe force count rows share runtime commander identity", ProbeForceCountRowsShareRuntimeCommanderIdentity);
        run("runtime groups perception by commander identity", RuntimeGroupsPerceptionByCommanderIdentity);
        run("player commanded group enters player scoped engagement from perception", PlayerCommandedGroupEntersPlayerScopedEngagementFromPerception);
    }

    private static void ProbeForceCountRowsShareRuntimeCommanderIdentity()
    {
        BattleStartRequest request = new()
        {
            RequestId = "request_layered_runtime",
            ContextId = "battle_layered_runtime",
            TargetSiteId = "site_1"
        };
        request.PlayerForces.Add(BuildForce("army_1:hero", "PlayerArmy", "army_1", "player", "hero_unit", 1, (0, 0)));
        request.PlayerForces.Add(BuildForce("army_1:corps", "PlayerArmy", "army_1", "player", "corps_unit", 3, (1, 0), (1, 1), (1, 2)));
        request.EnemyForces.Add(BuildForce("site_1:defender", "DefenderSite", "site_1", "enemy", "enemy_unit", 2, (5, 0), (5, 1)));
        TargetBattleTestTopology.CompileRequestRect(request, -2, -2, 8, 4);

        BattleGroupSessionProbeResult result = new BattleGroupSessionProbeService().PrepareSnapshot(request);

        AssertTrue(result.Success, "probe snapshot should prepare");
        BattleGroupSnapshot[] playerRows = result.Snapshot.BattleGroups
            .Where(item => item.SourceForceId is "army_1:hero" or "army_1:corps")
            .ToArray();
        AssertEqual(4, playerRows.Length, "force counts still produce runtime actor rows");
        AssertEqual(1, playerRows.Select(item => item.RuntimeCommanderGroupId).Distinct(StringComparer.Ordinal).Count(), "one battle group should have one commander id");
        AssertTrue(!string.IsNullOrWhiteSpace(playerRows[0].RuntimeCommanderGroupId), "commander id must be explicit");

        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(result.Snapshot);
        string[] playerCommanderIds = controller.State.Actors
            .Where(item => item.Kind == BattleRuntimeActorKind.Corps && item.FactionId == "player")
            .Select(item => item.BattleGroupId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        AssertSequence(new[] { playerRows[0].RuntimeCommanderGroupId }, playerCommanderIds, "runtime corps actors should share commander id");
    }

    private static void RuntimeGroupsPerceptionByCommanderIdentity()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildCommanderPerceptionSnapshot());

        controller.AdvanceNextTick();

        AssertTrue(controller.State.GroupPerceptionSummaries.ContainsKey("player_company"), "perception summary should be keyed by commander id");
        BattleGroupPerceptionSummary summary = controller.State.GroupPerceptionSummaries["player_company"];
        AssertEqual(2, summary.MemberCoverages.Count, "both player actors should contribute to one commander perception summary");
        AssertSequence(new[] { "enemy_force:1" }, summary.PerceivedHostileActorIds, "shared commander perception should see the hostile once");
    }

    private static void PlayerCommandedGroupEntersPlayerScopedEngagementFromPerception()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildCommanderPerceptionSnapshot());

        controller.AdvanceNextTick();

        BattleGroupTacticalState playerState = controller.State.TacticalStates["player_company"];
        AssertEqual(BattleGroupEngagementState.Engaged, playerState.EngagementState, "player group should enter local combat from scoped perception");
        AssertEqual(BattleGroupTacticalMode.PlayerCommanded, playerState.TacticalMode, "player scoped engagement must preserve player tactical mode");
        AssertTrue(playerState.LocalCombatRegion != null, "player scoped engagement should enable a local combat region");
        AssertEqual("player_company", playerState.LocalCombatRegion!.OwnerBattleGroupId, "local combat owner should be the commander id");
    }

    private static BattleStartSnapshot BuildCommanderPerceptionSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_commander_perception",
            BattleId = "battle_commander_perception",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("player_row_a", "player_company", "player", "player_a", 0, 0, BattleGroupTacticalMode.PlayerCommanded),
                BuildGroup("player_row_b", "player_company", "player", "player_b", 1, 0, BattleGroupTacticalMode.PlayerCommanded),
                BuildGroup("enemy_row", "enemy_company", "enemy", "enemy_force", 3, 0, BattleGroupTacticalMode.EnemyHoldDefense, initialCommandId: "HoldLine")
            }
        };

        for (int x = 0; x <= 5; x++)
        {
            AddSurface(snapshot, x, 0);
        }

        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleGroupSnapshot BuildGroup(
        string snapshotGroupId,
        string commanderGroupId,
        string factionId,
        string sourceForceId,
        int x,
        int y,
        BattleGroupTacticalMode tacticalMode,
        string initialCommandId = "")
    {
        return new BattleGroupSnapshot
        {
            BattleGroupId = snapshotGroupId,
            RuntimeCommanderGroupId = commanderGroupId,
            FactionId = factionId,
            SourceForceId = sourceForceId,
            HeroId = $"{sourceForceId}_hero",
            HeroDefinitionId = $"{sourceForceId}_hero_definition",
            CorpsId = $"{sourceForceId}_corps",
            CorpsDefinitionId = $"{sourceForceId}_corps_definition",
            CorpsStrength = 80,
            MaxHitPoints = 80,
            AttackDamage = 1,
            SourceLocationId = "site_1",
            CellX = x,
            CellY = y,
            InitialCorpsCommandId = initialCommandId,
            TacticalMode = tacticalMode
        };
    }

    private static BattleForceRequest BuildForce(
        string forceId,
        string sourceKind,
        string sourceId,
        string factionId,
        string unitDefinitionId,
        int count,
        params (int X, int Y)[] placements)
    {
        BattleForceRequest force = new()
        {
            ForceId = forceId,
            SourceKind = sourceKind,
            SourceId = sourceId,
            FactionId = factionId,
            UnitDefinitionId = unitDefinitionId,
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

    private static void AddSurface(BattleStartSnapshot snapshot, int x, int y)
    {
        snapshot.LocationContext.NavigationSurfaces.Add(new BattleNavigationSurfaceSnapshot
        {
            X = x,
            Y = y,
            Height = 0,
            MoveCost = 1
        });
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

    private static void AssertSequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string message)
    {
        if (!expected.SequenceEqual(actual))
        {
            throw new Exception($"{message}: expected=[{string.Join(",", expected)}] actual=[{string.Join(",", actual)}]");
        }
    }
}
