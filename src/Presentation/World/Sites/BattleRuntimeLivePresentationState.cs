using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle.Entities;
using Rpg.Runtime.Battle.Events;

namespace Rpg.Presentation.World.Sites;

internal sealed class BattleRuntimeLivePresentationState
{
    private readonly List<Task> _pendingPresentationTasks = new();
    private readonly Dictionary<string, Task> _actorActionTails = new(System.StringComparer.Ordinal);
    private readonly Dictionary<string, Task> _actorMovementTails = new(System.StringComparer.Ordinal);
    private readonly Dictionary<string, Task> _actorMovementStartGates = new(System.StringComparer.Ordinal);
    private readonly Dictionary<string, Task> _targetDamageTails = new(System.StringComparer.Ordinal);

    public BattleRuntimeLivePresentationState(Dictionary<string, BattleEntity> entitiesByRuntimeActor)
    {
        EntitiesByRuntimeActor = entitiesByRuntimeActor ?? new Dictionary<string, BattleEntity>(System.StringComparer.Ordinal);
    }

    public Dictionary<string, BattleEntity> EntitiesByRuntimeActor { get; }

    public int PendingPresentationTaskCount
    {
        get
        {
            PruneCompleted();
            return _pendingPresentationTasks.Count;
        }
    }

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

    public void TrackActorDamage(
        string actorId,
        string targetId,
        System.Func<Task> createTask)
    {
        if (createTask == null)
        {
            return;
        }

        PruneCompleted();
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

    public void TrackTargetDamage(
        string actorId,
        string targetId,
        System.Func<Task, Task> createTask,
        BattlePresentationFatalDamageDiagnostic diagnostic = null)
    {
        if (createTask == null)
        {
            return;
        }

        PruneCompleted();
        actorId ??= "";
        targetId ??= "";
        _actorMovementTails.TryGetValue(actorId, out Task actorMovementTail);
        _actorMovementTails.TryGetValue(targetId, out Task targetMovementTail);
        _targetDamageTails.TryGetValue(targetId, out Task previousTargetDamageTail);
        diagnostic?.LogQueued(actorMovementTail, targetMovementTail, previousTargetDamageTail, _pendingPresentationTasks.Count);
        Task task = RunAfterTargetDamageDependenciesAsync(actorMovementTail, targetMovementTail, previousTargetDamageTail, createTask, diagnostic);
        if (!string.IsNullOrWhiteSpace(targetId))
        {
            _targetDamageTails[targetId] = task;
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
            _actorActionTails.TryGetValue(actorId, out Task gatedPreviousActionTask);
            // Skill casts are anchored release presentations. Movement may
            // still be simulated later by Runtime, but the visual lane must
            // not start until the caster-side release has finished.
            Task gatedMovementTask = RunMovementAfterGateAsync(gatedPreviousActionTask ?? movementStartGate, observeMovement, wait);
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

        _actorActionTails.TryGetValue(actorId, out Task previousActionTask);
        _actorMovementTails.TryGetValue(actorId, out Task previousMovementTask);
        // Movement completion is a separate dependency from this actor's
        // action backlog. Incoming hits wait for movement, not for unrelated
        // attack feedback already queued on the target.
        Task movementTask = RunMovementTailAsync(previousMovementTask, movementSeconds, wait);
        Task tailTask = WaitForActorDependenciesAsync(previousActionTask, movementTask);
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

    private static Task RunAfterTargetDamageDependenciesAsync(
        Task actorMovementTail,
        Task targetMovementTail,
        Task previousTargetDamageTail,
        System.Func<Task, Task> createTask,
        BattlePresentationFatalDamageDiagnostic diagnostic = null)
    {
        return RunAfterTargetDamageDependenciesAsync(new[] { actorMovementTail, targetMovementTail }, previousTargetDamageTail, createTask, diagnostic);
    }

    private static async Task RunAfterDependenciesAsync(
        IReadOnlyList<Task> dependencies,
        System.Func<Task> createTask,
        BattlePresentationFatalDamageDiagnostic diagnostic = null)
    {
        await WaitForDependenciesAsync(dependencies);
        diagnostic?.LogDependenciesReady();
        Task task = createTask();
        if (task != null)
        {
            await task;
        }
    }

    private static async Task RunAfterTargetDamageDependenciesAsync(
        IReadOnlyList<Task> dependencies,
        Task previousTargetDamageTail,
        System.Func<Task, Task> createTask,
        BattlePresentationFatalDamageDiagnostic diagnostic = null)
    {
        await WaitForDependenciesAsync(dependencies);
        diagnostic?.LogDependenciesReady();
        Task task = createTask(previousTargetDamageTail);
        if (task != null)
        {
            await task;
        }
    }

    private static Task WaitForActorDependenciesAsync(Task actorActionTail, Task movementTask)
    {
        return WaitForDependenciesAsync(new[] { actorActionTail, movementTask });
    }

    private static async Task RunMovementTailAsync(Task previousTask, double movementSeconds, System.Func<double, Task> wait)
    {
        if (previousTask != null)
        {
            await previousTask;
        }

        Task movementWait = wait(movementSeconds);
        if (movementWait != null)
        {
            await movementWait;
        }
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

    internal sealed class BattlePresentationFatalDamageDiagnostic
    {
        private readonly long _queuedAtTicks = Stopwatch.GetTimestamp();

        private BattlePresentationFatalDamageDiagnostic(BattleEvent runtimeEvent)
        {
            BattleId = runtimeEvent.BattleId ?? "";
            RuntimeTick = runtimeEvent.RuntimeTick;
            RuntimeTimeSeconds = runtimeEvent.RuntimeTimeSeconds;
            ActorId = runtimeEvent.ActorId ?? "";
            TargetId = runtimeEvent.TargetId ?? "";
            ReasonCode = runtimeEvent.ReasonCode ?? "";
            Damage = System.Math.Max(0, -runtimeEvent.CorpsStrengthDelta);
            ActionDurationSeconds = runtimeEvent.ActionDurationSeconds;
            ActionImpactDelaySeconds = runtimeEvent.ActionImpactDelaySeconds;
        }

        private string BattleId { get; }

        private int RuntimeTick { get; }

        private double RuntimeTimeSeconds { get; }

        private string ActorId { get; }

        private string TargetId { get; }

        private string ReasonCode { get; }

        private int Damage { get; }

        private double ActionDurationSeconds { get; }

        private double ActionImpactDelaySeconds { get; }

        public static BattlePresentationFatalDamageDiagnostic TryCreate(BattleEvent runtimeEvent)
        {
            if (runtimeEvent?.Kind != BattleEventKind.DamageApplied ||
                !IsFatalDamageReason(runtimeEvent.ReasonCode))
            {
                return null;
            }

            return new BattlePresentationFatalDamageDiagnostic(runtimeEvent);
        }

        public void LogQueued(Task actorMovementTail, Task targetMovementTail, Task previousTargetDamageTail, int pendingPresentationTasks)
        {
            Log(
                "Queued",
                $"pendingTasks={pendingPresentationTasks} actorMovementTailPending={IsPending(actorMovementTail)} targetMovementTailPending={IsPending(targetMovementTail)} previousTargetDamageTailPending={IsPending(previousTargetDamageTail)} actionDuration={Format(ActionDurationSeconds)} impactDelay={Format(ActionImpactDelaySeconds)}");
        }

        public void LogDependenciesReady()
        {
            Log("DependenciesReady", $"movementWait={Format(ElapsedSinceQueuedSeconds())}");
        }

        public void LogSkipped(string reason)
        {
            Log("Skipped", $"reason={reason ?? ""}");
        }

        public void LogFeedbackStarted(
            int targetHpBeforeHit,
            int previewApplied,
            bool previewDefeated,
            double attackAnimationSeconds,
            bool isSkillDamage)
        {
            Log(
                "FeedbackStarted",
                $"targetHpBefore={targetHpBeforeHit} previewApplied={previewApplied} previewDefeated={previewDefeated} attackAnimation={Format(attackAnimationSeconds)} isSkillDamage={isSkillDamage}");
        }

        public void LogImpactDelayResolved(
            double rawImpactDelaySeconds,
            double clampedImpactDelaySeconds,
            double attackAnimationSeconds)
        {
            Log(
                "ImpactDelayResolved",
                $"rawImpactDelay={Format(rawImpactDelaySeconds)} clampedImpactDelay={Format(clampedImpactDelaySeconds)} attackAnimation={Format(attackAnimationSeconds)}");
        }

        public void LogDamageApplied(int applied, int hpBefore, int hpAfter, bool presentationDefeated)
        {
            Log(
                "DamageApplied",
                $"applied={applied} hp={hpBefore}->{hpAfter} presentationDefeated={presentationDefeated}");
        }

        public void LogMarkDefeatedRequested()
        {
            Log("MarkDefeatedRequested", "");
        }

        private static bool IsPending(Task task)
        {
            return task != null && !task.IsCompleted;
        }

        private static bool IsFatalDamageReason(string reasonCode)
        {
            return !string.IsNullOrWhiteSpace(reasonCode) &&
                   reasonCode.Contains("defeated", System.StringComparison.OrdinalIgnoreCase);
        }

        private void Log(string phase, string detail)
        {
            string context =
                $"BattlePresentationFatalDamage{phase} battle={BattleId} tick={RuntimeTick} runtimeTime={Format(RuntimeTimeSeconds)} actor={ActorId} target={TargetId} damage={Damage} reason={ReasonCode} elapsed={Format(ElapsedSinceQueuedSeconds())}";
            GameLog.Info(nameof(WorldSiteRoot), string.IsNullOrWhiteSpace(detail) ? context : $"{context} {detail}");
        }

        private double ElapsedSinceQueuedSeconds()
        {
            return (Stopwatch.GetTimestamp() - _queuedAtTicks) / (double)Stopwatch.Frequency;
        }

        private static string Format(double value)
        {
            return value.ToString("0.000", CultureInfo.InvariantCulture);
        }
    }
}
