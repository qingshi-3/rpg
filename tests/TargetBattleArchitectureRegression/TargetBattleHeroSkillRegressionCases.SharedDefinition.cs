using System;
using System.Collections.Generic;
using System.Linq;
using Rpg.Application.Battle;
using Rpg.Application.Battle.Commands;
using Rpg.Application.Battle.Snapshots;
using Rpg.Runtime.Battle;
using Rpg.Runtime.Battle.Events;

internal static partial class TargetBattleHeroSkillRegressionCases
{
    internal static void RuntimeSharedSkillDefinitionResolvesByHeroOwnerNotBattleGroup()
    {
        const string sharedSkillDefinitionId = "skill_shared_thunder_spiral";
        BattleStartSnapshot snapshot = BuildThreeHeroSharedSkillSnapshot(sharedSkillDefinitionId);
        BattleRuntimeSessionController controller = new BattleRuntimeSession().Begin(snapshot);
        BattleRuntimeActor secondCaster = controller.State.Actors.Single(item => item.ActorId == "force_second:1");

        AssertEqual(
            "hero_second",
            GetRequiredStringProperty(secondCaster, "SourceHeroId"),
            "runtime caster should retain stable hero identity independent of battle group context");

        BattleRuntimeCommandSubmitResult submit = controller.SubmitCommand(new CommandRequest
        {
            CommandId = "cmd_second_hero_shared_skill",
            BattleId = "battle_shared_skill_hero_owner",
            BattleGroupId = secondCaster.BattleGroupId,
            SourceActorId = secondCaster.ActorId,
            Channel = CommandChannel.Hero,
            Kind = CommandKind.CastSkill,
            SkillDefinitionId = sharedSkillDefinitionId,
            TargetActorId = EnemyActorId
        });

        AssertTrue(
            submit.Accepted,
            $"second hero should submit a shared skill definition through the hero-owned grant reason={submit.ReasonCode}");
        AssertTrue(
            submit.Events.Any(item =>
                item.Kind == BattleEventKind.CommandAccepted &&
                item.SourceCommandId == "cmd_second_hero_shared_skill" &&
                item.ActorId == secondCaster.ActorId &&
                item.SourceDefinitionId == sharedSkillDefinitionId),
            "accepted shared-definition command should keep caster and definition attribution");
    }

    private static BattleStartSnapshot BuildThreeHeroSharedSkillSnapshot(string skillDefinitionId)
    {
        BattleStartSnapshot snapshot = new()
        {
            SnapshotId = "snapshot_battle_shared_skill_hero_owner",
            BattleId = "battle_shared_skill_hero_owner",
            TargetLocationId = "site_1",
            BattleGroups =
            {
                BuildPlayerGroup("group_first", "force_first", "hero_first", "corps_first", 0),
                BuildPlayerGroup("group_second", "force_second", "hero_second", "corps_second", 2),
                BuildPlayerGroup("group_third", "force_third", "hero_third", "corps_third", 4),
                new BattleGroupSnapshot
                {
                    BattleGroupId = "group_enemy",
                    FactionId = "enemy",
                    SourceForceId = "force_enemy",
                    HeroId = "hero_enemy",
                    HeroDefinitionId = "hero_def_enemy",
                    CorpsId = "corps_enemy",
                    CorpsDefinitionId = "enemy_corps",
                    CorpsStrength = 40,
                    SourceLocationId = "site_1",
                    CellX = 6,
                    CellY = 0
                }
            }
        };

        AddHeroOwnedSharedSkill(snapshot, skillDefinitionId, "hero_first", "stale_group_first", "grant_first");
        AddHeroOwnedSharedSkill(snapshot, skillDefinitionId, "hero_second", "stale_group_second", "grant_second");
        AddHeroOwnedSharedSkill(snapshot, skillDefinitionId, "hero_third", "stale_group_third", "grant_third");
        TargetBattleTestTopology.CompileRect(snapshot, 0, 0, 8, 0);
        return snapshot;
    }

    private static BattleGroupSnapshot BuildPlayerGroup(
        string groupId,
        string sourceForceId,
        string heroId,
        string corpsId,
        int cellX)
    {
        return new BattleGroupSnapshot
        {
            BattleGroupId = groupId,
            FactionId = "player",
            SourceForceId = sourceForceId,
            HeroId = heroId,
            HeroDefinitionId = $"{heroId}_def",
            CorpsId = corpsId,
            CorpsDefinitionId = $"{corpsId}_def",
            CorpsStrength = 100,
            SourceLocationId = "city_player",
            CellX = cellX,
            CellY = 0
        };
    }

    private static void AddHeroOwnedSharedSkill(
        BattleStartSnapshot snapshot,
        string skillDefinitionId,
        string ownerHeroId,
        string staleBattleGroupId,
        string grantId)
    {
        BattleSkillSnapshot skill = new()
        {
            SkillDefinitionId = skillDefinitionId,
            GrantedSkillId = grantId,
            LoadoutSlotId = "shared_demo",
            OwnerBattleGroupId = staleBattleGroupId,
            RuntimeCommanderGroupId = staleBattleGroupId,
            DisplayName = "Shared Thunder",
            TargetingMode = BattleSkillTargetingMode.TargetedActor,
            Range = 20,
            CastSeconds = 0,
            ImpactDelaySeconds = 0,
            RecoverySeconds = 0,
            HasInterruptPolicy = true,
            CanInterruptBasicAttackWindup = true,
            CanCancelBasicAttackRecovery = false,
            Effects =
            {
                new DamageSkillEffectSnapshot
                {
                    BaseDamage = 6
                }
            }
        };
        SetRequiredStringProperty(skill, "OwnerHeroId", ownerHeroId);
        snapshot.SkillDefinitions.Add(skill);
    }

    private static string GetRequiredStringProperty(object instance, string propertyName)
    {
        if (instance == null)
        {
            throw new InvalidOperationException($"missing instance while reading {propertyName}");
        }

        System.Reflection.PropertyInfo property = instance.GetType().GetProperty(propertyName);
        AssertTrue(property != null, $"{instance.GetType().Name} should expose {propertyName} for stable hero skill ownership");
        AssertTrue(property.PropertyType == typeof(string) && property.CanRead, $"{propertyName} should be a readable string property");
        return property.GetValue(instance) as string ?? "";
    }
}
