using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.StrategicBattleBridge;
using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;
using Rpg.Infrastructure.Logging;

namespace Rpg.Application.StrategicManagement;

public sealed partial class StrategicManagementCommandService
{
    private readonly StrategicManagementDefinitionSet _definitions;
    private readonly StrategicManagementRules _rules;

    public StrategicManagementCommandService(
        StrategicManagementDefinitionSet definitions,
        StrategicManagementRules rules)
    {
        _definitions = definitions ?? new StrategicManagementDefinitionSet();
        _rules = rules ?? new StrategicManagementRules(_definitions);
    }

    private static StrategicEvent Event(
        string kind,
        string targetId,
        params (string Key, string Value)[] payload)
    {
        StrategicEvent strategicEvent = new()
        {
            Kind = kind
        };
        if (!string.IsNullOrWhiteSpace(targetId))
        {
            strategicEvent.TargetIds.Add(targetId);
        }

        foreach ((string key, string value) in payload ?? System.Array.Empty<(string, string)>())
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                strategicEvent.Payload[key] = value ?? "";
            }
        }

        return strategicEvent;
    }

    private static string FormatResourceAmounts(System.Collections.Generic.IReadOnlyList<StrategicResourceAmount> amounts)
    {
        return string.Join(
            ",",
            (amounts ?? System.Array.Empty<StrategicResourceAmount>())
                .Where(item => !string.IsNullOrWhiteSpace(item.ResourceId) && item.Amount != 0)
                .OrderBy(item => item.ResourceId)
                .Select(item => $"{item.ResourceId}:{item.Amount}"));
    }

    private static System.Collections.Generic.IReadOnlyList<StrategicResourceAmount> NormalizeResourceAmounts(
        System.Collections.Generic.IReadOnlyCollection<StrategicResourceAmount> amounts)
    {
        return (amounts ?? System.Array.Empty<StrategicResourceAmount>())
            .Where(item => item.Amount > 0 && !string.IsNullOrWhiteSpace(item.ResourceId))
            .GroupBy(item => item.ResourceId, System.StringComparer.Ordinal)
            .Select(group => new StrategicResourceAmount(group.Key, group.Sum(item => item.Amount)))
            .OrderBy(item => item.ResourceId)
            .ToList();
    }

    private static System.Collections.Generic.IReadOnlyList<StrategicResourceAmount> CombineResourceAmounts(
        System.Collections.Generic.IReadOnlyCollection<StrategicResourceAmount> first,
        System.Collections.Generic.IReadOnlyCollection<StrategicResourceAmount> second,
        int secondSign)
    {
        System.Collections.Generic.Dictionary<string, int> totals = new(System.StringComparer.Ordinal);
        AddAmounts(totals, first, 1);
        AddAmounts(totals, second, secondSign);
        return totals
            .Where(item => item.Value != 0)
            .OrderBy(item => item.Key)
            .Select(item => new StrategicResourceAmount(item.Key, item.Value))
            .ToList();
    }

    private static void AddAmounts(
        System.Collections.Generic.Dictionary<string, int> totals,
        System.Collections.Generic.IReadOnlyCollection<StrategicResourceAmount> amounts,
        int sign)
    {
        foreach (StrategicResourceAmount amount in amounts ?? System.Array.Empty<StrategicResourceAmount>())
        {
            if (string.IsNullOrWhiteSpace(amount.ResourceId) || amount.Amount == 0)
            {
                continue;
            }

            totals.TryGetValue(amount.ResourceId, out int current);
            totals[amount.ResourceId] = current + (amount.Amount * sign);
        }
    }

    private static void AddUnique(System.Collections.Generic.ICollection<string> values, string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !values.Contains(value))
        {
            values.Add(value);
        }
    }

    private static StrategicCommandResult Reject(string commandKind, string targetId, string failureReason)
    {
        GameLog.Warn(
            nameof(StrategicManagementCommandService),
            $"StrategicCommandRejected command={commandKind} target={targetId ?? ""} reason={failureReason}");
        return StrategicCommandResult.Failed(failureReason);
    }

    private static void Accept(string commandKind, string targetId, StrategicCommandResult result)
    {
        GameLog.Info(
            nameof(StrategicManagementCommandService),
            $"StrategicCommandAccepted command={commandKind} target={targetId ?? ""} changed={string.Join(",", result.ChangedFactIds)}");
    }
}
