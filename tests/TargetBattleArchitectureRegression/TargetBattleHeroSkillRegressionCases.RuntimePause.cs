using System.Linq;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;

internal static partial class TargetBattleHeroSkillRegressionCases
{
    internal static void RuntimePauseBlocksFixedTickTimeAndCombatEffects()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_runtime_pause_clock", enemyStrength: 40);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        BattleRuntimeCommandSubmitResult submit = SubmitTargetedSkill(
            controller,
            "battle_runtime_pause_clock",
            "cmd_runtime_pause_clock",
            EnemyActorId);
        AssertTrue(submit.Accepted, "pause clock setup skill command should be accepted");

        controller.SetPaused(true, "test_pause");
        BattleRuntimeAdvanceResult pausedAdvance = controller.AdvanceFixedTick(0.2);

        AssertEqual(0d, controller.CurrentTimeSeconds, "paused fixed tick should not advance controller time");
        AssertEqual(0d, pausedAdvance.RuntimeTimeSeconds, "paused fixed tick result should report frozen runtime time");
        AssertEqual(40, EnemyCorps(controller).HitPoints, "paused fixed tick should not apply queued skill damage");
        AssertTrue(
            pausedAdvance.Events.All(item => item.Kind != BattleEventKind.SkillUsed && item.Kind != BattleEventKind.DamageApplied),
            "paused fixed tick should not release skill or apply combat effects");

        controller.SetPaused(false, "test_resume");
        BattleRuntimeAdvanceResult resumedAdvance = controller.AdvanceFixedTick(0.2);

        AssertTrue(
            resumedAdvance.Events.Any(item =>
                item.Kind == BattleEventKind.SkillUsed &&
                item.SourceCommandId == "cmd_runtime_pause_clock"),
            "resumed runtime tick should release the queued skill");
        AssertEffectDamage(resumedAdvance, "cmd_runtime_pause_clock", 18, "resumed skill damage");
        AssertEqual(0.2d, controller.CurrentTimeSeconds, "resumed fixed tick should advance runtime time after resolving the slice");
    }

    internal static void RuntimePauseAcceptsHeroSkillIntentWithoutAdvancingBattlefieldFacts()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_runtime_pause_accepts_intent", enemyStrength: 40);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);

        controller.SetPaused(true, "test_pause");
        BattleRuntimeCommandSubmitResult submit = SubmitTargetedSkill(
            controller,
            "battle_runtime_pause_accepts_intent",
            "cmd_runtime_pause_accepts_intent",
            EnemyActorId);

        AssertTrue(submit.Accepted, "commands submitted during runtime pause should still accept or reject as command facts");
        AssertTrue(
            submit.Events.Any(item =>
                item.Kind == BattleEventKind.CommandAccepted &&
                item.SourceCommandId == "cmd_runtime_pause_accepts_intent" &&
                item.RuntimeTimeSeconds == 0),
            "accepted pause command should be stamped at frozen runtime time");
        AssertTrue(
            submit.Events.All(item => item.Kind != BattleEventKind.SkillUsed && item.Kind != BattleEventKind.DamageApplied),
            "paused command submission must not release skills or mutate combat effects");
        AssertEqual(40, EnemyCorps(controller).HitPoints, "enemy HP should stay unchanged after pause command acceptance");
    }

    internal static void RuntimePauseBlocksAdvanceNextTickTermination()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_runtime_pause_termination", enemyStrength: 40);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        EnemyCorps(controller).HitPoints = 0;

        controller.SetPaused(true, "test_pause");
        BattleRuntimeAdvanceResult pausedAdvance = controller.AdvanceNextTick();

        AssertTrue(!controller.IsComplete, "paused advance should not complete battle even when termination is already true");
        AssertTrue(!pausedAdvance.IsComplete, "paused advance result should remain incomplete");
        AssertTrue(
            pausedAdvance.Events.All(item => item.Kind != BattleEventKind.BattleEnded),
            "paused advance should not emit battle ended");
        AssertEqual(0d, controller.CurrentTimeSeconds, "paused next-tick advance should keep runtime time frozen");

        controller.SetPaused(false, "test_resume");
        BattleRuntimeAdvanceResult resumedAdvance = controller.AdvanceNextTick();

        AssertTrue(controller.IsComplete, "resumed advance should allow runtime termination to settle");
        AssertTrue(resumedAdvance.Events.Any(item => item.Kind == BattleEventKind.BattleEnded), "resumed advance should emit battle ended");
    }

    internal static void RuntimePausedAdvanceToCompletionReturnsIncompleteResult()
    {
        BattleStartSnapshot snapshot = BuildOpposedSnapshot("battle_runtime_pause_completion", enemyStrength: 40);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);

        controller.SetPaused(true, "test_pause");
        BattleRuntimeSessionResult pausedResult = controller.AdvanceToCompletion();

        AssertTrue(!controller.IsComplete, "paused advance-to-completion should not spin, complete, or mutate battle state");
        AssertTrue(!pausedResult.Outcome.IsComplete, "paused advance-to-completion should return the current incomplete outcome");
        AssertTrue(
            pausedResult.EventStream.Events.All(item => item.Kind != BattleEventKind.BattleEnded),
            "paused advance-to-completion should not emit battle ended");
        AssertEqual(0d, controller.CurrentTimeSeconds, "paused advance-to-completion should keep runtime time frozen");

        controller.SetPaused(false, "test_resume");
        BattleRuntimeSessionResult resumedResult = controller.AdvanceToCompletion();

        AssertTrue(controller.IsComplete, "resumed advance-to-completion should complete normally");
        AssertTrue(resumedResult.EventStream.Events.Any(item => item.Kind == BattleEventKind.BattleEnded), "resumed advance-to-completion should emit battle ended");
    }
}
