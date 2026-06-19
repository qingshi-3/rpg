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
            amounts.Select(item => $"{item.ResourceId}:{item.Amount}"));
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
