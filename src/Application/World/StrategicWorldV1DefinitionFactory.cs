using System.Collections.Generic;
using Godot;
using Rpg.Application.Battle;
using Rpg.Definitions.World;
using Rpg.Domain.World;

namespace Rpg.Application.World;

public static class StrategicWorldV1DefinitionFactory
{
    public static StrategicWorldDefinition Create()
    {
        return new StrategicWorldDefinition
        {
            Id = StrategicWorldIds.DefinitionChapter01,
            DisplayName = "第一章：埋骨地攻防",
            StartingSiteId = StrategicWorldIds.SitePlayerCamp,
            PlayerFactionId = StrategicWorldIds.FactionPlayer,
            EnemyFactionIds = new List<string> { StrategicWorldIds.FactionUndead },
            ResourceDefinitions = CreateResources(),
            FacilityDefinitions = CreateFacilities(),
            SiteDefinitions = CreateSites(),
            ActionDefinitions = CreateActions(),
            ThreatRules = CreateThreatRules(),
            InitialResources = new List<ResourceAmountDefinition>
            {
                new(StrategicWorldIds.ResourcePopulation, 8),
                new(StrategicWorldIds.ResourceEconomy, 10),
                new(StrategicWorldIds.ResourceStone, 4)
            }
        };
    }

    private static List<ResourceDefinition> CreateResources()
    {
        return new List<ResourceDefinition>
        {
            new()
            {
                Id = StrategicWorldIds.ResourcePopulation,
                DisplayName = "人口",
                Description = "可被建筑占用，也可训练为驻军。",
                Category = ResourceCategory.Workforce,
                IsReservable = true
            },
            new()
            {
                Id = StrategicWorldIds.ResourceEconomy,
                DisplayName = "经济",
                Description = "建造、训练和战略行动的通用消耗。",
                Category = ResourceCategory.Currency
            },
            new()
            {
                Id = StrategicWorldIds.ResourceStone,
                DisplayName = "石材",
                Description = "矿场产出，防御塔和修复设施消耗。",
                Category = ResourceCategory.Material
            }
        };
    }

    private static List<FacilityDefinition> CreateFacilities()
    {
        return new List<FacilityDefinition>
        {
            new()
            {
                Id = StrategicWorldIds.FacilityBarracks,
                DisplayName = "兵营",
                Description = "训练和驻军能力。",
                FacilityType = FacilityType.Military,
                Actions = new List<string> { StrategicWorldIds.ActionTrainMilitia }
            },
            new()
            {
                Id = StrategicWorldIds.FacilityMine,
                DisplayName = "矿场",
                Description = "占用人口，每个世界步产出石材。",
                FacilityType = FacilityType.Production,
                BuildCosts = new List<ResourceAmountDefinition> { new(StrategicWorldIds.ResourceEconomy, 2) },
                RequiredSlotTags = new List<string> { "mine" },
                PassiveEffects = new List<string> { "produce_stone_2" }
            },
            new()
            {
                Id = StrategicWorldIds.FacilityDefenseTower,
                DisplayName = "防御塔",
                Description = "提高防守值，防守战提供塔支援。",
                FacilityType = FacilityType.Defense,
                BuildCosts = new List<ResourceAmountDefinition>
                {
                    new(StrategicWorldIds.ResourceStone, 4),
                    new(StrategicWorldIds.ResourceEconomy, 2)
                },
                RequiredSlotTags = new List<string> { "tower" },
                BattleModifiers = new List<BattleModifierDefinition>
                {
                    new()
                    {
                        Id = "tower_support",
                        Type = "tower_support",
                        BattleAnchorId = "bonefield_north_tower",
                        Uses = 1,
                        Values = new Dictionary<string, int> { ["defense_score"] = 3 }
                    }
                }
            }
        };
    }

    private static List<WorldSiteDefinition> CreateSites()
    {
        return new List<WorldSiteDefinition>
        {
            new()
            {
                Id = StrategicWorldIds.SitePlayerCamp,
                DisplayName = "玩家营地",
                SiteKind = WorldSiteKind.Base,
                Description = "玩家初始据点，承担基础资源、建造、训练和出征入口。",
                MapPosition = new Vector2(236, 413),
                InitialOwnerFactionId = StrategicWorldIds.FactionPlayer,
                InitialControlState = SiteControlState.PlayerHeld,
                FacilitySlots = new List<FacilitySlotDefinition>
                {
                    new()
                    {
                        SlotId = "camp_barracks_01",
                        DisplayName = "营地兵营",
                        Tags = new List<string> { "barracks", "military" },
                        AllowedFacilityIds = new List<string> { StrategicWorldIds.FacilityBarracks },
                        InitialFacilityId = StrategicWorldIds.FacilityBarracks
                    }
                },
                DefaultGarrisonZoneId = WorldSiteDeploymentService.DefaultGarrisonZoneId,
                DeploymentZones = new List<SiteDeploymentZoneDefinition>
                {
                    new()
                    {
                        ZoneId = WorldSiteDeploymentService.DefaultGarrisonZoneId,
                        DisplayName = "营地驻军区",
                        ZoneKind = SiteDeploymentZoneKind.DefaultGarrison,
                        Capacity = 6,
                        Cells = new List<Vector2I>
                        {
                            new(17, 16),
                            new(18, 16),
                            new(19, 16),
                            new(17, 17),
                            new(18, 17),
                            new(19, 17)
                        }
                    }
                },
                InitialGarrison = new List<GarrisonDefinition>
                {
                    new() { UnitTypeId = StrategicWorldIds.UnitPlayerKnight, Count = 1, Morale = 80 },
                    new() { UnitTypeId = StrategicWorldIds.UnitMilitia, Count = 1 }
                },
                EntranceDefinitions = new List<BattleEntranceDefinition>
                {
                    new() { EntranceId = "camp_gate", DisplayName = "营地出口", FactionId = StrategicWorldIds.FactionPlayer, BattleAnchorId = "camp_gate" }
                }
            },
            new()
            {
                Id = StrategicWorldIds.SiteBonefield,
                DisplayName = "埋骨地",
                SiteKind = WorldSiteKind.ResourceSite,
                Description = "第一座可争夺资源场域，能启用矿场、建造防御塔并成为墓园 Raid 目标。",
                MapPosition = new Vector2(796, 413),
                InitialOwnerFactionId = StrategicWorldIds.FactionUndead,
                InitialControlState = SiteControlState.Hostile,
                FacilitySlots = new List<FacilitySlotDefinition>
                {
                    new()
                    {
                        SlotId = "mine_slot_01",
                        DisplayName = "埋骨地采石点",
                        Tags = new List<string> { "mine", "production" },
                        AllowedFacilityIds = new List<string> { StrategicWorldIds.FacilityMine },
                        BattleAnchorId = "bonefield_core"
                    },
                    new()
                    {
                        SlotId = "tower_slot_01",
                        DisplayName = "北侧塔基",
                        Tags = new List<string> { "tower", "defense" },
                        AllowedFacilityIds = new List<string> { StrategicWorldIds.FacilityDefenseTower },
                        BattleAnchorId = "bonefield_north_tower"
                    }
                },
                DefaultGarrisonZoneId = WorldSiteDeploymentService.DefaultGarrisonZoneId,
                DeploymentZones = new List<SiteDeploymentZoneDefinition>
                {
                    new()
                    {
                        ZoneId = WorldSiteDeploymentService.DefaultGarrisonZoneId,
                        DisplayName = "埋骨地驻军区",
                        ZoneKind = SiteDeploymentZoneKind.DefaultGarrison,
                        Capacity = 4,
                        Cells = new List<Vector2I>
                        {
                            new(18, 17),
                            new(20, 21),
                            new(18, 15),
                            new(19, 15)
                        }
                    }
                },
                InitialGarrison = new List<GarrisonDefinition>
                {
                    new() { UnitTypeId = StrategicWorldIds.UnitSkeletonWarrior, Count = 1, Morale = 35 },
                    new() { UnitTypeId = StrategicWorldIds.UnitSkeletonArcher, Count = 1, Morale = 35 }
                },
                EntranceDefinitions = new List<BattleEntranceDefinition>
                {
                    new() { EntranceId = "main_entrance", DisplayName = "埋骨地入口", FactionId = StrategicWorldIds.FactionPlayer, BattleAnchorId = "bonefield_main_entrance" },
                    new() { EntranceId = "defense_post", DisplayName = "防守据点", FactionId = StrategicWorldIds.FactionPlayer, BattleAnchorId = "bonefield_defense_post", Source = "Garrison" }
                },
                Tags = new List<string> { "resource_site" }
            },
            new()
            {
                Id = StrategicWorldIds.SiteGraveyard,
                DisplayName = "墓园",
                SiteKind = WorldSiteKind.EnemySource,
                Description = "第一版敌方源头，会定期向埋骨地生成亡灵 Raid。",
                MapPosition = new Vector2(1076, 413),
                InitialOwnerFactionId = StrategicWorldIds.FactionUndead,
                InitialControlState = SiteControlState.Hostile,
                DefaultGarrisonZoneId = WorldSiteDeploymentService.DefaultGarrisonZoneId,
                DeploymentZones = new List<SiteDeploymentZoneDefinition>
                {
                    new()
                    {
                        ZoneId = WorldSiteDeploymentService.DefaultGarrisonZoneId,
                        DisplayName = "墓园守军区",
                        ZoneKind = SiteDeploymentZoneKind.DefaultGarrison,
                        Capacity = 0
                    }
                },
                Tags = new List<string> { "undead_source" }
            }
        };
    }

    private static List<WorldActionDefinition> CreateActions()
    {
        return new List<WorldActionDefinition>
        {
            new()
            {
                Id = StrategicWorldIds.ActionBuildMine,
                DisplayName = "启用矿场",
                Description = "消耗经济并占用人口，让埋骨地每个世界步产出石材。",
                Scope = WorldActionScope.Site,
                AdvancesWorldTick = true,
                Costs = new List<ResourceAmountDefinition> { new(StrategicWorldIds.ResourceEconomy, 2) },
                Conditions = new List<WorldConditionDefinition>
                {
                    new() { Kind = WorldConditionKind.SiteOwnerIs, SiteId = StrategicWorldIds.SiteBonefield, FactionId = StrategicWorldIds.FactionPlayer, FailureReasonKey = "site_not_owned" },
                    new() { Kind = WorldConditionKind.SiteControlStateIs, SiteId = StrategicWorldIds.SiteBonefield, ControlStates = new List<SiteControlState> { SiteControlState.PlayerHeld, SiteControlState.Damaged }, FailureReasonKey = "site_not_owned" },
                    new() { Kind = WorldConditionKind.HasEmptyFacilitySlot, SiteId = StrategicWorldIds.SiteBonefield, TargetId = StrategicWorldIds.FacilityMine, SlotTag = "mine", FailureReasonKey = "no_valid_facility_slot" },
                    new() { Kind = WorldConditionKind.HasAvailablePopulation, Amount = 1, FailureReasonKey = "not_enough_population" }
                },
                Effects = new List<WorldEffectDefinition>
                {
                    new() { Kind = WorldEffectKind.AddFacility, SiteId = StrategicWorldIds.SiteBonefield, FacilityId = StrategicWorldIds.FacilityMine },
                    new() { Kind = WorldEffectKind.ReserveResource, ResourceId = StrategicWorldIds.ResourcePopulation, Amount = 1, TargetId = "last_facility" }
                }
            },
            new()
            {
                Id = StrategicWorldIds.ActionBuildDefenseTower,
                DisplayName = "建造防御塔",
                Description = "提高埋骨地防守值，并在防守战提供一次塔支援。",
                Scope = WorldActionScope.Site,
                AdvancesWorldTick = true,
                Costs = new List<ResourceAmountDefinition>
                {
                    new(StrategicWorldIds.ResourceStone, 4),
                    new(StrategicWorldIds.ResourceEconomy, 2)
                },
                Conditions = new List<WorldConditionDefinition>
                {
                    new() { Kind = WorldConditionKind.SiteOwnerIs, SiteId = StrategicWorldIds.SiteBonefield, FactionId = StrategicWorldIds.FactionPlayer, FailureReasonKey = "site_not_owned" },
                    new() { Kind = WorldConditionKind.HasEmptyFacilitySlot, SiteId = StrategicWorldIds.SiteBonefield, TargetId = StrategicWorldIds.FacilityDefenseTower, SlotTag = "tower", FailureReasonKey = "no_valid_facility_slot" }
                },
                Effects = new List<WorldEffectDefinition>
                {
                    new() { Kind = WorldEffectKind.AddFacility, SiteId = StrategicWorldIds.SiteBonefield, FacilityId = StrategicWorldIds.FacilityDefenseTower }
                }
            },
            new()
            {
                Id = StrategicWorldIds.ActionTrainMilitia,
                DisplayName = "训练民兵",
                Description = "消耗人口和经济，在玩家营地增加一队民兵。",
                Scope = WorldActionScope.Site,
                AdvancesWorldTick = true,
                Costs = new List<ResourceAmountDefinition>
                {
                    new(StrategicWorldIds.ResourcePopulation, 1),
                    new(StrategicWorldIds.ResourceEconomy, 2)
                },
                Conditions = new List<WorldConditionDefinition>
                {
                    new() { Kind = WorldConditionKind.SiteOwnerIs, SiteId = StrategicWorldIds.SitePlayerCamp, FactionId = StrategicWorldIds.FactionPlayer, FailureReasonKey = "site_not_owned" },
                    new() { Kind = WorldConditionKind.HasFacility, SiteId = StrategicWorldIds.SitePlayerCamp, TargetId = StrategicWorldIds.FacilityBarracks, FacilityState = FacilityState.Active, FailureReasonKey = "missing_facility" }
                },
                Effects = new List<WorldEffectDefinition>
                {
                    new() { Kind = WorldEffectKind.AddGarrison, SiteId = StrategicWorldIds.SitePlayerCamp, UnitTypeId = StrategicWorldIds.UnitMilitia, Amount = 1 }
                }
            },
            new()
            {
                Id = StrategicWorldIds.ActionDefendRaid,
                DisplayName = "进入防守战",
                Description = "带领埋骨地驻军进入战棋防守战。",
                Scope = WorldActionScope.Threat,
                Conditions = new List<WorldConditionDefinition>
                {
                    new() { Kind = WorldConditionKind.ThreatStageIs, ThreatStage = ThreatStage.Attacking, FailureReasonKey = "threat_not_attackable" }
                },
                Effects = new List<WorldEffectDefinition>
                {
                    new() { Kind = WorldEffectKind.StartBattle, SiteId = StrategicWorldIds.SiteBonefield, BattleKind = nameof(BattleKind.DefenseRaid) }
                }
            },
            new()
            {
                Id = StrategicWorldIds.ActionAutoResolveRaid,
                DisplayName = "自动结算",
                Description = "按埋骨地驻军和防御塔自动结算本次 Raid。",
                Scope = WorldActionScope.Threat,
                Conditions = new List<WorldConditionDefinition>
                {
                    new() { Kind = WorldConditionKind.ThreatStageIs, ThreatStage = ThreatStage.Attacking, FailureReasonKey = "threat_not_attackable" }
                }
            },
            new()
            {
                Id = StrategicWorldIds.ActionWaitTick,
                DisplayName = "等待 / 整顿",
                Description = "推进一个世界步，结算产出和敌方威胁。",
                Scope = WorldActionScope.Run,
                AdvancesWorldTick = true
            }
        };
    }

    private static List<ThreatRuleDefinition> CreateThreatRules()
    {
        return new List<ThreatRuleDefinition>
        {
            new()
            {
                Id = StrategicWorldIds.ThreatRuleGraveyardRaidBonefield,
                SourceSiteId = StrategicWorldIds.SiteGraveyard,
                TargetSiteId = StrategicWorldIds.SiteBonefield,
                ThreatType = ThreatType.Raid,
                InitialCountdownTicks = 3,
                EnemyGroupId = "undead_raid_01",
                EnemyForces = new List<GarrisonDefinition>
                {
                    new() { UnitTypeId = StrategicWorldIds.UnitSkeletonWarrior, Count = 1, Morale = 30 },
                    new() { UnitTypeId = StrategicWorldIds.UnitSkeletonArcher, Count = 1, Morale = 30 }
                },
                TriggerConditions = new List<WorldConditionDefinition>
                {
                    new() { Kind = WorldConditionKind.SiteControlStateIs, SiteId = StrategicWorldIds.SiteBonefield, ControlStates = new List<SiteControlState> { SiteControlState.PlayerHeld, SiteControlState.Damaged } },
                    new() { Kind = WorldConditionKind.SiteControlStateIs, SiteId = StrategicWorldIds.SiteGraveyard, ControlState = SiteControlState.Hostile },
                    new() { Kind = WorldConditionKind.NoActiveThreatOfRule, RuleId = StrategicWorldIds.ThreatRuleGraveyardRaidBonefield }
                }
            }
        };
    }
}
