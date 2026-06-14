using Rpg.Application.Battle.Snapshots;

namespace Rpg.Runtime.Battle.Effects;

internal sealed class BattleEffectPayload
{
    public BattleSkillEffectKind EffectKind { get; set; } = BattleSkillEffectKind.Damage;
    public int Amount { get; set; }
    public double DurationSeconds { get; set; }
    public double TickIntervalSeconds { get; set; }
    public int Radius { get; set; }
}
