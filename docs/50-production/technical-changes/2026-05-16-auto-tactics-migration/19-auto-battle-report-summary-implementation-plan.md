# Auto Battle Report Summary Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the minimum player-readable Chinese summary for completed auto battles and surface it through the existing WorldSite management notice.

**Architecture:** Keep report presentation below full HUD scope. Add a pure application-layer formatter that converts `AutoBattleReport` into stable Chinese notice text, then have the opt-in `WorldSiteRoot` auto battle branch append that summary to the existing world writeback message. `WorldSiteRoot` remains a composition shell; formatting owns text, `WorldBattleResultApplier` owns world mutation, and legacy manual battle UI stays unchanged.

**Tech Stack:** Godot 4.5 C#, .NET 8, `AutoBattleReport`, `WorldSiteAutoBattleAdapter`, `WorldSiteRoot`, console regression projects under `tests/`.

---

## Required Reading

- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration.md`
- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration/05-playback-ui-and-report.md`
- `docs/50-production/technical-changes/2026-05-16-auto-tactics-migration/18-worldsite-auto-battle-adapter-implementation-plan.md`
- `src/Application/Battle/Auto/AutoBattleReport.cs`
- `src/Application/Battle/Auto/AutoBattleForceReport.cs`
- `src/Application/Battle/Auto/AutoBattleReportBuilder.cs`
- `src/Presentation/World/Sites/WorldSiteRoot.cs`
- `tests/AutoBattleRuntimeRegression/Program.cs`
- `tests/WorldSiteDeploymentCacheRegression/Program.cs`

## Scope Boundaries

- Do not build a final battle report panel, playback HUD, event feed scene, animation, or authored UI resource in this slice.
- Do not make auto battle the default. `UseAutoBattleRuntime` remains opt-in.
- Do not change manual battle completion or `OnBattleEnded()` behavior.
- Do not mutate `StrategicWorldState` or `WorldSiteState` from the formatter.
- Do not infer casualties from scene nodes. The summary reads only `AutoBattleReport`.
- Do not add AP, `TurnSystem`, `BattleActionMenu`, or manual battle-time commands.

## File Structure

- Create `src/Application/Battle/Auto/AutoBattleReportSummaryFormatter.cs`
  - Pure formatter for concise Chinese summary text from `AutoBattleReport`.
- Modify `src/Presentation/World/Sites/WorldSiteRoot.cs`
  - Append formatter output to the auto battle branch's existing world writeback notice before returning to management UI.
- Modify `tests/AutoBattleRuntimeRegression/Program.cs`
  - Add formatter tests for victory, defeat reason text, and contribution line.
- Modify `tests/WorldSiteDeploymentCacheRegression/Program.cs`
  - Add source guard that the auto battle branch uses the formatter and avoids building a report panel in `WorldSiteRoot`.

## Task 1: Add Failing Formatter Tests

**Files:**
- Modify: `tests/AutoBattleRuntimeRegression/Program.cs`

- [ ] **Step 1: Add test run lines**

Add:

```csharp
Run("auto battle report summary formatter writes victory notice", AutoBattleReportSummaryFormatterWritesVictoryNotice);
Run("auto battle report summary formatter explains defeat reason", AutoBattleReportSummaryFormatterExplainsDefeatReason);
```

- [ ] **Step 2: Add victory summary test**

Add a test that creates an `AutoBattleReport` directly:

```csharp
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
```

- [ ] **Step 3: Add defeat summary test**

Add a test that checks failure reason localization:

```csharp
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
```

- [ ] **Step 4: Run tests and verify red**

Run:

```powershell
dotnet run --project tests/AutoBattleRuntimeRegression/AutoBattleRuntimeRegression.csproj
```

Expected: compile fails because `AutoBattleReportSummaryFormatter` does not exist.

## Task 2: Implement Pure Summary Formatter

**Files:**
- Create: `src/Application/Battle/Auto/AutoBattleReportSummaryFormatter.cs`

- [ ] **Step 1: Add formatter class**

Add:

```csharp
using System;
using System.Linq;

namespace Rpg.Application.Battle.Auto;

public sealed class AutoBattleReportSummaryFormatter
{
    public string Format(AutoBattleReport report)
    {
        if (report == null)
        {
            return "";
        }

        string outcome = report.Outcome switch
        {
            BattleOutcome.Victory => "自动战斗胜利",
            BattleOutcome.Defeat => "自动战斗失败",
            BattleOutcome.Draw => "自动战斗僵持",
            _ => "自动战斗结束"
        };

        string summary = $"{outcome}：参战 {Math.Max(0, report.InitialUnitCount)}，生还 {Math.Max(0, report.SurvivedUnitCount)}，战损 {Math.Max(0, report.DefeatedUnitCount)}。";
        string reason = FormatFailureReason(report.TopFailureReason);
        if (!string.IsNullOrWhiteSpace(reason))
        {
            summary += $" 失败原因：{reason}。";
        }

        AutoBattleForceReport topContribution = report.ForceReports
            .OrderByDescending(item => Math.Max(0, item.DamageDealt))
            .ThenByDescending(item => Math.Max(0, item.UnitsDefeated))
            .FirstOrDefault(item => Math.Max(0, item.DamageDealt) > 0 || Math.Max(0, item.UnitsDefeated) > 0);
        if (topContribution != null)
        {
            summary += $" 主要贡献：{ResolveForceLabel(topContribution)} 造成 {Math.Max(0, topContribution.DamageDealt)} 伤害，击败 {Math.Max(0, topContribution.UnitsDefeated)}。";
        }

        return summary;
    }

    private static string FormatFailureReason(string reasonKey)
    {
        return reasonKey switch
        {
            "player_force_eliminated" => "我方战斗单位全部失去战斗力",
            _ => ""
        };
    }

    private static string ResolveForceLabel(AutoBattleForceReport report)
    {
        return string.IsNullOrWhiteSpace(report.ForceId) ? "未知部队" : report.ForceId.Trim();
    }
}
```

- [ ] **Step 2: Run focused test**

Run:

```powershell
dotnet run --project tests/AutoBattleRuntimeRegression/AutoBattleRuntimeRegression.csproj
```

Expected: formatter tests pass. Any failure should be fixed in the formatter, not by weakening the tests.

## Task 3: Wire Summary Into Auto WorldSite Return Notice

**Files:**
- Modify: `src/Presentation/World/Sites/WorldSiteRoot.cs`
- Modify: `tests/WorldSiteDeploymentCacheRegression/Program.cs`

- [ ] **Step 1: Add failing source guard**

Add:

```csharp
Run("world site root appends auto battle report summary to notice", WorldSiteRootAppendsAutoBattleReportSummaryToNotice);
```

Add source assertions:

```csharp
static void WorldSiteRootAppendsAutoBattleReportSummaryToNotice()
{
    string rootSource = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Presentation", "World", "Sites", "WorldSiteRoot.cs"));

    AssertTrue(
        rootSource.Contains("AutoBattleReportSummaryFormatter", StringComparison.Ordinal),
        "WorldSiteRoot auto branch should use the application-layer auto battle report summary formatter");
    AssertTrue(
        rootSource.Contains("BuildAutoBattleReturnNotice", StringComparison.Ordinal),
        "WorldSiteRoot should keep notice composition in a focused helper");
    AssertTrue(
        rootSource.Contains("autoBattleNotice", StringComparison.Ordinal),
        "WorldSiteRoot should pass the auto battle summary notice into existing non-battle UI refresh");
    AssertTrue(
        !rootSource.Contains("AutoBattleReportPanel", StringComparison.Ordinal),
        "this slice should not add a full report panel to WorldSiteRoot");
}
```

Run:

```powershell
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj
```

Expected: source guard fails until `WorldSiteRoot` is wired.

- [ ] **Step 2: Add formatter field and notice helper**

In `WorldSiteRoot`, add:

```csharp
private readonly AutoBattleReportSummaryFormatter _autoBattleReportSummaryFormatter = new();
```

Add a helper near `ActivateAutoBattleRuntime`:

```csharp
private string BuildAutoBattleReturnNotice(WorldActionResult applyResult, AutoBattleReport report)
{
    string worldMessage = applyResult?.Message?.Trim() ?? "";
    string reportSummary = _autoBattleReportSummaryFormatter.Format(report).Trim();
    if (string.IsNullOrWhiteSpace(reportSummary))
    {
        return worldMessage;
    }

    if (string.IsNullOrWhiteSpace(worldMessage))
    {
        return reportSummary;
    }

    return $"{worldMessage}\n{reportSummary}";
}
```

- [ ] **Step 3: Use notice in auto branch only**

After `ApplyBattleResultToWorld(...)` in `ActivateAutoBattleRuntime`, build:

```csharp
string autoBattleNotice = BuildAutoBattleReturnNotice(applyResult, resolution.Report);
```

Pass `autoBattleNotice` into `SwitchToNonBattleUi(...)` by replacing the `applyResult` argument with a copy whose `Message` is `autoBattleNotice`, or by passing an equivalent result object. Do not change the manual `OnBattleEnded()` path.

- [ ] **Step 4: Run focused tests**

Run:

```powershell
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj
```

Expected: source guard passes.

## Task 4: Verification

Run:

```powershell
dotnet run --project tests/AutoBattleRuntimeRegression/AutoBattleRuntimeRegression.csproj
dotnet run --project tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj
dotnet run --project tests/WorldSiteIntelRegression/WorldSiteIntelRegression.csproj
dotnet run --project tests/BattleHitFeedbackRegression/BattleHitFeedbackRegression.csproj
dotnet build rpg.csproj -maxcpucount:2 -v:minimal
dotnet build-server shutdown
```

Expected:

- all regression projects exit `0`;
- main project build exits `0`;
- known Godot source generator warning may appear in console test projects and does not block this slice.

## Self-Review Checklist

- Formatter is pure application-layer code with no Godot or world-state dependency.
- Summary text is concise Chinese and does not require a full HUD.
- Auto branch appends the summary to existing management notice after world writeback.
- Manual battle completion path remains unchanged.
- No new report panel, AP, `TurnSystem`, or manual command dependency was introduced.
