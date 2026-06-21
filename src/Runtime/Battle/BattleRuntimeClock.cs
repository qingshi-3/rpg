using Rpg.Application.Battle;

namespace Rpg.Runtime.Battle;

public sealed class BattleRuntimeClock
{
    public double CurrentTimeSeconds { get; private set; }
    public bool IsPaused { get; private set; }
    public string PauseReason { get; private set; } = "";

    public void SetPaused(bool paused, string reason)
    {
        IsPaused = paused;
        PauseReason = paused ? reason ?? "" : "";
    }

    public double NormalizeFixedDelta(double fixedDeltaSeconds)
    {
        return double.IsNaN(fixedDeltaSeconds) ||
               double.IsInfinity(fixedDeltaSeconds) ||
               fixedDeltaSeconds <= 0
            ? BattleActionTimingPolicy.DefaultSimulationTickSeconds
            : System.Math.Clamp(fixedDeltaSeconds, 0.001, BattleActionTimingPolicy.MaxActionSeconds);
    }

    public bool AdvanceFixed(double fixedDeltaSeconds)
    {
        if (IsPaused)
        {
            return false;
        }

        CurrentTimeSeconds += NormalizeFixedDelta(fixedDeltaSeconds);
        return true;
    }

    public bool AdvanceTo(double targetTimeSeconds)
    {
        if (IsPaused || double.IsNaN(targetTimeSeconds) || double.IsInfinity(targetTimeSeconds))
        {
            return false;
        }

        if (targetTimeSeconds <= CurrentTimeSeconds)
        {
            return false;
        }

        CurrentTimeSeconds = targetTimeSeconds;
        return true;
    }
}
