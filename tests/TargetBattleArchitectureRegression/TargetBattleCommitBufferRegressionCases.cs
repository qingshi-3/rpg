using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rpg.Application.Battle.Commands;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;

internal static class TargetBattleCommitBufferRegressionCases
{
    internal static void Register(Action<string, Action> run)
    {
        run("runtime commit buffer and actor attack shell are authored", RuntimeCommitBufferAndActorAttackShellAreAuthored);
        run("runtime basic attack resolver routes mutation through commit buffer", RuntimeBasicAttackResolverRoutesMutationThroughCommitBuffer);
        run("runtime basic attack health mutation routes through health component", RuntimeBasicAttackHealthMutationRoutesThroughHealthComponent);
        run("runtime basic attack lifecycle is actor local", RuntimeBasicAttackLifecycleIsActorLocal);
        run("runtime basic attack enters windup before impact damage", RuntimeBasicAttackEntersWindupBeforeImpactDamage);
        run("runtime pause during basic attack windup blocks impact damage", RuntimePauseDuringBasicAttackWindupBlocksImpactDamage);
        run("runtime explicit zero attack impact stays instant", RuntimeExplicitZeroAttackImpactStaysInstant);
        run("runtime same tick windup and instant impacts accumulate once", RuntimeSameTickWindupAndInstantImpactsAccumulateOnce);
        run("runtime skill at attack impact boundary cannot cancel due impact", RuntimeSkillAtAttackImpactBoundaryCannotCancelDueImpact);
        run("runtime commit buffer compatible path preserves same tick mutual attacks", RuntimeCommitBufferCompatiblePathPreservesSameTickMutualAttacks);
        run("runtime commit buffer compatible path applies damage before same tick movement", RuntimeCommitBufferCompatiblePathAppliesDamageBeforeSameTickMovement);
    }

    internal static void RuntimeCommitBufferAndActorAttackShellAreAuthored()
    {
        string root = ProjectRoot();
        string battleRuntimePath = Path.Combine(root, "src", "Runtime", "Battle");

        AssertSourceTypeExists(battleRuntimePath, "BattleCommitBuffer", "Core Slice A should author the deterministic per-tick mutation request buffer");
        AssertSourceTypeExists(battleRuntimePath, "BattleActorRuntime", "Core Slice A should author the actor-local runtime shell");
        AssertSourceTypeExists(battleRuntimePath, "BattleActionController", "Core Slice A should author the actor-local basic action controller shell");
    }

    internal static void RuntimeBasicAttackResolverRoutesMutationThroughCommitBuffer()
    {
        string root = ProjectRoot();
        string attackResolverPath = Path.Combine(root, "src", "Runtime", "Battle", "BattleAttackResolver.cs");
        AssertTrue(File.Exists(attackResolverPath), "BattleAttackResolver source file should exist");

        string source = File.ReadAllText(attackResolverPath);
        string relativePath = ToRepoPath(root, attackResolverPath);

        // Core Slice A should leave BattleAttackResolver as a request producer,
        // with final stream writes, HP writes, and recovery transitions behind the commit boundary.
        AssertDoesNotContain(source, "stream.Add(", relativePath, "BattleAttackResolver should enqueue attack events through BattleCommitBuffer instead of writing BattleEventStream directly");
        AssertDoesNotContain(source, ".HitPoints =", relativePath, "BattleAttackResolver should enqueue HP mutations through BattleCommitBuffer instead of assigning actor HP directly");
        AssertDoesNotContain(source, "MarkAttackRecovery(", relativePath, "BattleAttackResolver should route attack recovery through the commit boundary");
        AssertTrue(
            source.Contains("BattleCommitBuffer", StringComparison.Ordinal),
            "BattleAttackResolver should submit basic attack mutation requests to BattleCommitBuffer");
    }

    internal static void RuntimeBasicAttackHealthMutationRoutesThroughHealthComponent()
    {
        string root = ProjectRoot();
        string commitBufferPath = Path.Combine(root, "src", "Runtime", "Battle", "BattleCommitBuffer.cs");
        string healthPath = Path.Combine(root, "src", "Runtime", "Battle", "BattleHealthComponent.cs");
        AssertTrue(File.Exists(commitBufferPath), "BattleCommitBuffer source file should exist");
        AssertTrue(File.Exists(healthPath), "BattleHealthComponent source file should exist");

        string commitSource = File.ReadAllText(commitBufferPath);
        string healthSource = File.ReadAllText(healthPath);
        string commitRelativePath = ToRepoPath(root, commitBufferPath);
        string healthRelativePath = ToRepoPath(root, healthPath);

        AssertContains(healthSource, "CommitBasicAttackDamage", healthRelativePath, "BattleHealthComponent should expose the basic-attack health commit path");
        AssertContains(healthSource, "BasicAttackDamageCommitResult", healthRelativePath, "BattleHealthComponent should return basic-attack damage transition facts");
        AssertContains(healthSource, "CommitHitPointChange", healthRelativePath, "BattleHealthComponent should share HP/defeat transition semantics across damage sources");
        AssertContains(commitSource, "new BattleHealthComponent(targetFact.Actor)", commitRelativePath, "BattleCommitBuffer should commit basic-attack HP through the target health component");
        AssertContains(commitSource, "CommitBasicAttackDamage", commitRelativePath, "BattleCommitBuffer should call the basic-attack health commit path");
        AssertContains(commitSource, "basicAttackTargetHitPoints", commitRelativePath, "BattleCommitBuffer should keep basic-attack health commits scoped to attacked targets");
        AssertContains(commitSource, "basicAttackTargetHitPoints.TryGetValue", commitRelativePath, "BattleCommitBuffer should skip non-target actors during basic-attack health commit");
        AssertContains(commitSource, "basicAttackTargetIds.Contains", commitRelativePath, "BattleCommitBuffer should explicitly gate basic-attack health commits to attacked target ids");
        AssertContainsInOrder(
            commitSource,
            commitRelativePath,
            "BattleCommitBuffer should emit basic-attack defeated plan-state events in target actor-id order",
            "foreach (BattleRuntimeTickStartActorFact targetFact in tickStartFacts.Values",
            ".Where(item => basicAttackTargetIds.Contains",
            ".OrderBy(item => item.Actor.ActorId",
            "BattlePlanStateEmitter.SetPlanState");
        AssertDoesNotContain(commitSource, ".HitPoints =", commitRelativePath, "BattleCommitBuffer should not assign actor HP directly");
        AssertDoesNotContain(commitSource, "MarkDefeated(", commitRelativePath, "BattleCommitBuffer should not mark actor defeat directly");
        AssertDoesNotContain(commitSource, "BattleRuntimeActorPhase.Defeated", commitRelativePath, "BattleCommitBuffer should not assign defeated phase directly");
        AssertDoesNotContain(commitSource, "BattleRuntimeActorMotionState.Defeated", commitRelativePath, "BattleCommitBuffer should not assign defeated motion state directly");
        AssertContains(commitSource, "BattlePlanStateEmitter.SetPlanState", commitRelativePath, "BattleCommitBuffer should preserve basic-attack defeated plan-state event emission");
    }

    internal static void RuntimeBasicAttackLifecycleIsActorLocal()
    {
        string root = ProjectRoot();
        string actionControllerPath = Path.Combine(root, "src", "Runtime", "Battle", "BattleActionController.cs");
        string attackResolverPath = Path.Combine(root, "src", "Runtime", "Battle", "BattleAttackResolver.cs");
        string commitBufferPath = Path.Combine(root, "src", "Runtime", "Battle", "BattleCommitBuffer.cs");

        string actionControllerSource = File.ReadAllText(actionControllerPath);
        string attackResolverSource = File.ReadAllText(attackResolverPath);
        string commitBufferSource = File.ReadAllText(commitBufferPath);

        AssertContains(actionControllerSource, "StartBasicAttackAction", ToRepoPath(root, actionControllerPath), "BattleActionController should own basic attack start and locked payload");
        AssertContains(actionControllerSource, "AdvanceBasicAttackAction", ToRepoPath(root, actionControllerPath), "BattleActionController should own basic attack windup, impact, and recovery advancement");
        AssertContains(actionControllerSource, "MarkAttackWindup", ToRepoPath(root, actionControllerPath), "basic attack start should enter the AttackWindup phase");
        AssertContains(attackResolverSource, "AdvanceBasicAttackAction", ToRepoPath(root, attackResolverPath), "BattleAttackResolver should orchestrate actor-local attack advancement");
        AssertDoesNotContain(commitBufferSource, "MarkAttackRecovery(", ToRepoPath(root, commitBufferPath), "BattleCommitBuffer should commit impact facts, not own attacker recovery lifecycle");
    }

    internal static void RuntimeBasicAttackEntersWindupBeforeImpactDamage()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_basic_attack_lifecycle", enemyCellX: 1, enemyCellY: 0);
        foreach (BattleGroupSnapshot group in snapshot.BattleGroups)
        {
            group.MaxHitPoints = 20;
            group.AttackDamage = 5;
            group.AttackRange = 1;
            group.AttackSpeed = 1.0;
            group.AttackActionSeconds = 1.0;
            group.AttackImpactDelaySeconds = 0.4;
        }

        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        BattleRuntimeActor player = controller.State.Actors.Single(item => item.ActorId == "force_player:1");
        BattleRuntimeActor enemy = controller.State.Actors.Single(item => item.ActorId == "force_enemy:1");

        BattleRuntimeAdvanceResult start = controller.AdvanceFixedTick(0.1);

        AssertTrue(
            start.Events.All(item => item.Kind != BattleEventKind.DamageApplied),
            "starting a basic attack should enter windup without applying impact damage");
        AssertEqual(20, player.HitPoints, "player HP before opposing attack impact");
        AssertEqual(20, enemy.HitPoints, "enemy HP before attack impact");
        AssertEqual(BattleRuntimeActorPhase.AttackWindup, player.Phase, "player should enter attack windup");
        AssertEqual(BattleRuntimeActorPhase.AttackWindup, enemy.Phase, "enemy should enter attack windup");
        AssertFloatEqual(0.4, player.ActionReadyAtSeconds, 0.0001, "player next action boundary should be attack impact");

        _ = controller.AdvanceFixedTick(0.2);
        BattleRuntimeAdvanceResult beforeImpact = controller.AdvanceFixedTick(0.1);
        AssertTrue(
            beforeImpact.Events.All(item => item.Kind != BattleEventKind.DamageApplied),
            "runtime should not emit damage before the locked impact boundary");

        BattleRuntimeAdvanceResult impact = controller.AdvanceFixedTick(0.1);
        BattleEvent[] damage = impact.Events
            .Where(item => item.Kind == BattleEventKind.DamageApplied)
            .OrderBy(item => item.ActorId, StringComparer.Ordinal)
            .ToArray();

        AssertEqual(2, damage.Length, "both adjacent attacks should impact on the same runtime boundary");
        AssertTrue(damage.All(item => Math.Abs(item.RuntimeTimeSeconds - 0.4) <= 0.0001), "damage should be emitted at the runtime impact time");
        AssertEqual(15, player.HitPoints, "player HP after opposing impact");
        AssertEqual(15, enemy.HitPoints, "enemy HP after impact");
        AssertEqual(BattleRuntimeActorPhase.AttackRecovery, player.Phase, "player should enter attack recovery after impact");
        AssertFloatEqual(1.0, player.ActionReadyAtSeconds, 0.0001, "recovery boundary should be the original action end, not a second full action");
    }

    internal static void RuntimePauseDuringBasicAttackWindupBlocksImpactDamage()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_basic_attack_pause_windup", enemyCellX: 1, enemyCellY: 0);
        foreach (BattleGroupSnapshot group in snapshot.BattleGroups)
        {
            group.MaxHitPoints = 20;
            group.AttackDamage = 5;
            group.AttackRange = 1;
            group.AttackSpeed = 1.0;
            group.AttackActionSeconds = 1.0;
            group.AttackImpactDelaySeconds = 0.4;
        }

        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        BattleRuntimeActor player = controller.State.Actors.Single(item => item.ActorId == "force_player:1");
        BattleRuntimeActor enemy = controller.State.Actors.Single(item => item.ActorId == "force_enemy:1");

        _ = controller.AdvanceFixedTick(0.1);
        controller.SetPaused(true, "test_pause_attack_windup");
        BattleRuntimeAdvanceResult paused = controller.AdvanceFixedTick(1.0);

        AssertFloatEqual(0.1, controller.CurrentTimeSeconds, 0.0001, "paused fixed tick should not advance runtime time");
        AssertTrue(paused.Events.All(item => item.Kind != BattleEventKind.DamageApplied), "paused windup should not apply damage");
        AssertEqual(BattleRuntimeActorPhase.AttackWindup, player.Phase, "paused attacker should stay in windup");
        AssertEqual(20, player.HitPoints, "player HP should stay frozen during paused windup");
        AssertEqual(20, enemy.HitPoints, "enemy HP should stay frozen during paused windup");

        controller.SetPaused(false, "resume_attack_windup");
        _ = controller.AdvanceFixedTick(0.3);
        BattleRuntimeAdvanceResult impact = controller.AdvanceFixedTick(0.1);

        AssertTrue(
            impact.Events.Any(item => item.Kind == BattleEventKind.DamageApplied && item.ActorId == "force_player:1"),
            "resumed runtime should emit the delayed attack impact after reaching the boundary");
        AssertEqual(15, enemy.HitPoints, "enemy HP should change only after resumed impact");
    }

    internal static void RuntimeExplicitZeroAttackImpactStaysInstant()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_explicit_zero_attack_impact", enemyCellX: 1, enemyCellY: 0);
        foreach (BattleGroupSnapshot group in snapshot.BattleGroups)
        {
            group.MaxHitPoints = 10;
            group.AttackDamage = 10;
            group.AttackRange = 1;
            group.AttackSpeed = 1.0;
            group.AttackActionSeconds = 1.0;
            group.AttackImpactDelaySeconds = 0;
        }

        BattleRuntimeAdvanceResult firstTick = new BattleRuntimeSession()
            .Begin(snapshot)
            .AdvanceNextTick();

        BattleEvent[] damage = firstTick.Events
            .Where(item => item.Kind == BattleEventKind.DamageApplied)
            .ToArray();

        AssertEqual(2, damage.Length, "explicit zero impact delay should preserve same-tick instant impact");
        AssertTrue(damage.All(item => Math.Abs(item.RuntimeTimeSeconds) <= 0.0001), "instant impacts should emit at tick-start time");
    }

    internal static void RuntimeSameTickWindupAndInstantImpactsAccumulateOnce()
    {
        BattleRuntimeSessionController controller = new BattleRuntimeSession()
            .Begin(BuildMixedImpactBatchSnapshot());
        BattleRuntimeActor instantAttacker = controller.State.Actors.Single(item => item.ActorId == "player_instant:1");
        instantAttacker.Phase = BattleRuntimeActorPhase.WaitingForCharge;
        instantAttacker.AttackCharge = 0;
        instantAttacker.ActionReadyAtSeconds = 0.4;

        _ = controller.AdvanceFixedTick(0.1);
        _ = controller.AdvanceFixedTick(0.2);
        _ = controller.AdvanceFixedTick(0.1);
        BattleRuntimeAdvanceResult mixedImpactTick = controller.AdvanceFixedTick(0.1);

        BattleEvent[] targetDamage = mixedImpactTick.Events
            .Where(item => item.Kind == BattleEventKind.DamageApplied && item.TargetId == "enemy_target:1")
            .OrderBy(item => item.ActorId, StringComparer.Ordinal)
            .ToArray();
        BattleRuntimeActor target = controller.State.Actors.Single(item => item.ActorId == "enemy_target:1");

        AssertEqual(2, targetDamage.Length, "same runtime impact boundary should include delayed and instant impacts");
        AssertTrue(targetDamage.All(item => Math.Abs(item.RuntimeTimeSeconds - 0.4) <= 0.0001), "both impacts should share the same runtime boundary");
        AssertEqual(14, targetDamage.Sum(item => Math.Abs(item.CorpsStrengthDelta)), "events should report both damage applications");
        AssertEqual(6, target.HitPoints, "target HP should accumulate both same-tick basic attack commits exactly once");
    }

    internal static void RuntimeSkillAtAttackImpactBoundaryCannotCancelDueImpact()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_skill_at_attack_impact", enemyCellX: 1, enemyCellY: 0);
        foreach (BattleGroupSnapshot group in snapshot.BattleGroups)
        {
            group.MaxHitPoints = 30;
            group.AttackDamage = 5;
            group.AttackRange = 1;
            group.AttackActionSeconds = 1.0;
            group.AttackImpactDelaySeconds = 0.4;
        }
        AddInterruptingDamageSkill(snapshot, "impact_boundary_skill", damage: 7);

        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        _ = controller.AdvanceFixedTick(0.4);
        BattleRuntimeCommandSubmitResult submit = controller.SubmitCommand(new CommandRequest
        {
            CommandId = "cmd_impact_boundary_skill",
            BattleId = "battle_skill_at_attack_impact",
            BattleGroupId = "group_player",
            SourceActorId = "force_player:1",
            Channel = CommandChannel.Hero,
            Kind = CommandKind.CastSkill,
            SkillId = "impact_boundary_skill",
            TargetActorId = "force_enemy:1"
        });
        AssertTrue(submit.Accepted, "impact-boundary skill command should be accepted as queued intent");

        BattleRuntimeAdvanceResult impact = controller.AdvanceFixedTick(0.1);

        AssertTrue(
            impact.Events.Any(item =>
                item.Kind == BattleEventKind.DamageApplied &&
                item.ActorId == "force_player:1" &&
                item.TargetId == "force_enemy:1" &&
                item.ReasonCode == "auto_attack"),
            "basic attack impact already due at the runtime boundary must resolve in the same tick");
        AssertTrue(
            impact.Events.All(item =>
                item.SourceCommandId != "cmd_impact_boundary_skill" ||
                item.Kind != BattleEventKind.SkillUsed && item.Kind != BattleEventKind.DamageApplied),
            "skill command at the impact boundary must wait instead of releasing over an already-due basic attack");
        AssertTrue(
            impact.Events.All(item =>
                item.Kind != BattleEventKind.CommandInterrupted ||
                item.SourceCommandId != "cmd_impact_boundary_skill"),
            "skill command at the impact boundary must not report pre-impact interruption");
    }

    internal static void RuntimeCommitBufferCompatiblePathPreservesSameTickMutualAttacks()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_commit_buffer_same_tick_mutual", enemyCellX: 1, enemyCellY: 0);
        foreach (BattleGroupSnapshot group in snapshot.BattleGroups)
        {
            group.MaxHitPoints = 10;
            group.AttackDamage = 10;
            group.AttackRange = 1;
            group.AttackSpeed = 1.0;
        }

        BattleRuntimeSessionResult result = new BattleRuntimeSession().RunMinimal(snapshot);

        BattleEvent[] tickZeroDamage = result.EventStream.Events
            .Where(item => item.Kind == BattleEventKind.DamageApplied && item.RuntimeTick == 0)
            .ToArray();
        AssertEqual(2, tickZeroDamage.Length, "adjacent opponents should both apply damage on tick zero");
        AssertTrue(
            tickZeroDamage.Any(item => item.ActorId == "force_player:1" && item.TargetId == "force_enemy:1"),
            "player should damage enemy on tick zero");
        AssertTrue(
            tickZeroDamage.Any(item => item.ActorId == "force_enemy:1" && item.TargetId == "force_player:1"),
            "enemy should damage player on tick zero");

        BattleRuntimeActor playerActor = result.FinalState.Actors.Single(item => item.ActorId == "force_player:1");
        BattleRuntimeActor enemyActor = result.FinalState.Actors.Single(item => item.ActorId == "force_enemy:1");
        AssertEqual(0, playerActor.HitPoints, "compatible path should preserve mutual defeat");
        AssertEqual(0, enemyActor.HitPoints, "compatible path should preserve mutual defeat");
    }

    internal static void RuntimeCommitBufferCompatiblePathAppliesDamageBeforeSameTickMovement()
    {
        BattleRuntimeAdvanceResult tick = new BattleRuntimeSession()
            .Begin(BuildDamageBeforeMovementSnapshot())
            .AdvanceNextTick();

        BattleEvent[] events = tick.Events.ToArray();
        int damageIndex = IndexOf(events, item =>
            item.Kind == BattleEventKind.DamageApplied &&
            item.ActorId == "player_ranged:1" &&
            item.TargetId == "enemy_hold:1");
        int movementIndex = IndexOf(events, item =>
            item.Kind == BattleEventKind.MovementStarted &&
            item.ActorId == "player_mover:1");

        AssertTrue(damageIndex >= 0, "same tick should include ranged damage from the attack path");
        AssertTrue(movementIndex >= 0, "same tick should include unrelated movement from the movement path");
        AssertTrue(
            damageIndex < movementIndex,
            $"damage should stay before same-tick movement in the compatible path: damageIndex={damageIndex} movementIndex={movementIndex}");
    }

    private static BattleStartSnapshot BuildOpposedSnapshot(
        string battleId,
        int enemyCellX,
        int enemyCellY)
    {
        return new BattleStartSnapshot
        {
            SnapshotId = $"snapshot_{battleId}",
            BattleId = battleId,
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("group_player", "player", "force_player", "hero_player", "corps_player", 0, 0),
                BuildGroup("group_enemy", "enemy", "force_enemy", "hero_enemy", "corps_enemy", enemyCellX, enemyCellY)
            }
        };
    }

    private static BattleStartSnapshot BuildDamageBeforeMovementSnapshot()
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_commit_buffer_damage_before_movement",
            BattleId = "battle_commit_buffer_damage_before_movement",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildGroup("group_player_ranged", "player", "player_ranged", "hero_player_ranged", "corps_player_ranged", 0, 0, hitPoints: 40, attackRange: 10),
                BuildGroup("group_enemy_hold", "enemy", "enemy_hold", "hero_enemy_hold", "corps_enemy_hold", 9, 0, hitPoints: 1, initialCommandId: "HoldLine", tacticalMode: BattleGroupTacticalMode.EnemyHoldDefense),
                BuildGroup("group_player_mover", "player", "player_mover", "hero_player_mover", "corps_player_mover", 0, 2, hitPoints: 40),
                BuildGroup("group_enemy_live", "enemy", "enemy_live", "hero_enemy_live", "corps_enemy_live", 4, 2, hitPoints: 40, initialCommandId: "HoldLine")
            }
        };

        AddRectSurfaces(snapshot, 0, 0, 9, 0);
        AddRectSurfaces(snapshot, 0, 2, 4, 2);
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static BattleStartSnapshot BuildMixedImpactBatchSnapshot()
    {
        BattleGroupSnapshot delayed = BuildGroup(
            "group_player_delayed",
            "player",
            "player_delayed",
            "hero_player_delayed",
            "corps_player_delayed",
            0,
            0,
            hitPoints: 30,
            attackDamage: 7);
        delayed.AttackActionSeconds = 1.0;
        delayed.AttackImpactDelaySeconds = 0.4;

        BattleGroupSnapshot instant = BuildGroup(
            "group_player_instant",
            "player",
            "player_instant",
            "hero_player_instant",
            "corps_player_instant",
            1,
            1,
            hitPoints: 30,
            attackDamage: 7);
        instant.AttackActionSeconds = 1.0;
        instant.AttackImpactDelaySeconds = 0;

        BattleGroupSnapshot target = BuildGroup(
            "group_enemy_target",
            "enemy",
            "enemy_target",
            "hero_enemy_target",
            "corps_enemy_target",
            1,
            0,
            hitPoints: 20,
            attackDamage: 1,
            initialCommandId: "HoldLine",
            tacticalMode: BattleGroupTacticalMode.EnemyHoldDefense);

        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_mixed_basic_attack_impacts",
            BattleId = "battle_mixed_basic_attack_impacts",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                delayed,
                instant,
                target
            }
        };
        AddRectSurfaces(snapshot, 0, 0, 1, 1);
        BattleNavigationTestTopology.Compile(snapshot.LocationContext);
        return snapshot;
    }

    private static void AddInterruptingDamageSkill(BattleStartSnapshot snapshot, string skillId, int damage)
    {
        snapshot.SkillDefinitions.Add(new BattleSkillSnapshot
        {
            SkillId = skillId,
            DisplayName = "Impact Boundary Skill",
            TargetingMode = BattleSkillTargetingMode.TargetedActor,
            Range = 8,
            CastSeconds = 0,
            ImpactDelaySeconds = 0,
            RecoverySeconds = 0.2,
            CanInterruptBasicAttackWindup = true,
            Effects =
            {
                new BattleSkillEffectSnapshot
                {
                    Kind = BattleSkillEffectKind.Damage,
                    Amount = damage
                }
            }
        });
    }

    private static BattleGroupSnapshot BuildGroup(
        string groupId,
        string factionId,
        string sourceForceId,
        string heroId,
        string corpsId,
        int cellX,
        int cellY,
        int hitPoints = 100,
        int attackRange = 1,
        int attackDamage = 1,
        string initialCommandId = "",
        BattleGroupTacticalMode tacticalMode = BattleGroupTacticalMode.PlayerCommanded)
    {
        return new BattleGroupSnapshot
        {
            BattleGroupId = groupId,
            FactionId = factionId,
            SourceForceId = sourceForceId,
            HeroId = heroId,
            HeroDefinitionId = $"{heroId}_definition",
            CorpsId = corpsId,
            CorpsDefinitionId = $"{corpsId}_definition",
            CorpsStrength = hitPoints,
            MaxHitPoints = hitPoints,
            AttackDamage = attackDamage,
            AttackRange = attackRange,
            AttackSpeed = 1.0,
            // Legacy commit-order fixtures use instant impact; dedicated Slice F
            // regressions above cover non-zero windup and pause-before-impact.
            AttackImpactDelaySeconds = 0,
            SourceLocationId = factionId == "player" ? "city_1" : "site_1",
            CellX = cellX,
            CellY = cellY,
            InitialCorpsCommandId = initialCommandId,
            TacticalMode = tacticalMode
        };
    }

    private static void AddRectSurfaces(BattleStartSnapshot snapshot, int minX, int minY, int maxX, int maxY)
    {
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                snapshot.LocationContext.NavigationSurfaces.Add(new BattleNavigationSurfaceSnapshot
                {
                    X = x,
                    Y = y,
                    Height = 0,
                    MoveCost = 1
                });
            }
        }
    }

    private static void AssertSourceTypeExists(string root, string typeName, string message)
    {
        bool exists = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .Any(source =>
                source.Contains($"class {typeName}", StringComparison.Ordinal) ||
                source.Contains($"record {typeName}", StringComparison.Ordinal) ||
                source.Contains($"struct {typeName}", StringComparison.Ordinal));
        AssertTrue(exists, message);
    }

    private static int IndexOf(IReadOnlyList<BattleEvent> events, Func<BattleEvent, bool> predicate)
    {
        for (int i = 0; i < events.Count; i++)
        {
            if (predicate(events[i]))
            {
                return i;
            }
        }

        return -1;
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

    private static string ToRepoPath(string root, string path)
    {
        return Path.GetRelativePath(root, path).Replace('\\', '/');
    }

    private static void AssertDoesNotContain(string source, string forbidden, string relativePath, string message)
    {
        AssertTrue(
            !source.Contains(forbidden, StringComparison.Ordinal),
            $"{message}: file={relativePath} forbidden={forbidden}");
    }

    private static void AssertContains(string source, string expected, string relativePath, string message)
    {
        AssertTrue(
            source.Contains(expected, StringComparison.Ordinal),
            $"{message}: file={relativePath} expected={expected}");
    }

    private static void AssertContainsInOrder(string source, string relativePath, string message, params string[] expected)
    {
        int cursor = 0;
        foreach (string token in expected)
        {
            int index = source.IndexOf(token, cursor, StringComparison.Ordinal);
            AssertTrue(index >= 0, $"{message}: file={relativePath} missing={token}");
            cursor = index + token.Length;
        }
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new Exception($"{message}: expected={expected} actual={actual}");
        }
    }

    private static void AssertFloatEqual(double expected, double actual, double tolerance, string message)
    {
        if (Math.Abs(expected - actual) > tolerance)
        {
            throw new Exception($"{message}: expected={expected} actual={actual} tolerance={tolerance}");
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
