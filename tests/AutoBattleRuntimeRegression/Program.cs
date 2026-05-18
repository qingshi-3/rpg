using Rpg.Application.Battle;
using Rpg.Application.Battle.Auto;
using Rpg.Application.World;

System.Environment.SetEnvironmentVariable(
    "RPG_GAMELOG_DIR",
    Path.Combine(Path.GetTempPath(), "rpg-auto-battle-runtime-tests"));

Run("auto battle spawns from preferred placements and returns victory force results", AutoBattleSpawnsFromPreferredPlacementsAndReturnsVictoryForceResults);
Run("auto battle returns defeat when player force is defeated", AutoBattleReturnsDefeatWhenPlayerForceIsDefeated);
Run("auto battle runtime stays isolated from legacy turn and world state owners", AutoBattleRuntimeStaysIsolatedFromLegacyTurnAndWorldStateOwners);
Run("auto battle session runner completes active handoff with force results", AutoBattleSessionRunnerCompletesActiveHandoffWithForceResults);
Run("auto battle session runner leaves active handoff when simulation cannot run", AutoBattleSessionRunnerLeavesActiveHandoffWhenSimulationCannotRun);
Run("auto battle report builder summarizes victory force results and event feed", AutoBattleReportBuilderSummarizesVictoryForceResultsAndEventFeed);
Run("auto battle report builder explains player defeat", AutoBattleReportBuilderExplainsPlayerDefeat);
Run("auto battle report summary formatter writes victory notice", AutoBattleReportSummaryFormatterWritesVictoryNotice);
Run("auto battle report summary formatter explains defeat reason", AutoBattleReportSummaryFormatterExplainsDefeatReason);
Run("auto battle runtime controller starts active battle and reveals report events", AutoBattleRuntimeControllerStartsActiveBattleAndRevealsReportEvents);
Run("auto battle runtime controller pause speed and skip control playback cursor", AutoBattleRuntimeControllerPauseSpeedAndSkipControlPlaybackCursor);
Run("auto battle runtime controller reports failed start without consuming handoff", AutoBattleRuntimeControllerReportsFailedStartWithoutConsumingHandoff);
Run("world site auto battle adapter resolves active handoff result and report", WorldSiteAutoBattleAdapterResolvesActiveHandoffResultAndReport);
Run("world site auto battle adapter preserves active handoff on failure", WorldSiteAutoBattleAdapterPreservesActiveHandoffOnFailure);

static void AutoBattleSpawnsFromPreferredPlacementsAndReturnsVictoryForceResults()
{
    BattleStartRequest request = BuildRequest(
        playerCount: 2,
        enemyCount: 1,
        playerPlacements: new[] { (0, 0, 0), (0, 1, 0) },
        enemyPlacements: new[] { (1, 0, 0) });

    AutoBattleSimulationResult simulation = new AutoBattleSimulation(new AutoBattleSimulationConfig
    {
        MaxTicks = 16,
        HealthPerUnit = 6,
        AttackDamage = 3,
        AttackRange = 1,
        AttackCooldownTicks = 1
    }).RunToEnd(request);

    AssertEqual(BattleOutcome.Victory, simulation.BattleResult.Outcome, "battle outcome");
    AssertEqual(request.RequestId, simulation.BattleResult.RequestId, "request id should be copied");
    AssertEqual(request.ContextId, simulation.BattleResult.ContextId, "context id should be copied");
    AssertEqual(request.BattleKind, simulation.BattleResult.BattleKind, "battle kind should be copied");
    AssertEqual(3, simulation.FinalState.Combatants.Count, "runtime combatant count");

    AssertSpawnedAt(simulation.Events, "player_force:0", "player_force", StrategicWorldIds.UnitMilitia, 0, 0, 0);
    AssertSpawnedAt(simulation.Events, "player_force:1", "player_force", StrategicWorldIds.UnitMilitia, 0, 1, 0);
    AssertSpawnedAt(simulation.Events, "enemy_force:0", "enemy_force", "skeleton", 1, 0, 0);
    AssertHasEvent(simulation.Events, AutoBattleEventKind.BattleStarted, "battle started event");
    AssertHasEvent(simulation.Events, AutoBattleEventKind.TargetAcquired, "target acquired event");
    AssertHasEvent(simulation.Events, AutoBattleEventKind.MovementStarted, "movement started event");
    AssertHasEvent(simulation.Events, AutoBattleEventKind.MovementCompleted, "movement completed event");
    AssertHasEvent(simulation.Events, AutoBattleEventKind.AttackResolved, "attack resolved event");
    AssertHasEvent(simulation.Events, AutoBattleEventKind.UnitDefeated, "unit defeated event");
    AssertHasEvent(simulation.Events, AutoBattleEventKind.BattleEnded, "battle ended event");

    BattleForceResult playerResult = FindForceResult(simulation.BattleResult, "player_force");
    AssertEqual(2, playerResult.InitialCount, "player initial count");
    AssertEqual(2, playerResult.SurvivedCount, "player survived count");
    AssertEqual(0, playerResult.DefeatedCount, "player defeated count");

    BattleForceResult enemyResult = FindForceResult(simulation.BattleResult, "enemy_force");
    AssertEqual(1, enemyResult.InitialCount, "enemy initial count");
    AssertEqual(0, enemyResult.SurvivedCount, "enemy survived count");
    AssertEqual(1, enemyResult.DefeatedCount, "enemy defeated count");
}

static void AutoBattleReturnsDefeatWhenPlayerForceIsDefeated()
{
    BattleStartRequest request = BuildRequest(
        playerCount: 1,
        enemyCount: 2,
        playerPlacements: new[] { (0, 0, 0) },
        enemyPlacements: new[] { (1, 0, 0), (1, 1, 0) });

    AutoBattleSimulationResult simulation = new AutoBattleSimulation(new AutoBattleSimulationConfig
    {
        MaxTicks = 16,
        HealthPerUnit = 4,
        AttackDamage = 2,
        AttackRange = 1,
        AttackCooldownTicks = 1
    }).RunToEnd(request);

    AssertEqual(BattleOutcome.Defeat, simulation.BattleResult.Outcome, "battle outcome");
    AssertHasEvent(simulation.Events, AutoBattleEventKind.BattleEnded, "battle ended event");

    BattleForceResult playerResult = FindForceResult(simulation.BattleResult, "player_force");
    AssertEqual(1, playerResult.InitialCount, "player initial count");
    AssertEqual(0, playerResult.SurvivedCount, "player survived count");
    AssertEqual(1, playerResult.DefeatedCount, "player defeated count");

    BattleForceResult enemyResult = FindForceResult(simulation.BattleResult, "enemy_force");
    AssertEqual(2, enemyResult.InitialCount, "enemy initial count");
    AssertEqual(1, enemyResult.SurvivedCount, "enemy survived count");
    AssertEqual(1, enemyResult.DefeatedCount, "enemy defeated count");
}

static void AutoBattleRuntimeStaysIsolatedFromLegacyTurnAndWorldStateOwners()
{
    string root = ProjectRoot();
    string autoBattlePath = Path.Combine(root, "src", "Application", "Battle", "Auto");
    AssertTrue(Directory.Exists(autoBattlePath), "auto battle application directory should exist");

    string combinedSource = string.Join(
        "\n",
        Directory.GetFiles(autoBattlePath, "*.cs", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(File.ReadAllText));

    AssertTrue(!combinedSource.Contains("BattleTurnController", StringComparison.Ordinal), "auto battle runtime should not reference BattleTurnController");
    AssertTrue(!combinedSource.Contains("TurnSystem", StringComparison.Ordinal), "auto battle runtime should not reference TurnSystem");
    AssertTrue(!combinedSource.Contains("ActionPoint", StringComparison.Ordinal), "auto battle runtime should not reference AP components");
    AssertTrue(!combinedSource.Contains("StrategicWorldState", StringComparison.Ordinal), "auto battle runtime should not mutate strategic world state");
    AssertTrue(!combinedSource.Contains("WorldSiteRoot", StringComparison.Ordinal), "auto battle runtime should not reference WorldSiteRoot");
    AssertTrue(!combinedSource.Contains("BattleActionMenu", StringComparison.Ordinal), "auto battle runtime should not reference manual battle action menu");
    AssertTrue(!combinedSource.Contains("Godot.Control", StringComparison.Ordinal), "auto battle runtime should not reference Godot UI controls");
    AssertTrue(!combinedSource.Contains(" : Node", StringComparison.Ordinal), "auto battle runtime should not define Godot nodes");
}

static void AutoBattleSessionRunnerCompletesActiveHandoffWithForceResults()
{
    BattleSessionHandoff.CancelBattle();
    BattleStartRequest request = BuildRequest(
        playerCount: 2,
        enemyCount: 1,
        playerPlacements: new[] { (0, 0, 0), (0, 1, 0) },
        enemyPlacements: new[] { (1, 0, 0) });
    request.RequestId = "request_auto_session_success";

    BattleSessionHandoff.BeginBattle(request);
    AutoBattleSessionRunner runner = new(new AutoBattleSimulation(new AutoBattleSimulationConfig
    {
        MaxTicks = 16,
        HealthPerUnit = 6,
        AttackDamage = 3,
        AttackRange = 1,
        AttackCooldownTicks = 1
    }));

    bool completed = runner.TryRunActiveBattle(out AutoBattleSimulationResult? simulation, out string failureReason);

    AssertTrue(completed, $"runner should complete active handoff failure={failureReason}");
    AssertTrue(simulation != null, "simulation result should be returned");
    AssertEqual("", failureReason, "failure reason");
    AssertTrue(!BattleSessionHandoff.HasActiveLaunch, "active handoff should be cleared");
    AssertTrue(
        BattleSessionHandoff.TryConsumeLastBattleResult(out BattleStartRequest consumedRequest, out BattleResult consumedResult),
        "completed auto battle result should be consumable");
    AssertTrue(object.ReferenceEquals(request, consumedRequest), "consumed request should be active request");
    AssertTrue(object.ReferenceEquals(simulation!.BattleResult, consumedResult), "handoff should preserve the simulation BattleResult instance");
    AssertEqual(BattleOutcome.Victory, consumedResult.Outcome, "consumed outcome");

    BattleForceResult playerResult = FindForceResult(consumedResult, "player_force");
    BattleForceResult enemyResult = FindForceResult(consumedResult, "enemy_force");
    AssertEqual(2, playerResult.SurvivedCount, "player survived count");
    AssertEqual(0, enemyResult.SurvivedCount, "enemy survived count");
    AssertEqual(1, enemyResult.DefeatedCount, "enemy defeated count");
}

static void AutoBattleSessionRunnerLeavesActiveHandoffWhenSimulationCannotRun()
{
    BattleSessionHandoff.CancelBattle();
    BattleStartRequest request = BuildRequest(
        playerCount: 2,
        enemyCount: 1,
        playerPlacements: new[] { (0, 0, 0) },
        enemyPlacements: new[] { (1, 0, 0) });
    request.RequestId = "request_auto_session_failed";

    BattleSessionHandoff.BeginBattle(request);
    AutoBattleSessionRunner runner = new(new AutoBattleSimulation(new AutoBattleSimulationConfig
    {
        MaxTicks = 16,
        HealthPerUnit = 6,
        AttackDamage = 3,
        AttackRange = 1,
        AttackCooldownTicks = 1
    }));

    bool completed = runner.TryRunActiveBattle(out AutoBattleSimulationResult? simulation, out string failureReason);

    AssertTrue(!completed, "runner should not complete invalid simulation");
    AssertTrue(simulation == null, "failed run should not return a simulation result");
    AssertTrue(
        failureReason.Contains("auto_battle_missing_preferred_placement", StringComparison.Ordinal),
        $"failure reason should expose authoritative deployment problem actual={failureReason}");
    AssertTrue(BattleSessionHandoff.HasActiveLaunch, "active handoff should remain unresolved after failed simulation");
    AssertTrue(
        !BattleSessionHandoff.TryConsumeLastBattleResult(out _, out _),
        "failed auto battle should not produce a consumable result");

    BattleSessionHandoff.CancelBattle();
}

static void AutoBattleReportBuilderSummarizesVictoryForceResultsAndEventFeed()
{
    BattleStartRequest request = BuildRequest(
        playerCount: 2,
        enemyCount: 1,
        playerPlacements: new[] { (0, 0, 0), (0, 1, 0) },
        enemyPlacements: new[] { (1, 0, 0) });

    AutoBattleSimulationResult simulation = new AutoBattleSimulation(new AutoBattleSimulationConfig
    {
        MaxTicks = 16,
        HealthPerUnit = 6,
        AttackDamage = 3,
        AttackRange = 1,
        AttackCooldownTicks = 1
    }).RunToEnd(request);

    AutoBattleReport report = new AutoBattleReportBuilder().Build(simulation);

    AssertEqual(BattleOutcome.Victory, report.Outcome, "report outcome");
    AssertEqual(3, report.InitialUnitCount, "report initial unit count");
    AssertEqual(2, report.SurvivedUnitCount, "report survived unit count");
    AssertEqual(1, report.DefeatedUnitCount, "report defeated unit count");
    AssertEqual("", report.TopFailureReason, "victory should not have failure reason");

    AutoBattleForceReport player = FindForceReport(report, "player_force");
    AssertEqual(2, player.InitialCount, "player initial count");
    AssertEqual(2, player.SurvivedCount, "player survived count");
    AssertEqual(0, player.DefeatedCount, "player defeated count");
    AssertTrue(player.DamageDealt > 0, "player damage dealt should be counted");
    AssertTrue(player.AttackCount > 0, "player attacks should be counted");

    AutoBattleForceReport enemy = FindForceReport(report, "enemy_force");
    AssertEqual(1, enemy.InitialCount, "enemy initial count");
    AssertEqual(0, enemy.SurvivedCount, "enemy survived count");
    AssertEqual(1, enemy.DefeatedCount, "enemy defeated count");

    AssertFeedHasSummaryKey(report, "battle_started");
    AssertFeedHasSummaryKey(report, "unit_deployed");
    AssertFeedHasSummaryKey(report, "attack_resolved");
    AssertFeedHasSummaryKey(report, "unit_defeated");
    AssertFeedHasSummaryKey(report, "battle_ended");
    AssertFeedMissingSummaryKey(report, "movement_started");
    AssertFeedMissingSummaryKey(report, "movement_completed");
}

static void AutoBattleReportBuilderExplainsPlayerDefeat()
{
    BattleStartRequest request = BuildRequest(
        playerCount: 1,
        enemyCount: 2,
        playerPlacements: new[] { (0, 0, 0) },
        enemyPlacements: new[] { (1, 0, 0), (1, 1, 0) });

    AutoBattleSimulationResult simulation = new AutoBattleSimulation(new AutoBattleSimulationConfig
    {
        MaxTicks = 16,
        HealthPerUnit = 4,
        AttackDamage = 2,
        AttackRange = 1,
        AttackCooldownTicks = 1
    }).RunToEnd(request);

    AutoBattleReport report = new AutoBattleReportBuilder().Build(simulation);

    AssertEqual(BattleOutcome.Defeat, report.Outcome, "report outcome");
    AssertEqual("player_force_eliminated", report.TopFailureReason, "defeat failure reason");

    AutoBattleForceReport player = FindForceReport(report, "player_force");
    AssertEqual(0, player.SurvivedCount, "player survived count");
    AssertEqual(1, player.DefeatedCount, "player defeated count");

    AutoBattleForceReport enemy = FindForceReport(report, "enemy_force");
    AssertEqual(1, enemy.SurvivedCount, "enemy survived count");
    AssertEqual(1, enemy.DefeatedCount, "enemy defeated count");
}

static void AutoBattleReportSummaryFormatterWritesVictoryNotice()
{
    AutoBattleReport report = new()
    {
        Outcome = BattleOutcome.Victory,
        InitialUnitCount = 3,
        SurvivedUnitCount = 2,
        DefeatedUnitCount = 1
    };
    report.ForceReports.Add(new AutoBattleForceReport
    {
        ForceId = "player_force",
        SourceKind = "PlayerArmy",
        InitialCount = 2,
        SurvivedCount = 2,
        DefeatedCount = 0,
        AttackCount = 3,
        DamageDealt = 6,
        UnitsDefeated = 1
    });
    report.ForceReports.Add(new AutoBattleForceReport
    {
        ForceId = "enemy_force",
        SourceKind = "DefenderSite",
        InitialCount = 1,
        SurvivedCount = 0,
        DefeatedCount = 1,
        AttackCount = 1,
        DamageDealt = 2,
        UnitsDefeated = 0
    });

    string summary = new AutoBattleReportSummaryFormatter().Format(report);

    AssertTrue(summary.Contains("自动战斗胜利", StringComparison.Ordinal), $"summary should include victory label actual={summary}");
    AssertTrue(summary.Contains("参战 3", StringComparison.Ordinal), $"summary should include initial count actual={summary}");
    AssertTrue(summary.Contains("生还 2", StringComparison.Ordinal), $"summary should include survived count actual={summary}");
    AssertTrue(summary.Contains("战损 1", StringComparison.Ordinal), $"summary should include defeated count actual={summary}");
    AssertTrue(summary.Contains("主要贡献：player_force 造成 6 伤害，击败 1。", StringComparison.Ordinal), $"summary should include contribution actual={summary}");
}

static void AutoBattleReportSummaryFormatterExplainsDefeatReason()
{
    AutoBattleReport report = new()
    {
        Outcome = BattleOutcome.Defeat,
        InitialUnitCount = 3,
        SurvivedUnitCount = 1,
        DefeatedUnitCount = 2,
        TopFailureReason = "player_force_eliminated"
    };
    report.ForceReports.Add(new AutoBattleForceReport
    {
        ForceId = "enemy_force",
        SourceKind = "DefenderSite",
        InitialCount = 2,
        SurvivedCount = 1,
        DefeatedCount = 1,
        AttackCount = 4,
        DamageDealt = 8,
        UnitsDefeated = 2
    });

    string summary = new AutoBattleReportSummaryFormatter().Format(report);

    AssertTrue(summary.Contains("自动战斗失败", StringComparison.Ordinal), $"summary should include defeat label actual={summary}");
    AssertTrue(summary.Contains("我方战斗单位全部失去战斗力", StringComparison.Ordinal), $"summary should explain player force elimination actual={summary}");
    AssertTrue(summary.Contains("主要贡献：enemy_force 造成 8 伤害，击败 2。", StringComparison.Ordinal), $"summary should include top contribution actual={summary}");
}

static void AutoBattleRuntimeControllerStartsActiveBattleAndRevealsReportEvents()
{
    BattleSessionHandoff.CancelBattle();
    BattleStartRequest request = BuildRequest(
        playerCount: 2,
        enemyCount: 1,
        playerPlacements: new[] { (0, 0, 0), (0, 1, 0) },
        enemyPlacements: new[] { (1, 0, 0) });
    BattleSessionHandoff.BeginBattle(request);

    AutoBattleRuntimeController controller = BuildRuntimeController();
    bool started = controller.StartActiveBattle(out string failureReason);

    AssertTrue(started, $"controller should start active battle failure={failureReason}");
    AssertEqual("", failureReason, "failure reason");
    AssertEqual(AutoBattleRuntimePhase.Playing, controller.Phase, "phase after start");
    AssertTrue(controller.SimulationResult != null, "simulation result should be stored");
    AssertTrue(controller.Report != null, "report should be stored");
    AssertTrue(!BattleSessionHandoff.HasActiveLaunch, "active handoff should be consumed after successful start");
    AssertEqual(0, controller.VisibleEventCount, "visible feed count after start");

    controller.AdvancePlayback(1.0);

    AssertTrue(controller.VisibleEventCount >= 2, "one second should reveal at least two events with 0.5s event cadence");
    AssertTrue(controller.VisibleEventFeed.Count == controller.VisibleEventCount, "visible feed count should match feed view");

    controller.AdvancePlayback(99.0);

    AssertEqual(AutoBattleRuntimePhase.Completed, controller.Phase, "phase after playback finishes");
    AssertEqual(controller.Report!.EventFeed.Count, controller.VisibleEventCount, "all events should be visible at completion");

    BattleSessionHandoff.TryConsumeLastBattleResult(out _, out _);
}

static void AutoBattleRuntimeControllerPauseSpeedAndSkipControlPlaybackCursor()
{
    BattleSessionHandoff.CancelBattle();
    BattleStartRequest request = BuildRequest(
        playerCount: 2,
        enemyCount: 1,
        playerPlacements: new[] { (0, 0, 0), (0, 1, 0) },
        enemyPlacements: new[] { (1, 0, 0) });
    BattleSessionHandoff.BeginBattle(request);

    AutoBattleRuntimeController controller = BuildRuntimeController();
    AssertTrue(controller.StartActiveBattle(out string failureReason), $"controller start failure={failureReason}");

    controller.AdvancePlayback(0.5);
    int afterFirstAdvance = controller.VisibleEventCount;
    AssertTrue(afterFirstAdvance > 0, "initial advance should reveal events");

    controller.Pause();
    AssertEqual(AutoBattleRuntimePhase.Paused, controller.Phase, "phase after pause");
    controller.AdvancePlayback(10.0);
    AssertEqual(afterFirstAdvance, controller.VisibleEventCount, "paused playback should not reveal events");

    controller.Resume();
    controller.SetPlaybackSpeed(2.0);
    controller.AdvancePlayback(0.5);
    AssertTrue(controller.VisibleEventCount >= afterFirstAdvance + 2, "2x speed should reveal at least two additional events");

    controller.SkipToEnd();
    AssertEqual(AutoBattleRuntimePhase.Completed, controller.Phase, "phase after skip");
    AssertEqual(controller.Report!.EventFeed.Count, controller.VisibleEventCount, "skip should reveal all events");

    BattleSessionHandoff.TryConsumeLastBattleResult(out _, out _);
}

static void AutoBattleRuntimeControllerReportsFailedStartWithoutConsumingHandoff()
{
    BattleSessionHandoff.CancelBattle();
    BattleStartRequest request = BuildRequest(
        playerCount: 2,
        enemyCount: 1,
        playerPlacements: new[] { (0, 0, 0) },
        enemyPlacements: new[] { (1, 0, 0) });
    BattleSessionHandoff.BeginBattle(request);

    AutoBattleRuntimeController controller = BuildRuntimeController();
    bool started = controller.StartActiveBattle(out string failureReason);

    AssertTrue(!started, "invalid request should fail controller start");
    AssertEqual(AutoBattleRuntimePhase.Failed, controller.Phase, "phase after failed start");
    AssertTrue(
        failureReason.Contains("auto_battle_missing_preferred_placement", StringComparison.Ordinal),
        $"failure reason should expose missing placement actual={failureReason}");
    AssertEqual(failureReason, controller.FailureReason, "controller failure reason");
    AssertTrue(controller.Report == null, "failed start should not build report");
    AssertTrue(controller.SimulationResult == null, "failed start should not store simulation");
    AssertTrue(BattleSessionHandoff.HasActiveLaunch, "failed start should not consume active handoff");

    BattleSessionHandoff.CancelBattle();
}

static void WorldSiteAutoBattleAdapterResolvesActiveHandoffResultAndReport()
{
    BattleSessionHandoff.CancelBattle();
    BattleStartRequest request = BuildRequest(
        playerCount: 2,
        enemyCount: 1,
        playerPlacements: new[] { (0, 0, 0), (0, 1, 0) },
        enemyPlacements: new[] { (1, 0, 0) });
    BattleSessionHandoff.BeginBattle(request);

    WorldSiteAutoBattleAdapter adapter = new(BuildRuntimeController());
    bool resolved = adapter.TryResolveActiveBattle(out WorldSiteAutoBattleResolveResult result);

    AssertTrue(resolved, $"adapter should resolve active handoff failure={result.FailureReason}");
    AssertTrue(result.Success, "result success");
    AssertTrue(object.ReferenceEquals(request, result.Request), "adapter should return consumed request");
    AssertEqual(BattleOutcome.Victory, result.BattleResult.Outcome, "battle outcome");
    AssertEqual(BattleOutcome.Victory, result.Report.Outcome, "report outcome");
    AssertEqual(2, FindForceResult(result.BattleResult, "player_force").SurvivedCount, "player survived count");
    AssertEqual(1, FindForceResult(result.BattleResult, "enemy_force").DefeatedCount, "enemy defeated count");
    AssertTrue(result.RuntimeController != null, "runtime controller should be exposed for playback state");
    AssertTrue(!BattleSessionHandoff.HasActiveLaunch, "active handoff should be cleared after adapter success");
    AssertTrue(
        !BattleSessionHandoff.TryConsumeLastBattleResult(out _, out _),
        "adapter should consume completed handoff result");
}

static void WorldSiteAutoBattleAdapterPreservesActiveHandoffOnFailure()
{
    BattleSessionHandoff.CancelBattle();
    BattleStartRequest request = BuildRequest(
        playerCount: 2,
        enemyCount: 1,
        playerPlacements: new[] { (0, 0, 0) },
        enemyPlacements: new[] { (1, 0, 0) });
    BattleSessionHandoff.BeginBattle(request);

    WorldSiteAutoBattleAdapter adapter = new(BuildRuntimeController());
    bool resolved = adapter.TryResolveActiveBattle(out WorldSiteAutoBattleResolveResult result);

    AssertTrue(!resolved, "adapter should not resolve invalid handoff");
    AssertTrue(!result.Success, "result success");
    AssertTrue(
        result.FailureReason.Contains("auto_battle_missing_preferred_placement", StringComparison.Ordinal),
        $"failure reason should expose missing placement actual={result.FailureReason}");
    AssertTrue(result.Request == null, "failed adapter result should not include request");
    AssertTrue(result.BattleResult == null, "failed adapter result should not include battle result");
    AssertTrue(result.Report == null, "failed adapter result should not include report");
    AssertTrue(BattleSessionHandoff.HasActiveLaunch, "failed adapter should preserve active handoff");

    BattleSessionHandoff.CancelBattle();
}

static BattleStartRequest BuildRequest(
    int playerCount,
    int enemyCount,
    IReadOnlyList<(int X, int Y, int Height)> playerPlacements,
    IReadOnlyList<(int X, int Y, int Height)> enemyPlacements)
{
    BattleStartRequest request = new()
    {
        RequestId = "request_auto_battle",
        ContextId = "context_auto_battle",
        BattleKind = BattleKind.AssaultSite,
        AttackerFactionId = "player",
        DefenderFactionId = "enemy",
        TargetSiteId = "site_under_test",
        MapDefinitionId = "site_battlefield"
    };

    BattleForceRequest playerForce = new()
    {
        ForceId = "player_force",
        SourceKind = "PlayerArmy",
        SourceId = "army_player",
        UnitDefinitionId = StrategicWorldIds.UnitMilitia,
        Count = playerCount,
        FactionId = "player"
    };
    AddPlacements(playerForce, "player", playerPlacements);
    request.PlayerForces.Add(playerForce);

    BattleForceRequest enemyForce = new()
    {
        ForceId = "enemy_force",
        SourceKind = "DefenderSite",
        SourceId = "site_under_test",
        UnitDefinitionId = "skeleton",
        Count = enemyCount,
        FactionId = "enemy"
    };
    AddPlacements(enemyForce, "enemy", enemyPlacements);
    request.EnemyForces.Add(enemyForce);

    return request;
}

static void AddPlacements(
    BattleForceRequest force,
    string prefix,
    IReadOnlyList<(int X, int Y, int Height)> placements)
{
    for (int index = 0; index < placements.Count; index++)
    {
        (int x, int y, int height) = placements[index];
        force.PreferredPlacements.Add(new BattleForcePlacementRequest
        {
            PlacementId = $"{prefix}_placement_{index}",
            CellX = x,
            CellY = y,
            CellHeight = height
        });
    }
}

static void AssertSpawnedAt(
    IReadOnlyList<AutoBattleEvent> events,
    string actorId,
    string forceId,
    string unitDefinitionId,
    int cellX,
    int cellY,
    int cellHeight)
{
    AutoBattleEvent? spawned = events.FirstOrDefault(item =>
        item.Kind == AutoBattleEventKind.UnitSpawned &&
        item.ActorId == actorId);
    AssertTrue(spawned != null, $"missing spawn event for {actorId}");
    AssertEqual(forceId, spawned!.ForceId, $"spawn force for {actorId}");
    AssertEqual(unitDefinitionId, spawned.UnitDefinitionId, $"spawn unit for {actorId}");
    AssertEqual(cellX, spawned.CellX, $"spawn cell x for {actorId}");
    AssertEqual(cellY, spawned.CellY, $"spawn cell y for {actorId}");
    AssertEqual(cellHeight, spawned.CellHeight, $"spawn cell height for {actorId}");
}

static BattleForceResult FindForceResult(BattleResult result, string forceId)
{
    BattleForceResult? forceResult = result.ForceResults.FirstOrDefault(item => item.ForceId == forceId);
    if (forceResult == null)
    {
        throw new InvalidOperationException($"Missing force result {forceId}");
    }

    return forceResult;
}

static AutoBattleForceReport FindForceReport(AutoBattleReport report, string forceId)
{
    AutoBattleForceReport? forceReport = report.ForceReports.FirstOrDefault(item => item.ForceId == forceId);
    if (forceReport == null)
    {
        throw new InvalidOperationException($"Missing force report {forceId}");
    }

    return forceReport;
}

static void AssertHasEvent(IReadOnlyList<AutoBattleEvent> events, AutoBattleEventKind kind, string message)
{
    AssertTrue(events.Any(item => item.Kind == kind), message);
}

static void AssertFeedHasSummaryKey(AutoBattleReport report, string summaryKey)
{
    AssertTrue(
        report.EventFeed.Any(item => item.SummaryKey == summaryKey),
        $"missing report feed summary key {summaryKey}");
}

static void AssertFeedMissingSummaryKey(AutoBattleReport report, string summaryKey)
{
    AssertTrue(
        !report.EventFeed.Any(item => item.SummaryKey == summaryKey),
        $"unexpected report feed summary key {summaryKey}");
}

static AutoBattleRuntimeController BuildRuntimeController()
{
    AutoBattleSimulation simulation = new(new AutoBattleSimulationConfig
    {
        MaxTicks = 16,
        HealthPerUnit = 6,
        AttackDamage = 3,
        AttackRange = 1,
        AttackCooldownTicks = 1
    });
    return new AutoBattleRuntimeController(
        new AutoBattleSessionRunner(simulation),
        new AutoBattleReportBuilder(),
        new AutoBattleRuntimeControllerConfig
        {
            SecondsPerReportEvent = 0.5
        });
}

static string ProjectRoot()
{
    DirectoryInfo? directory = new(AppContext.BaseDirectory);
    while (directory != null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "rpg.csproj")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException("Could not locate project root from test output directory.");
}

static void Run(string name, Action test)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"FAIL {name}: {exception.Message}");
        System.Environment.ExitCode = 1;
    }
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{message}: expected={expected} actual={actual}");
    }
}
