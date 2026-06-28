using System.Collections.Generic;
using System.Linq;
using Rpg.Definitions.Battle.Skills;
using DefinitionEffectKind = Rpg.Definitions.Battle.Skills.BattleSkillEffectKind;
using DefinitionTargetingMode = Rpg.Definitions.Battle.Skills.BattleSkillTargetingMode;
using SnapshotEffectKind = Rpg.Application.Battle.Snapshots.BattleSkillEffectKind;
using SnapshotTargetingMode = Rpg.Application.Battle.Snapshots.BattleSkillTargetingMode;

namespace Rpg.Application.Battle.Snapshots;

public static class BattleSkillSnapshotFactory
{
    public static IReadOnlyList<BattleSkillSnapshot> CreateSelectedHeroSkillSnapshots()
    {
        return CreateSnapshots(FirstSliceBattleSkillDefinitions.CreateSelectedHeroSkills());
    }

    public static IReadOnlyList<BattleSkillSnapshot> CreateSnapshots(
        IEnumerable<BattleSkillDefinition> definitions)
    {
        return (definitions ?? Enumerable.Empty<BattleSkillDefinition>())
            .Where(definition => definition != null && !string.IsNullOrWhiteSpace(definition.SkillId))
            .Select(CreateSnapshot)
            .ToArray();
    }

    private static BattleSkillSnapshot CreateSnapshot(BattleSkillDefinition definition)
    {
        BattleSkillSnapshot snapshot = new BattleSkillSnapshot
        {
            SkillId = definition.SkillId ?? "",
            DisplayName = definition.DisplayName ?? "",
            TargetingMode = MapTargetingMode(definition.TargetingMode),
            Range = System.Math.Max(0, definition.Range),
            CasterUnitIds = (definition.CasterUnitIds ?? new List<string>())
                .Where(unitId => !string.IsNullOrWhiteSpace(unitId))
                .Select(unitId => unitId.Trim())
                .Distinct(System.StringComparer.Ordinal)
                .ToList(),
            CastSeconds = System.Math.Max(0, definition.Timing?.CastSeconds ?? 0),
            ImpactDelaySeconds = System.Math.Max(0, definition.Timing?.ImpactDelaySeconds ?? 0),
            RecoverySeconds = System.Math.Max(0, definition.Timing?.RecoverySeconds ?? 0),
            HasInterruptPolicy = definition.InterruptPolicy != null,
            CanInterruptBasicAttackWindup = definition.InterruptPolicy?.CanInterruptBasicAttackWindup ?? false,
            CanCancelBasicAttackRecovery = definition.InterruptPolicy?.CanCancelBasicAttackRecovery ?? false,
            ReleasesWithoutOccupyingCaster = definition.InterruptPolicy?.ReleasesWithoutOccupyingCaster ?? false
        };

        foreach (BattleSkillEffectDefinition effect in definition.Effects ?? Enumerable.Empty<BattleSkillEffectDefinition>())
        {
            snapshot.Effects.Add(new BattleSkillEffectSnapshot
            {
                Kind = MapEffectKind(effect.Kind),
                Amount = System.Math.Max(0, effect.Amount),
                DurationSeconds = System.Math.Max(0, effect.DurationSeconds),
                TickIntervalSeconds = System.Math.Max(0, effect.TickIntervalSeconds),
                Radius = System.Math.Max(0, effect.Radius)
            });
        }

        return snapshot;
    }

    private static SnapshotTargetingMode MapTargetingMode(DefinitionTargetingMode mode)
    {
        return mode switch
        {
            DefinitionTargetingMode.TargetedActor => SnapshotTargetingMode.TargetedActor,
            DefinitionTargetingMode.TargetedCell => SnapshotTargetingMode.TargetedCell,
            DefinitionTargetingMode.TargetedActorOrCell => SnapshotTargetingMode.TargetedActorOrCell,
            _ => SnapshotTargetingMode.None
        };
    }

    private static SnapshotEffectKind MapEffectKind(DefinitionEffectKind kind)
    {
        return kind switch
        {
            DefinitionEffectKind.CreateThunderMark => SnapshotEffectKind.CreateThunderMark,
            DefinitionEffectKind.TeleportToThunderMark => SnapshotEffectKind.TeleportToThunderMark,
            DefinitionEffectKind.StartChanneledAreaDamage => SnapshotEffectKind.StartChanneledAreaDamage,
            _ => SnapshotEffectKind.Damage
        };
    }
}
