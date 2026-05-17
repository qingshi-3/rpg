using System.Linq;
using System.Collections.Generic;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Results;

namespace Rpg.Runtime.Battle;

public sealed class BattleRuntimeSession
{
    private const int MaxAutonomousCombatTicks = 128;
    private const int CorpsAttackDamage = 20;
    private const int EngagementRange = 1;
    private const int MaxFootprintSize = 3;

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
            int corpsFootprintWidth = NormalizeFootprintSize(group.FootprintWidth);
            int corpsFootprintHeight = NormalizeFootprintSize(group.FootprintHeight);
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
                GridHeight = group.CellHeight,
                MotionState = BattleRuntimeActorMotionState.Anchored,
                AttackRange = EngagementRange
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
                GridHeight = group.CellHeight,
                FootprintWidth = corpsFootprintWidth,
                FootprintHeight = corpsFootprintHeight,
                MotionState = BattleRuntimeActorMotionState.Anchored,
                AttackRange = EngagementRange
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
            HashSet<(int X, int Y)> occupiedCells = BuildOccupiedCells(state);
            HashSet<(int X, int Y)> reservedCells = new();
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
                    actor.TargetActorId = "";
                    continue;
                }

                actor.TargetActorId = target.ActorId;
                int distance = GetSquareGridDistance(actor, target);
                if (distance > System.Math.Max(1, actor.AttackRange))
                {
                    if (!TryAdvanceOneSquareGridStep(
                            state,
                            actor,
                            target,
                            occupiedCells,
                            reservedCells,
                            out (int FromX, int FromY, int FromHeight, int ToX, int ToY, int ToHeight) move))
                    {
                        continue;
                    }

                    stream.Add(new BattleEvent
                    {
                        EventId = $"{battleId}:tick_{tick}:{actor.ActorId}:move",
                        BattleId = battleId,
                        BattleGroupId = actor.BattleGroupId,
                        ActorId = actor.ActorId,
                        TargetId = target.ActorId,
                        Kind = BattleEventKind.MovementCompleted,
                        ReasonCode = "auto_advance",
                        HasMovementCells = true,
                        FromGridX = move.FromX,
                        FromGridY = move.FromY,
                        FromGridHeight = move.FromHeight,
                        ToGridX = move.ToX,
                        ToGridY = move.ToY,
                        ToGridHeight = move.ToHeight
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
            .OrderBy(item => GetSquareGridDistance(item, actor))
            .ThenBy(item => item.ActorId)
            .FirstOrDefault();
    }

    private static int GetSquareGridDistance(BattleRuntimeActor first, BattleRuntimeActor second)
    {
        if (first == null || second == null)
        {
            return 0;
        }

        return System.Math.Max(
            GetFootprintGap(first.GridX, first.FootprintWidth, second.GridX, second.FootprintWidth),
            GetFootprintGap(first.GridY, first.FootprintHeight, second.GridY, second.FootprintHeight));
    }

    private static HashSet<(int X, int Y)> BuildOccupiedCells(BattleRuntimeState state)
    {
        HashSet<(int X, int Y)> occupiedCells = new();
        foreach (BattleRuntimeActor actor in state?.Actors ?? Enumerable.Empty<BattleRuntimeActor>())
        {
            if (actor.Kind == BattleRuntimeActorKind.Corps && actor.HitPoints > 0)
            {
                foreach ((int x, int y) in EnumerateFootprintCells(actor))
                {
                    occupiedCells.Add((x, y));
                }
            }
        }

        return occupiedCells;
    }

    private static int GetFootprintGap(int firstStart, int firstSize, int secondStart, int secondSize)
    {
        int firstEnd = firstStart + NormalizeFootprintSize(firstSize) - 1;
        int secondEnd = secondStart + NormalizeFootprintSize(secondSize) - 1;
        if (firstStart > secondEnd)
        {
            return firstStart - secondEnd;
        }

        if (secondStart > firstEnd)
        {
            return secondStart - firstEnd;
        }

        return 0;
    }

    private static IEnumerable<(int X, int Y)> EnumerateFootprintCells(BattleRuntimeActor actor)
    {
        return EnumerateFootprintCells(actor, actor?.GridX ?? 0, actor?.GridY ?? 0);
    }

    private static IEnumerable<(int X, int Y)> EnumerateFootprintCells(BattleRuntimeActor actor, int anchorX, int anchorY)
    {
        int width = NormalizeFootprintSize(actor?.FootprintWidth ?? 1);
        int height = NormalizeFootprintSize(actor?.FootprintHeight ?? 1);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                yield return (anchorX + x, anchorY + y);
            }
        }
    }

    private static int NormalizeFootprintSize(int value)
    {
        // Footprint support is intentionally small in this slice so per-actor checks stay bounded.
        return System.Math.Clamp(value <= 0 ? 1 : value, 1, MaxFootprintSize);
    }

    private static bool TryAdvanceOneSquareGridStep(
        BattleRuntimeState state,
        BattleRuntimeActor actor,
        BattleRuntimeActor target,
        ISet<(int X, int Y)> occupiedCells,
        ISet<(int X, int Y)> reservedCells,
        out (int FromX, int FromY, int FromHeight, int ToX, int ToY, int ToHeight) move)
    {
        move = default;
        if (state?.Actors == null || actor == null || target == null)
        {
            return false;
        }

        int stepX = System.Math.Sign(target.GridX - actor.GridX);
        int stepY = System.Math.Sign(target.GridY - actor.GridY);
        if (TryReserveNeighbor(actor, occupiedCells, reservedCells, actor.GridX + stepX, actor.GridY + stepY, out move))
        {
            return true;
        }

        if (TryReserveNeighbor(actor, occupiedCells, reservedCells, actor.GridX + stepX, actor.GridY, out move))
        {
            return true;
        }

        return TryReserveNeighbor(actor, occupiedCells, reservedCells, actor.GridX, actor.GridY + stepY, out move);
    }

    private static bool TryReserveNeighbor(
        BattleRuntimeActor actor,
        ISet<(int X, int Y)> occupiedCells,
        ISet<(int X, int Y)> reservedCells,
        int x,
        int y,
        out (int FromX, int FromY, int FromHeight, int ToX, int ToY, int ToHeight) move)
    {
        move = default;
        if (actor == null ||
            (x == actor.GridX && y == actor.GridY))
        {
            return false;
        }

        HashSet<(int X, int Y)> currentFootprint = EnumerateFootprintCells(actor).ToHashSet();
        (int X, int Y)[] candidateFootprint = EnumerateFootprintCells(actor, x, y).ToArray();
        foreach ((int cellX, int cellY) in candidateFootprint)
        {
            if (reservedCells?.Contains((cellX, cellY)) == true ||
                (occupiedCells?.Contains((cellX, cellY)) == true && !currentFootprint.Contains((cellX, cellY))))
            {
                return false;
            }
        }

        move = (actor.GridX, actor.GridY, actor.GridHeight, x, y, actor.GridHeight);
        actor.HasReservedGridCell = true;
        actor.ReservedGridX = x;
        actor.ReservedGridY = y;
        actor.ReservedGridHeight = actor.GridHeight;
        actor.MotionState = BattleRuntimeActorMotionState.Moving;
        foreach ((int cellX, int cellY) in candidateFootprint)
        {
            reservedCells?.Add((cellX, cellY));
        }

        foreach ((int cellX, int cellY) in currentFootprint)
        {
            occupiedCells?.Remove((cellX, cellY));
        }

        foreach ((int cellX, int cellY) in candidateFootprint)
        {
            occupiedCells?.Add((cellX, cellY));
        }

        actor.GridX = x;
        actor.GridY = y;
        actor.Position = actor.GridX;
        actor.HasReservedGridCell = false;
        actor.MotionState = BattleRuntimeActorMotionState.Anchored;
        return true;
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
