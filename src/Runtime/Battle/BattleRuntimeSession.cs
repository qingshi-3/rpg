using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Results;

namespace Rpg.Runtime.Battle;

public sealed class BattleRuntimeSession
{
    private const int MaxAutonomousCombatTicks = 128;
    private const int CorpsAttackDamage = 20;
    private const int EngagementRange = 1;

    public BattleRuntimeSessionResult RunMinimal(BattleStartSnapshot snapshot)
    {
        BattleEventStream stream = new();
        string battleId = snapshot?.BattleId ?? "";
        string snapshotId = snapshot?.SnapshotId ?? "";
        if (string.IsNullOrWhiteSpace(snapshotId) ||
            string.IsNullOrWhiteSpace(battleId) ||
            snapshot?.BattleGroups == null ||
            snapshot.BattleGroups.Count == 0 ||
            snapshot.BattleGroups.Any(item => !HasRequiredGroupIdentity(item)))
        {
            stream.Add(new BattleEvent
            {
                EventId = $"{battleId}:snapshot_invalid",
                BattleId = battleId,
                Kind = BattleEventKind.CommandRejected,
                ReasonCode = "battle_snapshot_invalid"
            });

            return new BattleRuntimeSessionResult
            {
                EventStream = stream,
                FinalState = new BattleRuntimeState
                {
                    SnapshotId = snapshotId,
                    BattleId = battleId
                },
                Outcome = new BattleOutcomeResult
                {
                    SnapshotId = snapshotId,
                    BattleId = battleId,
                    IsComplete = false,
                    TerminationReason = BattleTerminationReason.RuntimeException
                }
            };
        }

        stream.Add(new BattleEvent
        {
            EventId = $"{battleId}:started",
            BattleId = battleId,
            Kind = BattleEventKind.BattleStarted
        });

        BattleRuntimeState state = BuildRuntimeState(snapshot);
        foreach (BattleRuntimeActor actor in state.Actors)
        {
            stream.Add(new BattleEvent
            {
                EventId = $"{battleId}:{actor.ActorId}:spawned",
                BattleId = battleId,
                BattleGroupId = actor.BattleGroupId,
                ActorId = actor.ActorId,
                Kind = BattleEventKind.RuntimeActorSpawned,
                ReasonCode = "runtime_actor_spawned"
            });
        }

        BattleTerminationReason terminationReason = ResolveAutonomousCombat(state, stream, battleId);
        stream.Add(new BattleEvent
        {
            EventId = $"{battleId}:ended",
            BattleId = battleId,
            Kind = BattleEventKind.BattleEnded,
            ReasonCode = terminationReason.ToString()
        });

        return new BattleRuntimeSessionResult
        {
            EventStream = stream,
            FinalState = state,
            Outcome = BuildCompletedOutcome(snapshotId, battleId, state, terminationReason)
        };
    }

    private static BattleRuntimeState BuildRuntimeState(BattleStartSnapshot snapshot)
    {
        BattleRuntimeState state = new()
        {
            SnapshotId = snapshot?.SnapshotId ?? "",
            BattleId = snapshot?.BattleId ?? ""
        };

        var sourceForceIndexes = new System.Collections.Generic.Dictionary<string, int>();
        foreach (BattleGroupSnapshot group in snapshot?.BattleGroups ?? Enumerable.Empty<BattleGroupSnapshot>())
        {
            string sourceForceId = string.IsNullOrWhiteSpace(group.SourceForceId)
                ? group.BattleGroupId
                : group.SourceForceId;
            sourceForceIndexes.TryGetValue(sourceForceId, out int sourceForceIndex);
            sourceForceIndexes[sourceForceId] = sourceForceIndex + 1;
            int corpsHitPoints = System.Math.Max(1, group.CorpsStrength);
            state.Actors.Add(new BattleRuntimeActor
            {
                ActorId = $"{group.BattleGroupId}:hero",
                BattleGroupId = group.BattleGroupId,
                FactionId = group.FactionId ?? "",
                SourceForceId = sourceForceId,
                SourceStateId = group.HeroId,
                Kind = BattleRuntimeActorKind.Hero,
                HitPoints = 1,
                Position = group.CellX,
                GridX = group.CellX,
                GridY = group.CellY,
                GridHeight = group.CellHeight
            });
            state.Actors.Add(new BattleRuntimeActor
            {
                ActorId = $"{sourceForceId}:{sourceForceIndex + 1}",
                BattleGroupId = group.BattleGroupId,
                FactionId = group.FactionId ?? "",
                SourceForceId = sourceForceId,
                SourceStateId = group.CorpsId,
                Kind = BattleRuntimeActorKind.Corps,
                HitPoints = corpsHitPoints,
                Position = group.CellX,
                GridX = group.CellX,
                GridY = group.CellY,
                GridHeight = group.CellHeight
            });
        }

        return state;
    }

    private static BattleTerminationReason ResolveAutonomousCombat(
        BattleRuntimeState state,
        BattleEventStream stream,
        string battleId)
    {
        // V0 keeps combat authority inside Runtime: Application supplies frozen
        // factions/actors, and settlement consumes only emitted events/results.
        if (state?.Actors == null)
        {
            return BattleTerminationReason.RuntimeException;
        }

        for (int tick = 0; tick < MaxAutonomousCombatTicks; tick++)
        {
            BattleTerminationReason resolved = ResolveTermination(state);
            if (resolved != BattleTerminationReason.None)
            {
                return resolved;
            }

            foreach (BattleRuntimeActor actor in state.Actors
                         .Where(item => item.Kind == BattleRuntimeActorKind.Corps && item.HitPoints > 0)
                         .OrderBy(item => item.ActorId)
                         .ToArray())
            {
                if (actor.HitPoints <= 0)
                {
                    continue;
                }

                BattleRuntimeActor target = FindNearestEnemyCorps(state, actor);
                if (target == null)
                {
                    continue;
                }

                int distance = GetManhattanDistance(actor, target);
                if (distance > EngagementRange)
                {
                    AdvanceOneGridStep(actor, target);
                    stream.Add(new BattleEvent
                    {
                        EventId = $"{battleId}:tick_{tick}:{actor.ActorId}:move",
                        BattleId = battleId,
                        BattleGroupId = actor.BattleGroupId,
                        ActorId = actor.ActorId,
                        TargetId = target.ActorId,
                        Kind = BattleEventKind.MovementCompleted,
                        ReasonCode = "auto_advance"
                    });
                    continue;
                }

                int damage = System.Math.Min(CorpsAttackDamage, System.Math.Max(0, target.HitPoints));
                target.HitPoints = System.Math.Max(0, target.HitPoints - damage);
                stream.Add(new BattleEvent
                {
                    EventId = $"{battleId}:tick_{tick}:{actor.ActorId}:attack:{target.ActorId}",
                    BattleId = battleId,
                    BattleGroupId = actor.BattleGroupId,
                    ActorId = actor.ActorId,
                    TargetId = target.ActorId,
                    Kind = BattleEventKind.DamageApplied,
                    ReasonCode = target.HitPoints <= 0 ? "auto_attack_target_defeated" : "auto_attack",
                    CorpsStrengthDelta = -damage
                });
            }
        }

        return BattleTerminationReason.RuntimeException;
    }

    private static BattleRuntimeActor FindNearestEnemyCorps(BattleRuntimeState state, BattleRuntimeActor actor)
    {
        return state.Actors
            .Where(item =>
                item.Kind == BattleRuntimeActorKind.Corps &&
                item.HitPoints > 0 &&
                !SameFaction(item, actor))
            .OrderBy(item => GetManhattanDistance(item, actor))
            .ThenBy(item => item.ActorId)
            .FirstOrDefault();
    }

    private static int GetManhattanDistance(BattleRuntimeActor first, BattleRuntimeActor second)
    {
        return System.Math.Abs((first?.GridX ?? 0) - (second?.GridX ?? 0)) +
               System.Math.Abs((first?.GridY ?? 0) - (second?.GridY ?? 0));
    }

    private static void AdvanceOneGridStep(BattleRuntimeActor actor, BattleRuntimeActor target)
    {
        int deltaX = (target?.GridX ?? 0) - actor.GridX;
        int deltaY = (target?.GridY ?? 0) - actor.GridY;
        if (System.Math.Abs(deltaX) >= System.Math.Abs(deltaY) && deltaX != 0)
        {
            actor.GridX += deltaX > 0 ? 1 : -1;
        }
        else if (deltaY != 0)
        {
            actor.GridY += deltaY > 0 ? 1 : -1;
        }

        actor.Position = actor.GridX;
    }

    private static BattleTerminationReason ResolveTermination(BattleRuntimeState state)
    {
        BattleRuntimeActor[] corps = state.Actors
            .Where(item => item.Kind == BattleRuntimeActorKind.Corps)
            .ToArray();
        if (corps.Length == 0)
        {
            return BattleTerminationReason.RuntimeException;
        }

        bool hasPlayer = corps.Any(item => item.HitPoints > 0 && IsPlayerFaction(item.FactionId));
        bool hasEnemy = corps.Any(item => item.HitPoints > 0 && !IsPlayerFaction(item.FactionId));
        if (hasPlayer && !hasEnemy)
        {
            return BattleTerminationReason.NormalVictory;
        }

        if (!hasPlayer && hasEnemy)
        {
            return BattleTerminationReason.NormalDefeat;
        }

        if (!hasPlayer && !hasEnemy)
        {
            return BattleTerminationReason.NormalDefeat;
        }

        return BattleTerminationReason.None;
    }

    private static BattleOutcomeResult BuildCompletedOutcome(
        string snapshotId,
        string battleId,
        BattleRuntimeState state,
        BattleTerminationReason terminationReason)
    {
        BattleOutcomeResult outcome = BattleOutcomeResult.Completed(
            snapshotId,
            battleId,
            terminationReason);
        foreach (BattleRuntimeActor actor in state?.Actors ?? Enumerable.Empty<BattleRuntimeActor>())
        {
            outcome.ActorOutcomes.Add(new BattleActorOutcome
            {
                ActorId = actor.ActorId,
                BattleGroupId = actor.BattleGroupId,
                FactionId = actor.FactionId,
                SourceForceId = actor.SourceForceId,
                SourceStateId = actor.SourceStateId,
                Kind = actor.Kind,
                Survived = actor.HitPoints > 0,
                RemainingHitPoints = System.Math.Max(0, actor.HitPoints)
            });
        }

        return outcome;
    }

    private static bool HasRequiredGroupIdentity(BattleGroupSnapshot group)
    {
        return group != null &&
            !string.IsNullOrWhiteSpace(group.BattleGroupId) &&
            !string.IsNullOrWhiteSpace(group.HeroId) &&
            !string.IsNullOrWhiteSpace(group.CorpsId) &&
            !string.IsNullOrWhiteSpace(group.HeroDefinitionId) &&
            !string.IsNullOrWhiteSpace(group.CorpsDefinitionId) &&
            !string.IsNullOrWhiteSpace(group.SourceLocationId);
    }

    private static bool SameFaction(BattleRuntimeActor first, BattleRuntimeActor second)
    {
        return string.Equals(
            NormalizeFaction(first?.FactionId),
            NormalizeFaction(second?.FactionId),
            System.StringComparison.Ordinal);
    }

    private static bool IsPlayerFaction(string factionId)
    {
        return string.Equals(NormalizeFaction(factionId), "player", System.StringComparison.Ordinal);
    }

    private static string NormalizeFaction(string factionId)
    {
        return string.IsNullOrWhiteSpace(factionId) ? "player" : factionId.Trim();
    }
}
