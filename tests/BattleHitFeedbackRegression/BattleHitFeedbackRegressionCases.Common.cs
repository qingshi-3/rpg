using Rpg.Presentation.Battle.Actions;
using Rpg.Domain.Battle.Grid;
using Rpg.Presentation.Battle;
using Rpg.Presentation.Battle.Entities;
using Rpg.Presentation.Battle.Debug;
using Rpg.Presentation.Battle.Feedback;
using Rpg.Presentation.Battle.Flow;
using Rpg.Presentation.Battle.Preview;
using Rpg.Presentation.Common;
using Rpg.Presentation.World;
using Rpg.Definitions.Battle.Audio;
using Rpg.Application.Battle;
using Rpg.Application.World;
using Rpg.Definitions.World;
using Rpg.Domain.World;
using System.Text.Json;

internal static partial class BattleHitFeedbackRegressionCases
{
internal static void Run(string name, Action test)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
        Environment.ExitCode = 1;
    }
}

internal static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{message}: expected={expected} actual={actual}");
    }
}

internal static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

internal static void AssertFloatEqual(float expected, float actual, float tolerance, string message)
{
    if (MathF.Abs(expected - actual) > tolerance)
    {
        throw new InvalidOperationException($"{message}: expected={expected} actual={actual}");
    }
}

internal static string ReadStrategicWorldRootSource()
{
    string dir = Path.Combine("src", "Presentation", "World");
    return string.Join("\n", Directory.GetFiles(dir, "StrategicWorldRoot*.cs").OrderBy(path => path).Select(File.ReadAllText));
}

internal static string ReadWorldSiteRootSource()
{
    string dir = Path.Combine("src", "Presentation", "World", "Sites");
    return string.Join("\n", Directory.GetFiles(dir, "WorldSiteRoot*.cs").OrderBy(path => path).Select(File.ReadAllText));
}

internal static string ReadBattleRuntimePresentationSource()
{
    string dir = Path.Combine("src", "Presentation", "World", "Sites");
    string[] files = Directory.GetFiles(dir, "WorldSiteRoot.BattleRuntime*.cs")
        .Append(Path.Combine(dir, "BattleRuntimeLivePresentationState.cs"))
        .Where(File.Exists)
        .OrderBy(path => path, StringComparer.Ordinal)
        .ToArray();
    return string.Join("\n", files.Select(File.ReadAllText));
}

internal static string ReadBattleUnitRootSource()
{
    string dir = Path.Combine("src", "Presentation", "Battle", "Entities");
    return string.Join("\n", Directory
        .GetFiles(dir, "BattleUnitRoot*.cs")
        .OrderBy(path => path, StringComparer.Ordinal)
        .Select(File.ReadAllText));
}

internal static string ExtractMethodBlock(string source, string methodSignature)
{
    int signatureIndex = source.IndexOf(methodSignature, StringComparison.Ordinal);
    if (signatureIndex < 0)
    {
        throw new InvalidOperationException($"missing method signature: {methodSignature}");
    }

    int braceIndex = source.IndexOf('{', signatureIndex);
    if (braceIndex < 0)
    {
        throw new InvalidOperationException($"missing method body: {methodSignature}");
    }

    int depth = 0;
    for (int i = braceIndex; i < source.Length; i++)
    {
        if (source[i] == '{')
        {
            depth++;
        }
        else if (source[i] == '}')
        {
            depth--;
            if (depth == 0)
            {
                return source[braceIndex..(i + 1)];
            }
        }
    }

    throw new InvalidOperationException($"unterminated method body: {methodSignature}");
}

internal static string NormalizeWhitespace(string source)
{
    return string.Join(" ", source.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}

internal static void AssertSequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string message)
{
    if (expected.Count != actual.Count)
    {
        throw new InvalidOperationException($"{message}: expectedCount={expected.Count} actualCount={actual.Count}");
    }

    for (int index = 0; index < expected.Count; index++)
    {
        if (!EqualityComparer<T>.Default.Equals(expected[index], actual[index]))
        {
            throw new InvalidOperationException($"{message}: index={index} expected={expected[index]} actual={actual[index]}");
        }
    }
}
}
