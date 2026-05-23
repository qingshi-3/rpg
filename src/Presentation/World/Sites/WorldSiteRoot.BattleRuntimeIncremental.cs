using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rpg.Application.World;
using Rpg.Presentation.Battle.Entities;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;

namespace Rpg.Presentation.World.Sites;

public partial class WorldSiteRoot
{
    private sealed class BattleRuntimeLivePresentationState
    {
        private readonly List<Task> _pendingPresentationTasks = new();
        private readonly Dictionary<string, Task> _actorActionTails = new(System.StringComparer.Ordinal);
        private readonly Dictionary<string, Task> _actorMovementTails = new(System.StringComparer.Ordinal);

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

        public void TrackActorAction(string actorId, System.Func<Task> createTask)
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

            double movementSeconds = System.Math.Max(0, observeMovement());
            if (movementSeconds <= 0)
            {
                return;
            }

            actorId ??= "";
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
    }

    private async Task AdvanceBattleGroupRuntimeOnLiveClockAsync(WorldSiteBattleGroupRuntimeResolveResult resolution)
    {
        BattleRuntimeSessionController controller = resolution?.RuntimeController;
        if (controller == null)
        {
            return;
        }

        BattleRuntimeLivePresentationState presentationState = new(BuildRuntimePlaybackEntityMap());
        while (!controller.IsComplete && IsInsideTree())
        {
            double tickSeconds = ResolveRuntimePlaybackTickSeconds();
            BattleRuntimeAdvanceResult advance = controller.AdvanceFixedTick(tickSeconds);
            _ = ObserveRuntimeEventsOnPresentationAsync(advance.Events, presentationState);
            await WaitSiteBattlePresentationSeconds(tickSeconds);
        }

        await presentationState.WaitForAllAsync();
        _unitRoot?.PlayIdleForActiveEntities();
        if (_unitRoot?.HasPendingDefeatedPresentations == true)
        {
            await _unitRoot.WaitForDefeatedPresentationsAsync();
        }
    }

    private Task ObserveRuntimeEventsOnPresentationAsync(
        IReadOnlyList<BattleEvent> events,
        BattleRuntimeLivePresentationState presentationState)
    {
        long observeStartedAt = Stopwatch.GetTimestamp();
        if (events == null || events.Count == 0 || presentationState == null)
        {
            _battlePerformanceCounters.RecordPresentationObserveElapsedTicks(Stopwatch.GetTimestamp() - observeStartedAt);
            return Task.CompletedTask;
        }

        foreach (BattleEvent runtimeEvent in events.Where(item => item?.Kind == BattleEventKind.MovementStarted))
        {
            presentationState.TrackActorMovement(
                runtimeEvent.ActorId,
                () => ObserveRuntimeMovementEvent(
                    runtimeEvent,
                    presentationState.EntitiesByRuntimeActor),
                WaitSiteBattlePresentationSeconds);
        }

        foreach (BattleEvent runtimeEvent in events.Where(item => item?.Kind == BattleEventKind.DamageApplied))
        {
            presentationState.TrackActorDamage(
                runtimeEvent.ActorId,
                runtimeEvent.TargetId,
                () => ObserveRuntimeDamageEventAsync(
                    runtimeEvent,
                    presentationState.EntitiesByRuntimeActor));
        }

        _battlePerformanceCounters.RecordPresentationObserveElapsedTicks(Stopwatch.GetTimestamp() - observeStartedAt);
        return Task.CompletedTask;
    }

    private Dictionary<string, BattleEntity> BuildRuntimePlaybackEntityMap()
    {
        if (_unitRoot == null)
        {
            return new Dictionary<string, BattleEntity>(System.StringComparer.Ordinal);
        }

        return _unitRoot.GetEntitiesSnapshot()
            .Where(entity => entity != null && GodotObject.IsInstanceValid(entity))
            .GroupBy(entity => entity.EntityId)
            .ToDictionary(group => group.Key, group => group.First(), System.StringComparer.Ordinal);
    }
}
