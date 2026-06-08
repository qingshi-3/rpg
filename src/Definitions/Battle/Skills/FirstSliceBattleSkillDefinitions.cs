using System.Collections.Generic;
using Rpg.Application.Battle.Commands;

namespace Rpg.Definitions.Battle.Skills;

public static class FirstSliceBattleSkillDefinitions
{
    public static IReadOnlyList<BattleSkillDefinition> CreateSelectedHeroSkills()
    {
        return new[]
        {
            new BattleSkillDefinition
            {
                SkillId = HeroSkillCommandIds.ShieldBarrierSkillId,
                DisplayName = "曦盾结界",
                CasterUnitIds = { "f1_grandmasterzir" },
                TargetingMode = BattleSkillTargetingMode.TargetedActor,
                Range = 8,
                Timing = new BattleSkillActionTimingDefinition
                {
                    CastSeconds = 0,
                    ImpactDelaySeconds = 0,
                    RecoverySeconds = 0.2
                },
                InterruptPolicy = new BattleSkillInterruptPolicyDefinition
                {
                    CanInterruptBasicAttackWindup = true,
                    CanCancelBasicAttackRecovery = false
                },
                Effects =
                {
                    new BattleSkillEffectDefinition
                    {
                        Kind = BattleSkillEffectKind.Damage,
                        Amount = 12
                    }
                }
            },
            new BattleSkillDefinition
            {
                SkillId = HeroSkillCommandIds.SunPiercerSkillId,
                DisplayName = "贯日一击",
                CasterUnitIds = { "f1_windbladecommander" },
                TargetingMode = BattleSkillTargetingMode.TargetedActor,
                Range = 8,
                Timing = new BattleSkillActionTimingDefinition
                {
                    CastSeconds = 0,
                    ImpactDelaySeconds = 0,
                    RecoverySeconds = 0.2
                },
                InterruptPolicy = new BattleSkillInterruptPolicyDefinition
                {
                    CanInterruptBasicAttackWindup = true,
                    CanCancelBasicAttackRecovery = false
                },
                Effects =
                {
                    new BattleSkillEffectDefinition
                    {
                        Kind = BattleSkillEffectKind.Damage,
                        Amount = 18
                    }
                }
            },
            new BattleSkillDefinition
            {
                SkillId = HeroSkillCommandIds.WhirlingBreakSkillId,
                DisplayName = "回旋破阵",
                CasterUnitIds = { "f1_elyxstormblade" },
                TargetingMode = BattleSkillTargetingMode.TargetedActor,
                Range = 8,
                Timing = new BattleSkillActionTimingDefinition
                {
                    CastSeconds = 0,
                    ImpactDelaySeconds = 0,
                    RecoverySeconds = 0.2
                },
                InterruptPolicy = new BattleSkillInterruptPolicyDefinition
                {
                    CanInterruptBasicAttackWindup = true,
                    CanCancelBasicAttackRecovery = false
                },
                Effects =
                {
                    new BattleSkillEffectDefinition
                    {
                        Kind = BattleSkillEffectKind.Damage,
                        Amount = 16
                    }
                }
            }
        };
    }
}
