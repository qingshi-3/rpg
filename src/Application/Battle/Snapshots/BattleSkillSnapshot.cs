using System.Collections.Generic;

namespace Rpg.Application.Battle.Snapshots;

public sealed class BattleSkillSnapshot
{
    public string SkillId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public BattleSkillTargetingMode TargetingMode { get; set; } = BattleSkillTargetingMode.None;
    public int Range { get; set; }
    public List<string> CasterUnitIds { get; set; } = new();
    public double CastSeconds { get; set; }
    public double ImpactDelaySeconds { get; set; }
    public double RecoverySeconds { get; set; }
    public bool CanInterruptBasicAttackWindup { get; set; }
    public bool CanCancelBasicAttackRecovery { get; set; }
    public bool ReleasesWithoutOccupyingCaster { get; set; }
    public List<BattleSkillEffectSnapshot> Effects { get; set; } = new();
}
