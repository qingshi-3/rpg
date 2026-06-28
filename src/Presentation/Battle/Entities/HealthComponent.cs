using Godot;
using Rpg.Infrastructure.Logging;

namespace Rpg.Presentation.Battle.Entities;

public sealed record HealthDamageEvent(
    BattleEntity Target,
    BattleEntity Source,
    int DamageApplied,
    int HpBefore,
    int HpAfter);

public partial class HealthComponent : BattleEntityComponent
{
    [Export]
    public int MaxHp { get; set; } = 1;

    [Export]
    public int Hp { get; set; } = 1;

    public bool IsDead => Hp <= 0;

    public event System.Action<HealthDamageEvent> Damaged;

    public event System.Action<HealthDamageEvent> Defeated;

    public event System.Action HealthChanged;

    public int MirrorRuntimeDamage(BattleEntity source, int runtimeDamageApplied, int runtimeHpBefore, int runtimeHpAfter)
    {
        int damageApplied = System.Math.Max(0, runtimeDamageApplied);
        int hpBefore = System.Math.Max(0, runtimeHpBefore);
        int hpAfter = System.Math.Clamp(runtimeHpAfter, 0, MaxHp);
        int presentationHpBefore = Hp;
        Hp = hpAfter;

        if (presentationHpBefore != hpBefore)
        {
            GameLog.Warn(
                nameof(HealthComponent),
                $"Runtime health mirror corrected presentation drift target={Entity?.EntityId} source={source?.EntityId} presentationHp={presentationHpBefore} runtimeHpBefore={hpBefore} runtimeHpAfter={hpAfter}");
        }

        if (presentationHpBefore != Hp)
        {
            HealthChanged?.Invoke();
        }

        if (damageApplied <= 0)
        {
            return 0;
        }

        // Presentation mirrors Runtime damage facts verbatim. It must not
        // recompute applied damage from local HP because Runtime is the combat authority.
        var damageEvent = new HealthDamageEvent(Entity, source, damageApplied, hpBefore, hpAfter);
        Damaged?.Invoke(damageEvent);
        if (hpBefore > 0 && hpAfter <= 0)
        {
            Defeated?.Invoke(damageEvent);
        }

        GameLog.Trace(
            nameof(HealthComponent),
            $"Runtime damage mirrored target={Entity?.EntityId} source={source?.EntityId} damage={damageApplied} hp={hpBefore}->{hpAfter}");
        return damageApplied;
    }

    public void Heal(int amount)
    {
        int healing = System.Math.Max(0, amount);
        int previousHp = Hp;
        Hp = System.Math.Clamp(Hp + healing, 0, MaxHp);
        if (Hp != previousHp)
        {
            HealthChanged?.Invoke();
        }
    }
}
