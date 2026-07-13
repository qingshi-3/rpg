using System.Linq;
using Rpg.Application.Battle.Commands;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;

internal static partial class TargetBattleHeroSkillRegressionCases
{
    private const string SecondaryEnemyActorId = "force_enemy_second:1";

    private static void RuntimeIdleCasterCanRetargetSamePendingSkillIntent()
    {
        const string battleId = "battle_hero_skill_same_pending_retarget";
        BattleStartSnapshot snapshot = BuildOpposedSnapshot(battleId, enemyStrength: 60);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        AddSecondaryEnemy(controller);

        BattleRuntimeCommandSubmitResult first = SubmitTargetedSkill(
            controller,
            battleId,
            "cmd_same_pending_first",
            EnemyActorId);
        BattleRuntimeCommandSubmitResult latest = SubmitTargetedSkill(
            controller,
            battleId,
            "cmd_same_pending_latest",
            SecondaryEnemyActorId);

        AssertTrue(first.Accepted, "initial same-skill pending intent should be accepted");
        AssertTrue(latest.Accepted, "idle caster should accept same-skill pending intent retarget");
        AssertTrue(
            latest.Events.Any(item =>
                item.Kind == BattleEventKind.CommandInterrupted &&
                item.SourceCommandId == "cmd_same_pending_first" &&
                item.SourceActionId == "cmd_same_pending_latest" &&
                item.ReasonCode == "skill_intent_superseded"),
            "same-skill retarget should explicitly supersede the older unstarted pending command");

        BattleRuntimeAdvanceResult advance = controller.AdvanceFixedTick();

        AssertTrue(
            advance.Events.All(item =>
                item.SourceCommandId != "cmd_same_pending_first" ||
                item.Kind is not (BattleEventKind.SkillUsed or BattleEventKind.DamageApplied)),
            "older same-skill pending command must not release after retarget supersession");
        AssertTrue(
            advance.Events.Any(item =>
                item.Kind == BattleEventKind.DamageApplied &&
                item.SourceCommandId == "cmd_same_pending_latest" &&
                item.TargetId == SecondaryEnemyActorId &&
                item.CorpsStrengthDelta == -18),
            "latest same-skill pending command should release against the new locked target");
        AssertEqual(60, EnemyCorps(controller).HitPoints, "original target should not be damaged by the superseded command");
        AssertEqual(22, SecondaryEnemyCorps(controller).HitPoints, "new target should receive the retargeted skill damage");
    }

    private static void AddSecondaryEnemy(BattleRuntimeSessionController controller)
    {
        controller.State.Actors.Add(new BattleRuntimeActor
        {
            ActorId = SecondaryEnemyActorId,
            BattleGroupId = "group_enemy",
            FactionId = "enemy",
            SourceForceId = "force_enemy_second",
            SourceStateId = "corps_enemy_second",
            Kind = BattleRuntimeActorKind.Corps,
            HitPoints = 40,
            GridX = 7,
            GridY = 0,
            GridHeight = 0,
            Position = 7
        });
    }

    private static BattleRuntimeActor SecondaryEnemyCorps(BattleRuntimeSessionController controller)
    {
        return controller.State.Actors.Single(item => item.ActorId == SecondaryEnemyActorId);
    }
}
