# Hero-Led Light RTS Code Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

## Current Status

Status: First Phase Implemented

This plan is now a completed first-phase implementation record, not an open task list. The target architecture regression project, target contract skeletons, boundary adapters, minimal vertical flow, and proposal note updates have landed. The implemented slice is still a contract/runtime skeleton beside the legacy battle path; it does not replace the live WorldSite battle entry or final light-RTS runtime.

**Goal:** Build the first target architecture vertical slice: battle-group long-term state, snapshot contracts, command validation, runtime event/result output, settlement writeback, and report generation.

**Architecture:** Add the target architecture beside the old implementation, then bridge old strategic data through explicit adapters. Runtime receives snapshots and emits events/results only; Application owns settlement and report truth. Old `BattleStartRequest`, `BattleResult`, `GarrisonState`, and `AutoBattle` remain isolated until their replacement paths are ready.

**Tech Stack:** Godot 4.5 C#, .NET 8, console regression test projects, existing `src/Definitions`, `src/Domain`, `src/Application`, `src/Presentation`, and `src/Infrastructure` layout.

---

## Scope

This plan implements the first refactor phase only. It does not replace all battle scenes, final UI, full enemy AI, full save schema, or balance values.

The phase is complete when one battle group can move through this path:

```text
HeroState + CorpsState + BattleGroupState
-> BattleStartSnapshot
-> Runtime session
-> BattleEventStream + BattleOutcomeResult
-> SettlementPlan + StateDeltaSet
-> BattleReportRecord
```

## File Structure

Create these folders and files:

```text
src/Definitions/Heroes/HeroDefinition.cs
src/Definitions/Heroes/HeroAttributeBlock.cs
src/Definitions/Corps/CorpsDefinition.cs
src/Definitions/Corps/CorpsCombatClass.cs
src/Definitions/Equipment/EquipmentDefinition.cs
src/Domain/Heroes/HeroState.cs
src/Domain/Heroes/HeroRank.cs
src/Domain/Heroes/HeroAttributeSet.cs
src/Domain/Corps/CorpsState.cs
src/Domain/Corps/CorpsStrengthPolicy.cs
src/Domain/BattleGroups/BattleGroupState.cs
src/Domain/BattleGroups/BattleGroupStatus.cs
src/Domain/Equipment/EquipmentInstance.cs
src/Domain/Equipment/EquipmentAssignment.cs
src/Application/Battle/Snapshots/BattleStartSnapshot.cs
src/Application/Battle/Snapshots/BattleGroupSnapshot.cs
src/Application/Battle/Snapshots/LocationBattleContext.cs
src/Application/Battle/Snapshots/BattleSnapshotBuilder.cs
src/Application/Battle/Commands/CommandChannel.cs
src/Application/Battle/Commands/CommandKind.cs
src/Application/Battle/Commands/CommandRequest.cs
src/Application/Battle/Commands/CommandValidationResult.cs
src/Application/Battle/Commands/BattleCommandApplicationValidator.cs
src/Application/Battle/Settlement/SettlementPlan.cs
src/Application/Battle/Settlement/StateDeltaSet.cs
src/Application/Battle/Settlement/BattleSettlementService.cs
src/Application/Battle/Reports/BattleReportRecord.cs
src/Application/Battle/Reports/BattleReportBuilder.cs
src/Application/Battle/Adapters/LegacyBattleGroupSeedAdapter.cs
src/Application/Battle/Adapters/LegacyBattleStartSnapshotAdapter.cs
src/Application/Battle/Adapters/LegacyBattleResultAdapter.cs
src/Application/BattleGroups/BattleGroupLifecycleService.cs
src/Application/Battle/BattleGroupBattleFlowService.cs
src/Runtime/Battle/BattleRuntimeActor.cs
src/Runtime/Battle/BattleRuntimeActorKind.cs
src/Runtime/Battle/BattleRuntimeSession.cs
src/Runtime/Battle/BattleRuntimeSessionResult.cs
src/Runtime/Battle/BattleTerminationReason.cs
src/Runtime/Battle/Events/BattleEvent.cs
src/Runtime/Battle/Events/BattleEventKind.cs
src/Runtime/Battle/Events/BattleEventStream.cs
src/Runtime/Battle/Results/BattleOutcomeResult.cs
tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj
tests/TargetBattleArchitectureRegression/Program.cs
```

Modify:

```text
design-proposals/active/2026-05-17-hero-led-light-rts-system-architecture/implementation-notes.md
```

Do not modify UI scenes in this phase.

## Task 1: Architecture Regression Project

**Files:**
- Create: `tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj`
- Create: `tests/TargetBattleArchitectureRegression/Program.cs`

- [x] **Step 1: Write the failing test project**

Create `tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\rpg.csproj" />
  </ItemGroup>
</Project>
```

Create `tests/TargetBattleArchitectureRegression/Program.cs`:

```csharp
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
```

- [x] **Step 2: Run test to verify it fails**

Run:

```powershell
dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj
```

Expected: FAIL at compile time with missing namespaces such as `Rpg.Domain.BattleGroups`, `Rpg.Runtime.Battle`, and `Rpg.Application.Battle.Snapshots`.

- [x] **Step 3: Commit the failing architecture tests**

```powershell
git add tests/TargetBattleArchitectureRegression
git commit -m "test: add target battle architecture regression suite"
```

## Task 2: Domain State Contracts

**Files:**
- Create: `src/Domain/Heroes/HeroRank.cs`
- Create: `src/Domain/Heroes/HeroAttributeSet.cs`
- Create: `src/Domain/Heroes/HeroState.cs`
- Create: `src/Domain/Corps/CorpsState.cs`
- Create: `src/Domain/Corps/CorpsStrengthPolicy.cs`
- Create: `src/Domain/BattleGroups/BattleGroupStatus.cs`
- Create: `src/Domain/BattleGroups/BattleGroupState.cs`
- Create: `src/Domain/Equipment/EquipmentInstance.cs`
- Create: `src/Domain/Equipment/EquipmentAssignment.cs`
- Test: `tests/TargetBattleArchitectureRegression/Program.cs`

- [x] **Step 1: Add hero state**

Create `src/Domain/Heroes/HeroRank.cs`:

```csharp
namespace Rpg.Domain.Heroes;

public enum HeroRank
{
    Ordinary = 0,
    Elite = 1,
    Renowned = 2,
    Legendary = 3,
    Mythic = 4
}
```

Create `src/Domain/Heroes/HeroAttributeSet.cs`:

```csharp
namespace Rpg.Domain.Heroes;

public sealed class HeroAttributeSet
{
    public int Martial { get; set; }
    public int Vitality { get; set; }
    public int Technique { get; set; }
    public int Tactics { get; set; }
    public int Willpower { get; set; }
    public int Charisma { get; set; }
    public int Craft { get; set; }
    public int Mystic { get; set; }
}
```

Create `src/Domain/Heroes/HeroState.cs`:

```csharp
using System.Collections.Generic;

namespace Rpg.Domain.Heroes;

public sealed class HeroState
{
    public string HeroId { get; set; } = "";
    public string HeroDefinitionId { get; set; } = "";
    public string OwnerFactionId { get; set; } = "player";
    public int Level { get; set; } = 1;
    public HeroRank Rank { get; set; } = HeroRank.Ordinary;
    public HeroAttributeSet BaseAttributes { get; set; } = new();
    public List<string> SkillIds { get; set; } = new();
    public bool IsAvailable { get; set; } = true;
}
```

- [x] **Step 2: Add corps state**

Create `src/Domain/Corps/CorpsState.cs`:

```csharp
namespace Rpg.Domain.Corps;

public sealed class CorpsState
{
    public string CorpsId { get; set; } = "";
    public string CorpsDefinitionId { get; set; } = "";
    public int Level { get; set; } = 1;
    public int EquipmentLevel { get; set; } = 1;
    public int CorpsStrength { get; set; } = CorpsStrengthPolicy.MaxStrength;
    public int TrainingProgress { get; set; }

    public void ClampStrength()
    {
        CorpsStrength = CorpsStrengthPolicy.Clamp(CorpsStrength);
    }
}
```

Create `src/Domain/Corps/CorpsStrengthPolicy.cs`:

```csharp
using System;

namespace Rpg.Domain.Corps;

public static class CorpsStrengthPolicy
{
    public const int MinStrength = 0;
    public const int MaxStrength = 100;

    public static int Clamp(int value)
    {
        return Math.Clamp(value, MinStrength, MaxStrength);
    }

    public static int CalculateVisibleSoldiers(int corpsStrength, int maxVisibleSoldiers)
    {
        int clampedStrength = Clamp(corpsStrength);
        int clampedMax = Math.Max(0, maxVisibleSoldiers);
        if (clampedStrength <= 0 || clampedMax == 0)
        {
            return 0;
        }

        return Math.Clamp((int)Math.Ceiling(clampedStrength / 100.0 * clampedMax), 1, clampedMax);
    }
}
```

- [x] **Step 3: Add battle group and equipment state**

Create `src/Domain/BattleGroups/BattleGroupStatus.cs`:

```csharp
namespace Rpg.Domain.BattleGroups;

public enum BattleGroupStatus
{
    Available = 0,
    Stationed = 1,
    SortieLocked = 2,
    InBattle = 3,
    Recovering = 4,
    Unavailable = 5
}
```

Create `src/Domain/BattleGroups/BattleGroupState.cs`:

```csharp
namespace Rpg.Domain.BattleGroups;

public sealed class BattleGroupState
{
    public string BattleGroupId { get; set; } = "";
    public string HeroId { get; set; } = "";
    public string CorpsId { get; set; } = "";
    public string CurrentLocationId { get; set; } = "";
    public BattleGroupStatus Status { get; set; } = BattleGroupStatus.Available;
    public string ActiveBattleId { get; set; } = "";

    public bool CanSortie => Status is BattleGroupStatus.Available or BattleGroupStatus.Stationed;
}
```

Create `src/Domain/Equipment/EquipmentInstance.cs`:

```csharp
namespace Rpg.Domain.Equipment;

public sealed class EquipmentInstance
{
    public string EquipmentInstanceId { get; set; } = "";
    public string EquipmentDefinitionId { get; set; } = "";
    public string OwnerFactionId { get; set; } = "player";
    public int Level { get; set; } = 1;
}
```

Create `src/Domain/Equipment/EquipmentAssignment.cs`:

```csharp
namespace Rpg.Domain.Equipment;

public sealed class EquipmentAssignment
{
    public string OwnerHeroId { get; set; } = "";
    public string WeaponInstanceId { get; set; } = "";
    public string ArmorInstanceId { get; set; } = "";
    public string TokenInstanceId { get; set; } = "";
}
```

- [x] **Step 4: Run architecture regression**

Run:

```powershell
dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj
```

Expected: FAIL with missing Application and Runtime namespaces, while `CorpsStrengthClampsAndVisibleSoldiersAreDerived` compiles.

- [x] **Step 5: Commit domain contracts**

```powershell
git add src/Domain/Heroes src/Domain/Corps src/Domain/BattleGroups src/Domain/Equipment tests/TargetBattleArchitectureRegression
git commit -m "feat: add battle group domain state contracts"
```

## Task 3: Content Definition Resources

**Files:**
- Create: `src/Definitions/Heroes/HeroAttributeBlock.cs`
- Create: `src/Definitions/Heroes/HeroDefinition.cs`
- Create: `src/Definitions/Corps/CorpsCombatClass.cs`
- Create: `src/Definitions/Corps/CorpsDefinition.cs`
- Create: `src/Definitions/Equipment/EquipmentDefinition.cs`

- [x] **Step 1: Add hero definition resources**

Create `src/Definitions/Heroes/HeroAttributeBlock.cs`:

```csharp
using Godot;

namespace Rpg.Definitions.Heroes;

[GlobalClass]
public partial class HeroAttributeBlock : Resource
{
    [Export] public int Martial { get; set; }
    [Export] public int Vitality { get; set; }
    [Export] public int Technique { get; set; }
    [Export] public int Tactics { get; set; }
    [Export] public int Willpower { get; set; }
    [Export] public int Charisma { get; set; }
    [Export] public int Craft { get; set; }
    [Export] public int Mystic { get; set; }
}
```

Create `src/Definitions/Heroes/HeroDefinition.cs`:

```csharp
using Godot;

namespace Rpg.Definitions.Heroes;

[GlobalClass]
public partial class HeroDefinition : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "英雄";
    [Export] public string ProfessionId { get; set; } = "";
    [Export] public HeroAttributeBlock BaseAttributes { get; set; }
    [Export] public Godot.Collections.Array<string> StartingSkillIds { get; set; } = new();
    [Export] public Godot.Collections.Array<string> Tags { get; set; } = new();
}
```

- [x] **Step 2: Add corps and equipment definition resources**

Create `src/Definitions/Corps/CorpsCombatClass.cs`:

```csharp
namespace Rpg.Definitions.Corps;

public enum CorpsCombatClass
{
    Infantry = 0,
    Shield = 1,
    Spear = 2,
    Archer = 3,
    Cavalry = 4,
    Mage = 5,
    Medic = 6,
    Assassin = 7
}
```

Create `src/Definitions/Corps/CorpsDefinition.cs`:

```csharp
using Godot;

namespace Rpg.Definitions.Corps;

[GlobalClass]
public partial class CorpsDefinition : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "兵团";
    [Export] public CorpsCombatClass CombatClass { get; set; } = CorpsCombatClass.Infantry;
    [Export] public string FormId { get; set; } = "";
    [Export] public int MaxVisibleSoldiers { get; set; } = 5;
    [Export] public Godot.Collections.Array<string> Tags { get; set; } = new();
    [Export] public Godot.Collections.Array<string> AbilityIds { get; set; } = new();
}
```

Create `src/Definitions/Equipment/EquipmentDefinition.cs`:

```csharp
using Godot;

namespace Rpg.Definitions.Equipment;

[GlobalClass]
public partial class EquipmentDefinition : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "装备";
    [Export] public string Slot { get; set; } = "";
    [Export] public string Grade { get; set; } = "common";
    [Export] public Godot.Collections.Array<string> Tags { get; set; } = new();
}
```

- [x] **Step 3: Build to verify Godot resource classes compile**

Run:

```powershell
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
```

Expected: exit code 0. This verifies the new Godot `Resource` classes compile without building the regression project that is still waiting for Application and Runtime contracts.

- [x] **Step 4: Commit definition resources**

```powershell
git add src/Definitions/Heroes src/Definitions/Corps src/Definitions/Equipment
git commit -m "feat: add hero corps equipment definitions"
```

## Task 4: Snapshot Contracts

**Files:**
- Create: `src/Application/Battle/Snapshots/BattleGroupSnapshot.cs`
- Create: `src/Application/Battle/Snapshots/LocationBattleContext.cs`
- Create: `src/Application/Battle/Snapshots/BattleStartSnapshot.cs`
- Create: `src/Application/Battle/Snapshots/BattleSnapshotBuilder.cs`
- Test: `tests/TargetBattleArchitectureRegression/Program.cs`

- [x] **Step 1: Add snapshot data contracts**

Create `src/Application/Battle/Snapshots/BattleGroupSnapshot.cs`:

```csharp
namespace Rpg.Application.Battle.Snapshots;

public sealed class BattleGroupSnapshot
{
    public string BattleGroupId { get; set; } = "";
    public string HeroId { get; set; } = "";
    public string HeroDefinitionId { get; set; } = "";
    public int HeroLevel { get; set; }
    public string CorpsId { get; set; } = "";
    public string CorpsDefinitionId { get; set; } = "";
    public int CorpsLevel { get; set; }
    public int CorpsEquipmentLevel { get; set; }
    public int CorpsStrength { get; set; }
    public string SourceLocationId { get; set; } = "";
}
```

Create `src/Application/Battle/Snapshots/LocationBattleContext.cs`:

```csharp
using System.Collections.Generic;

namespace Rpg.Application.Battle.Snapshots;

public sealed class LocationBattleContext
{
    public string LocationId { get; set; } = "";
    public List<string> ActiveFacilityIds { get; set; } = new();
    public List<string> ActiveTags { get; set; } = new();
}
```

Create `src/Application/Battle/Snapshots/BattleStartSnapshot.cs`:

```csharp
using System.Collections.Generic;

namespace Rpg.Application.Battle.Snapshots;

public sealed class BattleStartSnapshot
{
    public string SnapshotId { get; set; } = "";
    public string BattleId { get; set; } = "";
    public string TargetLocationId { get; set; } = "";
    public LocationBattleContext LocationContext { get; set; } = new();
    public List<BattleGroupSnapshot> BattleGroups { get; set; } = new();
}
```

- [x] **Step 2: Add snapshot builder**

Create `src/Application/Battle/Snapshots/BattleSnapshotBuilder.cs`:

```csharp
using System.Collections.Generic;
using Rpg.Domain.BattleGroups;
using Rpg.Domain.Corps;
using Rpg.Domain.Heroes;

namespace Rpg.Application.Battle.Snapshots;

public sealed class BattleSnapshotBuilder
{
    public BattleStartSnapshot Build(
        string snapshotId,
        string battleId,
        string targetLocationId,
        IEnumerable<BattleGroupState> battleGroups,
        IReadOnlyDictionary<string, HeroState> heroes,
        IReadOnlyDictionary<string, CorpsState> corps)
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = snapshotId ?? "",
            BattleId = battleId ?? "",
            TargetLocationId = targetLocationId ?? "",
            LocationContext = new LocationBattleContext { LocationId = targetLocationId ?? "" }
        };

        if (battleGroups == null)
        {
            return snapshot;
        }

        foreach (BattleGroupState group in battleGroups)
        {
            if (group == null ||
                !heroes.TryGetValue(group.HeroId, out HeroState hero) ||
                !corps.TryGetValue(group.CorpsId, out CorpsState corpsState))
            {
                continue;
            }

            snapshot.BattleGroups.Add(new BattleGroupSnapshot
            {
                BattleGroupId = group.BattleGroupId,
                HeroId = hero.HeroId,
                HeroDefinitionId = hero.HeroDefinitionId,
                HeroLevel = hero.Level,
                CorpsId = corpsState.CorpsId,
                CorpsDefinitionId = corpsState.CorpsDefinitionId,
                CorpsLevel = corpsState.Level,
                CorpsEquipmentLevel = corpsState.EquipmentLevel,
                CorpsStrength = CorpsStrengthPolicy.Clamp(corpsState.CorpsStrength),
                SourceLocationId = group.CurrentLocationId
            });
        }

        return snapshot;
    }
}
```

- [x] **Step 3: Run architecture regression**

Run:

```powershell
dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj
```

Expected: FAIL with missing Command, Settlement, Report, and Runtime types. `SnapshotCopiesBattleGroupFacts` should compile after this task.

- [x] **Step 4: Commit snapshot contracts**

```powershell
git add src/Application/Battle/Snapshots tests/TargetBattleArchitectureRegression
git commit -m "feat: add battle snapshot contracts"
```

## Task 5: Command Contracts

**Files:**
- Create: `src/Application/Battle/Commands/CommandChannel.cs`
- Create: `src/Application/Battle/Commands/CommandKind.cs`
- Create: `src/Application/Battle/Commands/CommandRequest.cs`
- Create: `src/Application/Battle/Commands/CommandValidationResult.cs`
- Create: `src/Application/Battle/Commands/BattleCommandApplicationValidator.cs`
- Test: `tests/TargetBattleArchitectureRegression/Program.cs`

- [x] **Step 1: Add command request and enums**

Create `src/Application/Battle/Commands/CommandChannel.cs`:

```csharp
namespace Rpg.Application.Battle.Commands;

public enum CommandChannel
{
    Hero = 0,
    Corps = 1,
    Combined = 2
}
```

Create `src/Application/Battle/Commands/CommandKind.cs`:

```csharp
namespace Rpg.Application.Battle.Commands;

public enum CommandKind
{
    Move = 0,
    Attack = 1,
    Hold = 2,
    Retreat = 3,
    CastSkill = 4,
    Regroup = 5
}
```

Create `src/Application/Battle/Commands/CommandRequest.cs`:

```csharp
namespace Rpg.Application.Battle.Commands;

public sealed class CommandRequest
{
    public string CommandId { get; set; } = "";
    public string BattleId { get; set; } = "";
    public string BattleGroupId { get; set; } = "";
    public CommandChannel Channel { get; set; }
    public CommandKind Kind { get; set; }
    public string TargetActorId { get; set; } = "";
    public string SkillId { get; set; } = "";
}
```

- [x] **Step 2: Add command validation result and validator**

Create `src/Application/Battle/Commands/CommandValidationResult.cs`:

```csharp
namespace Rpg.Application.Battle.Commands;

public enum CommandRejectionStage
{
    None = 0,
    UiHint = 1,
    Application = 2,
    Runtime = 3
}

public sealed class CommandValidationResult
{
    public bool Accepted { get; init; }
    public CommandRejectionStage RejectionStage { get; init; }
    public string ReasonCode { get; init; } = "";

    public static CommandValidationResult Accept()
    {
        return new CommandValidationResult { Accepted = true };
    }

    public static CommandValidationResult Reject(CommandRejectionStage stage, string reasonCode)
    {
        return new CommandValidationResult
        {
            Accepted = false,
            RejectionStage = stage,
            ReasonCode = reasonCode ?? ""
        };
    }
}
```

Create `src/Application/Battle/Commands/BattleCommandApplicationValidator.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;

namespace Rpg.Application.Battle.Commands;

public sealed class BattleCommandApplicationValidator
{
    public CommandValidationResult Validate(
        CommandRequest request,
        IEnumerable<string> availableBattleGroupIds,
        bool allowHero,
        bool allowCorps,
        bool allowCombined)
    {
        if (request == null)
        {
            return CommandValidationResult.Reject(CommandRejectionStage.Application, "command_missing");
        }

        if (string.IsNullOrWhiteSpace(request.BattleId))
        {
            return CommandValidationResult.Reject(CommandRejectionStage.Application, "battle_missing");
        }

        bool groupAvailable = availableBattleGroupIds?.Contains(request.BattleGroupId) == true;
        if (!groupAvailable)
        {
            return CommandValidationResult.Reject(CommandRejectionStage.Application, "battle_group_unavailable");
        }

        bool channelAllowed = request.Channel switch
        {
            CommandChannel.Hero => allowHero,
            CommandChannel.Corps => allowCorps,
            CommandChannel.Combined => allowCombined,
            _ => false
        };

        return channelAllowed
            ? CommandValidationResult.Accept()
            : CommandValidationResult.Reject(CommandRejectionStage.Application, "command_channel_unavailable");
    }
}
```

- [x] **Step 3: Run architecture regression**

Run:

```powershell
dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj
```

Expected: FAIL with missing Settlement, Report, and Runtime types. `CommandValidationDistinguishesApplicationRejection` should compile after this task.

- [x] **Step 4: Commit command contracts**

```powershell
git add src/Application/Battle/Commands tests/TargetBattleArchitectureRegression
git commit -m "feat: add battle command contracts"
```

## Task 6: Runtime Event And Result Contracts

**Files:**
- Create: `src/Runtime/Battle/BattleTerminationReason.cs`
- Create: `src/Runtime/Battle/BattleRuntimeActorKind.cs`
- Create: `src/Runtime/Battle/BattleRuntimeActor.cs`
- Create: `src/Runtime/Battle/Events/BattleEventKind.cs`
- Create: `src/Runtime/Battle/Events/BattleEvent.cs`
- Create: `src/Runtime/Battle/Events/BattleEventStream.cs`
- Create: `src/Runtime/Battle/Results/BattleOutcomeResult.cs`
- Create: `src/Runtime/Battle/BattleRuntimeSessionResult.cs`
- Create: `src/Runtime/Battle/BattleRuntimeSession.cs`
- Test: `tests/TargetBattleArchitectureRegression/Program.cs`

- [x] **Step 1: Add event stream**

Create `src/Runtime/Battle/Events/BattleEventKind.cs`:

```csharp
namespace Rpg.Runtime.Battle.Events;

public enum BattleEventKind
{
    BattleStarted = 0,
    CommandAccepted = 1,
    CommandRejected = 2,
    MovementCompleted = 3,
    DamageApplied = 4,
    CorpsStrengthChanged = 5,
    BattleEnded = 6
}
```

Create `src/Runtime/Battle/Events/BattleEvent.cs`:

```csharp
namespace Rpg.Runtime.Battle.Events;

public sealed class BattleEvent
{
    public string EventId { get; set; } = "";
    public string BattleId { get; set; } = "";
    public BattleEventKind Kind { get; set; }
    public string ActorId { get; set; } = "";
    public string BattleGroupId { get; set; } = "";
    public string SourceCommandId { get; set; } = "";
    public string TargetId { get; set; } = "";
    public string ReasonCode { get; set; } = "";
    public int CorpsStrengthDelta { get; set; }
}
```

Create `src/Runtime/Battle/Events/BattleEventStream.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;

namespace Rpg.Runtime.Battle.Events;

public sealed class BattleEventStream
{
    private readonly List<BattleEvent> _events = new();

    public static BattleEventStream Empty => new();
    public IReadOnlyList<BattleEvent> Events => _events;
    public IReadOnlyList<string> EventIds => _events.Select(item => item.EventId).ToList();

    public void Add(BattleEvent battleEvent)
    {
        if (battleEvent != null)
        {
            _events.Add(battleEvent);
        }
    }
}
```

- [x] **Step 2: Add runtime result contracts**

Create `src/Runtime/Battle/BattleTerminationReason.cs`:

```csharp
namespace Rpg.Runtime.Battle;

public enum BattleTerminationReason
{
    None = 0,
    NormalVictory = 1,
    NormalDefeat = 2,
    PlayerRetreat = 3,
    Interrupted = 4,
    RuntimeException = 5
}
```

Create `src/Runtime/Battle/Results/BattleOutcomeResult.cs`:

```csharp
using Rpg.Runtime.Battle;

namespace Rpg.Runtime.Battle.Results;

public sealed class BattleOutcomeResult
{
    public string SnapshotId { get; set; } = "";
    public string BattleId { get; set; } = "";
    public bool IsComplete { get; set; }
    public BattleTerminationReason TerminationReason { get; set; } = BattleTerminationReason.None;

    public static BattleOutcomeResult Completed(
        string snapshotId,
        string battleId,
        BattleTerminationReason terminationReason)
    {
        return new BattleOutcomeResult
        {
            SnapshotId = snapshotId ?? "",
            BattleId = battleId ?? "",
            IsComplete = true,
            TerminationReason = terminationReason
        };
    }
}
```

- [x] **Step 3: Add minimal runtime session**

Create `src/Runtime/Battle/BattleRuntimeActorKind.cs`:

```csharp
namespace Rpg.Runtime.Battle;

public enum BattleRuntimeActorKind
{
    Hero = 0,
    Corps = 1
}
```

Create `src/Runtime/Battle/BattleRuntimeActor.cs`:

```csharp
namespace Rpg.Runtime.Battle;

public sealed class BattleRuntimeActor
{
    public string ActorId { get; set; } = "";
    public string BattleGroupId { get; set; } = "";
    public BattleRuntimeActorKind Kind { get; set; }
    public int HitPoints { get; set; } = 1;
}
```

Create `src/Runtime/Battle/BattleRuntimeSessionResult.cs`:

```csharp
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Results;

namespace Rpg.Runtime.Battle;

public sealed class BattleRuntimeSessionResult
{
    public BattleOutcomeResult Outcome { get; init; } = new();
    public BattleEventStream EventStream { get; init; } = new();
}
```

Create `src/Runtime/Battle/BattleRuntimeSession.cs`:

```csharp
using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Results;

namespace Rpg.Runtime.Battle;

public sealed class BattleRuntimeSession
{
    public BattleRuntimeSessionResult RunMinimal(BattleStartSnapshot snapshot)
    {
        BattleEventStream stream = new();
        string battleId = snapshot?.BattleId ?? "";
        string snapshotId = snapshot?.SnapshotId ?? "";
        stream.Add(new BattleEvent
        {
            EventId = $"{battleId}:started",
            BattleId = battleId,
            Kind = BattleEventKind.BattleStarted
        });

        foreach (BattleGroupSnapshot group in snapshot?.BattleGroups ?? Enumerable.Empty<BattleGroupSnapshot>())
        {
            stream.Add(new BattleEvent
            {
                EventId = $"{battleId}:{group.BattleGroupId}:command",
                BattleId = battleId,
                BattleGroupId = group.BattleGroupId,
                Kind = BattleEventKind.CommandAccepted
            });
        }

        stream.Add(new BattleEvent
        {
            EventId = $"{battleId}:ended",
            BattleId = battleId,
            Kind = BattleEventKind.BattleEnded
        });

        return new BattleRuntimeSessionResult
        {
            EventStream = stream,
            Outcome = BattleOutcomeResult.Completed(snapshotId, battleId, BattleTerminationReason.NormalVictory)
        };
    }
}
```

- [x] **Step 4: Run architecture regression**

Run:

```powershell
dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj
```

Expected: FAIL with missing Settlement and Report types. Runtime source isolation tests should compile.

- [x] **Step 5: Commit runtime contracts**

```powershell
git add src/Runtime/Battle tests/TargetBattleArchitectureRegression
git commit -m "feat: add battle runtime event contracts"
```

## Task 7: Settlement And Report Contracts

**Files:**
- Create: `src/Application/Battle/Settlement/SettlementPlan.cs`
- Create: `src/Application/Battle/Settlement/StateDeltaSet.cs`
- Create: `src/Application/Battle/Settlement/BattleSettlementService.cs`
- Create: `src/Application/Battle/Reports/BattleReportRecord.cs`
- Create: `src/Application/Battle/Reports/BattleReportBuilder.cs`
- Test: `tests/TargetBattleArchitectureRegression/Program.cs`

- [x] **Step 1: Add settlement contracts**

Create `src/Application/Battle/Settlement/StateDeltaSet.cs`:

```csharp
using System.Collections.Generic;

namespace Rpg.Application.Battle.Settlement;

public sealed class StateDeltaSet
{
    public List<string> ChangedHeroIds { get; set; } = new();
    public List<string> ChangedCorpsIds { get; set; } = new();
    public List<string> ChangedBattleGroupIds { get; set; } = new();
    public List<string> ChangedLocationIds { get; set; } = new();
}
```

Create `src/Application/Battle/Settlement/SettlementPlan.cs`:

```csharp
using System.Collections.Generic;

namespace Rpg.Application.Battle.Settlement;

public sealed class SettlementPlan
{
    public bool Accepted { get; set; }
    public string RejectionReason { get; set; } = "";
    public string SnapshotId { get; set; } = "";
    public string BattleId { get; set; } = "";
    public List<string> SourceEventIds { get; set; } = new();
    public StateDeltaSet Deltas { get; set; } = new();
}
```

- [x] **Step 2: Add settlement service**

Create `src/Application/Battle/Settlement/BattleSettlementService.cs`:

```csharp
using System.Linq;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Results;

namespace Rpg.Application.Battle.Settlement;

public sealed class BattleSettlementService
{
    public SettlementPlan BuildPlan(
        string expectedSnapshotId,
        BattleOutcomeResult result,
        BattleEventStream eventStream)
    {
        if (result == null)
        {
            return Reject(expectedSnapshotId, "", "battle_result_missing");
        }

        if (!result.IsComplete)
        {
            return Reject(expectedSnapshotId, result.BattleId, "battle_result_incomplete");
        }

        if (result.SnapshotId != expectedSnapshotId)
        {
            return Reject(expectedSnapshotId, result.BattleId, "battle_snapshot_mismatch");
        }

        return new SettlementPlan
        {
            Accepted = true,
            SnapshotId = result.SnapshotId,
            BattleId = result.BattleId,
            SourceEventIds = eventStream?.EventIds.ToList() ?? new()
        };
    }

    private static SettlementPlan Reject(string snapshotId, string battleId, string reason)
    {
        return new SettlementPlan
        {
            Accepted = false,
            SnapshotId = snapshotId ?? "",
            BattleId = battleId ?? "",
            RejectionReason = reason ?? ""
        };
    }
}
```

- [x] **Step 3: Add report contracts**

Create `src/Application/Battle/Reports/BattleReportRecord.cs`:

```csharp
using System.Collections.Generic;

namespace Rpg.Application.Battle.Reports;

public sealed class BattleReportRecord
{
    public string ReportId { get; set; } = "";
    public string SnapshotId { get; set; } = "";
    public string BattleId { get; set; } = "";
    public string OutcomeSummary { get; set; } = "";
    public List<string> SourceEventIds { get; set; } = new();
    public List<string> FailureCandidates { get; set; } = new();
}
```

Create `src/Application/Battle/Reports/BattleReportBuilder.cs`:

```csharp
using System.Linq;
using Rpg.Application.Battle.Settlement;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Results;

namespace Rpg.Application.Battle.Reports;

public sealed class BattleReportBuilder
{
    public BattleReportRecord Build(
        BattleOutcomeResult result,
        BattleEventStream eventStream,
        SettlementPlan settlementPlan)
    {
        return new BattleReportRecord
        {
            ReportId = $"{result?.BattleId ?? ""}:report",
            SnapshotId = result?.SnapshotId ?? "",
            BattleId = result?.BattleId ?? "",
            OutcomeSummary = result?.TerminationReason.ToString() ?? "",
            SourceEventIds = eventStream?.EventIds.ToList() ?? new()
        };
    }
}
```

- [x] **Step 4: Run architecture regression**

Run:

```powershell
dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj
```

Expected: PASS all tests in `TargetBattleArchitectureRegression`.

- [x] **Step 5: Commit settlement and report contracts**

```powershell
git add src/Application/Battle/Settlement src/Application/Battle/Reports tests/TargetBattleArchitectureRegression
git commit -m "feat: add battle settlement and report contracts"
```

## Task 8: Legacy Boundary Adapters

**Files:**
- Create: `src/Application/Battle/Adapters/LegacyBattleGroupSeedAdapter.cs`
- Create: `src/Application/Battle/Adapters/LegacyBattleStartSnapshotAdapter.cs`
- Create: `src/Application/Battle/Adapters/LegacyBattleResultAdapter.cs`
- Test: `tests/TargetBattleArchitectureRegression/Program.cs`

- [x] **Step 1: Add adapter tests**

Append these test registrations near the top of `tests/TargetBattleArchitectureRegression/Program.cs`:

```csharp
Run("legacy garrison adapter creates explicit battle groups", LegacyGarrisonAdapterCreatesExplicitBattleGroups);
Run("legacy result adapter preserves request and outcome ids", LegacyResultAdapterPreservesRequestAndOutcomeIds);
```

Add these test methods:

```csharp
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
```

Add these usings:

```csharp
using Rpg.Application.Battle.Adapters;
using Rpg.Application.Battle;
```

- [x] **Step 2: Run adapter tests to verify they fail**

Run:

```powershell
dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj
```

Expected: FAIL at compile time with missing `Rpg.Application.Battle.Adapters`.

- [x] **Step 3: Add garrison seed adapter**

Create `src/Application/Battle/Adapters/LegacyBattleGroupSeedAdapter.cs`:

```csharp
using System.Collections.Generic;
using Rpg.Domain.BattleGroups;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.Battle.Adapters;

public sealed class LegacyBattleGroupSeedAdapter
{
    public IReadOnlyList<BattleGroupState> SeedFromGarrison(WorldSiteState site, string heroIdPrefix)
    {
        List<BattleGroupState> groups = new();
        if (site == null)
        {
            return groups;
        }

        int index = 0;
        foreach (GarrisonState garrison in site.Garrison)
        {
            for (int count = 0; count < garrison.Count; count++)
            {
                groups.Add(new BattleGroupState
                {
                    BattleGroupId = $"{site.SiteId}:{garrison.UnitTypeId}:{index}",
                    HeroId = $"{heroIdPrefix}_{index}",
                    CorpsId = $"{garrison.UnitTypeId}_corps_{index}",
                    CurrentLocationId = site.SiteId,
                    Status = BattleGroupStatus.Stationed
                });
                index++;
            }
        }

        GameLog.Info(nameof(LegacyBattleGroupSeedAdapter), $"Seeded battle groups from legacy garrison site={site.SiteId} count={groups.Count}");
        return groups;
    }
}
```

- [x] **Step 4: Add start snapshot and result adapters**

Create `src/Application/Battle/Adapters/LegacyBattleStartSnapshotAdapter.cs`:

```csharp
using System.Collections.Generic;
using Rpg.Application.Battle.Snapshots;
using Rpg.Domain.BattleGroups;
using Rpg.Domain.Corps;
using Rpg.Domain.Heroes;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.Battle.Adapters;

public sealed class LegacyBattleStartSnapshotAdapter
{
    private readonly BattleSnapshotBuilder _snapshotBuilder = new();

    public BattleStartSnapshot ToSnapshot(
        BattleStartRequest request,
        IEnumerable<BattleGroupState> groups,
        IReadOnlyDictionary<string, HeroState> heroes,
        IReadOnlyDictionary<string, CorpsState> corps)
    {
        string snapshotId = string.IsNullOrWhiteSpace(request?.RequestId)
            ? $"snapshot:{request?.ContextId ?? ""}"
            : $"snapshot:{request.RequestId}";

        BattleStartSnapshot snapshot = _snapshotBuilder.Build(
            snapshotId,
            request?.ContextId ?? "",
            request?.TargetSiteId ?? "",
            groups,
            heroes,
            corps);

        GameLog.Info(nameof(LegacyBattleStartSnapshotAdapter), $"Converted legacy battle request to snapshot request={request?.RequestId ?? ""} snapshot={snapshot.SnapshotId}");
        return snapshot;
    }
}
```

Create `src/Application/Battle/Adapters/LegacyBattleResultAdapter.cs`:

```csharp
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Results;

namespace Rpg.Application.Battle.Adapters;

public sealed class LegacyBattleResultAdapter
{
    public BattleResult ToLegacyResult(string requestId, BattleKind battleKind, BattleOutcomeResult outcome)
    {
        return new BattleResult
        {
            RequestId = requestId ?? "",
            ContextId = outcome?.BattleId ?? "",
            BattleKind = battleKind,
            Outcome = outcome?.TerminationReason == BattleTerminationReason.NormalVictory
                ? BattleOutcome.Victory
                : BattleOutcome.Defeat
        };
    }
}
```

- [x] **Step 5: Run adapter tests**

Run:

```powershell
dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj
```

Expected: PASS all tests in `TargetBattleArchitectureRegression`.

- [x] **Step 6: Commit adapters**

```powershell
git add src/Application/Battle/Adapters tests/TargetBattleArchitectureRegression
git commit -m "feat: add legacy battle architecture adapters"
```

## Task 9: Battle Group Lifecycle And Vertical Flow

**Files:**
- Create: `src/Application/BattleGroups/BattleGroupLifecycleService.cs`
- Create: `src/Application/Battle/BattleGroupBattleFlowService.cs`
- Test: `tests/TargetBattleArchitectureRegression/Program.cs`

- [x] **Step 1: Add vertical flow regression**

Append this test registration:

```csharp
Run("battle group vertical slice settles and reports from runtime facts", BattleGroupVerticalSliceSettlesAndReports);
```

Add this test method:

```csharp
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
    AssertSequence(result.SettlementPlan.SourceEventIds, result.Report.SourceEventIds, "same source events");
}
```

Add this using:

```csharp
using Rpg.Application.BattleGroups;
```

- [x] **Step 2: Run vertical flow test to verify it fails**

Run:

```powershell
dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj
```

Expected: FAIL at compile time with missing `BattleGroupLifecycleService`, `BattleGroupBattleFlowService`, and `BattleGroupBattleFlowResult`.

- [x] **Step 3: Add lifecycle service**

Create `src/Application/BattleGroups/BattleGroupLifecycleService.cs`:

```csharp
using Rpg.Domain.BattleGroups;

namespace Rpg.Application.BattleGroups;

public sealed class BattleGroupLifecycleService
{
    public BattleGroupState CreateAndStation(
        string battleGroupId,
        string heroId,
        string corpsId,
        string locationId)
    {
        return new BattleGroupState
        {
            BattleGroupId = battleGroupId ?? "",
            HeroId = heroId ?? "",
            CorpsId = corpsId ?? "",
            CurrentLocationId = locationId ?? "",
            Status = BattleGroupStatus.Stationed
        };
    }

    public bool TryLockForBattle(BattleGroupState group, string battleId)
    {
        if (group?.CanSortie != true)
        {
            return false;
        }

        group.Status = BattleGroupStatus.InBattle;
        group.ActiveBattleId = battleId ?? "";
        return true;
    }

    public void ReleaseAfterBattle(BattleGroupState group)
    {
        if (group == null)
        {
            return;
        }

        group.Status = BattleGroupStatus.Stationed;
        group.ActiveBattleId = "";
    }
}
```

- [x] **Step 4: Add vertical flow service**

Create `src/Application/Battle/BattleGroupBattleFlowService.cs`:

```csharp
using System.Collections.Generic;
using Rpg.Application.Battle.Reports;
using Rpg.Application.Battle.Settlement;
using Rpg.Application.Battle.Snapshots;
using Rpg.Domain.BattleGroups;
using Rpg.Domain.Corps;
using Rpg.Domain.Heroes;
using Rpg.Runtime.Battle;

namespace Rpg.Application.Battle;

public sealed class BattleGroupBattleFlowResult
{
    public BattleStartSnapshot Snapshot { get; init; } = new();
    public BattleRuntimeSessionResult RuntimeResult { get; init; } = new();
    public SettlementPlan SettlementPlan { get; init; } = new();
    public BattleReportRecord Report { get; init; } = new();
}

public sealed class BattleGroupBattleFlowService
{
    private readonly BattleSnapshotBuilder _snapshotBuilder = new();
    private readonly BattleRuntimeSession _runtimeSession = new();
    private readonly BattleSettlementService _settlementService = new();
    private readonly BattleReportBuilder _reportBuilder = new();

    public BattleGroupBattleFlowResult RunMinimalBattle(
        string snapshotId,
        string battleId,
        string targetLocationId,
        IEnumerable<BattleGroupState> groups,
        IReadOnlyDictionary<string, HeroState> heroes,
        IReadOnlyDictionary<string, CorpsState> corps)
    {
        BattleStartSnapshot snapshot = _snapshotBuilder.Build(
            snapshotId,
            battleId,
            targetLocationId,
            groups,
            heroes,
            corps);

        BattleRuntimeSessionResult runtimeResult = _runtimeSession.RunMinimal(snapshot);
        SettlementPlan settlementPlan = _settlementService.BuildPlan(
            snapshot.SnapshotId,
            runtimeResult.Outcome,
            runtimeResult.EventStream);
        BattleReportRecord report = _reportBuilder.Build(
            runtimeResult.Outcome,
            runtimeResult.EventStream,
            settlementPlan);

        return new BattleGroupBattleFlowResult
        {
            Snapshot = snapshot,
            RuntimeResult = runtimeResult,
            SettlementPlan = settlementPlan,
            Report = report
        };
    }
}
```

- [x] **Step 5: Run vertical flow test**

Run:

```powershell
dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj
```

Expected: PASS all tests in `TargetBattleArchitectureRegression`.

- [x] **Step 6: Commit vertical flow service**

```powershell
git add src/Application/BattleGroups src/Application/Battle/BattleGroupBattleFlowService.cs tests/TargetBattleArchitectureRegression
git commit -m "feat: wire minimal battle group vertical slice"
```

## Task 10: Proposal Notes And Full Verification

**Files:**
- Modify: `design-proposals/active/2026-05-17-hero-led-light-rts-system-architecture/implementation-notes.md`

- [x] **Step 1: Update implementation notes**

Modify `implementation-notes.md` to record the first phase entry point:

```markdown
# Implementation Notes

Status: First Phase Implemented

The first code refactor phase has been implemented beside the legacy battle path.

The user accepted the expected architecture and `code-refactor-design.md` on 2026-05-17. The first code refactor phase is tracked in `code-refactor-implementation-plan.md`.

First implementation phase:

```text
target contracts and boundary tests
-> battle-group domain state
-> snapshot and command contracts
-> runtime event/result contracts
-> settlement and report contracts
-> legacy boundary adapters
-> minimal battle-group vertical flow
```

Implementation work should use `expected/system-design/hero-led-light-rts-system-architecture.md` as the working architecture target. If implementation reveals an architecture change, pause and update the expected copy for user acceptance before continuing.
```

- [x] **Step 2: Run target architecture regression**

Run:

```powershell
dotnet run --project tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj
```

Expected: PASS all tests in `TargetBattleArchitectureRegression`.

- [x] **Step 3: Run low-concurrency solution build**

Run:

```powershell
dotnet build rpg.sln -maxcpucount:2 -v:minimal
```

Expected: exit code 0.

- [x] **Step 4: Run focused existing regression projects**

Run:

```powershell
dotnet run --project tests/AutoBattleRuntimeRegression/AutoBattleRuntimeRegression.csproj
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj
dotnet run --project tests/BattleHitFeedbackRegression/BattleHitFeedbackRegression.csproj
```

Expected: all three commands exit code 0.

- [x] **Step 5: Clean build servers**

Run:

```powershell
dotnet build-server shutdown
```

Expected: command exits successfully and reports build servers shut down or no servers running.

- [x] **Step 6: Commit proposal note update**

```powershell
git add design-proposals/active/2026-05-17-hero-led-light-rts-system-architecture/implementation-notes.md
git commit -m "docs: record battle architecture first phase plan"
```

## Self-Review Checklist

- [x] Every target file is listed in the File Structure section.
- [x] Boundary tests are written before target implementation tasks.
- [x] Runtime source isolation is tested.
- [x] Domain source isolation is tested.
- [x] Snapshot copies facts instead of passing live Domain objects.
- [x] Settlement rejects incomplete and mismatched results.
- [x] Report and Settlement consume the same event IDs.
- [x] Adapters are boundary-only and log migration decisions.
- [x] The plan does not edit UI scenes in this phase.
- [x] Verification uses low-concurrency `dotnet build`.
