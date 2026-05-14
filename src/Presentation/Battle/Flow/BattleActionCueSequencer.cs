using System;
using System.Threading.Tasks;
using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.Battle.Flow;

public readonly record struct BattleActionCueRequest(
    string EntityId,
    BattleFaction Faction,
    double DurationSeconds);

public sealed class BattleActionCueSequencer
{
    public const double DefaultDurationSeconds = 0.5;

    public async Task RunAsync(
        string entityId,
        BattleFaction faction,
        Func<BattleActionCueRequest, Task> showCue,
        Func<double, Task> wait,
        Func<Task> action,
        Func<string, Task> hideCue,
        double durationSeconds = DefaultDurationSeconds)
    {
        double duration = Math.Max(0.0, durationSeconds);
        BattleActionCueRequest cue = new(entityId ?? "", faction, duration);

        if (showCue != null)
        {
            await showCue(cue);
        }

        if (duration > 0.0 && wait != null)
        {
            await wait(duration);
        }

        try
        {
            if (action != null)
            {
                await action();
            }
        }
        finally
        {
            if (hideCue != null)
            {
                await hideCue(cue.EntityId);
            }
        }
    }
}
