using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Definitions.Battle.Skills;

namespace Rpg.Application.Battle.Snapshots;

public sealed class BattleSkillSnapshotCompiler
{
    public IReadOnlyList<BattleSkillSnapshot> CompileTemplates(
        IReadOnlyDictionary<string, BattleSkillSnapshot> templatesById,
        IEnumerable<BattleGroupSnapshot> participatingGroups,
        IEnumerable<BattleSkillGrantSnapshot> grants)
    {
        Dictionary<string, BattleGroupSnapshot> groupsById = BuildGroupLookup(participatingGroups);

        if (templatesById == null || templatesById.Count == 0)
        {
            throw new InvalidOperationException("battle_skill_definition_catalog_empty");
        }

        HashSet<string> grantIds = new(StringComparer.Ordinal);
        HashSet<string> ownerSlots = new(StringComparer.Ordinal);
        List<BattleSkillSnapshot> snapshots = new();

        foreach (BattleSkillGrantSnapshot grant in grants ?? Enumerable.Empty<BattleSkillGrantSnapshot>())
        {
            if (grant == null)
            {
                throw new InvalidOperationException("battle_skill_grant_missing");
            }

            string grantId = grant.GrantedSkillId?.Trim() ?? "";
            string loadoutSlotId = grant.LoadoutSlotId?.Trim() ?? "";
            string ownerHeroId = grant.OwnerHeroId?.Trim() ?? "";
            string ownerBattleGroupId = grant.OwnerBattleGroupId?.Trim() ?? "";
            string skillDefinitionId = grant.SkillDefinitionId?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(grantId))
            {
                throw new InvalidOperationException("battle_skill_grant_id_missing");
            }

            if (!grantIds.Add(grantId))
            {
                throw new InvalidOperationException($"battle_skill_grant_duplicate grant={grantId}");
            }

            if (string.IsNullOrWhiteSpace(loadoutSlotId))
            {
                throw new InvalidOperationException($"battle_skill_loadout_slot_missing grant={grantId}");
            }

            string ownerSlotKey = BuildOwnerSlotKey(ownerHeroId, ownerBattleGroupId, loadoutSlotId);
            if (!ownerSlots.Add(ownerSlotKey))
            {
                throw new InvalidOperationException($"battle_skill_loadout_slot_duplicate owner={ResolveDiagnosticOwner(ownerHeroId, ownerBattleGroupId)} slot={loadoutSlotId}");
            }

            if (!groupsById.TryGetValue(ownerBattleGroupId, out BattleGroupSnapshot owner))
            {
                throw new InvalidOperationException($"battle_skill_owner_missing owner={ownerBattleGroupId} grant={grantId}");
            }

            if (!templatesById.TryGetValue(skillDefinitionId, out BattleSkillSnapshot template))
            {
                throw new InvalidOperationException($"battle_skill_definition_missing skill={skillDefinitionId} grant={grantId}");
            }

            snapshots.Add(CompileTemplate(template, grant, owner));
        }

        return snapshots;
    }

    public IReadOnlyList<BattleSkillSnapshot> Compile(
        IReadOnlyDictionary<string, BattleSkillDefinitionResource> definitionsById,
        IEnumerable<BattleGroupSnapshot> participatingGroups,
        IEnumerable<BattleSkillGrantSnapshot> grants)
    {
        Dictionary<string, BattleGroupSnapshot> groupsById = BuildGroupLookup(participatingGroups);

        ValidateDuplicateDefinitions(definitionsById);

        HashSet<string> grantIds = new(StringComparer.Ordinal);
        HashSet<string> ownerSlots = new(StringComparer.Ordinal);
        List<BattleSkillSnapshot> snapshots = new();

        foreach (BattleSkillGrantSnapshot grant in grants ?? Enumerable.Empty<BattleSkillGrantSnapshot>())
        {
            if (grant == null)
            {
                throw new InvalidOperationException("battle_skill_grant_missing");
            }

            string grantId = grant.GrantedSkillId?.Trim() ?? "";
            string loadoutSlotId = grant.LoadoutSlotId?.Trim() ?? "";
            string ownerHeroId = grant.OwnerHeroId?.Trim() ?? "";
            string ownerBattleGroupId = grant.OwnerBattleGroupId?.Trim() ?? "";
            string skillDefinitionId = grant.SkillDefinitionId?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(grantId))
            {
                throw new InvalidOperationException("battle_skill_grant_id_missing");
            }

            if (!grantIds.Add(grantId))
            {
                throw new InvalidOperationException($"battle_skill_grant_duplicate grant={grantId}");
            }

            if (string.IsNullOrWhiteSpace(loadoutSlotId))
            {
                throw new InvalidOperationException($"battle_skill_loadout_slot_missing grant={grantId}");
            }

            string ownerSlotKey = BuildOwnerSlotKey(ownerHeroId, ownerBattleGroupId, loadoutSlotId);
            if (!ownerSlots.Add(ownerSlotKey))
            {
                throw new InvalidOperationException($"battle_skill_loadout_slot_duplicate owner={ResolveDiagnosticOwner(ownerHeroId, ownerBattleGroupId)} slot={loadoutSlotId}");
            }

            if (!groupsById.TryGetValue(ownerBattleGroupId, out BattleGroupSnapshot owner))
            {
                throw new InvalidOperationException($"battle_skill_owner_missing owner={ownerBattleGroupId} grant={grantId}");
            }

            if (definitionsById == null ||
                !definitionsById.TryGetValue(skillDefinitionId, out BattleSkillDefinitionResource definition))
            {
                throw new InvalidOperationException($"battle_skill_definition_missing skill={skillDefinitionId} grant={grantId}");
            }

            snapshots.Add(CompileOne(definition, grant, owner));
        }

        return snapshots;
    }

    private static Dictionary<string, BattleGroupSnapshot> BuildGroupLookup(IEnumerable<BattleGroupSnapshot> participatingGroups)
    {
        Dictionary<string, BattleGroupSnapshot> groupsById = new(StringComparer.Ordinal);
        foreach (BattleGroupSnapshot group in participatingGroups ?? Enumerable.Empty<BattleGroupSnapshot>())
        {
            if (group == null)
            {
                continue;
            }

            AddGroupLookup(groupsById, group.BattleGroupId, group);
            AddGroupLookup(groupsById, group.RuntimeCommanderGroupId, group);
            AddGroupLookup(groupsById, BattleCommanderGroupIdentity.Resolve(group), group);
        }

        return groupsById;
    }

    private static void AddGroupLookup(
        Dictionary<string, BattleGroupSnapshot> groupsById,
        string groupId,
        BattleGroupSnapshot group)
    {
        string normalized = groupId?.Trim() ?? "";
        if (!string.IsNullOrWhiteSpace(normalized) && !groupsById.ContainsKey(normalized))
        {
            groupsById[normalized] = group;
        }
    }

    private static BattleSkillSnapshot CompileTemplate(
        BattleSkillSnapshot template,
        BattleSkillGrantSnapshot grant,
        BattleGroupSnapshot owner)
    {
        BattleSkillSnapshot snapshot = CloneTemplate(template);
        snapshot.SkillDefinitionId = template.SkillDefinitionId ?? "";
        snapshot.GrantedSkillId = grant.GrantedSkillId ?? "";
        snapshot.LoadoutSlotId = grant.LoadoutSlotId ?? "";
        snapshot.OwnerHeroId = string.IsNullOrWhiteSpace(grant.OwnerHeroId)
            ? owner?.HeroId ?? ""
            : grant.OwnerHeroId;
        snapshot.OwnerBattleGroupId = grant.OwnerBattleGroupId ?? "";
        snapshot.RuntimeCommanderGroupId = string.IsNullOrWhiteSpace(grant.RuntimeCommanderGroupId)
            ? BattleCommanderGroupIdentity.Resolve(owner)
            : grant.RuntimeCommanderGroupId;
        return snapshot;
    }

    private static BattleSkillSnapshot CloneTemplate(BattleSkillSnapshot template)
    {
        BattleSkillSnapshot clone = new()
        {
            SkillDefinitionId = template.SkillDefinitionId ?? "",
            OwnerHeroId = template.OwnerHeroId ?? "",
            DisplayName = template.DisplayName ?? "",
            IconText = template.IconText ?? "",
            Tags = (template.Tags ?? new List<string>()).ToList(),
            CommandChannel = template.CommandChannel,
            SkillType = template.SkillType,
            Targeting = CloneTargeting(template.Targeting),
            Timing = CloneTiming(template.Timing),
            InterruptPolicy = CloneInterruptPolicy(template.InterruptPolicy),
            Costs = (template.Costs ?? new List<BattleSkillCostSnapshot>()).Select(CloneCost).Where(item => item != null).ToList(),
            Cooldown = CloneCooldown(template.Cooldown),
            Presentation = ClonePresentation(template.Presentation),
            TargetingMode = template.TargetingMode,
            Range = template.Range,
            CastSeconds = template.CastSeconds,
            ImpactDelaySeconds = template.ImpactDelaySeconds,
            RecoverySeconds = template.RecoverySeconds,
            HasInterruptPolicy = template.HasInterruptPolicy,
            CanInterruptBasicAttackWindup = template.CanInterruptBasicAttackWindup,
            CanCancelBasicAttackRecovery = template.CanCancelBasicAttackRecovery,
            ReleasesWithoutOccupyingCaster = template.ReleasesWithoutOccupyingCaster
        };
        clone.Effects.AddRange((template.Effects ?? new List<BattleSkillEffectSnapshot>())
            .Select(CloneEffect)
            .Where(item => item != null));
        return clone;
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

    private static void ValidateDuplicateDefinitions(
        IReadOnlyDictionary<string, BattleSkillDefinitionResource> definitionsById)
    {
        if (definitionsById == null || definitionsById.Count == 0)
        {
            throw new InvalidOperationException("battle_skill_definition_catalog_empty");
        }

        foreach ((string id, BattleSkillDefinitionResource definition) in definitionsById)
        {
            if (string.IsNullOrWhiteSpace(id) ||
                definition == null ||
                string.IsNullOrWhiteSpace(definition.SkillDefinitionId))
            {
                throw new InvalidOperationException("battle_skill_definition_missing");
            }

            if (!string.Equals(id, definition.SkillDefinitionId.Trim(), StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"battle_skill_definition_id_mismatch id={id} authored={definition.SkillDefinitionId}");
            }
        }
    }

    private static BattleSkillSnapshot CompileOne(
        BattleSkillDefinitionResource definition,
        BattleSkillGrantSnapshot grant,
        BattleGroupSnapshot owner)
    {
        BattleSkillTargetingSnapshot targeting = CompileTargeting(definition.Targeting);
        BattleSkillTimingSnapshot timing = CompileTiming(definition.Timing);
        BattleSkillInterruptPolicySnapshot interruptPolicy = CompileInterruptPolicy(definition.InterruptPolicy);
        BattleSkillPresentationSnapshot presentation = CompilePresentation(definition.Presentation);
        List<BattleSkillCostSnapshot> costs = CompileCosts(definition.CostRules);
        BattleSkillCooldownSnapshot cooldown = CompileCooldown(definition.CooldownRules);
        List<BattleSkillEffectSnapshot> effects = CompileEffects(definition.Effects, presentation.ProfileId);

        string skillDefinitionId = definition.SkillDefinitionId?.Trim() ?? "";
        BattleSkillSnapshot snapshot = new()
        {
            SkillDefinitionId = skillDefinitionId,
            GrantedSkillId = grant.GrantedSkillId ?? "",
            LoadoutSlotId = grant.LoadoutSlotId ?? "",
            OwnerHeroId = string.IsNullOrWhiteSpace(grant.OwnerHeroId)
                ? owner?.HeroId ?? ""
                : grant.OwnerHeroId,
            OwnerBattleGroupId = grant.OwnerBattleGroupId ?? "",
            RuntimeCommanderGroupId = string.IsNullOrWhiteSpace(grant.RuntimeCommanderGroupId)
                ? BattleCommanderGroupIdentity.Resolve(owner)
                : grant.RuntimeCommanderGroupId,
            DisplayName = definition.DisplayName ?? "",
            IconText = definition.IconText ?? "",
            Tags = (definition.Tags ?? new Godot.Collections.Array<string>())
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            CommandChannel = Map(definition.CommandChannel),
            SkillType = Map(definition.SkillType),
            Targeting = targeting,
            Timing = timing,
            InterruptPolicy = interruptPolicy,
            Costs = costs,
            Cooldown = cooldown,
            Presentation = presentation,
            TargetingMode = MapLegacyTargeting(targeting.InputFlow, targeting.TargetKind),
            Range = targeting.Range,
            CastSeconds = timing.CastSeconds,
            ImpactDelaySeconds = timing.ImpactDelaySeconds,
            RecoverySeconds = timing.RecoverySeconds,
            HasInterruptPolicy = true,
            CanInterruptBasicAttackWindup = interruptPolicy.CanInterruptBasicAttackWindup,
            CanCancelBasicAttackRecovery = interruptPolicy.CanCancelBasicAttackRecovery,
            ReleasesWithoutOccupyingCaster = interruptPolicy.ReleasesWithoutOccupyingCaster
        };

        snapshot.Effects.AddRange(effects);
        return snapshot;
    }

    private static BattleSkillTargetingSnapshot CompileTargeting(BattleSkillTargetingProfileResource resource)
    {
        BattleSkillTargetingSnapshot targeting = new()
        {
            InputFlow = Map(resource?.InputFlow ?? BattleSkillInputFlowDefinition.SelectActor),
            TargetKind = Map(resource?.TargetKind ?? BattleSkillTargetKindDefinition.Actor),
            Range = Math.Max(0, resource?.Range ?? 0),
            RangeMetric = Map(resource?.RangeMetric ?? BattleSkillRangeMetricDefinition.Manhattan),
            AreaShape = Map(resource?.AreaShape ?? BattleSkillAreaShapeDefinition.SingleActor),
            AreaRadius = Math.Max(0, resource?.AreaRadius ?? 0),
            DirectionMode = Map(resource?.DirectionMode ?? BattleSkillDirectionModeDefinition.None),
            RequiresSelectedMark = resource?.RequiresSelectedMark ?? false,
            RequiredMarkKind = Map(resource?.RequiredMarkKind ?? BattleSkillMarkKindDefinition.None),
            LandingRadius = Math.Max(0, resource?.LandingRadius ?? 0),
            PreviewProfileId = resource?.PreviewProfileId ?? ""
        };

        if (targeting.RequiresSelectedMark && targeting.RequiredMarkKind == BattleSkillMarkKind.None)
        {
            throw new InvalidOperationException("battle_skill_targeting_invalid requires_selected_mark_without_kind");
        }

        return targeting;
    }

    private static BattleSkillTimingSnapshot CompileTiming(BattleSkillTimingResource resource)
    {
        return new BattleSkillTimingSnapshot
        {
            CastSeconds = Math.Max(0, resource?.CastSeconds ?? 0),
            ImpactDelaySeconds = Math.Max(0, resource?.ImpactDelaySeconds ?? 0),
            RecoverySeconds = Math.Max(0, resource?.RecoverySeconds ?? 0)
        };
    }

    private static BattleSkillInterruptPolicySnapshot CompileInterruptPolicy(BattleSkillInterruptPolicyResource resource)
    {
        return new BattleSkillInterruptPolicySnapshot
        {
            CanInterruptBasicAttackWindup = resource?.CanInterruptBasicAttackWindup ?? false,
            CanCancelBasicAttackRecovery = resource?.CanCancelBasicAttackRecovery ?? false,
            ReleasesWithoutOccupyingCaster = resource?.ReleasesWithoutOccupyingCaster ?? false,
            CanInterruptActiveChannel = resource?.CanInterruptActiveChannel ?? false
        };
    }

    private static BattleSkillPresentationSnapshot CompilePresentation(BattleSkillPresentationProfileResource resource)
    {
        return new BattleSkillPresentationSnapshot
        {
            ProfileId = resource?.ProfileId ?? "",
            CastFxProfileId = resource?.CastFxProfileId ?? "",
            ImpactFxProfileId = resource?.ImpactFxProfileId ?? "",
            MarkFxProfileId = resource?.MarkFxProfileId ?? "",
            AreaFxProfileId = resource?.AreaFxProfileId ?? "",
            SuppressActorCastFx = resource?.SuppressActorCastFx ?? false,
            HoldCastAnimationDuringAction = resource?.HoldCastAnimationDuringAction ?? false
        };
    }

    private static List<BattleSkillCostSnapshot> CompileCosts(
        IEnumerable<BattleSkillCostRuleResource> resources)
    {
        List<BattleSkillCostSnapshot> costs = new();
        foreach (BattleSkillCostRuleResource resource in resources ?? Enumerable.Empty<BattleSkillCostRuleResource>())
        {
            costs.Add(resource switch
            {
                NoCostSkillCostRuleResource => new NoCostSkillCostSnapshot(),
                ManaCostSkillCostRuleResource mana => new ManaCostSkillCostSnapshot
                {
                    PoolId = mana.PoolId ?? "",
                    Amount = Math.Max(0, mana.Amount),
                    PayTiming = Map(mana.PayTiming),
                    RefundPolicy = Map(mana.RefundPolicy)
                },
                LimitedUseSkillCostRuleResource limited => new LimitedUseSkillCostSnapshot
                {
                    MaxUses = Math.Max(0, limited.MaxUses),
                    ConsumeTiming = Map(limited.ConsumeTiming),
                    RefundPolicy = Map(limited.RefundPolicy)
                },
                _ => throw new InvalidOperationException($"battle_skill_cost_resource_unsupported type={resource?.GetType().Name ?? "null"}")
            });
        }

        if (costs.Count == 0)
        {
            costs.Add(new NoCostSkillCostSnapshot());
        }

        return costs;
    }

    private static BattleSkillCooldownSnapshot CompileCooldown(
        IEnumerable<BattleSkillCooldownRuleResource> resources)
    {
        BattleSkillCooldownSnapshot cooldown = null;
        foreach (BattleSkillCooldownRuleResource resource in resources ?? Enumerable.Empty<BattleSkillCooldownRuleResource>())
        {
            if (cooldown != null)
            {
                throw new InvalidOperationException("battle_skill_cooldown_duplicate");
            }

            cooldown = resource switch
            {
                NoCooldownSkillCooldownRuleResource => new NoCooldownSkillCooldownSnapshot(),
                PerGrantCooldownRuleResource perGrant => new PerGrantCooldownSkillCooldownSnapshot
                {
                    DurationSeconds = Math.Max(0, perGrant.DurationSeconds),
                    StartsOn = Map(perGrant.StartsOn),
                    SharedCooldownGroupId = perGrant.SharedCooldownGroupId ?? ""
                },
                ChargeCooldownRuleResource charge => new ChargeCooldownSkillCooldownSnapshot
                {
                    MaxCharges = Math.Max(0, charge.MaxCharges),
                    RechargeSeconds = Math.Max(0, charge.RechargeSeconds),
                    StartsFull = charge.StartsFull
                },
                _ => throw new InvalidOperationException($"battle_skill_cooldown_resource_unsupported type={resource?.GetType().Name ?? "null"}")
            };
        }

        return cooldown ?? new NoCooldownSkillCooldownSnapshot();
    }

    private static List<BattleSkillEffectSnapshot> CompileEffects(
        IEnumerable<BattleSkillEffectResource> resources,
        string presentationProfileId)
    {
        List<BattleSkillEffectSnapshot> effects = new();
        foreach (BattleSkillEffectResource resource in resources ?? Enumerable.Empty<BattleSkillEffectResource>())
        {
            BattleSkillEffectSnapshot effect = resource switch
            {
                DamageSkillEffectResource damage => new DamageSkillEffectSnapshot
                {
                    BaseDamage = ValidatePositive(damage.BaseDamage, "battle_skill_effect_payload_invalid damage"),
                    DamageType = Map(damage.DamageType),
                    CanHitActors = damage.CanHitActors,
                    CanHitWorldObjects = damage.CanHitWorldObjects
                },
                CreateMarkSkillEffectResource mark => new CreateMarkSkillEffectSnapshot
                {
                    MarkKind = Map(mark.MarkKind),
                    LifetimeSeconds = ValidatePositive(mark.LifetimeSeconds, "battle_skill_effect_payload_invalid mark_lifetime"),
                    AttachToActorWhenTargeted = mark.AttachToActorWhenTargeted,
                    ReplaceExistingOwnedMark = mark.ReplaceExistingOwnedMark,
                    EffectInstancePolicy = BattleSkillEffectInstancePolicy.RuntimeInstance
                },
                TeleportToMarkSkillEffectResource teleport => new TeleportToMarkSkillEffectSnapshot
                {
                    RequiredMarkKind = Map(teleport.RequiredMarkKind),
                    LandingRadius = ValidatePositive(teleport.LandingRadius, "battle_skill_effect_payload_invalid landing_radius"),
                    ConsumesMark = teleport.ConsumesMark
                },
                ChanneledAreaDamageSkillEffectResource channel => new ChanneledAreaDamageSkillEffectSnapshot
                {
                    BaseDamage = ValidatePositive(channel.BaseDamage, "battle_skill_effect_payload_invalid channel_damage"),
                    DamageType = Map(channel.DamageType),
                    DurationSeconds = ValidatePositive(channel.DurationSeconds, "battle_skill_effect_payload_invalid channel_duration"),
                    TickIntervalSeconds = ValidatePositive(channel.TickIntervalSeconds, "battle_skill_effect_payload_invalid channel_interval"),
                    AreaShape = Map(channel.AreaShape),
                    Radius = ValidatePositive(channel.Radius, "battle_skill_effect_payload_invalid channel_radius"),
                    FollowsCaster = channel.FollowsCaster,
                    UsesTargetOffset = channel.UsesTargetOffset,
                    EffectInstancePolicy = BattleSkillEffectInstancePolicy.RuntimeInstance
                },
                _ => throw new InvalidOperationException($"battle_skill_effect_resource_unsupported type={resource?.GetType().Name ?? "null"}")
            };
            effect.PresentationProfileId = presentationProfileId ?? "";
            effects.Add(effect);
        }

        if (effects.Count == 0)
        {
            throw new InvalidOperationException("battle_skill_effects_missing");
        }

        return effects;
    }

    private static int ValidatePositive(int value, string reason)
    {
        if (value <= 0)
        {
            throw new InvalidOperationException(reason);
        }

        return value;
    }

    private static double ValidatePositive(double value, string reason)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
        {
            throw new InvalidOperationException(reason);
        }

        return value;
    }

    private static BattleSkillCommandChannel Map(BattleSkillCommandChannelDefinition value) => value switch
    {
        BattleSkillCommandChannelDefinition.Corps => BattleSkillCommandChannel.Corps,
        BattleSkillCommandChannelDefinition.Combined => BattleSkillCommandChannel.Combined,
        _ => BattleSkillCommandChannel.Hero
    };

    private static BattleSkillType Map(BattleSkillTypeDefinition value) => value switch
    {
        BattleSkillTypeDefinition.Passive => BattleSkillType.Passive,
        BattleSkillTypeDefinition.Toggle => BattleSkillType.Toggle,
        _ => BattleSkillType.Active
    };

    private static BattleSkillInputFlow Map(BattleSkillInputFlowDefinition value) => value switch
    {
        BattleSkillInputFlowDefinition.ImmediateSelf => BattleSkillInputFlow.ImmediateSelf,
        BattleSkillInputFlowDefinition.SelectCell => BattleSkillInputFlow.SelectCell,
        BattleSkillInputFlowDefinition.SelectActorOrCell => BattleSkillInputFlow.SelectActorOrCell,
        BattleSkillInputFlowDefinition.SelectMarkThenLandingCell => BattleSkillInputFlow.SelectMarkThenLandingCell,
        BattleSkillInputFlowDefinition.SelectDirectionArea => BattleSkillInputFlow.SelectDirectionArea,
        _ => BattleSkillInputFlow.SelectActor
    };

    private static BattleSkillTargetKind Map(BattleSkillTargetKindDefinition value) => value switch
    {
        BattleSkillTargetKindDefinition.None => BattleSkillTargetKind.None,
        BattleSkillTargetKindDefinition.Cell => BattleSkillTargetKind.Cell,
        BattleSkillTargetKindDefinition.ActorOrCell => BattleSkillTargetKind.ActorOrCell,
        BattleSkillTargetKindDefinition.Direction => BattleSkillTargetKind.Direction,
        BattleSkillTargetKindDefinition.Mark => BattleSkillTargetKind.Mark,
        _ => BattleSkillTargetKind.Actor
    };

    private static BattleSkillRangeMetric Map(BattleSkillRangeMetricDefinition value) => value switch
    {
        BattleSkillRangeMetricDefinition.Chebyshev => BattleSkillRangeMetric.Chebyshev,
        BattleSkillRangeMetricDefinition.Euclidean => BattleSkillRangeMetric.Euclidean,
        _ => BattleSkillRangeMetric.Manhattan
    };

    private static BattleSkillAreaShape Map(BattleSkillAreaShapeDefinition value) => value switch
    {
        BattleSkillAreaShapeDefinition.SingleCell => BattleSkillAreaShape.SingleCell,
        BattleSkillAreaShapeDefinition.Line => BattleSkillAreaShape.Line,
        BattleSkillAreaShapeDefinition.Cone => BattleSkillAreaShape.Cone,
        BattleSkillAreaShapeDefinition.CircleRadius => BattleSkillAreaShape.CircleRadius,
        BattleSkillAreaShapeDefinition.GridRadius => BattleSkillAreaShape.GridRadius,
        _ => BattleSkillAreaShape.SingleActor
    };

    private static BattleSkillDirectionMode Map(BattleSkillDirectionModeDefinition value) => value switch
    {
        BattleSkillDirectionModeDefinition.FreeAngle => BattleSkillDirectionMode.FreeAngle,
        BattleSkillDirectionModeDefinition.EightWay => BattleSkillDirectionMode.EightWay,
        BattleSkillDirectionModeDefinition.FourWay => BattleSkillDirectionMode.FourWay,
        BattleSkillDirectionModeDefinition.ForwardArc => BattleSkillDirectionMode.ForwardArc,
        _ => BattleSkillDirectionMode.None
    };

    private static BattleSkillCostPayTiming Map(BattleSkillCostPayTimingDefinition value) => value switch
    {
        BattleSkillCostPayTimingDefinition.CastStart => BattleSkillCostPayTiming.CastStart,
        BattleSkillCostPayTimingDefinition.EffectRelease => BattleSkillCostPayTiming.EffectRelease,
        BattleSkillCostPayTimingDefinition.SuccessfulCompletion => BattleSkillCostPayTiming.SuccessfulCompletion,
        _ => BattleSkillCostPayTiming.CommandAccepted
    };

    private static BattleSkillRefundPolicy Map(BattleSkillRefundPolicyDefinition value) => value switch
    {
        BattleSkillRefundPolicyDefinition.Never => BattleSkillRefundPolicy.Never,
        BattleSkillRefundPolicyDefinition.InterruptedBeforeRelease => BattleSkillRefundPolicy.InterruptedBeforeRelease,
        BattleSkillRefundPolicyDefinition.InvalidatedBeforeRelease => BattleSkillRefundPolicy.InvalidatedBeforeRelease,
        _ => BattleSkillRefundPolicy.FailedBeforeRelease
    };

    private static BattleSkillCooldownStart Map(BattleSkillCooldownStartDefinition value) => value switch
    {
        BattleSkillCooldownStartDefinition.CastStart => BattleSkillCooldownStart.CastStart,
        BattleSkillCooldownStartDefinition.EffectRelease => BattleSkillCooldownStart.EffectRelease,
        BattleSkillCooldownStartDefinition.SuccessfulCompletion => BattleSkillCooldownStart.SuccessfulCompletion,
        _ => BattleSkillCooldownStart.CommandAccepted
    };

    private static BattleSkillDamageType Map(BattleSkillDamageTypeDefinition value) => value switch
    {
        BattleSkillDamageTypeDefinition.Lightning => BattleSkillDamageType.Lightning,
        BattleSkillDamageTypeDefinition.Fire => BattleSkillDamageType.Fire,
        BattleSkillDamageTypeDefinition.Ice => BattleSkillDamageType.Ice,
        BattleSkillDamageTypeDefinition.Arcane => BattleSkillDamageType.Arcane,
        _ => BattleSkillDamageType.Physical
    };

    private static BattleSkillMarkKind Map(BattleSkillMarkKindDefinition value) => value switch
    {
        BattleSkillMarkKindDefinition.ThunderMark => BattleSkillMarkKind.ThunderMark,
        _ => BattleSkillMarkKind.None
    };

    private static BattleSkillTargetingMode MapLegacyTargeting(
        BattleSkillInputFlow inputFlow,
        BattleSkillTargetKind targetKind)
    {
        if (inputFlow == BattleSkillInputFlow.SelectActorOrCell ||
            targetKind == BattleSkillTargetKind.ActorOrCell)
        {
            return BattleSkillTargetingMode.TargetedActorOrCell;
        }

        if (inputFlow is BattleSkillInputFlow.SelectCell or BattleSkillInputFlow.SelectMarkThenLandingCell or BattleSkillInputFlow.SelectDirectionArea ||
            targetKind is BattleSkillTargetKind.Cell or BattleSkillTargetKind.Direction or BattleSkillTargetKind.Mark)
        {
            return BattleSkillTargetingMode.TargetedCell;
        }

        return targetKind == BattleSkillTargetKind.Actor
            ? BattleSkillTargetingMode.TargetedActor
            : BattleSkillTargetingMode.None;
    }

    private static string BuildOwnerSlotKey(string ownerHeroId, string ownerBattleGroupId, string loadoutSlotId)
    {
        string owner = !string.IsNullOrWhiteSpace(ownerHeroId)
            ? $"hero:{ownerHeroId.Trim()}"
            : $"group:{ownerBattleGroupId?.Trim() ?? ""}";
        return $"{owner}:{loadoutSlotId?.Trim() ?? ""}";
    }

    private static string ResolveDiagnosticOwner(string ownerHeroId, string ownerBattleGroupId)
    {
        return !string.IsNullOrWhiteSpace(ownerHeroId)
            ? ownerHeroId
            : ownerBattleGroupId ?? "";
    }
}
