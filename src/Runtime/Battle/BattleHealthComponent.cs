using Rpg.Runtime.Battle.Effects;

namespace Rpg.Runtime.Battle;

internal sealed class BattleHealthComponent
{
    private readonly BattleRuntimeActor _actor;

    internal BattleHealthComponent(BattleRuntimeActor actor)
    {
        _actor = actor ?? throw new System.ArgumentNullException(nameof(actor));
    }

    internal BattleRuntimeActor Actor => _actor;

    internal void RequestEffectDamage(
        BattleCommitBuffer commitBuffer,
        BattleEffectExecutionContext context,
        int amount,
        string effectKind)
    {
        commitBuffer?.RequestEffectDamage(context, this, amount, effectKind);
    }

    internal EffectDamageCommitResult CommitEffectDamage(int amount)
    {
        int normalizedAmount = System.Math.Max(0, amount);
        HitPointCommitResult commit = CommitHitPointChange(System.Math.Max(0, _actor.HitPoints) - normalizedAmount);

        return new EffectDamageCommitResult(normalizedAmount, commit.TransitionedToDefeated);
    }

    internal BasicAttackDamageCommitResult CommitBasicAttackDamage(int remainingHitPoints)
    {
        HitPointCommitResult commit = CommitHitPointChange(remainingHitPoints);

        return new BasicAttackDamageCommitResult(commit.RemainingHitPoints, commit.TransitionedToDefeated);
    }

    private HitPointCommitResult CommitHitPointChange(int remainingHitPoints)
    {
        int before = System.Math.Max(0, _actor.HitPoints);
        int after = System.Math.Max(0, remainingHitPoints);
        _actor.HitPoints = after;

        bool transitionedToDefeated = before > 0 && after <= 0;
        if (transitionedToDefeated && _actor.Phase != BattleRuntimeActorPhase.Defeated)
        {
            BattleRuntimeActorStateMachine.MarkDefeated(_actor);
        }

        return new HitPointCommitResult(after, transitionedToDefeated);
    }

    internal readonly record struct EffectDamageCommitResult(int DamageAmount, bool TransitionedToDefeated);

    internal readonly record struct BasicAttackDamageCommitResult(int RemainingHitPoints, bool TransitionedToDefeated);

    private readonly record struct HitPointCommitResult(int RemainingHitPoints, bool TransitionedToDefeated);
}
