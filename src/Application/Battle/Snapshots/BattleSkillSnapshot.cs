using System.Collections.Generic;

namespace Rpg.Application.Battle.Snapshots;

public sealed class BattleSkillSnapshot
{
    public string SkillDefinitionId { get; set; } = "";
    public string GrantedSkillId { get; set; } = "";
    public string LoadoutSlotId { get; set; } = "";
    public string OwnerHeroId { get; set; } = "";
    public string OwnerBattleGroupId { get; set; } = "";
    public string RuntimeCommanderGroupId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string IconText { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public BattleSkillCommandChannel CommandChannel { get; set; } = BattleSkillCommandChannel.Hero;
    public BattleSkillType SkillType { get; set; } = BattleSkillType.Active;
    public BattleSkillTargetingSnapshot Targeting { get; set; } = new();
    public BattleSkillTimingSnapshot Timing { get; set; } = new();
    public BattleSkillInterruptPolicySnapshot InterruptPolicy { get; set; } = new();
    public List<BattleSkillCostSnapshot> Costs { get; set; } = new();
    public BattleSkillCooldownSnapshot Cooldown { get; set; } = new NoCooldownSkillCooldownSnapshot();
    public BattleSkillPresentationSnapshot Presentation { get; set; } = new();
    public BattleSkillTargetingMode TargetingMode { get; set; } = BattleSkillTargetingMode.None;
    public int Range { get; set; }
    public List<string> CasterUnitIds { get; set; } = new();
    public double CastSeconds { get; set; }
    public double ImpactDelaySeconds { get; set; }
    public double RecoverySeconds { get; set; }
    public bool HasInterruptPolicy { get; set; }
    public bool CanInterruptBasicAttackWindup { get; set; }
    public bool CanCancelBasicAttackRecovery { get; set; }
    public bool ReleasesWithoutOccupyingCaster { get; set; }
    public List<BattleSkillEffectSnapshot> Effects { get; set; } = new();
}
