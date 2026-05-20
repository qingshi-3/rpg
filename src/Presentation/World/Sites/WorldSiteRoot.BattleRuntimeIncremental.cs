using System.Collections.Generic;
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
            BattleRuntimeAdvanceResult advance = controller.AdvanceNextTick();
            _ = ObserveRuntimeEventsOnPresentationAsync(advance.Events, presentationState);
            double waitSeconds = advance.NextAdvanceDelaySeconds > 0
                ? advance.NextAdvanceDelaySeconds
                : ResolveRuntimePlaybackTickSeconds();
            await WaitSiteBattlePresentationSeconds(waitSeconds);
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
        if (events == null || events.Count == 0 || presentationState == null)
        {
            return Task.CompletedTask;
        }

        Dictionary<string, double> sameTickMovingActors = events
            .Where(runtimeEvent =>
                runtimeEvent?.Kind == BattleEventKind.MovementCompleted &&
                !string.IsNullOrWhiteSpace(runtimeEvent.ActorId))
            .GroupBy(runtimeEvent => runtimeEvent.ActorId, System.StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Max(runtimeEvent => System.Math.Max(0, runtimeEvent.ActionDurationSeconds)),
                System.StringComparer.Ordinal);
        foreach (BattleEvent runtimeEvent in events)
        {
            if (runtimeEvent == null)
            {
                continue;
            }

            if (runtimeEvent.Kind == BattleEventKind.MovementCompleted)
            {
                presentationState.TrackActorAction(
                    runtimeEvent.ActorId,
                    () => ObserveRuntimeMovementEventAsync(
                        runtimeEvent,
                        presentationState.EntitiesByRuntimeActor));
                continue;
            }

            if (runtimeEvent.Kind == BattleEventKind.DamageApplied)
            {
                double sameTickMovementDelaySeconds = ResolveSameTickMovementDelaySeconds(
                    sameTickMovingActors,
                    runtimeEvent.ActorId,
                    runtimeEvent.TargetId);
                presentationState.TrackActorAction(
                    runtimeEvent.ActorId,
                    () => ObserveRuntimeDamageEventAfterSameTickMovementAsync(
                        runtimeEvent,
                        presentationState.EntitiesByRuntimeActor,
                        sameTickMovementDelaySeconds));
            }
        }

        return Task.CompletedTask;
    }

    private async Task ObserveRuntimeMovementEventAsync(
        BattleEvent runtimeEvent,
        IReadOnlyDictionary<string, BattleEntity> entitiesByRuntimeActor)
    {
        double movementSeconds = ObserveRuntimeMovementEvent(runtimeEvent, entitiesByRuntimeActor);
        if (movementSeconds > 0)
        {
            await WaitSiteBattlePresentationSeconds(movementSeconds);
        }
    }

    private async Task ObserveRuntimeDamageEventAfterSameTickMovementAsync(
        BattleEvent runtimeEvent,
        IReadOnlyDictionary<string, BattleEntity> entitiesByRuntimeActor,
        double sameTickMovementDelaySeconds)
    {
        if (sameTickMovementDelaySeconds > 0)
        {
            await WaitSiteBattlePresentationSeconds(sameTickMovementDelaySeconds);
        }

        await ObserveRuntimeDamageEventAsync(runtimeEvent, entitiesByRuntimeActor);
    }

    private static double ResolveSameTickMovementDelaySeconds(
        IReadOnlyDictionary<string, double> sameTickMovingActors,
        string actorId,
        string targetId)
    {
        double delaySeconds = 0;
        if (sameTickMovingActors == null)
        {
            return delaySeconds;
        }

        if (!string.IsNullOrWhiteSpace(actorId) &&
            sameTickMovingActors.TryGetValue(actorId, out double actorDelay))
        {
            delaySeconds = System.Math.Max(delaySeconds, actorDelay);
        }

        if (!string.IsNullOrWhiteSpace(targetId) &&
            sameTickMovingActors.TryGetValue(targetId, out double targetDelay))
        {
            delaySeconds = System.Math.Max(delaySeconds, targetDelay);
        }

        return delaySeconds;
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
