using System;
using System.Collections.Generic;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.AI;
using Rpg.Runtime.Battle.AI.BehaviorTree;
using Rpg.Runtime.Battle.Events;
using Rpg.Runtime.Battle.Tactics;

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

    internal static void RuntimeBehaviorTreeSelectsTargetFromCandidateFacts()
    {
        DefaultBattleRuntimeAiExecutor executor = new();

        BattleRuntimeAiActionRequest attack = executor.ChooseAction(new BattleRuntimeAiDecisionFacts
        {
            ActorId = "enemy_middle",
            AttackRange = 1,
            CanAttackNow = true,
            TargetCandidates =
            {
                new BattleRuntimeAiTargetCandidateFacts
                {
                    ActorId = "player_a_lexicographic_first",
                    SelectionTier = 0,
                    OrthogonalAttackGap = 1,
                    GridGap = 4,
                    CenterManhattanDistance = 5,
                    HitPoints = 100,
                    IsImmediateAttackOpportunity = true
                },
                new BattleRuntimeAiTargetCandidateFacts
                {
                    ActorId = "player_z_same_frontage",
                    SelectionTier = 0,
                    OrthogonalAttackGap = 1,
                    GridGap = 4,
                    CenterManhattanDistance = 1,
                    HitPoints = 100,
                    IsImmediateAttackOpportunity = true
                }
            }
        });

        AssertEqual(BattleRuntimeAiActionKind.AttackTarget, attack.Kind, "charged actor should attack the behavior-tree selected target");
        AssertEqual("player_z_same_frontage", attack.TargetActorId, "target selection should prefer nearest frontage before actor-id tie-breaking");
    }

    internal static void RuntimeTickResolverDoesNotPreselectOrdinaryTargetsBeforeBehaviorTree()
    {
        string root = ProjectRoot();
        string tickResolverSource = File.ReadAllText(Path.Combine(root, "src", "Runtime", "Battle", "BattleRuntimeTickResolver.cs"));
        string behaviorTreeSource = File.ReadAllText(Path.Combine(root, "src", "Runtime", "Battle", "AI", "BehaviorTree", "BattleRuntimeBehaviorTree.cs"));

        AssertTrue(
            !tickResolverSource.Contains("BattleTargetSelectionService.FindCombatZoneScopedEnemyCorps", StringComparison.Ordinal) &&
            !tickResolverSource.Contains("BattleTargetSelectionService.FindEnemyCorpsForCommand", StringComparison.Ordinal) &&
            !tickResolverSource.Contains("BattleTargetSelectionService.FindRegionScopedEnemyCorps", StringComparison.Ordinal),
            "runtime tick resolver must build candidate facts and let the behavior tree select ordinary targets");
        AssertTrue(
            behaviorTreeSource.Contains("SelectTarget", StringComparison.Ordinal) &&
            behaviorTreeSource.Contains("TargetCandidates", StringComparison.Ordinal),
            "behavior tree source should own reusable target-selection nodes over candidate facts");
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

    internal static void RuntimeAiExecutorDelegatesToBehaviorTreeBoundary()
    {
        string root = ProjectRoot();
        string defaultExecutorPath = Path.Combine(root, "src", "Runtime", "Battle", "AI", "DefaultBattleRuntimeAiExecutor.cs");
        string defaultExecutorSource = File.ReadAllText(defaultExecutorPath);

        AssertTrue(
            defaultExecutorSource.Contains("BattleRuntimeBehaviorTreeExecutor.CreateDefault", StringComparison.Ordinal),
            "default Runtime AI executor should delegate tactical decisions to the behavior-tree executor");
        AssertTrue(
            !defaultExecutorSource.Contains("if (facts.DistanceToTarget", StringComparison.Ordinal),
            "default Runtime AI executor should not reintroduce a flat tactical if/else chain");
        AssertTrue(
            !defaultExecutorSource.Contains("if (facts.HasLocalCombatSituation", StringComparison.Ordinal),
            "default Runtime AI executor should keep local-combat branching inside behavior-tree nodes");
    }

    internal static void RuntimeBehaviorTreeNodesUseSelectorAndSequenceSemantics()
    {
        BattleRuntimeAiDecisionFacts facts = new()
        {
            ActorId = "actor_a",
            TargetActorId = "actor_b",
            HasTarget = true
        };

        IBattleRuntimeBehaviorNode selector = BattleRuntimeBehaviorNode.Selector(
            BattleRuntimeBehaviorNode.Condition(_ => false, "first_failed"),
            BattleRuntimeBehaviorNode.Action(item => BattleRuntimeAiActionRequest.Hold(item.ActorId, "first_success")),
            BattleRuntimeBehaviorNode.Action(item => BattleRuntimeAiActionRequest.Hold(item.ActorId, "second_success")));

        BattleRuntimeBehaviorResult selected = selector.Tick(facts);

        AssertTrue(selected.Success, "selector should succeed when a later child succeeds");
        AssertEqual("first_success", selected.Request.FailureReason, "selector should stop at first successful child");

        IBattleRuntimeBehaviorNode sequence = BattleRuntimeBehaviorNode.Sequence(
            BattleRuntimeBehaviorNode.Condition(_ => true, "first_passed"),
            BattleRuntimeBehaviorNode.Condition(_ => false, "second_failed"),
            BattleRuntimeBehaviorNode.Action(item => BattleRuntimeAiActionRequest.Hold(item.ActorId, "should_not_run")));

        BattleRuntimeBehaviorResult sequenced = sequence.Tick(facts);

        AssertTrue(!sequenced.Success, "sequence should fail when a condition fails");
        AssertEqual("second_failed", sequenced.FailureReason, "sequence should report the first failed child reason");
        AssertTrue(sequenced.Request == null, "sequence should not run later actions after a failed child");
    }

    internal static void RuntimeBehaviorTreePreservesLocalCombatRequestOrder()
    {
        BattleRuntimeBehaviorTreeExecutor executor = BattleRuntimeBehaviorTreeExecutor.CreateDefault();

        BattleRuntimeAiActionRequest outsideLeash = executor.ChooseAction(new BattleRuntimeAiDecisionFacts
        {
            ActorId = "actor_a",
            TargetActorId = "actor_b",
            HasTarget = true,
            DistanceToTarget = 3,
            AttackRange = 1,
            HasLocalCombatSituation = true,
            LocalCombatInsideLeash = false,
            LocalCombatRejectReasonCode = "reject_test_leash",
            LocalCombatTargetActorId = "actor_b",
            LocalCombatHasReachableAttackSlot = true,
            LocalCombatHasReachableSupportSlot = true
        });
        AssertEqual(BattleRuntimeAiActionKind.Hold, outsideLeash.Kind, "outside leash should hold before slot selection");
        AssertEqual("reject_test_leash", outsideLeash.FailureReason, "outside leash should preserve rejection reason");

        BattleRuntimeAiActionRequest attackSlot = executor.ChooseAction(new BattleRuntimeAiDecisionFacts
        {
            ActorId = "actor_a",
            TargetActorId = "actor_b",
            HasTarget = true,
            DistanceToTarget = 3,
            AttackRange = 1,
            HasLocalCombatSituation = true,
            LocalCombatInsideLeash = true,
            LocalCombatSituationId = "local:1",
            LocalCombatTargetActorId = "actor_b",
            LocalCombatHasReachableAttackSlot = true,
            LocalCombatHasReachableSupportSlot = true,
            LocalCombatJoinReasonCode = "join_test"
        });
        AssertEqual(BattleRuntimeAiActionKind.JoinLocalCombat, attackSlot.Kind, "reachable attack slot should join before support fallback");
        AssertEqual("join_test", attackSlot.ReasonCode, "join request should preserve reason code");

        BattleRuntimeAiActionRequest supportSlot = executor.ChooseAction(new BattleRuntimeAiDecisionFacts
        {
            ActorId = "actor_a",
            TargetActorId = "actor_b",
            HasTarget = true,
            DistanceToTarget = 3,
            AttackRange = 1,
            HasLocalCombatSituation = true,
            LocalCombatInsideLeash = true,
            LocalCombatSituationId = "local:1",
            LocalCombatTargetActorId = "actor_b",
            LocalCombatHasReachableSupportSlot = true,
            LocalCombatSupportReasonCode = "support_test"
        });
        AssertEqual(BattleRuntimeAiActionKind.HoldSupport, supportSlot.Kind, "support slot should be used after attack slot is unavailable");
        AssertEqual("support_test", supportSlot.ReasonCode, "support request should preserve reason code");

        BattleRuntimeAiActionRequest noSlot = executor.ChooseAction(new BattleRuntimeAiDecisionFacts
        {
            ActorId = "actor_a",
            TargetActorId = "actor_b",
            HasTarget = true,
            DistanceToTarget = 3,
            AttackRange = 1,
            HasLocalCombatSituation = true,
            LocalCombatInsideLeash = true,
            LocalCombatTargetActorId = "actor_b"
        });
        AssertEqual(BattleRuntimeAiActionKind.Hold, noSlot.Kind, "local combat with no reachable slot should hold explicitly");
        AssertEqual(BattleGroupTacticalReasonCode.LocalRegionDegradeNoReachableSlot, noSlot.FailureReason, "no slot hold reason");
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
        BattleStartSnapshot snapshot = new()
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
        TargetBattleTestTopology.CompileAroundGroups(snapshot);
        return snapshot;
    }

    private static string ProjectRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current != null && !File.Exists(Path.Combine(current.FullName, "rpg.csproj")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("project root not found");
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
