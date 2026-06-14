using System.Collections.Generic;
using Rpg.Domain.StrategicManagement;

namespace Rpg.Application.StrategicManagement;

public sealed class StrategicCommandResult
{
    public bool Success { get; set; }
    public string FailureReason { get; set; } = "";
    public string CreatedEntityId { get; set; } = "";
    public StrategicHeroCorpsAptitudeGrade AptitudeGrade { get; set; } = StrategicHeroCorpsAptitudeGrade.B;
    public List<string> ChangedFactIds { get; set; } = new();
    public List<StrategicEvent> Events { get; set; } = new();

    public static StrategicCommandResult Failed(string failureReason)
    {
        return new StrategicCommandResult
        {
            Success = false,
            FailureReason = string.IsNullOrWhiteSpace(failureReason)
                ? StrategicFailureReasons.MissingDefinitions
                : failureReason
        };
    }

    public static StrategicCommandResult Ok(params string[] changedFactIds)
    {
        StrategicCommandResult result = new() { Success = true };
        foreach (string changedFactId in changedFactIds ?? System.Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(changedFactId))
            {
                result.ChangedFactIds.Add(changedFactId);
            }
        }

        return result;
    }
}
