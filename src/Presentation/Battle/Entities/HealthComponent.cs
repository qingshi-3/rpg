using Godot;

namespace Rpg.Presentation.Battle.Entities;

public partial class HealthComponent : BattleEntityComponent
{
    [Export]
    public int MaxHp { get; set; } = 1;

    [Export]
    public int Hp { get; set; } = 1;

    public bool IsDead => Hp <= 0;

    public int ApplyDamage(int amount)
    {
        int damage = System.Math.Max(0, amount);
        int previousHp = Hp;
        Hp = System.Math.Clamp(Hp - damage, 0, MaxHp);
        return previousHp - Hp;
    }

    public void Heal(int amount)
    {
        int healing = System.Math.Max(0, amount);
        Hp = System.Math.Clamp(Hp + healing, 0, MaxHp);
    }
}
