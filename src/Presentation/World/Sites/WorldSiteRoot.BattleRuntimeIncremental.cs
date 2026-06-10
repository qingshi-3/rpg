using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rpg.Application.World;
using Rpg.Infrastructure.Logging;
using Rpg.Presentation.Battle.Entities;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;

namespace Rpg.Presentation.World.Sites;

public partial class WorldSiteRoot
{
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
            await WaitForBattleRuntimeAdvanceGateAsync();
            BattleRuntimeAdvanceResult advance = controller.AdvanceFixedTick(tickSeconds);
            _ = ObserveRuntimeEventsOnPresentationAsync(advance.Events, presentationState);
            await WaitSiteBattlePresentationSeconds(tickSeconds);
        }

        int pendingBeforeDrain = presentationState.PendingPresentationTaskCount;
        if (pendingBeforeDrain > 0)
        {
            GameLog.Info(
                nameof(WorldSiteRoot),
                $"BattleRuntimePresentationDrainStarted request={resolution?.Request?.RequestId ?? ""} pendingTasks={pendingBeforeDrain}");
        }

        await presentationState.WaitForAllAsync();

        if (pendingBeforeDrain > 0)
        {
            GameLog.Info(
                nameof(WorldSiteRoot),
                $"BattleRuntimePresentationDrainCompleted request={resolution?.Request?.RequestId ?? ""}");
        }

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

        foreach (BattleEvent runtimeEvent in events.Where(item => item?.Kind == BattleEventKind.SkillUsed))
        {
            presentationState.TrackActorAction(
                runtimeEvent.ActorId,
                () => ObserveRuntimeSkillUsedEventAsync(
                    runtimeEvent,
                    presentationState.EntitiesByRuntimeActor),
                gateMovementStart: true);
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
            BattleRuntimeLivePresentationState.BattlePresentationFatalDamageDiagnostic diagnostic =
                BattleRuntimeLivePresentationState.BattlePresentationFatalDamageDiagnostic.TryCreate(runtimeEvent);
            presentationState.TrackActorDamage(
                runtimeEvent.ActorId,
                runtimeEvent.TargetId,
                () => PlayRuntimeDamageFeedbackEventAsync(
                    runtimeEvent,
                    presentationState.EntitiesByRuntimeActor,
                    diagnostic));
            presentationState.TrackTargetDamage(
                runtimeEvent.ActorId,
                runtimeEvent.TargetId,
                previousTargetDamageTail => ApplyRuntimeDamageEventAsync(
                    runtimeEvent,
                    presentationState.EntitiesByRuntimeActor,
                    diagnostic,
                    previousTargetDamageTail),
                diagnostic);
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
