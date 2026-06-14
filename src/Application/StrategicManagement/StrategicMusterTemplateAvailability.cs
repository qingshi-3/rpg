using System.Collections.Generic;

namespace Rpg.Application.StrategicManagement;

public sealed class StrategicMusterTemplateAvailability
{
    public string CorpsDefinitionId { get; set; } = "";
    public bool IsAvailable => FailureReasons.Count == 0;
    public List<string> FailureReasons { get; set; } = new();
}
