using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Effects;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Navigation;

namespace Rpg.Runtime.Battle;

internal sealed class BattleCommitBuffer
{
    private readonly List<BasicAttackRequest> _basicAttackRequests = new();
    private readonly List<EffectDeliveryRequest> _effectDeliveryRequests = new();
    private readonly List<EffectDamageRequest> _effectDamageRequests = new();
    private readonly Dictionary<string, int> _effectEventIdOccurrences = new(System.StringComparer.Ordinal);

    internal void RequestBasicAttack(
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        BattleGridCoord actorAnchor,
        BattleGridCoord targetAnchor,
        int declaredDamage)
    {
        if (actor == null ||
            target == null ||
            string.IsNullOrWhiteSpace(actor.ActorId) ||
            string.IsNullOrWhiteSpace(target.ActorId))
        {
            return;
        }

        _basicAttackRequests.Add(new BasicAttackRequest(
            actor,
            target,
            actorAnchor,
            targetAnchor,
            NormalizeBasicAttackDamage(declaredDamage)));
    }

    internal void RequestEffectDelivery(
        BattleEffectExecutionContext context,
        BattleRuntimeActor target,
        BattleSkillEffectKind effectKind,
        int amount)
    {
        if (context == null ||
            target == null ||
            string.IsNullOrWhiteSpace(context.Actor?.ActorId) ||
            string.IsNullOrWhiteSpace(target.ActorId))
        {
            return;
        }

        _effectDeliveryRequests.Add(new EffectDeliveryRequest(
            context.BattleId ?? "",
            context.RuntimeTick,
            context.RuntimeTimeSeconds,
            context.SourceCommandId ?? "",
            context.SourceActionId ?? "",
            context.SourceDefinitionId ?? "",
            context.Actor,
            new BattleGridCoord(context.Actor.GridX, context.Actor.GridY, context.Actor.GridHeight),
            target,
            new BattleGridCoord(target.GridX, target.GridY, target.GridHeight),
            effectKind,
            amount));
    }

    internal void RequestEffectDamage(
        BattleEffectExecutionContext context,
        BattleHealthComponent targetHealth,
        int amount,
        string effectKind)
    {
        if (context == null ||
            targetHealth?.Actor == null ||
            string.IsNullOrWhiteSpace(context.Actor?.ActorId) ||
            string.IsNullOrWhiteSpace(targetHealth.Actor.ActorId))
        {
            return;
        }

        _effectDamageRequests.Add(new EffectDamageRequest(
            context.BattleId ?? "",
            context.RuntimeTick,
            context.RuntimeTimeSeconds,
            context.SourceCommandId ?? "",
            context.SourceActionId ?? "",
            context.SourceDefinitionId ?? "",
            effectKind ?? "",
            context.Actor,
            context.ActorAnchorOverride ?? new BattleGridCoord(context.Actor.GridX, context.Actor.GridY, context.Actor.GridHeight),
            targetHealth,
            context.TargetAnchorOverride ?? new BattleGridCoord(targetHealth.Actor.GridX, targetHealth.Actor.GridY, targetHealth.Actor.GridHeight),
            NormalizeEffectDamage(amount)));
    }

    internal IReadOnlyList<BattleEvent> CommitEffectDeliveries()
    {
        if (_effectDeliveryRequests.Count == 0)
        {
            return System.Array.Empty<BattleEvent>();
        }

        foreach (EffectDeliveryRequest request in _effectDeliveryRequests)
        {
            BattleActorRuntime targetRuntime = new(request.Target);
            targetRuntime.EffectReceiver.ReceiveEffect(
                this,
                new BattleEffectExecutionContext
                {
                    BattleId = request.BattleId,
                    RuntimeTick = request.RuntimeTick,
                    RuntimeTimeSeconds = request.RuntimeTimeSeconds,
                    SourceCommandId = request.SourceCommandId,
                    SourceActionId = request.SourceActionId,
                    SourceDefinitionId = request.SourceDefinitionId,
                    CommitBuffer = this,
                    ActorAnchorOverride = request.ActorAnchor,
                    TargetAnchorOverride = request.TargetAnchor,
                    Actor = request.Actor,
                    Target = request.Target
                },
                new BattleEffectPayload
                {
                    EffectKind = request.EffectKind,
                    Amount = request.Amount
                });
        }

        _effectDeliveryRequests.Clear();
        return CommitEffectDamage();
    }

    internal IReadOnlyList<BattleEvent> CommitEffectDamage()
    {
        if (_effectDamageRequests.Count == 0)
        {
            return System.Array.Empty<BattleEvent>();
        }

        List<BattleEvent> events = new();
        foreach (EffectDamageRequest request in _effectDamageRequests)
        {
            BattleHealthComponent.EffectDamageCommitResult result = request.TargetHealth.CommitEffectDamage(request.DamageAmount);
            int delta = -result.DamageAmount;

            // Core Slice B keeps effect damage as an explicit commit boundary:
            // receivers request health changes, while this buffer owns commit timing and event order.
            events.Add(CreateEffectDamageEvent(
                request,
                BattleEventKind.EffectApplied,
                ResolveUniqueEffectEventId($"{request.BattleId}:tick_{request.RuntimeTick}:{request.Actor.ActorId}:effect:{request.TargetHealth.Actor.ActorId}"),
                "effect_applied",
                delta,
                result.HitPointsBefore,
                result.RemainingHitPoints));
            events.Add(CreateEffectDamageEvent(
                request,
                BattleEventKind.DamageApplied,
                ResolveUniqueEffectEventId($"{request.BattleId}:tick_{request.RuntimeTick}:{request.Actor.ActorId}:effect_damage:{request.TargetHealth.Actor.ActorId}"),
                result.TransitionedToDefeated ? "effect_damage_target_defeated" : "effect_damage",
                delta,
                result.HitPointsBefore,
                result.RemainingHitPoints));
        }

        _effectDamageRequests.Clear();
        return events;
    }

    internal void CommitBasicAttacks(
        IReadOnlyDictionary<string, BattleRuntimeTickStartActorFact> tickStartFacts,
        BattleEventStream stream,
        string battleId,
        int tick,
        double currentTimeSeconds)
    {
        if (_basicAttackRequests.Count == 0)
        {
            return;
        }

        System.ArgumentNullException.ThrowIfNull(tickStartFacts);
        System.ArgumentNullException.ThrowIfNull(stream);

        Dictionary<string, int> basicAttackTargetHitPoints = tickStartFacts.Values.ToDictionary(
            item => item.Actor.ActorId,
            item => System.Math.Max(0, item.HitPoints),
            System.StringComparer.Ordinal);
        HashSet<string> basicAttackTargetIds = new(System.StringComparer.Ordinal);

        List<BasicAttackApplication> applications = new();
        foreach (IGrouping<string, BasicAttackRequest> targetGroup in _basicAttackRequests
                     .GroupBy(item => item.Target.ActorId, System.StringComparer.Ordinal))
        {
            if (!basicAttackTargetHitPoints.TryGetValue(targetGroup.Key, out int remaining))
            {
                continue;
            }

            basicAttackTargetIds.Add(targetGroup.Key);
            foreach (BasicAttackRequest request in targetGroup.OrderBy(
                         item => item.Actor.ActorId,
                         System.StringComparer.Ordinal))
            {
                int hpBefore = remaining;
                int applied = System.Math.Min(request.DeclaredDamage, remaining);
                remaining = System.Math.Max(0, remaining - applied);
                applications.Add(new BasicAttackApplication(
                    request,
                    applied,
                    HpBefore: hpBefore,
                    HpAfter: remaining,
                    IsFinishingHit: applied > 0 && remaining == 0));
            }

            basicAttackTargetHitPoints[targetGroup.Key] = remaining;
        }

        // Basic-attack impact facts commit here; attacker phase changes stay
        // with the actor-local action controller that owns the action lifecycle.
        foreach (BasicAttackApplication application in applications.OrderBy(
                     item => item.Request.Actor.ActorId,
                     System.StringComparer.Ordinal))
        {
            stream.Add(BattleRuntimeEventFactory.CreateDamageApplied(
                battleId,
                tick,
                currentTimeSeconds,
                application.Request.Actor,
                application.Request.Target,
                application.Request.ActorAnchor,
                application.Request.TargetAnchor,
                application.AppliedDamage,
                application.HpBefore,
                application.HpAfter,
                application.IsFinishingHit));
        }

        // Basic attacks still batch from tick-start HP for same-tick mutual hits.
        // Only actors that were actually targeted in this batch may receive the
        // absolute remaining-HP commit; other effect sources must commit before
        // this phase or through their own buffer phase rather than interleaving here.
        foreach (BattleRuntimeTickStartActorFact targetFact in tickStartFacts.Values
                     .Where(item => basicAttackTargetIds.Contains(item.Actor.ActorId ?? ""))
                     .OrderBy(item => item.Actor.ActorId, System.StringComparer.Ordinal))
        {
            string targetActorId = targetFact.Actor.ActorId ?? "";
            if (!basicAttackTargetHitPoints.TryGetValue(targetActorId, out int remainingHitPoints))
            {
                continue;
            }

            BattleHealthComponent.BasicAttackDamageCommitResult result =
                new BattleHealthComponent(targetFact.Actor).CommitBasicAttackDamage(remainingHitPoints);
            if (result.TransitionedToDefeated)
            {
                BattlePlanStateEmitter.SetPlanState(
                    stream,
                    battleId,
                    tick,
                    currentTimeSeconds,
                    targetFact.Actor,
                    BattleGroupPlanRuntimeState.Defeated,
                    "defeated");
            }
        }

        _basicAttackRequests.Clear();
    }

    private readonly record struct BasicAttackRequest(
        BattleRuntimeActor Actor,
        BattleRuntimeActor Target,
        BattleGridCoord ActorAnchor,
        BattleGridCoord TargetAnchor,
        int DeclaredDamage);

    private readonly record struct BasicAttackApplication(
        BasicAttackRequest Request,
        int AppliedDamage,
        int HpBefore,
        int HpAfter,
        bool IsFinishingHit);

    private readonly record struct EffectDeliveryRequest(
        string BattleId,
        int RuntimeTick,
        double RuntimeTimeSeconds,
        string SourceCommandId,
        string SourceActionId,
        string SourceDefinitionId,
        BattleRuntimeActor Actor,
        BattleGridCoord ActorAnchor,
        BattleRuntimeActor Target,
        BattleGridCoord TargetAnchor,
        BattleSkillEffectKind EffectKind,
        int Amount);

    private readonly record struct EffectDamageRequest(
        string BattleId,
        int RuntimeTick,
        double RuntimeTimeSeconds,
        string SourceCommandId,
        string SourceActionId,
        string SourceDefinitionId,
        string EffectKind,
        BattleRuntimeActor Actor,
        BattleGridCoord ActorAnchor,
        BattleHealthComponent TargetHealth,
        BattleGridCoord TargetAnchor,
        int DamageAmount);

    private static int NormalizeBasicAttackDamage(int attackDamage)
    {
        return attackDamage > 0 ? attackDamage : 1;
    }

    private static int NormalizeEffectDamage(int amount)
    {
        return System.Math.Max(0, amount);
    }

    private string ResolveUniqueEffectEventId(string baseEventId)
    {
        string normalized = baseEventId ?? "";
        if (!_effectEventIdOccurrences.TryGetValue(normalized, out int count))
        {
            _effectEventIdOccurrences[normalized] = 1;
            return normalized;
        }

        count++;
        _effectEventIdOccurrences[normalized] = count;
        return $"{normalized}:{count}";
    }

    private static BattleEvent CreateEffectDamageEvent(
        EffectDamageRequest request,
        BattleEventKind kind,
        string eventId,
        string reasonCode,
        int corpsStrengthDelta,
        int targetHpBefore,
        int targetHpAfter)
    {
        BattleRuntimeActor target = request.TargetHealth.Actor;
        return new BattleEvent
        {
            EventId = eventId,
            BattleId = request.BattleId,
            BattleGroupId = request.Actor.BattleGroupId,
            ActorId = request.Actor.ActorId,
            TargetId = target.ActorId,
            SourceCommandId = request.SourceCommandId,
            SourceActionId = request.SourceActionId,
            SourceDefinitionId = request.SourceDefinitionId,
            EffectKind = request.EffectKind,
            Kind = kind,
            ReasonCode = reasonCode,
            RuntimeTick = request.RuntimeTick,
            RuntimeTimeSeconds = request.RuntimeTimeSeconds,
            CorpsStrengthDelta = corpsStrengthDelta,
            HasTargetHitPoints = kind == BattleEventKind.DamageApplied,
            TargetHpBefore = targetHpBefore,
            TargetHpAfter = targetHpAfter,
            HasActorCells = true,
            ActorGridX = request.ActorAnchor.X,
            ActorGridY = request.ActorAnchor.Y,
            ActorGridHeight = request.ActorAnchor.Height,
            HasTargetCells = true,
            TargetGridX = request.TargetAnchor.X,
            TargetGridY = request.TargetAnchor.Y,
            TargetGridHeight = request.TargetAnchor.Height
        };
    }
}
