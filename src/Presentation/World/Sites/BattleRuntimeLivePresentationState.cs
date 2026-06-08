using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rpg.Presentation.Battle.Entities;

namespace Rpg.Presentation.World.Sites;

internal sealed class BattleRuntimeLivePresentationState
{
    private readonly List<Task> _pendingPresentationTasks = new();
    private readonly Dictionary<string, Task> _actorActionTails = new(System.StringComparer.Ordinal);
    private readonly Dictionary<string, Task> _actorMovementTails = new(System.StringComparer.Ordinal);
    private readonly Dictionary<string, Task> _actorMovementStartGates = new(System.StringComparer.Ordinal);

    public BattleRuntimeLivePresentationState(Dictionary<string, BattleEntity> entitiesByRuntimeActor)
    {
        EntitiesByRuntimeActor = entitiesByRuntimeActor ?? new Dictionary<string, BattleEntity>(System.StringComparer.Ordinal);
    }

    public Dictionary<string, BattleEntity> EntitiesByRuntimeActor { get; }

    public void Track(Task task)
    {
        if (task == null)
        {
            return;
        }

        PruneCompleted();
        _pendingPresentationTasks.Add(task);
    }

    public void TrackActorAction(string actorId, System.Func<Task> createTask, bool gateMovementStart = false)
    {
        if (createTask == null)
        {
            return;
        }

        actorId ??= "";
        _actorActionTails.TryGetValue(actorId, out Task previousTask);
        Task task = RunAfterActorTailAsync(previousTask, createTask);
        if (!string.IsNullOrWhiteSpace(actorId))
        {
            _actorActionTails[actorId] = task;
            if (gateMovementStart)
            {
                _actorMovementStartGates[actorId] = task;
            }
        }

        Track(task);
    }

    public void TrackActorDamage(string actorId, string targetId, System.Func<Task> createTask)
    {
        if (createTask == null)
        {
            return;
        }

        actorId ??= "";
        targetId ??= "";
        _actorActionTails.TryGetValue(actorId, out Task actorActionTail);
        _actorMovementTails.TryGetValue(targetId, out Task targetMovementTail);
        Task task = RunAfterActorDependenciesAsync(actorActionTail, targetMovementTail, createTask);
        if (!string.IsNullOrWhiteSpace(actorId))
        {
            _actorActionTails[actorId] = task;
        }

        Track(task);
    }

    public void TrackActorMovement(string actorId, System.Func<double> observeMovement, System.Func<double, Task> wait)
    {
        if (observeMovement == null || wait == null)
        {
            return;
        }

        actorId ??= "";
        _actorMovementStartGates.TryGetValue(actorId, out Task movementStartGate);
        if (movementStartGate != null && !movementStartGate.IsCompleted)
        {
            _actorActionTails.TryGetValue(actorId, out Task previousActionTask);
            // Skill casts are anchored release presentations. Movement may
            // still be simulated later by Runtime, but the visual lane must
            // not start until the caster-side release has finished.
            Task gatedMovementTask = RunMovementAfterGateAsync(previousActionTask ?? movementStartGate, observeMovement, wait);
            if (!string.IsNullOrWhiteSpace(actorId))
            {
                _actorActionTails[actorId] = gatedMovementTask;
                _actorMovementTails[actorId] = gatedMovementTask;
            }

            Track(gatedMovementTask);
            return;
        }

        double movementSeconds = System.Math.Max(0, observeMovement());
        if (movementSeconds <= 0)
        {
            return;
        }

        _actorActionTails.TryGetValue(actorId, out Task previousTask);
        // Movement completion is a separate dependency from this actor's
        // action backlog. Incoming hits wait for movement, not for unrelated
        // attack feedback already queued on the target.
        Task movementTask = wait(movementSeconds);
        Task tailTask = WaitForActorDependenciesAsync(previousTask, movementTask);
        if (!string.IsNullOrWhiteSpace(actorId))
        {
            _actorActionTails[actorId] = tailTask;
            _actorMovementTails[actorId] = movementTask;
        }

        Track(tailTask);
    }

    public async Task WaitForAllAsync()
    {
        PruneCompleted();
        Task[] tasks = _pendingPresentationTasks.Where(task => task != null).ToArray();
        if (tasks.Length > 0)
        {
            await Task.WhenAll(tasks);
        }

        _pendingPresentationTasks.Clear();
    }

    private void PruneCompleted()
    {
        _pendingPresentationTasks.RemoveAll(task => task == null || task.IsCompleted);
    }

    private static async Task RunAfterActorTailAsync(Task previousTask, System.Func<Task> createTask)
    {
        if (previousTask != null)
        {
            await previousTask;
        }

        Task task = createTask();
        if (task != null)
        {
            await task;
        }
    }

    private static Task RunAfterActorDependenciesAsync(
        Task actorActionTail,
        Task targetMovementTail,
        System.Func<Task> createTask)
    {
        return RunAfterDependenciesAsync(new[] { actorActionTail, targetMovementTail }, createTask);
    }

    private static async Task RunAfterDependenciesAsync(
        IReadOnlyList<Task> dependencies,
        System.Func<Task> createTask)
    {
        await WaitForDependenciesAsync(dependencies);
        Task task = createTask();
        if (task != null)
        {
            await task;
        }
    }

    private static Task WaitForActorDependenciesAsync(Task actorActionTail, Task movementTask)
    {
        return WaitForDependenciesAsync(new[] { actorActionTail, movementTask });
    }

    private static Task WaitForDependenciesAsync(IReadOnlyList<Task> dependencies)
    {
        Task[] pending = dependencies?
            .Where(task => task != null)
            .ToArray() ?? System.Array.Empty<Task>();
        return pending.Length == 0 ? Task.CompletedTask : Task.WhenAll(pending);
    }

    private static async Task RunMovementAfterGateAsync(
        Task movementStartGate,
        System.Func<double> observeMovement,
        System.Func<double, Task> wait)
    {
        if (movementStartGate != null)
        {
            await movementStartGate;
        }

        double movementSeconds = System.Math.Max(0, observeMovement());
        if (movementSeconds <= 0)
        {
            return;
        }

        Task movementWait = wait(movementSeconds);
        if (movementWait != null)
        {
            await movementWait;
        }
    }
}
