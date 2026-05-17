using Rpg.Application.Battle;
using Rpg.Application.Battle.Adapters;
using Rpg.Application.Battle.Commands;
using Rpg.Application.Battle.Reports;
using Rpg.Application.Battle.Settlement;
using Rpg.Application.Battle.Snapshots;
using Rpg.Application.BattleGroups;
using Rpg.Domain.BattleGroups;
using Rpg.Domain.Corps;
using Rpg.Domain.Heroes;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Results;

Environment.SetEnvironmentVariable("RPG_GAMELOG_DIR", Path.Combine(Path.GetTempPath(), "rpg-test-logs"));

Run("corps strength clamps and visible soldiers are derived", CorpsStrengthClampsAndVisibleSoldiersAreDerived);
Run("runtime source stays isolated from domain and presentation owners", RuntimeSourceStaysIsolated);
Run("runtime rejects invalid battle handoff", RuntimeRejectsInvalidBattleHandoff);
Run("domain source stays isolated from runtime and Godot scene nodes", DomainSourceStaysIsolated);
Run("snapshot copies battle group facts", SnapshotCopiesBattleGroupFacts);
Run("battle group lifecycle rejects invalid identities", BattleGroupLifecycleRejectsInvalidIdentities);
Run("battle group lifecycle preserves state on invalid lock", BattleGroupLifecyclePreservesStateOnInvalidLock);
Run("battle group lifecycle releases only active battle groups", BattleGroupLifecycleReleasesOnlyActiveBattleGroups);
Run("command validation distinguishes application rejection", CommandValidationDistinguishesApplicationRejection);
Run("settlement rejects incomplete result", SettlementRejectsIncompleteResult);
Run("report and settlement consume the same event ids", ReportAndSettlementConsumeSameEventIds);
Run("legacy garrison adapter creates explicit battle groups", LegacyGarrisonAdapterCreatesExplicitBattleGroups);
Run("legacy result adapter preserves request and outcome ids", LegacyResultAdapterPreservesRequestAndOutcomeIds);
Run("legacy result adapter maps failed handoff to disaster", LegacyResultAdapterMapsFailedHandoffToDisaster);
Run("battle group vertical slice settles and reports from runtime facts", BattleGroupVerticalSliceSettlesAndReports);

static void CorpsStrengthClampsAndVisibleSoldiersAreDerived()
{
    CorpsState corps = new() { CorpsId = "corps_1", CorpsDefinitionId = "shield", CorpsStrength = 140 };
    corps.ClampStrength();
    AssertEqual(100, corps.CorpsStrength, "strength upper clamp");
    corps.CorpsStrength = -8;
    corps.ClampStrength();
    AssertEqual(0, corps.CorpsStrength, "strength lower clamp");
    corps.CorpsStrength = 80;
    AssertEqual(4, CorpsStrengthPolicy.CalculateVisibleSoldiers(corps.CorpsStrength, 5), "derived visible soldiers");
}

static void RuntimeSourceStaysIsolated()
{
    string source = CombinedSource("src", "Runtime", "Battle");
    AssertTrue(!source.Contains("StrategicWorldState", StringComparison.Ordinal), "runtime must not reference StrategicWorldState");
    AssertTrue(!source.Contains("WorldSiteRoot", StringComparison.Ordinal), "runtime must not reference WorldSiteRoot");
    AssertTrue(!source.Contains("Godot.Control", StringComparison.Ordinal), "runtime must not reference Godot UI controls");
    AssertTrue(!source.Contains("Rpg.Domain", StringComparison.Ordinal), "runtime must not reference Domain owners");
    AssertTrue(!source.Contains("Rpg.Presentation", StringComparison.Ordinal), "runtime must not reference Presentation owners");
    AssertTrue(!source.Contains("using Godot", StringComparison.Ordinal), "runtime must not reference Godot");
    AssertTrue(!source.Contains("Godot.Node", StringComparison.Ordinal), "runtime must not reference scene nodes");
    AssertTrue(!source.Contains("SaveService", StringComparison.Ordinal), "runtime must not reference save services");
    AssertTrue(!source.Contains("StrategicWorldSaveService", StringComparison.Ordinal), "runtime must not reference save services");
    AssertTrue(!source.Contains("BattleStartRequest", StringComparison.Ordinal), "runtime must not reference legacy battle requests");
    AssertTrue(!source.Contains("BattleResult", StringComparison.Ordinal), "runtime must not reference legacy battle results");
    AssertTrue(!source.Contains("AutoBattle", StringComparison.Ordinal), "runtime must not reference old auto battle");
}

static void RuntimeRejectsInvalidBattleHandoff()
{
    AssertInvalidBattleHandoff(new BattleRuntimeSession().RunMinimal(null), "null snapshot");
    AssertInvalidBattleHandoff(new BattleRuntimeSession().RunMinimal(new BattleStartSnapshot()), "blank snapshot ids");
    AssertInvalidBattleHandoff(new BattleRuntimeSession().RunMinimal(new BattleStartSnapshot
    {
        SnapshotId = "snapshot_empty",
        BattleId = "battle_empty"
    }), "empty battle groups");
    AssertInvalidBattleHandoff(new BattleRuntimeSession().RunMinimal(new BattleStartSnapshot
    {
        SnapshotId = "snapshot_null_groups",
        BattleId = "battle_null_groups",
        BattleGroups = null
    }), "null battle groups");
    AssertInvalidBattleHandoff(new BattleRuntimeSession().RunMinimal(new BattleStartSnapshot
    {
        SnapshotId = "snapshot_blank_group",
        BattleId = "battle_blank_group",
        BattleGroups = { new BattleGroupSnapshot() }
    }), "blank battle group payload");
    AssertInvalidBattleHandoff(new BattleRuntimeSession().RunMinimal(new BattleStartSnapshot
    {
        SnapshotId = "snapshot_null_group",
        BattleId = "battle_null_group",
        BattleGroups = { null! }
    }), "null battle group payload");
}

static void AssertInvalidBattleHandoff(BattleRuntimeSessionResult result, string message)
{
    AssertTrue(!result.Outcome.IsComplete, "invalid handoff must not complete");
    AssertEqual(BattleTerminationReason.RuntimeException, result.Outcome.TerminationReason, $"{message} termination");
    AssertTrue(
        result.EventStream.Events.Any(item =>
            (item.Kind == BattleEventKind.CommandRejected || item.Kind == BattleEventKind.BattleEnded)
            && item.ReasonCode == "battle_snapshot_invalid"),
        $"{message} should emit rejection event");
}

static void DomainSourceStaysIsolated()
{
    string source = string.Join("\n", new[]
    {
        CombinedSource("src", "Domain", "Heroes"),
        CombinedSource("src", "Domain", "Corps"),
        CombinedSource("src", "Domain", "BattleGroups"),
        CombinedSource("src", "Domain", "Equipment")
    });
    AssertTrue(!source.Contains("Rpg.Runtime", StringComparison.Ordinal), "domain must not reference runtime");
    AssertTrue(!source.Contains("Godot.Node", StringComparison.Ordinal), "domain must not reference scene nodes");
    AssertTrue(!source.Contains("Godot.Control", StringComparison.Ordinal), "domain must not reference UI controls");
}

static void SnapshotCopiesBattleGroupFacts()
{
    HeroState hero = new() { HeroId = "hero_1", HeroDefinitionId = "hero_def_1", Level = 3 };
    CorpsState corps = new() { CorpsId = "corps_1", CorpsDefinitionId = "shield", Level = 2, CorpsStrength = 77 };
    BattleGroupState group = new()
    {
        BattleGroupId = "group_1",
        HeroId = hero.HeroId,
        CorpsId = corps.CorpsId,
        CurrentLocationId = "city_1",
        Status = BattleGroupStatus.Stationed
    };

    BattleStartSnapshot snapshot = new BattleSnapshotBuilder().Build(
        "snapshot_1",
        "battle_1",
        "site_1",
        new[] { group },
        new Dictionary<string, HeroState> { [hero.HeroId] = hero },
        new Dictionary<string, CorpsState> { [corps.CorpsId] = corps });

    AssertEqual("snapshot_1", snapshot.SnapshotId, "snapshot id");
    AssertEqual("battle_1", snapshot.BattleId, "battle id");
    AssertEqual(1, snapshot.BattleGroups.Count, "battle group count");
    AssertEqual("hero_def_1", snapshot.BattleGroups[0].HeroDefinitionId, "hero definition copied");
    AssertEqual("shield", snapshot.BattleGroups[0].CorpsDefinitionId, "corps definition copied");
    corps.CorpsStrength = 12;
    AssertEqual(77, snapshot.BattleGroups[0].CorpsStrength, "snapshot must not track live domain object");
}

static void BattleGroupLifecycleRejectsInvalidIdentities()
{
    BattleGroupLifecycleService service = new();

    AssertThrows<ArgumentException>(
        () => service.CreateAndStation("", "hero_1", "corps_1", "city_1"),
        "blank group id should throw");
    AssertThrows<ArgumentException>(
        () => service.CreateAndStation("group_1", " ", "corps_1", "city_1"),
        "blank hero id should throw");
    AssertThrows<ArgumentException>(
        () => service.CreateAndStation("group_1", "hero_1", null, "city_1"),
        "blank corps id should throw");
    AssertThrows<ArgumentException>(
        () => service.CreateAndStation("group_1", "hero_1", "corps_1", ""),
        "blank location id should throw");
}

static void BattleGroupLifecyclePreservesStateOnInvalidLock()
{
    BattleGroupLifecycleService service = new();
    BattleGroupState group = new()
    {
        BattleGroupId = "group_1",
        HeroId = "hero_1",
        CorpsId = "corps_1",
        CurrentLocationId = "city_1",
        Status = BattleGroupStatus.Stationed,
        ActiveBattleId = "existing_battle"
    };

    bool locked = service.TryLockForBattle(group, " ");

    AssertTrue(!locked, "blank battle id should not lock");
    AssertEqual(BattleGroupStatus.Stationed, group.Status, "status unchanged");
    AssertEqual("existing_battle", group.ActiveBattleId, "active battle unchanged");

    group.BattleGroupId = "";
    locked = service.TryLockForBattle(group, "battle_1");

    AssertTrue(!locked, "blank group identity should not lock");
    AssertEqual(BattleGroupStatus.Stationed, group.Status, "blank group status unchanged");
    AssertEqual("existing_battle", group.ActiveBattleId, "blank group active battle unchanged");

    group.BattleGroupId = "group_1";
    group.HeroId = "";
    locked = service.TryLockForBattle(group, "battle_1");

    AssertTrue(!locked, "blank hero identity should not lock");
    AssertEqual(BattleGroupStatus.Stationed, group.Status, "blank hero status unchanged");
    AssertEqual("existing_battle", group.ActiveBattleId, "blank hero active battle unchanged");

    group.HeroId = "hero_1";
    group.CorpsId = "";
    locked = service.TryLockForBattle(group, "battle_1");

    AssertTrue(!locked, "blank corps identity should not lock");
    AssertEqual(BattleGroupStatus.Stationed, group.Status, "blank corps status unchanged");
    AssertEqual("existing_battle", group.ActiveBattleId, "blank corps active battle unchanged");

    AssertTrue(!service.TryLockForBattle(null, "battle_1"), "null group should not lock");

    BattleGroupState recovering = new()
    {
        BattleGroupId = "group_recovering",
        HeroId = "hero_1",
        CorpsId = "corps_1",
        Status = BattleGroupStatus.Recovering,
        ActiveBattleId = "recovering_battle"
    };

    locked = service.TryLockForBattle(recovering, "battle_1");

    AssertTrue(!locked, "non sortie group should not lock");
    AssertEqual(BattleGroupStatus.Recovering, recovering.Status, "non sortie status unchanged");
    AssertEqual("recovering_battle", recovering.ActiveBattleId, "non sortie active battle unchanged");
}

static void BattleGroupLifecycleReleasesOnlyActiveBattleGroups()
{
    BattleGroupLifecycleService service = new();
    BattleGroupState recovering = new()
    {
        BattleGroupId = "group_recovering",
        Status = BattleGroupStatus.Recovering,
        ActiveBattleId = "battle_1"
    };
    BattleGroupState stationed = new()
    {
        BattleGroupId = "group_stationed",
        Status = BattleGroupStatus.InBattle,
        CurrentLocationId = "city_1",
        ActiveBattleId = "battle_1"
    };
    BattleGroupState unstationed = new()
    {
        BattleGroupId = "group_unstationed",
        Status = BattleGroupStatus.InBattle,
        ActiveBattleId = "battle_1"
    };

    service.ReleaseAfterBattle(recovering);
    service.ReleaseAfterBattle(stationed);
    service.ReleaseAfterBattle(unstationed);

    AssertEqual(BattleGroupStatus.Recovering, recovering.Status, "non battle status unchanged");
    AssertEqual("battle_1", recovering.ActiveBattleId, "non battle active id unchanged");
    AssertEqual(BattleGroupStatus.Stationed, stationed.Status, "stationed group released to station");
    AssertEqual("", stationed.ActiveBattleId, "stationed group active id cleared");
    AssertEqual(BattleGroupStatus.Available, unstationed.Status, "unstationed group released to available");
    AssertEqual("", unstationed.ActiveBattleId, "unstationed group active id cleared");
}

static void CommandValidationDistinguishesApplicationRejection()
{
    CommandRequest request = new()
    {
        BattleId = "battle_1",
        BattleGroupId = "group_missing",
        Channel = CommandChannel.Corps,
        Kind = CommandKind.Attack
    };

    CommandValidationResult result = new BattleCommandApplicationValidator()
        .Validate(request, new[] { "group_1" }, allowHero: true, allowCorps: true, allowCombined: true);

    AssertTrue(!result.Accepted, "missing group should reject");
    AssertEqual(CommandRejectionStage.Application, result.RejectionStage, "rejection stage");
    AssertEqual("battle_group_unavailable", result.ReasonCode, "reason code");
}

static void SettlementRejectsIncompleteResult()
{
    BattleOutcomeResult result = new()
    {
        SnapshotId = "snapshot_1",
        BattleId = "battle_1",
        IsComplete = false,
        TerminationReason = BattleTerminationReason.RuntimeException
    };

    SettlementPlan plan = new BattleSettlementService().BuildPlan("snapshot_1", result, BattleEventStream.Empty);

    AssertTrue(!plan.Accepted, "incomplete result should reject");
    AssertEqual("battle_result_incomplete", plan.RejectionReason, "rejection reason");
}

static void ReportAndSettlementConsumeSameEventIds()
{
    BattleEventStream stream = new();
    stream.Add(new BattleEvent { EventId = "event_1", BattleId = "battle_1", Kind = BattleEventKind.CommandAccepted });
    stream.Add(new BattleEvent { EventId = "event_2", BattleId = "battle_1", Kind = BattleEventKind.DamageApplied });
    BattleOutcomeResult result = BattleOutcomeResult.Completed("snapshot_1", "battle_1", BattleTerminationReason.NormalVictory);

    SettlementPlan plan = new BattleSettlementService().BuildPlan("snapshot_1", result, stream);
    BattleReportRecord report = new BattleReportBuilder().Build(result, stream, plan);

    AssertSequence(new[] { "event_1", "event_2" }, plan.SourceEventIds, "settlement source events");
    AssertSequence(new[] { "event_1", "event_2" }, report.SourceEventIds, "report source events");
}

static void LegacyGarrisonAdapterCreatesExplicitBattleGroups()
{
    Rpg.Domain.World.WorldSiteState site = new() { SiteId = "city_1" };
    site.Garrison.Add(new Rpg.Domain.World.GarrisonState { UnitTypeId = "militia", Count = 2 });

    Rpg.Application.Battle.Adapters.LegacyBattleGroupSeedAdapter adapter = new();
    IReadOnlyList<BattleGroupState> groups = adapter.SeedFromGarrison(site, "hero_seed");

    AssertEqual(2, groups.Count, "group count");
    AssertEqual("city_1", groups[0].CurrentLocationId, "location copied");
    AssertTrue(groups.All(item => !string.IsNullOrWhiteSpace(item.HeroId)), "hero ids assigned");
    AssertTrue(groups.All(item => !string.IsNullOrWhiteSpace(item.CorpsId)), "corps ids assigned");
}

static void LegacyResultAdapterPreservesRequestAndOutcomeIds()
{
    BattleOutcomeResult outcome = BattleOutcomeResult.Completed("snapshot_1", "battle_1", BattleTerminationReason.NormalVictory);
    Rpg.Application.Battle.BattleResult result = new Rpg.Application.Battle.Adapters.LegacyBattleResultAdapter()
        .ToLegacyResult("request_1", Rpg.Application.Battle.BattleKind.AssaultSite, outcome);

    AssertEqual("request_1", result.RequestId, "legacy request id");
    AssertEqual("battle_1", result.ContextId, "legacy context id");
    AssertEqual(Rpg.Application.Battle.BattleOutcome.Victory, result.Outcome, "legacy outcome");
}

static void LegacyResultAdapterMapsFailedHandoffToDisaster()
{
    LegacyBattleResultAdapter adapter = new();

    Rpg.Application.Battle.BattleResult nullOutcome = adapter
        .ToLegacyResult("request_null", Rpg.Application.Battle.BattleKind.AssaultSite, null);

    BattleOutcomeResult runtimeExceptionOutcome = new()
    {
        SnapshotId = "snapshot_1",
        BattleId = "battle_1",
        IsComplete = false,
        TerminationReason = BattleTerminationReason.RuntimeException
    };
    Rpg.Application.Battle.BattleResult incompleteRuntimeException = adapter
        .ToLegacyResult("request_runtime_exception", Rpg.Application.Battle.BattleKind.AssaultSite, runtimeExceptionOutcome);

    AssertEqual(Rpg.Application.Battle.BattleOutcome.Disaster, nullOutcome.Outcome, "null outcome maps to disaster");
    AssertTrue(nullOutcome.Outcome != Rpg.Application.Battle.BattleOutcome.Victory, "null outcome must not map to victory");
    AssertTrue(nullOutcome.Outcome != Rpg.Application.Battle.BattleOutcome.Defeat, "null outcome must not map to defeat");
    AssertEqual(Rpg.Application.Battle.BattleOutcome.Disaster, incompleteRuntimeException.Outcome, "incomplete runtime exception maps to disaster");
    AssertTrue(incompleteRuntimeException.Outcome != Rpg.Application.Battle.BattleOutcome.Victory, "incomplete runtime exception must not map to victory");
    AssertTrue(incompleteRuntimeException.Outcome != Rpg.Application.Battle.BattleOutcome.Defeat, "incomplete runtime exception must not map to defeat");
}

static void BattleGroupVerticalSliceSettlesAndReports()
{
    HeroState hero = new() { HeroId = "hero_1", HeroDefinitionId = "hero_def_1", Level = 3 };
    CorpsState corps = new() { CorpsId = "corps_1", CorpsDefinitionId = "shield", Level = 2, CorpsStrength = 90 };
    BattleGroupState group = new BattleGroupLifecycleService().CreateAndStation("group_1", hero.HeroId, corps.CorpsId, "city_1");

    Rpg.Application.Battle.BattleGroupBattleFlowService flow = new();
    Rpg.Application.Battle.BattleGroupBattleFlowResult result = flow.RunMinimalBattle(
        "snapshot_1",
        "battle_1",
        "site_1",
        new[] { group },
        new Dictionary<string, HeroState> { [hero.HeroId] = hero },
        new Dictionary<string, CorpsState> { [corps.CorpsId] = corps });

    AssertTrue(result.SettlementPlan.Accepted, "settlement accepted");
    AssertEqual("battle_1", result.Report.BattleId, "report battle id");
    AssertSequence(result.RuntimeResult.EventStream.EventIds, result.SettlementPlan.SourceEventIds, "settlement uses runtime events");
    AssertSequence(result.RuntimeResult.EventStream.EventIds, result.Report.SourceEventIds, "report uses runtime events");
    AssertSequence(result.SettlementPlan.SourceEventIds, result.Report.SourceEventIds, "same source events");

    Rpg.Application.Battle.BattleGroupBattleFlowResult rejected = flow.RunMinimalBattle(
        "snapshot_missing",
        "battle_missing",
        "site_1",
        new[] { group },
        new Dictionary<string, HeroState>(),
        new Dictionary<string, CorpsState> { [corps.CorpsId] = corps });

    AssertTrue(!rejected.RuntimeResult.Outcome.IsComplete, "missing hero handoff must not complete");
    AssertTrue(!rejected.SettlementPlan.Accepted, "missing hero handoff must not settle");
    AssertEqual("battle_result_incomplete", rejected.SettlementPlan.RejectionReason, "missing hero settlement rejection");
}

static string CombinedSource(params string[] pathParts)
{
    string root = ProjectRoot();
    string path = Path.Combine(new[] { root }.Concat(pathParts).ToArray());
    if (!Directory.Exists(path))
    {
        return "";
    }

    return string.Join("\n", Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories)
        .OrderBy(item => item, StringComparer.Ordinal)
        .Select(File.ReadAllText));
}

static string ProjectRoot()
{
    DirectoryInfo? current = new(AppContext.BaseDirectory);
    while (current != null && !File.Exists(Path.Combine(current.FullName, "rpg.csproj")))
    {
        current = current.Parent;
    }

    return current?.FullName ?? throw new InvalidOperationException("project root not found");
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
        Environment.ExitCode = 1;
    }
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new Exception(message);
    }
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new Exception($"{message}: expected={expected} actual={actual}");
    }
}

static void AssertThrows<T>(Action action, string message)
    where T : Exception
{
    try
    {
        action();
    }
    catch (T)
    {
        return;
    }
    catch (Exception exception)
    {
        throw new Exception($"{message}: expected={typeof(T).Name} actual={exception.GetType().Name}");
    }

    throw new Exception($"{message}: expected={typeof(T).Name} actual=no exception");
}

static void AssertSequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string message)
{
    if (!expected.SequenceEqual(actual))
    {
        throw new Exception($"{message}: expected=[{string.Join(",", expected)}] actual=[{string.Join(",", actual)}]");
    }
}
