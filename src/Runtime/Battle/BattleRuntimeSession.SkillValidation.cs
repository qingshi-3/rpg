using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle.Snapshots;

namespace Rpg.Runtime.Battle;

public sealed partial class BattleRuntimeSession
{
    private static BattleSkillSnapshotValidationFailure ValidateSkillSnapshots(
        IReadOnlyList<BattleSkillSnapshot> skillDefinitions)
    {
        var grantIds = new HashSet<string>(System.StringComparer.Ordinal);
        var ownerLoadoutSlots = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (BattleSkillSnapshot skill in skillDefinitions ?? System.Array.Empty<BattleSkillSnapshot>())
        {
            if (skill == null)
            {
                return BattleSkillSnapshotValidationFailure.Create("", "battle_skill_definition_missing");
            }

            string skillDefinitionId = ResolveSkillDefinitionId(skill);
            if (string.IsNullOrWhiteSpace(skillDefinitionId))
            {
                return BattleSkillSnapshotValidationFailure.Create("", "battle_skill_definition_id_missing");
            }

            string grantedSkillId = skill.GrantedSkillId?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(grantedSkillId) &&
                !grantIds.Add(grantedSkillId))
            {
                return BattleSkillSnapshotValidationFailure.Create(skillDefinitionId, "battle_skill_grant_duplicate");
            }

            string ownerBattleGroupId = skill.OwnerBattleGroupId?.Trim() ?? "";
            string ownerHeroId = skill.OwnerHeroId?.Trim() ?? "";
            string loadoutSlotId = skill.LoadoutSlotId?.Trim() ?? "";
            string ownerLoadoutKey = !string.IsNullOrWhiteSpace(ownerHeroId)
                ? $"hero:{ownerHeroId}:{loadoutSlotId}"
                : $"group:{ownerBattleGroupId}:{loadoutSlotId}";
            if ((!string.IsNullOrWhiteSpace(ownerHeroId) || !string.IsNullOrWhiteSpace(ownerBattleGroupId)) &&
                !string.IsNullOrWhiteSpace(loadoutSlotId) &&
                !ownerLoadoutSlots.Add(ownerLoadoutKey))
            {
                return BattleSkillSnapshotValidationFailure.Create(skillDefinitionId, "battle_skill_loadout_slot_duplicate");
            }

            if (!HasValidTargeting(skill))
            {
                return BattleSkillSnapshotValidationFailure.Create(skillDefinitionId, "battle_skill_targeting_invalid");
            }

            if (ResolveSkillRange(skill) <= 0)
            {
                return BattleSkillSnapshotValidationFailure.Create(skillDefinitionId, "battle_skill_range_invalid");
            }

            if (string.IsNullOrWhiteSpace(ownerHeroId) &&
                string.IsNullOrWhiteSpace(ownerBattleGroupId) &&
                (skill.CasterUnitIds ?? new List<string>())
                .All(unitId => string.IsNullOrWhiteSpace(unitId)))
            {
                return BattleSkillSnapshotValidationFailure.Create(skillDefinitionId, "battle_skill_caster_bindings_missing");
            }

            if (!IsNonNegativeFinite(ResolveCastSeconds(skill)) ||
                !IsNonNegativeFinite(ResolveImpactDelaySeconds(skill)) ||
                !IsNonNegativeFinite(ResolveRecoverySeconds(skill)))
            {
                return BattleSkillSnapshotValidationFailure.Create(skillDefinitionId, "battle_skill_timing_invalid");
            }

            if (!HasInterruptPolicy(skill))
            {
                return BattleSkillSnapshotValidationFailure.Create(skillDefinitionId, "battle_skill_interrupt_policy_missing");
            }

            if (skill.Effects == null || skill.Effects.Count == 0)
            {
                return BattleSkillSnapshotValidationFailure.Create(skillDefinitionId, "battle_skill_effects_missing");
            }

            foreach (BattleSkillEffectSnapshot effect in skill.Effects)
            {
                BattleSkillSnapshotValidationFailure failure = ValidateSkillEffectSnapshot(skillDefinitionId, effect);
                if (failure.HasFailure)
                {
                    return failure;
                }
            }
        }

        return BattleSkillSnapshotValidationFailure.None;
    }

    private static bool HasValidTargeting(BattleSkillSnapshot skill)
    {
        if (HasAuthoredTargetingSnapshot(skill) &&
            System.Enum.IsDefined(typeof(BattleSkillInputFlow), skill.Targeting.InputFlow) &&
            System.Enum.IsDefined(typeof(BattleSkillTargetKind), skill.Targeting.TargetKind) &&
            skill.Targeting.TargetKind != BattleSkillTargetKind.None)
        {
            return !skill.Targeting.RequiresSelectedMark ||
                skill.Targeting.RequiredMarkKind != BattleSkillMarkKind.None;
        }

        return System.Enum.IsDefined(typeof(BattleSkillTargetingMode), skill?.TargetingMode ?? BattleSkillTargetingMode.None) &&
            skill.TargetingMode != BattleSkillTargetingMode.None;
    }

    private static bool HasAuthoredTargetingSnapshot(BattleSkillSnapshot skill)
    {
        BattleSkillTargetingSnapshot targeting = skill?.Targeting;
        if (targeting == null)
        {
            return false;
        }

        // Default-constructed snapshots exist on legacy fixtures. Treat the typed
        // targeting profile as authored only when the compiler or fixture supplied
        // actual non-default traits.
        return targeting.Range > 0 ||
            targeting.AreaRadius > 0 ||
            targeting.LandingRadius > 0 ||
            targeting.RequiresSelectedMark ||
            !string.IsNullOrWhiteSpace(targeting.PreviewProfileId) ||
            targeting.InputFlow != BattleSkillInputFlow.SelectActor ||
            targeting.TargetKind != BattleSkillTargetKind.Actor ||
            targeting.AreaShape != BattleSkillAreaShape.SingleActor ||
            targeting.DirectionMode != BattleSkillDirectionMode.None;
    }

    private static int ResolveSkillRange(BattleSkillSnapshot skill)
    {
        return skill?.Targeting?.Range > 0
            ? skill.Targeting.Range
            : skill?.Range ?? 0;
    }

    private static double ResolveCastSeconds(BattleSkillSnapshot skill)
    {
        return skill?.Timing != null
            ? skill.Timing.CastSeconds
            : skill?.CastSeconds ?? 0;
    }

    private static double ResolveImpactDelaySeconds(BattleSkillSnapshot skill)
    {
        return skill?.Timing != null
            ? skill.Timing.ImpactDelaySeconds
            : skill?.ImpactDelaySeconds ?? 0;
    }

    private static double ResolveRecoverySeconds(BattleSkillSnapshot skill)
    {
        return skill?.Timing != null
            ? skill.Timing.RecoverySeconds
            : skill?.RecoverySeconds ?? 0;
    }

    private static bool HasInterruptPolicy(BattleSkillSnapshot skill)
    {
        return skill?.HasInterruptPolicy == true;
    }

    private static BattleSkillSnapshotValidationFailure ValidateSkillEffectSnapshot(
        string skillDefinitionId,
        BattleSkillEffectSnapshot effect)
    {
        if (effect == null)
        {
            return BattleSkillSnapshotValidationFailure.Create(skillDefinitionId, "battle_skill_effect_payload_missing");
        }

        if (!System.Enum.IsDefined(typeof(BattleSkillEffectSnapshotType), effect.EffectSnapshotType))
        {
            return BattleSkillSnapshotValidationFailure.Create(skillDefinitionId, "battle_skill_effect_kind_invalid");
        }

        switch (effect)
        {
            case DamageSkillEffectSnapshot damage when damage.BaseDamage <= 0:
                return BattleSkillSnapshotValidationFailure.Create(skillDefinitionId, "battle_skill_effect_damage_amount_invalid");
            case CreateMarkSkillEffectSnapshot mark when !IsPositiveFinite(mark.LifetimeSeconds):
                return BattleSkillSnapshotValidationFailure.Create(skillDefinitionId, "battle_skill_effect_payload_invalid");
            case TeleportToMarkSkillEffectSnapshot teleport when teleport.LandingRadius <= 0:
                return BattleSkillSnapshotValidationFailure.Create(skillDefinitionId, "battle_skill_effect_teleport_radius_invalid");
            case ChanneledAreaDamageSkillEffectSnapshot channel:
                if (channel.BaseDamage <= 0)
                {
                    return BattleSkillSnapshotValidationFailure.Create(skillDefinitionId, "battle_skill_effect_channel_amount_invalid");
                }

                if (!IsPositiveFinite(channel.DurationSeconds) ||
                    !IsPositiveFinite(channel.TickIntervalSeconds))
                {
                    return BattleSkillSnapshotValidationFailure.Create(skillDefinitionId, "battle_skill_effect_channel_timing_invalid");
                }

                if (channel.Radius <= 0)
                {
                    return BattleSkillSnapshotValidationFailure.Create(skillDefinitionId, "battle_skill_effect_payload_invalid");
                }

                break;
            case DamageSkillEffectSnapshot:
            case CreateMarkSkillEffectSnapshot:
            case TeleportToMarkSkillEffectSnapshot:
                break;
            default:
                return BattleSkillSnapshotValidationFailure.Create(skillDefinitionId, "battle_skill_effect_payload_invalid");
        }

        return BattleSkillSnapshotValidationFailure.None;
    }

    private static bool IsNonNegativeFinite(double value)
    {
        return !System.Double.IsNaN(value) &&
            !System.Double.IsInfinity(value) &&
            value >= 0;
    }

    private static bool IsPositiveFinite(double value)
    {
        return !System.Double.IsNaN(value) &&
            !System.Double.IsInfinity(value) &&
            value > 0;
    }
}
