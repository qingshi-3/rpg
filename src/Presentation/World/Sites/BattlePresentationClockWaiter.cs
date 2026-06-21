using System;
using System.Threading.Tasks;
using Godot;
using Rpg.Infrastructure.Logging;

namespace Rpg.Presentation.World.Sites;

internal static class BattlePresentationClockWaiter
{
    private const double MaxStepSeconds = 0.05;

    internal static async Task WaitSecondsAsync(
        Node owner,
        double seconds,
        Func<bool> isPaused,
        string pauseLogName)
    {
        if (owner == null || !owner.IsInsideTree() || seconds <= 0)
        {
            return;
        }

        double remainingSeconds = seconds;
        bool loggedPauseWait = false;
        while (owner.IsInsideTree() && remainingSeconds > 0)
        {
            bool paused = isPaused?.Invoke() == true;
            if (paused && !loggedPauseWait)
            {
                GameLog.Info(nameof(BattlePresentationClockWaiter), pauseLogName ?? "BattleRuntimePresentationWaitPaused");
                loggedPauseWait = true;
            }

            double stepSeconds = Math.Min(MaxStepSeconds, remainingSeconds);
            await owner.ToSignal(
                owner.GetTree().CreateTimer(stepSeconds, processAlways: true),
                SceneTreeTimer.SignalName.Timeout);

            bool pausedAfterStep = isPaused?.Invoke() == true;
            if (!paused && !pausedAfterStep)
            {
                remainingSeconds -= stepSeconds;
            }
        }
    }
}
