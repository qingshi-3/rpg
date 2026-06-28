using System.Collections.Generic;
using Godot;
using Rpg.Domain.World;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.World;

public sealed class StrategicCommandNavigationResult
{
    public Dictionary<string, StrategicNavigationPath> CommandPaths { get; } = new();
    public List<string> DeferredArmyIds { get; } = new();
    public bool HasDeferredPaths => DeferredArmyIds.Count > 0;
}

public static class StrategicCommandNavigationService
{
    public static bool TryBuildOrDeferPaths(
        IReadOnlyList<WorldArmyState> armies,
        Vector2 destination,
        IStrategicNavigationContext navigationContext,
        out StrategicCommandNavigationResult result,
        out string failureReason)
    {
        result = new StrategicCommandNavigationResult();
        failureReason = "";
        if (armies == null || armies.Count == 0)
        {
            failureReason = "no_commandable_army";
            return false;
        }

        if (navigationContext == null)
        {
            failureReason = "strategic_navigation_context_missing";
            GameLog.Warn(nameof(StrategicCommandNavigationService), $"StrategicCommandPathRequestRejected reason={failureReason} destination={destination}");
            return false;
        }

        GameLog.Info(
            nameof(StrategicCommandNavigationService),
            $"StrategicCommandPathRequest count={armies.Count} destination={destination} navigation={DescribeNavigationContext(navigationContext)}");

        int commandableArmyCount = 0;
        foreach (WorldArmyState army in armies)
        {
            if (army == null)
            {
                continue;
            }

            commandableArmyCount++;
            if (!TryBuildOrDeferPath(
                    navigationContext,
                    army.ArmyId,
                    army.WorldPosition,
                    destination,
                    out StrategicNavigationPath path,
                    out bool isDeferred,
                    out string pathFailureReason))
            {
                result.CommandPaths.Clear();
                result.DeferredArmyIds.Clear();
                failureReason = pathFailureReason;
                return false;
            }

            if (isDeferred)
            {
                result.DeferredArmyIds.Add(army.ArmyId);
                continue;
            }

            result.CommandPaths[army.ArmyId] = path;
        }

        if (commandableArmyCount == 0)
        {
            failureReason = "no_commandable_army";
            GameLog.Warn(nameof(StrategicCommandNavigationService), $"StrategicCommandPathRequestRejected reason={failureReason} destination={destination}");
            return false;
        }

        return true;
    }

    public static bool TryBuildOrDeferPath(
        IStrategicNavigationContext navigationContext,
        string subjectId,
        Vector2 start,
        Vector2 destination,
        out StrategicNavigationPath path,
        out bool isDeferred,
        out string failureReason)
    {
        path = null;
        isDeferred = false;
        failureReason = "";
        if (navigationContext == null)
        {
            failureReason = "strategic_navigation_context_missing";
            GameLog.Warn(nameof(StrategicCommandNavigationService), $"StrategicCommandPathRejected subject={subjectId} reason={failureReason} start={start} destination={destination}");
            return false;
        }

        if (navigationContext.TryBuildPath(start, destination, out path, out failureReason))
        {
            GameLog.Info(
                nameof(StrategicCommandNavigationService),
                $"StrategicCommandPathBuilt subject={subjectId} start={start} destination={destination} points={path.Points.Count} navigation={DescribeNavigationContext(navigationContext)}");
            return true;
        }

        GameLog.Warn(
            nameof(StrategicCommandNavigationService),
            $"StrategicCommandPathRejected subject={subjectId} reason={failureReason} start={start} destination={destination} {DescribePointMapping(navigationContext, "start", start)} {DescribePointMapping(navigationContext, "destination", destination)} navigation={DescribeNavigationContext(navigationContext)}");
        failureReason = PrefixSubject(subjectId, failureReason);
        return false;
    }

    private static string PrefixSubject(string subjectId, string failureReason)
    {
        return string.IsNullOrWhiteSpace(subjectId)
            ? failureReason
            : $"army={subjectId} {failureReason}";
    }

    private static string DescribeNavigationContext(IStrategicNavigationContext navigationContext)
    {
        return navigationContext is StrategicNavigationContext strategicContext
            ? strategicContext.DiagnosticsSummary
            : $"provider={navigationContext?.PrimaryProviderId ?? "<missing>"} version={navigationContext?.Version ?? 0}";
    }

    private static string DescribePointMapping(IStrategicNavigationContext navigationContext, string label, Vector2 point)
    {
        return navigationContext is StrategicNavigationContext strategicContext
            ? $"{label}({strategicContext.DescribePointForDiagnostics(point)})"
            : $"{label}(point={point})";
    }
}
