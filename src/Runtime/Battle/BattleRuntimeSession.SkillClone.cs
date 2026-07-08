using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Snapshots;

namespace Rpg.Runtime.Battle;

public sealed partial class BattleRuntimeSession
{
    private static List<BattleSkillSnapshot> CloneSkillDefinitions(
        IEnumerable<BattleSkillSnapshot> skillDefinitions)
    {
        // Skill snapshots have already passed the production launch gate. Cloning must
        // preserve authored facts instead of converting malformed content into defaults.
        return (skillDefinitions ?? Enumerable.Empty<BattleSkillSnapshot>())
            .Select(skill =>
            {
                string skillDefinitionId = ResolveSkillDefinitionId(skill);
                BattleSkillSnapshot clone = new()
                {
                    SkillDefinitionId = skillDefinitionId,
                    GrantedSkillId = skill.GrantedSkillId ?? "",
                    LoadoutSlotId = skill.LoadoutSlotId ?? "",
                    OwnerHeroId = skill.OwnerHeroId ?? "",
                    OwnerBattleGroupId = skill.OwnerBattleGroupId ?? "",
                    RuntimeCommanderGroupId = skill.RuntimeCommanderGroupId ?? "",
                    DisplayName = skill.DisplayName ?? "",
                    IconText = skill.IconText ?? "",
                    IconPath = skill.IconPath ?? "",
                    Tags = (skill.Tags ?? new List<string>())
                        .Where(tag => !string.IsNullOrWhiteSpace(tag))
                        .Select(tag => tag.Trim())
                        .Distinct(System.StringComparer.Ordinal)
                        .ToList(),
                    CommandChannel = skill.CommandChannel,
                    SkillType = skill.SkillType,
                    Targeting = CloneTargeting(skill.Targeting),
                    Timing = CloneTiming(skill.Timing),
                    InterruptPolicy = CloneInterruptPolicy(skill.InterruptPolicy),
                    Costs = CloneCosts(skill.Costs),
                    Cooldown = CloneCooldown(skill.Cooldown),
                    Presentation = ClonePresentation(skill.Presentation),
                    TargetingMode = skill.TargetingMode,
                    Range = skill.Range,
                    CasterUnitIds = (skill.CasterUnitIds ?? new List<string>())
                        .Where(unitId => !string.IsNullOrWhiteSpace(unitId))
                        .Select(unitId => unitId.Trim())
                        .Distinct(System.StringComparer.Ordinal)
                        .ToList(),
                    CastSeconds = skill.CastSeconds,
                    ImpactDelaySeconds = skill.ImpactDelaySeconds,
                    RecoverySeconds = skill.RecoverySeconds,
                    HasInterruptPolicy = skill.HasInterruptPolicy,
                    CanInterruptBasicAttackWindup = skill.CanInterruptBasicAttackWindup,
                    CanCancelBasicAttackRecovery = skill.CanCancelBasicAttackRecovery,
                    ReleasesWithoutOccupyingCaster = skill.ReleasesWithoutOccupyingCaster
                };
                clone.Effects.AddRange(CloneEffects(skill.Effects));

                return clone;
            })
            .ToList();
    }

    private static string ResolveSkillDefinitionId(BattleSkillSnapshot skill)
    {
        return skill?.SkillDefinitionId?.Trim() ?? "";
    }

    private static BattleSkillTargetingSnapshot CloneTargeting(BattleSkillTargetingSnapshot source)
    {
        return source == null
            ? new BattleSkillTargetingSnapshot()
            : new BattleSkillTargetingSnapshot
            {
                InputFlow = source.InputFlow,
                TargetKind = source.TargetKind,
                Range = source.Range,
                RangeMetric = source.RangeMetric,
                AreaShape = source.AreaShape,
                AreaRadius = source.AreaRadius,
                DirectionMode = source.DirectionMode,
                RequiresSelectedMark = source.RequiresSelectedMark,
                RequiredMarkKind = source.RequiredMarkKind,
                LandingRadius = source.LandingRadius,
                PreviewProfileId = source.PreviewProfileId ?? ""
            };
    }

    private static BattleSkillTimingSnapshot CloneTiming(BattleSkillTimingSnapshot source)
    {
        return source == null
            ? new BattleSkillTimingSnapshot()
            : new BattleSkillTimingSnapshot
            {
                CastSeconds = source.CastSeconds,
                ImpactDelaySeconds = source.ImpactDelaySeconds,
                RecoverySeconds = source.RecoverySeconds
            };
    }

    private static BattleSkillInterruptPolicySnapshot CloneInterruptPolicy(BattleSkillInterruptPolicySnapshot source)
    {
        return source == null
            ? new BattleSkillInterruptPolicySnapshot()
            : new BattleSkillInterruptPolicySnapshot
            {
                CanInterruptBasicAttackWindup = source.CanInterruptBasicAttackWindup,
                CanCancelBasicAttackRecovery = source.CanCancelBasicAttackRecovery,
                ReleasesWithoutOccupyingCaster = source.ReleasesWithoutOccupyingCaster,
                CanInterruptActiveChannel = source.CanInterruptActiveChannel
            };
    }

    private static BattleSkillPresentationSnapshot ClonePresentation(BattleSkillPresentationSnapshot source)
    {
        return source == null
            ? new BattleSkillPresentationSnapshot()
            : new BattleSkillPresentationSnapshot
            {
                ProfileId = source.ProfileId ?? "",
                CastFxProfileId = source.CastFxProfileId ?? "",
                ImpactFxProfileId = source.ImpactFxProfileId ?? "",
                MarkFxProfileId = source.MarkFxProfileId ?? "",
                AreaFxProfileId = source.AreaFxProfileId ?? "",
                SuppressActorCastFx = source.SuppressActorCastFx,
                HoldCastAnimationDuringAction = source.HoldCastAnimationDuringAction
            };
    }

    private static List<BattleSkillCostSnapshot> CloneCosts(IEnumerable<BattleSkillCostSnapshot> costs)
    {
        return (costs ?? Enumerable.Empty<BattleSkillCostSnapshot>())
            .Select(CloneCost)
            .Where(item => item != null)
            .ToList();
    }

    private static BattleSkillCostSnapshot CloneCost(BattleSkillCostSnapshot cost)
    {
        return cost switch
        {
            ManaCostSkillCostSnapshot mana => new ManaCostSkillCostSnapshot
            {
                PoolId = mana.PoolId ?? "",
                Amount = mana.Amount,
                PayTiming = mana.PayTiming,
                RefundPolicy = mana.RefundPolicy
            },
            LimitedUseSkillCostSnapshot limitedUse => new LimitedUseSkillCostSnapshot
            {
                MaxUses = limitedUse.MaxUses,
                ConsumeTiming = limitedUse.ConsumeTiming,
                RefundPolicy = limitedUse.RefundPolicy
            },
            NoCostSkillCostSnapshot => new NoCostSkillCostSnapshot(),
            null => null,
            _ => null
        };
    }

    private static BattleSkillCooldownSnapshot CloneCooldown(BattleSkillCooldownSnapshot cooldown)
    {
        return cooldown switch
        {
            PerGrantCooldownSkillCooldownSnapshot perGrant => new PerGrantCooldownSkillCooldownSnapshot
            {
                DurationSeconds = perGrant.DurationSeconds,
                StartsOn = perGrant.StartsOn,
                SharedCooldownGroupId = perGrant.SharedCooldownGroupId ?? ""
            },
            ChargeCooldownSkillCooldownSnapshot charge => new ChargeCooldownSkillCooldownSnapshot
            {
                MaxCharges = charge.MaxCharges,
                RechargeSeconds = charge.RechargeSeconds,
                StartsFull = charge.StartsFull
            },
            NoCooldownSkillCooldownSnapshot => new NoCooldownSkillCooldownSnapshot(),
            _ => new NoCooldownSkillCooldownSnapshot()
        };
    }

    private static List<BattleSkillEffectSnapshot> CloneEffects(IEnumerable<BattleSkillEffectSnapshot> effects)
    {
        return (effects ?? Enumerable.Empty<BattleSkillEffectSnapshot>())
            .Select(CloneEffect)
            .Where(item => item != null)
            .ToList();
    }

    private static BattleSkillEffectSnapshot CloneEffect(BattleSkillEffectSnapshot effect)
    {
        BattleSkillEffectSnapshot clone = effect switch
        {
            DamageSkillEffectSnapshot damage => new DamageSkillEffectSnapshot
            {
                BaseDamage = damage.BaseDamage,
                DamageType = damage.DamageType,
                CanHitActors = damage.CanHitActors,
                CanHitWorldObjects = damage.CanHitWorldObjects
            },
            CreateMarkSkillEffectSnapshot mark => new CreateMarkSkillEffectSnapshot
            {
                MarkKind = mark.MarkKind,
                LifetimeSeconds = mark.LifetimeSeconds,
                AttachToActorWhenTargeted = mark.AttachToActorWhenTargeted,
                ReplaceExistingOwnedMark = mark.ReplaceExistingOwnedMark
            },
            TeleportToMarkSkillEffectSnapshot teleport => new TeleportToMarkSkillEffectSnapshot
            {
                RequiredMarkKind = teleport.RequiredMarkKind,
                LandingRadius = teleport.LandingRadius,
                ConsumesMark = teleport.ConsumesMark
            },
            ChanneledAreaDamageSkillEffectSnapshot channel => new ChanneledAreaDamageSkillEffectSnapshot
            {
                BaseDamage = channel.BaseDamage,
                DamageType = channel.DamageType,
                DurationSeconds = channel.DurationSeconds,
                TickIntervalSeconds = channel.TickIntervalSeconds,
                AreaShape = channel.AreaShape,
                Radius = channel.Radius,
                FollowsCaster = channel.FollowsCaster,
                UsesTargetOffset = channel.UsesTargetOffset
            },
            _ => null
        };
        if (clone != null)
        {
            clone.EffectInstancePolicy = effect.EffectInstancePolicy;
            clone.PresentationProfileId = effect.PresentationProfileId ?? "";
        }

        return clone;
    }
}
