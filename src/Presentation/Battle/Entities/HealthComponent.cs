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

    public int ApplyDamage(int amount, BattleEntity source = null)
    {
        int damage = System.Math.Max(0, amount);
        int previousHp = Hp;
        Hp = System.Math.Clamp(Hp - damage, 0, MaxHp);
        int damageApplied = previousHp - Hp;
        if (damageApplied <= 0)
        {
            return 0;
        }

        var damageEvent = new HealthDamageEvent(Entity, source, damageApplied, previousHp, Hp);
        Damaged?.Invoke(damageEvent);
        if (previousHp > 0 && Hp <= 0)
        {
            Defeated?.Invoke(damageEvent);
        }

        GameLog.Info(
            nameof(HealthComponent),
            $"Damage applied target={Entity?.EntityId} source={source?.EntityId} damage={damageApplied} hp={previousHp}->{Hp}");
        return damageApplied;
    }

    public void Heal(int amount)
    {
        int healing = System.Math.Max(0, amount);
        Hp = System.Math.Clamp(Hp + healing, 0, MaxHp);
    }
}
