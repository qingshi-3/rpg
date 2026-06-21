using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

internal static class TargetBattleRuntimeIdentityRulesRegressionCases
{
    internal static void Register(Action<string, Action> run)
    {
        run("runtime identity and command rules are not owned by tick resolver", RuntimeIdentityAndCommandRulesAreNotOwnedByTickResolver);
        run("runtime identity rules preserve command and faction compatibility", RuntimeIdentityRulesPreserveCommandAndFactionCompatibility);
    }

    private static void RuntimeIdentityAndCommandRulesAreNotOwnedByTickResolver()
    {
        string root = ProjectRoot();
        string battleRuntimePath = Path.Combine(root, "src", "Runtime", "Battle");
        string helperPath = Path.Combine(battleRuntimePath, "BattleRuntimeIdentityRules.cs");
        string tickResolverPath = Path.Combine(battleRuntimePath, "BattleRuntimeTickResolver.cs");
        string sessionPath = Path.Combine(battleRuntimePath, "BattleRuntimeSession.cs");

        AssertTrue(File.Exists(helperPath), "runtime faction and command rules should live in BattleRuntimeIdentityRules");
        string helperSource = File.ReadAllText(helperPath);
        AssertContains(helperSource, "class BattleRuntimeIdentityRules", ToRepoPath(root, helperPath), "identity rules helper should be authored");
        AssertContains(helperSource, "NormalizeCorpsCommandId", ToRepoPath(root, helperPath), "identity rules should normalize command ids");
        AssertContains(helperSource, "IsFocusFireCommand", ToRepoPath(root, helperPath), "identity rules should own focus-fire command checks");
        AssertContains(helperSource, "IsHoldLineCommand", ToRepoPath(root, helperPath), "identity rules should own hold-line command checks");
        AssertContains(helperSource, "SameFaction", ToRepoPath(root, helperPath), "identity rules should own same-faction checks");
        AssertContains(helperSource, "NormalizeFaction", ToRepoPath(root, helperPath), "identity rules should own faction normalization");
        AssertContains(helperSource, "IsPlayerFaction", ToRepoPath(root, helperPath), "identity rules should own player-faction checks");

        string tickResolverSource = File.ReadAllText(tickResolverPath);
        string tickResolverRelativePath = ToRepoPath(root, tickResolverPath);
        AssertDoesNotContain(tickResolverSource, "CommandFocusFire", tickResolverRelativePath, "tick resolver should not own command constants");
        AssertDoesNotContain(tickResolverSource, "CommandHoldLine", tickResolverRelativePath, "tick resolver should not own command constants");
        AssertDoesNotContain(tickResolverSource, "CommandAssault", tickResolverRelativePath, "tick resolver should not own command constants");
        AssertNoMethodDefinition(tickResolverSource, tickResolverRelativePath, "IsFocusFireCommand");
        AssertNoMethodDefinition(tickResolverSource, tickResolverRelativePath, "IsHoldLineCommand");
        AssertNoMethodDefinition(tickResolverSource, tickResolverRelativePath, "NormalizeCorpsCommandId");
        AssertNoMethodDefinition(tickResolverSource, tickResolverRelativePath, "SameFaction");
        AssertNoMethodDefinition(tickResolverSource, tickResolverRelativePath, "NormalizeFaction");

        string sessionSource = File.ReadAllText(sessionPath);
        string sessionRelativePath = ToRepoPath(root, sessionPath);
        AssertDoesNotContain(sessionSource, "private const string CommandFocusFire", sessionRelativePath, "runtime session should use shared command normalization");
        AssertDoesNotContain(sessionSource, "private const string CommandHoldLine", sessionRelativePath, "runtime session should use shared command normalization");
        AssertDoesNotContain(sessionSource, "private const string CommandAssault", sessionRelativePath, "runtime session should use shared command normalization");
        AssertNoMethodDefinition(sessionSource, sessionRelativePath, "NormalizeCorpsCommandId");
        AssertNoMethodDefinition(sessionSource, sessionRelativePath, "IsHoldLineCommand");
        AssertNoMethodDefinition(sessionSource, sessionRelativePath, "SameFaction");
        AssertNoMethodDefinition(sessionSource, sessionRelativePath, "NormalizeFaction");
        AssertNoMethodDefinition(sessionSource, sessionRelativePath, "IsPlayerFaction");

        foreach (string runtimeFile in Directory.GetFiles(battleRuntimePath, "*.cs", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal))
        {
            if (string.Equals(runtimeFile, helperPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string source = File.ReadAllText(runtimeFile);
            string relativePath = ToRepoPath(root, runtimeFile);
            AssertDoesNotContain(source, "BattleRuntimeTickResolver.SameFaction", relativePath, "runtime services should use BattleRuntimeIdentityRules for faction checks");
            AssertDoesNotContain(source, "BattleRuntimeTickResolver.NormalizeFaction", relativePath, "runtime services should use BattleRuntimeIdentityRules for faction normalization");
            AssertDoesNotContain(source, "BattleRuntimeTickResolver.IsFocusFireCommand", relativePath, "runtime services should use BattleRuntimeIdentityRules for focus-fire checks");
            AssertDoesNotContain(source, "BattleRuntimeTickResolver.IsHoldLineCommand", relativePath, "runtime services should use BattleRuntimeIdentityRules for hold-line checks");
            AssertDoesNotContain(source, "BattleRuntimeTickResolver.NormalizeCorpsCommandId", relativePath, "runtime services should use BattleRuntimeIdentityRules for command normalization");
            AssertNoDuplicate(source, relativePath, "NormalizeFaction");
            AssertNoDuplicate(source, relativePath, "SameFaction");
            AssertNoDuplicate(source, relativePath, "NormalizeCorpsCommandId");
            AssertNoDuplicate(source, relativePath, "IsFocusFireCommand");
            AssertNoDuplicate(source, relativePath, "IsHoldLineCommand");
            AssertNoDuplicate(source, relativePath, "IsPlayerFaction");
        }
    }

    private static void RuntimeIdentityRulesPreserveCommandAndFactionCompatibility()
    {
        AssertEqual("player", InvokeString("NormalizeFaction", null), "null faction defaults to player");
        AssertEqual("player", InvokeString("NormalizeFaction", ""), "empty faction defaults to player");
        AssertEqual("player", InvokeString("NormalizeFaction", "   "), "blank faction defaults to player");
        AssertTrue(InvokeBool("SameFaction", " player ", "player"), "same-faction checks should use shared normalization");
        AssertTrue(InvokeBool("IsPlayerFaction", " player "), "player-faction check should use shared normalization");

        AssertEqual("FocusFire", InvokeString("NormalizeCorpsCommandId", "focusfire"), "focus-fire command is case-insensitive");
        AssertEqual("FocusFire", InvokeString("NormalizeCorpsCommandId", " FocusFire "), "focus-fire command trims whitespace");
        AssertTrue(InvokeBool("IsFocusFireCommand", " FocusFire "), "focus-fire command check uses normalized command");

        AssertEqual("HoldLine", InvokeString("NormalizeCorpsCommandId", "holdline"), "hold-line command is case-insensitive");
        AssertEqual("HoldLine", InvokeString("NormalizeCorpsCommandId", " HoldLine "), "hold-line command trims whitespace");
        AssertTrue(InvokeBool("IsHoldLineCommand", " HoldLine "), "hold-line command check uses normalized command");

        AssertEqual("Assault", InvokeString("NormalizeCorpsCommandId", null), "null command defaults to Assault");
        AssertEqual("Assault", InvokeString("NormalizeCorpsCommandId", ""), "empty command defaults to Assault");
        AssertEqual("Assault", InvokeString("NormalizeCorpsCommandId", "unknown"), "unknown command defaults to Assault");
    }

    private static void AssertNoMethodDefinition(string source, string relativePath, string methodName)
    {
        AssertTrue(
            !Regex.IsMatch(source, $@"\b(?:private|internal|public)\s+static\s+\w+(?:\s*\?)?\s+{Regex.Escape(methodName)}\s*\("),
            $"center/runtime file should not define shared identity rule method: file={relativePath} method={methodName}");
    }

    private static void AssertNoDuplicate(string source, string relativePath, string methodName)
    {
        AssertTrue(
            !Regex.IsMatch(source, $@"\b(?:private|internal|public)\s+static\s+\w+(?:\s*\?)?\s+{Regex.Escape(methodName)}\s*\("),
            $"runtime file should not duplicate shared identity rules: file={relativePath} method={methodName}");
    }

    private static string InvokeString(string methodName, string? value)
    {
        object? result = Invoke(methodName, new[] { typeof(string) }, value);
        return result as string ?? throw new Exception($"identity rule should return string: method={methodName}");
    }

    private static bool InvokeBool(string methodName, params string?[] values)
    {
        object? result = Invoke(methodName, values.Select(_ => typeof(string)).ToArray(), values.Cast<object?>().ToArray());
        return result is bool boolResult
            ? boolResult
            : throw new Exception($"identity rule should return bool: method={methodName}");
    }

    private static object? Invoke(string methodName, Type[] parameterTypes, params object?[] values)
    {
        Type type = typeof(Rpg.Runtime.Battle.BattleRuntimeSession).Assembly.GetType("Rpg.Runtime.Battle.BattleRuntimeIdentityRules") ??
                    throw new Exception("BattleRuntimeIdentityRules type not found");
        MethodInfo method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic, null, parameterTypes, null) ??
                            throw new Exception($"identity rule method not found: {methodName}");
        return method.Invoke(null, values);
    }

    private static string ProjectRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current != null && !File.Exists(Path.Combine(current.FullName, "rpg.csproj")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("project root not found");
    }

    private static string ToRepoPath(string root, string path)
    {
        return Path.GetRelativePath(root, path).Replace('\\', '/');
    }

    private static void AssertContains(string source, string expected, string relativePath, string message)
    {
        AssertTrue(source.Contains(expected, StringComparison.Ordinal), $"{message}: file={relativePath} expected={expected}");
    }

    private static void AssertDoesNotContain(string source, string forbidden, string relativePath, string message)
    {
        AssertTrue(!source.Contains(forbidden, StringComparison.Ordinal), $"{message}: file={relativePath} forbidden={forbidden}");
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
        if (!Equals(expected, actual))
        {
            throw new Exception($"{message}: expected={expected} actual={actual}");
        }
    }
}
