using Rpg.Definitions.StrategicManagement;
using Rpg.Domain.StrategicManagement;

namespace Rpg.Application.StrategicManagement;

public sealed class StrategicWorldTimeflowController
{
    private readonly StrategicManagementCommandService _commands;

    public StrategicWorldTimeflowController(StrategicManagementCommandService commands)
    {
        _commands = commands ?? new StrategicManagementCommandService(
            new StrategicManagementDefinitionSet(),
            new StrategicManagementRules(new StrategicManagementDefinitionSet()));
    }

    public string Mode { get; private set; } = StrategicWorldTimeflowModes.WorldMapRunning;

    public bool IsWorldMapRunning =>
        string.Equals(Mode, StrategicWorldTimeflowModes.WorldMapRunning, System.StringComparison.Ordinal);

    public void ResumeWorldMapTime()
    {
        Mode = StrategicWorldTimeflowModes.WorldMapRunning;
    }

    public void PauseForCityManagement()
    {
        Mode = StrategicWorldTimeflowModes.PausedForCityManagement;
    }

    public StrategicCommandResult SettleElapsedWorldTime(
        StrategicManagementState state,
        string factionId,
        int elapsedPulses)
    {
        if (!IsWorldMapRunning)
        {
            return StrategicCommandResult.Failed(StrategicFailureReasons.WorldTimePaused);
        }

        return _commands.SettleElapsedWorldTime(state, factionId, elapsedPulses);
    }
}

public static class StrategicWorldTimeflowModes
{
    public const string WorldMapRunning = "world_map_running";
    public const string PausedForCityManagement = "paused_for_city_management";
}
