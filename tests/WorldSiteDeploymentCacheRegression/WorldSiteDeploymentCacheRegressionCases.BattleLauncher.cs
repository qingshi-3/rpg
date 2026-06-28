using Godot;
using Rpg.Application.Battle;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.Battle.Grid;
using Rpg.Domain.World;

internal static partial class WorldSiteDeploymentCacheRegressionCases
{
internal static void BattleLauncherCancelsHandoffAndRestoresSiteOnActivationFailure()
{
    BattleSessionHandoff.CancelBattle();
    StrategicWorldState state = new() { WorldTick = 7 };
    WorldSiteState site = BuildDeploymentSite();
    site.SiteMode = WorldSiteMode.Peacetime;
    state.SiteStates[site.SiteId] = site;

    WorldSiteBattleLauncher launcher = new();
    WorldSiteBattleLaunchRollback rollback = launcher.CaptureRollback(site);
    site.SiteMode = WorldSiteMode.Wartime;

    int applyCalls = 0;
    int cleanupCalls = 0;
    int runtimeDisableCalls = 0;
    WorldSiteBattleLaunchResult result = launcher.BeginAndActivate(
        state,
        new BattleStartRequest { RequestId = "request_under_test", TargetSiteId = site.SiteId },
        rollback,
        () => applyCalls++,
        () => false,
        () => "activation_blocked",
        () => cleanupCalls++,
        () => cleanupCalls++,
        enabled =>
        {
            if (!enabled)
            {
                runtimeDisableCalls++;
            }
        });

    AssertTrue(!result.Success, "failed activation should return failure");
    AssertEqual("activation_blocked", result.FailureReason, "failure reason");
    AssertTrue(!BattleSessionHandoff.HasActiveLaunch, "failed launch should cancel active handoff");
    AssertEqual(1, applyCalls, "apply start request call count");
    AssertEqual(2, cleanupCalls, "cleanup callbacks should run during rollback");
    AssertEqual(1, runtimeDisableCalls, "runtime should be disabled during rollback");
    AssertEqual(WorldSiteMode.Peacetime, site.SiteMode, "site mode should be restored");

    BattleSessionHandoff.CancelBattle();
}

internal static void BattleGroupSessionProbeRunsWithoutConsumingLegacyHandoff()
{
    BattleSessionHandoff.CancelBattle();
    BattleStartRequest legacyRequest = BuildProbeBattleRequest("probe_request", "probe_battle", "site_under_test");
    BattleSessionHandoff.BeginBattle(legacyRequest);

    BattleGroupSessionProbeResult probe = new BattleGroupSessionProbeService().Probe(legacyRequest);

    AssertTrue(probe.Success, $"probe should accept legacy request failure={probe.FailureReason}");
    AssertEqual("snapshot:probe_request", probe.Snapshot.SnapshotId, "probe snapshot id");
    AssertEqual("probe_battle", probe.Snapshot.BattleId, "probe battle id");
    AssertEqual("site_under_test", probe.Snapshot.TargetLocationId, "probe target location id");
    AssertEqual("site_under_test", probe.Snapshot.LocationContext.LocationId, "probe location context id");
    AssertEqual(2, probe.Snapshot.BattleGroups.Count, "probe battle group count");
    AssertEqual("player_camp", probe.Snapshot.BattleGroups[0].SourceLocationId, "probe source location id");
    AssertTrue(probe.FlowResult.SettlementPlan.Accepted, "probe settlement should accept");
    AssertEqual("probe_battle", probe.FlowResult.Report.BattleId, "probe report battle id");
    AssertTrue(BattleSessionHandoff.HasActiveLaunch, "probe should not consume active legacy handoff");
    AssertTrue(BattleSessionHandoff.TryPeekActiveRequest(out BattleStartRequest active), "active handoff should remain peekable");
    AssertEqual(legacyRequest.RequestId, active.RequestId, "active request remains unchanged");
    AssertTrue(ReferenceEquals(legacyRequest, active), "probe should not replace active request object");

    BattleSessionHandoff.CancelBattle();
}

internal static void BattleLauncherRunsBattleGroupSessionProbeBesideLegacyHandoff()
{
    BattleSessionHandoff.CancelBattle();
    StrategicWorldState state = new() { WorldTick = 8 };
    WorldSiteState site = BuildDeploymentSite();
    state.SiteStates[site.SiteId] = site;

    WorldSiteBattleLauncher launcher = new(
        battleGroupSessionProbe: new BattleGroupSessionProbeService());
    BattleStartRequest request = BuildProbeBattleRequest("launcher_probe_request", "launcher_probe_battle", site.SiteId);
    int applyCalls = 0;

    WorldSiteBattleLaunchResult result = launcher.BeginAndActivate(
        state,
        request,
        launcher.CaptureRollback(site),
        () => applyCalls++,
        () => true,
        () => "",
        null,
        null,
        null);

    AssertTrue(result.Success, "legacy launch should still succeed");
    AssertEqual(1, applyCalls, "legacy apply start request should run once");
    AssertTrue(result.ProbeResult != null, "probe result should be exposed on launch result");
    BattleGroupSessionProbeResult probeResult = result.ProbeResult!;
    AssertTrue(probeResult.Success, $"probe should succeed failure={probeResult.FailureReason}");
    AssertEqual("snapshot:launcher_probe_request", probeResult.Snapshot.SnapshotId, "launcher probe snapshot id");
    AssertTrue(BattleSessionHandoff.HasActiveLaunch, "successful legacy launch should keep active handoff");
    AssertTrue(BattleSessionHandoff.TryPeekActiveRequest(out BattleStartRequest active), "active request should remain available");
    AssertEqual(request.RequestId, active.RequestId, "probe should not replace active request");

    BattleSessionHandoff.CancelBattle();
}

internal static void BattleLauncherKeepsLegacyHandoffWhenProbeRejectsSnapshot()
{
    BattleSessionHandoff.CancelBattle();
    StrategicWorldState state = new() { WorldTick = 9 };
    WorldSiteState site = BuildDeploymentSite();
    state.SiteStates[site.SiteId] = site;

    WorldSiteBattleLauncher launcher = new(
        battleGroupSessionProbe: new BattleGroupSessionProbeService());
    BattleStartRequest request = new()
    {
        RequestId = "launcher_probe_reject_request",
        ContextId = "launcher_probe_reject_battle",
        SourceSiteId = "player_camp",
        TargetSiteId = site.SiteId,
        BattleKind = BattleKind.AssaultSite
    };
    int applyCalls = 0;

    WorldSiteBattleLaunchResult result = launcher.BeginAndActivate(
        state,
        request,
        launcher.CaptureRollback(site),
        () => applyCalls++,
        () => true,
        () => "",
        null,
        null,
        null);

    AssertTrue(result.Success, "legacy launch should still succeed when probe rejects");
    AssertEqual(1, applyCalls, "legacy apply start request should run once with rejected probe");
    AssertTrue(result.ProbeResult != null, "probe result should exist on rejected probe");
    BattleGroupSessionProbeResult probeResult = result.ProbeResult!;
    AssertTrue(!probeResult.Success, "probe should reject request without player forces");
    AssertEqual("battle_result_incomplete", probeResult.FailureReason, "probe rejection reason");
    AssertEqual(0, probeResult.Snapshot.BattleGroups.Count, "rejected probe snapshot should have zero groups");
    AssertTrue(
        probeResult.FlowResult.RuntimeResult.EventStream.Events.Any(item => item.ReasonCode == "battle_snapshot_invalid"),
        "rejected probe should expose target architecture rejection event");
    AssertTrue(BattleSessionHandoff.HasActiveLaunch, "rejected probe should not cancel legacy handoff");
    AssertTrue(BattleSessionHandoff.TryPeekActiveRequest(out BattleStartRequest active), "active request should remain after rejected probe");
    AssertEqual(request.RequestId, active.RequestId, "rejected probe should keep active request id");
    AssertTrue(ReferenceEquals(request, active), "rejected probe should not replace active request object");

    BattleSessionHandoff.CancelBattle();
}

internal static BattleStartRequest BuildProbeBattleRequest(string requestId, string battleId, string siteId)
{
    BattleStartRequest request = new()
    {
        RequestId = requestId,
        ContextId = battleId,
        SourceSiteId = "player_camp",
        TargetSiteId = siteId,
        BattleKind = BattleKind.AssaultSite
    };
    request.PlayerForces.Add(new BattleForceRequest
    {
        ForceId = "player_force",
        SourceKind = "PlayerArmy",
        SourceId = "army_1",
        UnitDefinitionId = StrategicWorldIds.UnitMilitia,
        Count = 1,
        FactionId = "player",
        MaxHitPoints = 20,
        AttackDamage = 5,
        AttackRange = 1,
        AttackSpeed = 1,
        MoveStepSeconds = 0.2,
        AttackActionSeconds = 0.4,
        AttackImpactDelaySeconds = 0.2,
        FootprintWidth = 1,
        FootprintHeight = 1
    });
    request.EnemyForces.Add(new BattleForceRequest
    {
        ForceId = "enemy_force",
        UnitDefinitionId = StrategicWorldIds.UnitMilitia,
        Count = 1,
        FactionId = "enemy",
        MaxHitPoints = 20,
        AttackDamage = 5,
        AttackRange = 1,
        AttackSpeed = 1,
        MoveStepSeconds = 0.2,
        AttackActionSeconds = 0.4,
        AttackImpactDelaySeconds = 0.2,
        FootprintWidth = 1,
        FootprintHeight = 1,
        PreferredPlacements =
        {
            new BattleForcePlacementRequest
            {
                PlacementId = "enemy_force:preferred",
                CellX = 3,
                CellY = 0,
                CellHeight = 0
            }
        }
    });
    AttachFlatRequestTopology(request);
    return request;
}

internal static GridCellSurface AddWalkableSurface(
    BattleGridMap grid,
    int x,
    int y,
    int height = 0,
    string terrainTag = "plain")
{
    GridCellSurface surface = grid.GetOrCreateSurface(new GridPosition(x, y), height);
    surface.AddLayer(new GridCellLayerData(
        "foundation",
        LayerRole.Foundation,
        height,
        affectsWalkability: true,
        affectsLineOfSight: false,
        isHeightTransitionLayer: false,
        isVisualOnly: false,
        walkable: true,
        moveCost: 1,
        canStandOn: true,
        isObstacle: false,
        terrainTag,
        sourceId: 0,
        atlasX: 0,
        atlasY: 0,
        alternativeTile: 0));
    return surface;
}
}
