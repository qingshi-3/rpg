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
                SkillId = HeroSkillCommandIds.ThunderTagThrowSkillId,
                DisplayName = "雷签飞投",
                CasterUnitIds = { "f1_elyxstormblade" },
                TargetingMode = BattleSkillTargetingMode.TargetedActorOrCell,
                Range = 8,
                Timing = new BattleSkillActionTimingDefinition
                {
                    CastSeconds = 0,
                    ImpactDelaySeconds = 0,
                    RecoverySeconds = 0
                },
                InterruptPolicy = new BattleSkillInterruptPolicyDefinition
                {
                    CanInterruptBasicAttackWindup = true,
                    CanCancelBasicAttackRecovery = true,
                    ReleasesWithoutOccupyingCaster = true
                },
                Effects =
                {
                    new BattleSkillEffectDefinition
                    {
                        Kind = BattleSkillEffectKind.Damage,
                        Amount = 12
                    },
                    new BattleSkillEffectDefinition
                    {
                        Kind = BattleSkillEffectKind.CreateThunderMark,
                        Amount = 1
                    }
                }
            },
            new BattleSkillDefinition
            {
                SkillId = HeroSkillCommandIds.ThunderMarkFoldSkillId,
                DisplayName = "雷印折跃",
                CasterUnitIds = { "f1_elyxstormblade" },
                TargetingMode = BattleSkillTargetingMode.TargetedCell,
                Range = 8,
                Timing = new BattleSkillActionTimingDefinition
                {
                    CastSeconds = 0,
                    ImpactDelaySeconds = 0,
                    RecoverySeconds = 0
                },
                InterruptPolicy = new BattleSkillInterruptPolicyDefinition
                {
                    CanInterruptBasicAttackWindup = true,
                    CanCancelBasicAttackRecovery = true
                },
                Effects =
                {
                    new BattleSkillEffectDefinition
                    {
                        Kind = BattleSkillEffectKind.TeleportToThunderMark,
                        Amount = 3
                    }
                }
            },
            new BattleSkillDefinition
            {
                SkillId = HeroSkillCommandIds.ThunderSpiralBreakSkillId,
                DisplayName = "雷旋破",
                CasterUnitIds = { "f1_elyxstormblade" },
                TargetingMode = BattleSkillTargetingMode.None,
                Range = 0,
                Timing = new BattleSkillActionTimingDefinition
                {
                    CastSeconds = 0,
                    ImpactDelaySeconds = 0,
                    RecoverySeconds = 0
                },
                InterruptPolicy = new BattleSkillInterruptPolicyDefinition
                {
                    CanInterruptBasicAttackWindup = true,
                    CanCancelBasicAttackRecovery = true
                },
                Effects =
                {
                    new BattleSkillEffectDefinition
                    {
                        Kind = BattleSkillEffectKind.StartChanneledAreaDamage,
                        Amount = 14,
                        DurationSeconds = 0.8,
                        TickIntervalSeconds = 0.2,
                        Radius = 1
                    }
                }
            }
        };
    }
}
