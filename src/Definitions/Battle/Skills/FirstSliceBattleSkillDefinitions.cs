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
                SkillId = HeroSkillCommandIds.FirstSliceHeroSkillId,
                DisplayName = "破阵",
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
            }
        };
    }
}
