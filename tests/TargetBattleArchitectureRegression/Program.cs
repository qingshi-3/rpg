using Rpg.Application.Battle.Commands;
using Rpg.Application.Battle.Reports;
using Rpg.Application.Battle.Settlement;
using Rpg.Application.Battle.Snapshots;
using Rpg.Domain.BattleGroups;
using Rpg.Domain.Corps;
using Rpg.Domain.Heroes;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Results;

Run("corps strength clamps and visible soldiers are derived", CorpsStrengthClampsAndVisibleSoldiersAreDerived);
Run("runtime source stays isolated from domain and presentation owners", RuntimeSourceStaysIsolated);
Run("domain source stays isolated from runtime and Godot scene nodes", DomainSourceStaysIsolated);
Run("snapshot copies battle group facts", SnapshotCopiesBattleGroupFacts);
Run("command validation distinguishes application rejection", CommandValidationDistinguishesApplicationRejection);
Run("settlement rejects incomplete result", SettlementRejectsIncompleteResult);
Run("report and settlement consume the same event ids", ReportAndSettlementConsumeSameEventIds);

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
    AssertTrue(!source.Contains("StrategicWorldSaveService", StringComparison.Ordinal), "runtime must not reference save services");
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

static void AssertSequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string message)
{
    if (!expected.SequenceEqual(actual))
    {
        throw new Exception($"{message}: expected=[{string.Join(",", expected)}] actual=[{string.Join(",", actual)}]");
    }
}
