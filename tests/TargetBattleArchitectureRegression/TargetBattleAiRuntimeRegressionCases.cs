using System;
using System.Collections.Generic;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.Events;

internal static class TargetBattleAiRuntimeRegressionCases
{
    internal static void RuntimeAiExecutorBoundaryUsesTypedRequests()
    {
        DefaultBattleRuntimeAiExecutor executor = new();

        BattleRuntimeAiActionRequest advance = executor.ChooseAction(new BattleRuntimeAiDecisionFacts
        {
            ActorId = "actor_a",
            TargetActorId = "actor_b",
            HasTarget = true,
            DistanceToTarget = 3,
            AttackRange = 1,
            CanAttackNow = false
        });
        AssertEqual(BattleRuntimeAiActionKind.AdvanceTowardTarget, advance.Kind, "out-of-range actor should request an advance, not mutate position");
        AssertEqual("actor_a", advance.ActorId, "advance actor id");
        AssertEqual("actor_b", advance.TargetActorId, "advance target id");

        BattleRuntimeAiActionRequest wait = executor.ChooseAction(new BattleRuntimeAiDecisionFacts
        {
            ActorId = "actor_a",
            TargetActorId = "actor_b",
            HasTarget = true,
            DistanceToTarget = 1,
            AttackRange = 1,
            CanAttackNow = false
        });
        AssertEqual(BattleRuntimeAiActionKind.WaitForAttackCharge, wait.Kind, "in-range actor without charge should wait for runtime charge");

        BattleRuntimeAiActionRequest attack = executor.ChooseAction(new BattleRuntimeAiDecisionFacts
        {
            ActorId = "actor_a",
            TargetActorId = "actor_b",
            HasTarget = true,
            DistanceToTarget = 1,
            AttackRange = 1,
            CanAttackNow = true
        });
        AssertEqual(BattleRuntimeAiActionKind.AttackTarget, attack.Kind, "charged in-range actor should request an attack");

        BattleRuntimeAiActionRequest hold = executor.ChooseAction(new BattleRuntimeAiDecisionFacts
        {
            ActorId = "actor_a",
            HasTarget = false
        });
        AssertEqual(BattleRuntimeAiActionKind.Hold, hold.Kind, "actor without target should request hold");
        AssertEqual("no_target", hold.FailureReason, "hold reason");
    }

    internal static void RuntimeAiExecutorConsumesFactsWithoutMutableRuntimeAuthority()
    {
        Type factsType = typeof(BattleRuntimeAiDecisionFacts);
        string[] forbiddenMutableTypes =
        {
            "BattleRuntimeState",
            "BattleRuntimeActor",
            "BattleEventStream",
            "BattleNavigationGraph",
            "BattleDynamicOccupancy",
            "BattleMovementReservationMap"
        };
        string exposedFactsShape = string.Join(
            "|",
            factsType.GetProperties().Select(property => $"{property.Name}:{property.PropertyType.Name}"));

        foreach (string forbidden in forbiddenMutableTypes)
        {
            AssertTrue(!exposedFactsShape.Contains(forbidden, StringComparison.Ordinal), $"AI decision facts must not expose mutable runtime authority type={forbidden}");
        }

        RecordingBattleRuntimeAiExecutor executor = new(new DefaultBattleRuntimeAiExecutor());
        BattleRuntimeSessionResult result = new BattleRuntimeSession(executor).RunMinimal(BuildOpposedSnapshot());

        AssertTrue(executor.SeenFacts.Count > 0, "runtime session should ask the injected AI executor for decisions");
        AssertTrue(
            executor.SeenFacts.Any(facts =>
                facts.ActorId == "force_player:1" &&
                facts.TargetActorId == "force_enemy:1" &&
                facts.HasTarget),
            "executor should receive typed actor/target facts");
        AssertTrue(
            result.EventStream.Events.Any(item => item.Kind == BattleEventKind.DamageApplied),
            "runtime must still apply damage through runtime authority after AI request selection");
    }

    private sealed class RecordingBattleRuntimeAiExecutor : IBattleRuntimeAiExecutor
    {
        private readonly IBattleRuntimeAiExecutor _inner;

        public RecordingBattleRuntimeAiExecutor(IBattleRuntimeAiExecutor inner)
        {
            _inner = inner;
        }

        public List<BattleRuntimeAiDecisionFacts> SeenFacts { get; } = new();

        public BattleRuntimeAiActionRequest ChooseAction(BattleRuntimeAiDecisionFacts facts)
        {
            SeenFacts.Add(facts);
            return _inner.ChooseAction(facts);
        }
    }

    private static BattleStartSnapshot BuildOpposedSnapshot()
    {
        return new BattleStartSnapshot
        {
            SnapshotId = "snapshot_runtime_ai_boundary",
            BattleId = "battle_runtime_ai_boundary",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                new BattleGroupSnapshot
                {
                    BattleGroupId = "group_player",
                    FactionId = "player",
                    SourceForceId = "force_player",
                    HeroId = "hero_player",
                    HeroDefinitionId = "hero_def_player",
                    CorpsId = "corps_player",
                    CorpsDefinitionId = "player_corps",
                    CorpsStrength = 80,
                    SourceLocationId = "city_player",
                    CellX = 0,
                    CellY = 0
                },
                new BattleGroupSnapshot
                {
                    BattleGroupId = "group_enemy",
                    FactionId = "enemy",
                    SourceForceId = "force_enemy",
                    HeroId = "hero_enemy",
                    HeroDefinitionId = "hero_def_enemy",
                    CorpsId = "corps_enemy",
                    CorpsDefinitionId = "enemy_corps",
                    CorpsStrength = 20,
                    SourceLocationId = "site_1",
                    CellX = 3,
                    CellY = 0
                }
            }
        };
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"{message}: expected={expected} actual={actual}");
        }
    }
}
