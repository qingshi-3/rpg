using System;
using System.Threading.Tasks;
using Godot;
using Rpg.Definitions.Battle.Audio;
using Rpg.Infrastructure.Logging;

namespace Rpg.Presentation.Battle.Entities;

public partial class DamageReactionComponent : BattleEntityComponent
{
    [Export]
    public bool FaceDamageSource { get; set; } = true;

    [Export]
    public bool AlignHitWithSourceAttack { get; set; } = true;

    [Export]
    public double FallbackHitDelaySeconds { get; set; }

    private HealthComponent _health;
    private UnitAnimationComponent _animation;
    private bool _hasPendingDefeatedPresentationTiming;
    private double _pendingDefeatedDelaySeconds;
    private double _pendingDefeatedMinimumDurationSeconds;

    protected override void OnAttached()
    {
        _health = RequireComponent<HealthComponent>();
        _animation = RequireComponent<UnitAnimationComponent>();
        _health.Damaged += OnDamaged;
        _health.Defeated += OnDefeated;

        GameLog.Trace(
            nameof(DamageReactionComponent),
            $"Attached entity={Entity?.EntityId} health={_health != null} animation={_animation != null}");
    }

    public override void _ExitTree()
    {
        if (_health != null)
        {
            _health.Damaged -= OnDamaged;
            _health.Defeated -= OnDefeated;
        }

        _health = null;
        _animation = null;
    }

    public bool TryConsumeDefeatedPresentationTiming(
        out double delaySeconds,
        out double minimumDurationSeconds)
    {
        delaySeconds = _pendingDefeatedDelaySeconds;
        minimumDurationSeconds = _pendingDefeatedMinimumDurationSeconds;

        bool hasTiming = _hasPendingDefeatedPresentationTiming;
        _hasPendingDefeatedPresentationTiming = false;
        _pendingDefeatedDelaySeconds = 0;
        _pendingDefeatedMinimumDurationSeconds = 0;
        return hasTiming;
    }

    private T RequireComponent<T>() where T : BattleEntityComponent
    {
        T component = Entity?.GetComponent<T>();
        if (component != null)
        {
            return component;
        }

        string message = $"{nameof(DamageReactionComponent)} requires {typeof(T).Name} entity={Entity?.EntityId} path={GetPath()}";
        GameLog.Error(nameof(DamageReactionComponent), message);
        GD.PushError(message);
        throw new InvalidOperationException(message);
    }

    private void OnDamaged(HealthDamageEvent damage)
    {
        if (damage.DamageApplied <= 0 || damage.Target != Entity || damage.HpAfter <= 0)
        {
            return;
        }

        double delaySeconds = ResolveHitDelaySeconds(damage);
        if (delaySeconds <= 0 || !IsInsideTree())
        {
            PlayDamageReaction(damage, delaySeconds);
            return;
        }

        _ = PlayDamageReactionAfterDelay(damage, delaySeconds);
    }

    private void OnDefeated(HealthDamageEvent damage)
    {
        if (damage.DamageApplied <= 0 || damage.Target != Entity)
        {
            return;
        }

        _pendingDefeatedDelaySeconds = ResolveHitDelaySeconds(damage);
        _pendingDefeatedMinimumDurationSeconds = ResolveMinimumDefeatedDurationSeconds(damage);
        _hasPendingDefeatedPresentationTiming = true;
        GameLog.Trace(
            nameof(DamageReactionComponent),
            $"Defeated presentation timing prepared target={Entity?.EntityId} source={damage.Source?.EntityId} delay={_pendingDefeatedDelaySeconds:0.00} minDuration={_pendingDefeatedMinimumDurationSeconds:0.00}");
    }

    private double ResolveHitDelaySeconds(HealthDamageEvent damage)
    {
        if (!AlignHitWithSourceAttack ||
            damage.Source == null ||
            !GodotObject.IsInstanceValid(damage.Source))
        {
            return System.Math.Max(0, FallbackHitDelaySeconds);
        }

        UnitAnimationComponent sourceAnimation = damage.Source.GetComponent<UnitAnimationComponent>();
        double sourceImpactDelay = sourceAnimation?.ResolveAttackImpactDelaySeconds() ?? 0;
        return sourceImpactDelay > 0
            ? sourceImpactDelay
            : System.Math.Max(0, FallbackHitDelaySeconds);
    }

    private double ResolveMinimumDefeatedDurationSeconds(HealthDamageEvent damage)
    {
        if (!AlignHitWithSourceAttack ||
            damage.Source == null ||
            !GodotObject.IsInstanceValid(damage.Source))
        {
            return 0;
        }

        UnitAnimationComponent sourceAnimation = damage.Source.GetComponent<UnitAnimationComponent>();
        double attackDurationSeconds = sourceAnimation?.ResolveAttackDurationSeconds() ?? 0;
        return _animation.ResolveMinimumDefeatedDurationSeconds(attackDurationSeconds);
    }

    private async Task PlayDamageReactionAfterDelay(HealthDamageEvent damage, double delaySeconds)
    {
        await ToSignal(GetTree().CreateTimer(delaySeconds), SceneTreeTimer.SignalName.Timeout);
        PlayDamageReaction(damage, delaySeconds);
    }

    private void PlayDamageReaction(HealthDamageEvent damage, double delaySeconds)
    {
        if (Entity == null ||
            !GodotObject.IsInstanceValid(Entity) ||
            damage.Target != Entity ||
            _health == null ||
            _health.IsDead)
        {
            return;
        }

        if (FaceDamageSource &&
            damage.Source != null &&
            GodotObject.IsInstanceValid(damage.Source))
        {
            _animation.FaceToward(damage.Source.GlobalPosition);
        }

        Entity.GetComponent<BattleUnitAudioComponent>()?.PlayCue(BattleUnitAudioCue.Hit);
        GameLog.Trace(
            nameof(DamageReactionComponent),
            $"Damage feedback played target={Entity?.EntityId} source={damage.Source?.EntityId} damage={damage.DamageApplied} hp={damage.HpBefore}->{damage.HpAfter} delay={delaySeconds:0.00}");
    }
}
